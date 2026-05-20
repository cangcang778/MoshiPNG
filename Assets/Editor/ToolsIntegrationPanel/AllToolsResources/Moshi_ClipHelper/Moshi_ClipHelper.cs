using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// AnimationClip 辅助工具 - 提供关键帧操作、时间轴控制、曲线优化等功能
/// 支持 Unity 2021.3+，兼容 Humanoid 和 Generic 动画类型
/// </summary>
public class Moshi_ClipHelper : EditorWindow
{
    #region 枚举定义
    
    public enum ToolTab
    {
        KeyframeOps,      // 关键帧操作
        CurveOptimize,    // 曲线优化
        CurveConvert      // 曲线转换
    }

    public enum CurveOptimizeMode
    {
        SmartOptimize,    // 智能优化（Douglas-Peucker算法）
        FlipHorizontal,   // 水平翻转（时间轴翻转）
        FlipVertical      // 垂直翻转（值翻转）
    }

    /// <summary>
    /// 批量操作偏移模式
    /// </summary>
    public enum BatchOffsetMode
    {
        Incremental,      // 递增偏移：每个对象比前一个多偏移固定值
        Uniform,          // 统一偏移：所有对象偏移相同时间
        EvenDistribute    // 均匀分布：在指定时间范围内均匀排开
    }

    /// <summary>
    /// 已有曲线处理模式
    /// </summary>
    public enum ExistingCurveMode
    {
        Overwrite,    // 覆盖：完全替换已有曲线
        Merge,        // 合并：保留已有关键帧，相同时间点用新值覆盖
        Skip          // 跳过：如果已有曲线则不处理
    }

    /// <summary>
    /// 曲线转换方法
    /// </summary>
    public enum ConversionMethod
    {
        CumulativeVelocity,  // 累积速度法(推荐)
        InstantVelocity,     // 瞬时速度法
        AverageVelocity      // 平均速度法
    }

    /// <summary>
    /// 曲线优化数据源类型
    /// </summary>
    public enum CurveOptimizeSource
    {
        AnimationClip,       // 动画剪辑
        ParticleSystem       // 粒子系统
    }

    /// <summary>
    /// 翻转范围选择
    /// </summary>
    public enum FlipScope
    {
        AllCurves,           // 翻转所有曲线
        SelectedFromWindow,  // 翻转Animation窗口选中的曲线
        SingleProperty       // 翻转单个属性
    }

    #endregion

    #region 数据结构

    [System.Serializable]
    public class ClipInfo
    {
        public AnimationClip clip;
        public float length;
        public float frameRate;
        public int keyframeCount;
        public int curveCount;
        public bool isLooping;
        public bool isHumanoid;
        public List<string> propertyPaths = new List<string>();
    }

    [System.Serializable]
    public class KeyframeData
    {
        public float time;
        public float value;
        public float inTangent;
        public float outTangent;
        public WeightedMode weightedMode;
        public float inWeight;
        public float outWeight;
    }

    /// <summary>
    /// 曲线分组 - 按对象路径分组的位置曲线
    /// </summary>
    [System.Serializable]
    public class CurveGroup
    {
        public string path;           // 对象路径
        public string displayName;    // 显示名称
        public EditorCurveBinding? bindingX;
        public EditorCurveBinding? bindingY;
        public EditorCurveBinding? bindingZ;
        
        public bool HasAllAxes => bindingX.HasValue && bindingY.HasValue && bindingZ.HasValue;
        public int AxisCount => (bindingX.HasValue ? 1 : 0) + (bindingY.HasValue ? 1 : 0) + (bindingZ.HasValue ? 1 : 0);
    }

    /// <summary>
    /// 粒子系统曲线信息
    /// </summary>
    [System.Serializable]
    public class ParticleCurveInfo
    {
        public ParticleSystem particleSystem;            // 所属粒子系统
        public string particleSystemName;                // 粒子系统名称
        public string moduleName;                        // 模块名称
        public string propertyName;                      // 属性名称
        public string displayName;                       // 显示名称
        public ParticleSystemCurveMode mode;             // 曲线模式
        public AnimationCurve curve;                     // 主曲线（Curve模式）
        public AnimationCurve curveMin;                  // 最小曲线（TwoCurves模式）
        public float multiplier;                         // 乘数
        public bool isSelected;                          // 是否选中优化
        public int originalKeyCount;                     // 原始关键帧数
        public int optimizedKeyCount;                    // 优化后关键帧数
        public System.Action<AnimationCurve, AnimationCurve, float> applyCurve;  // 应用曲线回调
        
        public bool CanOptimize => mode == ParticleSystemCurveMode.Curve || mode == ParticleSystemCurveMode.TwoCurves;
        public int TotalKeyCount => (curve?.keys.Length ?? 0) + (curveMin?.keys.Length ?? 0);
    }

    #endregion

    #region 字段

    private ToolTab currentTab = ToolTab.KeyframeOps;
    private Vector2 scrollPosition;
    
    // Animation 窗口反射相关
    private System.Type animationWindowType;
    private System.Type animationWindowStateType;
    private AnimationClip activeClipFromWindow;
    private List<EditorCurveBinding> selectedBindingsFromWindow = new List<EditorCurveBinding>();
    private List<float> selectedKeyTimesFromWindow = new List<float>();
    private bool hasValidSelection = false;
    
    // 动画剪辑选择
    private AnimationClip sourceClip;
    private ClipInfo sourceClipInfo;
    
    // 关键帧操作参数 - 层级偏移
    private float offsetValue = 0.1f;             // 偏移时间(秒)
    private int offsetFrames = 1;                 // 偏移帧数
    private bool useFrameOffsetMode = false;      // 是否使用每帧模式
    private bool useCyclicOffset = false;         // 是否使用循环偏移
    private bool useFixedCycleLength = false;     // 是否使用固定循环帧长
    private bool useCycleLengthFrameMode = true;  // 循环帧长输入模式：true=帧数, false=秒
    private int cycleLengthFrames = 60;           // 固定循环帧长（帧数）
    private float cycleLengthSeconds = 1f;        // 固定循环帧长（秒）
    private int selectedPropertyIndex = 0;
    private List<EditorCurveBinding> availableBindings = new List<EditorCurveBinding>();
    

    
    // 曲线优化参数
    private CurveOptimizeMode curveOptMode = CurveOptimizeMode.SmartOptimize;
    
    // 翻转参数
    private FlipScope flipScope = FlipScope.SelectedFromWindow;  // 默认使用窗口选中
    private float verticalFlipCenter = 0f;
    private bool autoCalculateCenter = false;  // 默认不自动计算，使用0作为中心值
    
    // 智能曲线优化参数
    private bool enableSmartOptimizeFeature = true;  // 功能启用开关
    private float curveOptimizeTolerance = 0.001f;   // 优化误差容忍度
    private bool optimizeSelectedOnly = false;       // 仅优化选中曲线
    
    // UI 样式
    private GUIStyle headerStyle;
    private GUIStyle boxStyle;
    private bool stylesInitialized = false;

    // 自动刷新相关
    private AnimationClip lastActiveClip;
    private int lastSelectedCurvesCount;
    private int lastSelectedKeysCount;
    private double lastCheckTime;
    private const double CHECK_INTERVAL = 0.1; // 检查间隔（秒）

    // 批量粘贴相关
    private List<GameObject> selectedHierarchyObjects = new List<GameObject>();
    private bool autoSortByName = true;
    private Vector2 hierarchyScrollPosition;

    // 批量操作偏移模式
    private BatchOffsetMode batchOffsetMode = BatchOffsetMode.Incremental;  // 偏移模式
    private float evenDistributeTotalSeconds = 1f;   // 均匀分布总时长（秒）
    private int evenDistributeTotalFrames = 30;      // 均匀分布总时长（帧）
    
    // 批量操作功能开关（独立控制）
    private bool enableBatchPaste = true;    // 启用粘贴功能
    private bool enableBatchOffset = true;   // 启用偏移功能
    
    // 已有曲线处理模式
    private ExistingCurveMode existingCurveMode = ExistingCurveMode.Overwrite;
    
    // 轴过滤（批量粘贴时只处理指定轴）
    private bool enableAxisFilter = false;    // 是否启用轴过滤
    private bool filterAxisX = true;          // 处理X轴
    private bool filterAxisY = true;          // 处理Y轴
    private bool filterAxisZ = true;          // 处理Z轴
    private bool filterAxisW = true;          // 处理W轴（四元数旋转、颜色Alpha等）

    // ===== 曲线转换相关字段 =====
    private ParticleSystem targetParticleSystem;
    private ConversionMethod conversionMethod = ConversionMethod.CumulativeVelocity;
    private int convertSampleRate = 60;
    private bool useHighPrecision = true;
    private Vector3 velocityMultiplier = Vector3.one;

    // 曲线分组（基于Animation窗口选中的曲线自动分析）
    private List<CurveGroup> curveGroups = new List<CurveGroup>();
    private int selectedGroupIndex = 0;
    private bool curveGroupsScanned = false;

    // 转换结果
    private AnimationCurve resultXCurve;
    private AnimationCurve resultYCurve;
    private AnimationCurve resultZCurve;
    private float xMultiplier = 1.0f;
    private float yMultiplier = 1.0f;
    private float zMultiplier = 1.0f;

    // 曲线转换滚动位置
    private Vector2 convertScrollPos;

    // ===== 粒子系统曲线优化相关字段 =====
    private CurveOptimizeSource curveOptimizeSource = CurveOptimizeSource.AnimationClip;
    private List<ParticleSystem> optimizeTargetParticleSystems = new List<ParticleSystem>();  // 支持批量选中
    private bool particleSystemListFoldout = true;  // 粒子系统列表折叠状态
    private List<ParticleCurveInfo> particleCurveInfos = new List<ParticleCurveInfo>();
    private Vector2 particleCurveScrollPos;
    private bool particleCurvesScanned = false;
    private bool particleSelectAll = false;
    private float particleOptimizeTolerance = 0.001f;
    private Dictionary<string, bool> particleModuleFoldouts = new Dictionary<string, bool>();
    
    // 粒子系统优化模式
    private CurveOptimizeMode particleOptMode = CurveOptimizeMode.SmartOptimize;
    private bool particleFlipAllCurves = false;
    private float particleVerticalFlipCenter = 0f;
    private bool particleAutoCalculateCenter = false;
    
    // 自动扫描追踪字段
    private int lastParticleSystemsCount = 0;      // 追踪上一次的粒子系统数量
    private int lastParticleSystemsHash = 0;       // 追踪粒子系统列表的哈希值
    private CurveOptimizeSource lastCurveOptimizeSource = (CurveOptimizeSource)(-1);  // 初始化为无效值，确保首次进入时触发
    private bool particleSystemUIInitialized = false;             // 追踪粒子系统UI是否已初始化
    
    // 曲线模式变化检测
    private Dictionary<string, ParticleSystemCurveMode> lastCurveModes = new Dictionary<string, ParticleSystemCurveMode>();
    
    private const string TOOL_NAME = "动画片段助手";

    #endregion

    #region 窗口初始化

    [MenuItem("工具/Moshi/" + TOOL_NAME)]
    public static void ShowWindow()
    {
        var window = GetWindow<Moshi_ClipHelper>(TOOL_NAME);
        window.minSize = new Vector2(450, 600);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
        Selection.selectionChanged += OnSelectionChanged;
        InitAnimationWindowReflection();
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Selection.selectionChanged -= OnSelectionChanged;
    }
    
    /// <summary>
    /// Hierarchy 选择变化时的回调 - 支持批量选中粒子系统
    /// </summary>
    private void OnSelectionChanged()
    {
        // 只在粒子系统数据源模式下自动更新
        if (curveOptimizeSource != CurveOptimizeSource.ParticleSystem)
            return;
        
        // 从所有选中的 GameObject 中获取粒子系统
        var selectedParticleSystems = new List<ParticleSystem>();
        
        foreach (var go in Selection.gameObjects)
        {
            // 获取选中对象及其所有子对象上的粒子系统
            var particleSystems = go.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particleSystems)
            {
                if (!selectedParticleSystems.Contains(ps))
                {
                    selectedParticleSystems.Add(ps);
                }
            }
        }
        
        if (selectedParticleSystems.Count > 0)
        {
            optimizeTargetParticleSystems = selectedParticleSystems;
            // 自动扫描
            ScanAllParticleSystemsCurves();
            Repaint();
        }
    }
    
    /// <summary>
    /// Undo/Redo 事件回调 - 用于检测粒子系统属性变化
    /// </summary>
    private void OnUndoRedoPerformed()
    {
        // 只在粒子系统模式下响应
        if (curveOptimizeSource == CurveOptimizeSource.ParticleSystem && optimizeTargetParticleSystems.Count > 0)
        {
            // 重新扫描粒子系统曲线
            ScanAllParticleSystemsCurves();
            particleCurvesScanned = true;
            Repaint();
        }
    }

    private void InitStyles()
    {
        if (stylesInitialized) return;
        
        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            margin = new RectOffset(0, 0, 10, 10)
        };
        
        boxStyle = new GUIStyle("box")
        {
            padding = new RectOffset(10, 10, 10, 10),
            margin = new RectOffset(5, 5, 5, 5)
        };
        
        stylesInitialized = true;
    }

    #endregion

    #region GUI 绘制

    private void OnGUI()
    {
        InitStyles();
        
        DrawHeader();
        DrawTabBar();
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        switch (currentTab)
        {
            case ToolTab.KeyframeOps:
                DrawKeyframeOpsTab();
                break;
            case ToolTab.CurveOptimize:
                DrawCurveOptimizeTab();
                break;
            case ToolTab.CurveConvert:
                DrawCurveConvertTab();
                break;
        }
        
        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("AnimationClip 辅助工具", headerStyle);
        GUILayout.FlexibleSpace();
        MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("支持 Humanoid & Generic 动画", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
    }

    private void DrawTabBar()
    {
        EditorGUILayout.BeginHorizontal();
        
        string[] tabNames = { "关键帧操作", "曲线优化", "曲线转换" };
        
        for (int i = 0; i < tabNames.Length; i++)
        {
            GUI.backgroundColor = (currentTab == (ToolTab)i) ? Color.cyan : Color.white;
            if (GUILayout.Button(tabNames[i], GUILayout.Height(30)))
            {
                currentTab = (ToolTab)i;
            }
        }
        
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
    }

    private void DrawClipSelector(string label, ref AnimationClip clip, ref ClipInfo clipInfo)
    {
        EditorGUILayout.BeginVertical(boxStyle);
        
        EditorGUI.BeginChangeCheck();
        clip = (AnimationClip)EditorGUILayout.ObjectField(label, clip, typeof(AnimationClip), false);
        
        if (EditorGUI.EndChangeCheck() && clip != null)
        {
            clipInfo = AnalyzeClip(clip);
            RefreshBindings(clip);
        }
        
        if (clip != null && clipInfo != null)
        {
            EditorGUILayout.Space(5);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"时长: {clipInfo.length:F3}s | 帧率: {clipInfo.frameRate} FPS");
            EditorGUILayout.LabelField($"曲线数: {clipInfo.curveCount} | 关键帧总数: {clipInfo.keyframeCount}");
            EditorGUILayout.LabelField($"循环: {(clipInfo.isLooping ? "是" : "否")} | 类型: {(clipInfo.isHumanoid ? "Humanoid" : "Generic")}");
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
    }

    #endregion

    #region 关键帧操作 Tab

    private void DrawKeyframeOpsTab()
    {
        // 显示当前 Animation 窗口状态
        DrawAnimationWindowStatus();
        
        // 获取当前工作剪辑
        AnimationClip workingClip = GetWorkingClip();
        
        if (workingClip == null)
        {
            EditorGUILayout.HelpBox("请在 Animation 窗口中选择动画剪辑", MessageType.Info);
            return;
        }
        
        EditorGUILayout.Space(10);
        
        DrawLayerOffsetUI();
    }

    private void DrawLayerOffsetUI()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("关键帧时间偏移", EditorStyles.boldLabel);
        
        EditorGUILayout.HelpBox("先在Animation窗口中手动Ctrl+C复制关键帧，粘贴后使用此功能偏移时间", MessageType.Info);
        
        AnimationClip workingClip = GetWorkingClip();
        float frameRate = workingClip != null ? workingClip.frameRate : 30f;
        
        // 单位模式选择
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("单位", GUILayout.Width(50));
        if (GUILayout.Toggle(!useFrameOffsetMode, "按秒", EditorStyles.miniButtonLeft, GUILayout.Width(60)))
            useFrameOffsetMode = false;
        if (GUILayout.Toggle(useFrameOffsetMode, "按帧", EditorStyles.miniButtonRight, GUILayout.Width(60)))
            useFrameOffsetMode = true;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        // 偏移值设置
        float actualOffset;
        if (useFrameOffsetMode)
        {
            offsetFrames = EditorGUILayout.IntField("偏移值", offsetFrames);
            actualOffset = offsetFrames / frameRate;
            EditorGUILayout.LabelField($"  = {actualOffset:F4} 秒 (帧率: {frameRate} FPS)", EditorStyles.miniLabel);
        }
        else
        {
            offsetValue = EditorGUILayout.FloatField("偏移值", offsetValue);
            actualOffset = offsetValue;
            float frames = offsetValue * frameRate;
            EditorGUILayout.LabelField($"  = {frames:F2} 帧 (帧率: {frameRate} FPS)", EditorStyles.miniLabel);
        }
        
        EditorGUILayout.Space(5);
        
        // 偏移模式选择（递增/统一/均匀分布）
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("偏移模式", GUILayout.Width(60));
        if (GUILayout.Toggle(batchOffsetMode == BatchOffsetMode.Incremental, "递增", EditorStyles.miniButtonLeft))
            batchOffsetMode = BatchOffsetMode.Incremental;
        if (GUILayout.Toggle(batchOffsetMode == BatchOffsetMode.Uniform, "统一", EditorStyles.miniButtonMid))
            batchOffsetMode = BatchOffsetMode.Uniform;
        if (GUILayout.Toggle(batchOffsetMode == BatchOffsetMode.EvenDistribute, "均匀分布", EditorStyles.miniButtonRight))
            batchOffsetMode = BatchOffsetMode.EvenDistribute;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        // 均匀分布模式下显示总时长设置
        if (batchOffsetMode == BatchOffsetMode.EvenDistribute)
        {
            EditorGUILayout.Space(3);
            if (useFrameOffsetMode)
            {
                evenDistributeTotalFrames = EditorGUILayout.IntField("总时长(帧)", evenDistributeTotalFrames);
                if (evenDistributeTotalFrames < 1) evenDistributeTotalFrames = 1;
                EditorGUILayout.LabelField($"  = {evenDistributeTotalFrames / frameRate:F4} 秒", EditorStyles.miniLabel);
            }
            else
            {
                evenDistributeTotalSeconds = EditorGUILayout.FloatField("总时长(秒)", evenDistributeTotalSeconds);
                if (evenDistributeTotalSeconds < 0.001f) evenDistributeTotalSeconds = 0.001f;
                EditorGUILayout.LabelField($"  = {evenDistributeTotalSeconds * frameRate:F1} 帧", EditorStyles.miniLabel);
            }
        }
        
        EditorGUILayout.Space(5);
        
        // 循环偏移选项
        useCyclicOffset = EditorGUILayout.Toggle("循环偏移", useCyclicOffset);
        if (useCyclicOffset)
        {
            EditorGUI.indentLevel++;
            
            // 固定帧长选项
            useFixedCycleLength = EditorGUILayout.Toggle("固定循环帧长", useFixedCycleLength);
            
            if (useFixedCycleLength)
            {
                // 帧长输入模式选择
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("帧长模式", GUILayout.Width(80));
                if (GUILayout.Toggle(useCycleLengthFrameMode, "按帧", EditorStyles.miniButtonLeft, GUILayout.Width(60)))
                    useCycleLengthFrameMode = true;
                if (GUILayout.Toggle(!useCycleLengthFrameMode, "按秒", EditorStyles.miniButtonRight, GUILayout.Width(60)))
                    useCycleLengthFrameMode = false;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                
                float cycleLength;
                if (useCycleLengthFrameMode)
                {
                    cycleLengthFrames = EditorGUILayout.IntField("循环帧长(帧)", cycleLengthFrames);
                    if (cycleLengthFrames < 2) cycleLengthFrames = 2;
                    cycleLength = cycleLengthFrames / frameRate;
                    EditorGUILayout.LabelField($"  = {cycleLength:F4} 秒 (帧率: {frameRate} FPS)", EditorStyles.miniLabel);
                }
                else
                {
                    cycleLengthSeconds = EditorGUILayout.FloatField("循环帧长(秒)", cycleLengthSeconds);
                    if (cycleLengthSeconds < 0.01f) cycleLengthSeconds = 0.01f;
                    cycleLength = cycleLengthSeconds;
                    float frames = cycleLengthSeconds * frameRate;
                    EditorGUILayout.LabelField($"  = {frames:F2} 帧 (帧率: {frameRate} FPS)", EditorStyles.miniLabel);
                }
                
                EditorGUILayout.HelpBox(
                    "固定循环帧长模式：\n" +
                    "• 首尾帧值相同，确保循环无缝\n" +
                    "• 偏移后自动从原曲线插值计算首尾帧值\n" +
                    $"• 有效周期 = {cycleLength:F4}秒 - 1帧", 
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "自动循环模式：\n" +
                    "• 循环周期 = 动画长度 - 1帧\n" +
                    "• 偏移超出周期时会循环回到开头", 
                    MessageType.Info);
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.Space(10);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("向后偏移 (+)", GUILayout.Height(30)))
        {
            ApplyLayerOffset(Mathf.Abs(actualOffset));
        }
        if (GUILayout.Button("向前偏移 (-)", GUILayout.Height(30)))
        {
            ApplyLayerOffset(-Mathf.Abs(actualOffset));
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        
        // ========== 批量操作功能 ==========
        EditorGUILayout.Space(10);
        DrawBatchPasteUI(actualOffset, frameRate);
    }

    /// <summary>
    /// 绘制批量操作UI
    /// </summary>
    private void DrawBatchPasteUI(float offsetPerLayer, float frameRate)
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("批量操作", EditorStyles.boldLabel);
        
        // 功能选择开关（独立控制）
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("功能选择:", GUILayout.Width(60));
        enableBatchPaste = EditorGUILayout.ToggleLeft("粘贴", enableBatchPaste, GUILayout.Width(60));
        enableBatchOffset = EditorGUILayout.ToggleLeft("偏移", enableBatchOffset, GUILayout.Width(60));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        // 已有曲线处理选项（仅在启用粘贴时显示）
        if (enableBatchPaste)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("已有曲线:", GUILayout.Width(60));
            if (GUILayout.Toggle(existingCurveMode == ExistingCurveMode.Overwrite, "覆盖", EditorStyles.miniButtonLeft, GUILayout.Width(50)))
                existingCurveMode = ExistingCurveMode.Overwrite;
            if (GUILayout.Toggle(existingCurveMode == ExistingCurveMode.Merge, "合并", EditorStyles.miniButtonMid, GUILayout.Width(50)))
                existingCurveMode = ExistingCurveMode.Merge;
            if (GUILayout.Toggle(existingCurveMode == ExistingCurveMode.Skip, "跳过", EditorStyles.miniButtonRight, GUILayout.Width(50)))
                existingCurveMode = ExistingCurveMode.Skip;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        
        // 轴过滤选项（粘贴和偏移都可用）
        if (enableBatchPaste || enableBatchOffset)
        {
            EditorGUILayout.BeginHorizontal();
            enableAxisFilter = EditorGUILayout.ToggleLeft("轴过滤:", enableAxisFilter, GUILayout.Width(70));
            GUI.enabled = enableAxisFilter;
            filterAxisX = EditorGUILayout.ToggleLeft("X", filterAxisX, GUILayout.Width(35));
            filterAxisY = EditorGUILayout.ToggleLeft("Y", filterAxisY, GUILayout.Width(35));
            filterAxisZ = EditorGUILayout.ToggleLeft("Z", filterAxisZ, GUILayout.Width(35));
            filterAxisW = EditorGUILayout.ToggleLeft("W", filterAxisW, GUILayout.Width(35));
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            if (enableAxisFilter)
            {
                string hint = enableBatchPaste ? "仅处理勾选的轴，其他轴保持目标原有值" : "仅偏移勾选的轴";
                EditorGUILayout.LabelField("  " + hint, EditorStyles.miniLabel);
            }
        }
        
        // 功能说明
        string functionDesc = "";
        if (enableBatchPaste && enableBatchOffset)
            functionDesc = "复制曲线到目标对象，并应用时间偏移";
        else if (enableBatchPaste && !enableBatchOffset)
            functionDesc = "仅复制曲线到目标对象，不改变时间";
        else if (!enableBatchPaste && enableBatchOffset)
            functionDesc = "仅对目标对象已有的同名曲线进行时间偏移";
        else
            functionDesc = "请至少选择一个功能";
        
        // 追加已有曲线处理说明
        if (enableBatchPaste)
        {
            switch (existingCurveMode)
            {
                case ExistingCurveMode.Overwrite:
                    functionDesc += "（覆盖已有曲线）";
                    break;
                case ExistingCurveMode.Merge:
                    functionDesc += "（与已有曲线合并）";
                    break;
                case ExistingCurveMode.Skip:
                    functionDesc += "（跳过已有曲线）";
                    break;
            }
        }
        
        EditorGUILayout.LabelField(functionDesc, EditorStyles.miniLabel);
        
        EditorGUILayout.Space(5);
        
        // 刷新按钮和自动排序选项
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("刷新选中对象", GUILayout.Width(100)))
        {
            RefreshHierarchySelection();
        }
        autoSortByName = EditorGUILayout.Toggle("按Hierarchy顺序排序", autoSortByName);
        EditorGUILayout.EndHorizontal();
        
        // 显示当前Animation窗口选中的曲线
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"选中曲线: {selectedBindingsFromWindow.Count} 条", EditorStyles.miniLabel);
        
        // 显示选中的Hierarchy对象
        RefreshHierarchySelection();
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"目标对象: {selectedHierarchyObjects.Count} 个", EditorStyles.boldLabel);
        
        if (selectedHierarchyObjects.Count > 0)
        {
            // 滚动视图显示选中的对象
            float listHeight = Mathf.Min(selectedHierarchyObjects.Count * 20f, 100f);
            hierarchyScrollPosition = EditorGUILayout.BeginScrollView(
                hierarchyScrollPosition, 
                GUILayout.Height(listHeight + 10));
            
            EditorGUI.indentLevel++;
            for (int i = 0; i < selectedHierarchyObjects.Count; i++)
            {
                var obj = selectedHierarchyObjects[i];
                if (obj != null)
                {
                    // 根据偏移模式计算显示的偏移量
                    float layerOffset = CalculateLayerOffset(i, selectedHierarchyObjects.Count, offsetPerLayer, frameRate);
                    string offsetStr;
                    if (useFrameOffsetMode)
                    {
                        int offsetInFrames = Mathf.RoundToInt(layerOffset * frameRate);
                        offsetStr = $"+{offsetInFrames}帧";
                    }
                    else
                    {
                        offsetStr = $"+{layerOffset:F3}s";
                    }
                    
                    // 如果不启用偏移，显示无偏移
                    if (!enableBatchOffset)
                        offsetStr = "(无偏移)";
                    
                    EditorGUILayout.LabelField($"{i + 1}. {obj.name} ({offsetStr})");
                }
            }
            EditorGUI.indentLevel--;
            
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.HelpBox("请在Hierarchy面板选择目标对象", MessageType.Warning);
        }
        
        EditorGUILayout.Space(10);
        
        // 执行按钮
        bool canExecute = (enableBatchPaste || enableBatchOffset) && selectedHierarchyObjects.Count > 0;
        if (enableBatchPaste)
            canExecute = canExecute && selectedBindingsFromWindow.Count > 0;
        
        GUI.enabled = canExecute;
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        
        // 根据功能组合显示不同的按钮文字
        string buttonText = "执行";
        if (enableBatchPaste && enableBatchOffset)
            buttonText = "▶ 批量粘贴并偏移";
        else if (enableBatchPaste)
            buttonText = "▶ 批量粘贴";
        else if (enableBatchOffset)
            buttonText = "▶ 批量偏移";
        
        if (GUILayout.Button(buttonText, GUILayout.Height(35)))
        {
            ApplyBatchOperation(offsetPerLayer, frameRate);
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
        
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 计算指定对象的偏移时间（根据偏移模式）
    /// </summary>
    private float CalculateLayerOffset(int objectIndex, int totalObjects, float baseOffset, float frameRate)
    {
        switch (batchOffsetMode)
        {
            case BatchOffsetMode.Incremental:
                // 递增偏移：每个对象比前一个多偏移固定值
                return objectIndex * baseOffset;
                
            case BatchOffsetMode.Uniform:
                // 统一偏移：所有对象偏移相同时间
                return baseOffset;
                
            case BatchOffsetMode.EvenDistribute:
                // 均匀分布：在指定时间范围内均匀排开
                if (totalObjects <= 1) return 0;
                float totalTime = useFrameOffsetMode 
                    ? evenDistributeTotalFrames / frameRate 
                    : evenDistributeTotalSeconds;
                return (objectIndex / (float)(totalObjects - 1)) * totalTime;
                
            default:
                return objectIndex * baseOffset;
        }
    }

    private void DrawPropertySelector()
    {
        if (availableBindings.Count > 0)
        {
            EditorGUILayout.Space(5);
            string[] propertyNames = availableBindings.Select(b => $"{b.path}/{b.propertyName}").ToArray();
            selectedPropertyIndex = EditorGUILayout.Popup("选择属性", selectedPropertyIndex, propertyNames);
            
            if (selectedPropertyIndex >= propertyNames.Length)
                selectedPropertyIndex = 0;
        }
        else
        {
            EditorGUILayout.HelpBox("未找到可用的动画属性", MessageType.Warning);
        }
    }

    #endregion

    #region 曲线优化 Tab

    private void DrawCurveOptimizeTab()
    {
        // 数据源选择
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("数据源", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = curveOptimizeSource == CurveOptimizeSource.AnimationClip ? Color.cyan : Color.white;
        if (GUILayout.Button("Animation Clip", GUILayout.Height(25)))
        {
            curveOptimizeSource = CurveOptimizeSource.AnimationClip;
        }
        GUI.backgroundColor = curveOptimizeSource == CurveOptimizeSource.ParticleSystem ? Color.cyan : Color.white;
        if (GUILayout.Button("Particle System", GUILayout.Height(25)))
        {
            curveOptimizeSource = CurveOptimizeSource.ParticleSystem;
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(5);
        
        // 检测是否刚切换到粒子系统数据源
        bool justSwitchedToParticle = (curveOptimizeSource == CurveOptimizeSource.ParticleSystem && 
                                        lastCurveOptimizeSource != CurveOptimizeSource.ParticleSystem);
        lastCurveOptimizeSource = curveOptimizeSource;
        
        // 根据数据源显示不同的UI
        if (curveOptimizeSource == CurveOptimizeSource.AnimationClip)
        {
            // 切换到 AnimationClip 时，重置粒子系统UI初始化标记
            particleSystemUIInitialized = false;
            DrawAnimationClipOptimizeUI();
        }
        else
        {
            DrawParticleSystemOptimizeUI(justSwitchedToParticle);
        }
    }
    
    /// <summary>
    /// 绘制 Animation Clip 优化UI（原有逻辑）
    /// </summary>
    private void DrawAnimationClipOptimizeUI()
    {
        // 显示当前 Animation 窗口状态
        DrawAnimationWindowStatus();
        
        // 获取当前工作剪辑
        AnimationClip workingClip = GetWorkingClip();
        
        if (workingClip == null)
        {
            EditorGUILayout.HelpBox("请在 Animation 窗口中选择动画剪辑", MessageType.Info);
            return;
        }
        
        EditorGUILayout.Space(10);
        
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("优化模式", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("选择模式", GUILayout.Width(60));
        if (GUILayout.Toggle(curveOptMode == CurveOptimizeMode.SmartOptimize, "智能优化", EditorStyles.miniButtonLeft))
            curveOptMode = CurveOptimizeMode.SmartOptimize;
        if (GUILayout.Toggle(curveOptMode == CurveOptimizeMode.FlipHorizontal, "水平翻转", EditorStyles.miniButtonMid))
            curveOptMode = CurveOptimizeMode.FlipHorizontal;
        if (GUILayout.Toggle(curveOptMode == CurveOptimizeMode.FlipVertical, "垂直翻转", EditorStyles.miniButtonRight))
            curveOptMode = CurveOptimizeMode.FlipVertical;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(5);
        
        switch (curveOptMode)
        {
            case CurveOptimizeMode.SmartOptimize:
                DrawSmartOptimizeUI();
                break;
            case CurveOptimizeMode.FlipHorizontal:
                DrawFlipHorizontalUI();
                break;
            case CurveOptimizeMode.FlipVertical:
                DrawFlipVerticalUI();
                break;
        }
    }
    
    /// <summary>
    /// 绘制粒子系统曲线优化UI
    /// </summary>
    private void DrawParticleSystemOptimizeUI(bool justSwitchedToParticle = false)
    {
        // 粒子系统选择
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("粒子系统选择", EditorStyles.boldLabel);
        
        // 显示已选中的粒子系统数量
        EditorGUILayout.LabelField($"已选中: {optimizeTargetParticleSystems.Count} 个粒子系统", EditorStyles.miniLabel);
        
        // 可折叠的粒子系统列表
        if (optimizeTargetParticleSystems.Count > 0)
        {
            particleSystemListFoldout = EditorGUILayout.Foldout(particleSystemListFoldout, "粒子系统列表", true);
            if (particleSystemListFoldout)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < optimizeTargetParticleSystems.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    optimizeTargetParticleSystems[i] = (ParticleSystem)EditorGUILayout.ObjectField(
                        optimizeTargetParticleSystems[i], typeof(ParticleSystem), true);
                    
                    // 移除按钮
                    if (GUILayout.Button("×", GUILayout.Width(20)))
                    {
                        optimizeTargetParticleSystems.RemoveAt(i);
                        i--;
                        ScanAllParticleSystemsCurves();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }
        
        // 操作按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("从 Hierarchy 获取选中", GUILayout.Height(22)))
        {
            OnSelectionChanged();
        }
        if (GUILayout.Button("清空列表", GUILayout.Height(22)))
        {
            optimizeTargetParticleSystems.Clear();
            particleCurveInfos.Clear();
            particleCurvesScanned = false;
        }
        EditorGUILayout.EndHorizontal();
        
        // 计算当前列表的哈希值用于检测变化
        int currentHash = GetParticleSystemsHash();
        bool particleSystemsChanged = (currentHash != lastParticleSystemsHash);
        bool needsInitialScan = (!particleSystemUIInitialized && optimizeTargetParticleSystems.Count > 0 && !particleCurvesScanned);
        
        if (optimizeTargetParticleSystems.Count > 0)
        {
            if (justSwitchedToParticle || particleSystemsChanged || needsInitialScan)
            {
                lastParticleSystemsHash = currentHash;
                lastParticleSystemsCount = optimizeTargetParticleSystems.Count;
                ScanAllParticleSystemsCurves();
                particleCurvesScanned = true;
                lastCurveModes = GetCurrentCurveModes();
                Repaint();
            }
        }
        else if (particleSystemsChanged)
        {
            // 粒子系统列表被清空
            lastParticleSystemsHash = 0;
            lastParticleSystemsCount = 0;
            particleCurvesScanned = false;
            particleCurveInfos.Clear();
            lastCurveModes.Clear();
        }
        
        // 标记UI已初始化
        particleSystemUIInitialized = true;
        
        if (optimizeTargetParticleSystems.Count == 0)
        {
            EditorGUILayout.HelpBox("请在 Hierarchy 中选择包含粒子系统的对象，将自动获取所有粒子系统", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }
        
        EditorGUILayout.EndVertical();;
        
        EditorGUILayout.Space(5);
        
        // 优化模式选择
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("优化模式", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("选择模式", GUILayout.Width(60));
        if (GUILayout.Toggle(particleOptMode == CurveOptimizeMode.SmartOptimize, "智能优化", EditorStyles.miniButtonLeft))
            particleOptMode = CurveOptimizeMode.SmartOptimize;
        if (GUILayout.Toggle(particleOptMode == CurveOptimizeMode.FlipHorizontal, "水平翻转", EditorStyles.miniButtonMid))
            particleOptMode = CurveOptimizeMode.FlipHorizontal;
        if (GUILayout.Toggle(particleOptMode == CurveOptimizeMode.FlipVertical, "垂直翻转", EditorStyles.miniButtonRight))
            particleOptMode = CurveOptimizeMode.FlipVertical;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(5);
        
        // 根据模式显示不同UI
        switch (particleOptMode)
        {
            case CurveOptimizeMode.SmartOptimize:
                DrawParticleSmartOptimizeUI();
                break;
            case CurveOptimizeMode.FlipHorizontal:
                DrawParticleFlipHorizontalUI();
                break;
            case CurveOptimizeMode.FlipVertical:
                DrawParticleFlipVerticalUI();
                break;
        }
    }
    
    /// <summary>
    /// 绘制粒子系统智能优化UI
    /// </summary>
    private void DrawParticleSmartOptimizeUI()
    {
        // 扫描并匹配曲线
        DrawParticleCurveScanUI();
        
        if (!particleCurvesScanned || particleCurveInfos.Count == 0)
        {
            EditorGUILayout.HelpBox("点击\"扫描所有曲线\"按钮以分析粒子系统", MessageType.Info);
            return;
        }
        
        EditorGUILayout.Space(5);
        
        // 曲线列表
        DrawParticleCurveListUI();
        
        EditorGUILayout.Space(5);
        
        // 优化参数
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("优化参数", EditorStyles.boldLabel);
        
        // 容忍度设置
        particleOptimizeTolerance = EditorGUILayout.Slider("误差容忍度", particleOptimizeTolerance, 0.0001f, 0.05f);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("精确", GUILayout.Width(50))) particleOptimizeTolerance = 0.0001f;
        if (GUILayout.Button("标准", GUILayout.Width(50))) particleOptimizeTolerance = 0.001f;
        if (GUILayout.Button("宽松", GUILayout.Width(50))) particleOptimizeTolerance = 0.01f;
        if (GUILayout.Button("激进", GUILayout.Width(50))) particleOptimizeTolerance = 0.05f;
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(10);
        
        // 执行按钮
        int totalSelectedCount = particleCurveInfos.Count(c => c.isSelected);
        int totalKeyCount = particleCurveInfos.Where(c => c.isSelected).Sum(c => c.TotalKeyCount);
        
        EditorGUI.BeginDisabledGroup(totalSelectedCount == 0);
        if (GUILayout.Button($"执行优化 ({totalSelectedCount} 条曲线, {totalKeyCount} 关键帧)", GUILayout.Height(35)))
        {
            ExecuteParticleCurveOptimize();
        }
        EditorGUI.EndDisabledGroup();
    }
    
    /// <summary>
    /// 绘制粒子系统水平翻转UI
    /// </summary>
    private void DrawParticleFlipHorizontalUI()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("水平翻转（时间轴翻转）", EditorStyles.boldLabel);
        
        EditorGUILayout.HelpBox("将粒子系统曲线在时间轴上翻转，使曲线效果倒放。\n例如：原本从小到大的变化变为从大到小。", MessageType.Info);
        
        particleFlipAllCurves = EditorGUILayout.Toggle("翻转所有曲线", particleFlipAllCurves);
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(5);
        
        // 扫描曲线
        DrawParticleCurveScanUI();
        
        if (!particleCurvesScanned || particleCurveInfos.Count == 0)
        {
            EditorGUILayout.HelpBox("点击\"扫描所有曲线\"按钮以分析粒子系统", MessageType.Info);
            return;
        }
        
        EditorGUILayout.Space(5);
        
        // 曲线列表
        if (!particleFlipAllCurves)
        {
            DrawParticleCurveListUI();
            EditorGUILayout.Space(5);
        }
        
        // 执行按钮
        int totalSelectedCount = particleFlipAllCurves ? 
            particleCurveInfos.Count(c => c.CanOptimize) : 
            particleCurveInfos.Count(c => c.isSelected);
        
        EditorGUI.BeginDisabledGroup(totalSelectedCount == 0);
        if (GUILayout.Button($"执行水平翻转 ({totalSelectedCount} 条曲线)", GUILayout.Height(35)))
        {
            ExecuteParticleFlipHorizontal();
        }
        EditorGUI.EndDisabledGroup();
    }
    
    /// <summary>
    /// 绘制粒子系统垂直翻转UI
    /// </summary>
    private void DrawParticleFlipVerticalUI()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("垂直翻转（值翻转）", EditorStyles.boldLabel);
        
        EditorGUILayout.HelpBox("将粒子系统曲线值沿中心点上下翻转。\n例如：从0到1的曲线变为从1到0。", MessageType.Info);
        
        particleFlipAllCurves = EditorGUILayout.Toggle("翻转所有曲线", particleFlipAllCurves);
        
        EditorGUILayout.Space(5);
        
        particleAutoCalculateCenter = EditorGUILayout.Toggle("自动计算中心值", particleAutoCalculateCenter);
        
        if (!particleAutoCalculateCenter)
        {
            particleVerticalFlipCenter = EditorGUILayout.FloatField("翻转中心值", particleVerticalFlipCenter);
        }
        else
        {
            EditorGUILayout.HelpBox("中心值 = (最大值 + 最小值) / 2", MessageType.None);
        }
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(5);
        
        // 扫描曲线
        DrawParticleCurveScanUI();
        
        if (!particleCurvesScanned || particleCurveInfos.Count == 0)
        {
            EditorGUILayout.HelpBox("点击\"扫描所有曲线\"按钮以分析粒子系统", MessageType.Info);
            return;
        }
        
        EditorGUILayout.Space(5);
        
        // 曲线列表
        if (!particleFlipAllCurves)
        {
            DrawParticleCurveListUI();
            EditorGUILayout.Space(5);
        }
        
        // 执行按钮
        int totalSelectedCount = particleFlipAllCurves ? 
            particleCurveInfos.Count(c => c.CanOptimize) : 
            particleCurveInfos.Count(c => c.isSelected);
        
        EditorGUI.BeginDisabledGroup(totalSelectedCount == 0);
        if (GUILayout.Button($"执行垂直翻转 ({totalSelectedCount} 条曲线)", GUILayout.Height(35)))
        {
            ExecuteParticleFlipVertical();
        }
        EditorGUI.EndDisabledGroup();
    }
    
    /// <summary>
    /// 绘制粒子曲线扫描UI（共用）
    /// </summary>
    private void DrawParticleCurveScanUI()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("曲线扫描", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("扫描所有曲线", GUILayout.Height(25)))
        {
            ScanParticleSystemCurves();
        }
        
        if (particleCurvesScanned)
        {
            int canOptimizeCount = particleCurveInfos.Count(c => c.CanOptimize);
            int selectedCount = particleCurveInfos.Count(c => c.isSelected);
            EditorGUILayout.LabelField($"已选: {selectedCount}/{canOptimizeCount}", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
    }
    
    /// <summary>
    /// 绘制粒子曲线列表UI（共用）
    /// </summary>
    private void DrawParticleCurveListUI()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("待处理曲线", EditorStyles.boldLabel);
        
        // 全选/取消全选
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        particleSelectAll = EditorGUILayout.Toggle("全选可优化曲线", particleSelectAll);
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var info in particleCurveInfos)
            {
                if (info.CanOptimize)
                    info.isSelected = particleSelectAll;
            }
        }
        
        int totalSelectedCount = particleCurveInfos.Count(c => c.isSelected);
        int totalKeyCount = particleCurveInfos.Where(c => c.isSelected).Sum(c => c.TotalKeyCount);
        EditorGUILayout.LabelField($"共 {totalKeyCount} 个关键帧", EditorStyles.miniLabel, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
        
        // 快捷按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全选", GUILayout.Width(50)))
        {
            foreach (var info in particleCurveInfos)
                if (info.CanOptimize) info.isSelected = true;
            particleSelectAll = true;
        }
        if (GUILayout.Button("全不选", GUILayout.Width(60)))
        {
            foreach (var info in particleCurveInfos)
                info.isSelected = false;
            particleSelectAll = false;
        }
        if (GUILayout.Button("反选", GUILayout.Width(50)))
        {
            foreach (var info in particleCurveInfos)
                if (info.CanOptimize) info.isSelected = !info.isSelected;
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(3);
        
        // 按模块分组显示曲线
        particleCurveScrollPos = EditorGUILayout.BeginScrollView(particleCurveScrollPos, GUILayout.MaxHeight(250));
        
        var groupedCurves = particleCurveInfos.Where(c => c.CanOptimize).GroupBy(c => c.moduleName);
        foreach (var group in groupedCurves)
        {
            string moduleName = group.Key;
            if (!particleModuleFoldouts.ContainsKey(moduleName))
                particleModuleFoldouts[moduleName] = true;
            
            int moduleSelected = group.Count(c => c.isSelected);
            int moduleTotal = group.Count();
            
            EditorGUILayout.BeginHorizontal();
            particleModuleFoldouts[moduleName] = EditorGUILayout.Foldout(
                particleModuleFoldouts[moduleName], 
                $"{moduleName} ({moduleSelected}/{moduleTotal})", 
                true);
            
            // 模块级别的全选/取消按钮
            if (GUILayout.Button(moduleSelected == moduleTotal ? "取消" : "选中", 
                EditorStyles.miniButton, GUILayout.Width(40)))
            {
                bool newState = moduleSelected != moduleTotal;
                foreach (var info in group)
                    info.isSelected = newState;
            }
            EditorGUILayout.EndHorizontal();
            
            if (particleModuleFoldouts[moduleName])
            {
                EditorGUI.indentLevel++;
                foreach (var info in group)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    // 使用 ToggleLeft，整个文字区域都可点击
                    info.isSelected = EditorGUILayout.ToggleLeft(
                        info.propertyName, 
                        info.isSelected,
                        GUILayout.Width(160));
                    
                    EditorGUILayout.LabelField($"{info.TotalKeyCount} keys", EditorStyles.miniLabel, GUILayout.Width(60));
                    
                    GUI.color = Color.white;
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }
        
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }
    
    /// <summary>
    /// 获取曲线模式的显示字符串
    /// </summary>
    private string GetCurveModeString(ParticleSystemCurveMode mode)
    {
        switch (mode)
        {
            case ParticleSystemCurveMode.Constant:
                return "常量";
            case ParticleSystemCurveMode.Curve:
                return "曲线";
            case ParticleSystemCurveMode.TwoCurves:
                return "双曲线";
            case ParticleSystemCurveMode.TwoConstants:
                return "双常量";
            default:
                return mode.ToString();
        }
    }

    private void DrawFlipHorizontalUI()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("水平翻转（时间轴翻转）", EditorStyles.boldLabel);
        
        EditorGUILayout.HelpBox("将动画在时间轴上翻转，使动画倒放。\n例如：原本从左到右的动画变为从右到左。", MessageType.Info);
        
        // 翻转范围选择
        EditorGUILayout.Space(5);
        string[] scopeNames = { "所有曲线", "Animation窗口选中的曲线", "单个属性" };
        flipScope = (FlipScope)EditorGUILayout.Popup("翻转范围", (int)flipScope, scopeNames);
        
        // 根据选择显示不同的UI
        switch (flipScope)
        {
            case FlipScope.SelectedFromWindow:
                if (selectedBindingsFromWindow.Count == 0)
                {
                    EditorGUILayout.HelpBox("请在Animation窗口选择要翻转的曲线", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox($"已选中 {selectedBindingsFromWindow.Count} 条曲线", MessageType.Info);
                }
                break;
            case FlipScope.SingleProperty:
                DrawPropertySelector();
                break;
        }
        
        AnimationClip workingClip = GetWorkingClip();
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"动画时长: {workingClip.length:F3}s（翻转后保持不变）");
        
        EditorGUILayout.Space(10);
        
        // 根据选择禁用按钮
        bool canFlip = true;
        if (flipScope == FlipScope.SelectedFromWindow && selectedBindingsFromWindow.Count == 0)
            canFlip = false;
        if (flipScope == FlipScope.SingleProperty && availableBindings.Count == 0)
            canFlip = false;
        
        EditorGUI.BeginDisabledGroup(!canFlip);
        if (GUILayout.Button("执行水平翻转", GUILayout.Height(35)))
        {
            FlipHorizontal();
        }
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.EndVertical();
    }

    private void DrawFlipVerticalUI()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("垂直翻转（值翻转）", EditorStyles.boldLabel);
        
        EditorGUILayout.HelpBox("将曲线值沿中心点上下翻转。\n例如：向上的运动变为向下，正值变负值。", MessageType.Info);
        
        // 翻转范围选择
        EditorGUILayout.Space(5);
        string[] scopeNames = { "所有曲线", "Animation窗口选中的曲线", "单个属性" };
        flipScope = (FlipScope)EditorGUILayout.Popup("翻转范围", (int)flipScope, scopeNames);
        
        // 根据选择显示不同的UI
        switch (flipScope)
        {
            case FlipScope.SelectedFromWindow:
                if (selectedBindingsFromWindow.Count == 0)
                {
                    EditorGUILayout.HelpBox("请在Animation窗口选择要翻转的曲线", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox($"已选中 {selectedBindingsFromWindow.Count} 条曲线", MessageType.Info);
                }
                break;
            case FlipScope.SingleProperty:
                DrawPropertySelector();
                break;
        }
        
        EditorGUILayout.Space(5);
        
        autoCalculateCenter = EditorGUILayout.Toggle("自动计算中心值", autoCalculateCenter);
        
        if (!autoCalculateCenter)
        {
            verticalFlipCenter = EditorGUILayout.FloatField("翻转中心值", verticalFlipCenter);
        }
        else
        {
            EditorGUILayout.HelpBox("中心值 = (最大值 + 最小值) / 2", MessageType.None);
        }
        
        EditorGUILayout.Space(10);
        
        // 根据选择禁用按钮
        bool canFlip = true;
        if (flipScope == FlipScope.SelectedFromWindow && selectedBindingsFromWindow.Count == 0)
            canFlip = false;
        if (flipScope == FlipScope.SingleProperty && availableBindings.Count == 0)
            canFlip = false;
        
        EditorGUI.BeginDisabledGroup(!canFlip);
        if (GUILayout.Button("执行垂直翻转", GUILayout.Height(35)))
        {
            FlipVertical();
        }
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.EndVertical();
    }

    private void DrawSmartOptimizeUI()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        
        // 功能开关
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("智能曲线优化", EditorStyles.boldLabel);
        enableSmartOptimizeFeature = EditorGUILayout.Toggle(enableSmartOptimizeFeature, GUILayout.Width(20));
        EditorGUILayout.EndHorizontal();
        
        if (!enableSmartOptimizeFeature)
        {
            EditorGUILayout.HelpBox("功能已禁用，勾选右侧开关以启用", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }
        
        EditorGUILayout.HelpBox(
            "使用Douglas-Peucker算法智能减少关键帧：\n" +
            "• 保留曲线形状的关键拐点\n" +
            "• 移除可被插值近似的冗余点\n" +
            "• 适用于Spine偏移后的密集曲线", 
            MessageType.Info);
        
        EditorGUILayout.Space(5);
        
        // 容忍度设置
        curveOptimizeTolerance = EditorGUILayout.Slider("误差容忍度", curveOptimizeTolerance, 0.0001f, 0.05f);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("精确", GUILayout.Width(50))) curveOptimizeTolerance = 0.0001f;
        if (GUILayout.Button("标准", GUILayout.Width(50))) curveOptimizeTolerance = 0.001f;
        if (GUILayout.Button("宽松", GUILayout.Width(50))) curveOptimizeTolerance = 0.01f;
        if (GUILayout.Button("激进", GUILayout.Width(50))) curveOptimizeTolerance = 0.05f;
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        // 优化范围
        optimizeSelectedOnly = EditorGUILayout.Toggle("仅优化选中曲线", optimizeSelectedOnly);
        
        if (optimizeSelectedOnly && selectedBindingsFromWindow.Count == 0)
        {
            EditorGUILayout.HelpBox("请在Animation窗口选择要优化的曲线", MessageType.Warning);
        }
        
        // 统计信息
        if (sourceClipInfo != null)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"当前: {sourceClipInfo.curveCount} 条曲线, {sourceClipInfo.keyframeCount} 个关键帧");
        }
        
        EditorGUILayout.Space(10);
        
        if (GUILayout.Button("执行智能优化", GUILayout.Height(35)))
        {
            ExecuteSmartOptimize();
        }
        
        EditorGUILayout.EndVertical();
    }

    #endregion

    #region Animation 窗口集成

    /// <summary>
    /// 编辑器更新回调 - 自动检测 Animation 窗口选择变化
    /// </summary>
    private void OnEditorUpdate()
    {
        // 限制检查频率
        double currentTime = EditorApplication.timeSinceStartup;
        if (currentTime - lastCheckTime < CHECK_INTERVAL)
            return;
        lastCheckTime = currentTime;
        
        // 检测选择变化
        CheckSelectionChange();
        
        // 检测粒子系统曲线模式变化
        CheckParticleCurveModeChange();
    }
    
    /// <summary>
    /// 检测粒子系统曲线模式是否发生变化
    /// </summary>
    private void CheckParticleCurveModeChange()
    {
        // 只在粒子系统模式下且有目标时检测
        if (curveOptimizeSource != CurveOptimizeSource.ParticleSystem || optimizeTargetParticleSystems == null || optimizeTargetParticleSystems.Count == 0)
            return;
        
        // 获取当前曲线模式快照
        var currentModes = GetCurrentCurveModes();
        
        // 比较是否有变化
        bool hasChanged = false;
        
        // 检查数量变化或模式变化
        if (currentModes.Count != lastCurveModes.Count)
        {
            hasChanged = true;
        }
        else
        {
            foreach (var kvp in currentModes)
            {
                if (!lastCurveModes.TryGetValue(kvp.Key, out var lastMode) || lastMode != kvp.Value)
                {
                    hasChanged = true;
                    break;
                }
            }
        }
        
        // 如果有变化，重新扫描
        if (hasChanged)
        {
            lastCurveModes = currentModes;
            ScanParticleSystemCurves();
            particleCurvesScanned = true;
            Repaint();
        }
    }
    
    /// <summary>
    /// 获取当前粒子系统所有曲线的模式快照（支持批量）
    /// </summary>
    private Dictionary<string, ParticleSystemCurveMode> GetCurrentCurveModes()
    {
        var modes = new Dictionary<string, ParticleSystemCurveMode>();
        
        if (optimizeTargetParticleSystems == null || optimizeTargetParticleSystems.Count == 0) return modes;
        
        foreach (var ps in optimizeTargetParticleSystems)
        {
            if (ps == null) continue;
            
            string psName = ps.gameObject.name;
            
            // Main 模块
            var main = ps.main;
            modes[$"{psName}|Main|Start Lifetime"] = main.startLifetime.mode;
            modes[$"{psName}|Main|Start Speed"] = main.startSpeed.mode;
            modes[$"{psName}|Main|Start Size"] = main.startSize.mode;
            modes[$"{psName}|Main|Start Rotation"] = main.startRotation.mode;
            modes[$"{psName}|Main|Gravity Modifier"] = main.gravityModifier.mode;
            
            // Emission 模块
            var emission = ps.emission;
            if (emission.enabled)
            {
                modes[$"{psName}|Emission|Rate over Time"] = emission.rateOverTime.mode;
                modes[$"{psName}|Emission|Rate over Distance"] = emission.rateOverDistance.mode;
            }
            
            // Velocity over Lifetime 模块
            var vol = ps.velocityOverLifetime;
            if (vol.enabled)
            {
                modes[$"{psName}|Velocity over Lifetime|X"] = vol.x.mode;
                modes[$"{psName}|Velocity over Lifetime|Y"] = vol.y.mode;
                modes[$"{psName}|Velocity over Lifetime|Z"] = vol.z.mode;
                modes[$"{psName}|Velocity over Lifetime|Speed Modifier"] = vol.speedModifier.mode;
            }
            
            // Size over Lifetime 模块
            var sol = ps.sizeOverLifetime;
            if (sol.enabled)
            {
                modes[$"{psName}|Size over Lifetime|Size"] = sol.size.mode;
            }
            
            // Rotation over Lifetime 模块
            var rol = ps.rotationOverLifetime;
            if (rol.enabled)
            {
                modes[$"{psName}|Rotation over Lifetime|Angular Velocity"] = rol.z.mode;
            }
        }
        
        return modes;
    }

    /// <summary>
    /// 检测 Animation 窗口选择是否发生变化
    /// </summary>
    private void CheckSelectionChange()
    {
        var currentClip = GetActiveClipFromAnimationWindow();
        var currentBindings = GetSelectedCurvesFromAnimationWindow();
        var currentKeyTimes = GetSelectedKeyTimesFromAnimationWindow();
        
        bool hasChanged = false;
        
        // 检查剪辑是否变化
        if (currentClip != lastActiveClip)
        {
            hasChanged = true;
            lastActiveClip = currentClip;
        }
        
        // 检查选中曲线数量是否变化
        if (currentBindings.Count != lastSelectedCurvesCount)
        {
            hasChanged = true;
            lastSelectedCurvesCount = currentBindings.Count;
        }
        
        // 检查选中关键帧数量是否变化
        if (currentKeyTimes.Count != lastSelectedKeysCount)
        {
            hasChanged = true;
            lastSelectedKeysCount = currentKeyTimes.Count;
        }
        
        // 如果有变化，刷新选择
        if (hasChanged)
        {
            RefreshAnimationWindowSelection();
        }
    }

    /// <summary>
    /// 初始化 Animation 窗口反射类型
    /// </summary>
    private void InitAnimationWindowReflection()
    {
        if (animationWindowType == null)
        {
            animationWindowType = System.Type.GetType("UnityEditor.AnimationWindow,UnityEditor");
        }
        if (animationWindowStateType == null)
        {
            animationWindowStateType = System.Type.GetType("UnityEditorInternal.AnimationWindowState,UnityEditor");
        }
    }

    /// <summary>
    /// 获取 Animation 窗口实例
    /// </summary>
    private EditorWindow GetAnimationWindow()
    {
        InitAnimationWindowReflection();
        if (animationWindowType == null) return null;
        
        var windows = Resources.FindObjectsOfTypeAll(animationWindowType);
        return windows.Length > 0 ? windows[0] as EditorWindow : null;
    }

    /// <summary>
    /// 获取 AnimationWindowState 对象
    /// </summary>
    private object GetAnimationWindowState()
    {
        var window = GetAnimationWindow();
        if (window == null) return null;
        
        var stateProperty = animationWindowType.GetProperty("state",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        
        if (stateProperty == null)
        {
            // 尝试获取字段
            var stateField = animationWindowType.GetField("m_State",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (stateField != null)
            {
                return stateField.GetValue(window);
            }
            return null;
        }
        
        return stateProperty.GetValue(window);
    }

    /// <summary>
    /// 从 Animation 窗口获取当前活动的 AnimationClip
    /// </summary>
    private AnimationClip GetActiveClipFromAnimationWindow()
    {
        var state = GetAnimationWindowState();
        if (state == null) return null;
        
        // 获取 activeAnimationClip
        var clipProperty = animationWindowStateType?.GetProperty("activeAnimationClip",
            BindingFlags.Instance | BindingFlags.Public);
        
        if (clipProperty != null)
        {
            return clipProperty.GetValue(state) as AnimationClip;
        }
        
        // 备用方案：尝试获取字段
        var clipField = animationWindowStateType?.GetField("m_ActiveAnimationClip",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (clipField != null)
        {
            return clipField.GetValue(state) as AnimationClip;
        }
        
        return null;
    }

    /// <summary>
    /// 从 Animation 窗口获取选中的曲线绑定
    /// </summary>
    private List<EditorCurveBinding> GetSelectedCurvesFromAnimationWindow()
    {
        var result = new List<EditorCurveBinding>();
        var state = GetAnimationWindowState();
        if (state == null) return result;
        
        // 尝试获取 activeCurves（当前选中的曲线）
        var curvesProperty = animationWindowStateType?.GetProperty("activeCurves",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        
        if (curvesProperty == null)
        {
            curvesProperty = animationWindowStateType?.GetProperty("selectedCurves",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
        
        if (curvesProperty != null)
        {
            var curves = curvesProperty.GetValue(state) as System.Collections.IEnumerable;
            if (curves != null)
            {
                foreach (var curve in curves)
                {
                    if (curve == null) continue;
                    
                    // AnimationWindowCurve 包含 binding 属性
                    var bindingProperty = curve.GetType().GetProperty("binding",
                        BindingFlags.Instance | BindingFlags.Public);
                    
                    if (bindingProperty != null)
                    {
                        var binding = (EditorCurveBinding)bindingProperty.GetValue(curve);
                        result.Add(binding);
                    }
                    else
                    {
                        // 尝试获取字段
                        var bindingField = curve.GetType().GetField("m_Binding",
                            BindingFlags.Instance | BindingFlags.NonPublic);
                        if (bindingField != null)
                        {
                            var binding = (EditorCurveBinding)bindingField.GetValue(curve);
                            result.Add(binding);
                        }
                    }
                }
            }
        }
        
        return result;
    }

    /// <summary>
    /// 从 Animation 窗口获取选中的关键帧时间
    /// </summary>
    private List<float> GetSelectedKeyTimesFromAnimationWindow()
    {
        var result = new List<float>();
        var state = GetAnimationWindowState();
        if (state == null) return result;
        
        // 尝试获取 selectedKeys
        var keysProperty = animationWindowStateType?.GetProperty("selectedKeys",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        
        if (keysProperty != null)
        {
            var keys = keysProperty.GetValue(state) as System.Collections.IEnumerable;
            if (keys != null)
            {
                foreach (var key in keys)
                {
                    if (key == null) continue;
                    
                    var timeProperty = key.GetType().GetProperty("time",
                        BindingFlags.Instance | BindingFlags.Public);
                    
                    if (timeProperty != null)
                    {
                        result.Add((float)timeProperty.GetValue(key));
                    }
                }
            }
        }
        
        // 如果没有获取到，尝试其他方式
        if (result.Count == 0)
        {
            // 尝试获取 selectedKeyHashes 和通过曲线反查
            var hashesField = animationWindowStateType?.GetField("m_SelectedKeyHashes",
                BindingFlags.Instance | BindingFlags.NonPublic);
            
            if (hashesField != null)
            {
                var hashes = hashesField.GetValue(state) as System.Collections.IEnumerable;
                if (hashes != null)
                {
                    // 如果有选中的 hash，说明有选中的关键帧
                    int count = 0;
                    foreach (var h in hashes) count++;
                    
                    if (count > 0 && activeClipFromWindow != null)
                    {
                        // 获取当前时间作为参考
                        var currentTimeProperty = animationWindowStateType?.GetProperty("currentTime",
                            BindingFlags.Instance | BindingFlags.Public);
                        if (currentTimeProperty != null)
                        {
                            float currentTime = (float)currentTimeProperty.GetValue(state);
                            result.Add(currentTime);
                        }
                    }
                }
            }
        }
        
        return result.Distinct().OrderBy(t => t).ToList();
    }

    /// <summary>
    /// 刷新 Animation 窗口选择状态
    /// </summary>
    private void RefreshAnimationWindowSelection()
    {
        activeClipFromWindow = GetActiveClipFromAnimationWindow();
        selectedBindingsFromWindow = GetSelectedCurvesFromAnimationWindow();
        selectedKeyTimesFromWindow = GetSelectedKeyTimesFromAnimationWindow();
        
        hasValidSelection = activeClipFromWindow != null;
        
        if (hasValidSelection)
        {
            // 同步到工具的源剪辑
            if (sourceClip != activeClipFromWindow)
            {
                sourceClip = activeClipFromWindow;
                sourceClipInfo = AnalyzeClip(sourceClip);
                RefreshBindings(sourceClip);
            }
            
            // 如果有选中的曲线，更新选中的属性索引
            if (selectedBindingsFromWindow.Count > 0 && availableBindings.Count > 0)
            {
                var firstSelected = selectedBindingsFromWindow[0];
                int index = availableBindings.FindIndex(b => 
                    b.path == firstSelected.path && 
                    b.propertyName == firstSelected.propertyName);
                
                if (index >= 0)
                {
                    selectedPropertyIndex = index;
                }
            }
        }
        
        Repaint();
    }

    /// <summary>
    /// 绘制 Animation 窗口状态（精简版）
    /// </summary>
    private void DrawAnimationWindowStatus()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Animation 窗口", EditorStyles.boldLabel, GUILayout.Width(100));
        GUI.color = hasValidSelection ? Color.green : Color.yellow;
        EditorGUILayout.LabelField(hasValidSelection ? "● 已同步" : "○ 等待选择", GUILayout.Width(70));
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
        
        if (hasValidSelection && activeClipFromWindow != null)
        {
            EditorGUILayout.LabelField($"当前剪辑: {activeClipFromWindow.name} | 曲线: {selectedBindingsFromWindow.Count} | 关键帧: {selectedKeyTimesFromWindow.Count}");
        }
        else
        {
            EditorGUILayout.HelpBox("请打开 Animation 窗口并选择动画剪辑", MessageType.Info);
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    /// <summary>
    /// 获取当前工作的动画剪辑（从 Animation 窗口获取）
    /// </summary>
    private AnimationClip GetWorkingClip()
    {
        return activeClipFromWindow;
    }

    /// <summary>
    /// 检查是否有有效的工作剪辑
    /// </summary>
    private bool HasValidWorkingClip()
    {
        return GetWorkingClip() != null;
    }

    /// <summary>
    /// 刷新 Hierarchy 面板选中的 GameObject 列表
    /// </summary>
    private void RefreshHierarchySelection()
    {
        selectedHierarchyObjects.Clear();
        
        // 获取场景中选中的 GameObject（排除 Project 面板中的资源）
        foreach (var go in Selection.gameObjects)
        {
            // 检查是否是场景中的对象（不是 Prefab 资源）
            if (go != null && !EditorUtility.IsPersistent(go))
            {
                selectedHierarchyObjects.Add(go);
            }
        }
        
        // 按Hierarchy面板顺序排序（从上到下）
        if (autoSortByName && selectedHierarchyObjects.Count > 1)
        {
            selectedHierarchyObjects.Sort(CompareHierarchyOrder);
        }
    }

    /// <summary>
    /// 比较两个GameObject在Hierarchy面板中的顺序
    /// </summary>
    private int CompareHierarchyOrder(GameObject a, GameObject b)
    {
        var pathA = GetHierarchyPath(a.transform);
        var pathB = GetHierarchyPath(b.transform);
        
        int minLength = Mathf.Min(pathA.Count, pathB.Count);
        for (int i = 0; i < minLength; i++)
        {
            if (pathA[i] != pathB[i])
                return pathA[i].CompareTo(pathB[i]);
        }
        return pathA.Count.CompareTo(pathB.Count);
    }

    /// <summary>
    /// 获取Transform从根到自身的SiblingIndex路径
    /// </summary>
    private List<int> GetHierarchyPath(Transform t)
    {
        var path = new List<int>();
        var current = t;
        while (current != null)
        {
            path.Insert(0, current.GetSiblingIndex());
            current = current.parent;
        }
        return path;
    }

    /// <summary>
    /// 获取动画根对象（带有 Animator 或 Animation 组件的对象）
    /// </summary>
    private GameObject GetAnimationRootObject()
    {
        var state = GetAnimationWindowState();
        if (state == null) return null;
        
        // 尝试获取 activeRootGameObject
        var rootProperty = animationWindowStateType?.GetProperty("activeRootGameObject",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        
        if (rootProperty != null)
        {
            return rootProperty.GetValue(state) as GameObject;
        }
        
        // 备用方案：尝试获取字段
        var rootField = animationWindowStateType?.GetField("m_ActiveRootGameObject",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (rootField != null)
        {
            return rootField.GetValue(state) as GameObject;
        }
        
        // 再次备用：从选中对象向上查找
        if (Selection.activeGameObject != null)
        {
            var current = Selection.activeGameObject.transform;
            while (current != null)
            {
                if (current.GetComponent<Animator>() != null || 
                    current.GetComponent<Animation>() != null)
                {
                    return current.gameObject;
                }
                current = current.parent;
            }
        }
        
        return null;
    }

    /// <summary>
    /// 计算目标对象相对于动画根对象的路径
    /// </summary>
    private string GetRelativePath(GameObject target, GameObject root)
    {
        if (target == null || root == null) return "";
        if (target == root) return "";
        
        var path = new List<string>();
        var current = target.transform;
        
        while (current != null && current.gameObject != root)
        {
            path.Insert(0, current.name);
            current = current.parent;
            
            // 防止无限循环
            if (path.Count > 100) break;
        }
        
        // 检查是否成功到达根对象
        if (current == null || current.gameObject != root)
        {
            // 目标不在根对象的层级下，返回目标名称
            return target.name;
        }
        
        return string.Join("/", path);
    }

    #endregion

    #region 核心功能实现

    private ClipInfo AnalyzeClip(AnimationClip clip)
    {
        if (clip == null) return null;
        
        var info = new ClipInfo
        {
            clip = clip,
            length = clip.length,
            frameRate = clip.frameRate,
            isLooping = clip.isLooping,
            isHumanoid = clip.humanMotion
        };
        
        var bindings = AnimationUtility.GetCurveBindings(clip);
        info.curveCount = bindings.Length;
        
        int totalKeyframes = 0;
        foreach (var binding in bindings)
        {
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve != null)
            {
                totalKeyframes += curve.keys.Length;
                info.propertyPaths.Add($"{binding.path}/{binding.propertyName}");
            }
        }
        info.keyframeCount = totalKeyframes;
        
        return info;
    }

    private void RefreshBindings(AnimationClip clip)
    {
        availableBindings.Clear();
        if (clip == null) return;
        
        availableBindings.AddRange(AnimationUtility.GetCurveBindings(clip));
        selectedPropertyIndex = 0;
    }

    private void ApplyLayerOffset(float offsetTime)
    {
        AnimationClip workingClip = GetWorkingClip();
        if (workingClip == null)
        {
            ShowNotification(new GUIContent("请先在Animation窗口选择动画剪辑"));
            return;
        }
        
        float clipLength = workingClip.length;
        float frameRate = workingClip.frameRate;
        
        Undo.RecordObject(workingClip, "Offset Keyframes");
        
        // 刷新 Animation 窗口选中的曲线
        // Refresh selected curves from Animation window
        selectedBindingsFromWindow = GetSelectedCurvesFromAnimationWindow();
        
        // 确定要处理的曲线：有选中则只处理选中的，否则处理全部
        // Determine bindings to process: selected only if any, otherwise all
        EditorCurveBinding[] bindings;
        bool isSelectedOnly;
        if (selectedBindingsFromWindow.Count > 0)
        {
            bindings = selectedBindingsFromWindow.ToArray();
            isSelectedOnly = true;
        }
        else
        {
            bindings = AnimationUtility.GetCurveBindings(workingClip);
            isSelectedOnly = false;
        }
        
        int totalCurvesOffset = 0;
        
        // ===== 确定循环长度 =====
        float cycleLength = clipLength;  // 默认使用动画长度
        if (useCyclicOffset && useFixedCycleLength)
        {
            cycleLength = useCycleLengthFrameMode 
                ? cycleLengthFrames / frameRate 
                : cycleLengthSeconds;
        }
        
        foreach (var binding in bindings)
        {
            // 轴过滤检查：如果启用了轴过滤，跳过不需要处理的轴
            if (!ShouldProcessProperty(binding.propertyName))
                continue;
                
            var curve = AnimationUtility.GetEditorCurve(workingClip, binding);
            if (curve == null || curve.keys.Length == 0) continue;
            
            AnimationCurve newCurve;
            
            if (useCyclicOffset)
            {
                // ===== Spine风格循环偏移 =====
                newCurve = CreateSpineStyleOffsetCurve(curve, offsetTime, cycleLength, frameRate);
            }
            else
            {
                // ===== 普通偏移模式：直接移动关键帧时间 =====
                var keys = curve.keys;
                var offsetKeysList = new List<Keyframe>();
                
                for (int i = 0; i < keys.Length; i++)
                {
                    // 普通偏移：限制最小为0
                    float newTime = Mathf.Max(0, keys[i].time + offsetTime);
                    
                    // 对齐到帧
                    newTime = Mathf.Round(newTime * frameRate) / frameRate;
                    
                    offsetKeysList.Add(new Keyframe(
                        newTime,
                        keys[i].value,
                        keys[i].inTangent,
                        keys[i].outTangent,
                        keys[i].inWeight,
                        keys[i].outWeight
                    ) { weightedMode = keys[i].weightedMode });
                }
                
                var sortedKeys = offsetKeysList.OrderBy(k => k.time).ToArray();
                newCurve = new AnimationCurve(sortedKeys);
            }
            
            AnimationUtility.SetEditorCurve(workingClip, binding, newCurve);
            totalCurvesOffset++;
        }
        
        EditorUtility.SetDirty(workingClip);
        AssetDatabase.SaveAssets();
        
        sourceClipInfo = AnalyzeClip(workingClip);
        
        string scope = isSelectedOnly ? $"(选中 {totalCurvesOffset} 条)" : $"(全部 {totalCurvesOffset} 条)";
        string mode = useCyclicOffset ? (useFixedCycleLength ? "(固定帧长循环)" : "(循环)") : "";
        string direction = offsetTime >= 0 ? "后移" : "前移";
        ShowNotification(new GUIContent($"已{direction} {Mathf.Abs(offsetTime):F3}秒{mode}，{scope}曲线"));
    }

    /// <summary>
    /// 批量操作：支持独立的粘贴和偏移功能
    /// </summary>
    private void ApplyBatchOperation(float baseOffset, float frameRate)
    {
        AnimationClip workingClip = GetWorkingClip();
        if (workingClip == null)
        {
            ShowNotification(new GUIContent("请先在Animation窗口选择动画剪辑"));
            return;
        }
        
        // 刷新选中的对象
        RefreshHierarchySelection();
        
        if (selectedHierarchyObjects.Count == 0)
        {
            ShowNotification(new GUIContent("请在Hierarchy面板选择目标对象"));
            return;
        }
        
        // 获取Animation窗口中选中的曲线作为模板
        var templateBindings = GetSelectedCurvesFromAnimationWindow();
        if (enableBatchPaste && templateBindings.Count == 0)
        {
            ShowNotification(new GUIContent("请在Animation窗口选择要复制的曲线"));
            return;
        }
        
        // ===== 调试输出：轴过滤状态 =====
        Debug.Log($"[轴过滤调试] enableAxisFilter={enableAxisFilter}, X={filterAxisX}, Y={filterAxisY}, Z={filterAxisZ}, W={filterAxisW}");
        Debug.Log($"[轴过滤调试] 选中的曲线数量: {templateBindings.Count}");
        foreach (var binding in templateBindings)
        {
            bool shouldProcess = ShouldProcessProperty(binding.propertyName);
            Debug.Log($"[轴过滤调试] 属性: '{binding.propertyName}', 是否处理: {shouldProcess}");
        }
        // ===== 调试输出结束 =====
        
        // 获取动画根对象
        GameObject animRoot = GetAnimationRootObject();
        if (animRoot == null)
        {
            ShowNotification(new GUIContent("无法获取动画根对象"));
            return;
        }
        
        Undo.RecordObject(workingClip, "Batch Operation");
        
        float clipFrameRate = workingClip.frameRate;
        float clipLength = workingClip.length;
        
        // ===== 确定循环长度 =====
        float cycleLength = clipLength;
        if (useCyclicOffset && useFixedCycleLength)
        {
            cycleLength = useCycleLengthFrameMode 
                ? cycleLengthFrames / clipFrameRate 
                : cycleLengthSeconds;
        }
        
        int totalCurvesProcessed = 0;
        int totalObjects = selectedHierarchyObjects.Count;
        
        // ===== 预先缓存源曲线数据 =====
        // 在循环开始前保存模板曲线的副本，防止源对象在目标列表中时被修改
        var cachedTemplateCurves = new Dictionary<EditorCurveBinding, AnimationCurve>();
        if (enableBatchPaste)
        {
            foreach (var templateBinding in templateBindings)
            {
                var templateCurve = AnimationUtility.GetEditorCurve(workingClip, templateBinding);
                if (templateCurve != null && templateCurve.keys.Length > 0)
                {
                    // 创建曲线的深拷贝
                    cachedTemplateCurves[templateBinding] = new AnimationCurve(templateCurve.keys);
                }
            }
        }
        
        // 遍历每个选中的目标对象
        for (int objIndex = 0; objIndex < totalObjects; objIndex++)
        {
            GameObject targetObj = selectedHierarchyObjects[objIndex];
            if (targetObj == null) continue;
            
            // 计算目标对象相对于动画根的路径
            string targetPath = GetRelativePath(targetObj, animRoot);
            
            // ===== 计算该层的时间偏移 =====
            float layerTimeOffset = enableBatchOffset 
                ? CalculateLayerOffset(objIndex, totalObjects, baseOffset, clipFrameRate) 
                : 0f;
            
            if (enableBatchPaste)
            {
                // ===== 粘贴模式：复制模板曲线到目标对象 =====
                foreach (var templateBinding in templateBindings)
                {
                    // 轴过滤检查：如果启用了轴过滤，跳过不需要处理的轴
                    if (!ShouldProcessProperty(templateBinding.propertyName))
                    {
                        // 被过滤跳过的轴：检查目标是否已有曲线，若无则创建常量曲线保持当前值
                        if (enableAxisFilter)
                        {
                            var preserveBinding = new EditorCurveBinding
                            {
                                path = targetPath,
                                type = templateBinding.type,
                                propertyName = templateBinding.propertyName
                            };
                            var preserveCurve = AnimationUtility.GetEditorCurve(workingClip, preserveBinding);
                            if (preserveCurve == null)
                            {
                                // 获取当前值并创建常量曲线
                                float currentValue = GetCurrentPropertyValue(targetObj, templateBinding.type, templateBinding.propertyName);
                                var constantCurve = new AnimationCurve(new Keyframe(0f, currentValue));
                                AnimationUtility.SetEditorCurve(workingClip, preserveBinding, constantCurve);
                            }
                        }
                        continue;
                    }
                    
                    // 使用缓存的模板曲线
                    if (!cachedTemplateCurves.TryGetValue(templateBinding, out var templateCurve))
                        continue;
                    
                    // 创建新的绑定，使用目标对象的路径
                    var newBinding = new EditorCurveBinding
                    {
                        path = targetPath,
                        type = templateBinding.type,
                        propertyName = templateBinding.propertyName
                    };
                    
                    AnimationCurve newCurve;
                    
                    // 检查是否是源对象（路径与模板相同）
                    bool isSameAsSource = (targetPath == templateBinding.path);
                    
                    if (useCyclicOffset && useFixedCycleLength)
                    {
                        // ===== 循环偏移 + 固定帧长模式：始终转换为Spine格式 =====
                        // 即使偏移为0，也需要转换成逐帧采样格式以保持一致性
                        newCurve = CreateSpineStyleOffsetCurve(templateCurve, layerTimeOffset, cycleLength, clipFrameRate);
                    }
                    else if (enableBatchOffset && layerTimeOffset != 0)
                    {
                        // 需要偏移
                        if (useCyclicOffset)
                        {
                            newCurve = CreateSpineStyleOffsetCurve(templateCurve, layerTimeOffset, cycleLength, clipFrameRate);
                        }
                        else
                        {
                            newCurve = CreateOffsetCurve(templateCurve, layerTimeOffset, clipFrameRate);
                        }
                    }
                    else
                    {
                        // 不需要偏移，直接复制
                        newCurve = new AnimationCurve(templateCurve.keys);
                        CopyTangentModes(templateCurve, newCurve);
                    }
                    
                    // 检查是否已存在该绑定的曲线，根据模式处理
                    // 注意：如果是源对象自身，existingCurve可能已被修改，但我们用的是缓存的模板
                    var existingCurve = isSameAsSource ? null : AnimationUtility.GetEditorCurve(workingClip, newBinding);
                    AnimationCurve finalCurve = ProcessExistingCurve(existingCurve, newCurve, clipFrameRate, existingCurveMode);
                    
                    // 如果返回null表示跳过
                    if (finalCurve != null)
                    {
                        AnimationUtility.SetEditorCurve(workingClip, newBinding, finalCurve);
                        totalCurvesProcessed++;
                    }
                }
            }
            else if (enableBatchOffset)
            {
                // ===== 仅偏移模式：对目标对象已有的同名曲线进行偏移 =====
                foreach (var templateBinding in templateBindings)
                {
                    // 轴过滤检查：如果启用了轴过滤，跳过不需要处理的轴
                    if (!ShouldProcessProperty(templateBinding.propertyName))
                        continue;
                    
                    // 构建目标绑定
                    var targetBinding = new EditorCurveBinding
                    {
                        path = targetPath,
                        type = templateBinding.type,
                        propertyName = templateBinding.propertyName
                    };
                    
                    // 获取目标对象已有的曲线
                    var existingCurve = AnimationUtility.GetEditorCurve(workingClip, targetBinding);
                    if (existingCurve == null || existingCurve.keys.Length == 0) continue;
                    
                    AnimationCurve offsetCurve;
                    if (useCyclicOffset)
                    {
                        offsetCurve = CreateSpineStyleOffsetCurve(existingCurve, layerTimeOffset, cycleLength, clipFrameRate);
                    }
                    else
                    {
                        offsetCurve = CreateOffsetCurve(existingCurve, layerTimeOffset, clipFrameRate);
                    }
                    
                    AnimationUtility.SetEditorCurve(workingClip, targetBinding, offsetCurve);
                    totalCurvesProcessed++;
                }
            }
        }
        
        EditorUtility.SetDirty(workingClip);
        AssetDatabase.SaveAssets();
        
        // 刷新剪辑信息
        sourceClipInfo = AnalyzeClip(workingClip);
        
        // 构建通知消息
        string mode = useCyclicOffset ? (useFixedCycleLength ? "(固定帧长循环)" : "(循环)") : "";
        string action = "";
        if (enableBatchPaste && enableBatchOffset)
            action = "粘贴并偏移";
        else if (enableBatchPaste)
            action = "粘贴";
        else
            action = "偏移";
        
        ShowNotification(new GUIContent($"批量{action}完成{mode}: {totalObjects} 个对象, {totalCurvesProcessed} 条曲线"));
    }

    /// <summary>
    /// 获取对象当前属性值（用于轴过滤时保持未处理轴的值）
    /// </summary>
    private float GetCurrentPropertyValue(GameObject obj, System.Type componentType, string propertyName)
    {
        if (obj == null) return 0f;
        
        // 处理Transform属性
        if (componentType == typeof(Transform))
        {
            Transform t = obj.transform;
            switch (propertyName)
            {
                case "m_LocalPosition.x": return t.localPosition.x;
                case "m_LocalPosition.y": return t.localPosition.y;
                case "m_LocalPosition.z": return t.localPosition.z;
                case "m_LocalRotation.x": return t.localRotation.x;
                case "m_LocalRotation.y": return t.localRotation.y;
                case "m_LocalRotation.z": return t.localRotation.z;
                case "m_LocalRotation.w": return t.localRotation.w;
                case "m_LocalScale.x": return t.localScale.x;
                case "m_LocalScale.y": return t.localScale.y;
                case "m_LocalScale.z": return t.localScale.z;
                case "localEulerAnglesRaw.x": return t.localEulerAngles.x;
                case "localEulerAnglesRaw.y": return t.localEulerAngles.y;
                case "localEulerAnglesRaw.z": return t.localEulerAngles.z;
            }
        }
        
        // 其他组件类型：尝试通过反射获取
        var component = obj.GetComponent(componentType);
        if (component != null)
        {
            // 解析属性路径（如 "m_LocalPosition.x" -> "localPosition", "x"）
            string[] parts = propertyName.Split('.');
            if (parts.Length >= 2)
            {
                string fieldName = parts[0].TrimStart('m', '_');
                // 首字母小写
                if (fieldName.Length > 0)
                    fieldName = char.ToLower(fieldName[0]) + fieldName.Substring(1);
                
                var field = componentType.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var prop = componentType.GetProperty(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                
                object value = null;
                if (field != null) value = field.GetValue(component);
                else if (prop != null) value = prop.GetValue(component);
                
                if (value != null)
                {
                    string axis = parts[parts.Length - 1];
                    if (value is Vector3 v3)
                    {
                        if (axis == "x") return v3.x;
                        if (axis == "y") return v3.y;
                        if (axis == "z") return v3.z;
                    }
                    else if (value is Vector4 v4)
                    {
                        if (axis == "x") return v4.x;
                        if (axis == "y") return v4.y;
                        if (axis == "z") return v4.z;
                        if (axis == "w") return v4.w;
                    }
                    else if (value is Quaternion q)
                    {
                        if (axis == "x") return q.x;
                        if (axis == "y") return q.y;
                        if (axis == "z") return q.z;
                        if (axis == "w") return q.w;
                    }
                    else if (value is float f)
                    {
                        return f;
                    }
                }
            }
        }
        
        return 0f;
    }

    /// <summary>
    /// 创建偏移后的曲线（普通模式）
    /// </summary>
    private AnimationCurve CreateOffsetCurve(AnimationCurve originalCurve, float offset, float frameRate)
    {
        var originalKeys = originalCurve.keys;
        var offsetKeysList = new List<Keyframe>();
        
        // 保存原始切线模式
        var leftTangentModes = new AnimationUtility.TangentMode[originalKeys.Length];
        var rightTangentModes = new AnimationUtility.TangentMode[originalKeys.Length];
        for (int i = 0; i < originalKeys.Length; i++)
        {
            leftTangentModes[i] = AnimationUtility.GetKeyLeftTangentMode(originalCurve, i);
            rightTangentModes[i] = AnimationUtility.GetKeyRightTangentMode(originalCurve, i);
        }
        
        for (int i = 0; i < originalKeys.Length; i++)
        {
            float newTime = Mathf.Max(0, originalKeys[i].time + offset);
            newTime = Mathf.Round(newTime * frameRate) / frameRate;
            
            offsetKeysList.Add(new Keyframe(
                newTime,
                originalKeys[i].value,
                originalKeys[i].inTangent,
                originalKeys[i].outTangent,
                originalKeys[i].inWeight,
                originalKeys[i].outWeight
            ) { weightedMode = originalKeys[i].weightedMode });
        }
        
        var newCurve = new AnimationCurve(offsetKeysList.ToArray());
        
        // 恢复切线模式
        for (int i = 0; i < newCurve.keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(newCurve, i, leftTangentModes[i]);
            AnimationUtility.SetKeyRightTangentMode(newCurve, i, rightTangentModes[i]);
        }
        
        return newCurve;
    }

    /// <summary>
    /// 复制切线模式
    /// </summary>
    private void CopyTangentModes(AnimationCurve source, AnimationCurve target)
    {
        int count = Mathf.Min(source.keys.Length, target.keys.Length);
        for (int i = 0; i < count; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(target, i, AnimationUtility.GetKeyLeftTangentMode(source, i));
            AnimationUtility.SetKeyRightTangentMode(target, i, AnimationUtility.GetKeyRightTangentMode(source, i));
        }
    }

    /// <summary>
    /// 处理已有曲线（覆盖、合并或跳过）
    /// </summary>
    /// <param name="existingCurve">已存在的曲线（可为null）</param>
    /// <param name="newCurve">新曲线</param>
    /// <param name="frameRate">帧率</param>
    /// <param name="mode">处理模式</param>
    /// <returns>处理后的曲线，如果返回null表示跳过</returns>
    private AnimationCurve ProcessExistingCurve(AnimationCurve existingCurve, AnimationCurve newCurve, float frameRate, ExistingCurveMode mode)
    {
        // 如果没有已存在的曲线，直接返回新曲线
        if (existingCurve == null || existingCurve.keys.Length == 0)
        {
            return newCurve;
        }
        
        switch (mode)
        {
            case ExistingCurveMode.Overwrite:
                // 覆盖：直接返回新曲线
                return newCurve;
                
            case ExistingCurveMode.Merge:
                // 合并：保留两者的关键帧，相同时间点用新的覆盖
                var keyDict = new Dictionary<float, Keyframe>();
                
                // 先添加已有曲线的关键帧
                foreach (var key in existingCurve.keys)
                {
                    float roundedTime = Mathf.Round(key.time * frameRate) / frameRate;
                    keyDict[roundedTime] = key;
                }
                
                // 再添加新曲线的关键帧（会覆盖相同时间点）
                foreach (var key in newCurve.keys)
                {
                    float roundedTime = Mathf.Round(key.time * frameRate) / frameRate;
                    keyDict[roundedTime] = key;
                }
                
                return new AnimationCurve(keyDict.Values.OrderBy(k => k.time).ToArray());
                
            case ExistingCurveMode.Skip:
                // 跳过：返回null表示不处理
                return null;
                
            default:
                return newCurve;
        }
    }

    /// <summary>
    /// 检查属性名是否应该被处理（根据轴过滤设置）
    /// </summary>
    /// <param name="propertyName">属性名称，如 "m_LocalPosition.x"</param>
    /// <returns>是否应该处理该属性</returns>
    private bool ShouldProcessProperty(string propertyName)
    {
        if (!enableAxisFilter) return true;
        
        // 提取属性名末尾的轴标识
        string lowerName = propertyName.ToLower();
        
        // 检查是否以 .x, .y, .z, .w 结尾
        if (lowerName.EndsWith(".x")) return filterAxisX;
        if (lowerName.EndsWith(".y")) return filterAxisY;
        if (lowerName.EndsWith(".z")) return filterAxisZ;
        if (lowerName.EndsWith(".w")) return filterAxisW;
        
        // 检查颜色属性 .r, .g, .b, .a
        if (lowerName.EndsWith(".r")) return filterAxisX;  // R对应X
        if (lowerName.EndsWith(".g")) return filterAxisY;  // G对应Y
        if (lowerName.EndsWith(".b")) return filterAxisZ;  // B对应Z
        if (lowerName.EndsWith(".a")) return filterAxisW;  // A对应W
        
        // 非轴属性（如单值属性）默认处理
        return true;
    }

    /// <summary>
    /// 批量粘贴动画曲线并应用层级时间偏移（Spine风格：循环偏移和增量偏移）
    /// [保留旧方法以兼容]
    /// </summary>
    private void ApplyBatchPasteWithOffset(float offsetPerLayer)
    {
        AnimationClip workingClip = GetWorkingClip();
        float frameRate = workingClip != null ? workingClip.frameRate : 30f;
        ApplyBatchOperation(offsetPerLayer, frameRate);
    }

    /// <summary>
    /// 创建Spine风格的循环偏移曲线
    /// Spine帧偏移原理：在每个时间点T，采样原曲线在(T - offset) % cycleLength处的值
    /// 效果：整个曲线在时间轴上"滚动"，超出边界的部分循环回来
    /// </summary>
    private AnimationCurve CreateSpineStyleOffsetCurve(AnimationCurve originalCurve, float offset, float cycleLength, float frameRate)
    {
        var originalKeys = originalCurve.keys;
        if (originalKeys.Length == 0) return new AnimationCurve();
        
        // Spine风格：逐帧采样，在每个帧时间点采样偏移后的值
        int totalFrames = Mathf.RoundToInt(cycleLength * frameRate);
        var newKeys = new List<Keyframe>();
        
        for (int frame = 0; frame <= totalFrames; frame++)
        {
            float currentTime = frame / frameRate;
            
            // 计算采样时间：当前时间 - 偏移量，然后循环
            float sampleTime = currentTime - offset;
            
            // 循环处理：确保采样时间在 [0, cycleLength] 范围内
            sampleTime = sampleTime % cycleLength;
            if (sampleTime < 0) sampleTime += cycleLength;
            
            // 从原曲线采样该时间点的值
            float sampledValue = originalCurve.Evaluate(sampleTime);
            
            // 创建新关键帧
            newKeys.Add(new Keyframe(currentTime, sampledValue));
        }
        
        var newCurve = new AnimationCurve(newKeys.ToArray());
        
        // 设置所有关键帧为Auto切线模式，确保平滑
        for (int i = 0; i < newCurve.keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(newCurve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(newCurve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        
        return newCurve;
    }

    /// <summary>
    /// 执行智能曲线优化
    /// </summary>
    private void ExecuteSmartOptimize()
    {
        AnimationClip workingClip = GetWorkingClip();
        if (workingClip == null)
        {
            ShowNotification(new GUIContent("请先在Animation窗口选择动画剪辑"));
            return;
        }
        
        Undo.RecordObject(workingClip, "Smart Optimize Curves");
        
        // 执行前强制刷新 Animation 窗口选中的曲线
        // Refresh selected curves from Animation window before execution
        if (optimizeSelectedOnly)
        {
            selectedBindingsFromWindow = GetSelectedCurvesFromAnimationWindow();
            if (selectedBindingsFromWindow.Count == 0)
            {
                ShowNotification(new GUIContent("请先在Animation窗口选择要优化的曲线"));
                return;
            }
        }
        
        var bindings = AnimationUtility.GetCurveBindings(workingClip);
        int originalTotal = 0, optimizedTotal = 0, curvesOptimized = 0;
        
        foreach (var binding in bindings)
        {
            if (optimizeSelectedOnly && !selectedBindingsFromWindow.Contains(binding))
                continue;
            
            var curve = AnimationUtility.GetEditorCurve(workingClip, binding);
            if (curve == null || curve.keys.Length < 3) continue;
            
            originalTotal += curve.keys.Length;
            var optimized = OptimizeCurveDP(curve, curveOptimizeTolerance, workingClip.frameRate);
            optimizedTotal += optimized.keys.Length;
            
            AnimationUtility.SetEditorCurve(workingClip, binding, optimized);
            curvesOptimized++;
        }
        
        EditorUtility.SetDirty(workingClip);
        AssetDatabase.SaveAssets();
        sourceClipInfo = AnalyzeClip(workingClip);
        
        float reduction = originalTotal > 0 ? (1f - (float)optimizedTotal / originalTotal) * 100f : 0f;
        ShowNotification(new GUIContent($"优化完成: {originalTotal} → {optimizedTotal} (减少{reduction:F1}%)"));
        Debug.Log($"[智能优化] 处理了 {curvesOptimized} 条曲线, 关键帧: {originalTotal} → {optimizedTotal}, 减少: {reduction:F1}%");
    }
    
    /// <summary>
    /// 混合优化算法：DP初筛 + 切线优化 + 冗余检测 + 误差验证
    /// </summary>
    private AnimationCurve OptimizeCurveDP(AnimationCurve curve, float tolerance, float frameRate)
    {
        var keys = curve.keys;
        if (keys.Length < 3) return curve;
        
        // ===== 第1步：Douglas-Peucker初步筛选 =====
        var points = keys.Select(k => new Vector2(k.time, k.value)).ToList();
        var keepIndices = new List<int> { 0, points.Count - 1 };
        DPRecursive(points, 0, points.Count - 1, tolerance, keepIndices);
        keepIndices.Sort();
        
        var simplifiedKeys = keepIndices.Select(i => keys[i]).ToList();
        
        // ===== 第2步：切线优化 - 从原曲线采样最优切线 =====
        var optimizedKeys = new List<Keyframe>();
        for (int i = 0; i < simplifiedKeys.Count; i++)
        {
            var key = simplifiedKeys[i];
            float time = key.time;
            float value = key.value;
            
            // 从原曲线计算该点的切线（导数）
            float delta = 1f / frameRate;
            float inTangent, outTangent;
            
            if (i == 0)
            {
                // 首帧：只计算出切线
                outTangent = (curve.Evaluate(time + delta) - value) / delta;
                inTangent = outTangent;
            }
            else if (i == simplifiedKeys.Count - 1)
            {
                // 尾帧：只计算入切线
                inTangent = (value - curve.Evaluate(time - delta)) / delta;
                outTangent = inTangent;
            }
            else
            {
                // 中间帧：使用中心差分计算切线
                float slope = (curve.Evaluate(time + delta) - curve.Evaluate(time - delta)) / (2f * delta);
                inTangent = slope;
                outTangent = slope;
            }
            
            var newKey = new Keyframe(time, value, inTangent, outTangent);
            optimizedKeys.Add(newKey);
        }
        
        var result = new AnimationCurve(optimizedKeys.ToArray());
        
        // 设置切线模式为Free（保持我们计算的切线值）
        for (int i = 0; i < result.keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(result, i, AnimationUtility.TangentMode.Free);
            AnimationUtility.SetKeyRightTangentMode(result, i, AnimationUtility.TangentMode.Free);
        }
        
        // ===== 第3步：误差验证 - 在误差大的地方补点 =====
        result = ValidateAndAddKeys(curve, result, tolerance, frameRate);
        
        // ===== 第4步：冗余检测 - 尝试删除不必要的点 =====
        result = RemoveRedundantKeys(curve, result, tolerance, frameRate);
        
        return result;
    }
    
    /// <summary>
    /// Douglas-Peucker递归简化
    /// </summary>
    private void DPRecursive(List<Vector2> pts, int start, int end, float tol, List<int> keep)
    {
        if (end <= start + 1) return;
        
        float maxD = 0; int maxI = start;
        Vector2 line = pts[end] - pts[start];
        float len = line.magnitude;
        
        for (int i = start + 1; i < end; i++)
        {
            float d;
            if (len < 0.0001f)
            {
                d = Mathf.Abs(pts[i].y - pts[start].y);
            }
            else
            {
                Vector2 toPoint = pts[i] - pts[start];
                Vector2 lineDir = line.normalized;
                float projLength = Vector2.Dot(toPoint, lineDir);
                Vector2 projPoint = pts[start] + lineDir * projLength;
                d = Vector2.Distance(pts[i], projPoint);
            }
            if (d > maxD) { maxD = d; maxI = i; }
        }
        
        if (maxD > tol)
        {
            keep.Add(maxI);
            DPRecursive(pts, start, maxI, tol, keep);
            DPRecursive(pts, maxI, end, tol, keep);
        }
    }
    
    /// <summary>
    /// 验证曲线误差，在误差过大的地方添加关键帧
    /// </summary>
    private AnimationCurve ValidateAndAddKeys(AnimationCurve orig, AnimationCurve opt, float tol, float fps)
    {
        var cur = opt;
        float dur = orig.keys.Last().time;
        
        for (int iter = 0; iter < 10; iter++)
        {
            float maxE = 0, maxT = 0, maxV = 0;
            int totalFrames = Mathf.RoundToInt(dur * fps);
            
            // 逐帧检查误差
            for (int f = 0; f <= totalFrames; f++)
            {
                float t = f / fps;
                float origVal = orig.Evaluate(t);
                float optVal = cur.Evaluate(t);
                float e = Mathf.Abs(origVal - optVal);
                if (e > maxE) { maxE = e; maxT = t; maxV = origVal; }
            }
            
            // 误差在容忍范围内，完成
            if (maxE <= tol) break;
            
            // 在误差最大处添加关键帧
            var list = cur.keys.ToList();
            if (!list.Any(k => Mathf.Abs(k.time - maxT) < 0.0001f))
            {
                // 计算该点的切线
                float delta = 1f / fps;
                float slope = (orig.Evaluate(maxT + delta) - orig.Evaluate(maxT - delta)) / (2f * delta);
                
                var newKey = new Keyframe(maxT, maxV, slope, slope);
                list.Add(newKey);
                list.Sort((a, b) => a.time.CompareTo(b.time));
                cur = new AnimationCurve(list.ToArray());
                
                // 设置切线模式
                for (int i = 0; i < cur.keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(cur, i, AnimationUtility.TangentMode.Free);
                    AnimationUtility.SetKeyRightTangentMode(cur, i, AnimationUtility.TangentMode.Free);
                }
            }
            else break;
        }
        return cur;
    }
    
    /// <summary>
    /// 冗余检测：尝试删除每个非首尾关键帧，如果误差仍在阈值内则删除
    /// </summary>
    private AnimationCurve RemoveRedundantKeys(AnimationCurve orig, AnimationCurve opt, float tol, float fps)
    {
        var keys = opt.keys.ToList();
        if (keys.Count <= 2) return opt;
        
        float dur = orig.keys.Last().time;
        int totalFrames = Mathf.RoundToInt(dur * fps);
        
        // 计算每个关键帧的重要性评分
        var importanceScores = new List<(int index, float score)>();
        for (int i = 1; i < keys.Count - 1; i++) // 跳过首尾
        {
            float score = CalculateKeyImportance(orig, keys, i, fps);
            importanceScores.Add((i, score));
        }
        
        // 按重要性从低到高排序
        importanceScores.Sort((a, b) => a.score.CompareTo(b.score));
        
        // 尝试删除低重要性的关键帧
        var indicesToRemove = new HashSet<int>();
        foreach (var (index, score) in importanceScores)
        {
            // 创建不包含该点的临时曲线
            var tempKeys = new List<Keyframe>();
            for (int i = 0; i < keys.Count; i++)
            {
                if (i != index && !indicesToRemove.Contains(i))
                    tempKeys.Add(keys[i]);
            }
            
            if (tempKeys.Count < 2) continue;
            
            var tempCurve = new AnimationCurve(tempKeys.ToArray());
            for (int i = 0; i < tempCurve.keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(tempCurve, i, AnimationUtility.TangentMode.Free);
                AnimationUtility.SetKeyRightTangentMode(tempCurve, i, AnimationUtility.TangentMode.Free);
            }
            
            // 检查误差
            float maxError = CalculateMaxError(orig, tempCurve, fps, totalFrames);
            
            // 如果误差仍在容忍范围内，标记删除
            if (maxError <= tol)
            {
                indicesToRemove.Add(index);
            }
        }
        
        // 构建最终曲线
        if (indicesToRemove.Count > 0)
        {
            var finalKeys = new List<Keyframe>();
            for (int i = 0; i < keys.Count; i++)
            {
                if (!indicesToRemove.Contains(i))
                    finalKeys.Add(keys[i]);
            }
            
            var finalCurve = new AnimationCurve(finalKeys.ToArray());
            for (int i = 0; i < finalCurve.keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(finalCurve, i, AnimationUtility.TangentMode.Free);
                AnimationUtility.SetKeyRightTangentMode(finalCurve, i, AnimationUtility.TangentMode.Free);
            }
            return finalCurve;
        }
        
        return opt;
    }
    
    /// <summary>
    /// 计算关键帧的重要性评分（评分越低越容易被删除）
    /// </summary>
    private float CalculateKeyImportance(AnimationCurve orig, List<Keyframe> keys, int index, float fps)
    {
        if (index <= 0 || index >= keys.Count - 1) return float.MaxValue;
        
        var prevKey = keys[index - 1];
        var currKey = keys[index];
        var nextKey = keys[index + 1];
        
        // 1. 位置重要性：删除该点后的线性插值误差
        float t = (currKey.time - prevKey.time) / (nextKey.time - prevKey.time);
        float interpolated = Mathf.Lerp(prevKey.value, nextKey.value, t);
        float positionError = Mathf.Abs(currKey.value - interpolated);
        
        // 2. 切线重要性：前后斜率变化
        float slopeBefore = (currKey.value - prevKey.value) / Mathf.Max(0.0001f, currKey.time - prevKey.time);
        float slopeAfter = (nextKey.value - currKey.value) / Mathf.Max(0.0001f, nextKey.time - currKey.time);
        float slopeChange = Mathf.Abs(slopeBefore - slopeAfter);
        
        // 3. 曲率重要性：该点处原曲线的二阶导数
        float delta = 1f / fps;
        float v0 = orig.Evaluate(currKey.time - delta);
        float v1 = orig.Evaluate(currKey.time);
        float v2 = orig.Evaluate(currKey.time + delta);
        float curvature = Mathf.Abs(v0 - 2f * v1 + v2) / (delta * delta);
        
        // 综合评分（权重可调）
        return positionError * 1.0f + slopeChange * 0.3f + curvature * 0.1f;
    }
    
    /// <summary>
    /// 计算两条曲线之间的最大误差
    /// </summary>
    private float CalculateMaxError(AnimationCurve orig, AnimationCurve opt, float fps, int totalFrames)
    {
        float maxError = 0f;
        for (int f = 0; f <= totalFrames; f++)
        {
            float t = f / fps;
            float error = Mathf.Abs(orig.Evaluate(t) - opt.Evaluate(t));
            maxError = Mathf.Max(maxError, error);
        }
        return maxError;
    }

    private void FlipHorizontal()
    {
        if (sourceClip == null) return;
        
        // 执行前强制刷新Animation窗口选择，确保使用最新的选中曲线
        if (flipScope == FlipScope.SelectedFromWindow)
        {
            selectedBindingsFromWindow = GetSelectedCurvesFromAnimationWindow();
        }
        
        Undo.RecordObject(sourceClip, "Flip Horizontal");
        
        float clipLength = sourceClip.length;
        var bindings = AnimationUtility.GetCurveBindings(sourceClip);
        int flippedCount = 0;
        
        foreach (var binding in bindings)
        {
            // 根据翻转范围过滤曲线
            switch (flipScope)
            {
                case FlipScope.SelectedFromWindow:
                    if (!selectedBindingsFromWindow.Any(b => b.path == binding.path && b.propertyName == binding.propertyName))
                        continue;
                    break;
                case FlipScope.SingleProperty:
                    if (selectedPropertyIndex >= availableBindings.Count) continue;
                    if (!binding.Equals(availableBindings[selectedPropertyIndex])) continue;
                    break;
                // FlipScope.AllCurves 不需要过滤
            }
            
            var curve = AnimationUtility.GetEditorCurve(sourceClip, binding);
            if (curve == null || curve.keys.Length == 0) continue;
            
            var keys = curve.keys;
            
            // 保存原始切线模式和切线值
            var leftTangentModes = new AnimationUtility.TangentMode[keys.Length];
            var rightTangentModes = new AnimationUtility.TangentMode[keys.Length];
            var inTangents = new float[keys.Length];
            var outTangents = new float[keys.Length];
            var inWeights = new float[keys.Length];
            var outWeights = new float[keys.Length];
            var weightedModes = new WeightedMode[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                leftTangentModes[i] = AnimationUtility.GetKeyLeftTangentMode(curve, i);
                rightTangentModes[i] = AnimationUtility.GetKeyRightTangentMode(curve, i);
                inTangents[i] = keys[i].inTangent;
                outTangents[i] = keys[i].outTangent;
                inWeights[i] = keys[i].inWeight;
                outWeights[i] = keys[i].outWeight;
                weightedModes[i] = keys[i].weightedMode;
            }
            
            // 水平翻转需要创建新曲线（因为时间顺序会改变）
            var newCurve = new AnimationCurve();
            
            for (int i = 0; i < keys.Length; i++)
            {
                int reverseIndex = keys.Length - 1 - i;
                float newTime = clipLength - keys[reverseIndex].time;
                
                // 翻转时间，同时交换并取反切线
                var newKey = new Keyframe(
                    newTime,
                    keys[reverseIndex].value,
                    -outTangents[reverseIndex],  // in切线 = 原out切线取反
                    -inTangents[reverseIndex],   // out切线 = 原in切线取反
                    outWeights[reverseIndex],    // weight也要交换
                    inWeights[reverseIndex]
                );
                newKey.weightedMode = weightedModes[reverseIndex];
                
                newCurve.AddKey(newKey);
            }
            
            // 恢复切线模式（水平翻转后左右交换）
            for (int i = 0; i < newCurve.keys.Length; i++)
            {
                int reverseIndex = keys.Length - 1 - i;
                AnimationUtility.SetKeyLeftTangentMode(newCurve, i, rightTangentModes[reverseIndex]);
                AnimationUtility.SetKeyRightTangentMode(newCurve, i, leftTangentModes[reverseIndex]);
            }
            
            // 对于 Free 模式，需要重新设置切线值
            var finalKeys = newCurve.keys;
            bool needsUpdate = false;
            for (int i = 0; i < finalKeys.Length; i++)
            {
                int reverseIndex = keys.Length - 1 - i;
                var leftMode = rightTangentModes[reverseIndex];
                var rightMode = leftTangentModes[reverseIndex];
                
                bool leftIsFree = leftMode == AnimationUtility.TangentMode.Free;
                bool rightIsFree = rightMode == AnimationUtility.TangentMode.Free;
                
                if (leftIsFree || rightIsFree)
                {
                    if (leftIsFree)
                    {
                        finalKeys[i].inTangent = -outTangents[reverseIndex];
                    }
                    if (rightIsFree)
                    {
                        finalKeys[i].outTangent = -inTangents[reverseIndex];
                    }
                    needsUpdate = true;
                }
            }
            
            if (needsUpdate)
            {
                // 使用 MoveKey 逐个更新以保留切线模式
                for (int i = 0; i < finalKeys.Length; i++)
                {
                    newCurve.MoveKey(i, finalKeys[i]);
                }
                // 再次恢复切线模式
                for (int i = 0; i < newCurve.keys.Length; i++)
                {
                    int reverseIndex = keys.Length - 1 - i;
                    AnimationUtility.SetKeyLeftTangentMode(newCurve, i, rightTangentModes[reverseIndex]);
                    AnimationUtility.SetKeyRightTangentMode(newCurve, i, leftTangentModes[reverseIndex]);
                }
            }
            
            AnimationUtility.SetEditorCurve(sourceClip, binding, newCurve);
            flippedCount++;
        }
        
        EditorUtility.SetDirty(sourceClip);
        AssetDatabase.SaveAssets();
        
        ShowNotification(new GUIContent($"水平翻转完成，处理了 {flippedCount} 条曲线"));
    }

    private void FlipVertical()
    {
        if (sourceClip == null) return;
        
        // 执行前强制刷新Animation窗口选择，确保使用最新的选中曲线
        if (flipScope == FlipScope.SelectedFromWindow)
        {
            selectedBindingsFromWindow = GetSelectedCurvesFromAnimationWindow();
        }
        
        Undo.RecordObject(sourceClip, "Flip Vertical");
        
        var bindings = AnimationUtility.GetCurveBindings(sourceClip);
        int flippedCount = 0;
        
        foreach (var binding in bindings)
        {
            // 根据翻转范围过滤曲线
            switch (flipScope)
            {
                case FlipScope.SelectedFromWindow:
                    if (!selectedBindingsFromWindow.Any(b => b.path == binding.path && b.propertyName == binding.propertyName))
                        continue;
                    break;
                case FlipScope.SingleProperty:
                    if (selectedPropertyIndex >= availableBindings.Count) continue;
                    if (!binding.Equals(availableBindings[selectedPropertyIndex])) continue;
                    break;
                // FlipScope.AllCurves 不需要过滤
            }
            
            var curve = AnimationUtility.GetEditorCurve(sourceClip, binding);
            if (curve == null || curve.keys.Length == 0) continue;
            
            var keys = curve.keys;
            
            // 计算中心值
            float centerValue = verticalFlipCenter;
            if (autoCalculateCenter)
            {
                float minValue = keys.Min(k => k.value);
                float maxValue = keys.Max(k => k.value);
                centerValue = (minValue + maxValue) / 2f;
            }
            
            // 保存原始切线模式和切线值
            var leftTangentModes = new AnimationUtility.TangentMode[keys.Length];
            var rightTangentModes = new AnimationUtility.TangentMode[keys.Length];
            var inTangents = new float[keys.Length];
            var outTangents = new float[keys.Length];
            var inWeights = new float[keys.Length];
            var outWeights = new float[keys.Length];
            var weightedModes = new WeightedMode[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                leftTangentModes[i] = AnimationUtility.GetKeyLeftTangentMode(curve, i);
                rightTangentModes[i] = AnimationUtility.GetKeyRightTangentMode(curve, i);
                inTangents[i] = keys[i].inTangent;
                outTangents[i] = keys[i].outTangent;
                inWeights[i] = keys[i].inWeight;
                outWeights[i] = keys[i].outWeight;
                weightedModes[i] = keys[i].weightedMode;
            }
            
            // 直接在原曲线上修改每个关键帧
            for (int i = 0; i < keys.Length; i++)
            {
                // 值关于中心点翻转：newValue = center - (value - center) = 2*center - value
                float flippedValue = 2f * centerValue - keys[i].value;
                
                var newKey = new Keyframe(
                    keys[i].time,
                    flippedValue,
                    -inTangents[i],   // 切线取反
                    -outTangents[i],  // 切线取反
                    inWeights[i],
                    outWeights[i]
                );
                newKey.weightedMode = weightedModes[i];
                
                curve.MoveKey(i, newKey);
            }
            
            // 恢复切线模式（垂直翻转保持左右不变）
            for (int i = 0; i < curve.keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, leftTangentModes[i]);
                AnimationUtility.SetKeyRightTangentMode(curve, i, rightTangentModes[i]);
            }
            
            // 对于 Free/Broken 模式，需要重新设置切线值
            var finalKeys = curve.keys;
            bool needsUpdate = false;
            for (int i = 0; i < finalKeys.Length; i++)
            {
                bool leftIsFree = leftTangentModes[i] == AnimationUtility.TangentMode.Free;
                bool rightIsFree = rightTangentModes[i] == AnimationUtility.TangentMode.Free;
                
                if (leftIsFree || rightIsFree)
                {
                    if (leftIsFree)
                    {
                        finalKeys[i].inTangent = -inTangents[i];
                    }
                    if (rightIsFree)
                    {
                        finalKeys[i].outTangent = -outTangents[i];
                    }
                    needsUpdate = true;
                }
            }
            
            if (needsUpdate)
            {
                // 使用 MoveKey 逐个更新以保留切线模式
                for (int i = 0; i < finalKeys.Length; i++)
                {
                    curve.MoveKey(i, finalKeys[i]);
                }
                // 再次恢复切线模式
                for (int i = 0; i < curve.keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, leftTangentModes[i]);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, rightTangentModes[i]);
                }
            }
            
            AnimationUtility.SetEditorCurve(sourceClip, binding, curve);
            flippedCount++;
        }
        
        EditorUtility.SetDirty(sourceClip);
        AssetDatabase.SaveAssets();
        
        ShowNotification(new GUIContent($"垂直翻转完成，处理了 {flippedCount} 条曲线"));
    }

    #endregion

    #region 曲线转换 Tab

    /// <summary>
    /// 绘制曲线转换Tab
    /// </summary>
    private void DrawCurveConvertTab()
    {
        // 显示当前 Animation 窗口状态
        DrawAnimationWindowStatus();
        
        AnimationClip workingClip = GetWorkingClip();
        
        if (workingClip == null)
        {
            EditorGUILayout.HelpBox("请在 Animation 窗口中选择动画剪辑", MessageType.Info);
            return;
        }
        
        EditorGUILayout.Space(10);
        
        // 数据源设置
        DrawConvertInputSection();
        
        // 转换设置
        DrawConvertSettingsSection();
        
        // 转换按钮
        DrawConvertExecuteSection();
        
        // 结果显示
        DrawConvertResultSection();
    }

    /// <summary>
    /// 绘制数据源设置
    /// </summary>
    private void DrawConvertInputSection()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("数据源", EditorStyles.boldLabel);
        
        // 显示当前动画剪辑（从Animation窗口同步）
        AnimationClip workingClip = GetWorkingClip();
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField("源动画", workingClip, typeof(AnimationClip), false);
        EditorGUI.EndDisabledGroup();
        
        // 目标粒子系统
        targetParticleSystem = (ParticleSystem)EditorGUILayout.ObjectField("目标粒子系统", targetParticleSystem, typeof(ParticleSystem), true);
        
        EditorGUILayout.Space(5);
        
        // 曲线选择 - 沿用Animation窗口的选中方式
        EditorGUILayout.LabelField("曲线选择", EditorStyles.boldLabel);
        
        if (selectedBindingsFromWindow.Count > 0)
        {
            EditorGUILayout.HelpBox($"已选中 {selectedBindingsFromWindow.Count} 条曲线（从Animation窗口同步）", MessageType.Info);
            
            // 扫描并分组选中的曲线
            GUI.backgroundColor = new Color(0.8f, 0.95f, 1f);
            if (GUILayout.Button("分析选中曲线", GUILayout.Height(25)))
            {
                AnalyzeSelectedCurvesForConvert();
            }
            GUI.backgroundColor = Color.white;
            
            // 显示分组结果
            if (curveGroupsScanned && curveGroups.Count > 0)
            {
                EditorGUILayout.Space(5);
                
                // 构建下拉选项
                string[] options = new string[curveGroups.Count];
                for (int i = 0; i < curveGroups.Count; i++)
                {
                    var g = curveGroups[i];
                    options[i] = $"{g.displayName} ({g.AxisCount}轴)" + (g.HasAllAxes ? " ✓" : "");
                }
                
                // 单个下拉框选择目标对象
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("目标对象", GUILayout.Width(60));
                selectedGroupIndex = EditorGUILayout.Popup(selectedGroupIndex, options);
                EditorGUILayout.EndHorizontal();
                
                // 显示选中对象的详情
                if (selectedGroupIndex >= 0 && selectedGroupIndex < curveGroups.Count)
                {
                    var group = curveGroups[selectedGroupIndex];
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"完整路径: {(string.IsNullOrEmpty(group.path) ? "(根对象)" : group.path)}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"  X: {(group.bindingX.HasValue ? "✓ " + group.bindingX.Value.propertyName : "✗ 无")}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"  Y: {(group.bindingY.HasValue ? "✓ " + group.bindingY.Value.propertyName : "✗ 无")}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"  Z: {(group.bindingZ.HasValue ? "✓ " + group.bindingZ.Value.propertyName : "✗ 无")}", EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();
                }
            }
            else if (curveGroupsScanned)
            {
                EditorGUILayout.HelpBox("选中的曲线中没有找到位置相关曲线", MessageType.Warning);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("请在Animation窗口中选择要转换的位置曲线", MessageType.Warning);
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    /// <summary>
    /// 分析选中的曲线，按对象路径分组
    /// </summary>
    private void AnalyzeSelectedCurvesForConvert()
    {
        curveGroups.Clear();
        
        if (selectedBindingsFromWindow.Count == 0)
        {
            curveGroupsScanned = true;
            return;
        }
        
        // 按路径分组
        Dictionary<string, CurveGroup> groupDict = new Dictionary<string, CurveGroup>();
        
        foreach (var binding in selectedBindingsFromWindow)
        {
            string propLower = binding.propertyName.ToLower();
            
            // 只关注位置相关曲线
            if (!propLower.Contains("position") && !propLower.Contains("pos") && 
                !propLower.Contains("localposition") && !propLower.Contains("m_localposition"))
                continue;
            
            // 获取或创建组
            if (!groupDict.TryGetValue(binding.path, out CurveGroup group))
            {
                group = new CurveGroup
                {
                    path = binding.path,
                    displayName = string.IsNullOrEmpty(binding.path) 
                        ? "(根对象)" 
                        : System.IO.Path.GetFileName(binding.path)
                };
                groupDict[binding.path] = group;
            }
            
            // 分配到对应轴
            if (propLower.EndsWith(".x") || propLower.EndsWith("x"))
                group.bindingX = binding;
            else if (propLower.EndsWith(".y") || propLower.EndsWith("y"))
                group.bindingY = binding;
            else if (propLower.EndsWith(".z") || propLower.EndsWith("z"))
                group.bindingZ = binding;
        }
        
        // 转换为列表并排序（优先显示完整的组）
        curveGroups = new List<CurveGroup>(groupDict.Values);
        curveGroups.Sort((a, b) => {
            int axisCompare = b.AxisCount.CompareTo(a.AxisCount);
            if (axisCompare != 0) return axisCompare;
            return string.Compare(a.displayName, b.displayName);
        });
        
        curveGroupsScanned = true;
        selectedGroupIndex = 0;
        
        Debug.Log($"[曲线转换] 分析完成: 找到 {curveGroups.Count} 个对象的位置曲线");
    }

    /// <summary>
    /// 绘制转换设置
    /// </summary>
    private void DrawConvertSettingsSection()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("转换设置", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("转换方法", GUILayout.Width(60));
        if (GUILayout.Toggle(conversionMethod == ConversionMethod.CumulativeVelocity, "累积速度法", EditorStyles.miniButtonLeft))
            conversionMethod = ConversionMethod.CumulativeVelocity;
        if (GUILayout.Toggle(conversionMethod == ConversionMethod.InstantVelocity, "瞬时速度法", EditorStyles.miniButtonMid))
            conversionMethod = ConversionMethod.InstantVelocity;
        if (GUILayout.Toggle(conversionMethod == ConversionMethod.AverageVelocity, "平均速度法", EditorStyles.miniButtonRight))
            conversionMethod = ConversionMethod.AverageVelocity;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        switch (conversionMethod)
        {
            case ConversionMethod.CumulativeVelocity:
                EditorGUILayout.HelpBox("累积速度法(推荐)\n√ 计算位置增量除以时间增量\n√ 轨迹与原动画完全一致\n√ 适用于所有类型的运动", MessageType.None);
                break;
                
            case ConversionMethod.InstantVelocity:
                EditorGUILayout.HelpBox("瞬时速度法\n√ 使用曲线切线计算瞬时速度\n√ 保留原始曲线的平滑度\n√ 可能有轻微偏差", MessageType.None);
                break;
                
            case ConversionMethod.AverageVelocity:
                EditorGUILayout.HelpBox("平均速度法\n√ 计算区间平均速度\n√ 更平滑但精度稍低", MessageType.None);
                break;
        }
        
        EditorGUILayout.Space(5);
        
        convertSampleRate = EditorGUILayout.IntSlider("采样率(fps)", convertSampleRate, 5, 120);
        EditorGUILayout.LabelField($"每秒 {convertSampleRate} 个样本点", EditorStyles.miniLabel);
        
        useHighPrecision = EditorGUILayout.Toggle("高精度模式", useHighPrecision);
        
        velocityMultiplier = EditorGUILayout.Vector3Field("速度倍数", velocityMultiplier);
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    /// <summary>
    /// 绘制执行转换按钮
    /// </summary>
    private void DrawConvertExecuteSection()
    {
        bool canExecute = targetParticleSystem != null && 
                          curveGroupsScanned && 
                          curveGroups.Count > 0 &&
                          selectedGroupIndex >= 0 && 
                          selectedGroupIndex < curveGroups.Count;
        
        GUI.enabled = canExecute;
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("执行完美转换", GUILayout.Height(40)))
        {
            ExecutePerfectConversion();
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
        
        EditorGUILayout.Space(10);
    }

    /// <summary>
    /// 绘制转换结果
    /// </summary>
    private void DrawConvertResultSection()
    {
        if (resultXCurve == null && resultYCurve == null && resultZCurve == null)
            return;
        
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("转换结果", EditorStyles.boldLabel);
        
        // 显示曲线乘数信息
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("曲线乘数 (Curve Multiplier)", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"X: {xMultiplier:F3}  |  Y: {yMultiplier:F3}  |  Z: {zMultiplier:F3}");
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(5);
        
        // 计算归一化曲线的显示范围 (-1 到 1)
        AnimationClip workingClip = GetWorkingClip();
        float maxTime = workingClip != null ? workingClip.length : 1;
        Rect normalizedRange = new Rect(0, -1.1f, maxTime, 2.2f);
        
        // 获取并显示归一化后的曲线
        if (resultXCurve != null && resultXCurve.keys.Length > 0)
        {
            float mult;
            AnimationCurve normalizedX = NormalizeCurveAndGetMultiplier(resultXCurve, out mult);
            EditorGUILayout.LabelField($"X轴速度 ({resultXCurve.keys.Length} 关键帧, 乘数: {mult:F2})", EditorStyles.miniLabel);
            EditorGUILayout.CurveField(normalizedX, Color.red, normalizedRange, GUILayout.Height(60));
        }
        
        if (resultYCurve != null && resultYCurve.keys.Length > 0)
        {
            float mult;
            AnimationCurve normalizedY = NormalizeCurveAndGetMultiplier(resultYCurve, out mult);
            EditorGUILayout.LabelField($"Y轴速度 ({resultYCurve.keys.Length} 关键帧, 乘数: {mult:F2})", EditorStyles.miniLabel);
            EditorGUILayout.CurveField(normalizedY, Color.green, normalizedRange, GUILayout.Height(60));
        }
        
        if (resultZCurve != null && resultZCurve.keys.Length > 0)
        {
            float mult;
            AnimationCurve normalizedZ = NormalizeCurveAndGetMultiplier(resultZCurve, out mult);
            EditorGUILayout.LabelField($"Z轴速度 ({resultZCurve.keys.Length} 关键帧, 乘数: {mult:F2})", EditorStyles.miniLabel);
            EditorGUILayout.CurveField(normalizedZ, Color.blue, normalizedRange, GUILayout.Height(60));
        }
        
        EditorGUILayout.HelpBox("上方显示的是归一化曲线(-1到1范围),实际速度 = 乘数 × 曲线值", MessageType.Info);
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    #endregion

    #region 曲线转换核心功能

    /// <summary>
    /// 执行完美转换
    /// </summary>
    private void ExecutePerfectConversion()
    {
        AnimationClip workingClip = GetWorkingClip();
        if (workingClip == null)
        {
            ShowNotification(new GUIContent("请先在Animation窗口选择动画剪辑"));
            return;
        }
        
        if (targetParticleSystem == null)
        {
            ShowNotification(new GUIContent("请选择目标粒子系统"));
            return;
        }
        
        if (!curveGroupsScanned || curveGroups.Count == 0)
        {
            ShowNotification(new GUIContent("请先分析选中的曲线"));
            return;
        }
        
        if (selectedGroupIndex < 0 || selectedGroupIndex >= curveGroups.Count)
        {
            ShowNotification(new GUIContent("请选择目标对象"));
            return;
        }
        
        Debug.Log("========== 开始完美转换 ==========");
        
        var group = curveGroups[selectedGroupIndex];
        Debug.Log($"目标对象: {group.displayName} (路径: {group.path})");
        
        // 从选中的对象组提取位置曲线
        AnimationCurve posX = ExtractCurveFromBinding(workingClip, group.bindingX);
        AnimationCurve posY = ExtractCurveFromBinding(workingClip, group.bindingY);
        AnimationCurve posZ = ExtractCurveFromBinding(workingClip, group.bindingZ);
        
        // 转换为速度曲线
        resultXCurve = ConvertPositionToVelocity(workingClip, posX, velocityMultiplier.x);
        resultYCurve = ConvertPositionToVelocity(workingClip, posY, velocityMultiplier.y);
        resultZCurve = ConvertPositionToVelocity(workingClip, posZ, velocityMultiplier.z);
        
        // 应用到粒子系统
        ApplyToParticleSystem(workingClip);
        
        Debug.Log("========== 完美转换完成 ==========");
        ShowNotification(new GUIContent($"转换完成: {group.displayName}"));
    }

    /// <summary>
    /// 从 EditorCurveBinding 提取曲线
    /// </summary>
    private AnimationCurve ExtractCurveFromBinding(AnimationClip clip, EditorCurveBinding? binding)
    {
        if (!binding.HasValue || clip == null)
            return AnimationCurve.Constant(0, clip?.length ?? 1, 0);
        
        var curve = AnimationUtility.GetEditorCurve(clip, binding.Value);
        if (curve != null)
        {
            Debug.Log($"√ 提取曲线: {binding.Value.path}/{binding.Value.propertyName}, 关键帧: {curve.keys.Length}");
            return curve;
        }
        
        return AnimationCurve.Constant(0, clip.length, 0);
    }

    /// <summary>
    /// 将位置曲线转换为速度曲线
    /// </summary>
    private AnimationCurve ConvertPositionToVelocity(AnimationClip clip, AnimationCurve positionCurve, float multiplier)
    {
        if (positionCurve == null || positionCurve.keys.Length == 0)
        {
            return AnimationCurve.Constant(0, 1, 0);
        }
        
        switch (conversionMethod)
        {
            case ConversionMethod.CumulativeVelocity:
                return ConvertUsingCumulativeMethod(clip, positionCurve, multiplier);
            
            case ConversionMethod.InstantVelocity:
                return ConvertUsingInstantMethod(positionCurve, multiplier);
            
            case ConversionMethod.AverageVelocity:
                return ConvertUsingAverageMethod(clip, positionCurve, multiplier);
            
            default:
                return ConvertUsingCumulativeMethod(clip, positionCurve, multiplier);
        }
    }

    /// <summary>
    /// 累积速度法 - 最精确的方法
    /// </summary>
    private AnimationCurve ConvertUsingCumulativeMethod(AnimationClip clip, AnimationCurve posCurve, float multiplier)
    {
        AnimationCurve velCurve = new AnimationCurve();
        
        float duration = clip.length;
        float dt = 1f / convertSampleRate;
        int samples = Mathf.CeilToInt(duration * convertSampleRate);
        
        for (int i = 0; i <= samples; i++)
        {
            float t = Mathf.Min(i * dt, duration);
            float velocity = 0;
            
            if (i < samples)
            {
                float t_next = Mathf.Min((i + 1) * dt, duration);
                float pos_current = posCurve.Evaluate(t);
                float pos_next = posCurve.Evaluate(t_next);
                
                velocity = (pos_next - pos_current) / (t_next - t);
            }
            else
            {
                if (i > 0)
                {
                    float t_prev = (i - 1) * dt;
                    float pos_prev = posCurve.Evaluate(t_prev);
                    float pos_current = posCurve.Evaluate(t);
                    
                    velocity = (pos_current - pos_prev) / dt;
                }
            }
            
            velocity *= multiplier;
            velCurve.AddKey(new Keyframe(t, velocity));
        }
        
        for (int i = 0; i < velCurve.keys.Length; i++)
        {
            velCurve.SmoothTangents(i, 0);
        }
        
        return velCurve;
    }

    /// <summary>
    /// 瞬时速度法 - 使用切线计算
    /// </summary>
    private AnimationCurve ConvertUsingInstantMethod(AnimationCurve posCurve, float multiplier)
    {
        AnimationCurve velCurve = new AnimationCurve();
        
        Keyframe[] keys = posCurve.keys;
        
        for (int i = 0; i < keys.Length; i++)
        {
            float time = keys[i].time;
            float velocity;
            
            if (i == 0 && keys.Length > 1)
            {
                float dt = keys[i + 1].time - keys[i].time;
                velocity = (keys[i + 1].value - keys[i].value) / dt;
            }
            else if (i == keys.Length - 1 && keys.Length > 1)
            {
                float dt = keys[i].time - keys[i - 1].time;
                velocity = (keys[i].value - keys[i - 1].value) / dt;
            }
            else
            {
                float dt = keys[i + 1].time - keys[i - 1].time;
                velocity = (keys[i + 1].value - keys[i - 1].value) / dt;
            }
            
            velocity *= multiplier;
            velCurve.AddKey(new Keyframe(time, velocity));
        }
        
        for (int i = 0; i < velCurve.keys.Length; i++)
        {
            velCurve.SmoothTangents(i, 0);
        }
        
        return velCurve;
    }

    /// <summary>
    /// 平均速度法
    /// </summary>
    private AnimationCurve ConvertUsingAverageMethod(AnimationClip clip, AnimationCurve posCurve, float multiplier)
    {
        AnimationCurve velCurve = new AnimationCurve();
        
        float duration = clip.length;
        float dt = 1f / convertSampleRate;
        int samples = Mathf.CeilToInt(duration * convertSampleRate);
        
        for (int i = 0; i <= samples; i++)
        {
            float t = i * dt;
            if (t > duration) t = duration;
            
            float t_before = Mathf.Max(0, t - dt * 0.5f);
            float t_after = Mathf.Min(duration, t + dt * 0.5f);
            
            float pos_before = posCurve.Evaluate(t_before);
            float pos_after = posCurve.Evaluate(t_after);
            
            float velocity = (pos_after - pos_before) / (t_after - t_before);
            velocity *= multiplier;
            
            velCurve.AddKey(new Keyframe(t, velocity));
        }
        
        for (int i = 0; i < velCurve.keys.Length; i++)
        {
            velCurve.SmoothTangents(i, 0);
        }
        
        return velCurve;
    }

    /// <summary>
    /// 应用到粒子系统
    /// </summary>
    private void ApplyToParticleSystem(AnimationClip clip)
    {
        targetParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        
        var velocityModule = targetParticleSystem.velocityOverLifetime;
        velocityModule.enabled = true;
        velocityModule.space = ParticleSystemSimulationSpace.Local;
        
        float actualCurveLength = clip != null ? clip.length : 1f;
        
        AnimationCurve remappedX = RemapCurveTimeRange(resultXCurve, actualCurveLength);
        AnimationCurve remappedY = RemapCurveTimeRange(resultYCurve, actualCurveLength);
        AnimationCurve remappedZ = RemapCurveTimeRange(resultZCurve, actualCurveLength);
        
        AnimationCurve normalizedX = NormalizeCurveAndGetMultiplier(remappedX ?? AnimationCurve.Constant(0, 1, 0), out xMultiplier);
        AnimationCurve normalizedY = NormalizeCurveAndGetMultiplier(remappedY ?? AnimationCurve.Constant(0, 1, 0), out yMultiplier);
        AnimationCurve normalizedZ = NormalizeCurveAndGetMultiplier(remappedZ ?? AnimationCurve.Constant(0, 1, 0), out zMultiplier);
        
        velocityModule.x = new ParticleSystem.MinMaxCurve(xMultiplier, normalizedX);
        velocityModule.y = new ParticleSystem.MinMaxCurve(yMultiplier, normalizedY);
        velocityModule.z = new ParticleSystem.MinMaxCurve(zMultiplier, normalizedZ);
        
        var main = targetParticleSystem.main;
        main.duration = actualCurveLength;
        main.startLifetime = actualCurveLength;
        main.startSpeed = 0;
        
        EditorUtility.SetDirty(targetParticleSystem);
        
        Debug.Log($"√ 速度曲线已应用到粒子系统");
        Debug.Log($"  曲线乘数 - X: {xMultiplier:F3}, Y: {yMultiplier:F3}, Z: {zMultiplier:F3}");
        Debug.Log($"  粒子生命周期: {actualCurveLength:F3} 秒");
    }

    /// <summary>
    /// 重新映射曲线时间范围从[0, maxTime]到[0, 1]
    /// </summary>
    private AnimationCurve RemapCurveTimeRange(AnimationCurve curve, float maxTime)
    {
        if (curve == null || curve.keys.Length == 0)
        {
            return AnimationCurve.Constant(0, 1, 0);
        }
        
        if (maxTime <= 0) maxTime = 1f;
        
        AnimationCurve remapped = new AnimationCurve();
        
        foreach (Keyframe key in curve.keys)
        {
            float newTime = Mathf.Clamp01(key.time / maxTime);
            
            float timeScale = 1f / maxTime;
            float newInTangent = key.inTangent * timeScale;
            float newOutTangent = key.outTangent * timeScale;
            
            Keyframe remappedKey = new Keyframe(newTime, key.value, newInTangent, newOutTangent);
            remappedKey.weightedMode = key.weightedMode;
            remappedKey.inWeight = key.inWeight;
            remappedKey.outWeight = key.outWeight;
            
            remapped.AddKey(remappedKey);
        }
        
        return remapped;
    }

    /// <summary>
    /// 归一化曲线并获取乘数
    /// </summary>
    private AnimationCurve NormalizeCurveAndGetMultiplier(AnimationCurve curve, out float multiplier)
    {
        if (curve == null || curve.length == 0)
        {
            multiplier = 1.0f;
            return new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 0));
        }
        
        float maxAbsValue = 0;
        foreach (Keyframe key in curve.keys)
        {
            float absValue = Mathf.Abs(key.value);
            if (absValue > maxAbsValue)
            {
                maxAbsValue = absValue;
            }
        }
        
        if (maxAbsValue < 0.0001f)
        {
            multiplier = 1.0f;
            return curve;
        }
        
        multiplier = maxAbsValue;
        
        AnimationCurve normalizedCurve = new AnimationCurve();
        foreach (Keyframe key in curve.keys)
        {
            float normalizedValue = key.value / maxAbsValue;
            float normalizedInTangent = key.inTangent / maxAbsValue;
            float normalizedOutTangent = key.outTangent / maxAbsValue;
            
            Keyframe newKey = new Keyframe(key.time, normalizedValue, normalizedInTangent, normalizedOutTangent);
            newKey.weightedMode = key.weightedMode;
            newKey.inWeight = key.inWeight;
            newKey.outWeight = key.outWeight;
            
            normalizedCurve.AddKey(newKey);
        }
        
        return normalizedCurve;
    }

    #endregion

    #region 粒子系统曲线优化

    /// <summary>
    /// 计算粒子系统列表的哈希值，用于检测变化
    /// </summary>
    private int GetParticleSystemsHash()
    {
        if (optimizeTargetParticleSystems == null || optimizeTargetParticleSystems.Count == 0)
            return 0;
        
        int hash = optimizeTargetParticleSystems.Count;
        foreach (var ps in optimizeTargetParticleSystems)
        {
            if (ps != null)
                hash ^= ps.GetInstanceID();
        }
        return hash;
    }
    
    /// <summary>
    /// 扫描所有选中粒子系统的曲线（批量扫描）
    /// </summary>
    private void ScanAllParticleSystemsCurves(bool preserveSelection = false)
    {
        // 保存当前选中状态
        HashSet<string> selectedCurveKeys = null;
        if (preserveSelection && particleCurveInfos != null && particleCurveInfos.Count > 0)
        {
            selectedCurveKeys = new HashSet<string>(
                particleCurveInfos.Where(c => c.isSelected)
                                  .Select(c => $"{c.particleSystemName}|{c.moduleName}|{c.propertyName}")
            );
        }
        
        particleCurveInfos.Clear();
        particleModuleFoldouts.Clear();
        
        if (optimizeTargetParticleSystems == null || optimizeTargetParticleSystems.Count == 0) return;
        
        // 遍历所有粒子系统
        foreach (var ps in optimizeTargetParticleSystems)
        {
            if (ps == null) continue;
            ScanSingleParticleSystemCurves(ps);
        }
        
        // 初始化模块折叠状态
        foreach (var info in particleCurveInfos)
        {
            string foldoutKey = $"{info.particleSystemName}|{info.moduleName}";
            if (!particleModuleFoldouts.ContainsKey(foldoutKey))
            {
                particleModuleFoldouts[foldoutKey] = true;
            }
        }
        
        particleCurvesScanned = true;
        
        // 恢复选中状态或默认选中所有可优化的曲线
        if (preserveSelection && selectedCurveKeys != null && selectedCurveKeys.Count > 0)
        {
            foreach (var info in particleCurveInfos)
            {
                string key = $"{info.particleSystemName}|{info.moduleName}|{info.propertyName}";
                info.isSelected = selectedCurveKeys.Contains(key);
            }
        }
        else
        {
            foreach (var info in particleCurveInfos)
            {
                info.isSelected = info.CanOptimize && particleSelectAll;
            }
        }
    }
    
    /// <summary>
    /// 扫描单个粒子系统的所有曲线
    /// </summary>
    private void ScanSingleParticleSystemCurves(ParticleSystem ps)
    {
        if (ps == null) return;
        
        string psName = ps.gameObject.name;
        
        // Main 模块
        var main = ps.main;
        AddMinMaxCurve(ps, psName, "Main", "Start Lifetime", main.startLifetime, 
            (curve, curveMin, mult) => { var m = ps.main; m.startLifetime = CreateMinMaxCurve(curve, curveMin, mult, main.startLifetime.mode); });
        AddMinMaxCurve(ps, psName, "Main", "Start Speed", main.startSpeed,
            (curve, curveMin, mult) => { var m = ps.main; m.startSpeed = CreateMinMaxCurve(curve, curveMin, mult, main.startSpeed.mode); });
        AddMinMaxCurve(ps, psName, "Main", "Start Size", main.startSize,
            (curve, curveMin, mult) => { var m = ps.main; m.startSize = CreateMinMaxCurve(curve, curveMin, mult, main.startSize.mode); });
        AddMinMaxCurve(ps, psName, "Main", "Start Rotation", main.startRotation,
            (curve, curveMin, mult) => { var m = ps.main; m.startRotation = CreateMinMaxCurve(curve, curveMin, mult, main.startRotation.mode); });
        AddMinMaxCurve(ps, psName, "Main", "Gravity Modifier", main.gravityModifier,
            (curve, curveMin, mult) => { var m = ps.main; m.gravityModifier = CreateMinMaxCurve(curve, curveMin, mult, main.gravityModifier.mode); });
        
        // Emission 模块
        var emission = ps.emission;
        if (emission.enabled)
        {
            AddMinMaxCurve(ps, psName, "Emission", "Rate over Time", emission.rateOverTime,
                (curve, curveMin, mult) => { var m = ps.emission; m.rateOverTime = CreateMinMaxCurve(curve, curveMin, mult, emission.rateOverTime.mode); });
            AddMinMaxCurve(ps, psName, "Emission", "Rate over Distance", emission.rateOverDistance,
                (curve, curveMin, mult) => { var m = ps.emission; m.rateOverDistance = CreateMinMaxCurve(curve, curveMin, mult, emission.rateOverDistance.mode); });
        }
        
        // Velocity over Lifetime 模块
        var velocityOverLifetime = ps.velocityOverLifetime;
        if (velocityOverLifetime.enabled)
        {
            AddMinMaxCurve(ps, psName, "Velocity over Lifetime", "X", velocityOverLifetime.x,
                (curve, curveMin, mult) => { var m = ps.velocityOverLifetime; m.x = CreateMinMaxCurve(curve, curveMin, mult, velocityOverLifetime.x.mode); });
            AddMinMaxCurve(ps, psName, "Velocity over Lifetime", "Y", velocityOverLifetime.y,
                (curve, curveMin, mult) => { var m = ps.velocityOverLifetime; m.y = CreateMinMaxCurve(curve, curveMin, mult, velocityOverLifetime.y.mode); });
            AddMinMaxCurve(ps, psName, "Velocity over Lifetime", "Z", velocityOverLifetime.z,
                (curve, curveMin, mult) => { var m = ps.velocityOverLifetime; m.z = CreateMinMaxCurve(curve, curveMin, mult, velocityOverLifetime.z.mode); });
            AddMinMaxCurve(ps, psName, "Velocity over Lifetime", "Speed Modifier", velocityOverLifetime.speedModifier,
                (curve, curveMin, mult) => { var m = ps.velocityOverLifetime; m.speedModifier = CreateMinMaxCurve(curve, curveMin, mult, velocityOverLifetime.speedModifier.mode); });
            AddMinMaxCurve(ps, psName, "Velocity over Lifetime", "Radial", velocityOverLifetime.radial,
                (curve, curveMin, mult) => { var m = ps.velocityOverLifetime; m.radial = CreateMinMaxCurve(curve, curveMin, mult, velocityOverLifetime.radial.mode); });
            AddMinMaxCurve(ps, psName, "Velocity over Lifetime", "Orbital X", velocityOverLifetime.orbitalX,
                (curve, curveMin, mult) => { var m = ps.velocityOverLifetime; m.orbitalX = CreateMinMaxCurve(curve, curveMin, mult, velocityOverLifetime.orbitalX.mode); });
            AddMinMaxCurve(ps, psName, "Velocity over Lifetime", "Orbital Y", velocityOverLifetime.orbitalY,
                (curve, curveMin, mult) => { var m = ps.velocityOverLifetime; m.orbitalY = CreateMinMaxCurve(curve, curveMin, mult, velocityOverLifetime.orbitalY.mode); });
            AddMinMaxCurve(ps, psName, "Velocity over Lifetime", "Orbital Z", velocityOverLifetime.orbitalZ,
                (curve, curveMin, mult) => { var m = ps.velocityOverLifetime; m.orbitalZ = CreateMinMaxCurve(curve, curveMin, mult, velocityOverLifetime.orbitalZ.mode); });
        }
        
        // Limit Velocity over Lifetime 模块
        var limitVelocity = ps.limitVelocityOverLifetime;
        if (limitVelocity.enabled)
        {
            AddMinMaxCurve(ps, psName, "Limit Velocity", "Limit X", limitVelocity.limitX,
                (curve, curveMin, mult) => { var m = ps.limitVelocityOverLifetime; m.limitX = CreateMinMaxCurve(curve, curveMin, mult, limitVelocity.limitX.mode); });
            AddMinMaxCurve(ps, psName, "Limit Velocity", "Limit Y", limitVelocity.limitY,
                (curve, curveMin, mult) => { var m = ps.limitVelocityOverLifetime; m.limitY = CreateMinMaxCurve(curve, curveMin, mult, limitVelocity.limitY.mode); });
            AddMinMaxCurve(ps, psName, "Limit Velocity", "Limit Z", limitVelocity.limitZ,
                (curve, curveMin, mult) => { var m = ps.limitVelocityOverLifetime; m.limitZ = CreateMinMaxCurve(curve, curveMin, mult, limitVelocity.limitZ.mode); });
            AddMinMaxCurve(ps, psName, "Limit Velocity", "Drag", limitVelocity.drag,
                (curve, curveMin, mult) => { var m = ps.limitVelocityOverLifetime; m.drag = CreateMinMaxCurve(curve, curveMin, mult, limitVelocity.drag.mode); });
        }
        
        // Force over Lifetime 模块
        var forceOverLifetime = ps.forceOverLifetime;
        if (forceOverLifetime.enabled)
        {
            AddMinMaxCurve(ps, psName, "Force over Lifetime", "X", forceOverLifetime.x,
                (curve, curveMin, mult) => { var m = ps.forceOverLifetime; m.x = CreateMinMaxCurve(curve, curveMin, mult, forceOverLifetime.x.mode); });
            AddMinMaxCurve(ps, psName, "Force over Lifetime", "Y", forceOverLifetime.y,
                (curve, curveMin, mult) => { var m = ps.forceOverLifetime; m.y = CreateMinMaxCurve(curve, curveMin, mult, forceOverLifetime.y.mode); });
            AddMinMaxCurve(ps, psName, "Force over Lifetime", "Z", forceOverLifetime.z,
                (curve, curveMin, mult) => { var m = ps.forceOverLifetime; m.z = CreateMinMaxCurve(curve, curveMin, mult, forceOverLifetime.z.mode); });
        }
        
        // Size over Lifetime 模块
        var sizeOverLifetime = ps.sizeOverLifetime;
        if (sizeOverLifetime.enabled)
        {
            AddMinMaxCurve(ps, psName, "Size over Lifetime", "Size", sizeOverLifetime.size,
                (curve, curveMin, mult) => { var m = ps.sizeOverLifetime; m.size = CreateMinMaxCurve(curve, curveMin, mult, sizeOverLifetime.size.mode); });
            AddMinMaxCurve(ps, psName, "Size over Lifetime", "Size X", sizeOverLifetime.x,
                (curve, curveMin, mult) => { var m = ps.sizeOverLifetime; m.x = CreateMinMaxCurve(curve, curveMin, mult, sizeOverLifetime.x.mode); });
            AddMinMaxCurve(ps, psName, "Size over Lifetime", "Size Y", sizeOverLifetime.y,
                (curve, curveMin, mult) => { var m = ps.sizeOverLifetime; m.y = CreateMinMaxCurve(curve, curveMin, mult, sizeOverLifetime.y.mode); });
            AddMinMaxCurve(ps, psName, "Size over Lifetime", "Size Z", sizeOverLifetime.z,
                (curve, curveMin, mult) => { var m = ps.sizeOverLifetime; m.z = CreateMinMaxCurve(curve, curveMin, mult, sizeOverLifetime.z.mode); });
        }
        
        // Size by Speed 模块
        var sizeBySpeed = ps.sizeBySpeed;
        if (sizeBySpeed.enabled)
        {
            AddMinMaxCurve(ps, psName, "Size by Speed", "Size", sizeBySpeed.size,
                (curve, curveMin, mult) => { var m = ps.sizeBySpeed; m.size = CreateMinMaxCurve(curve, curveMin, mult, sizeBySpeed.size.mode); });
            AddMinMaxCurve(ps, psName, "Size by Speed", "Size X", sizeBySpeed.x,
                (curve, curveMin, mult) => { var m = ps.sizeBySpeed; m.x = CreateMinMaxCurve(curve, curveMin, mult, sizeBySpeed.x.mode); });
            AddMinMaxCurve(ps, psName, "Size by Speed", "Size Y", sizeBySpeed.y,
                (curve, curveMin, mult) => { var m = ps.sizeBySpeed; m.y = CreateMinMaxCurve(curve, curveMin, mult, sizeBySpeed.y.mode); });
            AddMinMaxCurve(ps, psName, "Size by Speed", "Size Z", sizeBySpeed.z,
                (curve, curveMin, mult) => { var m = ps.sizeBySpeed; m.z = CreateMinMaxCurve(curve, curveMin, mult, sizeBySpeed.z.mode); });
        }
        
        // Rotation over Lifetime 模块
        var rotationOverLifetime = ps.rotationOverLifetime;
        if (rotationOverLifetime.enabled)
        {
            AddMinMaxCurve(ps, psName, "Rotation over Lifetime", "Angular Velocity", rotationOverLifetime.z,
                (curve, curveMin, mult) => { var m = ps.rotationOverLifetime; m.z = CreateMinMaxCurve(curve, curveMin, mult, rotationOverLifetime.z.mode); });
            AddMinMaxCurve(ps, psName, "Rotation over Lifetime", "Angular Velocity X", rotationOverLifetime.x,
                (curve, curveMin, mult) => { var m = ps.rotationOverLifetime; m.x = CreateMinMaxCurve(curve, curveMin, mult, rotationOverLifetime.x.mode); });
            AddMinMaxCurve(ps, psName, "Rotation over Lifetime", "Angular Velocity Y", rotationOverLifetime.y,
                (curve, curveMin, mult) => { var m = ps.rotationOverLifetime; m.y = CreateMinMaxCurve(curve, curveMin, mult, rotationOverLifetime.y.mode); });
        }
        
        // Rotation by Speed 模块
        var rotationBySpeed = ps.rotationBySpeed;
        if (rotationBySpeed.enabled)
        {
            AddMinMaxCurve(ps, psName, "Rotation by Speed", "Angular Velocity", rotationBySpeed.z,
                (curve, curveMin, mult) => { var m = ps.rotationBySpeed; m.z = CreateMinMaxCurve(curve, curveMin, mult, rotationBySpeed.z.mode); });
            AddMinMaxCurve(ps, psName, "Rotation by Speed", "Angular Velocity X", rotationBySpeed.x,
                (curve, curveMin, mult) => { var m = ps.rotationBySpeed; m.x = CreateMinMaxCurve(curve, curveMin, mult, rotationBySpeed.x.mode); });
            AddMinMaxCurve(ps, psName, "Rotation by Speed", "Angular Velocity Y", rotationBySpeed.y,
                (curve, curveMin, mult) => { var m = ps.rotationBySpeed; m.y = CreateMinMaxCurve(curve, curveMin, mult, rotationBySpeed.y.mode); });
        }
        
        // Noise 模块
        var noise = ps.noise;
        if (noise.enabled)
        {
            AddMinMaxCurve(ps, psName, "Noise", "Strength", noise.strength,
                (curve, curveMin, mult) => { var m = ps.noise; m.strength = CreateMinMaxCurve(curve, curveMin, mult, noise.strength.mode); });
            AddMinMaxCurve(ps, psName, "Noise", "Strength X", noise.strengthX,
                (curve, curveMin, mult) => { var m = ps.noise; m.strengthX = CreateMinMaxCurve(curve, curveMin, mult, noise.strengthX.mode); });
            AddMinMaxCurve(ps, psName, "Noise", "Strength Y", noise.strengthY,
                (curve, curveMin, mult) => { var m = ps.noise; m.strengthY = CreateMinMaxCurve(curve, curveMin, mult, noise.strengthY.mode); });
            AddMinMaxCurve(ps, psName, "Noise", "Strength Z", noise.strengthZ,
                (curve, curveMin, mult) => { var m = ps.noise; m.strengthZ = CreateMinMaxCurve(curve, curveMin, mult, noise.strengthZ.mode); });
            AddMinMaxCurve(ps, psName, "Noise", "Scroll Speed", noise.scrollSpeed,
                (curve, curveMin, mult) => { var m = ps.noise; m.scrollSpeed = CreateMinMaxCurve(curve, curveMin, mult, noise.scrollSpeed.mode); });
            AddMinMaxCurve(ps, psName, "Noise", "Position Amount", noise.positionAmount,
                (curve, curveMin, mult) => { var m = ps.noise; m.positionAmount = CreateMinMaxCurve(curve, curveMin, mult, noise.positionAmount.mode); });
            AddMinMaxCurve(ps, psName, "Noise", "Rotation Amount", noise.rotationAmount,
                (curve, curveMin, mult) => { var m = ps.noise; m.rotationAmount = CreateMinMaxCurve(curve, curveMin, mult, noise.rotationAmount.mode); });
            AddMinMaxCurve(ps, psName, "Noise", "Size Amount", noise.sizeAmount,
                (curve, curveMin, mult) => { var m = ps.noise; m.sizeAmount = CreateMinMaxCurve(curve, curveMin, mult, noise.sizeAmount.mode); });
        }
    }

    /// <summary>
    /// 扫描粒子系统的所有曲线（保留向后兼容）
    /// </summary>
    /// <param name="preserveSelection">是否保留当前选中状态</param>
    private void ScanParticleSystemCurves(bool preserveSelection = false)
    {
        ScanAllParticleSystemsCurves(preserveSelection);
    }
    
    /// <summary>
    /// 添加 MinMaxCurve 到曲线列表（支持批量）
    /// </summary>
    private void AddMinMaxCurve(ParticleSystem ps, string psName, string moduleName, string propertyName, ParticleSystem.MinMaxCurve minMaxCurve, 
        System.Action<AnimationCurve, AnimationCurve, float> applyCurve)
    {
        var info = new ParticleCurveInfo
        {
            particleSystem = ps,
            particleSystemName = psName,
            moduleName = moduleName,
            propertyName = propertyName,
            displayName = $"[{psName}] {moduleName}/{propertyName}",
            mode = minMaxCurve.mode,
            multiplier = minMaxCurve.curveMultiplier,
            applyCurve = applyCurve,
            isSelected = false
        };
        
        switch (minMaxCurve.mode)
        {
            case ParticleSystemCurveMode.Curve:
                info.curve = CopyAnimationCurve(minMaxCurve.curve);
                info.originalKeyCount = info.curve?.keys.Length ?? 0;
                break;
            case ParticleSystemCurveMode.TwoCurves:
                info.curve = CopyAnimationCurve(minMaxCurve.curveMax);
                info.curveMin = CopyAnimationCurve(minMaxCurve.curveMin);
                info.originalKeyCount = (info.curve?.keys.Length ?? 0) + (info.curveMin?.keys.Length ?? 0);
                break;
            case ParticleSystemCurveMode.Constant:
            case ParticleSystemCurveMode.TwoConstants:
                info.originalKeyCount = 0;
                break;
        }
        
        particleCurveInfos.Add(info);
    }
    
    /// <summary>
    /// 复制 AnimationCurve
    /// </summary>
    private AnimationCurve CopyAnimationCurve(AnimationCurve source)
    {
        if (source == null) return null;
        var copy = new AnimationCurve(source.keys);
        copy.preWrapMode = source.preWrapMode;
        copy.postWrapMode = source.postWrapMode;
        return copy;
    }
    
    /// <summary>
    /// 根据模式创建 MinMaxCurve
    /// </summary>
    private ParticleSystem.MinMaxCurve CreateMinMaxCurve(AnimationCurve curve, AnimationCurve curveMin, float multiplier, ParticleSystemCurveMode mode)
    {
        switch (mode)
        {
            case ParticleSystemCurveMode.Curve:
                return new ParticleSystem.MinMaxCurve(multiplier, curve ?? AnimationCurve.Constant(0, 1, 1));
            case ParticleSystemCurveMode.TwoCurves:
                return new ParticleSystem.MinMaxCurve(multiplier, 
                    curveMin ?? AnimationCurve.Constant(0, 1, 0), 
                    curve ?? AnimationCurve.Constant(0, 1, 1));
            default:
                return new ParticleSystem.MinMaxCurve(multiplier, curve ?? AnimationCurve.Constant(0, 1, 1));
        }
    }
    
    /// <summary>
    /// 执行粒子系统曲线优化（支持批量）
    /// </summary>
    private void ExecuteParticleCurveOptimize()
    {
        if (optimizeTargetParticleSystems == null || optimizeTargetParticleSystems.Count == 0)
        {
            ShowNotification(new GUIContent("请先选择粒子系统"));
            return;
        }
        
        var selectedCurves = particleCurveInfos.Where(c => c.isSelected && c.CanOptimize).ToList();
        if (selectedCurves.Count == 0)
        {
            ShowNotification(new GUIContent("请选择要优化的曲线"));
            return;
        }
        
        // 记录所有涉及的粒子系统用于 Undo
        var affectedParticleSystems = selectedCurves.Select(c => c.particleSystem).Where(ps => ps != null).Distinct().ToList();
        foreach (var ps in affectedParticleSystems)
        {
            Undo.RecordObject(ps, "Optimize Particle Curves");
        }
        
        int totalOriginal = 0, totalOptimized = 0, curvesOptimized = 0;
        
        foreach (var info in selectedCurves)
        {
            int originalKeys = 0;
            int optimizedKeys = 0;
            
            // 优化主曲线
            if (info.curve != null && info.curve.keys.Length >= 3)
            {
                originalKeys += info.curve.keys.Length;
                info.curve = OptimizeCurveDP(info.curve, particleOptimizeTolerance, 60f);
                optimizedKeys += info.curve.keys.Length;
            }
            
            // 优化最小曲线（TwoCurves模式）
            if (info.curveMin != null && info.curveMin.keys.Length >= 3)
            {
                originalKeys += info.curveMin.keys.Length;
                info.curveMin = OptimizeCurveDP(info.curveMin, particleOptimizeTolerance, 60f);
                optimizedKeys += info.curveMin.keys.Length;
            }
            
            // 应用优化后的曲线
            info.applyCurve?.Invoke(info.curve, info.curveMin, info.multiplier);
            info.optimizedKeyCount = optimizedKeys;
            
            totalOriginal += originalKeys;
            totalOptimized += optimizedKeys;
            curvesOptimized++;
        }
        
        // 标记所有涉及的粒子系统为脏
        foreach (var ps in affectedParticleSystems)
        {
            EditorUtility.SetDirty(ps);
        }
        
        // 重新扫描以更新显示（保留选中状态）
        ScanParticleSystemCurves(true);
        
        float reduction = totalOriginal > 0 ? (1f - (float)totalOptimized / totalOriginal) * 100f : 0f;
        ShowNotification(new GUIContent($"优化完成: {totalOriginal} → {totalOptimized} (减少{reduction:F1}%)"));
        Debug.Log($"[粒子曲线优化] 处理了 {curvesOptimized} 条曲线, 涉及 {affectedParticleSystems.Count} 个粒子系统, 关键帧: {totalOriginal} → {totalOptimized}, 减少: {reduction:F1}%");
    }
    
    /// <summary>
    /// 执行粒子系统曲线水平翻转（支持批量）
    /// </summary>
    private void ExecuteParticleFlipHorizontal()
    {
        if (optimizeTargetParticleSystems == null || optimizeTargetParticleSystems.Count == 0)
        {
            ShowNotification(new GUIContent("请先选择粒子系统"));
            return;
        }
        
        var curvesToFlip = particleFlipAllCurves ? 
            particleCurveInfos.Where(c => c.CanOptimize).ToList() :
            particleCurveInfos.Where(c => c.isSelected && c.CanOptimize).ToList();
        
        if (curvesToFlip.Count == 0)
        {
            ShowNotification(new GUIContent("没有可翻转的曲线"));
            return;
        }
        
        // 记录所有涉及的粒子系统用于 Undo
        var affectedParticleSystems = curvesToFlip.Select(c => c.particleSystem).Where(ps => ps != null).Distinct().ToList();
        foreach (var ps in affectedParticleSystems)
        {
            Undo.RecordObject(ps, "Flip Particle Curves Horizontal");
        }
        
        int flippedCount = 0;
        
        foreach (var info in curvesToFlip)
        {
            bool flipped = false;
            
            // 翻转主曲线
            if (info.curve != null && info.curve.keys.Length >= 2)
            {
                info.curve = FlipAnimationCurveHorizontal(info.curve);
                flipped = true;
            }
            
            // 翻转最小曲线（TwoCurves模式）
            if (info.curveMin != null && info.curveMin.keys.Length >= 2)
            {
                info.curveMin = FlipAnimationCurveHorizontal(info.curveMin);
                flipped = true;
            }
            
            if (flipped)
            {
                // 应用翻转后的曲线
                info.applyCurve?.Invoke(info.curve, info.curveMin, info.multiplier);
                flippedCount++;
            }
        }
        
        // 标记所有涉及的粒子系统为脏
        foreach (var ps in affectedParticleSystems)
        {
            EditorUtility.SetDirty(ps);
        }
        
        // 重新扫描以更新显示（保留选中状态）
        ScanParticleSystemCurves(true);
        
        ShowNotification(new GUIContent($"水平翻转完成: {flippedCount} 条曲线"));
        Debug.Log($"[粒子曲线翻转] 水平翻转了 {flippedCount} 条曲线, 涉及 {affectedParticleSystems.Count} 个粒子系统");
    }
    
    /// <summary>
    /// 执行粒子系统曲线垂直翻转（支持批量）
    /// </summary>
    private void ExecuteParticleFlipVertical()
    {
        if (optimizeTargetParticleSystems == null || optimizeTargetParticleSystems.Count == 0)
        {
            ShowNotification(new GUIContent("请先选择粒子系统"));
            return;
        }
        
        var curvesToFlip = particleFlipAllCurves ? 
            particleCurveInfos.Where(c => c.CanOptimize).ToList() :
            particleCurveInfos.Where(c => c.isSelected && c.CanOptimize).ToList();
        
        if (curvesToFlip.Count == 0)
        {
            ShowNotification(new GUIContent("没有可翻转的曲线"));
            return;
        }
        
        // 记录所有涉及的粒子系统用于 Undo
        var affectedParticleSystems = curvesToFlip.Select(c => c.particleSystem).Where(ps => ps != null).Distinct().ToList();
        foreach (var ps in affectedParticleSystems)
        {
            Undo.RecordObject(ps, "Flip Particle Curves Vertical");
        }
        
        int flippedCount = 0;
        
        foreach (var info in curvesToFlip)
        {
            bool flipped = false;
            
            // 计算中心值
            float centerValue = particleVerticalFlipCenter;
            if (particleAutoCalculateCenter)
            {
                float minVal = float.MaxValue;
                float maxVal = float.MinValue;
                
                if (info.curve != null)
                {
                    foreach (var key in info.curve.keys)
                    {
                        minVal = Mathf.Min(minVal, key.value);
                        maxVal = Mathf.Max(maxVal, key.value);
                    }
                }
                if (info.curveMin != null)
                {
                    foreach (var key in info.curveMin.keys)
                    {
                        minVal = Mathf.Min(minVal, key.value);
                        maxVal = Mathf.Max(maxVal, key.value);
                    }
                }
                
                if (minVal != float.MaxValue && maxVal != float.MinValue)
                {
                    centerValue = (minVal + maxVal) / 2f;
                }
            }
            
            // 翻转主曲线
            if (info.curve != null && info.curve.keys.Length >= 1)
            {
                info.curve = FlipAnimationCurveVertical(info.curve, centerValue);
                flipped = true;
            }
            
            // 翻转最小曲线（TwoCurves模式）
            if (info.curveMin != null && info.curveMin.keys.Length >= 1)
            {
                info.curveMin = FlipAnimationCurveVertical(info.curveMin, centerValue);
                flipped = true;
            }
            
            if (flipped)
            {
                // 应用翻转后的曲线
                info.applyCurve?.Invoke(info.curve, info.curveMin, info.multiplier);
                flippedCount++;
            }
        }
        
        // 标记所有涉及的粒子系统为脏
        foreach (var ps in affectedParticleSystems)
        {
            EditorUtility.SetDirty(ps);
        }
        
        // 重新扫描以更新显示（保留选中状态）
        ScanParticleSystemCurves(true);
        
        ShowNotification(new GUIContent($"垂直翻转完成: {flippedCount} 条曲线"));
        Debug.Log($"[粒子曲线翻转] 垂直翻转了 {flippedCount} 条曲线, 涉及 {affectedParticleSystems.Count} 个粒子系统");
    }
    
    /// <summary>
    /// 水平翻转AnimationCurve（时间轴翻转）
    /// </summary>
    private AnimationCurve FlipAnimationCurveHorizontal(AnimationCurve curve)
    {
        if (curve == null || curve.keys.Length == 0) return curve;
        
        var keys = curve.keys;
        float startTime = keys[0].time;
        float endTime = keys[keys.Length - 1].time;
        float curveLength = endTime - startTime;
        
        var newCurve = new AnimationCurve();
        
        // 从后向前遍历，创建翻转后的关键帧
        for (int i = keys.Length - 1; i >= 0; i--)
        {
            float newTime = startTime + curveLength - (keys[i].time - startTime);
            
            // 水平翻转时，交换并取反切线
            var newKey = new Keyframe(
                newTime,
                keys[i].value,
                -keys[i].outTangent,  // in切线 = 原out切线取反
                -keys[i].inTangent,   // out切线 = 原in切线取反
                keys[i].outWeight,    // weight也要交换
                keys[i].inWeight
            );
            newKey.weightedMode = keys[i].weightedMode;
            
            newCurve.AddKey(newKey);
        }
        
        return newCurve;
    }
    
    /// <summary>
    /// 垂直翻转AnimationCurve（值翻转）
    /// </summary>
    private AnimationCurve FlipAnimationCurveVertical(AnimationCurve curve, float centerValue)
    {
        if (curve == null || curve.keys.Length == 0) return curve;
        
        var keys = curve.keys;
        
        for (int i = 0; i < keys.Length; i++)
        {
            // 值关于中心点翻转：newValue = center - (value - center) = 2*center - value
            float flippedValue = 2f * centerValue - keys[i].value;
            
            keys[i] = new Keyframe(
                keys[i].time,
                flippedValue,
                -keys[i].inTangent,   // 切线取反
                -keys[i].outTangent,  // 切线取反
                keys[i].inWeight,
                keys[i].outWeight
            );
            keys[i].weightedMode = curve.keys[i].weightedMode;
        }
        
        return new AnimationCurve(keys);
    }

    #endregion
}
