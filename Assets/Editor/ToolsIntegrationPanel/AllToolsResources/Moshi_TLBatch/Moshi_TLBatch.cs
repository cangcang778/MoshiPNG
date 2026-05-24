using UnityEngine;
using UnityEditor;
using UnityEngine.Timeline;
using UnityEditor.Timeline;
using UnityEngine.Playables;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// Timeline Clip批量导入工具
/// 支持多种排列模式：阶梯下行、阶梯上行、单轨序列、单轨重叠
/// </summary>
public class Moshi_TLBatch : EditorWindow
{
    private const string TOOL_NAME = "TL批量导入";

    #region 枚举定义
    
    /// <summary>
    /// 排列模式
    /// </summary>
    private enum ArrangeMode
    {
        StairDown,      // 阶梯下行：独立轨道，时间递增（从上到下）
        StairUp,        // 阶梯上行：独立轨道，时间递增（从下到上）
        SingleSequence, // 单轨序列：同一轨道，首尾相接
        SingleOverlap   // 单轨重叠：同一轨道，可配置间隔
    }

    /// <summary>
    /// 时间单位
    /// </summary>
    private enum TimeUnit
    {
        Frame,  // 帧
        Second  // 秒
    }

    /// <summary>
    /// 起始时间模式
    /// </summary>
    private enum StartTimeMode
    {
        FromPlayhead,   // 从播放头
        FromZero,       // 从零开始
        Custom          // 自定义
    }

    /// <summary>
    /// 插入位置
    /// </summary>
    private enum InsertPosition
    {
        InSelected,  // 选中轨道内
        Above,       // 轨道上新建
        Below        // 轨道下新建
    }

    /// <summary>
    /// 对象排序方式
    /// </summary>
    private enum SortMode
    {
        ByName,      // 名称排列（按名称字母/数字排序）
        Natural,     // 自然排列（保持Selection原始顺序）
        ByHierarchy  // 结构排列（按Hierarchy层级结构）
    }

    /// <summary>
    /// 主页签类型
    /// </summary>
    private enum MainTabType
    {
        Import,  // 导入
        Modify   // 修改
    }

    /// <summary>
    /// 批量起始时间模式
    /// </summary>
    private enum BatchStartTimeMode
    {
        Custom,         // 自定义
        FromPlayhead,   // 播放头
        FromZero,       // 从零
        KeepFirst       // 保持首位
    }

    #endregion

    #region 字段

    // 导入设置
    private ArrangeMode arrangeMode = ArrangeMode.StairDown;
    private StartTimeMode startTimeMode = StartTimeMode.Custom;
    private InsertPosition insertPosition = InsertPosition.InSelected;
    private SortMode sortMode = SortMode.ByHierarchy;

    // 时间参数（默认按帧）
    private TimeUnit durationUnit = TimeUnit.Frame;
    private TimeUnit offsetUnit = TimeUnit.Frame;
    private TimeUnit startTimeUnit = TimeUnit.Frame;

    private int durationFrames = 120;
    private float durationSeconds = 2f;
    private int offsetFrames = 15;
    private float offsetSeconds = 0.25f;
    private int customStartFrames = 0;
    private float customStartSeconds = 0f;

    // 帧率
    private float frameRate = 60f;

    // UI
    private Vector2 scrollPosition;

    // 主页签
    private MainTabType mainTab = MainTabType.Import;
    
    // 修改时长激活状态
    private bool enableDurationModify = true;
    
    // 重新排列激活状态
    private bool enableRearrange = true;
    
    // 批量修改Duration
    private TimeUnit batchDurationUnit = TimeUnit.Frame;
    private int batchDurationFrames = 120;
    private float batchDurationSeconds = 2f;
    
    // 批量重新排列
    private ArrangeMode batchArrangeMode = ArrangeMode.StairDown;
    private BatchStartTimeMode batchStartTimeMode = BatchStartTimeMode.KeepFirst;
    private TimeUnit batchStartTimeUnit = TimeUnit.Frame;
    private int batchCustomStartFrames = 0;
    private float batchCustomStartSeconds = 0f;
    private TimeUnit batchOffsetUnit = TimeUnit.Frame;
    private int batchOffsetFrames = 15;
    private float batchOffsetSeconds = 0.25f;

    // 样式
    private GUIStyle headerStyle;
    private bool stylesInitialized = false;

    // 锁定的导入源
    private List<GameObject> lockedObjects = new List<GameObject>();
    private bool useLockedObjects = false;

    #endregion

    [MenuItem("工具/Moshi/" + TOOL_NAME)]
    public static void ShowWindow()
    {
        var window = GetWindow<Moshi_TLBatch>(TOOL_NAME);
        window.minSize = new Vector2(420, 550);
    }

    private void OnEnable()
    {
        UpdateFrameRate();
    }

    private void InitStyles()
    {
        if (stylesInitialized) return;

        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13
        };

        stylesInitialized = true;
    }

    private void OnGUI()
    {
        InitStyles();
        UpdateFrameRate();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // 标题栏
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Timeline Clip批量工具", headerStyle);
        GUILayout.FlexibleSpace();
        MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);

        // 显示当前帧率
        EditorGUILayout.HelpBox($"当前帧率: {frameRate} fps", MessageType.None);

        GUILayout.Space(10);

        // 主页签切换
        DrawMainTabs();

        GUILayout.Space(10);

        // 根据页签显示内容
        if (mainTab == MainTabType.Import)
        {
            DrawImportTab();
        }
        else
        {
            DrawModifyTab();
        }

        EditorGUILayout.EndScrollView();
    }

    #region UI绘制

    /// <summary>
    /// 绘制主页签切换
    /// </summary>
    private void DrawMainTabs()
    {
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Toggle(mainTab == MainTabType.Import, "导入", EditorStyles.miniButtonLeft, GUILayout.Width(60), GUILayout.Height(25)))
            mainTab = MainTabType.Import;
        if (GUILayout.Toggle(mainTab == MainTabType.Modify, "修改", EditorStyles.miniButtonRight, GUILayout.Width(60), GUILayout.Height(25)))
            mainTab = MainTabType.Modify;
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制导入页签内容
    /// </summary>
    private void DrawImportTab()
    {
        // Hierarchy选择信息
        DrawHierarchySelectionInfo();

        GUILayout.Space(10);

        // 排列模式选择
        DrawArrangeModeSelector();

        GUILayout.Space(10);

        // 时间参数配置
        DrawTimeSettings();

        GUILayout.Space(10);

        // 操作按钮
        DrawActionButtons();
    }

    /// <summary>
    /// 绘制修改页签内容
    /// </summary>
    private void DrawModifyTab()
    {
        EditorGUILayout.HelpBox("选中Timeline中的Clip后，可批量修改时长和重新排列", MessageType.Info);

        GUILayout.Space(10);

        // 修改时长区域（带激活开关）
        DrawDurationModifySection();

        GUILayout.Space(15);

        // 重新排列区域（始终显示）
        DrawRearrangeSection();

        GUILayout.Space(15);

        // 应用按钮
        DrawModifyApplyButton();
    }

    /// <summary>
    /// 绘制修改时长区域（带激活开关）
    /// </summary>
    private void DrawDurationModifySection()
    {
        EditorGUILayout.BeginHorizontal();
        enableDurationModify = EditorGUILayout.ToggleLeft("修改时长", enableDurationModify, EditorStyles.boldLabel, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        if (enableDurationModify)
        {
            EditorGUI.indentLevel++;
            DrawBatchModifyDurationUI();
            EditorGUI.indentLevel--;
        }
    }

    /// <summary>
    /// 绘制重新排列区域
    /// </summary>
    private void DrawRearrangeSection()
    {
        EditorGUILayout.BeginHorizontal();
        enableRearrange = EditorGUILayout.ToggleLeft("重新排列", enableRearrange, EditorStyles.boldLabel, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();
        
        if (enableRearrange)
        {
            EditorGUI.indentLevel++;
            DrawBatchRearrangeUI();
            EditorGUI.indentLevel--;
        }
    }

    /// <summary>
    /// 绘制修改应用按钮
    /// </summary>
    private void DrawModifyApplyButton()
    {
        if (GUILayout.Button("应用修改", GUILayout.Height(35)))
        {
            ApplyModifications();
        }
    }

    /// <summary>
    /// 应用所有修改（时长+排列）
    /// </summary>
    private void ApplyModifications()
    {
        // 检查是否至少启用了一个功能
        if (!enableDurationModify && !enableRearrange)
        {
            EditorUtility.DisplayDialog("提示", "请至少启用一个修改选项（修改时长或重新排列）", "确定");
            return;
        }
        
        var selectedClips = GetSelectedTimelineClips();

        if (selectedClips.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先在Timeline中选中要修改的Clip", "确定");
            return;
        }

        var timelineAsset = TimelineEditor.inspectedAsset;
        if (timelineAsset == null) return;

        // 记录Undo
        var tracksToRecord = GetTracksToRecord(selectedClips);
        Undo.RecordObjects(tracksToRecord, "批量修改Clip");

        // 如果启用了修改时长，先修改时长
        if (enableDurationModify)
        {
            double newDuration = batchDurationUnit == TimeUnit.Frame
                ? batchDurationFrames / (double)frameRate
                : batchDurationSeconds;

            foreach (var clip in selectedClips)
            {
                clip.duration = newDuration;
            }
        }

        // 如果启用了重新排列
        if (enableRearrange)
        {
            var sortedClips = SortClipsByBindingName(selectedClips);
            double startTime = GetBatchStartTimeInSeconds(sortedClips);
            double offset = GetBatchOffsetInSeconds();

            switch (batchArrangeMode)
            {
                case ArrangeMode.StairDown:
                    RearrangeStairDown(sortedClips, startTime, offset);
                    break;
                case ArrangeMode.StairUp:
                    RearrangeStairUp(sortedClips, startTime, offset);
                    break;
                case ArrangeMode.SingleSequence:
                    RearrangeSingleSequence(sortedClips, startTime);
                    break;
                case ArrangeMode.SingleOverlap:
                    RearrangeSingleOverlap(sortedClips, startTime, offset);
                    break;
            }
        }

        EditorUtility.SetDirty(timelineAsset);
        ForceRefreshTimelineWindow();

        // 生成提示消息
        string message;
        if (enableDurationModify && enableRearrange)
            message = $"已修改 {selectedClips.Count} 个Clip的时长并重新排列";
        else if (enableDurationModify)
            message = $"已修改 {selectedClips.Count} 个Clip的时长";
        else
            message = $"已重新排列 {selectedClips.Count} 个Clip";
        Debug.Log($"[TLBatch] {message}");
    }

    /// <summary>
    /// 绘制Hierarchy选择信息
    /// </summary>
    private void DrawHierarchySelectionInfo()
    {
        EditorGUILayout.LabelField("导入源", EditorStyles.boldLabel);

        // 获取当前显示的对象列表
        var displayObjects = useLockedObjects ? lockedObjects : GetHierarchySelectedGameObjects();
        
        // 清理已删除的锁定对象
        if (useLockedObjects)
        {
            lockedObjects.RemoveAll(obj => obj == null);
            if (lockedObjects.Count == 0)
            {
                useLockedObjects = false;
                displayObjects = GetHierarchySelectedGameObjects();
            }
        }

        if (displayObjects.Count == 0)
        {
            EditorGUILayout.HelpBox("请在 Hierarchy 面板中选择要导入的 GameObject", MessageType.Info);
        }
        else
        {
            string sourceLabel = useLockedObjects ? $"已锁定 {displayObjects.Count} 个对象" : $"已选择 {displayObjects.Count} 个对象";
            EditorGUILayout.HelpBox(sourceLabel, useLockedObjects ? MessageType.None : MessageType.None);
            
            // 显示选中的对象列表（最多显示5个）
            EditorGUI.indentLevel++;
            int showCount = Mathf.Min(displayObjects.Count, 5);
            for (int i = 0; i < showCount; i++)
            {
                EditorGUILayout.LabelField($"• {displayObjects[i].name}");
            }
            if (displayObjects.Count > 5)
            {
                EditorGUILayout.LabelField($"... 还有 {displayObjects.Count - 5} 个");
            }
            EditorGUI.indentLevel--;
        }

        // 锁定/解锁按钮
        EditorGUILayout.BeginHorizontal();
        
        if (!useLockedObjects)
        {
            // 未锁定状态：显示锁定按钮
            var currentSelection = GetHierarchySelectedGameObjects();
            GUI.enabled = currentSelection.Count > 0;
            if (GUILayout.Button("锁定导入源", GUILayout.Height(22)))
            {
                lockedObjects = new List<GameObject>(currentSelection);
                useLockedObjects = true;
            }
            GUI.enabled = true;
        }
        else
        {
            // 已锁定状态：显示解锁和追加按钮
            if (GUILayout.Button("解锁", EditorStyles.miniButtonLeft, GUILayout.Width(50), GUILayout.Height(22)))
            {
                useLockedObjects = false;
                lockedObjects.Clear();
            }
            
            var currentSelection = GetHierarchySelectedGameObjects();
            GUI.enabled = currentSelection.Count > 0;
            if (GUILayout.Button("追加选中", EditorStyles.miniButtonMid, GUILayout.Height(22)))
            {
                foreach (var obj in currentSelection)
                {
                    if (!lockedObjects.Contains(obj))
                    {
                        lockedObjects.Add(obj);
                    }
                }
                // 重新排序
                SortLockedObjects();
            }
            if (GUILayout.Button("替换选中", EditorStyles.miniButtonRight, GUILayout.Height(22)))
            {
                lockedObjects = new List<GameObject>(currentSelection);
                SortLockedObjects();
            }
            GUI.enabled = true;
        }
        
        EditorGUILayout.EndHorizontal();

        // 排序方式选择
        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("排序方式", GUILayout.Width(60));

        if (GUILayout.Toggle(sortMode == SortMode.ByHierarchy, "结构", EditorStyles.miniButtonLeft, GUILayout.Width(45)))
            sortMode = SortMode.ByHierarchy;
        if (GUILayout.Toggle(sortMode == SortMode.ByName, "名称", EditorStyles.miniButtonMid, GUILayout.Width(45)))
            sortMode = SortMode.ByName;
        if (GUILayout.Toggle(sortMode == SortMode.Natural, "自然", EditorStyles.miniButtonRight, GUILayout.Width(45)))
            sortMode = SortMode.Natural;

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // 显示选中的轨道信息和插入位置选择
        GUILayout.Space(5);
        var selectedTrack = GetSelectedTrack();
        if (selectedTrack != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("插入位置", GUILayout.Width(60));

            if (GUILayout.Toggle(insertPosition == InsertPosition.InSelected, "轨道内", EditorStyles.miniButtonLeft))
            {
                insertPosition = InsertPosition.InSelected;
            }
            if (GUILayout.Toggle(insertPosition == InsertPosition.Above, "上新建", EditorStyles.miniButtonMid))
            {
                insertPosition = InsertPosition.Above;
            }
            if (GUILayout.Toggle(insertPosition == InsertPosition.Below, "下新建", EditorStyles.miniButtonRight))
            {
                insertPosition = InsertPosition.Below;
            }

            GUILayout.Space(10);
            EditorGUILayout.LabelField($"[{selectedTrack.name}]", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("提示: 选中Timeline轨道可指定插入位置", MessageType.None);
        }
    }

    /// <summary>
    /// 绘制排列模式选择器
    /// </summary>
    private void DrawArrangeModeSelector()
    {
        EditorGUILayout.LabelField("排列模式", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Toggle(arrangeMode == ArrangeMode.StairDown, "阶梯下行", EditorStyles.miniButtonLeft))
        {
            arrangeMode = ArrangeMode.StairDown;
        }
        if (GUILayout.Toggle(arrangeMode == ArrangeMode.StairUp, "阶梯上行", EditorStyles.miniButtonMid))
        {
            arrangeMode = ArrangeMode.StairUp;
        }
        if (GUILayout.Toggle(arrangeMode == ArrangeMode.SingleSequence, "单轨序列", EditorStyles.miniButtonMid))
        {
            arrangeMode = ArrangeMode.SingleSequence;
        }
        if (GUILayout.Toggle(arrangeMode == ArrangeMode.SingleOverlap, "单轨重叠", EditorStyles.miniButtonRight))
        {
            arrangeMode = ArrangeMode.SingleOverlap;
        }

        EditorGUILayout.EndHorizontal();

        // 模式说明
        string modeDesc = arrangeMode switch
        {
            ArrangeMode.StairDown => "每个Clip独立轨道，时间从上到下递增",
            ArrangeMode.StairUp => "每个Clip独立轨道，时间从下到上递增",
            ArrangeMode.SingleSequence => "所有Clip在同一轨道，首尾相接无间隔",
            ArrangeMode.SingleOverlap => "所有Clip在同一轨道，可配置间隔/重叠",
            _ => ""
        };
        EditorGUILayout.HelpBox(modeDesc, MessageType.None);
    }

    /// <summary>
    /// 绘制时间参数设置
    /// </summary>
    private void DrawTimeSettings()
    {
        EditorGUILayout.LabelField("时间参数", EditorStyles.boldLabel);

        // 起始时间模式
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("起始时间", GUILayout.Width(60));

        if (GUILayout.Toggle(startTimeMode == StartTimeMode.Custom, "自定义", EditorStyles.miniButtonLeft, GUILayout.Width(55)))
        {
            startTimeMode = StartTimeMode.Custom;
        }
        if (GUILayout.Toggle(startTimeMode == StartTimeMode.FromPlayhead, "播放头", EditorStyles.miniButtonMid, GUILayout.Width(55)))
        {
            startTimeMode = StartTimeMode.FromPlayhead;
        }
        if (GUILayout.Toggle(startTimeMode == StartTimeMode.FromZero, "从零", EditorStyles.miniButtonRight, GUILayout.Width(45)))
        {
            startTimeMode = StartTimeMode.FromZero;
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // 自定义起始时间
        if (startTimeMode == StartTimeMode.Custom)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.Width(60));

            // 单位按钮在前
            if (GUILayout.Toggle(startTimeUnit == TimeUnit.Frame, "按帧", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
                startTimeUnit = TimeUnit.Frame;
            if (GUILayout.Toggle(startTimeUnit == TimeUnit.Second, "按秒", EditorStyles.miniButtonRight, GUILayout.Width(40)))
                startTimeUnit = TimeUnit.Second;

            GUILayout.Space(5);

            if (startTimeUnit == TimeUnit.Frame)
            {
                customStartFrames = EditorGUILayout.IntField(customStartFrames, GUILayout.Width(60));
                EditorGUILayout.LabelField("f", GUILayout.Width(15));
            }
            else
            {
                customStartSeconds = EditorGUILayout.FloatField(customStartSeconds, GUILayout.Width(60));
                EditorGUILayout.LabelField("s", GUILayout.Width(15));
            }

            // 读取播放头按钮
            GUILayout.Space(5);
            if (GUILayout.Button("←读取", GUILayout.Width(50)))
            {
                SyncFromPlayhead();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(5);

        // Duration设置
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Duration", GUILayout.Width(60));

        // 单位按钮在前
        if (GUILayout.Toggle(durationUnit == TimeUnit.Frame, "按帧", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
            durationUnit = TimeUnit.Frame;
        if (GUILayout.Toggle(durationUnit == TimeUnit.Second, "按秒", EditorStyles.miniButtonRight, GUILayout.Width(40)))
            durationUnit = TimeUnit.Second;

        GUILayout.Space(5);

        if (durationUnit == TimeUnit.Frame)
        {
            durationFrames = EditorGUILayout.IntField(durationFrames, GUILayout.Width(60));
            EditorGUILayout.LabelField("f", GUILayout.Width(15));
        }
        else
        {
            durationSeconds = EditorGUILayout.FloatField(durationSeconds, GUILayout.Width(60));
            EditorGUILayout.LabelField("s", GUILayout.Width(15));
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // Duration快捷按钮
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("", GUILayout.Width(60));
        if (durationUnit == TimeUnit.Frame)
        {
            if (GUILayout.Button("30", GUILayout.Width(35))) durationFrames = 30;
            if (GUILayout.Button("60", GUILayout.Width(35))) durationFrames = 60;
            if (GUILayout.Button("90", GUILayout.Width(35))) durationFrames = 90;
            if (GUILayout.Button("120", GUILayout.Width(35))) durationFrames = 120;
            if (GUILayout.Button("150", GUILayout.Width(35))) durationFrames = 150;
        }
        else
        {
            if (GUILayout.Button("0.5", GUILayout.Width(35))) durationSeconds = 0.5f;
            if (GUILayout.Button("1", GUILayout.Width(35))) durationSeconds = 1f;
            if (GUILayout.Button("2", GUILayout.Width(35))) durationSeconds = 2f;
            if (GUILayout.Button("3", GUILayout.Width(35))) durationSeconds = 3f;
            if (GUILayout.Button("5", GUILayout.Width(35))) durationSeconds = 5f;
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // 阶梯模式或重叠模式需要Offset
        if (arrangeMode == ArrangeMode.StairDown || arrangeMode == ArrangeMode.StairUp || arrangeMode == ArrangeMode.SingleOverlap)
        {
            GUILayout.Space(5);

            string offsetLabel = arrangeMode == ArrangeMode.SingleOverlap ? "间隔" : "时间偏移";
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(offsetLabel, GUILayout.Width(60));

            // 单位按钮在前
            if (GUILayout.Toggle(offsetUnit == TimeUnit.Frame, "按帧", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
                offsetUnit = TimeUnit.Frame;
            if (GUILayout.Toggle(offsetUnit == TimeUnit.Second, "按秒", EditorStyles.miniButtonRight, GUILayout.Width(40)))
                offsetUnit = TimeUnit.Second;

            GUILayout.Space(5);

            if (offsetUnit == TimeUnit.Frame)
            {
                offsetFrames = EditorGUILayout.IntField(offsetFrames, GUILayout.Width(60));
                EditorGUILayout.LabelField("f", GUILayout.Width(15));
            }
            else
            {
                offsetSeconds = EditorGUILayout.FloatField(offsetSeconds, GUILayout.Width(60));
                EditorGUILayout.LabelField("s", GUILayout.Width(15));
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Offset快捷按钮
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.Width(60));
            if (offsetUnit == TimeUnit.Frame)
            {
                if (GUILayout.Button("0", GUILayout.Width(30))) offsetFrames = 0;
                if (GUILayout.Button("5", GUILayout.Width(30))) offsetFrames = 5;
                if (GUILayout.Button("10", GUILayout.Width(30))) offsetFrames = 10;
                if (GUILayout.Button("15", GUILayout.Width(30))) offsetFrames = 15;
                if (GUILayout.Button("30", GUILayout.Width(30))) offsetFrames = 30;
                if (GUILayout.Button("-15", GUILayout.Width(35))) offsetFrames = -15;
            }
            else
            {
                if (GUILayout.Button("0", GUILayout.Width(30))) offsetSeconds = 0f;
                if (GUILayout.Button("0.1", GUILayout.Width(35))) offsetSeconds = 0.1f;
                if (GUILayout.Button("0.25", GUILayout.Width(40))) offsetSeconds = 0.25f;
                if (GUILayout.Button("0.5", GUILayout.Width(35))) offsetSeconds = 0.5f;
                if (GUILayout.Button("-0.25", GUILayout.Width(45))) offsetSeconds = -0.25f;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (arrangeMode == ArrangeMode.SingleOverlap)
            {
                EditorGUILayout.HelpBox("正值=间隔，负值=重叠", MessageType.None);
            }
        }
    }

    /// <summary>
    /// 绘制操作按钮
    /// </summary>
    private void DrawActionButtons()
    {
        var objectsToImport = useLockedObjects ? lockedObjects : GetHierarchySelectedGameObjects();
        bool hasSelection = objectsToImport.Count > 0;
        bool hasTimeline = TimelineEditor.inspectedAsset != null;

        GUI.enabled = hasSelection && hasTimeline;

        if (GUILayout.Button("导入到Timeline", GUILayout.Height(35)))
        {
            ImportToTimeline();
        }

        GUI.enabled = true;

        if (!hasTimeline)
        {
            EditorGUILayout.HelpBox("请先在Timeline窗口中打开一个Timeline", MessageType.Warning);
        }
        else if (!hasSelection)
        {
            EditorGUILayout.HelpBox("请在Hierarchy面板中选择GameObject并锁定导入源", MessageType.Warning);
        }
    }

    /// <summary>
    /// 绘制批量修改时长UI
    /// </summary>
    private void DrawBatchModifyDurationUI()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Duration", GUILayout.Width(60));

        // 单位按钮在前
        if (GUILayout.Toggle(batchDurationUnit == TimeUnit.Frame, "按帧", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
            batchDurationUnit = TimeUnit.Frame;
        if (GUILayout.Toggle(batchDurationUnit == TimeUnit.Second, "按秒", EditorStyles.miniButtonRight, GUILayout.Width(40)))
            batchDurationUnit = TimeUnit.Second;

        GUILayout.Space(5);

        if (batchDurationUnit == TimeUnit.Frame)
        {
            batchDurationFrames = EditorGUILayout.IntField(batchDurationFrames, GUILayout.Width(60));
            EditorGUILayout.LabelField("f", GUILayout.Width(15));
        }
        else
        {
            batchDurationSeconds = EditorGUILayout.FloatField(batchDurationSeconds, GUILayout.Width(60));
            EditorGUILayout.LabelField("s", GUILayout.Width(15));
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // 快捷按钮
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("", GUILayout.Width(60));
        if (batchDurationUnit == TimeUnit.Frame)
        {
            if (GUILayout.Button("30", GUILayout.Width(35))) batchDurationFrames = 30;
            if (GUILayout.Button("60", GUILayout.Width(35))) batchDurationFrames = 60;
            if (GUILayout.Button("90", GUILayout.Width(35))) batchDurationFrames = 90;
            if (GUILayout.Button("120", GUILayout.Width(35))) batchDurationFrames = 120;
            if (GUILayout.Button("150", GUILayout.Width(35))) batchDurationFrames = 150;
        }
        else
        {
            if (GUILayout.Button("0.5", GUILayout.Width(35))) batchDurationSeconds = 0.5f;
            if (GUILayout.Button("1", GUILayout.Width(35))) batchDurationSeconds = 1f;
            if (GUILayout.Button("2", GUILayout.Width(35))) batchDurationSeconds = 2f;
            if (GUILayout.Button("3", GUILayout.Width(35))) batchDurationSeconds = 3f;
            if (GUILayout.Button("5", GUILayout.Width(35))) batchDurationSeconds = 5f;
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制批量重新排列UI
    /// </summary>
    private void DrawBatchRearrangeUI()
    {
        // 排列模式
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("排列模式", GUILayout.Width(60));

        if (GUILayout.Toggle(batchArrangeMode == ArrangeMode.StairDown, "阶梯下行", EditorStyles.miniButtonLeft))
            batchArrangeMode = ArrangeMode.StairDown;
        if (GUILayout.Toggle(batchArrangeMode == ArrangeMode.StairUp, "阶梯上行", EditorStyles.miniButtonMid))
            batchArrangeMode = ArrangeMode.StairUp;
        if (GUILayout.Toggle(batchArrangeMode == ArrangeMode.SingleSequence, "单轨序列", EditorStyles.miniButtonMid))
            batchArrangeMode = ArrangeMode.SingleSequence;
        if (GUILayout.Toggle(batchArrangeMode == ArrangeMode.SingleOverlap, "单轨重叠", EditorStyles.miniButtonRight))
            batchArrangeMode = ArrangeMode.SingleOverlap;

        EditorGUILayout.EndHorizontal();

        // 模式说明
        string modeDesc = batchArrangeMode switch
        {
            ArrangeMode.StairDown => "调整时间：从上到下递增",
            ArrangeMode.StairUp => "调整时间：从下到上递增",
            ArrangeMode.SingleSequence => "调整时间：首尾相接无间隔",
            ArrangeMode.SingleOverlap => "调整时间：可配置间隔/重叠",
            _ => ""
        };
        EditorGUILayout.HelpBox(modeDesc, MessageType.None);

        GUILayout.Space(3);

        // 排序方式选择（与导入模式一致）
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("排序方式", GUILayout.Width(60));

        if (GUILayout.Toggle(sortMode == SortMode.ByHierarchy, "结构", EditorStyles.miniButtonLeft, GUILayout.Width(45)))
            sortMode = SortMode.ByHierarchy;
        if (GUILayout.Toggle(sortMode == SortMode.ByName, "名称", EditorStyles.miniButtonMid, GUILayout.Width(45)))
            sortMode = SortMode.ByName;
        if (GUILayout.Toggle(sortMode == SortMode.Natural, "自然", EditorStyles.miniButtonRight, GUILayout.Width(45)))
            sortMode = SortMode.Natural;

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(3);

        // 起始时间模式
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("起始时间", GUILayout.Width(60));

        if (GUILayout.Toggle(batchStartTimeMode == BatchStartTimeMode.KeepFirst, "保持首位", EditorStyles.miniButtonLeft, GUILayout.Width(60)))
            batchStartTimeMode = BatchStartTimeMode.KeepFirst;
        if (GUILayout.Toggle(batchStartTimeMode == BatchStartTimeMode.Custom, "自定义", EditorStyles.miniButtonMid, GUILayout.Width(50)))
            batchStartTimeMode = BatchStartTimeMode.Custom;
        if (GUILayout.Toggle(batchStartTimeMode == BatchStartTimeMode.FromPlayhead, "播放头", EditorStyles.miniButtonMid, GUILayout.Width(50)))
            batchStartTimeMode = BatchStartTimeMode.FromPlayhead;
        if (GUILayout.Toggle(batchStartTimeMode == BatchStartTimeMode.FromZero, "从零", EditorStyles.miniButtonRight, GUILayout.Width(40)))
            batchStartTimeMode = BatchStartTimeMode.FromZero;

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // 自定义起始时间
        if (batchStartTimeMode == BatchStartTimeMode.Custom)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.Width(60));

            if (GUILayout.Toggle(batchStartTimeUnit == TimeUnit.Frame, "按帧", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
                batchStartTimeUnit = TimeUnit.Frame;
            if (GUILayout.Toggle(batchStartTimeUnit == TimeUnit.Second, "按秒", EditorStyles.miniButtonRight, GUILayout.Width(40)))
                batchStartTimeUnit = TimeUnit.Second;

            GUILayout.Space(5);

            if (batchStartTimeUnit == TimeUnit.Frame)
            {
                batchCustomStartFrames = EditorGUILayout.IntField(batchCustomStartFrames, GUILayout.Width(60));
                EditorGUILayout.LabelField("f", GUILayout.Width(15));
            }
            else
            {
                batchCustomStartSeconds = EditorGUILayout.FloatField(batchCustomStartSeconds, GUILayout.Width(60));
                EditorGUILayout.LabelField("s", GUILayout.Width(15));
            }

            GUILayout.Space(5);
            if (GUILayout.Button("←读取", GUILayout.Width(50)))
            {
                double playheadTime = GetPlayheadTime();
                if (batchStartTimeUnit == TimeUnit.Frame)
                {
                    batchCustomStartFrames = Mathf.RoundToInt((float)(playheadTime * frameRate));
                }
                else
                {
                    batchCustomStartSeconds = (float)playheadTime;
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // 阶梯模式或重叠模式需要Offset
        if (batchArrangeMode == ArrangeMode.StairDown || batchArrangeMode == ArrangeMode.StairUp || batchArrangeMode == ArrangeMode.SingleOverlap)
        {
            GUILayout.Space(3);

            string offsetLabel = batchArrangeMode == ArrangeMode.SingleOverlap ? "间隔" : "时间偏移";
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(offsetLabel, GUILayout.Width(60));

            if (GUILayout.Toggle(batchOffsetUnit == TimeUnit.Frame, "按帧", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
                batchOffsetUnit = TimeUnit.Frame;
            if (GUILayout.Toggle(batchOffsetUnit == TimeUnit.Second, "按秒", EditorStyles.miniButtonRight, GUILayout.Width(40)))
                batchOffsetUnit = TimeUnit.Second;

            GUILayout.Space(5);

            if (batchOffsetUnit == TimeUnit.Frame)
            {
                batchOffsetFrames = EditorGUILayout.IntField(batchOffsetFrames, GUILayout.Width(60));
                EditorGUILayout.LabelField("f", GUILayout.Width(15));
            }
            else
            {
                batchOffsetSeconds = EditorGUILayout.FloatField(batchOffsetSeconds, GUILayout.Width(60));
                EditorGUILayout.LabelField("s", GUILayout.Width(15));
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Offset快捷按钮
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.Width(60));
            if (batchOffsetUnit == TimeUnit.Frame)
            {
                if (GUILayout.Button("0", GUILayout.Width(30))) batchOffsetFrames = 0;
                if (GUILayout.Button("5", GUILayout.Width(30))) batchOffsetFrames = 5;
                if (GUILayout.Button("10", GUILayout.Width(30))) batchOffsetFrames = 10;
                if (GUILayout.Button("15", GUILayout.Width(30))) batchOffsetFrames = 15;
                if (GUILayout.Button("30", GUILayout.Width(30))) batchOffsetFrames = 30;
                if (GUILayout.Button("-15", GUILayout.Width(35))) batchOffsetFrames = -15;
            }
            else
            {
                if (GUILayout.Button("0", GUILayout.Width(30))) batchOffsetSeconds = 0f;
                if (GUILayout.Button("0.1", GUILayout.Width(35))) batchOffsetSeconds = 0.1f;
                if (GUILayout.Button("0.25", GUILayout.Width(40))) batchOffsetSeconds = 0.25f;
                if (GUILayout.Button("0.5", GUILayout.Width(35))) batchOffsetSeconds = 0.5f;
                if (GUILayout.Button("-0.25", GUILayout.Width(45))) batchOffsetSeconds = -0.25f;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (batchArrangeMode == ArrangeMode.SingleOverlap)
            {
                EditorGUILayout.HelpBox("正值=间隔，负值=重叠", MessageType.None);
            }
        }
    }

    #endregion

    #region 核心功能

    /// <summary>
    /// 更新帧率
    /// </summary>
    private void UpdateFrameRate()
    {
        var timelineAsset = TimelineEditor.inspectedAsset;
        if (timelineAsset != null)
        {
            // 尝试获取帧率，兼容不同Unity版本
            var settings = timelineAsset.editorSettings;
            if (settings != null)
            {
                // 尝试 frameRate 属性 (新版本)
                var frameRateProp = settings.GetType().GetProperty("frameRate");
                if (frameRateProp != null)
                {
                    var value = frameRateProp.GetValue(settings);
                    if (value != null)
                    {
                        frameRate = (float)System.Convert.ToDouble(value);
                    }
                }
                else
                {
                    // 尝试 fps 属性 (旧版本)
                    var fpsProp = settings.GetType().GetProperty("fps");
                    if (fpsProp != null)
                    {
                        var value = fpsProp.GetValue(settings);
                        if (value != null)
                        {
                            frameRate = (float)System.Convert.ToSingle(value);
                        }
                    }
                }
            }
            if (frameRate <= 0) frameRate = 60f;
        }
    }

    /// <summary>
    /// 获取Duration（秒）
    /// </summary>
    private double GetDurationInSeconds()
    {
        return durationUnit == TimeUnit.Frame
            ? durationFrames / (double)frameRate
            : durationSeconds;
    }

    /// <summary>
    /// 获取Offset（秒）
    /// </summary>
    private double GetOffsetInSeconds()
    {
        return offsetUnit == TimeUnit.Frame
            ? offsetFrames / (double)frameRate
            : offsetSeconds;
    }

    /// <summary>
    /// 获取起始时间（秒）
    /// </summary>
    private double GetStartTimeInSeconds()
    {
        switch (startTimeMode)
        {
            case StartTimeMode.FromPlayhead:
                return GetPlayheadTime();
            case StartTimeMode.FromZero:
                return 0;
            case StartTimeMode.Custom:
                return startTimeUnit == TimeUnit.Frame
                    ? customStartFrames / (double)frameRate
                    : customStartSeconds;
            default:
                return 0;
        }
    }

    /// <summary>
    /// 获取Hierarchy中选中的GameObject列表（已排序）
    /// </summary>
    private List<GameObject> GetHierarchySelectedGameObjects()
    {
        var result = new List<GameObject>();
        foreach (var obj in Selection.gameObjects)
        {
            // 排除Prefab资源，只要场景中的对象
            if (obj != null && !PrefabUtility.IsPartOfPrefabAsset(obj))
            {
                result.Add(obj);
            }
        }

        // 根据排序模式排序
        switch (sortMode)
        {
            case SortMode.ByName:
                // 名称排列（自然排序，支持数字）
                result.Sort((a, b) => NaturalCompare(a.name, b.name));
                break;
            case SortMode.Natural:
                // 自然排列，保持Selection原始顺序，不做处理
                break;
            case SortMode.ByHierarchy:
                // 结构排列，按Hierarchy层级顺序
                result.Sort((a, b) => CompareHierarchyOrder(a, b));
                break;
        }

        return result;
    }

    /// <summary>
    /// 自然字符串比较（支持数字排序：Item2 < Item10）
    /// </summary>
    private int NaturalCompare(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 0;
        if (string.IsNullOrEmpty(a)) return -1;
        if (string.IsNullOrEmpty(b)) return 1;

        int ia = 0, ib = 0;
        while (ia < a.Length && ib < b.Length)
        {
            char ca = a[ia];
            char cb = b[ib];

            // 两边都是数字，按数值比较
            if (char.IsDigit(ca) && char.IsDigit(cb))
            {
                int numA = 0, numB = 0;
                while (ia < a.Length && char.IsDigit(a[ia]))
                {
                    numA = numA * 10 + (a[ia] - '0');
                    ia++;
                }
                while (ib < b.Length && char.IsDigit(b[ib]))
                {
                    numB = numB * 10 + (b[ib] - '0');
                    ib++;
                }
                if (numA != numB) return numA.CompareTo(numB);
            }
            else
            {
                // 按字符比较（忽略大小写）
                int cmp = char.ToLowerInvariant(ca).CompareTo(char.ToLowerInvariant(cb));
                if (cmp != 0) return cmp;
                ia++;
                ib++;
            }
        }

        return a.Length.CompareTo(b.Length);
    }

    /// <summary>
    /// 比较Hierarchy层级顺序
    /// </summary>
    private int CompareHierarchyOrder(GameObject a, GameObject b)
    {
        // 获取完整路径的sibling index序列
        var pathA = GetHierarchyPath(a);
        var pathB = GetHierarchyPath(b);

        int minLen = Mathf.Min(pathA.Count, pathB.Count);
        for (int i = 0; i < minLen; i++)
        {
            if (pathA[i] != pathB[i])
                return pathA[i].CompareTo(pathB[i]);
        }
        return pathA.Count.CompareTo(pathB.Count);
    }

    /// <summary>
    /// 获取对象的层级路径（sibling index序列）
    /// </summary>
    private List<int> GetHierarchyPath(GameObject go)
    {
        var path = new List<int>();
        var current = go.transform;
        while (current != null)
        {
            path.Insert(0, current.GetSiblingIndex());
            current = current.parent;
        }
        return path;
    }

    /// <summary>
    /// 对锁定的对象列表进行排序
    /// </summary>
    private void SortLockedObjects()
    {
        switch (sortMode)
        {
            case SortMode.ByName:
                lockedObjects.Sort((a, b) => NaturalCompare(a.name, b.name));
                break;
            case SortMode.Natural:
                // 保持原顺序
                break;
            case SortMode.ByHierarchy:
                lockedObjects.Sort((a, b) => CompareHierarchyOrder(a, b));
                break;
        }
    }

    /// <summary>
    /// 获取当前选中的Timeline轨道
    /// </summary>
    private TrackAsset GetSelectedTrack()
    {
        foreach (var obj in Selection.objects)
        {
            if (obj is TrackAsset track)
            {
                return track;
            }
        }
        return null;
    }

    /// <summary>
    /// 获取选中轨道的父分组（如果有）
    /// </summary>
    private GroupTrack GetParentGroupTrack(TrackAsset track)
    {
        if (track == null) return null;
        return track.parent as GroupTrack;
    }

    /// <summary>
    /// 获取轨道在其容器（Timeline或GroupTrack）中的索引
    /// </summary>
    private int GetTrackIndexInContainer(TrackAsset track)
    {
        if (track == null) return -1;
        
        IEnumerable<TrackAsset> siblings;
        var parent = track.parent as GroupTrack;
        if (parent != null)
        {
            // 在分组内
            siblings = parent.GetChildTracks();
        }
        else
        {
            // 在根级别
            var timeline = track.timelineAsset;
            siblings = timeline.GetRootTracks();
        }
        
        int index = 0;
        foreach (var t in siblings)
        {
            if (t == track) return index;
            index++;
        }
        return -1;
    }

    /// <summary>
    /// 获取轨道在Timeline中的索引（兼容旧方法）
    /// </summary>
    private int GetTrackIndex(TimelineAsset timeline, TrackAsset track)
    {
        return GetTrackIndexInContainer(track);
    }

    /// <summary>
    /// 导入到Timeline
    /// </summary>
    private void ImportToTimeline()
    {
        var timelineAsset = TimelineEditor.inspectedAsset;
        var director = TimelineEditor.inspectedDirector;

        if (timelineAsset == null || director == null)
        {
            EditorUtility.DisplayDialog("错误", "请先打开Timeline窗口并选择一个Timeline", "确定");
            return;
        }

        var selectedObjects = useLockedObjects ? lockedObjects : GetHierarchySelectedGameObjects();
        if (selectedObjects.Count == 0)
        {
            EditorUtility.DisplayDialog("错误", "请先在Hierarchy中选择GameObject并锁定导入源", "确定");
            return;
        }

        Undo.RecordObject(timelineAsset, "批量导入Clip");

        double startTime = GetStartTimeInSeconds();
        double duration = GetDurationInSeconds();
        double offset = GetOffsetInSeconds();

        // 获取选中的轨道作为插入位置参考
        var selectedTrack = GetSelectedTrack();
        int insertIndex = -1;
        GroupTrack parentGroup = null;
        bool useSelectedTrack = false;  // 是否在选中轨道内创建Clip
        
        if (selectedTrack != null)
        {
            // 检测是否在分组内
            parentGroup = GetParentGroupTrack(selectedTrack);
            int trackIndex = GetTrackIndexInContainer(selectedTrack);
            
            if (insertPosition == InsertPosition.InSelected)
            {
                // 在选中轨道内创建Clip（仅对ControlTrack有效）
                useSelectedTrack = selectedTrack is ControlTrack;
                if (!useSelectedTrack)
                {
                    EditorUtility.DisplayDialog("提示", "选中轨道内模式仅支持Control Track类型轨道", "确定");
                    return;
                }
            }
            else
            {
                insertIndex = insertPosition == InsertPosition.Above ? trackIndex : trackIndex + 1;
            }
        }

        int createdCount = 0;

        // 如果使用选中轨道内模式
        if (useSelectedTrack && selectedTrack is ControlTrack controlTrack)
        {
            createdCount = ImportIntoSelectedTrack(controlTrack, director, selectedObjects, startTime, duration, offset);
        }
        else
        {
            switch (arrangeMode)
            {
                case ArrangeMode.StairDown:
                    createdCount = ImportStairDown(timelineAsset, director, selectedObjects, startTime, duration, offset, insertIndex, parentGroup);
                    break;
                case ArrangeMode.StairUp:
                    createdCount = ImportStairUp(timelineAsset, director, selectedObjects, startTime, duration, offset, insertIndex, parentGroup);
                    break;
                case ArrangeMode.SingleSequence:
                    createdCount = ImportSingleSequence(timelineAsset, director, selectedObjects, startTime, duration, insertIndex, parentGroup);
                    break;
                case ArrangeMode.SingleOverlap:
                    createdCount = ImportSingleOverlap(timelineAsset, director, selectedObjects, startTime, duration, offset, insertIndex, parentGroup);
                    break;
            }
        }

        EditorUtility.SetDirty(timelineAsset);
        ForceRefreshTimelineWindow();

        Debug.Log($"[TLBatch] 成功创建 {createdCount} 个Clip");
    }

    /// <summary>
    /// 强制刷新Timeline窗口（解决锁定状态下不刷新问题）
    /// </summary>
    private void ForceRefreshTimelineWindow()
    {
        // 标准刷新
        TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
        
        // 强制重绘Timeline窗口
        try
        {
            var timelineWindowType = System.Type.GetType("UnityEditor.Timeline.TimelineWindow, Unity.Timeline.Editor");
            if (timelineWindowType != null)
            {
                var instanceProp = timelineWindowType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp != null)
                {
                    var window = instanceProp.GetValue(null) as EditorWindow;
                    if (window != null)
                    {
                        window.Repaint();
                    }
                }
            }
        }
        catch { }
        
        // 额外触发一次SceneView刷新
        SceneView.RepaintAll();
    }

    /// <summary>
    /// 在选中轨道内导入Clip（不新建轨道）
    /// </summary>
    private int ImportIntoSelectedTrack(ControlTrack track, PlayableDirector director, List<GameObject> objects, double startTime, double duration, double offset)
    {
        int count = 0;
        double currentTime = startTime;

        foreach (var go in objects)
        {
            var clip = CreateControlClip(track, director, go, currentTime, duration);
            if (clip != null)
            {
                count++;
                currentTime += offset;
            }
        }

        return count;
    }

    /// <summary>
    /// 阶梯下行导入
    /// </summary>
    private int ImportStairDown(TimelineAsset timeline, PlayableDirector director, List<GameObject> objects, double startTime, double duration, double offset, int insertIndex, GroupTrack parentGroup = null)
    {
        int count = 0;
        double currentTime = startTime;
        var createdTracks = new List<TrackAsset>();

        foreach (var go in objects)
        {
            var track = CreateControlTrack(timeline, go.name, parentGroup);
            if (track != null)
            {
                createdTracks.Add(track);
                var clip = CreateControlClip(track, director, go, currentTime, duration);
                if (clip != null)
                {
                    count++;
                    currentTime += offset;
                }
            }
        }

        // 移动轨道到指定位置
        if (insertIndex >= 0 && createdTracks.Count > 0)
        {
            MoveTracksToIndex(timeline, createdTracks, insertIndex, parentGroup);
        }

        return count;
    }

    /// <summary>
    /// 阶梯上行导入
    /// </summary>
    private int ImportStairUp(TimelineAsset timeline, PlayableDirector director, List<GameObject> objects, double startTime, double duration, double offset, int insertIndex, GroupTrack parentGroup = null)
    {
        int count = 0;
        double currentTime = startTime + offset * (objects.Count - 1);
        var createdTracks = new List<TrackAsset>();

        // 反向处理，使时间从下到上递增
        var reversedObjects = objects.ToList();
        reversedObjects.Reverse();

        foreach (var go in reversedObjects)
        {
            var track = CreateControlTrack(timeline, go.name, parentGroup);
            if (track != null)
            {
                createdTracks.Add(track);
                var clip = CreateControlClip(track, director, go, currentTime, duration);
                if (clip != null)
                {
                    count++;
                    currentTime -= offset;
                }
            }
        }

        // 移动轨道到指定位置
        if (insertIndex >= 0 && createdTracks.Count > 0)
        {
            MoveTracksToIndex(timeline, createdTracks, insertIndex, parentGroup);
        }

        return count;
    }

    /// <summary>
    /// 单轨序列导入
    /// </summary>
    private int ImportSingleSequence(TimelineAsset timeline, PlayableDirector director, List<GameObject> objects, double startTime, double duration, int insertIndex, GroupTrack parentGroup = null)
    {
        var track = CreateControlTrack(timeline, "Batch Track", parentGroup);
        if (track == null) return 0;

        int count = 0;
        double currentTime = startTime;

        foreach (var go in objects)
        {
            var clip = CreateControlClip(track, director, go, currentTime, duration);
            if (clip != null)
            {
                count++;
                currentTime += duration; // 首尾相接
            }
        }

        // 移动轨道到指定位置
        if (insertIndex >= 0)
        {
            MoveTracksToIndex(timeline, new List<TrackAsset> { track }, insertIndex, parentGroup);
        }

        return count;
    }

    /// <summary>
    /// 单轨重叠导入
    /// </summary>
    private int ImportSingleOverlap(TimelineAsset timeline, PlayableDirector director, List<GameObject> objects, double startTime, double duration, double gap, int insertIndex, GroupTrack parentGroup = null)
    {
        var track = CreateControlTrack(timeline, "Batch Track", parentGroup);
        if (track == null) return 0;

        int count = 0;
        double currentTime = startTime;

        foreach (var go in objects)
        {
            var clip = CreateControlClip(track, director, go, currentTime, duration);
            if (clip != null)
            {
                count++;
                currentTime += duration + gap; // 加上间隔（负值则重叠）
            }
        }

        // 移动轨道到指定位置
        if (insertIndex >= 0)
        {
            MoveTracksToIndex(timeline, new List<TrackAsset> { track }, insertIndex, parentGroup);
        }

        return count;
    }

    /// <summary>
    /// 创建Control轨道（支持分组）
    /// </summary>
    private ControlTrack CreateControlTrack(TimelineAsset timeline, string name, GroupTrack parentGroup = null)
    {
        return timeline.CreateTrack<ControlTrack>(parentGroup, name);
    }

    /// <summary>
    /// 创建Control Clip
    /// </summary>
    private TimelineClip CreateControlClip(ControlTrack track, PlayableDirector director, GameObject go, double startTime, double duration)
    {
        var clip = track.CreateDefaultClip();
        clip.start = startTime;
        clip.duration = duration;
        clip.displayName = go.name;

        // 设置ControlPlayableAsset
        var controlAsset = clip.asset as ControlPlayableAsset;
        if (controlAsset != null)
        {
            // 场景对象，使用ExposedReference
            var exposedRef = new ExposedReference<GameObject>();
            var guid = GUID.Generate().ToString();
            exposedRef.exposedName = new PropertyName(guid);
            director.SetReferenceValue(guid, go);
            controlAsset.sourceGameObject = exposedRef;
        }

        return clip;
    }

    /// <summary>
    /// 移动轨道到指定索引位置（支持分组）
    /// </summary>
    private void MoveTracksToIndex(TimelineAsset timeline, List<TrackAsset> tracks, int targetIndex, GroupTrack parentGroup = null)
    {
        try
        {
            // 使用反射调用内部方法移动轨道
            var timelineAssetType = typeof(TimelineAsset);
            var moveTrackMethod = timelineAssetType.GetMethod("MoveTrack", 
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (moveTrackMethod != null)
            {
                int currentIndex = targetIndex;
                foreach (var track in tracks)
                {
                    moveTrackMethod.Invoke(timeline, new object[] { track, currentIndex });
                    currentIndex++;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[TLBatch] 移动轨道失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取批量操作的起始时间（秒）
    /// </summary>
    private double GetBatchStartTimeInSeconds(List<TimelineClip> clips)
    {
        switch (batchStartTimeMode)
        {
            case BatchStartTimeMode.KeepFirst:
                // 保持第一个Clip的起始时间
                if (clips.Count > 0)
                {
                    return clips[0].start;
                }
                return 0;
            case BatchStartTimeMode.FromPlayhead:
                return GetPlayheadTime();
            case BatchStartTimeMode.FromZero:
                return 0;
            case BatchStartTimeMode.Custom:
                return batchStartTimeUnit == TimeUnit.Frame
                    ? batchCustomStartFrames / (double)frameRate
                    : batchCustomStartSeconds;
            default:
                return 0;
        }
    }

    /// <summary>
    /// 获取批量操作的偏移时间（秒）
    /// </summary>
    private double GetBatchOffsetInSeconds()
    {
        return batchOffsetUnit == TimeUnit.Frame
            ? batchOffsetFrames / (double)frameRate
            : batchOffsetSeconds;
    }

    /// <summary>
    /// 按绑定对象名称排序Clip
    /// </summary>
    private List<TimelineClip> SortClipsByBindingName(List<TimelineClip> clips)
    {
        var director = TimelineEditor.inspectedDirector;
        
        // 创建Clip与名称的映射
        var clipInfos = clips.Select(clip => new
        {
            Clip = clip,
            Name = GetClipBindingName(clip, director),
            OriginalStart = clip.start
        }).ToList();

        // 根据排序模式排序
        switch (sortMode)
        {
            case SortMode.ByName:
                clipInfos.Sort((a, b) => NaturalCompare(a.Name, b.Name));
                break;
            case SortMode.Natural:
                // 按原始时间顺序
                clipInfos.Sort((a, b) => a.OriginalStart.CompareTo(b.OriginalStart));
                break;
            case SortMode.ByHierarchy:
                // 对于Clip，按原始时间顺序作为默认
                clipInfos.Sort((a, b) => a.OriginalStart.CompareTo(b.OriginalStart));
                break;
        }

        return clipInfos.Select(info => info.Clip).ToList();
    }

    /// <summary>
    /// 获取Clip绑定的对象名称
    /// </summary>
    private string GetClipBindingName(TimelineClip clip, PlayableDirector director)
    {
        if (clip == null) return "";
        
        // 尝试从ControlPlayableAsset获取绑定对象名称
        var controlAsset = clip.asset as ControlPlayableAsset;
        if (controlAsset != null && director != null)
        {
            try
            {
                var sourceGO = controlAsset.sourceGameObject.Resolve(director);
                if (sourceGO != null)
                {
                    return sourceGO.name;
                }
            }
            catch { }
        }
        
        // 回退到displayName
        return clip.displayName;
    }

    /// <summary>
    /// 阶梯下行重排 - 调整时间，每个Clip递增偏移
    /// </summary>
    private void RearrangeStairDown(List<TimelineClip> clips, double startTime, double offset)
    {
        double currentTime = startTime;
        foreach (var clip in clips)
        {
            clip.start = currentTime;
            currentTime += offset;
        }
    }

    /// <summary>
    /// 阶梯上行重排 - 调整时间，从后往前递增
    /// </summary>
    private void RearrangeStairUp(List<TimelineClip> clips, double startTime, double offset)
    {
        double currentTime = startTime + offset * (clips.Count - 1);
        foreach (var clip in clips)
        {
            clip.start = currentTime;
            currentTime -= offset;
        }
    }

    /// <summary>
    /// 单轨序列重排 - 首尾相接
    /// </summary>
    private void RearrangeSingleSequence(List<TimelineClip> clips, double startTime)
    {
        double currentTime = startTime;
        foreach (var clip in clips)
        {
            clip.start = currentTime;
            currentTime += clip.duration;
        }
    }

    /// <summary>
    /// 单轨重叠重排 - 可配置间隔
    /// </summary>
    private void RearrangeSingleOverlap(List<TimelineClip> clips, double startTime, double gap)
    {
        double currentTime = startTime;
        foreach (var clip in clips)
        {
            clip.start = currentTime;
            currentTime += clip.duration + gap;
        }
    }

    /// <summary>
    /// 获取播放头时间
    /// </summary>
    private double GetPlayheadTime()
    {
        var director = TimelineEditor.inspectedDirector;
        if (director != null)
        {
            return director.time;
        }
        return 0;
    }

    /// <summary>
    /// 从播放头同步起始时间
    /// </summary>
    private void SyncFromPlayhead()
    {
        double playheadTime = GetPlayheadTime();
        if (startTimeUnit == TimeUnit.Frame)
        {
            customStartFrames = Mathf.RoundToInt((float)(playheadTime * frameRate));
        }
        else
        {
            customStartSeconds = (float)playheadTime;
        }
    }

    /// <summary>
    /// 获取选中的Timeline Clips
    /// </summary>
    private List<TimelineClip> GetSelectedTimelineClips()
    {
        var clips = new List<TimelineClip>();

        try
        {
            var timelineEditorType = typeof(TimelineEditor);
            var selectedClipsProperty = timelineEditorType.GetProperty("selectedClips",
                BindingFlags.Public | BindingFlags.Static);

            if (selectedClipsProperty != null)
            {
                var selectedClips = selectedClipsProperty.GetValue(null) as IEnumerable<TimelineClip>;
                if (selectedClips != null)
                {
                    clips.AddRange(selectedClips);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"获取选中Clip失败: {ex.Message}");
        }

        return clips;
    }

    /// <summary>
    /// 获取需要记录 Undo 的轨道对象（兼容 Timeline 1.4.x）
    /// </summary>
    private Object[] GetTracksToRecord(List<TimelineClip> clips)
    {
        return clips
            .Select(GetParentTrack)
            .Where(track => track != null)
            .Distinct()
            .Cast<Object>()
            .ToArray();
    }

    /// <summary>
    /// 获取 Clip 所属轨道（兼容 Timeline 1.4.x）
    /// </summary>
    private TrackAsset GetParentTrack(TimelineClip clip)
    {
        if (clip == null) return null;

        var timelineAsset = TimelineEditor.inspectedAsset;
        if (timelineAsset == null) return null;

        foreach (var track in timelineAsset.GetRootTracks())
        {
            var parentTrack = FindParentTrackRecursive(track, clip);
            if (parentTrack != null) return parentTrack;
        }

        return null;
    }

    /// <summary>
    /// 递归查找 Clip 所属轨道
    /// </summary>
    private TrackAsset FindParentTrackRecursive(TrackAsset track, TimelineClip clip)
    {
        if (track == null) return null;

        foreach (var trackClip in track.GetClips())
        {
            if (ReferenceEquals(trackClip, clip)) return track;
        }

        foreach (var childTrack in track.GetChildTracks())
        {
            var parentTrack = FindParentTrackRecursive(childTrack, clip);
            if (parentTrack != null) return parentTrack;
        }

        return null;
    }

    #endregion
}
