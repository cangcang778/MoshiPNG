using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MoshiTools
{
    /// <summary>
    /// 透明逐帧渲染环境生成器
    /// Transparent frame render environment generator
    /// </summary>
    public class Moshi_FrameEnv : EditorWindow
    {
        private const string TOOL_NAME = "逐帧渲染环境";
        private const string MENU_ROOT = "工具/Moshi/";
        private const string DEFAULT_SCENE_DIR = "Assets/Scenes/Moshi_FrameRenderEnv";

        private enum EnvMode { VFX, UI, Character, Custom }
        private enum CameraMode { Perspective, Orthographic }
        private enum PostMode { SyncGame, NewVolume, Off }

        private EnvMode envMode = EnvMode.VFX;
        private CameraMode cameraMode = CameraMode.Perspective;
        private PostMode postMode = PostMode.NewVolume;

        private Transform targetRoot;
        private Camera referenceCamera;
        private int width = 1024;
        private int height = 1024;
        private float cameraDistance = 5f;
        private float orthographicSize = 3f;
        private bool generateDualCamera = true;
        private bool isolateTargetLayer = true;
        private bool generateLighting = true;
        private bool generateGuide = true;
        private bool useBloom = true;
        private bool useToneMapping = true;
        private bool useColorAdjust = true;
        private string sceneName = "Moshi_FrameRenderEnv";
        private string outputDirectory = "Assets/Moshi_FrameOutput";
        private Vector2 scrollPos;

        [MenuItem(MENU_ROOT + TOOL_NAME, false, 106)]
        public static void ShowWindow()
        {
            Moshi_FrameEnv window = GetWindow<Moshi_FrameEnv>(TOOL_NAME);
            window.minSize = new Vector2(460, 640);
            window.Show();
        }

        private void OnEnable()
        {
            if (referenceCamera == null)
            {
                referenceCamera = Camera.main;
            }
            ApplyModeDefaults();
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            DrawHeader();
            DrawModeSection();
            DrawTargetSection();
            DrawCameraSection();
            DrawTransparentSection();
            DrawPostSection();
            DrawEnvironmentSection();
            DrawOutputSection();
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
            EditorGUILayout.HelpBox("生成黑白差分透明逐帧渲染专用环境。环境默认无实体背景，保留 URP 后处理，供 Moshi_FrameRenderPro 输出透明 PNG。", MessageType.Info);
        }

        private void DrawModeSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("环境模式", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(envMode == EnvMode.VFX, "特效", EditorStyles.miniButtonLeft)) SetMode(EnvMode.VFX);
            if (GUILayout.Toggle(envMode == EnvMode.UI, "UI", EditorStyles.miniButtonMid)) SetMode(EnvMode.UI);
            if (GUILayout.Toggle(envMode == EnvMode.Character, "角色", EditorStyles.miniButtonMid)) SetMode(EnvMode.Character);
            if (GUILayout.Toggle(envMode == EnvMode.Custom, "自定", EditorStyles.miniButtonRight)) SetMode(EnvMode.Custom);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTargetSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("渲染目标", EditorStyles.boldLabel);
            targetRoot = (Transform)EditorGUILayout.ObjectField("目标根节点", targetRoot, typeof(Transform), true);
            EditorGUILayout.BeginHorizontal();
            referenceCamera = (Camera)EditorGUILayout.ObjectField("参考相机", referenceCamera, typeof(Camera), true);
            if (GUILayout.Button("主相机", GUILayout.Width(60))) referenceCamera = Camera.main;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCameraSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("相机配置", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("模式", GUILayout.Width(60));
            if (GUILayout.Toggle(cameraMode == CameraMode.Perspective, "透视", EditorStyles.miniButtonLeft, GUILayout.Width(55))) cameraMode = CameraMode.Perspective;
            if (GUILayout.Toggle(cameraMode == CameraMode.Orthographic, "正交", EditorStyles.miniButtonRight, GUILayout.Width(55))) cameraMode = CameraMode.Orthographic;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            width = EditorGUILayout.IntField("宽度", width);
            height = EditorGUILayout.IntField("高度", height);
            cameraDistance = EditorGUILayout.FloatField("相机距离", cameraDistance);
            orthographicSize = EditorGUILayout.FloatField("正交尺寸", orthographicSize);
        }

        private void DrawTransparentSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("透明方案", EditorStyles.boldLabel);
            generateDualCamera = EditorGUILayout.ToggleLeft("生成黑白差分双相机", generateDualCamera);
            isolateTargetLayer = EditorGUILayout.ToggleLeft("渲染时隔离目标根节点", isolateTargetLayer);
        }

        private void DrawPostSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("后处理", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("来源", GUILayout.Width(60));
            if (GUILayout.Toggle(postMode == PostMode.SyncGame, "同步", EditorStyles.miniButtonLeft, GUILayout.Width(55))) postMode = PostMode.SyncGame;
            if (GUILayout.Toggle(postMode == PostMode.NewVolume, "新建", EditorStyles.miniButtonMid, GUILayout.Width(55))) postMode = PostMode.NewVolume;
            if (GUILayout.Toggle(postMode == PostMode.Off, "关闭", EditorStyles.miniButtonRight, GUILayout.Width(55))) postMode = PostMode.Off;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(postMode == PostMode.Off))
            {
                useBloom = EditorGUILayout.ToggleLeft("Bloom", useBloom);
                useToneMapping = EditorGUILayout.ToggleLeft("ToneMapping", useToneMapping);
                useColorAdjust = EditorGUILayout.ToggleLeft("Color Adjust", useColorAdjust);
            }
        }

        private void DrawEnvironmentSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("环境内容", EditorStyles.boldLabel);
            generateLighting = EditorGUILayout.ToggleLeft("生成灯光", generateLighting);
            generateGuide = EditorGUILayout.ToggleLeft("生成参考框", generateGuide);
            EditorGUILayout.HelpBox("透明逐帧环境默认不生成背景板和地面，避免背景进入透明 PNG。", MessageType.None);
        }

        private void DrawOutputSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("输出", EditorStyles.boldLabel);
            sceneName = EditorGUILayout.TextField("环境名称", sceneName);
            outputDirectory = EditorGUILayout.TextField("输出目录", outputDirectory);
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(10);
            if (GUILayout.Button("生成环境", GUILayout.Height(36)))
            {
                GenerateEnvironment(false);
            }
            if (GUILayout.Button("生成并保存场景", GUILayout.Height(30)))
            {
                GenerateEnvironment(true);
            }
            if (GUILayout.Button("打开逐帧渲染器", GUILayout.Height(24)))
            {
                Moshi_FrameRenderPro.ShowWindow();
            }
        }

        private void SetMode(EnvMode mode)
        {
            if (envMode == mode) return;
            envMode = mode;
            ApplyModeDefaults();
        }

        private void ApplyModeDefaults()
        {
            switch (envMode)
            {
                case EnvMode.VFX:
                    cameraMode = CameraMode.Perspective;
                    width = 1024;
                    height = 1024;
                    cameraDistance = 5f;
                    orthographicSize = 3f;
                    generateLighting = true;
                    useBloom = true;
                    useToneMapping = true;
                    break;
                case EnvMode.UI:
                    cameraMode = CameraMode.Orthographic;
                    width = 1024;
                    height = 1024;
                    cameraDistance = 10f;
                    orthographicSize = 5f;
                    generateLighting = false;
                    useBloom = false;
                    useToneMapping = false;
                    break;
                case EnvMode.Character:
                    cameraMode = CameraMode.Perspective;
                    width = 1024;
                    height = 1024;
                    cameraDistance = 6f;
                    orthographicSize = 3f;
                    generateLighting = true;
                    useBloom = true;
                    useToneMapping = true;
                    break;
            }
        }

        private void GenerateEnvironment(bool saveScene)
        {
            Undo.SetCurrentGroupName("生成逐帧渲染环境");
            int group = Undo.GetCurrentGroup();

            GameObject root = new GameObject(sceneName);
            Undo.RegisterCreatedObjectUndo(root, "创建逐帧渲染环境");

            GameObject target = new GameObject("RenderTargetRoot");
            target.transform.SetParent(root.transform);
            if (targetRoot != null)
            {
                target.transform.position = targetRoot.position;
            }

            GameObject cameras = new GameObject("Cameras");
            cameras.transform.SetParent(root.transform);

            Camera refCam = referenceCamera != null ? referenceCamera : Camera.main;
            Camera blackCam = CreateCamera("Moshi_RenderCamera_Black", cameras.transform, refCam, Color.black);
            Camera whiteCam = generateDualCamera ? CreateCamera("Moshi_RenderCamera_White", cameras.transform, refCam, Color.white) : null;

            Volume volume = null;
            if (postMode != PostMode.Off)
            {
                volume = CreateVolume(root.transform);
            }

            if (generateLighting)
            {
                CreateLighting(root.transform);
            }

            if (generateGuide)
            {
                CreateGuide(root.transform);
            }

            Moshi_FrameRenderConfig config = root.AddComponent<Moshi_FrameRenderConfig>();
            config.environmentMode = ConvertMode(envMode);
            config.targetRoot = targetRoot != null ? targetRoot : target.transform;
            config.referenceCamera = refCam;
            config.blackCamera = blackCam;
            config.whiteCamera = whiteCam;
            config.postProcessVolume = volume;
            config.width = Mathf.Max(16, width);
            config.height = Mathf.Max(16, height);
            config.outputDirectory = outputDirectory;
            config.useProfessionalAlpha = true;
            config.useDifferentialAlpha = true;
            config.useHDRBuffer = true;
            config.glowAlphaStrength = 2f;
            config.alphaThreshold = 0.001f;
            config.isolateTargetLayer = isolateTargetLayer;
            config.renderLayerMask = blackCam.cullingMask;

            Selection.activeGameObject = root;
            Undo.CollapseUndoOperations(group);

            if (saveScene)
            {
                Directory.CreateDirectory(DEFAULT_SCENE_DIR);
                string path = DEFAULT_SCENE_DIR + "/" + sceneName + ".unity";
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("完成", "逐帧渲染环境已生成并保存：\n" + path, "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("完成", "逐帧渲染环境已生成。", "确定");
            }
        }

        private Camera CreateCamera(string name, Transform parent, Camera source, Color background)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent);
            Camera cam = go.AddComponent<Camera>();
            if (source != null)
            {
                cam.CopyFrom(source);
                go.transform.position = source.transform.position;
                go.transform.rotation = source.transform.rotation;
            }
            else
            {
                go.transform.position = new Vector3(0f, 0f, -cameraDistance);
                go.transform.rotation = Quaternion.identity;
                cam.fieldOfView = 35f;
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.orthographic = cameraMode == CameraMode.Orthographic;
            cam.orthographicSize = Mathf.Max(0.01f, orthographicSize);
            cam.allowHDR = true;
            cam.allowMSAA = true;

            Moshi_RenderCameraTag tag = go.AddComponent<Moshi_RenderCameraTag>();
            tag.preset = Moshi_RenderCameraTag.RenderPreset.Custom;
            tag.useTransparentBackground = true;
            tag.useOrthographic = cam.orthographic;
            tag.referenceWidth = width;
            tag.referenceHeight = height;

            UniversalAdditionalCameraData data = go.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = postMode != PostMode.Off;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.stopNaN = false;
            data.dithering = false;
            return cam;
        }

        private Volume CreateVolume(Transform parent)
        {
            GameObject go = new GameObject("Moshi_PostProcessVolume");
            go.transform.SetParent(parent);
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;

            Volume sourceVolume = postMode == PostMode.SyncGame ? FindSourceVolume() : null;
            if (sourceVolume != null && sourceVolume.sharedProfile != null)
            {
                volume.isGlobal = sourceVolume.isGlobal;
                volume.priority = sourceVolume.priority;
                volume.weight = sourceVolume.weight;
                volume.sharedProfile = sourceVolume.sharedProfile;
                return volume;
            }

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            if (useBloom)
            {
                Bloom bloom = profile.Add<Bloom>(true);
                bloom.intensity.Override(envMode == EnvMode.VFX ? 0.8f : 0.25f);
                bloom.threshold.Override(0.8f);
                bloom.scatter.Override(0.65f);
            }
            if (useToneMapping)
            {
                Tonemapping tone = profile.Add<Tonemapping>(true);
                tone.mode.Override(TonemappingMode.ACES);
            }
            if (useColorAdjust)
            {
                ColorAdjustments color = profile.Add<ColorAdjustments>(true);
                color.postExposure.Override(0f);
                color.contrast.Override(5f);
                color.saturation.Override(0f);
                color.colorFilter.Override(Color.white);
            }
            volume.sharedProfile = profile;
            return volume;
        }

        private void CreateLighting(Transform parent)
        {
            GameObject lighting = new GameObject("Lighting");
            lighting.transform.SetParent(parent);
            CreateLight("KeyLight", lighting.transform, new Vector3(40f, -30f, 0f), 1.2f);
            CreateLight("FillLight", lighting.transform, new Vector3(20f, 130f, 0f), 0.45f);
            CreateLight("RimLight", lighting.transform, new Vector3(-25f, 180f, 0f), 0.35f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = envMode == EnvMode.VFX ? new Color(0.05f, 0.05f, 0.05f) : Color.gray;
        }

        private void CreateLight(string name, Transform parent, Vector3 euler, float intensity)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.rotation = Quaternion.Euler(euler);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
        }

        private void CreateGuide(Transform parent)
        {
            GameObject guide = new GameObject("FrameBounds_Guide");
            guide.transform.SetParent(parent);
            guide.transform.localPosition = Vector3.zero;
        }

        private Volume FindSourceVolume()
        {
            Volume[] volumes = FindObjectsOfType<Volume>();
            Volume best = null;
            for (int i = 0; i < volumes.Length; i++)
            {
                Volume volume = volumes[i];
                if (volume == null || !volume.isActiveAndEnabled || volume.sharedProfile == null) continue;
                if (best == null || volume.priority > best.priority)
                {
                    best = volume;
                }
            }
            return best;
        }

        private Moshi_FrameRenderConfig.EnvironmentMode ConvertMode(EnvMode mode)
        {
            switch (mode)
            {
                case EnvMode.UI: return Moshi_FrameRenderConfig.EnvironmentMode.UI;
                case EnvMode.Character: return Moshi_FrameRenderConfig.EnvironmentMode.Character;
                case EnvMode.Custom: return Moshi_FrameRenderConfig.EnvironmentMode.Custom;
                default: return Moshi_FrameRenderConfig.EnvironmentMode.VFX;
            }
        }
    }
}
