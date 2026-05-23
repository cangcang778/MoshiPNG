using UnityEngine;
using UnityEditor;
using Moshi.PathTool;

namespace Moshi.PathTool.Editor
{
    /// <summary>
    /// 路径阵列循环工具窗口
    /// 可视化配置路径阵列：选择路径+模型，一键生成沿路径排列并循环运动的阵列
    /// 支持三轴旋转曲线(含扭曲)、透明渐变、大小渐变
    /// </summary>
    public class Moshi_PathArrayWindow : EditorWindow
    {
        // ========== 路径 ==========
        private PathCreator pathCreator;

        // ========== 阵列 ==========
        private GameObject prefab;
        private int instanceCount = 8;

        // ========== 运动 ==========
        private float speed = 1f;
        private ArrayCycleMode cycleMode = ArrayCycleMode.Loop;
        private bool alignToPath = true;
        private Vector3 perInstanceRotation = Vector3.zero;
        private float rotationSpeed = 0f;

        // ========== 旋转曲线 ==========
        private bool enableRotationCurve = false;
        private AnimationCurve rotationXOverPath = AnimationCurve.Constant(0f, 1f, 0f);
        private AnimationCurve rotationYOverPath = AnimationCurve.Constant(0f, 1f, 0f);
        private AnimationCurve rotationZOverPath = AnimationCurve.Constant(0f, 1f, 0f);

        // ========== 透明渐变 ==========
        private bool enableAlphaGradient = false;
        private AnimationCurve alphaOverPath = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        // ========== 大小渐变 ==========
        private bool enableScaleGradient = false;
        private AnimationCurve scaleOverPath = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        // ========== 生长动画 ==========
        private bool enableGrowAnimation = false;
        private float growthDuration = 0f;

        // ========== 预览 ==========
        private float previewPhase = 0f;
        private bool isPreviewPlaying = false;
        private double lastPreviewTime = 0;

        // ========== 引用 ==========
        private PathArrayCycler activeCycler;

        private Vector2 scrollPos;
        private bool showRotationSection = true;
        private bool showAlphaSection = false;
        private bool showScaleSection = false;
        private bool showGrowSection = false;

        [MenuItem("工具/Moshi/路径工具/路径阵列循环", false, 5)]
        public static void ShowWindow()
        {
            var window = GetWindow<Moshi_PathArrayWindow>("路径阵列循环");
            window.minSize = new Vector2(420, 700);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            SceneView.duringSceneGui += OnSceneGUI;
            AutoDetectPath();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            SceneView.duringSceneGui -= OnSceneGUI;
            isPreviewPlaying = false;
        }

        private void AutoDetectPath()
        {
            if (pathCreator == null)
                pathCreator = Object.FindObjectOfType<PathCreator>();

            if (activeCycler == null)
                activeCycler = Object.FindObjectOfType<PathArrayCycler>();

            if (activeCycler != null)
            {
                pathCreator = activeCycler.pathCreator;
                prefab = activeCycler.prefab;
                instanceCount = activeCycler.count;
                speed = activeCycler.speed;
                cycleMode = activeCycler.cycleMode;
                alignToPath = activeCycler.alignToPath;
                perInstanceRotation = activeCycler.perInstanceRotation;
                rotationSpeed = activeCycler.rotationSpeed;
                enableRotationCurve = activeCycler.enableRotationCurve;
                rotationXOverPath = activeCycler.rotationXOverPath;
                rotationYOverPath = activeCycler.rotationYOverPath;
                rotationZOverPath = activeCycler.rotationZOverPath;
                enableAlphaGradient = activeCycler.enableAlphaGradient;
                alphaOverPath = activeCycler.alphaOverPath;
                enableScaleGradient = activeCycler.enableScaleGradient;
                scaleOverPath = activeCycler.scaleOverPath;
                enableGrowAnimation = activeCycler.enableGrowAnimation;
                growthDuration = activeCycler.growthDuration;
            }
        }

        private void OnEditorUpdate()
        {
            if (!isPreviewPlaying || activeCycler == null) return;

            double deltaTime = EditorApplication.timeSinceStartup - lastPreviewTime;
            lastPreviewTime = EditorApplication.timeSinceStartup;

            if (deltaTime > 0 && deltaTime < 0.5)
            {
                previewPhase += (float)(speed * deltaTime);
                if (cycleMode == ArrayCycleMode.Loop)
                    previewPhase %= 1f;
                else
                    previewPhase = Mathf.Clamp01(previewPhase);

                activeCycler.SetPhase(previewPhase);
                SceneView.RepaintAll();
                Repaint();
            }
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // ========== 标题 ==========
            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("路径阵列循环", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            MoshiHelpButton.DrawHelpButtonMini("路径阵列循环");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("将模型沿路径均匀排列，驱动循环运动", EditorStyles.miniLabel);

            GUILayout.Space(10);

            // ========== 路径设置 ==========
            DrawSectionHeader("路径设置");
            EditorGUI.BeginChangeCheck();
            pathCreator = (PathCreator)EditorGUILayout.ObjectField(
                new GUIContent("目标路径", "场景中的 PathCreator 组件"),
                pathCreator, typeof(PathCreator), true);
            if (EditorGUI.EndChangeCheck() && activeCycler != null)
                activeCycler.pathCreator = pathCreator;

            if (pathCreator != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"路径名: {pathCreator.pathName}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"点数: {pathCreator.pathPoints.Count}  |  "
                    + $"总长: {pathCreator.GetPathLength():F2}  |  "
                    + $"闭合: {(pathCreator.closePath ? "√" : "×")}", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            else
            {
                if (GUILayout.Button("从场景中选取路径"))
                {
                    var paths = Object.FindObjectsOfType<PathCreator>();
                    if (paths.Length == 0)
                        EditorUtility.DisplayDialog("提示", "场景中没有 PathCreator", "确定");
                    else
                        pathCreator = paths[0];
                }
            }

            GUILayout.Space(6);

            // ========== 阵列配置 ==========
            DrawSectionHeader("阵列配置");

            EditorGUI.BeginChangeCheck();
            prefab = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("阵列模板", "沿路径排列的模型/特效预制体"),
                prefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck() && activeCycler != null)
                activeCycler.prefab = prefab;

            EditorGUI.BeginChangeCheck();
            instanceCount = EditorGUILayout.IntSlider(
                new GUIContent("实例数量", "沿路径均匀分布的对象数量"),
                instanceCount, 1, 200);
            if (pathCreator != null)
                EditorGUILayout.LabelField(
                    $"  → 间距: {(pathCreator.GetPathLength() / instanceCount).ToString("F2")} 单位",
                    EditorStyles.miniLabel);
            if (EditorGUI.EndChangeCheck() && activeCycler != null)
                activeCycler.count = instanceCount;

            GUILayout.Space(6);

            // ========== 运动设置 ==========
            DrawSectionHeader("运动设置");

            EditorGUI.BeginChangeCheck();
            speed = EditorGUILayout.Slider(
                new GUIContent("循环速度", "每秒完成多少圈完整循环"),
                speed, 0.01f, 10f);
            if (EditorGUI.EndChangeCheck() && activeCycler != null)
                activeCycler.speed = speed;

            EditorGUI.BeginChangeCheck();
            cycleMode = (ArrayCycleMode)EditorGUILayout.EnumPopup(
                new GUIContent("运动模式", "Loop: 持续循环 | PingPong: 来回往返 | Manual: 手动相位"),
                cycleMode);
            DrawCycleModeDescription(cycleMode);
            if (EditorGUI.EndChangeCheck() && activeCycler != null)
                activeCycler.cycleMode = cycleMode;

            EditorGUI.BeginChangeCheck();
            alignToPath = EditorGUILayout.Toggle(
                new GUIContent("朝向路径", "对象是否旋转以对齐路径切线方向"),
                alignToPath);
            if (EditorGUI.EndChangeCheck() && activeCycler != null)
                activeCycler.alignToPath = alignToPath;

            GUILayout.Space(6);

            // ========== 旋转设置（可折叠） ==========
            showRotationSection = EditorGUILayout.Foldout(showRotationSection, "旋转设置（含扭曲）", true);
            if (showRotationSection)
            {
                EditorGUI.indentLevel++;

                // 固定偏移旋转
                EditorGUI.BeginChangeCheck();
                perInstanceRotation = EditorGUILayout.Vector3Field(
                    new GUIContent("偏移旋转", "所有实例统一的 Euler 角度偏移(X/Y/Z轴)，叠加在曲线之前"),
                    perInstanceRotation);
                if (EditorGUI.EndChangeCheck() && activeCycler != null)
                {
                    activeCycler.perInstanceRotation = perInstanceRotation;
                    activeCycler.UpdateAllPositions();
                    SceneView.RepaintAll();
                }

                // 自旋速度
                EditorGUI.BeginChangeCheck();
                rotationSpeed = EditorGUILayout.Slider(
                    new GUIContent("自旋速度", "每秒旋转度数（绕自身Z轴），正=逆时针，负=顺时针"),
                    rotationSpeed, -720f, 720f);
                if (EditorGUI.EndChangeCheck() && activeCycler != null)
                    activeCycler.rotationSpeed = rotationSpeed;

                GUILayout.Space(4);

                // 旋转曲线开关
                EditorGUI.BeginChangeCheck();
                enableRotationCurve = EditorGUILayout.Toggle(
                    new GUIContent("启用旋转曲线", "用三条独立曲线分别控制沿路径的 X/Y/Z 旋转角度"),
                    enableRotationCurve);
                if (EditorGUI.EndChangeCheck() && activeCycler != null)
                {
                    activeCycler.enableRotationCurve = enableRotationCurve;
                    activeCycler.UpdateAllPositions();
                    SceneView.RepaintAll();
                }

                if (enableRotationCurve)
                {
                    // RX
                    EditorGUI.BeginChangeCheck();
                    rotationXOverPath = EditorGUILayout.CurveField(
                        new GUIContent("X轴旋转(Pitch)", "横轴=路径位置(0→1), 纵轴=俯仰角度(度)"),
                        rotationXOverPath,
                        Color.red,
                        new Rect(0f, -180f, 1f, 360f));
                    if (EditorGUI.EndChangeCheck()) ApplyRotationCurve();

                    // RY
                    EditorGUI.BeginChangeCheck();
                    rotationYOverPath = EditorGUILayout.CurveField(
                        new GUIContent("Y轴旋转(Yaw)", "横轴=路径位置(0→1), 纵轴=偏航角度(度)"),
                        rotationYOverPath,
                        Color.green,
                        new Rect(0f, -180f, 1f, 360f));
                    if (EditorGUI.EndChangeCheck()) ApplyRotationCurve();

                    // RZ = Twist
                    EditorGUI.BeginChangeCheck();
                    rotationZOverPath = EditorGUILayout.CurveField(
                        new GUIContent("Z轴旋转(Roll/扭曲)", "横轴=路径位置(0→1), 纵轴=翻滚/扭曲角度(度)\n绕路径切线方向扭转实例"),
                        rotationZOverPath,
                        Color.blue,
                        new Rect(0f, -360f, 1f, 720f));
                    if (EditorGUI.EndChangeCheck()) ApplyRotationCurve();

                    // 扭曲快捷预设
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("扭曲预设:", GUILayout.Width(60));
                    if (GUILayout.Button("螺旋1圈", GUILayout.Height(20)))
                        SetTwistPreset(360f);
                    if (GUILayout.Button("螺旋2圈", GUILayout.Height(20)))
                        SetTwistPreset(720f);
                    if (GUILayout.Button("反向螺旋1圈", GUILayout.Height(20)))
                        SetTwistPreset(-360f);
                    if (GUILayout.Button("零扭曲", GUILayout.Height(20)))
                        SetTwistPreset(0f);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("综合预设:", GUILayout.Width(60));
                    if (GUILayout.Button("全轴归零", GUILayout.Height(20)))
                        ResetAllRotationCurves();
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }

            GUILayout.Space(6);

            // ========== 透明渐变（可折叠） ==========
            showAlphaSection = EditorGUILayout.Foldout(showAlphaSection, "透明渐变", true);
            if (showAlphaSection)
            {
                EditorGUI.indentLevel++;

                EditorGUI.BeginChangeCheck();
                enableAlphaGradient = EditorGUILayout.Toggle(
                    new GUIContent("启用透明渐变", "沿路径改变实例透明度"),
                    enableAlphaGradient);
                if (EditorGUI.EndChangeCheck() && activeCycler != null)
                {
                    activeCycler.enableAlphaGradient = enableAlphaGradient;
                    activeCycler.UpdateAllPositions();
                    SceneView.RepaintAll();
                }

                if (enableAlphaGradient)
                {
                    EditorGUI.BeginChangeCheck();
                    alphaOverPath = EditorGUILayout.CurveField(
                        new GUIContent("透明度曲线", "横轴=路径位置(左0起点→右1终点)\n纵轴=Alpha(0透明→1不透明)"),
                        alphaOverPath,
                        Color.cyan,
                        new Rect(0f, 0f, 1f, 1f));
                    if (EditorGUI.EndChangeCheck() && activeCycler != null)
                    {
                        activeCycler.alphaOverPath = alphaOverPath;
                        activeCycler.UpdateAllPositions();
                        SceneView.RepaintAll();
                    }

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("快捷:", GUILayout.Width(40));
                    if (GUILayout.Button("淡入", GUILayout.Height(20))) SetAlphaPreset(0f, 1f);
                    if (GUILayout.Button("淡出", GUILayout.Height(20))) SetAlphaPreset(1f, 0f);
                    if (GUILayout.Button("两端淡入淡出", GUILayout.Height(20))) SetAlphaPresetV();
                    if (GUILayout.Button("全不透明", GUILayout.Height(20))) SetAlphaPreset(1f, 1f);
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }

            GUILayout.Space(6);

            // ========== 大小渐变（可折叠） ==========
            showScaleSection = EditorGUILayout.Foldout(showScaleSection, "大小渐变", true);
            if (showScaleSection)
            {
                EditorGUI.indentLevel++;

                EditorGUI.BeginChangeCheck();
                enableScaleGradient = EditorGUILayout.Toggle(
                    new GUIContent("启用大小渐变", "沿路径改变实例缩放"),
                    enableScaleGradient);
                if (EditorGUI.EndChangeCheck() && activeCycler != null)
                {
                    activeCycler.enableScaleGradient = enableScaleGradient;
                    activeCycler.UpdateAllPositions();
                    SceneView.RepaintAll();
                }

                if (enableScaleGradient)
                {
                    EditorGUI.BeginChangeCheck();
                    scaleOverPath = EditorGUILayout.CurveField(
                        new GUIContent("缩放曲线", "横轴=路径位置(左0起点→右1终点)\n纵轴=缩放倍率"),
                        scaleOverPath,
                        Color.green,
                        new Rect(0f, 0f, 1f, 3f));
                    if (EditorGUI.EndChangeCheck() && activeCycler != null)
                    {
                        activeCycler.scaleOverPath = scaleOverPath;
                        activeCycler.UpdateAllPositions();
                        SceneView.RepaintAll();
                    }

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("快捷:", GUILayout.Width(40));
                    if (GUILayout.Button("中间大", GUILayout.Height(20))) SetScalePresetMidBig();
                    if (GUILayout.Button("中间小", GUILayout.Height(20))) SetScalePresetMidSmall();
                    if (GUILayout.Button("由小变大", GUILayout.Height(20))) SetScalePreset(0.3f, 1.5f);
                    if (GUILayout.Button("统一大小", GUILayout.Height(20))) SetScalePreset(1f, 1f);
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }

            GUILayout.Space(6);

            // ========== 生长动画（可折叠） ==========
            showGrowSection = EditorGUILayout.Foldout(showGrowSection, "生长动画", true);
            if (showGrowSection)
            {
                EditorGUI.indentLevel++;

                EditorGUI.BeginChangeCheck();
                enableGrowAnimation = EditorGUILayout.Toggle(
                    new GUIContent("启用生长动画", "实例按路径位置顺序逐个出现（scale: 0→正常值），模拟生长效果"),
                    enableGrowAnimation);
                if (EditorGUI.EndChangeCheck() && activeCycler != null)
                {
                    activeCycler.enableGrowAnimation = enableGrowAnimation;
                    activeCycler.UpdateAllPositions();
                    SceneView.RepaintAll();
                }

                if (enableGrowAnimation)
                {
                    EditorGUI.BeginChangeCheck();
                    growthDuration = EditorGUILayout.Slider(
                        new GUIContent("生长时长(秒)", "所有实例完全出现所需时间，0=自动使用1圈时间"),
                        growthDuration, 0f, 30f);
                    if (EditorGUI.EndChangeCheck() && activeCycler != null)
                    {
                        activeCycler.growthDuration = growthDuration;
                        activeCycler.UpdateAllPositions();
                        SceneView.RepaintAll();
                    }

                    float actualGrowTime = growthDuration > 0.001f ? growthDuration : (activeCycler != null ? (1f / Mathf.Max(activeCycler.speed, 0.001f)) : 1f);
                    EditorGUILayout.LabelField(
                        $"  → 首个实例立即出现，末尾实例 {actualGrowTime:F2}s 后出现",
                        EditorStyles.miniLabel);
                }

                EditorGUI.indentLevel--;
            }

            GUILayout.Space(6);

            // ========== 预览 ==========
            DrawSectionHeader("编辑器预览");

            if (activeCycler == null || activeCycler.instances.Count == 0)
            {
                EditorGUILayout.HelpBox("点击下方「生成阵列」后可预览", MessageType.Info);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                previewPhase = EditorGUILayout.Slider("预览相位", previewPhase, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    activeCycler.SetPhase(previewPhase);
                    SceneView.RepaintAll();
                }

                EditorGUILayout.BeginHorizontal();
                float[] phases = { 0f, 0.25f, 0.5f, 0.75f };
                string[] labels = { "起点", "25%", "50%", "75%" };
                for (int i = 0; i < phases.Length; i++)
                {
                    if (GUILayout.Button(labels[i], GUILayout.Height(22)))
                    {
                        previewPhase = phases[i];
                        activeCycler.SetPhase(previewPhase);
                        SceneView.RepaintAll();
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = isPreviewPlaying ? Color.red : Color.green;
                if (GUILayout.Button(isPreviewPlaying ? "⏸ 暂停" : "▶ 播放", GUILayout.Height(28)))
                {
                    isPreviewPlaying = !isPreviewPlaying;
                    lastPreviewTime = EditorApplication.timeSinceStartup;
                    if (!isPreviewPlaying)
                        activeCycler.SetPhase(previewPhase);
                }
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("🔄 重置", GUILayout.Height(28)))
                {
                    isPreviewPlaying = false;
                    previewPhase = 0f;
                    activeCycler.SetPhase(0f);
                    SceneView.RepaintAll();
                }
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            // ========== 生成/清除 ==========
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.28f, 0.7f, 0.28f);
            if (GUILayout.Button("🏗️ 生成阵列", GUILayout.Height(40)))
            {
                if (pathCreator == null || pathCreator.pathPoints.Count < 2)
                    EditorUtility.DisplayDialog("错误", "请先选择有效的路径（至少2个点）", "确定");
                else if (prefab == null)
                    EditorUtility.DisplayDialog("错误", "请先指定阵列模板(GameObject)", "确定");
                else
                    GenerateOrUpdateArray();
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("🗑️ 清除", GUILayout.Height(40)))
            {
                if (activeCycler != null)
                {
                    Undo.RecordObject(activeCycler, "Clear Path Array");
                    activeCycler.ClearInstances();
                }
                isPreviewPlaying = false;
                previewPhase = 0f;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (activeCycler != null && activeCycler.instances.Count > 0)
            {
                EditorGUILayout.LabelField(
                    $"√ 已生成 {activeCycler.instances.Count} 个实例 → "
                    + $"GameObject: {activeCycler.gameObject.name}",
                    EditorStyles.miniLabel);

                if (GUILayout.Button("定位到管理器"))
                {
                    Selection.activeGameObject = activeCycler.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        // =================================================================
        // 帮助方法
        // =================================================================

        private void DrawSectionHeader(string title)
        {
            EditorGUILayout.LabelField($"═══ {title} ═══", EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawCycleModeDescription(ArrayCycleMode mode)
        {
            string desc = mode switch
            {
                ArrayCycleMode.Loop => "🔄 持续循环 — 到达终点后无缝回到起点（推荐）",
                ArrayCycleMode.PingPong => "↔️ 来回往返 — 到终点后反向运动",
                ArrayCycleMode.Manual => "🎚️ 手动控制 — 通过相位滑块精确控制位置",
                _ => ""
            };
            EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedMiniLabel);
        }

        // ---- 旋转曲线同步 ----

        private void ApplyRotationCurve()
        {
            if (activeCycler == null) return;
            activeCycler.rotationXOverPath = rotationXOverPath;
            activeCycler.rotationYOverPath = rotationYOverPath;
            activeCycler.rotationZOverPath = rotationZOverPath;
            activeCycler.UpdateAllPositions();
            SceneView.RepaintAll();
        }

        private void SetTwistPreset(float totalDegrees)
        {
            rotationZOverPath = AnimationCurve.Linear(0f, 0f, 1f, totalDegrees);
            rotationXOverPath = AnimationCurve.Constant(0f, 1f, 0f);
            rotationYOverPath = AnimationCurve.Constant(0f, 1f, 0f);
            ApplyRotationCurve();
        }

        private void ResetAllRotationCurves()
        {
            rotationXOverPath = AnimationCurve.Constant(0f, 1f, 0f);
            rotationYOverPath = AnimationCurve.Constant(0f, 1f, 0f);
            rotationZOverPath = AnimationCurve.Constant(0f, 1f, 0f);
            ApplyRotationCurve();
        }

        // ---- 透明度预设 ----

        private void SetAlphaPreset(float startAlpha, float endAlpha)
        {
            alphaOverPath = AnimationCurve.Linear(0f, startAlpha, 1f, endAlpha);
            if (activeCycler != null)
            {
                activeCycler.alphaOverPath = alphaOverPath;
                activeCycler.UpdateAllPositions();
                SceneView.RepaintAll();
            }
        }

        private void SetAlphaPresetV()
        {
            alphaOverPath = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0f)
            );
            if (activeCycler != null)
            {
                activeCycler.alphaOverPath = alphaOverPath;
                activeCycler.UpdateAllPositions();
                SceneView.RepaintAll();
            }
        }

        // ---- 缩放预设 ----

        private void SetScalePreset(float startScale, float endScale)
        {
            scaleOverPath = AnimationCurve.Linear(0f, startScale, 1f, endScale);
            if (activeCycler != null)
            {
                activeCycler.scaleOverPath = scaleOverPath;
                activeCycler.UpdateAllPositions();
                SceneView.RepaintAll();
            }
        }

        private void SetScalePresetMidBig()
        {
            scaleOverPath = new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.5f, 1.5f), new Keyframe(1f, 0.5f));
            if (activeCycler != null)
            {
                activeCycler.scaleOverPath = scaleOverPath;
                activeCycler.UpdateAllPositions();
                SceneView.RepaintAll();
            }
        }

        private void SetScalePresetMidSmall()
        {
            scaleOverPath = new AnimationCurve(
                new Keyframe(0f, 1.5f), new Keyframe(0.5f, 0.5f), new Keyframe(1f, 1.5f));
            if (activeCycler != null)
            {
                activeCycler.scaleOverPath = scaleOverPath;
                activeCycler.UpdateAllPositions();
                SceneView.RepaintAll();
            }
        }

        // =================================================================
        // 生成 / 场景绘制
        // =================================================================

        private void GenerateOrUpdateArray()
        {
            if (activeCycler == null)
            {
                var go = new GameObject("PathArrayCycler");
                Undo.RegisterCreatedObjectUndo(go, "Create PathArrayCycler");
                activeCycler = Undo.AddComponent<PathArrayCycler>(go);
            }

            Undo.RecordObject(activeCycler, "Generate Path Array");

            activeCycler.pathCreator = pathCreator;
            activeCycler.prefab = prefab;
            activeCycler.count = instanceCount;
            activeCycler.speed = speed;
            activeCycler.cycleMode = cycleMode;
            activeCycler.alignToPath = alignToPath;
            activeCycler.perInstanceRotation = perInstanceRotation;
            activeCycler.rotationSpeed = rotationSpeed;
            activeCycler.enableRotationCurve = enableRotationCurve;
            activeCycler.rotationXOverPath = rotationXOverPath;
            activeCycler.rotationYOverPath = rotationYOverPath;
            activeCycler.rotationZOverPath = rotationZOverPath;
            activeCycler.enableAlphaGradient = enableAlphaGradient;
            activeCycler.alphaOverPath = alphaOverPath;
            activeCycler.enableScaleGradient = enableScaleGradient;
            activeCycler.scaleOverPath = scaleOverPath;
            activeCycler.enableGrowAnimation = enableGrowAnimation;
            activeCycler.growthDuration = growthDuration;

            activeCycler.GenerateInstances();

            EditorUtility.SetDirty(activeCycler);
            isPreviewPlaying = false;
            previewPhase = 0f;

            Selection.activeGameObject = activeCycler.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            EditorGUIUtility.PingObject(activeCycler.gameObject);

            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (activeCycler == null || activeCycler.instances.Count == 0)
                return;

            Handles.color = new Color(0.5f, 0.8f, 1f, 0.3f);
            for (int i = 0; i < activeCycler.instances.Count; i++)
            {
                if (activeCycler.instances[i] == null) continue;
                int next = (i + 1) % activeCycler.instances.Count;
                if (activeCycler.instances[next] == null) continue;

                Handles.DrawDottedLine(
                    activeCycler.instances[i].transform.position,
                    activeCycler.instances[next].transform.position,
                    3f);
            }

            if (activeCycler.instances.Count > 0 && activeCycler.instances[0] != null)
            {
                Handles.color = Color.yellow;
                Handles.SphereHandleCap(0,
                    activeCycler.instances[0].transform.position,
                    Quaternion.identity,
                    HandleUtility.GetHandleSize(activeCycler.instances[0].transform.position) * 0.15f,
                    EventType.Repaint);
            }
        }
    }
}
