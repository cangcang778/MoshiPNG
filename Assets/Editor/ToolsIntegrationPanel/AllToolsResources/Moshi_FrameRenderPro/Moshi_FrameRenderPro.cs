using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MoshiTools
{
    /// <summary>
    /// 带后期透明逐帧渲染器
    /// Transparent frame renderer with post-processing
    /// </summary>
    public class Moshi_FrameRenderPro : EditorWindow
    {
        private const string TOOL_NAME = "透明逐帧渲染";
        private const string MENU_ROOT = "工具/Moshi/";
        private const int DIFFERENTIAL_LAYER = 31;

        private enum AlphaMode { MaskedPost, Differential, Direct }
        private enum SampleMode { Stable, Fast }
        private enum CropMode { None, TrimAlpha }

        private Moshi_FrameRenderConfig config;
        private Transform targetRoot;
        private Camera blackCamera;
        private Camera whiteCamera;
        private Camera referenceCamera;
        private PlayableDirector playableDirector;
        private int startFrame = 0;
        private int endFrame = 60;
        private int fps = 30;
        private int width = 1024;
        private int height = 1024;
        private int superSample = 1;
        private float warmupTime = 0f;
        private string outputDirectory = "Assets/Moshi_FrameOutput";
        private string filePrefix = "Frame";
        private AlphaMode alphaMode = AlphaMode.MaskedPost;
        private SampleMode sampleMode = SampleMode.Stable;
        private CropMode cropMode = CropMode.None;
        private bool isolateTargetLayer = true;
        private bool useHDRBuffer = true;
        private float glowAlphaStrength = 2f;
        private float alphaThreshold = 0.001f;
        private bool packSpriteSheet = false;
        private bool importAsSprites = true;
        private bool isRendering = false;
        private bool cancelRequested = false;
        private Vector2 scrollPos;
        private Texture2D previewTexture;
        private List<Texture2D> renderedFrames = new List<Texture2D>();
        private Stopwatch stopwatch;

        [MenuItem(MENU_ROOT + TOOL_NAME, false, 107)]
        public static void ShowWindow()
        {
            Moshi_FrameRenderPro window = GetWindow<Moshi_FrameRenderPro>(TOOL_NAME);
            window.minSize = new Vector2(500, 700);
            window.Show();
        }

        private void OnEnable()
        {
            AutoDetectConfig();
        }

        private void OnDisable()
        {
            cancelRequested = true;
            if (previewTexture != null)
            {
                DestroyImmediate(previewTexture);
                previewTexture = null;
            }
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            DrawHeader();
            DrawConfigSection();
            DrawFrameSection();
            DrawRenderSection();
            DrawOutputSection();
            DrawPreviewSection();
            DrawActions();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(TOOL_NAME, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            MoshiHelpButton.DrawHelpButton(TOOL_NAME);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("通过黑白背景双渲染差分重建透明 Alpha，保留 URP 后处理效果，输出透明 PNG 序列帧。", MessageType.Info);
        }

        private void DrawConfigSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("环境配置", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            config = (Moshi_FrameRenderConfig)EditorGUILayout.ObjectField("配置", config, typeof(Moshi_FrameRenderConfig), true);
            if (GUILayout.Button("自动", GUILayout.Width(50))) AutoDetectConfig();
            if (GUILayout.Button("读取", GUILayout.Width(50))) ApplyConfig();
            EditorGUILayout.EndHorizontal();
            targetRoot = (Transform)EditorGUILayout.ObjectField("目标根节点", targetRoot, typeof(Transform), true);
            referenceCamera = (Camera)EditorGUILayout.ObjectField("参考相机", referenceCamera, typeof(Camera), true);
            blackCamera = (Camera)EditorGUILayout.ObjectField("黑底相机", blackCamera, typeof(Camera), true);
            whiteCamera = (Camera)EditorGUILayout.ObjectField("白底相机", whiteCamera, typeof(Camera), true);
            playableDirector = (PlayableDirector)EditorGUILayout.ObjectField("Timeline", playableDirector, typeof(PlayableDirector), true);
        }

        private void DrawFrameSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("帧范围", EditorStyles.boldLabel);
            startFrame = EditorGUILayout.IntField("起始帧", startFrame);
            endFrame = EditorGUILayout.IntField("结束帧", endFrame);
            fps = EditorGUILayout.IntSlider("FPS", fps, 1, 120);
            warmupTime = EditorGUILayout.FloatField("预热秒", warmupTime);
        }

        private void DrawRenderSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("渲染设置", EditorStyles.boldLabel);
            width = EditorGUILayout.IntField("宽度", width);
            height = EditorGUILayout.IntField("高度", height);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("超采样", GUILayout.Width(60));
            if (GUILayout.Toggle(superSample == 1, "1x", EditorStyles.miniButtonLeft, GUILayout.Width(45))) superSample = 1;
            if (GUILayout.Toggle(superSample == 2, "2x", EditorStyles.miniButtonMid, GUILayout.Width(45))) superSample = 2;
            if (GUILayout.Toggle(superSample == 4, "4x", EditorStyles.miniButtonRight, GUILayout.Width(45))) superSample = 4;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("透明", GUILayout.Width(60));
            if (GUILayout.Toggle(alphaMode == AlphaMode.MaskedPost, "遮罩", EditorStyles.miniButtonLeft, GUILayout.Width(55))) alphaMode = AlphaMode.MaskedPost;
            if (GUILayout.Toggle(alphaMode == AlphaMode.Differential, "差分", EditorStyles.miniButtonMid, GUILayout.Width(55))) alphaMode = AlphaMode.Differential;
            if (GUILayout.Toggle(alphaMode == AlphaMode.Direct, "直出", EditorStyles.miniButtonRight, GUILayout.Width(55))) alphaMode = AlphaMode.Direct;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            useHDRBuffer = EditorGUILayout.ToggleLeft("使用 HDR 缓冲", useHDRBuffer);
            using (new EditorGUI.DisabledScope(alphaMode != AlphaMode.MaskedPost))
            {
                glowAlphaStrength = EditorGUILayout.Slider("辉光 Alpha", glowAlphaStrength, 0f, 5f);
                alphaThreshold = EditorGUILayout.Slider("透明阈值", alphaThreshold, 0f, 0.05f);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("采样", GUILayout.Width(60));
            if (GUILayout.Toggle(sampleMode == SampleMode.Stable, "稳定", EditorStyles.miniButtonLeft, GUILayout.Width(55))) sampleMode = SampleMode.Stable;
            if (GUILayout.Toggle(sampleMode == SampleMode.Fast, "快速", EditorStyles.miniButtonRight, GUILayout.Width(55))) sampleMode = SampleMode.Fast;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("裁切", GUILayout.Width(60));
            if (GUILayout.Toggle(cropMode == CropMode.None, "不裁", EditorStyles.miniButtonLeft, GUILayout.Width(55))) cropMode = CropMode.None;
            if (GUILayout.Toggle(cropMode == CropMode.TrimAlpha, "透明", EditorStyles.miniButtonRight, GUILayout.Width(55))) cropMode = CropMode.TrimAlpha;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            isolateTargetLayer = EditorGUILayout.ToggleLeft("隔离目标根节点", isolateTargetLayer);
        }

        private void DrawOutputSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("输出", EditorStyles.boldLabel);
            outputDirectory = EditorGUILayout.TextField("目录", outputDirectory);
            filePrefix = EditorGUILayout.TextField("前缀", filePrefix);
            packSpriteSheet = EditorGUILayout.ToggleLeft("同时输出图集", packSpriteSheet);
            importAsSprites = EditorGUILayout.ToggleLeft("导入为 Sprite", importAsSprites);
        }

        private void DrawPreviewSection()
        {
            if (previewTexture == null) return;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("预览", EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetAspectRect((float)previewTexture.width / previewTexture.height, GUILayout.MaxHeight(240));
            EditorGUI.DrawPreviewTexture(rect, previewTexture, null, ScaleMode.ScaleToFit);
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(10);
            EditorGUI.BeginDisabledGroup(isRendering);
            if (GUILayout.Button("预览起始帧", GUILayout.Height(30)))
            {
                previewTexture = RenderFrame(startFrame, false);
            }
            if (GUILayout.Button("开始渲染", GUILayout.Height(42)))
            {
                StartRenderSequence();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(!isRendering);
            if (GUILayout.Button("停止渲染", GUILayout.Height(28)))
            {
                cancelRequested = true;
            }
            EditorGUI.EndDisabledGroup();
        }

        private void AutoDetectConfig()
        {
            config = FindObjectOfType<Moshi_FrameRenderConfig>();
            ApplyConfig();
        }

        private void ApplyConfig()
        {
            if (config == null) return;
            targetRoot = config.targetRoot;
            referenceCamera = config.referenceCamera;
            blackCamera = config.blackCamera;
            whiteCamera = config.whiteCamera;
            width = config.width;
            height = config.height;
            fps = config.fps;
            outputDirectory = config.outputDirectory;
            isolateTargetLayer = config.isolateTargetLayer;
            useHDRBuffer = config.useHDRBuffer;
            glowAlphaStrength = config.glowAlphaStrength;
            alphaThreshold = config.alphaThreshold;
            if (config.useProfessionalAlpha)
            {
                alphaMode = AlphaMode.MaskedPost;
            }
            else
            {
                alphaMode = config.useDifferentialAlpha ? AlphaMode.Differential : AlphaMode.Direct;
            }
        }

        private void StartRenderSequence()
        {
            if (!ValidateRender()) return;
            isRendering = true;
            cancelRequested = false;
            stopwatch = Stopwatch.StartNew();
            renderedFrames.Clear();
            string absDir = GetAbsolutePath(outputDirectory);
            Directory.CreateDirectory(absDir);

            try
            {
                int frameCount = Mathf.Max(1, endFrame - startFrame + 1);
                for (int frame = startFrame; frame <= endFrame; frame++)
                {
                    if (cancelRequested) break;
                    float progress = (frame - startFrame) / (float)frameCount;
                    EditorUtility.DisplayProgressBar(TOOL_NAME, "渲染帧 " + frame + " / " + endFrame, progress);
                    Texture2D tex = RenderFrame(frame, true);
                    if (tex == null) continue;
                    string file = Path.Combine(absDir, filePrefix + "_" + frame.ToString("0000") + ".png");
                    File.WriteAllBytes(file, tex.EncodeToPNG());
                    if (packSpriteSheet)
                    {
                        renderedFrames.Add(tex);
                    }
                    else
                    {
                        DestroyImmediate(tex);
                    }
                }

                if (!cancelRequested && packSpriteSheet && renderedFrames.Count > 0)
                {
                    SaveSpriteSheet(absDir);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                isRendering = false;
                stopwatch.Stop();
                AssetDatabase.Refresh();
                if (importAsSprites)
                {
                    ConfigureOutputImporters(outputDirectory);
                }
                EditorUtility.DisplayDialog(TOOL_NAME, cancelRequested ? "渲染已停止" : "渲染完成", "确定");
            }
        }

        private bool ValidateRender()
        {
            if (targetRoot == null)
            {
                EditorUtility.DisplayDialog("缺少目标", "请指定目标根节点。", "确定");
                return false;
            }
            if (blackCamera == null)
            {
                EditorUtility.DisplayDialog("缺少相机", "请指定黑底相机。", "确定");
                return false;
            }
            if (alphaMode != AlphaMode.Direct && whiteCamera == null)
            {
                EditorUtility.DisplayDialog("缺少相机", "遮罩或差分模式需要白底相机。", "确定");
                return false;
            }
            width = Mathf.Max(16, width);
            height = Mathf.Max(16, height);
            endFrame = Mathf.Max(startFrame, endFrame);
            fps = Mathf.Max(1, fps);
            return true;
        }

        private Texture2D RenderFrame(int frame, bool keepTexture)
        {
            if (!ValidateRender()) return null;
            float time = frame / (float)fps;
            if (warmupTime > 0f) SampleScene(Mathf.Max(0f, time + warmupTime));
            SampleScene(time);

            Dictionary<Transform, int> layerBackup = null;
            CameraState blackState = new CameraState(blackCamera);
            CameraState whiteState = whiteCamera != null ? new CameraState(whiteCamera) : null;
            if (isolateTargetLayer)
            {
                layerBackup = BackupLayers(targetRoot);
                SetLayerRecursive(targetRoot, DIFFERENTIAL_LAYER);
                blackCamera.cullingMask = 1 << DIFFERENTIAL_LAYER;
                if (whiteCamera != null) whiteCamera.cullingMask = 1 << DIFFERENTIAL_LAYER;
            }

            Texture2D result = null;
            try
            {
                int rw = width * superSample;
                int rh = height * superSample;
                if (alphaMode == AlphaMode.Direct)
                {
                    Texture2D direct = RenderCamera(blackCamera, Color.clear, rw, rh, true);
                    result = ConvertToOutputTexture(direct);
                    DestroyImmediate(direct);
                }
                else if (alphaMode == AlphaMode.Differential)
                {
                    Texture2D black = RenderCamera(blackCamera, Color.black, rw, rh, true);
                    Texture2D white = RenderCamera(whiteCamera, Color.white, rw, rh, true);
                    result = ComposeDifferential(black, white);
                    DestroyImmediate(black);
                    DestroyImmediate(white);
                }
                else
                {
                    Texture2D color = RenderCamera(blackCamera, Color.black, rw, rh, true);
                    Texture2D maskBlack = RenderCamera(blackCamera, Color.black, rw, rh, false);
                    Texture2D maskWhite = RenderCamera(whiteCamera, Color.white, rw, rh, false);
                    result = ComposeMaskedPost(color, maskBlack, maskWhite);
                    if (IsTextureTransparent(result))
                    {
                        DestroyImmediate(result);
                        result = ComposeBrightnessAlpha(color);
                    }
                    DestroyImmediate(color);
                    DestroyImmediate(maskBlack);
                    DestroyImmediate(maskWhite);
                }
                if (superSample > 1)
                {
                    Texture2D down = Downsample(result, width, height);
                    DestroyImmediate(result);
                    result = down;
                }
                if (cropMode == CropMode.TrimAlpha)
                {
                    Texture2D cropped = CropTransparent(result);
                    DestroyImmediate(result);
                    result = cropped;
                }
                if (!keepTexture && previewTexture != null && previewTexture != result)
                {
                    DestroyImmediate(previewTexture);
                }
            }
            finally
            {
                blackState.Restore(blackCamera);
                if (whiteState != null && whiteCamera != null) whiteState.Restore(whiteCamera);
                if (layerBackup != null) RestoreLayers(layerBackup);
                if (!isRendering) StopEditorAnimationMode();
            }
            return result;
        }

        private Texture2D RenderCamera(Camera cam, Color background, int rw, int rh, bool postProcessing)
        {
            RenderTextureFormat rtFormat = useHDRBuffer ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32;
            TextureFormat texFormat = TextureFormat.RGBA32;
            RenderTexture rt = RenderTexture.GetTemporary(rw, rh, 24, rtFormat, RenderTextureReadWrite.Default);
            RenderTexture prev = RenderTexture.active;
            CameraClearFlags oldClear = cam.clearFlags;
            Color oldBg = cam.backgroundColor;
            RenderTexture oldTarget = cam.targetTexture;
            bool oldHDR = cam.allowHDR;
            UniversalAdditionalCameraData cameraData = cam.GetComponent<UniversalAdditionalCameraData>();
            bool oldPost = cameraData != null && cameraData.renderPostProcessing;
            try
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = background;
                cam.allowHDR = useHDRBuffer;
                cam.targetTexture = rt;
                if (cameraData != null) cameraData.renderPostProcessing = postProcessing;
                cam.Render();
                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(rw, rh, texFormat, false, false);
                tex.ReadPixels(new Rect(0, 0, rw, rh), 0, 0);
                tex.Apply();
                return tex;
            }
            finally
            {
                if (cameraData != null) cameraData.renderPostProcessing = oldPost;
                cam.targetTexture = oldTarget;
                cam.clearFlags = oldClear;
                cam.backgroundColor = oldBg;
                cam.allowHDR = oldHDR;
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private Texture2D ComposeDifferential(Texture2D black, Texture2D white)
        {
            Color[] b = black.GetPixels();
            Color[] w = white.GetPixels();
            Color[] o = new Color[b.Length];
            for (int i = 0; i < b.Length; i++)
            {
                float a = CalculateDifferentialAlpha(b[i], w[i]);
                if (a <= alphaThreshold)
                {
                    o[i] = Color.clear;
                }
                else
                {
                    o[i] = ToStraightColor(b[i], a);
                }
            }
            Texture2D output = new Texture2D(black.width, black.height, TextureFormat.RGBA32, false, false);
            output.SetPixels(o);
            output.Apply();
            return output;
        }

        private Texture2D ComposeMaskedPost(Texture2D color, Texture2D maskBlack, Texture2D maskWhite)
        {
            Color[] c = color.GetPixels();
            Color[] b = maskBlack.GetPixels();
            Color[] w = maskWhite.GetPixels();
            Color[] o = new Color[c.Length];
            for (int i = 0; i < c.Length; i++)
            {
                float baseAlpha = CalculateDifferentialAlpha(b[i], w[i]);
                float glowAlpha = Mathf.Clamp01(GetMaxChannel(c[i]) * glowAlphaStrength);
                float a = Mathf.Clamp01(Mathf.Max(baseAlpha, glowAlpha));
                if (a <= alphaThreshold)
                {
                    o[i] = Color.clear;
                }
                else
                {
                    o[i] = ToStraightColor(c[i], a);
                }
            }
            Texture2D output = new Texture2D(color.width, color.height, TextureFormat.RGBA32, false, false);
            output.SetPixels(o);
            output.Apply();
            return output;
        }

        private Texture2D ComposeBrightnessAlpha(Texture2D color)
        {
            Color[] c = color.GetPixels();
            Color[] o = new Color[c.Length];
            for (int i = 0; i < c.Length; i++)
            {
                float a = Mathf.Clamp01(GetMaxChannel(c[i]) * glowAlphaStrength);
                if (a <= alphaThreshold)
                {
                    o[i] = Color.clear;
                }
                else
                {
                    o[i] = ToStraightColor(c[i], a);
                }
            }
            Texture2D output = new Texture2D(color.width, color.height, TextureFormat.RGBA32, false, false);
            output.SetPixels(o);
            output.Apply();
            return output;
        }

        private float CalculateDifferentialAlpha(Color black, Color white)
        {
            float ar = 1f - (white.r - black.r);
            float ag = 1f - (white.g - black.g);
            float ab = 1f - (white.b - black.b);
            return Mathf.Clamp01(Mathf.Max(ar, Mathf.Max(ag, ab)));
        }

        private Color ToStraightColor(Color premultiplied, float alpha)
        {
            float invAlpha = 1f / Mathf.Max(alpha, 0.0001f);
            return new Color(
                Mathf.Clamp01(premultiplied.r * invAlpha),
                Mathf.Clamp01(premultiplied.g * invAlpha),
                Mathf.Clamp01(premultiplied.b * invAlpha),
                alpha);
        }

        private float GetMaxChannel(Color color)
        {
            return Mathf.Max(0f, Mathf.Max(color.r, Mathf.Max(color.g, color.b)));
        }

        private bool IsTextureTransparent(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 2) return false;
            }
            return true;
        }

        private Texture2D ConvertToOutputTexture(Texture2D source)
        {
            Color[] pixels = source.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(Mathf.Clamp01(pixels[i].r), Mathf.Clamp01(pixels[i].g), Mathf.Clamp01(pixels[i].b), Mathf.Clamp01(pixels[i].a));
            }
            Texture2D output = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
            output.SetPixels(pixels);
            output.Apply();
            return output;
        }

        private Texture2D Downsample(Texture2D source, int targetWidth, int targetHeight)
        {
            Texture2D dst = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false, false);
            int scaleX = Mathf.Max(1, source.width / targetWidth);
            int scaleY = Mathf.Max(1, source.height / targetHeight);
            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    Color sum = Color.clear;
                    float alphaSum = 0f;
                    int count = 0;
                    for (int sy = 0; sy < scaleY; sy++)
                    {
                        for (int sx = 0; sx < scaleX; sx++)
                        {
                            Color p = source.GetPixel(Mathf.Min(source.width - 1, x * scaleX + sx), Mathf.Min(source.height - 1, y * scaleY + sy));
                            sum.r += p.r * p.a;
                            sum.g += p.g * p.a;
                            sum.b += p.b * p.a;
                            alphaSum += p.a;
                            count++;
                        }
                    }
                    float a = count > 0 ? alphaSum / count : 0f;
                    if (a <= alphaThreshold)
                    {
                        dst.SetPixel(x, y, Color.clear);
                    }
                    else
                    {
                        float inv = 1f / Mathf.Max(alphaSum, 0.0001f);
                        dst.SetPixel(x, y, new Color(Mathf.Clamp01(sum.r * inv), Mathf.Clamp01(sum.g * inv), Mathf.Clamp01(sum.b * inv), a));
                    }
                }
            }
            dst.Apply();
            return dst;
        }

        private Texture2D CropTransparent(Texture2D source)
        {
            Color32[] pixels = source.GetPixels32();
            int minX = source.width;
            int minY = source.height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < source.height; y++)
            {
                for (int x = 0; x < source.width; x++)
                {
                    if (pixels[y * source.width + x].a > 2)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            if (maxX < minX || maxY < minY) return source;
            int w = maxX - minX + 1;
            int h = maxY - minY + 1;
            Texture2D cropped = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            cropped.SetPixels(source.GetPixels(minX, minY, w, h));
            cropped.Apply();
            return cropped;
        }

        private void SampleScene(float time)
        {
            if (playableDirector != null)
            {
                playableDirector.time = time;
                playableDirector.Evaluate();
            }

            ParticleSystem[] particleSystems = targetRoot.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (sampleMode == SampleMode.Stable)
                {
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Simulate(time, false, true, true);
                }
                else
                {
                    ps.Simulate(1f / fps, false, false, true);
                }
            }

            Animator[] animators = targetRoot.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator.runtimeAnimatorController == null) continue;
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                if (clips != null && clips.Length > 0)
                {
                    StartEditorAnimationMode();
                    float clipTime = clips[0].length > 0f ? time % clips[0].length : time;
                    AnimationMode.SampleAnimationClip(animator.gameObject, clips[0], clipTime);
                }
            }
            Animation[] animations = targetRoot.GetComponentsInChildren<Animation>(true);
            for (int i = 0; i < animations.Length; i++)
            {
                if (animations[i].clip != null)
                {
                    StartEditorAnimationMode();
                    float clipTime = animations[i].clip.length > 0f ? time % animations[i].clip.length : time;
                    AnimationMode.SampleAnimationClip(animations[i].gameObject, animations[i].clip, clipTime);
                }
            }
            SceneView.RepaintAll();
        }

        private void StartEditorAnimationMode()
        {
            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
            }
        }

        private void StopEditorAnimationMode()
        {
            if (AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
            }
        }

        private Dictionary<Transform, int> BackupLayers(Transform root)
        {
            Dictionary<Transform, int> dict = new Dictionary<Transform, int>();
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                dict[transforms[i]] = transforms[i].gameObject.layer;
            }
            return dict;
        }

        private void RestoreLayers(Dictionary<Transform, int> backup)
        {
            foreach (KeyValuePair<Transform, int> pair in backup)
            {
                if (pair.Key != null) pair.Key.gameObject.layer = pair.Value;
            }
        }

        private void SetLayerRecursive(Transform root, int layer)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.layer = layer;
            }
        }

        private void SaveSpriteSheet(string absDir)
        {
            int count = renderedFrames.Count;
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt(count / (float)cols);
            int frameW = renderedFrames[0].width;
            int frameH = renderedFrames[0].height;
            Texture2D sheet = new Texture2D(cols * frameW, rows * frameH, TextureFormat.RGBA32, false, false);
            Color[] clear = new Color[sheet.width * sheet.height];
            for (int i = 0; i < clear.Length; i++) clear[i] = Color.clear;
            sheet.SetPixels(clear);
            for (int i = 0; i < count; i++)
            {
                int x = (i % cols) * frameW;
                int y = (rows - 1 - i / cols) * frameH;
                sheet.SetPixels(x, y, frameW, frameH, renderedFrames[i].GetPixels());
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(absDir, filePrefix + "_Sheet.png"), sheet.EncodeToPNG());
            string json = "{\n  \"fps\": " + fps + ",\n  \"frameCount\": " + count + ",\n  \"frameWidth\": " + frameW + ",\n  \"frameHeight\": " + frameH + ",\n  \"columns\": " + cols + ",\n  \"rows\": " + rows + "\n}";
            File.WriteAllText(Path.Combine(absDir, filePrefix + "_Sheet.json"), json);
            for (int i = 0; i < renderedFrames.Count; i++) DestroyImmediate(renderedFrames[i]);
            renderedFrames.Clear();
            DestroyImmediate(sheet);
        }

        private void ConfigureOutputImporters(string dir)
        {
            AssetDatabase.Refresh();
            string projectDir = Directory.GetCurrentDirectory().Replace("\\", "/");
            string absDir = GetAbsolutePath(dir).Replace("\\", "/");
            if (!absDir.StartsWith(projectDir)) return;
            string assetDir = absDir.Substring(projectDir.Length + 1);
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new string[] { assetDir });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        private string GetAbsolutePath(string path)
        {
            if (Path.IsPathRooted(path)) return path;
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        }

        private class CameraState
        {
            private RenderTexture target;
            private CameraClearFlags clearFlags;
            private Color background;
            private int cullingMask;

            public CameraState(Camera camera)
            {
                target = camera.targetTexture;
                clearFlags = camera.clearFlags;
                background = camera.backgroundColor;
                cullingMask = camera.cullingMask;
            }

            public void Restore(Camera camera)
            {
                camera.targetTexture = target;
                camera.clearFlags = clearFlags;
                camera.backgroundColor = background;
                camera.cullingMask = cullingMask;
            }
        }
    }
}
