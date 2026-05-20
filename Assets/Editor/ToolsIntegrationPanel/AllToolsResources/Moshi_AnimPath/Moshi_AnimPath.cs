using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
#if UNITY_2017_1_OR_NEWER
using UnityEngine.Timeline;
using UnityEngine.Playables;
#endif

/// <summary>
/// 动画路径修复工具 - 批量修改AnimationClip中的路径
/// </summary>
public class Moshi_AnimPath : EditorWindow
{
    private const string TOOL_NAME = "动画路径修复";

    #region 枚举定义
    private enum TabType { ScanFix, OperationLog, Settings }
    private enum ScanMode { SelectedOnly, SingleFolder, MultiFolders }
    private enum AssetType { AnimClip, Timeline, Both }
    private enum PathEditMode { Replace, AddPrefix, AddSuffix, InsertAt, RemoveSegment }
    #endregion

    #region 字段
    // 标签页
    private TabType currentTab = TabType.ScanFix;

    // 扫描设置
    private ScanMode scanMode = ScanMode.SelectedOnly;
    private AssetType assetType = AssetType.Both;
    private DefaultAsset singleFolder;
    private List<DefaultAsset> multipleFolders = new List<DefaultAsset>();
    private bool includeSubfolders = true;

    // Timeline相关
    private Dictionary<AnimationClip, Object> clipToParentAsset = new Dictionary<AnimationClip, Object>();

    // 路径编辑
    private PathEditMode pathEditMode = PathEditMode.Replace;
    private string searchPattern = "";
    private string replaceWith = "";
    private string prefixToAdd = "";
    private string suffixToAdd = "";
    private string insertText = "";
    private int insertAtIndex = 0;
    private int segmentIndexToRemove = 0;

    // 扫描结果
    private List<AnimationClip> scannedClips = new List<AnimationClip>();
    private Dictionary<AnimationClip, List<string>> clipPaths = new Dictionary<AnimationClip, List<string>>();
    private Dictionary<AnimationClip, bool> clipFoldouts = new Dictionary<AnimationClip, bool>();
    private HashSet<string> selectedPaths = new HashSet<string>();

    // 日志
    private List<string> operationLogs = new List<string>();
    private Vector2 logScrollPosition;

    // 设置
    private bool autoBackup = true;
    private string backupFolder = "Assets/AnimationBackups";

    // UI
    private Vector2 mainScrollPosition;
    private Vector2 clipListScrollPosition;
    private string filterKeyword = "";        // 路径过滤
    private string clipNameFilter = "";       // 动画片段名称过滤
    private string lastClickedPathKey = "";   // 用于Shift多选
    private List<string> flatPathKeyList = new List<string>(); // 扁平化的路径列表，用于Shift多选
    private GUIStyle pathLabelStyle;          // 缓存的路径文本样式
    #endregion

    [MenuItem("工具/Moshi/" + TOOL_NAME)]
    public static void ShowWindow()
    {
        var window = GetWindow<Moshi_AnimPath>(TOOL_NAME);
        window.minSize = new Vector2(500, 600);
    }

    private void OnGUI()
    {
        DrawTabBar();

        mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition);

        switch (currentTab)
        {
            case TabType.ScanFix:
                DrawScanFixTab();
                break;
            case TabType.OperationLog:
                DrawOperationLogTab();
                break;
            case TabType.Settings:
                DrawSettingsTab();
                break;
        }

        EditorGUILayout.EndScrollView();
    }

    #region 标签栏
    private void DrawTabBar()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Toggle(currentTab == TabType.ScanFix, "扫描修复", EditorStyles.miniButtonLeft))
            currentTab = TabType.ScanFix;
        if (GUILayout.Toggle(currentTab == TabType.OperationLog, "操作日志", EditorStyles.miniButtonMid))
            currentTab = TabType.OperationLog;
        if (GUILayout.Toggle(currentTab == TabType.Settings, "设置", EditorStyles.miniButtonRight))
            currentTab = TabType.Settings;

        GUILayout.FlexibleSpace();
        MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
    }
    #endregion

    #region 扫描修复标签页
    private void DrawScanFixTab()
    {
        // 扫描范围
        EditorGUILayout.LabelField("扫描范围", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("扫描模式", GUILayout.Width(60));

        if (GUILayout.Toggle(scanMode == ScanMode.SelectedOnly, "选中项", EditorStyles.miniButtonLeft))
            scanMode = ScanMode.SelectedOnly;
        if (GUILayout.Toggle(scanMode == ScanMode.SingleFolder, "单文件夹", EditorStyles.miniButtonMid))
            scanMode = ScanMode.SingleFolder;
        if (GUILayout.Toggle(scanMode == ScanMode.MultiFolders, "多文件夹", EditorStyles.miniButtonRight))
            scanMode = ScanMode.MultiFolders;

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);

        // 资产类型
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("资产类型", GUILayout.Width(60));

        if (GUILayout.Toggle(assetType == AssetType.Both, "全部", EditorStyles.miniButtonLeft))
            assetType = AssetType.Both;
        if (GUILayout.Toggle(assetType == AssetType.AnimClip, "动画片段", EditorStyles.miniButtonMid))
            assetType = AssetType.AnimClip;
        if (GUILayout.Toggle(assetType == AssetType.Timeline, "Timeline", EditorStyles.miniButtonRight))
            assetType = AssetType.Timeline;

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);

        switch (scanMode)
        {
            case ScanMode.SelectedOnly:
                EditorGUILayout.HelpBox("将扫描当前选中的AnimationClip或Timeline文件", MessageType.Info);
                break;
            case ScanMode.SingleFolder:
                singleFolder = (DefaultAsset)EditorGUILayout.ObjectField("目标文件夹", singleFolder, typeof(DefaultAsset), false);
                includeSubfolders = EditorGUILayout.Toggle("包含子文件夹", includeSubfolders);
                break;
            case ScanMode.MultiFolders:
                DrawMultipleFoldersUI();
                includeSubfolders = EditorGUILayout.Toggle("包含子文件夹", includeSubfolders);
                break;
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("开始扫描", GUILayout.Height(28)))
        {
            ScanAnimationClips();
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // 扫描结果
        if (scannedClips.Count > 0)
        {
            DrawScanResults();
            EditorGUILayout.Space(10);
            DrawPathEditSection();
        }
    }

    private void DrawMultipleFoldersUI()
    {
        EditorGUILayout.LabelField("目标文件夹列表:");

        for (int i = 0; i < multipleFolders.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            multipleFolders[i] = (DefaultAsset)EditorGUILayout.ObjectField(multipleFolders[i], typeof(DefaultAsset), false);
            if (GUILayout.Button("删除", GUILayout.Width(50)))
            {
                multipleFolders.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("添加文件夹"))
        {
            multipleFolders.Add(null);
        }
    }

    private void DrawScanResults()
    {
        EditorGUILayout.LabelField($"扫描结果 ({scannedClips.Count} 个动画片段)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // === 过滤区域 ===
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("过滤搜索", EditorStyles.miniBoldLabel);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("文件名", GUILayout.Width(45));
        clipNameFilter = EditorGUILayout.TextField(clipNameFilter);
        if (GUILayout.Button("×", GUILayout.Width(20)))
            clipNameFilter = "";
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("路径", GUILayout.Width(45));
        filterKeyword = EditorGUILayout.TextField(filterKeyword);
        if (GUILayout.Button("×", GUILayout.Width(20)))
            filterKeyword = "";
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(3);

        // === 统计过滤后的结果 ===
        int filteredClipCount = 0;
        int filteredPathCount = 0;
        var filteredClips = new List<AnimationClip>();
        var filteredClipPaths = new Dictionary<AnimationClip, List<string>>();

        foreach (var clip in scannedClips)
        {
            if (!clipPaths.ContainsKey(clip) || clipPaths[clip].Count == 0)
                continue;

            // 文件名过滤
            string clipDisplayName = clip.name;
            bool isFromTimeline = clipToParentAsset.ContainsKey(clip) && clipToParentAsset[clip] != null;
            if (isFromTimeline)
                clipDisplayName = $"[TL] {clip.name}";

            if (!string.IsNullOrEmpty(clipNameFilter) && 
                !clipDisplayName.ToLower().Contains(clipNameFilter.ToLower()))
                continue;

            // 路径过滤
            var paths = clipPaths[clip];
            var matchedPaths = string.IsNullOrEmpty(filterKeyword)
                ? paths
                : paths.Where(p => p.ToLower().Contains(filterKeyword.ToLower())).ToList();

            if (matchedPaths.Count == 0)
                continue;

            filteredClips.Add(clip);
            filteredClipPaths[clip] = matchedPaths;
            filteredClipCount++;
            filteredPathCount += matchedPaths.Count;
        }

        // 显示过滤统计
        bool hasFilter = !string.IsNullOrEmpty(clipNameFilter) || !string.IsNullOrEmpty(filterKeyword);
        if (hasFilter)
        {
            EditorGUILayout.HelpBox($"过滤结果: {filteredClipCount} 个动画片段, {filteredPathCount} 条路径", MessageType.Info);
        }

        EditorGUILayout.Space(3);

        // 选择按钮组
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全选", EditorStyles.miniButtonLeft, GUILayout.Width(50)))
        {
            SelectAllPaths(filteredClipPaths);
        }
        if (GUILayout.Button("反选", EditorStyles.miniButtonMid, GUILayout.Width(50)))
        {
            InvertSelection(filteredClipPaths);
        }
        if (GUILayout.Button("清空", EditorStyles.miniButtonRight, GUILayout.Width(50)))
        {
            selectedPaths.Clear();
        }
        GUILayout.Space(10);
        if (GUILayout.Button("展开全部", EditorStyles.miniButtonLeft, GUILayout.Width(60)))
        {
            foreach (var clip in filteredClips)
                clipFoldouts[clip] = true;
        }
        if (GUILayout.Button("折叠全部", EditorStyles.miniButtonRight, GUILayout.Width(60)))
        {
            foreach (var clip in filteredClips)
                clipFoldouts[clip] = false;
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // 提示信息
        EditorGUILayout.HelpBox("点击行选择，Shift+点击多选范围，Ctrl+点击切换单个", MessageType.None);

        // 构建扁平化路径列表（用于Shift多选）
        flatPathKeyList.Clear();
        foreach (var clip in filteredClips)
        {
            foreach (var path in filteredClipPaths[clip])
            {
                flatPathKeyList.Add(GetPathKey(clip, path));
            }
        }

        // 路径列表
        clipListScrollPosition = EditorGUILayout.BeginScrollView(clipListScrollPosition, GUILayout.MaxHeight(350));

        foreach (var clip in filteredClips)
        {
            var matchedPaths = filteredClipPaths[clip];

            if (!clipFoldouts.ContainsKey(clip))
                clipFoldouts[clip] = false;

            // 显示来源信息
            string clipDisplayName = clip.name;
            bool isFromTimeline = clipToParentAsset.ContainsKey(clip) && clipToParentAsset[clip] != null;
            if (isFromTimeline)
            {
                clipDisplayName = $"[TL] {clip.name}";
            }

            // 计算该Clip下已选中的路径数
            int selectedInClip = 0;
            foreach (var p in matchedPaths)
            {
                if (selectedPaths.Contains(GetPathKey(clip, p)))
                    selectedInClip++;
            }

            EditorGUILayout.BeginHorizontal();
            
            // Clip级别的复选框
            bool allSelected = selectedInClip == matchedPaths.Count;
            bool someSelected = selectedInClip > 0 && selectedInClip < matchedPaths.Count;
            
            EditorGUI.showMixedValue = someSelected;
            EditorGUI.BeginChangeCheck();
            bool newAllSelected = EditorGUILayout.Toggle(allSelected, GUILayout.Width(18));
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var p in matchedPaths)
                {
                    string key = GetPathKey(clip, p);
                    if (newAllSelected)
                        selectedPaths.Add(key);
                    else
                        selectedPaths.Remove(key);
                }
            }
            
            clipFoldouts[clip] = EditorGUILayout.Foldout(clipFoldouts[clip], $"{clipDisplayName} ({selectedInClip}/{matchedPaths.Count})", true);

            if (GUILayout.Button("定位", GUILayout.Width(40)))
            {
                if (isFromTimeline)
                {
                    EditorGUIUtility.PingObject(clipToParentAsset[clip]);
                    Selection.activeObject = clipToParentAsset[clip];
                }
                else
                {
                    EditorGUIUtility.PingObject(clip);
                    Selection.activeObject = clip;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (clipFoldouts[clip])
            {
                foreach (var path in matchedPaths)
                {
                    string pathKey = GetPathKey(clip, path);
                    bool isSelected = selectedPaths.Contains(pathKey);

                    // 整行
                    Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
                    
                    // 绘制背景高亮
                    if (isSelected)
                    {
                        EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.9f, 0.3f));
                    }
                    
                    // 悬停高亮
                    bool isHovering = rowRect.Contains(Event.current.mousePosition);
                    if (isHovering)
                    {
                        EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.08f));
                        if (Event.current.type == EventType.Repaint)
                            Repaint(); // 确保悬停效果实时更新
                    }

                    GUILayout.Space(30); // 缩进

                    // 复选框
                    EditorGUI.BeginChangeCheck();
                    bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(18));
                    if (EditorGUI.EndChangeCheck())
                    {
                        HandlePathSelection(pathKey, newSelected);
                    }

                    // 初始化样式（只在需要时创建一次）
                    if (pathLabelStyle == null)
                    {
                        pathLabelStyle = new GUIStyle(EditorStyles.label);
                        pathLabelStyle.richText = true;
                    }
                    // 每次绘制时强制设置文字颜色为浅灰色（比标题暗一些）
                    pathLabelStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
                    
                    // 高亮显示搜索关键字
                    string displayPath = path;
                    if (!string.IsNullOrEmpty(filterKeyword))
                    {
                        int idx = path.ToLower().IndexOf(filterKeyword.ToLower());
                        if (idx >= 0)
                        {
                            string before = path.Substring(0, idx);
                            string match = path.Substring(idx, filterKeyword.Length);
                            string after = path.Substring(idx + filterKeyword.Length);
                            displayPath = $"{before}<color=yellow>{match}</color>{after}";
                        }
                    }
                    
                    // 路径文本显示
                    EditorGUILayout.LabelField(displayPath, pathLabelStyle);
                    
                    GUILayout.FlexibleSpace();

                    // 复制按钮
                    if (GUILayout.Button("复制", EditorStyles.miniButton, GUILayout.Width(36)))
                    {
                        EditorGUIUtility.systemCopyBuffer = path;
                    }

                    EditorGUILayout.EndHorizontal();
                    
                    // 处理整行点击（在EndHorizontal之后处理）
                    if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                    {
                        // 计算复选框和按钮区域，避免冲突
                        float checkboxEndX = rowRect.x + 30 + 18 + 4; // 缩进 + toggle宽度 + 间距
                        float buttonStartX = rowRect.xMax - 40;
                        
                        if (Event.current.mousePosition.x > checkboxEndX && 
                            Event.current.mousePosition.x < buttonStartX)
                        {
                            HandleRowClick(pathKey);
                            Event.current.Use();
                        }
                    }
                }
            }
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.LabelField($"已选择 {selectedPaths.Count} 条路径", EditorStyles.boldLabel);
        EditorGUILayout.EndVertical();
    }

    private void HandleRowClick(string pathKey)
    {
        Event e = Event.current;
        if (e == null) return;
        
        if (e.shift && !string.IsNullOrEmpty(lastClickedPathKey))
        {
            // Shift多选：选择范围内所有路径
            int startIdx = flatPathKeyList.IndexOf(lastClickedPathKey);
            int endIdx = flatPathKeyList.IndexOf(pathKey);
            
            if (startIdx >= 0 && endIdx >= 0)
            {
                int minIdx = Mathf.Min(startIdx, endIdx);
                int maxIdx = Mathf.Max(startIdx, endIdx);
                
                for (int i = minIdx; i <= maxIdx; i++)
                {
                    selectedPaths.Add(flatPathKeyList[i]);
                }
            }
        }
        else if (e.control || e.command)
        {
            // Ctrl切换单个
            if (selectedPaths.Contains(pathKey))
                selectedPaths.Remove(pathKey);
            else
                selectedPaths.Add(pathKey);
        }
        else
        {
            // 普通点击：切换选中状态
            if (selectedPaths.Contains(pathKey))
                selectedPaths.Remove(pathKey);
            else
                selectedPaths.Add(pathKey);
        }
        
        lastClickedPathKey = pathKey;
        GUI.changed = true;
        Repaint();
    }

    private void HandlePathSelection(string pathKey, bool selected)
    {
        if (selected)
            selectedPaths.Add(pathKey);
        else
            selectedPaths.Remove(pathKey);
        
        lastClickedPathKey = pathKey;
    }

    private void SelectAllPaths(Dictionary<AnimationClip, List<string>> filteredData = null)
    {
        var dataToUse = filteredData ?? clipPaths;
        foreach (var kvp in dataToUse)
        {
            foreach (var path in kvp.Value)
            {
                selectedPaths.Add(GetPathKey(kvp.Key, path));
            }
        }
    }

    private void InvertSelection(Dictionary<AnimationClip, List<string>> filteredData = null)
    {
        var dataToUse = filteredData ?? clipPaths;
        var allPaths = new HashSet<string>();
        foreach (var kvp in dataToUse)
        {
            foreach (var path in kvp.Value)
            {
                allPaths.Add(GetPathKey(kvp.Key, path));
            }
        }

        var newSelection = new HashSet<string>();
        foreach (var pathKey in allPaths)
        {
            if (!selectedPaths.Contains(pathKey))
                newSelection.Add(pathKey);
        }
        selectedPaths = newSelection;
    }

    private void DrawPathEditSection()
    {
        EditorGUILayout.LabelField("路径编辑", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // 编辑模式
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("编辑模式", GUILayout.Width(60));

        if (GUILayout.Toggle(pathEditMode == PathEditMode.Replace, "替换", EditorStyles.miniButtonLeft))
            pathEditMode = PathEditMode.Replace;
        if (GUILayout.Toggle(pathEditMode == PathEditMode.AddPrefix, "加前缀", EditorStyles.miniButtonMid))
            pathEditMode = PathEditMode.AddPrefix;
        if (GUILayout.Toggle(pathEditMode == PathEditMode.AddSuffix, "加后缀", EditorStyles.miniButtonMid))
            pathEditMode = PathEditMode.AddSuffix;
        if (GUILayout.Toggle(pathEditMode == PathEditMode.InsertAt, "插入", EditorStyles.miniButtonMid))
            pathEditMode = PathEditMode.InsertAt;
        if (GUILayout.Toggle(pathEditMode == PathEditMode.RemoveSegment, "删节点", EditorStyles.miniButtonRight))
            pathEditMode = PathEditMode.RemoveSegment;

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // 编辑参数
        switch (pathEditMode)
        {
            case PathEditMode.Replace:
                searchPattern = EditorGUILayout.TextField("查找内容", searchPattern);
                replaceWith = EditorGUILayout.TextField("替换为", replaceWith);
                break;
            case PathEditMode.AddPrefix:
                prefixToAdd = EditorGUILayout.TextField("前缀内容", prefixToAdd);
                break;
            case PathEditMode.AddSuffix:
                suffixToAdd = EditorGUILayout.TextField("后缀内容", suffixToAdd);
                break;
            case PathEditMode.InsertAt:
                insertText = EditorGUILayout.TextField("插入内容", insertText);
                insertAtIndex = EditorGUILayout.IntField("插入位置(节点索引)", insertAtIndex);
                EditorGUILayout.HelpBox("路径按'/'分割，0表示在最前面插入", MessageType.Info);
                break;
            case PathEditMode.RemoveSegment:
                segmentIndexToRemove = EditorGUILayout.IntField("删除节点索引", segmentIndexToRemove);
                EditorGUILayout.HelpBox("路径按'/'分割，0表示第一个节点", MessageType.Info);
                break;
        }

        EditorGUILayout.Space(5);

        // 预览
        if (selectedPaths.Count > 0)
        {
            EditorGUILayout.LabelField("修改预览:", EditorStyles.boldLabel);
            var previewPath = selectedPaths.First();
            var parts = previewPath.Split('|');
            if (parts.Length == 2)
            {
                string originalPath = parts[1];
                string newPath = ApplyPathEdit(originalPath);
                EditorGUILayout.LabelField($"原路径: {originalPath}");
                EditorGUILayout.LabelField($"新路径: {newPath}");
            }
        }

        EditorGUILayout.Space(5);

        EditorGUI.BeginDisabledGroup(selectedPaths.Count == 0);
        if (GUILayout.Button("执行修改", GUILayout.Height(30)))
        {
            ExecutePathModification();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndVertical();
    }
    #endregion

    #region 操作日志标签页
    private void DrawOperationLogTab()
    {
        EditorGUILayout.LabelField("操作日志", EditorStyles.boldLabel);

        if (GUILayout.Button("清空日志"))
        {
            operationLogs.Clear();
        }

        EditorGUILayout.Space(5);

        logScrollPosition = EditorGUILayout.BeginScrollView(logScrollPosition, EditorStyles.helpBox);

        if (operationLogs.Count == 0)
        {
            EditorGUILayout.LabelField("暂无操作记录", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            foreach (var log in operationLogs)
            {
                EditorGUILayout.LabelField(log, EditorStyles.wordWrappedLabel);
            }
        }

        EditorGUILayout.EndScrollView();
    }
    #endregion

    #region 设置标签页
    private void DrawSettingsTab()
    {
        EditorGUILayout.LabelField("设置", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        autoBackup = EditorGUILayout.Toggle("修改前自动备份", autoBackup);

        if (autoBackup)
        {
            EditorGUILayout.BeginHorizontal();
            backupFolder = EditorGUILayout.TextField("备份文件夹", backupFolder);
            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("选择备份文件夹", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        backupFolder = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", "请选择Assets目录下的文件夹", "确定");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }
    #endregion

    #region 核心功能
    private void ScanAnimationClips()
    {
        scannedClips.Clear();
        clipPaths.Clear();
        clipFoldouts.Clear();
        selectedPaths.Clear();
        clipToParentAsset.Clear();

        List<AnimationClip> clips = new List<AnimationClip>();

        switch (scanMode)
        {
            case ScanMode.SelectedOnly:
                foreach (var obj in Selection.objects)
                {
                    if (obj is AnimationClip clip && (assetType == AssetType.AnimClip || assetType == AssetType.Both))
                    {
                        clips.Add(clip);
                    }
#if UNITY_2017_1_OR_NEWER
                    if (obj is TimelineAsset timeline && (assetType == AssetType.Timeline || assetType == AssetType.Both))
                    {
                        clips.AddRange(GetClipsFromTimeline(timeline));
                    }
#endif
                }
                break;

            case ScanMode.SingleFolder:
                if (singleFolder != null)
                {
                    string folderPath = AssetDatabase.GetAssetPath(singleFolder);
                    if (assetType == AssetType.AnimClip || assetType == AssetType.Both)
                        clips.AddRange(FindClipsInFolder(folderPath, includeSubfolders));
                    if (assetType == AssetType.Timeline || assetType == AssetType.Both)
                        clips.AddRange(FindTimelineClipsInFolder(folderPath, includeSubfolders));
                }
                break;

            case ScanMode.MultiFolders:
                foreach (var folder in multipleFolders)
                {
                    if (folder != null)
                    {
                        string folderPath = AssetDatabase.GetAssetPath(folder);
                        if (assetType == AssetType.AnimClip || assetType == AssetType.Both)
                            clips.AddRange(FindClipsInFolder(folderPath, includeSubfolders));
                        if (assetType == AssetType.Timeline || assetType == AssetType.Both)
                            clips.AddRange(FindTimelineClipsInFolder(folderPath, includeSubfolders));
                    }
                }
                break;
        }

        foreach (var clip in clips)
        {
            var paths = GetAllPathsFromClip(clip);
            if (paths.Count > 0)
            {
                scannedClips.Add(clip);
                clipPaths[clip] = paths;
            }
        }

        int timelineClipCount = clipToParentAsset.Count;
        int animClipCount = scannedClips.Count - timelineClipCount;
        AddLog($"扫描完成，找到 {animClipCount} 个独立动画片段，{timelineClipCount} 个Timeline内嵌动画片段");
    }

#if UNITY_2017_1_OR_NEWER
    private List<AnimationClip> GetClipsFromTimeline(TimelineAsset timeline)
    {
        var clips = new List<AnimationClip>();
        
        foreach (var track in timeline.GetOutputTracks())
        {
            if (track is AnimationTrack animTrack)
            {
                foreach (var timelineClip in animTrack.GetClips())
                {
                    var animClip = timelineClip.animationClip;
                    if (animClip != null)
                    {
                        clips.Add(animClip);
                        clipToParentAsset[animClip] = timeline;
                    }
                }

                // 获取无限剪辑（Infinite Clip）
                var infiniteClip = animTrack.infiniteClip;
                if (infiniteClip != null)
                {
                    clips.Add(infiniteClip);
                    clipToParentAsset[infiniteClip] = timeline;
                }
            }
        }

        return clips;
    }

    private List<AnimationClip> FindTimelineClipsInFolder(string folderPath, bool includeSubfolders)
    {
        var clips = new List<AnimationClip>();
        var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), folderPath);
        if (!Directory.Exists(fullPath))
            return clips;

        var files = Directory.GetFiles(fullPath, "*.playable", searchOption);
        foreach (var file in files)
        {
            string assetPath = "Assets" + file.Replace(Application.dataPath, "").Replace("\\", "/");
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(assetPath);
            if (timeline != null)
            {
                clips.AddRange(GetClipsFromTimeline(timeline));
            }
        }

        return clips;
    }
#else
    private List<AnimationClip> FindTimelineClipsInFolder(string folderPath, bool includeSubfolders)
    {
        return new List<AnimationClip>();
    }
#endif

    private List<AnimationClip> FindClipsInFolder(string folderPath, bool includeSubfolders)
    {
        var clips = new List<AnimationClip>();
        var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), folderPath);
        if (!Directory.Exists(fullPath))
            return clips;

        var files = Directory.GetFiles(fullPath, "*.anim", searchOption);
        foreach (var file in files)
        {
            string assetPath = "Assets" + file.Replace(Application.dataPath, "").Replace("\\", "/");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip != null)
            {
                clips.Add(clip);
            }
        }

        return clips;
    }

    private List<string> GetAllPathsFromClip(AnimationClip clip)
    {
        var paths = new HashSet<string>();

        var bindings = AnimationUtility.GetCurveBindings(clip);
        foreach (var binding in bindings)
        {
            if (!string.IsNullOrEmpty(binding.path))
                paths.Add(binding.path);
        }

        var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        foreach (var binding in objectBindings)
        {
            if (!string.IsNullOrEmpty(binding.path))
                paths.Add(binding.path);
        }

        return paths.ToList();
    }

    private string GetPathKey(AnimationClip clip, string path)
    {
        // 对于Timeline内嵌的动画，使用Timeline路径+clip名称作为key
        if (clipToParentAsset.ContainsKey(clip) && clipToParentAsset[clip] != null)
        {
            string timelinePath = AssetDatabase.GetAssetPath(clipToParentAsset[clip]);
            return $"{timelinePath}#{clip.name}|{path}";
        }
        return $"{AssetDatabase.GetAssetPath(clip)}|{path}";
    }

    private string ApplyPathEdit(string originalPath)
    {
        switch (pathEditMode)
        {
            case PathEditMode.Replace:
                if (string.IsNullOrEmpty(searchPattern))
                    return originalPath;
                return originalPath.Replace(searchPattern, replaceWith);

            case PathEditMode.AddPrefix:
                return prefixToAdd + originalPath;

            case PathEditMode.AddSuffix:
                return originalPath + suffixToAdd;

            case PathEditMode.InsertAt:
                var segments = originalPath.Split('/').ToList();
                int insertIdx = Mathf.Clamp(insertAtIndex, 0, segments.Count);
                segments.Insert(insertIdx, insertText);
                return string.Join("/", segments);

            case PathEditMode.RemoveSegment:
                var segs = originalPath.Split('/').ToList();
                if (segmentIndexToRemove >= 0 && segmentIndexToRemove < segs.Count)
                {
                    segs.RemoveAt(segmentIndexToRemove);
                }
                return string.Join("/", segs);

            default:
                return originalPath;
        }
    }

    private void ExecutePathModification()
    {
        if (selectedPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先选择要修改的路径", "确定");
            return;
        }

        // 按Clip分组
        var clipPathGroups = new Dictionary<AnimationClip, List<string>>();
        var modifiedTimelines = new HashSet<Object>();

        foreach (var pathKey in selectedPaths)
        {
            // 解析pathKey: 可能是 "assetPath|animPath" 或 "timelinePath#clipName|animPath"
            int pipeIndex = pathKey.LastIndexOf('|');
            if (pipeIndex < 0) continue;

            string assetPart = pathKey.Substring(0, pipeIndex);
            string animPath = pathKey.Substring(pipeIndex + 1);

            AnimationClip clip = null;

            if (assetPart.Contains("#"))
            {
                // Timeline内嵌动画
                int hashIndex = assetPart.LastIndexOf('#');
                string timelinePath = assetPart.Substring(0, hashIndex);
                string clipName = assetPart.Substring(hashIndex + 1);

                // 在已扫描的clips中查找
                clip = scannedClips.FirstOrDefault(c => 
                    c.name == clipName && 
                    clipToParentAsset.ContainsKey(c) && 
                    AssetDatabase.GetAssetPath(clipToParentAsset[c]) == timelinePath);

                if (clip != null && clipToParentAsset.ContainsKey(clip))
                {
                    modifiedTimelines.Add(clipToParentAsset[clip]);
                }
            }
            else
            {
                // 独立动画文件
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPart);
            }

            if (clip == null) continue;

            if (!clipPathGroups.ContainsKey(clip))
                clipPathGroups[clip] = new List<string>();
            clipPathGroups[clip].Add(animPath);
        }

        int modifiedCount = 0;

        foreach (var kvp in clipPathGroups)
        {
            var clip = kvp.Key;
            var pathsToModify = kvp.Value;

            // 备份（仅对独立动画文件）
            if (autoBackup && !clipToParentAsset.ContainsKey(clip))
            {
                BackupClip(clip);
            }

            // 修改路径
            bool modified = ModifyClipPaths(clip, pathsToModify);
            if (modified)
            {
                modifiedCount++;
                bool isTimeline = clipToParentAsset.ContainsKey(clip);
                AddLog($"已修改: {clip.name}{(isTimeline ? " (Timeline内嵌)" : "")}");
            }
        }

        // 标记Timeline为dirty
        foreach (var timeline in modifiedTimelines)
        {
            EditorUtility.SetDirty(timeline);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("完成", $"已修改 {modifiedCount} 个动画片段", "确定");
        AddLog($"批量修改完成，共修改 {modifiedCount} 个动画片段");

        // 重新扫描
        ScanAnimationClips();
    }

    private bool ModifyClipPaths(AnimationClip clip, List<string> pathsToModify)
    {
        bool anyModified = false;

        // 处理普通曲线
        var bindings = AnimationUtility.GetCurveBindings(clip);
        foreach (var binding in bindings)
        {
            if (pathsToModify.Contains(binding.path))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                string newPath = ApplyPathEdit(binding.path);

                if (newPath != binding.path)
                {
                    AnimationUtility.SetEditorCurve(clip, binding, null); // 删除旧曲线

                    var newBinding = new EditorCurveBinding
                    {
                        path = newPath,
                        propertyName = binding.propertyName,
                        type = binding.type
                    };
                    AnimationUtility.SetEditorCurve(clip, newBinding, curve);
                    anyModified = true;
                }
            }
        }

        // 处理对象引用曲线
        var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        foreach (var binding in objectBindings)
        {
            if (pathsToModify.Contains(binding.path))
            {
                var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                string newPath = ApplyPathEdit(binding.path);

                if (newPath != binding.path)
                {
                    AnimationUtility.SetObjectReferenceCurve(clip, binding, null); // 删除旧曲线

                    var newBinding = new EditorCurveBinding
                    {
                        path = newPath,
                        propertyName = binding.propertyName,
                        type = binding.type
                    };
                    AnimationUtility.SetObjectReferenceCurve(clip, newBinding, keyframes);
                    anyModified = true;
                }
            }
        }

        if (anyModified)
        {
            EditorUtility.SetDirty(clip);
        }

        return anyModified;
    }

    private void BackupClip(AnimationClip clip)
    {
        string clipPath = AssetDatabase.GetAssetPath(clip);
        string fileName = Path.GetFileName(clipPath);
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupPath = $"{backupFolder}/{timestamp}_{fileName}";

        // 确保备份文件夹存在
        if (!AssetDatabase.IsValidFolder(backupFolder))
        {
            string[] folders = backupFolder.Split('/');
            string currentPath = folders[0];
            for (int i = 1; i < folders.Length; i++)
            {
                string nextPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath = nextPath;
            }
        }

        AssetDatabase.CopyAsset(clipPath, backupPath);
        AddLog($"已备份: {clipPath} -> {backupPath}");
    }

    private void AddLog(string message)
    {
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        operationLogs.Insert(0, $"[{timestamp}] {message}");

        // 限制日志数量
        if (operationLogs.Count > 100)
        {
            operationLogs.RemoveAt(operationLogs.Count - 1);
        }
    }
    #endregion
}
