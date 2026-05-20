using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace MoshiTools
{
    /// <summary>
    /// Moshi 逐帧渲染器
    /// 逐帧推进动画/粒子时间轴，每帧截图输出PNG序列帧
    /// </summary>
    public class Moshi_FrameRenderer : EditorWindow
    {
        private const string TOOL_NAME = "逐帧渲染";
        private const string MENU_ROOT = "工具/Moshi/";
        private const string OUTPUT_ROOT = "Assets/Moshi_FrameOutput/";

        #region 枚举

        private enum RenderTargetMode { AutoDetect, Manual }
        private enum BackgroundMode { UseCameraSetting, ForceOpaque, ForceTransparent }

        #endregion

        #region 渲染配置字段

        private RenderTargetMode targetMode = RenderTargetMode.AutoDetect;
        private Camera targetCamera;
        private GameObject targetObject;

        // 自动检测结果
        private Moshi_RenderCameraTag detectedTag;
        private bool hasDetectedCamera;

        // 帧参数
        private int startFrame = 0;
        private int endFrame = 60;
        private int fps = 30;

        // 分辨率
        private bool useCameraResolution = true;
        private int customWidth = 1920;
        private int customHeight = 1080;

        // 超采样
        private int superSample = 1;

        // 背景覆盖
        private BackgroundMode bgOverride = BackgroundMode.UseCameraSetting;
        private Color overrideBgColor = Color.clear;

        // 输出
        private string fileNamePrefix = "Frame";
        private string outputDir = "";

        // 预览
        private int previewFrame = 0;
        private Texture2D previewTexture;
        private bool isPlaying = false;
        private double playStartTime;
        private double lastPreviewTime;

        // 引擎状态
        private bool isRendering = false;
        private int currentRenderFrame = 0;
        private int totalRenderFrames = 0;
        private Stopwatch renderStopwatch;
        private bool cancelRequested = false;
        private List<Texture2D> capturedFrames; // 用于图集打包

        // GUI
        private Vector2 scrollPos;
        private bool stylesInitialized;
        private GUIStyle headerStyle;
        private GUIStyle detectOkStyle;
        private GUIStyle detectWarnStyle;
        private GUIStyle bigGreenButtonStyle;

        // 渲染用的临时相机（透明背景时需要）
        private Camera renderCamera;

        #endregion

        #region 窗口初始化

        [MenuItem(MENU_ROOT + TOOL_NAME, false, 101)]
        public static void ShowWindow()
        {
            var window = GetWindow<Moshi_FrameRenderer>(TOOL_NAME);
            window.minSize = new Vector2(440, 620);
            window.Show();
        }

        private void OnEnable()
        {
            AutoDetectScene();
            if (string.IsNullOrEmpty(outputDir))
                outputDir = OUTPUT_ROOT;
        }

        private void OnDisable()
        {
            StopPreview();
            if (isRendering) cancelRequested = true;
            CleanupRenderCamera();
        }

        private void OnDestroy()
        {
            CleanupRenderCamera();
            if (previewTexture != null) DestroyImmediate(previewTexture);
        }

        #endregion

        #region 自动检测

        private void AutoDetectScene()
        {
            detectedTag = FindObjectOfType<Moshi_RenderCameraTag>();
            if (detectedTag != null)
            {
                hasDetectedCamera = true;
                targetCamera = detectedTag.GetComponent<Camera>();
                if (targetCamera != null)
                {
                    useOrthographic = detectedTag.useOrthographic;
                    customWidth = detectedTag.referenceWidth;
                    customHeight = detectedTag.referenceHeight;
                    bgOverride = detectedTag.useTransparentBackground
                        ? BackgroundMode.ForceTransparent
                        : BackgroundMode.UseCameraSetting;
                }
            }
            else
            {
                hasDetectedCamera = false;
                targetCamera = Camera.main;
            }

            TryAutoDetectAnimationRange();
        }

        private bool useOrthographic = false;

        private void TryAutoDetectAnimationRange()
        {
            var go = Selection.activeGameObject;
            if (go == null) return;

            var animator = go.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var clips = animator.runtimeAnimatorController.animationClips;
                if (clips.Length > 0)
                {
                    var clip = clips[0];
                    startFrame = 0;
                    endFrame = Mathf.RoundToInt(clip.length * fps);
                    // 如果检测到 fbx，尝试用实际帧率
                    float detectedFps = clip.frameRate;
                    if (detectedFps > 0)
                    {
                        fps = Mathf.RoundToInt(detectedFps);
                        endFrame = Mathf.RoundToInt(clip.length * fps);
                    }
                }
                return;
            }

            var animation = go.GetComponent<Animation>();
            if (animation != null && animation.clip != null)
            {
                var clip = animation.clip;
                startFrame = 0;
                endFrame = Mathf.RoundToInt(clip.length * fps);
                return;
            }

            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                startFrame = 0;
                endFrame = Mathf.RoundToInt(ps.main.duration * fps);
            }
        }

        #endregion

        #region GUI绘制

        private void OnGUI()
        {
            InitStyles();
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawTitleBar();
            DrawDetectionSection();
            DrawRenderParamsSection();
            DrawPreviewSection();
            DrawExecutionSection();

            EditorGUILayout.EndScrollView();

            // 处理取消（在 OnGUI 结束时检查，避免在渲染循环中直接操作）
            if (cancelRequested && !isRendering)
                cancelRequested = false;

            // 预览播放
            if (isPlaying && !isRendering)
                UpdatePreviewPlay();
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            detectOkStyle = new GUIStyle(EditorStyles.helpBox) { richText = true };
            detectWarnStyle = new GUIStyle(EditorStyles.helpBox) { richText = true };

            bigGreenButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                fixedHeight = 42
            };
        }

        private void DrawTitleBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("🎬 " + TOOL_NAME, headerStyle);
            GUILayout.FlexibleSpace();
            MoshiHelpButton.DrawHelpButton(TOOL_NAME);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private void DrawDetectionSection()
        {
            EditorGUILayout.LabelField("═══ 渲染目标 ═══", EditorStyles.centeredGreyMiniLabel);

            targetMode = (RenderTargetMode)EditorGUILayout.EnumPopup("模式", targetMode);

            if (targetMode == RenderTargetMode.Manual)
            {
                targetCamera = (Camera)EditorGUILayout.ObjectField("渲染相机", targetCamera, typeof(Camera), true);
            }
            else
            {
                // 自动检测结果显示
                if (hasDetectedCamera && detectedTag != null)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField("✅ 检测到渲染场景", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"  📷 RenderCamera (预设: {detectedTag.preset})");
                    EditorGUILayout.LabelField($"  🎨 透明背景: {(detectedTag.useTransparentBackground ? "是" : "否")}");
                    EditorGUILayout.LabelField($"  📐 正交模式: {(detectedTag.useOrthographic ? "是" : "否")}");
                    EditorGUILayout.LabelField($"  📏 参考分辨率: {detectedTag.referenceWidth}×{detectedTag.referenceHeight}");
                    EditorGUILayout.EndVertical();
                }
                else
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField("⚠ 未检测到渲染场景相机", EditorStyles.boldLabel);
                    EditorGUILayout.Space(4);
                    if (GUILayout.Button("🏗️ 打开渲染场景生成器", GUILayout.Height(30)))
                    {
                        Moshi_SceneGenerator.ShowWindow();
                    }
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.Space(4);
                if (GUILayout.Button("🔄 重新检测", GUILayout.Width(100)))
                    AutoDetectScene();
            }

            targetObject = (GameObject)EditorGUILayout.ObjectField("渲染目标(可选)", targetObject, typeof(GameObject), true);

            EditorGUILayout.Space(6);
        }

        private void DrawRenderParamsSection()
        {
            EditorGUILayout.LabelField("═══ 渲染参数 ═══", EditorStyles.centeredGreyMiniLabel);

            // 帧范围
            EditorGUILayout.BeginHorizontal();
            startFrame = EditorGUILayout.IntField("起始帧", startFrame);
            endFrame = EditorGUILayout.IntField("结束帧", endFrame);
            EditorGUILayout.EndHorizontal();
            fps = EditorGUILayout.IntSlider("FPS", fps, 1, 120);

            int totalFrames = Mathf.Max(0, endFrame - startFrame);
            float duration = fps > 0 ? (float)totalFrames / fps : 0;
            EditorGUILayout.LabelField($"  总帧数: {totalFrames}  时长: {duration:F2}秒", EditorStyles.miniLabel);

            EditorGUILayout.Space(2);

            // 分辨率
            useCameraResolution = EditorGUILayout.Toggle("使用相机分辨率", useCameraResolution);
            if (!useCameraResolution)
            {
                EditorGUILayout.BeginHorizontal();
                customWidth = EditorGUILayout.IntField("宽", customWidth);
                customHeight = EditorGUILayout.IntField("高", customHeight);
                EditorGUILayout.EndHorizontal();
            }

            // 超采样
            superSample = EditorGUILayout.IntPopup("超采样", superSample, new[] { "1x", "2x", "4x" }, new[] { 1, 2, 4 });

            // 背景覆盖
            bgOverride = (BackgroundMode)EditorGUILayout.EnumPopup("背景模式", bgOverride);
            if (bgOverride != BackgroundMode.UseCameraSetting)
            {
                overrideBgColor = EditorGUILayout.ColorField("背景颜色", overrideBgColor);
            }

            EditorGUILayout.Space(4);

            // 文件名
            fileNamePrefix = EditorGUILayout.TextField("文件名前缀", fileNamePrefix);

            // 输出目录
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("输出目录", GUILayout.Width(60));
            outputDir = EditorGUILayout.TextField(outputDir);
            if (GUILayout.Button("📂", GUILayout.Width(30)))
            {
                string selected = EditorUtility.OpenFolderPanel("选择输出目录", "Assets/", "");
                if (!string.IsNullOrEmpty(selected))
                {
                    // 转为相对路径
                    string dataPath = Application.dataPath.Replace("/", "\\");
                    selected = selected.Replace("/", "\\");
                    if (selected.StartsWith(dataPath))
                        outputDir = "Assets" + selected.Substring(dataPath.Length) + "/";
                    else
                        outputDir = selected + "/";
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!outputDir.StartsWith("Assets/"))
            {
                EditorGUILayout.HelpBox("输出目录建议在 Assets 目录下", MessageType.Warning);
            }

            EditorGUILayout.Space(6);
        }

        #endregion

        #region 预览面板

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("═══ 预览 ═══", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = previewFrame > startFrame;
            if (GUILayout.Button("◀◀", GUILayout.Width(35))) { previewFrame = startFrame; Repaint(); }
            if (GUILayout.Button("◀", GUILayout.Width(35))) { previewFrame = Mathf.Max(startFrame, previewFrame - 1); Repaint(); }
            GUI.enabled = true;

            int totalPreview = Mathf.Max(1, endFrame - startFrame);
            previewFrame = EditorGUILayout.IntSlider(previewFrame, startFrame, endFrame);

            GUI.enabled = previewFrame < endFrame;
            if (GUILayout.Button("▶", GUILayout.Width(35))) { previewFrame = Mathf.Min(endFrame, previewFrame + 1); Repaint(); }
            if (GUILayout.Button("▶▶", GUILayout.Width(35))) { previewFrame = endFrame; Repaint(); }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            float time = fps > 0 ? (float)(previewFrame - startFrame) / fps : 0;
            EditorGUILayout.LabelField($"  当前帧: {previewFrame} / {endFrame}    时间: {time:F3}s", EditorStyles.miniLabel);

            // 播放按钮
            EditorGUILayout.BeginHorizontal();
            var playLabel = isPlaying ? "⏸ 停止预览" : "▶ 循环预览";
            if (GUILayout.Button(playLabel, GUILayout.Width(100)))
            {
                if (isPlaying) StopPreview();
                else StartPreview();
            }
            EditorGUILayout.EndHorizontal();

            // 缩略图
            if (previewTexture != null)
            {
                float maxWidth = position.width - 20;
                float aspect = (float)previewTexture.width / previewTexture.height;
                float h = maxWidth / aspect;
                h = Mathf.Min(h, 200);
                Rect previewRect = GUILayoutUtility.GetRect(maxWidth, h);
                GUI.DrawTexture(previewRect, previewTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUILayout.LabelField("(点击预览播放查看画面)", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.Space(6);
        }

        private void StartPreview()
        {
            isPlaying = true;
            previewFrame = startFrame;
            lastPreviewTime = EditorApplication.timeSinceStartup;
        }

        private void StopPreview()
        {
            isPlaying = false;
        }

        private void UpdatePreviewPlay()
        {
            double now = EditorApplication.timeSinceStartup;
            float frameDuration = 1f / Mathf.Max(1, fps);
            if (now - lastPreviewTime >= frameDuration)
            {
                lastPreviewTime = now;
                previewFrame++;
                if (previewFrame > endFrame)
                    previewFrame = startFrame;

                CapturePreviewFrame();
                Repaint();
            }
        }

        private void CapturePreviewFrame()
        {
            Camera cam = GetWorkingCamera();
            if (cam == null) return;

            int w = GetOutputWidth();
            int h = GetOutputHeight();

            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevRT = cam.targetTexture;
            var prevFlags = cam.clearFlags;
            var prevBg = cam.backgroundColor;

            cam.targetTexture = rt;

            if (bgOverride == BackgroundMode.ForceTransparent)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
            }
            else if (bgOverride == BackgroundMode.ForceOpaque)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = overrideBgColor;
            }

            // 采样动画/粒子
            SampleFrame(previewFrame, cam);

            cam.Render();

            if (previewTexture != null) DestroyImmediate(previewTexture);
            previewTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            previewTexture.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            previewTexture.Apply();
            RenderTexture.active = null;

            cam.targetTexture = prevRT;
            cam.clearFlags = prevFlags;
            cam.backgroundColor = prevBg;
            RenderTexture.ReleaseTemporary(rt);
        }

        #endregion

        #region 动画/粒子帧采样

        private void SampleFrame(int frame, Camera cam)
        {
            float time = fps > 0 ? (float)(frame - startFrame) / fps : 0f;
            time = Mathf.Max(0, time);

            if (targetObject != null)
            {
                // 动画采样
                var animator = targetObject.GetComponent<Animator>();
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    var clips = animator.runtimeAnimatorController.animationClips;
                    if (clips.Length > 0)
                    {
                        float clipTime = time % clips[0].length;
                        if (!AnimationMode.InAnimationMode())
                            AnimationMode.StartAnimationMode();
                        AnimationMode.SampleAnimationClip(targetObject, clips[0], clipTime);
                    }
                }
                else
                {
                    var anim = targetObject.GetComponent<Animation>();
                    if (anim != null && anim.clip != null)
                    {
                        float clipTime = time % anim.clip.length;
                        if (!AnimationMode.InAnimationMode())
                            AnimationMode.StartAnimationMode();
                        AnimationMode.SampleAnimationClip(targetObject, anim.clip, clipTime);
                    }
                }

                // 粒子系统采样
                var allParticles = targetObject.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in allParticles)
                {
                    ps.Simulate(time, true, true);
                    if (!ps.isPlaying)
                        ps.Play();
                    ps.Simulate(time, true, true);
                }
            }
            else
            {
                // 没有指定目标时采样场景中所有粒子
                var allParticles = FindObjectsOfType<ParticleSystem>();
                foreach (var ps in allParticles)
                {
                    ps.Simulate(time, true, true);
                    if (!ps.isPlaying)
                        ps.Play();
                    ps.Simulate(time, true, true);
                }
            }
        }

        private void StopAnimationMode()
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
        }

        #endregion

        #region 逐帧渲染引擎

        private void DrawExecutionSection()
        {
            EditorGUILayout.LabelField("═══ 执行 ═══", EditorStyles.centeredGreyMiniLabel);

            EditorGUI.BeginDisabledGroup(isRendering);
            EditorGUILayout.BeginHorizontal();

            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.28f, 0.7f, 0.28f);
            if (GUILayout.Button("🎬 开始逐帧渲染", bigGreenButtonStyle))
                StartFrameRender();
            GUI.backgroundColor = oldBg;

            if (GUILayout.Button("📦 打包为图集", GUILayout.Height(42), GUILayout.Width(100)))
            {
                // 先渲染再打包
                renderThenPack = true;
                StartFrameRender();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();

            if (isRendering)
            {
                EditorGUILayout.Space(4);
                int done = currentRenderFrame - startFrame;
                int total = totalRenderFrames;
                float progress = total > 0 ? (float)done / total : 0;

                var progressRect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(progressRect, progress, $"{done}/{total} 帧 ({progress * 100:F0}%)");

                if (renderStopwatch != null && done > 0)
                {
                    float elapsed = (float)renderStopwatch.Elapsed.TotalSeconds;
                    float remaining = (elapsed / done) * (total - done);
                    EditorGUILayout.LabelField($"  剩余约 {remaining:F1} 秒", EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(2);
                if (GUILayout.Button("❌ 取消渲染", GUILayout.Height(30)))
                {
                    cancelRequested = true;
                }
            }
        }

        private bool renderThenPack = false;
        private bool packAfterRender = false;

        private void StartFrameRender()
        {
            Camera cam = GetWorkingCamera();
            if (cam == null)
            {
                EditorUtility.DisplayDialog("错误", "未找到可用相机! 请先生成渲染场景。", "确定");
                return;
            }

            string dir = outputDir;
            if (string.IsNullOrEmpty(dir))
                dir = OUTPUT_ROOT;

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            isRendering = true;
            cancelRequested = false;
            packAfterRender = renderThenPack;
            renderThenPack = false;
            currentRenderFrame = startFrame;
            totalRenderFrames = endFrame - startFrame;
            capturedFrames = new List<Texture2D>();
            renderStopwatch = Stopwatch.StartNew();

            EditorApplication.update -= DoRenderStep;
            EditorApplication.update += DoRenderStep;

            ShowNotification(new GUIContent("开始逐帧渲染..."));
        }

        private void DoRenderStep()
        {
            if (!isRendering)
            {
                EditorApplication.update -= DoRenderStep;
                return;
            }

            if (cancelRequested)
            {
                FinishRendering(false);
                return;
            }

            if (currentRenderFrame > endFrame)
            {
                FinishRendering(true);
                return;
            }

            try
            {
                RenderSingleFrame(currentRenderFrame);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"Frame {currentRenderFrame} render error: {ex}");
            }

            currentRenderFrame++;
            Repaint();
        }

        private void RenderSingleFrame(int frame)
        {
            Camera cam = GetWorkingCamera();
            if (cam == null) return;

            int w = GetOutputWidth() * superSample;
            int h = GetOutputHeight() * superSample;

            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevRT = cam.targetTexture;
            var prevFlags = cam.clearFlags;
            var prevBg = cam.backgroundColor;

            cam.targetTexture = rt;

            // 背景设置
            if (bgOverride == BackgroundMode.ForceTransparent)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
            }
            else if (bgOverride == BackgroundMode.ForceOpaque)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = overrideBgColor;
            }

            // 采样帧
            SampleFrame(frame, cam);

            // 渲染
            cam.Render();

            // 读取像素
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            // 恢复相机
            cam.targetTexture = prevRT;
            cam.clearFlags = prevFlags;
            cam.backgroundColor = prevBg;
            RenderTexture.ReleaseTemporary(rt);

            // 超采样降采样
            if (superSample > 1)
            {
                tex = DownsampleTexture(tex, GetOutputWidth(), GetOutputHeight());
            }

            // 保存PNG
            string numbering = $"_{frame:D4}";
            string filePath = Path.Combine(outputDir, $"{fileNamePrefix}{numbering}.png");
            byte[] pngData = tex.EncodeToPNG();
            File.WriteAllBytes(filePath, pngData);

            // 存储用于图集
            capturedFrames.Add(tex);
        }

        private Texture2D DownsampleTexture(Texture2D src, int targetW, int targetH)
        {
            var rt = RenderTexture.GetTemporary(targetW, targetH, 24, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var result = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            result.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0);
            result.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            DestroyImmediate(src);
            return result;
        }

        private void FinishRendering(bool completed)
        {
            EditorApplication.update -= DoRenderStep;
            isRendering = false;
            StopAnimationMode();
            CleanupRenderCamera();

            // 清理纹理（除了最后一个）
            for (int i = 0; i < capturedFrames.Count - 1; i++)
            {
                if (capturedFrames[i] != null)
                    DestroyImmediate(capturedFrames[i]);
            }

            if (completed)
            {
                string dir = outputDir;
                int count = totalRenderFrames;
                string msg = $"渲染完成!\n\n输出目录: {dir}\n帧数: {count}\n\n可在 Project 窗口查看输出。";

                // 打包图集
                if (packAfterRender && capturedFrames.Count > 0)
                {
                    string atlasPath = PackSpriteSheet();
                    if (!string.IsNullOrEmpty(atlasPath))
                        msg += $"\n\n图集已生成: {atlasPath}";
                    packAfterRender = false;
                }

                EditorUtility.DisplayDialog("渲染完成", msg, "确定");

                // 定位到输出目录
                var folder = AssetDatabase.LoadAssetAtPath<Object>(dir.TrimEnd('/'));
                if (folder != null)
                {
                    EditorGUIUtility.PingObject(folder);
                }

                // 清理剩余纹理
                foreach (var t in capturedFrames)
                {
                    if (t != null) DestroyImmediate(t);
                }
                capturedFrames.Clear();

                AssetDatabase.Refresh();
                ShowNotification(new GUIContent($"✅ {count}帧渲染完成"));
            }
            else
            {
                // 取消 — 清理
                foreach (var t in capturedFrames)
                {
                    if (t != null) DestroyImmediate(t);
                }
                capturedFrames.Clear();

                ShowNotification(new GUIContent("❌ 渲染已取消"));
            }

            renderStopwatch = null;
            Repaint();
        }

        #endregion

        #region 图集打包

        private string PackSpriteSheet()
        {
            if (capturedFrames.Count == 0) return null;

            int cols = Mathf.CeilToInt(Mathf.Sqrt(capturedFrames.Count));
            int rows = Mathf.CeilToInt((float)capturedFrames.Count / cols);

            int frameW = capturedFrames[0].width;
            int frameH = capturedFrames[0].height;

            int atlasW = cols * frameW;
            int atlasH = rows * frameH;

            var atlas = new Texture2D(atlasW, atlasH, TextureFormat.RGBA32, false);

            // 填充透明
            Color[] clear = new Color[atlasW * atlasH];
            for (int i = 0; i < clear.Length; i++) clear[i] = Color.clear;
            atlas.SetPixels(clear);

            for (int i = 0; i < capturedFrames.Count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                int x = col * frameW;
                int y = atlasH - (row + 1) * frameH; // 从底部往上放

                var frame = capturedFrames[i];
                if (frame == null) continue;

                // 如果尺寸不匹配，缩放
                if (frame.width != frameW || frame.height != frameH)
                {
                    frame = ResizeTexture(frame, frameW, frameH);
                }

                atlas.SetPixels(x, y, frameW, frameH, frame.GetPixels());
            }

            atlas.Apply();

            string atlasPath = Path.Combine(outputDir, $"{fileNamePrefix}_SpriteSheet.png");
            File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());
            DestroyImmediate(atlas);

            AssetDatabase.Refresh();

            // 设置导入格式
            var importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return atlasPath;
        }

        private Texture2D ResizeTexture(Texture2D src, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            result.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        #endregion

        #region 辅助

        private Camera GetWorkingCamera()
        {
            if (targetMode == RenderTargetMode.Manual && targetCamera != null)
                return targetCamera;

            if (hasDetectedCamera && targetCamera != null)
                return targetCamera;

            return Camera.main;
        }

        private int GetOutputWidth()
        {
            if (useCameraResolution && GetWorkingCamera() != null)
            {
                var cam = GetWorkingCamera();
                var tag = cam.GetComponent<Moshi_RenderCameraTag>();
                if (tag != null)
                    return tag.referenceWidth;
                return cam.pixelWidth;
            }
            return customWidth;
        }

        private int GetOutputHeight()
        {
            if (useCameraResolution && GetWorkingCamera() != null)
            {
                var cam = GetWorkingCamera();
                var tag = cam.GetComponent<Moshi_RenderCameraTag>();
                if (tag != null)
                    return tag.referenceHeight;
                return cam.pixelHeight;
            }
            return customHeight;
        }

        private void CleanupRenderCamera()
        {
            if (renderCamera != null)
            {
                if (renderCamera.gameObject != null)
                    DestroyImmediate(renderCamera.gameObject);
                renderCamera = null;
            }
        }

        #endregion
    }
}
