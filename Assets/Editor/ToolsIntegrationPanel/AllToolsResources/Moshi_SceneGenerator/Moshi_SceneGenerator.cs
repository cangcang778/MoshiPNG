using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.IO;
using UnityTemplateProjects;

namespace MoshiTools
{
    /// <summary>
    /// Moshi 渲染场景生成器
    /// 一键生成标准化渲染环境（灯光/相机/背景/展示台），为逐帧渲染器提供渲染基础
    /// </summary>
    public class Moshi_SceneGenerator : EditorWindow
    {
        private const string TOOL_NAME = "渲染场景生成";
        private const string MENU_ROOT = "工具/Moshi/";
        private const string SCENE_SAVE_DIR = "Assets/Scenes/Moshi_RenderEnv/";

        #region 预设枚举

        private enum EnvPreset
        {
            ModelTurntable,   // 模型展示台
            VFXStage,         // 特效平面台
            ProductShot,      // 产品展示台
            Sprite2D,         // 2D序列帧
            Custom            // 自定义
        }

        private enum LightPreset
        {
            ThreePoint,       // 三点光
            Softbox,          // 柔光箱
            Environment,      // 均匀环境光
            None              // 无灯光
        }

        private enum BackgroundMode
        {
            SolidColor,       // 纯色
            Transparent,      // 透明
            Gradient,         // 渐变
            Skybox,           // 天空盒
            Black             // 黑色
        }

        private enum LightColorMode
        {
            ColorTemperature, // 色温模式
            ColorPicker       // 拾色器模式
        }

        #endregion

        #region 配置字段

        // 预设选择
        private EnvPreset currentPreset = EnvPreset.ModelTurntable;

        // 灯光
        private LightPreset lightPreset = LightPreset.ThreePoint;
        private LightColorMode lightColorMode = LightColorMode.ColorTemperature;
        private float keyLightIntensity = 1.2f;
        private float keyLightKelvin = 5500f;
        private Color keyLightColor = Color.white;
        private float fillLightIntensity = 0.6f;
        private float fillLightKelvin = 4500f;
        private Color fillLightColor = new Color(1f, 0.97f, 0.91f);
        private float rimLightIntensity = 0.4f;
        private float rimLightKelvin = 6500f;
        private Color rimLightColor = new Color(0.91f, 0.94f, 1f);
        private bool enableShadow = true;

        // 相机
        private bool useOrthographic = false;
        private float orbitAngle = 45f;
        private float cameraDistance = 5f;
        private float cameraHeight = 1.5f;
        private float pitchAngle = 15f;
        private int resolutionWidth = 1920;
        private int resolutionHeight = 1080;
        private bool attachFreeLook = false;

        // 背景
        private BackgroundMode bgMode = BackgroundMode.SolidColor;
        private Color bgColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        private bool showFloor = true;
        private bool showPedestal = true;

        // 输出
        private string sceneName = "Moshi_RenderEnv_ModelTurntable";
        private string[] resolutionPresets = { "1920×1080", "1024×1024", "512×512", "3840×2160", "自定义" };
        private int resolutionPresetIndex = 0;

        // 后期处理
        private bool enablePostProcess = false;
        private bool useBloom = false;
        private float bloomThreshold = 0.9f;
        private float bloomIntensity = 0.3f;
        private bool useTonemapping = false;
        private int tonemappingMode = 0;                // 0=Neutral, 1=ACES
        private bool useVignette = false;
        private float vignetteIntensity = 0.3f;
        private bool useColorAdjust = false;
        private float postExposure = 0f;
        private float contrast = 0f;
        private float saturation = 0f;
        private bool useDOF = false;

        // GUI 滚动
        private Vector2 scrollPos;

        // 预设按钮缓存（避免每帧创建Texture2D）
        private Dictionary<int, Texture2D> cachedButtonBg = new Dictionary<int, Texture2D>();
        private Color cachedBgColor = Color.clear;

        #endregion

        #region 预设切换

        private void ApplyPreset(EnvPreset preset)
        {
            currentPreset = preset;
            switch (preset)
            {
                case EnvPreset.ModelTurntable:
                    lightPreset = LightPreset.ThreePoint;
                    useOrthographic = false;
                    orbitAngle = 45f;
                    cameraDistance = 5f;
                    cameraHeight = 1.5f;
                    pitchAngle = 15f;
                    bgMode = BackgroundMode.SolidColor;
                    bgColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                    showFloor = true;
                    showPedestal = true;
                    enableShadow = true;
                    sceneName = "Moshi_RenderEnv_ModelTurntable";
                    // 后期: 轻微Bloom + ACES调色
                    enablePostProcess = true;
                    useBloom = true;  bloomThreshold = 1.0f; bloomIntensity = 0.2f;
                    useTonemapping = true; tonemappingMode = 1;  // ACES
                    useVignette = true; vignetteIntensity = 0.25f;
                    useColorAdjust = true; postExposure = 0f; contrast = 5f; saturation = 0f;
                    useDOF = false;
                    break;

                case EnvPreset.VFXStage:
                    lightPreset = LightPreset.Environment;
                    useOrthographic = false;
                    orbitAngle = 0f;
                    cameraDistance = 8f;
                    cameraHeight = 0f;
                    pitchAngle = 0f;
                    bgMode = BackgroundMode.Black;
                    showFloor = false;
                    showPedestal = false;
                    enableShadow = false;
                    sceneName = "Moshi_RenderEnv_VFXStage";
                    // 后期: 强Bloom让粒子发光 + 轻微暗角
                    enablePostProcess = true;
                    useBloom = true;  bloomThreshold = 0.7f; bloomIntensity = 0.8f;
                    useTonemapping = true; tonemappingMode = 1;
                    useVignette = true; vignetteIntensity = 0.35f;
                    useColorAdjust = true; postExposure = 0.2f; contrast = 10f; saturation = 5f;
                    useDOF = false;
                    break;

                case EnvPreset.ProductShot:
                    lightPreset = LightPreset.Softbox;
                    useOrthographic = false;
                    orbitAngle = 30f;
                    cameraDistance = 3f;
                    cameraHeight = 0.8f;
                    pitchAngle = 10f;
                    bgMode = BackgroundMode.SolidColor;
                    bgColor = Color.white;
                    showFloor = true;
                    showPedestal = true;
                    enableShadow = true;
                    sceneName = "Moshi_RenderEnv_ProductShot";
                    // 后期: 干净画面，轻微锐化感(高对比低饱和)
                    enablePostProcess = true;
                    useBloom = false; bloomThreshold = 1f; bloomIntensity = 0f;
                    useTonemapping = true; tonemappingMode = 0;  // Neutral
                    useVignette = true; vignetteIntensity = 0.2f;
                    useColorAdjust = true; postExposure = 0f; contrast = 15f; saturation = -5f;
                    useDOF = false;
                    break;

                case EnvPreset.Sprite2D:
                    lightPreset = LightPreset.Environment;
                    useOrthographic = true;
                    orbitAngle = 0f;
                    cameraDistance = 10f;
                    cameraHeight = 0f;
                    pitchAngle = 0f;
                    bgMode = BackgroundMode.Transparent;
                    showFloor = false;
                    showPedestal = false;
                    enableShadow = false;
                    sceneName = "Moshi_RenderEnv_Sprite2D";
                    // 后期: 原汁原味，透明背景下不用后期
                    enablePostProcess = false;
                    useBloom = false; bloomThreshold = 1f; bloomIntensity = 0f;
                    useTonemapping = false; tonemappingMode = 0;
                    useVignette = false; vignetteIntensity = 0f;
                    useColorAdjust = false; postExposure = 0f; contrast = 0f; saturation = 0f;
                    useDOF = false;
                    break;

                case EnvPreset.Custom:
                    sceneName = "Moshi_RenderEnv_Custom";
                    // 后期: 默认关闭，用户自行配置
                    enablePostProcess = false;
                    useBloom = false; bloomThreshold = 1f; bloomIntensity = 0.3f;
                    useTonemapping = false; tonemappingMode = 0;
                    useVignette = false; vignetteIntensity = 0.3f;
                    useColorAdjust = false; postExposure = 0f; contrast = 0f; saturation = 0f;
                    useDOF = false;
                    break;
            }
            UpdateResolutionPresetIndex();
        }

        #endregion

        #region 窗口初始化

        [MenuItem(MENU_ROOT + TOOL_NAME, false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<Moshi_SceneGenerator>(TOOL_NAME);
            window.minSize = new Vector2(420, 600);
            window.Show();
        }

        private void OnEnable()
        {
            ApplyPreset(EnvPreset.ModelTurntable);
        }

        private void OnDisable()
        {
            // 清理缓存的纹理
            foreach (var tex in cachedButtonBg.Values)
            {
                if (tex != null) DestroyImmediate(tex);
            }
            cachedButtonBg.Clear();
        }

        #endregion

        #region GUI绘制

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawTitleBar();
            DrawPresetButtons();
            DrawLightingSection();
            DrawPostProcessSection();
            DrawCameraSection();
            DrawBackgroundSection();
            DrawOutputSection();
            DrawGenerateButton();

            EditorGUILayout.EndScrollView();
        }

        private void DrawTitleBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("🎬 " + TOOL_NAME, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            MoshiHelpButton.DrawHelpButton(TOOL_NAME);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private void DrawPresetButtons()
        {
            EditorGUILayout.LabelField("环境预设", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (DrawPresetButton("🎯 模型\n展示台", currentPreset == EnvPreset.ModelTurntable))
                ApplyPreset(EnvPreset.ModelTurntable);
            if (DrawPresetButton("✨ 特效\n平面台", currentPreset == EnvPreset.VFXStage))
                ApplyPreset(EnvPreset.VFXStage);
            if (DrawPresetButton("📦 产品\n展示台", currentPreset == EnvPreset.ProductShot))
                ApplyPreset(EnvPreset.ProductShot);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();

            if (DrawPresetButton("🎨 2D\n序列帧", currentPreset == EnvPreset.Sprite2D))
                ApplyPreset(EnvPreset.Sprite2D);
            if (DrawPresetButton("⚙ 自定义", currentPreset == EnvPreset.Custom))
                ApplyPreset(EnvPreset.Custom);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);
        }

        private bool DrawPresetButton(string label, bool isActive)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 48,
                fixedWidth = 75,
                fontSize = 11,
                wordWrap = true
            };

            if (isActive)
            {
                int hash = 0; // 一个活动的预设按钮，复用同一纹理
                if (!cachedButtonBg.TryGetValue(hash, out var bgTex) || bgTex == null)
                {
                    bgTex = new Texture2D(1, 1);
                    bgTex.hideFlags = HideFlags.HideAndDontSave;
                    cachedButtonBg[hash] = bgTex;
                }
                bgTex.SetPixel(0, 0, new Color(0.28f, 0.52f, 0.9f, 1f));
                bgTex.Apply();
                style.normal.background = bgTex;
                style.normal.textColor = Color.white;
                style.fontStyle = FontStyle.Bold;
            }

            return GUILayout.Button(label, style);
        }

        #endregion

        #region 灯光面板

        private void DrawLightingSection()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("═══════ 灯光配置 ═══════", EditorStyles.centeredGreyMiniLabel);

            lightPreset = (LightPreset)EditorGUILayout.EnumPopup(
                new GUIContent("预设灯光", "选择灯光的预设方案"),
                lightPreset);
            DrawLightPresetDescription(lightPreset);

            if (lightPreset == LightPreset.None)
                return;

            EditorGUILayout.Space(2);

            // 颜色模式切换
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("灯光颜色模式", GUILayout.Width(90));
            if (GUILayout.Toggle(lightColorMode == LightColorMode.ColorTemperature, "色温模式", "Button", GUILayout.Width(80)))
                lightColorMode = LightColorMode.ColorTemperature;
            if (GUILayout.Toggle(lightColorMode == LightColorMode.ColorPicker, "拾色器", "Button", GUILayout.Width(70)))
                lightColorMode = LightColorMode.ColorPicker;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            // 三点光 或 柔光箱 时显示三灯参数
            if (lightPreset == LightPreset.ThreePoint || lightPreset == LightPreset.Softbox)
            {
                if (lightColorMode == LightColorMode.ColorTemperature)
                {
                    keyLightKelvin = EditorGUILayout.Slider("主光色温(K)", keyLightKelvin, 2700f, 10000f);
                    fillLightKelvin = EditorGUILayout.Slider("补光色温(K)", fillLightKelvin, 2700f, 10000f);
                    if (lightPreset == LightPreset.ThreePoint)
                        rimLightKelvin = EditorGUILayout.Slider("背光色温(K)", rimLightKelvin, 2700f, 10000f);
                }
                else
                {
                    keyLightColor = EditorGUILayout.ColorField("主光颜色", keyLightColor);
                    fillLightColor = EditorGUILayout.ColorField("补光颜色", fillLightColor);
                    if (lightPreset == LightPreset.ThreePoint)
                        rimLightColor = EditorGUILayout.ColorField("背光颜色", rimLightColor);
                }

                EditorGUILayout.Space(2);
                keyLightIntensity = EditorGUILayout.Slider("主光强度", keyLightIntensity, 0f, 3f);
                fillLightIntensity = EditorGUILayout.Slider("补光强度", fillLightIntensity, 0f, 2f);
                if (lightPreset == LightPreset.ThreePoint)
                    rimLightIntensity = EditorGUILayout.Slider("背光强度", rimLightIntensity, 0f, 2f);
            }
            else if (lightPreset == LightPreset.Environment)
            {
                if (lightColorMode == LightColorMode.ColorTemperature)
                {
                    keyLightKelvin = EditorGUILayout.Slider("环境光色温(K)", keyLightKelvin, 2700f, 10000f);
                }
                else
                {
                    keyLightColor = EditorGUILayout.ColorField("环境光颜色", keyLightColor);
                }
                keyLightIntensity = EditorGUILayout.Slider("环境光强度", keyLightIntensity, 0f, 3f);
            }

            enableShadow = EditorGUILayout.Toggle("启用阴影", enableShadow);
            EditorGUILayout.Space(4);
        }

        private void DrawLightPresetDescription(LightPreset preset)
        {
            string desc = preset switch
            {
                LightPreset.ThreePoint => "💡 推荐用于角色/模型展示，主光勾边+补光减影+背光分离背景",
                LightPreset.Softbox    => "📦 推荐用于产品/道具摄影，多方向柔光消除硬阴影",
                LightPreset.Environment => "✨ 推荐用于特效/2D序列帧，均匀无方向环境光",
                LightPreset.None       => "🌑 无灯光，适合纯自发光材质渲染",
                _                      => ""
            };
            EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedMiniLabel);
        }

        #endregion

        #region 后期处理面板

        private void DrawPostProcessSection()
        {
            EditorGUILayout.LabelField("═══════ 后期设置 ═══════", EditorStyles.centeredGreyMiniLabel);

            enablePostProcess = EditorGUILayout.Toggle(
                new GUIContent("启用后期处理", "为渲染相机添加URP Volume后处理效果"),
                enablePostProcess);
            DrawPostProcessDescription();

            if (!enablePostProcess)
            {
                EditorGUILayout.Space(4);
                return;
            }

            EditorGUI.indentLevel++;

            // Bloom（辉光）
            useBloom = EditorGUILayout.ToggleLeft(" Bloom 辉光 — 让高亮区域产生光晕，适合特效粒子", useBloom);
            if (useBloom)
            {
                EditorGUI.indentLevel++;
                bloomThreshold = EditorGUILayout.Slider("阈值", bloomThreshold, 0f, 2f);
                bloomIntensity = EditorGUILayout.Slider("强度", bloomIntensity, 0f, 2f);
                EditorGUI.indentLevel--;
            }

            // Tonemapping（色调映射）
            useTonemapping = EditorGUILayout.ToggleLeft(" Tonemapping 色调映射 — 将HDR映射到显示范围", useTonemapping);
            if (useTonemapping)
            {
                EditorGUI.indentLevel++;
                string[] tmModes = { "Neutral (中性)", "ACES (电影感)" };
                tonemappingMode = EditorGUILayout.Popup("模式", tonemappingMode, tmModes);
                EditorGUI.indentLevel--;
            }

            // Color Adjustments
            useColorAdjust = EditorGUILayout.ToggleLeft(" Color Adjustments 颜色校正 — 曝光/对比/饱和度", useColorAdjust);
            if (useColorAdjust)
            {
                EditorGUI.indentLevel++;
                postExposure = EditorGUILayout.Slider("曝光(EV)", postExposure, -3f, 3f);
                contrast = EditorGUILayout.Slider("对比度", contrast, -50f, 50f);
                saturation = EditorGUILayout.Slider("饱和度", saturation, -50f, 50f);
                EditorGUI.indentLevel--;
            }

            // Vignette
            useVignette = EditorGUILayout.ToggleLeft(" Vignette 暗角 — 画面边缘变暗，聚焦中心", useVignette);
            if (useVignette)
            {
                EditorGUI.indentLevel++;
                vignetteIntensity = EditorGUILayout.Slider("强度", vignetteIntensity, 0f, 1f);
                EditorGUI.indentLevel--;
            }

            // Depth of Field
            useDOF = EditorGUILayout.ToggleLeft(" Depth of Field 景深 — 仅产品展示推荐", useDOF);

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        private void DrawPostProcessDescription()
        {
            if (!enablePostProcess)
            {
                EditorGUILayout.LabelField("⚪ 后期关闭 — 渲染纯原始画面", EditorStyles.miniLabel);
                return;
            }
            int count = (useBloom ? 1 : 0) + (useTonemapping ? 1 : 0) + (useColorAdjust ? 1 : 0) + (useVignette ? 1 : 0) + (useDOF ? 1 : 0);
            if (count == 0)
                EditorGUILayout.LabelField("⚪ 已启用但未勾选效果 — 相当于关闭", EditorStyles.miniLabel);
            else
                EditorGUILayout.LabelField($"✅ 已启用 {count} 个后处理效果", EditorStyles.miniLabel);
        }

        #endregion

        #region 相机面板

        private void DrawCameraSection()
        {
            EditorGUILayout.LabelField("═══════ 相机配置 ═══════", EditorStyles.centeredGreyMiniLabel);

            EditorGUI.BeginDisabledGroup(currentPreset == EnvPreset.Sprite2D);
            useOrthographic = EditorGUILayout.Toggle("正交投影", useOrthographic);
            EditorGUI.EndDisabledGroup();

            if (currentPreset == EnvPreset.Sprite2D)
                useOrthographic = true;

            EditorGUILayout.Space(2);

            orbitAngle = EditorGUILayout.Slider("环绕角度", orbitAngle, 0f, 360f);
            cameraDistance = EditorGUILayout.Slider("相机距离", cameraDistance, 0.5f, 50f);
            cameraHeight = EditorGUILayout.Slider("相机高度", cameraHeight, -10f, 10f);
            pitchAngle = EditorGUILayout.Slider("俯仰角度", pitchAngle, -89f, 89f);

            EditorGUILayout.Space(2);

            // 分辨率预设
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("预览分辨率", GUILayout.Width(80));
            resolutionPresetIndex = EditorGUILayout.Popup(resolutionPresetIndex, resolutionPresets);
            EditorGUILayout.EndHorizontal();

            if (resolutionPresetIndex == 4) // 自定义
            {
                EditorGUILayout.BeginHorizontal();
                resolutionWidth = EditorGUILayout.IntField("宽", resolutionWidth);
                resolutionHeight = EditorGUILayout.IntField("高", resolutionHeight);
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                ApplyResolutionFromPreset(resolutionPresetIndex);
            }

            attachFreeLook = EditorGUILayout.Toggle("挂载自由旋转脚本", attachFreeLook);
            EditorGUILayout.Space(4);
        }

        private void ApplyResolutionFromPreset(int index)
        {
            switch (index)
            {
                case 0: resolutionWidth = 1920; resolutionHeight = 1080; break;
                case 1: resolutionWidth = 1024; resolutionHeight = 1024; break;
                case 2: resolutionWidth = 512; resolutionHeight = 512; break;
                case 3: resolutionWidth = 3840; resolutionHeight = 2160; break;
            }
        }

        private void UpdateResolutionPresetIndex()
        {
            if (resolutionWidth == 1920 && resolutionHeight == 1080) resolutionPresetIndex = 0;
            else if (resolutionWidth == 1024 && resolutionHeight == 1024) resolutionPresetIndex = 1;
            else if (resolutionWidth == 512 && resolutionHeight == 512) resolutionPresetIndex = 2;
            else if (resolutionWidth == 3840 && resolutionHeight == 2160) resolutionPresetIndex = 3;
            else resolutionPresetIndex = 4;
        }

        #endregion

        #region 背景面板

        private void DrawBackgroundSection()
        {
            EditorGUILayout.LabelField("═══════ 背景配置 ═══════", EditorStyles.centeredGreyMiniLabel);

            bgMode = (BackgroundMode)EditorGUILayout.EnumPopup(
                new GUIContent("背景模式", "选择背景的渲染模式"),
                bgMode);
            DrawBackgroundModeDescription(bgMode);

            if (bgMode == BackgroundMode.SolidColor || bgMode == BackgroundMode.Gradient)
            {
                bgColor = EditorGUILayout.ColorField("背景颜色", bgColor);
            }

            showFloor = EditorGUILayout.Toggle("显示地面", showFloor);
            showPedestal = EditorGUILayout.Toggle("展示台底座", showPedestal);
            EditorGUILayout.Space(4);
        }

        private void DrawBackgroundModeDescription(BackgroundMode mode)
        {
            string desc = mode switch
            {
                BackgroundMode.SolidColor   => "🎨 推荐用于模型/产品展示，干净统一背景色",
                BackgroundMode.Transparent  => "🫧 推荐用于2D序列帧/贴图导出，输出带Alpha通道",
                BackgroundMode.Gradient     => "🌈 渐变背景，用于风格化展示渲染",
                BackgroundMode.Skybox       => "🌌 使用场景天空盒，适合环境氛围渲染",
                BackgroundMode.Black        => "⬛ 推荐用于VFX特效，纯黑背景便于叠加合成",
                _                           => ""
            };
            EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedMiniLabel);
        }

        #endregion

        #region 输出面板

        private void DrawOutputSection()
        {
            EditorGUILayout.LabelField("═══════ 输出配置 ═══════", EditorStyles.centeredGreyMiniLabel);

            sceneName = EditorGUILayout.TextField("场景名称", sceneName);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("保存路径", GUILayout.Width(60));
            EditorGUILayout.SelectableLabel(SCENE_SAVE_DIR, GUILayout.Height(18));
            EditorGUILayout.EndHorizontal();

            GUILayout.Label("将新建独立场景文件，不会覆盖当前场景", EditorStyles.miniLabel);
            EditorGUILayout.Space(8);
        }

        #endregion

        #region 生成按钮 + 核心生成逻辑

        private void DrawGenerateButton()
        {
            EditorGUILayout.Space(4);

            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.28f, 0.7f, 0.28f);
            if (GUILayout.Button(" 🏗️  一键生成渲染场景", GUILayout.Height(44)))
            {
                // 延迟到下一帧执行，避免在GUI布局过程中切换场景
                EditorApplication.delayCall += GenerateScene;
            }
            GUI.backgroundColor = oldColor;

            EditorGUILayout.Space(8);
        }

        private void GenerateScene()
        {
            // 防止 delayCall 重复触发
            EditorApplication.delayCall -= GenerateScene;

            // 验证
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                EditorUtility.DisplayDialog("错误", "请输入场景名称", "确定");
                return;
            }

            // 确认
            if (!EditorUtility.DisplayDialog("确认",
                $"将在 {SCENE_SAVE_DIR} 创建新场景:\n\n{sceneName}.unity\n\n当前场景不会被修改。\n\n是否继续？",
                "生成", "取消"))
                return;

            try
            {
                // 保存当前场景（如果有修改）
                if (EditorSceneManager.GetActiveScene().isDirty)
                {
                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                }

                // 新建空场景
                var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // 构建渲染环境
                var root = new GameObject("Moshi_RenderEnv");

                CreateLighting(root);
                var cameraRig = CreateCameraRig(root);
                CreateStage(root);
                CreateBackground(root, cameraRig);
                CreatePostProcess(root, cameraRig);

                // 挂载标记组件到相机
                var renderCam = cameraRig.transform.Find("RenderCamera");
                if (renderCam != null)
                {
                    var tag = renderCam.gameObject.AddComponent<Moshi_RenderCameraTag>();
                    tag.preset = ConvertPreset(currentPreset);
                    tag.useTransparentBackground = (bgMode == BackgroundMode.Transparent);
                    tag.useOrthographic = useOrthographic;
                    tag.referenceWidth = resolutionWidth;
                    tag.referenceHeight = resolutionHeight;
                }

                // 保存场景
                if (!Directory.Exists(SCENE_SAVE_DIR))
                    Directory.CreateDirectory(SCENE_SAVE_DIR);

                string scenePath = SCENE_SAVE_DIR + sceneName + ".unity";
                EditorSceneManager.SaveScene(newScene, scenePath);
                AssetDatabase.Refresh();

                // 选中渲染环境根物体
                Selection.activeGameObject = root;

                EditorUtility.DisplayDialog("成功",
                    $"渲染场景已生成!\n\n路径: {scenePath}\n\n" +
                    "你可以拖入模型/特效到场景中，\n" +
                    "然后使用「工具/Moshi/逐帧渲染」进行帧序列输出。",
                    "确定");

                // 定位到场景文件
                var sceneAsset = AssetDatabase.LoadAssetAtPath<Object>(scenePath);
                if (sceneAsset != null)
                {
                    EditorGUIUtility.PingObject(sceneAsset);
                }
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("错误", $"生成场景失败:\n{ex.Message}", "确定");
                Debug.LogError($"Scene generation failed: {ex}");
            }
        }

        private Moshi_RenderCameraTag.RenderPreset ConvertPreset(EnvPreset p)
        {
            switch (p)
            {
                case EnvPreset.ModelTurntable: return Moshi_RenderCameraTag.RenderPreset.ModelTurntable;
                case EnvPreset.VFXStage: return Moshi_RenderCameraTag.RenderPreset.VFXStage;
                case EnvPreset.ProductShot: return Moshi_RenderCameraTag.RenderPreset.ProductShot;
                case EnvPreset.Sprite2D: return Moshi_RenderCameraTag.RenderPreset.Sprite2D;
                default: return Moshi_RenderCameraTag.RenderPreset.Custom;
            }
        }

        #endregion

        #region 灯光生成

        private void CreateLighting(GameObject root)
        {
            var lightingGroup = new GameObject("Lighting");
            lightingGroup.transform.SetParent(root.transform);

            // 设置环境光
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.1f, 0.1f, 0.1f);

            switch (lightPreset)
            {
                case LightPreset.ThreePoint:
                    CreateThreePointLighting(lightingGroup.transform);
                    break;
                case LightPreset.Softbox:
                    CreateSoftboxLighting(lightingGroup.transform);
                    break;
                case LightPreset.Environment:
                    CreateEnvironmentLight(lightingGroup.transform);
                    break;
            }
        }

        private void CreateThreePointLighting(Transform parent)
        {
            Color keyColor = GetLightColor(keyLightColor, keyLightKelvin);
            Color fillColor = GetLightColor(fillLightColor, fillLightKelvin);
            Color rimColor = GetLightColor(rimLightColor, rimLightKelvin);

            // 主光 (Key) — 右侧45°, 下俯30°
            var keyLight = CreateDirectionalLight(parent, "KeyLight", keyColor, keyLightIntensity);
            keyLight.transform.rotation = Quaternion.Euler(30, 45, 0);
            keyLight.shadows = enableShadow ? LightShadows.Soft : LightShadows.None;

            // 补光 (Fill) — 左侧30°, 水平偏上
            var fillLight = CreateDirectionalLight(parent, "FillLight", fillColor, fillLightIntensity);
            fillLight.transform.rotation = Quaternion.Euler(15, -30, 0);
            fillLight.shadows = LightShadows.None;

            // 背光 (Rim) — 后方
            var rimLight = CreateDirectionalLight(parent, "RimLight", rimColor, rimLightIntensity);
            rimLight.transform.rotation = Quaternion.Euler(20, 180, 0);
            rimLight.shadows = LightShadows.None;
        }

        private void CreateSoftboxLighting(Transform parent)
        {
            Color keyColor = GetLightColor(keyLightColor, keyLightKelvin);
            Color fillColor = GetLightColor(fillLightColor, fillLightKelvin);

            // 主光 — 上方大面积柔光
            var keyLight = CreateDirectionalLight(parent, "KeyLight_Top", keyColor, keyLightIntensity);
            keyLight.transform.rotation = Quaternion.Euler(60, 0, 0);
            keyLight.shadows = enableShadow ? LightShadows.Soft : LightShadows.None;

            // 补光 — 前方偏下
            var fillLight = CreateDirectionalLight(parent, "FillLight_Front", fillColor, fillLightIntensity);
            fillLight.transform.rotation = Quaternion.Euler(10, 0, 0);
            fillLight.shadows = LightShadows.None;

            // 左侧辅助光
            var leftLight = CreateDirectionalLight(parent, "AuxLight_Left", fillColor, fillLightIntensity * 0.7f);
            leftLight.transform.rotation = Quaternion.Euler(0, -90, 0);
            leftLight.shadows = LightShadows.None;

            // 右侧辅助光
            var rightLight = CreateDirectionalLight(parent, "AuxLight_Right", fillColor, fillLightIntensity * 0.7f);
            rightLight.transform.rotation = Quaternion.Euler(0, 90, 0);
            rightLight.shadows = LightShadows.None;
        }

        private void CreateEnvironmentLight(Transform parent)
        {
            Color envColor = GetLightColor(keyLightColor, keyLightKelvin);
            CreateDirectionalLight(parent, "AmbientLight", envColor, keyLightIntensity)
                .transform.rotation = Quaternion.Euler(90, 0, 0);
        }

        private Light CreateDirectionalLight(Transform parent, string name, Color color, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>
        /// 色温转RGB颜色（开尔文→sRGB近似）
        /// </summary>
        private Color TemperatureToColor(float kelvin)
        {
            kelvin = Mathf.Clamp(kelvin, 1000f, 40000f) / 100f;
            float r, g, b;

            if (kelvin <= 66f)
            {
                r = 255f;
                g = Mathf.Clamp(99.4708025861f * Mathf.Log(kelvin) - 161.1195681661f, 0f, 255f);
                b = kelvin <= 19f ? 0f : Mathf.Clamp(138.5177312231f * Mathf.Log(kelvin - 10f) - 305.0447927307f, 0f, 255f);
            }
            else
            {
                r = Mathf.Clamp(329.698727446f * Mathf.Pow(kelvin - 60f, -0.1332047592f), 0f, 255f);
                g = Mathf.Clamp(288.1221695283f * Mathf.Pow(kelvin - 60f, -0.0755148492f), 0f, 255f);
                b = 255f;
            }

            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        private Color GetLightColor(Color pickerColor, float kelvin)
        {
            return lightColorMode == LightColorMode.ColorTemperature
                ? TemperatureToColor(kelvin)
                : pickerColor;
        }

        #endregion

        #region 相机生成

        private GameObject CreateCameraRig(GameObject root)
        {
            var rig = new GameObject("CameraRig");
            rig.transform.SetParent(root.transform);
            rig.transform.position = Vector3.zero;

            // 根据环绕角度旋转rig
            rig.transform.rotation = Quaternion.Euler(0, orbitAngle, 0);

            var camGo = new GameObject("RenderCamera");
            camGo.transform.SetParent(rig.transform);

            // 计算相机在rig空间下的位置
            float radPitch = pitchAngle * Mathf.Deg2Rad;
            float x = 0;
            float y = Mathf.Sin(radPitch) * cameraDistance + cameraHeight;
            float z = -Mathf.Cos(radPitch) * cameraDistance;
            camGo.transform.localPosition = new Vector3(x, y, z);
            camGo.transform.LookAt(rig.transform.position + Vector3.up * cameraHeight * 0.3f);

            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = useOrthographic;
            if (useOrthographic)
            {
                cam.orthographicSize = 3f;
            }
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 1000f;

            // 背景设置
            switch (bgMode)
            {
                case BackgroundMode.SolidColor:
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = bgColor;
                    break;
                case BackgroundMode.Transparent:
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0, 0, 0, 0);
                    break;
                case BackgroundMode.Skybox:
                    cam.clearFlags = CameraClearFlags.Skybox;
                    break;
                case BackgroundMode.Black:
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black;
                    break;
                case BackgroundMode.Gradient:
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = bgColor;
                    break;
            }

            // 可选自由旋转脚本
            if (attachFreeLook)
            {
                camGo.AddComponent<SimpleCameraController>();
            }

            return rig;
        }

        #endregion

        #region 展示台生成

        private void CreateStage(GameObject root)
        {
            if (!showFloor && !showPedestal)
                return;

            var stageGroup = new GameObject("Stage");
            stageGroup.transform.SetParent(root.transform);

            if (showFloor)
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Floor";
                floor.transform.SetParent(stageGroup.transform);
                floor.transform.position = Vector3.zero;

                // 替换为无光照材质（白色）
                var renderer = floor.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.85f, 0.85f, 0.85f);
                    renderer.sharedMaterial = mat;
                }
            }

            if (showPedestal)
            {
                var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pedestal.name = "Pedestal";
                pedestal.transform.SetParent(stageGroup.transform);
                pedestal.transform.position = new Vector3(0, 0.05f, 0);
                pedestal.transform.localScale = new Vector3(1.5f, 0.05f, 1.5f);

                var renderer = pedestal.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.7f, 0.7f, 0.7f);
                    renderer.sharedMaterial = mat;
                }
            }

            // Turntable — 旋转台，用户把模型放这里
            var turntable = new GameObject("Turntable");
            turntable.transform.SetParent(stageGroup.transform);
            turntable.transform.position = new Vector3(0, showPedestal ? 0.1f : 0, 0);
        }

        #endregion

        #region 背景板生成

        private void CreateBackground(GameObject root, GameObject cameraRig)
        {
            if (bgMode == BackgroundMode.Gradient)
            {
                var bgGroup = new GameObject("Background");
                bgGroup.transform.SetParent(root.transform);

                var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
                backdrop.name = "Backdrop";
                backdrop.transform.SetParent(bgGroup.transform);

                // 将背景板放在相机后方远处
                if (cameraRig != null)
                {
                    var cam = cameraRig.transform.Find("RenderCamera");
                    if (cam != null)
                    {
                        Vector3 camForward = cam.forward;
                        backdrop.transform.position = cam.position + camForward * 20f;
                        backdrop.transform.rotation = Quaternion.LookRotation(camForward);
                    }
                }

                backdrop.transform.localScale = new Vector3(30, 20, 1);

                // 使用无光照shader作渐变底色
                var renderer = backdrop.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Unlit/Color"));
                    mat.color = bgColor;
                    renderer.sharedMaterial = mat;
                }
            }
        }

        #endregion

        #region 后期处理生成

        private void CreatePostProcess(GameObject root, GameObject cameraRig)
        {
            if (!enablePostProcess) return;

            // 检查是否有相机
            var camGo = cameraRig != null ? cameraRig.transform.Find("RenderCamera") : null;
            if (camGo == null) return;

            // 在相机下创建 Volume GameObject
            var volumeGo = new GameObject("PostProcessVolume");
            volumeGo.transform.SetParent(root.transform);

            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0;

            // 创建 Profile
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            if (useBloom)
            {
                var bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(bloomThreshold);
                bloom.intensity.Override(bloomIntensity);
                bloom.scatter.Override(0.7f);
                bloom.clamp.Override(65472f);
                bloom.tint.Override(Color.white);
            }

            if (useTonemapping)
            {
                var tm = profile.Add<Tonemapping>(true);
                tm.mode.Override(tonemappingMode == 0
                    ? TonemappingMode.Neutral
                    : TonemappingMode.ACES);
            }

            if (useColorAdjust)
            {
                var ca = profile.Add<ColorAdjustments>(true);
                ca.postExposure.Override(postExposure);
                ca.contrast.Override(contrast);
                ca.saturation.Override(saturation);
                ca.colorFilter.Override(Color.white);
            }

            if (useVignette)
            {
                var vg = profile.Add<Vignette>(true);
                vg.intensity.Override(vignetteIntensity);
                vg.smoothness.Override(0.3f);
                vg.rounded.Override(false);
                vg.color.Override(Color.black);
            }

            if (useDOF)
            {
                var dof = profile.Add<DepthOfField>(true);
                dof.mode.Override(DepthOfFieldMode.Gaussian);
                dof.gaussianStart.Override(1f);
                dof.gaussianEnd.Override(5f);
                dof.gaussianMaxRadius.Override(1f);
            }

            volume.sharedProfile = profile;

            // 确保相机上有 Universal Additional Camera Data (URP 必需)
            var cam = camGo.GetComponent<Camera>();
            if (cam != null)
            {
                var camData = camGo.GetComponent<UniversalAdditionalCameraData>();
                if (camData == null)
                    camGo.gameObject.AddComponent<UniversalAdditionalCameraData>();

                // 开启后处理
                camData = camGo.GetComponent<UniversalAdditionalCameraData>();
                if (camData != null)
                {
                    camData.renderPostProcessing = true;
                    camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    camData.stopNaN = false;
                    camData.dithering = false;
                }
            }
        }

        #endregion
    }
}
