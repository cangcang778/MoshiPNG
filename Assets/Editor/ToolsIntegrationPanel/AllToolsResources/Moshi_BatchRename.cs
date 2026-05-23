#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public class Moshi_BatchRename : EditorWindow
{
    private const string TOOL_NAME = "批量重命名";
    
    // 标签页控制
    private enum TabType { Project, Hierarchy }
    private TabType currentTab = TabType.Project;
    
    // 重命名模式
    private enum RenameMode { Modify, Direct }
    private RenameMode renameMode = RenameMode.Modify;
    
    // 文件来源模式
    private enum SourceMode { Selection, Folders }
    private SourceMode sourceMode = SourceMode.Selection;
    
    // 排序模式
    private enum SortMode { None, ByName, ByNaturalName, ByHierarchy }
    private SortMode projectSortMode = SortMode.ByNaturalName;
    private SortMode hierarchySortMode = SortMode.ByHierarchy;
    
    // 多文件夹相关变量
    private List<string> targetFolders = new List<string>();
    private bool includeSubfolders = true;
    private Vector2 folderListScrollPosition;
    
    // 重命名相关变量
    private string renameSearch = "";
    private string renameReplace = "";
    private string prefix = "";
    private string suffix = "";
    private bool showRenameSection = true;
    private bool includeHierarchyChildren = true;
    private string fileTypeFilter = "";
    private bool showNumberingOptions = false;
    private bool enableSequentialNumbering = false;
    private int sequenceStart = 1;
    private int sequenceStep = 1;
    private int sequenceDigits = 2;
    
    // 直接重命名模式变量
    private bool enableDirectHead = true;
    private bool enableDirectMiddle = true;
    private bool enableDirectTail = false;
    private bool enableDirectNumber = true;
    
    private string directHead = "";
    private string directMiddle = "";
    private string directTail = "";
    private string partSeparator = "_";
    
    private enum NumberPosition { Front, Middle, Back }
    private NumberPosition numberPosition = NumberPosition.Back;
    
    // 界面控制
    private Vector2 scrollPosition;
    private Vector2 previewScrollPosition;
    private List<string> operationLog = new List<string>();
    private List<string> errorLog = new List<string>();
    private List<string> renamePreviewEntries = new List<string>();

    private const string NumberingPlaceholder = "{#}";
    
    /// <summary>
    /// 自然排序比较器（正确处理数字：1,2,3...10 而非 1,10,2...）
    /// </summary>
    private class NaturalStringComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            
            // 使用正则表达式分割字符串为文本和数字部分
            var regex = new Regex(@"(\d+|\D+)");
            var partsX = regex.Matches(x);
            var partsY = regex.Matches(y);
            
            int minCount = Math.Min(partsX.Count, partsY.Count);
            
            for (int i = 0; i < minCount; i++)
            {
                string partX = partsX[i].Value;
                string partY = partsY[i].Value;
                
                bool isNumX = int.TryParse(partX, out int numX);
                bool isNumY = int.TryParse(partY, out int numY);
                
                int result;
                if (isNumX && isNumY)
                {
                    // 两者都是数字，按数值比较
                    result = numX.CompareTo(numY);
                }
                else
                {
                    // 至少一个是文本，按字符串比较
                    result = string.Compare(partX, partY, StringComparison.OrdinalIgnoreCase);
                }
                
                if (result != 0) return result;
            }
            
            // 如果前面部分都相同，较短的排在前面
            return partsX.Count.CompareTo(partsY.Count);
        }
    }
    
    private static readonly NaturalStringComparer naturalComparer = new NaturalStringComparer();

    [MenuItem("工具/Moshi/" + TOOL_NAME)]
    public static void ShowWindow()
    {
        GetWindow<Moshi_BatchRename>(TOOL_NAME);
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(TOOL_NAME, EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        // 标签页选择
        currentTab = (TabType)GUILayout.Toolbar((int)currentTab, new string[] { "Project窗口", "Hierarchy面板" });
        EditorGUILayout.Space();

        // 根据当前标签显示不同的操作界面
        switch (currentTab)
        {
            case TabType.Project:
                DrawProjectTab();
                break;
            case TabType.Hierarchy:
                DrawHierarchyTab();
                break;
        }

        EditorGUILayout.Space();
        DrawRenamePreviewSection();

        // 操作日志显示
        DisplayOperationLog();
        
        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// 绘制Project窗口标签页
    /// </summary>
    private void DrawProjectTab()
    {
        // 批量重命名部分
        showRenameSection = EditorGUILayout.Foldout(showRenameSection, "批量重命名文件");
        if (showRenameSection)
        {
            EditorGUI.indentLevel++;
            
            // 文件来源模式选择
            DrawSourceModeSelector();
            
            // 重命名模式选择
            DrawRenameModeSelector();
            
            if (renameMode == RenameMode.Modify)
            {
                // 修改模式UI
                renameSearch = EditorGUILayout.TextField("查找内容:", renameSearch);
                renameReplace = EditorGUILayout.TextField("替换为:", renameReplace);
                prefix = EditorGUILayout.TextField("文件抬头名:", prefix);
                suffix = EditorGUILayout.TextField("文件后缀名:", suffix);
                DrawNumberingOptionsUI();
            }
            else
            {
                // 直接重命名模式UI
                DrawDirectRenameUI();
            }
            
            EditorGUILayout.Space(5);
            
            // 排序模式选择（Project只有不排序、按名称、按自然排序三个选项）
            DrawSortModeSelector(false);
            
            fileTypeFilter = EditorGUILayout.TextField("文件类型过滤 (.prefab;.mat):", fileTypeFilter);

            string buttonSuffix = sourceMode == SourceMode.Selection ? "(选中文件)" : "(指定文件夹)";
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"生成重命名预览 {buttonSuffix}"))
            {
                GenerateProjectRenamePreview();
            }
            if (GUILayout.Button($"执行重命名 {buttonSuffix}"))
            {
                BatchRenameSelectedFiles();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }
    }

    /// <summary>
    /// 绘制Hierarchy窗口标签页
    /// </summary>
    private void DrawHierarchyTab()
    {
        GUILayout.Label("Hierarchy对象重命名", EditorStyles.boldLabel);
        
        EditorGUILayout.HelpBox("此功能用于重命名Hierarchy面板中的GameObject", MessageType.Info);
        
        // 重命名模式选择
        DrawRenameModeSelector();
        
        if (renameMode == RenameMode.Modify)
        {
            // 修改模式UI
            renameSearch = EditorGUILayout.TextField("查找内容:", renameSearch);
            renameReplace = EditorGUILayout.TextField("替换为:", renameReplace);
            prefix = EditorGUILayout.TextField("对象抬头名:", prefix);
            suffix = EditorGUILayout.TextField("对象后缀名:", suffix);
            DrawNumberingOptionsUI();
        }
        else
        {
            // 直接重命名模式UI
            DrawDirectRenameUI();
        }
        
        EditorGUILayout.Space(5);
        includeHierarchyChildren = EditorGUILayout.Toggle("包含子对象", includeHierarchyChildren);
        
        // 排序模式选择（Hierarchy有按层级选项）
        DrawSortModeSelector(true);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("生成重命名预览 (Hierarchy对象)"))
        {
            GenerateHierarchyRenamePreview();
        }
        if (GUILayout.Button("执行重命名 (Hierarchy对象)"))
        {
            BatchRenameHierarchyObjects();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("提示：Hierarchy重命名支持Ctrl+Z撤销操作。", MessageType.Info);
    }
    
    /// <summary>
    /// 绘制排序模式选择器
    /// </summary>
    private void DrawSortModeSelector(bool isHierarchyTab)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("排序方式", GUILayout.Width(60));
        
        if (isHierarchyTab)
        {
            // Hierarchy标签页
            if (GUILayout.Toggle(hierarchySortMode == SortMode.None, "不排序", EditorStyles.miniButtonLeft))
            {
                hierarchySortMode = SortMode.None;
            }
            if (GUILayout.Toggle(hierarchySortMode == SortMode.ByName, "按名称", EditorStyles.miniButtonMid))
            {
                hierarchySortMode = SortMode.ByName;
            }
            if (GUILayout.Toggle(hierarchySortMode == SortMode.ByNaturalName, "自然排序", EditorStyles.miniButtonMid))
            {
                hierarchySortMode = SortMode.ByNaturalName;
            }
            if (GUILayout.Toggle(hierarchySortMode == SortMode.ByHierarchy, "按层级", EditorStyles.miniButtonRight))
            {
                hierarchySortMode = SortMode.ByHierarchy;
            }
        }
        else
        {
            // Project标签页
            if (GUILayout.Toggle(projectSortMode == SortMode.None, "不排序", EditorStyles.miniButtonLeft))
            {
                projectSortMode = SortMode.None;
            }
            if (GUILayout.Toggle(projectSortMode == SortMode.ByName, "按名称", EditorStyles.miniButtonMid))
            {
                projectSortMode = SortMode.ByName;
            }
            if (GUILayout.Toggle(projectSortMode == SortMode.ByNaturalName, "自然排序", EditorStyles.miniButtonRight))
            {
                projectSortMode = SortMode.ByNaturalName;
            }
        }
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制自动编号折叠面板
    /// </summary>
    private void DrawNumberingOptionsUI()
    {
        showNumberingOptions = EditorGUILayout.Foldout(showNumberingOptions, "自动编号设置");
        if (!showNumberingOptions)
            return;

        EditorGUI.indentLevel++;
        enableSequentialNumbering = EditorGUILayout.Toggle("启用自动编号", enableSequentialNumbering);

        using (new EditorGUI.DisabledScope(!enableSequentialNumbering))
        {
            sequenceStart = EditorGUILayout.IntField("起始值", sequenceStart);
            sequenceStep = Mathf.Max(1, EditorGUILayout.IntField("步长", sequenceStep));
            sequenceDigits = Mathf.Max(0, EditorGUILayout.IntField("序号位数 (0=不补零)", sequenceDigits));
            EditorGUILayout.HelpBox($"可在结果中使用 {NumberingPlaceholder} 指定序号位置，未使用时将自动附加到末尾。", MessageType.Info);
        }

        EditorGUI.indentLevel--;
    }

    /// <summary>
    /// 绘制文件来源模式选择器
    /// </summary>
    private void DrawSourceModeSelector()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("文件来源:", GUILayout.Width(80));
        sourceMode = (SourceMode)GUILayout.Toolbar((int)sourceMode, new string[] { "选中文件", "指定文件夹" });
        EditorGUILayout.EndHorizontal();
        
        if (sourceMode == SourceMode.Folders)
        {
            DrawFolderListUI();
        }
        
        EditorGUILayout.Space(5);
    }

    /// <summary>
    /// 绘制多文件夹列表UI
    /// </summary>
    private void DrawFolderListUI()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("目标文件夹列表", EditorStyles.boldLabel);
        
        // 文件夹列表滚动区域
        folderListScrollPosition = EditorGUILayout.BeginScrollView(folderListScrollPosition, GUILayout.Height(120));
        
        int removeIndex = -1;
        for (int i = 0; i < targetFolders.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            
            // 可编辑的路径输入框
            string newPath = EditorGUILayout.TextField(targetFolders[i]);
            if (newPath != targetFolders[i])
            {
                targetFolders[i] = newPath;
            }
            
            // 浏览按钮
            if (GUILayout.Button("浏览", GUILayout.Width(45)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("选择文件夹", "Assets", "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    // 转换为相对路径
                    string dataPath = Application.dataPath;
                    if (selectedPath.StartsWith(dataPath))
                    {
                        selectedPath = "Assets" + selectedPath.Substring(dataPath.Length);
                    }
                    targetFolders[i] = selectedPath;
                }
            }
            
            // 删除按钮
            if (GUILayout.Button("×", GUILayout.Width(25)))
            {
                removeIndex = i;
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        if (removeIndex >= 0)
        {
            targetFolders.RemoveAt(removeIndex);
        }
        
        // 快捷键提示（小字）
        var miniLabelStyle = new GUIStyle(EditorStyles.miniLabel);
        miniLabelStyle.normal.textColor = Color.gray;
        EditorGUILayout.LabelField("提示: Project窗口选中文件夹后按 Ctrl+Alt+C 复制路径", miniLabelStyle);
        
        // 拖拽区域
        Rect dropArea = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "拖拽文件夹到此处添加", EditorStyles.helpBox);
        HandleDragAndDrop(dropArea);
        
        EditorGUILayout.EndScrollView();
        
        // 操作按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 添加文件夹"))
        {
            targetFolders.Add("");  // 添加空路径，用户可编辑
        }
        if (GUILayout.Button("清空列表"))
        {
            targetFolders.Clear();
        }
        EditorGUILayout.EndHorizontal();
        
        // 子文件夹选项
        includeSubfolders = EditorGUILayout.Toggle("包含子文件夹", includeSubfolders);
        
        // 显示统计信息（过滤无效路径）
        int validFolderCount = targetFolders.Count(f => !string.IsNullOrEmpty(f) && AssetDatabase.IsValidFolder(f));
        if (targetFolders.Count > 0)
        {
            int fileCount = CountFilesInFolders();
            EditorGUILayout.HelpBox($"有效文件夹: {validFolderCount}/{targetFolders.Count} 个，共 {fileCount} 个文件符合条件", MessageType.Info);
        }
        
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 处理拖拽事件
    /// </summary>
    private void HandleDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;
        
        if (!dropArea.Contains(evt.mousePosition))
            return;
            
        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                bool hasValidFolder = false;
                foreach (var path in DragAndDrop.paths)
                {
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        hasValidFolder = true;
                        break;
                    }
                }
                
                if (hasValidFolder)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (var path in DragAndDrop.paths)
                        {
                            if (AssetDatabase.IsValidFolder(path) && !targetFolders.Contains(path))
                            {
                                targetFolders.Add(path);
                            }
                        }
                    }
                }
                else
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                }
                evt.Use();
                break;
        }
    }

    /// <summary>
    /// 添加当前选中的文件夹
    /// </summary>
    private void AddSelectedFolders()
    {
        foreach (var obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (AssetDatabase.IsValidFolder(path) && !targetFolders.Contains(path))
            {
                targetFolders.Add(path);
            }
        }
        
        if (targetFolders.Count == 0)
        {
            AddError("请在Project窗口中选择文件夹！");
        }
    }

    /// <summary>
    /// 统计文件夹中的文件数量
    /// </summary>
    private int CountFilesInFolders()
    {
        var normalizedFilters = BuildNormalizedFileTypeFilters();
        int count = 0;
        
        foreach (var folder in targetFolders)
        {
            var files = CollectFilesFromFolder(folder, normalizedFilters);
            count += files.Count;
        }
        
        return count;
    }

    /// <summary>
    /// 从指定文件夹收集文件
    /// </summary>
    private List<FileInfoData> CollectFilesFromFolder(string folderPath, List<string> normalizedFilters)
    {
        List<FileInfoData> result = new List<FileInfoData>();
        
        // 跳过空路径或无效路径
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            return result;
        
        string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), folderPath);
        if (!Directory.Exists(fullPath))
            return result;
            
        SearchOption searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        string[] files = Directory.GetFiles(fullPath, "*.*", searchOption);
        
        foreach (string file in files)
        {
            // 跳过meta文件
            if (file.EndsWith(".meta"))
                continue;
                
            string relativePath = "Assets" + file.Substring(Application.dataPath.Length).Replace("\\", "/");
            
            if (!IsAssetAllowedByFilter(relativePath, normalizedFilters))
                continue;
                
            string fileName = Path.GetFileNameWithoutExtension(file);
            result.Add(new FileInfoData { Path = relativePath, Name = fileName });
        }
        
        return result;
    }

    /// <summary>
    /// 从所有目标文件夹收集文件
    /// </summary>
    private List<FileInfoData> CollectAllFilesFromFolders()
    {
        var normalizedFilters = BuildNormalizedFileTypeFilters();
        List<FileInfoData> allFiles = new List<FileInfoData>();
        HashSet<string> addedPaths = new HashSet<string>();
        
        foreach (var folder in targetFolders)
        {
            var files = CollectFilesFromFolder(folder, normalizedFilters);
            foreach (var file in files)
            {
                if (!addedPaths.Contains(file.Path))
                {
                    addedPaths.Add(file.Path);
                    allFiles.Add(file);
                }
            }
        }
        
        return allFiles;
    }

    /// <summary>
    /// 文件信息数据结构
    /// </summary>
    private struct FileInfoData
    {
        public string Path;
        public string Name;
    }

    /// <summary>
    /// 绘制重命名模式选择器
    /// </summary>
    private void DrawRenameModeSelector()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("重命名模式:", GUILayout.Width(80));
        renameMode = (RenameMode)GUILayout.Toolbar((int)renameMode, new string[] { "修改模式(基于原名)", "直接重命名模式" });
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);
    }

    /// <summary>
    /// 绘制直接重命名模式UI
    /// </summary>
    private void DrawDirectRenameUI()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("直接重命名设置", EditorStyles.boldLabel);
        
        // 分隔符设置
        partSeparator = EditorGUILayout.TextField("分隔符:", partSeparator);
        EditorGUILayout.Space(3);
        
        // 启用开关行
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("启用部分:", GUILayout.Width(60));
        enableDirectHead = GUILayout.Toggle(enableDirectHead, "抬头", GUILayout.Width(50));
        enableDirectMiddle = GUILayout.Toggle(enableDirectMiddle, "中段", GUILayout.Width(50));
        enableDirectTail = GUILayout.Toggle(enableDirectTail, "后缀", GUILayout.Width(50));
        enableDirectNumber = GUILayout.Toggle(enableDirectNumber, "编号", GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        // 内容输入
        using (new EditorGUI.DisabledScope(!enableDirectHead))
        {
            directHead = EditorGUILayout.TextField("抬头内容:", directHead);
        }
        
        using (new EditorGUI.DisabledScope(!enableDirectMiddle))
        {
            directMiddle = EditorGUILayout.TextField("中段内容:", directMiddle);
        }
        
        using (new EditorGUI.DisabledScope(!enableDirectTail))
        {
            directTail = EditorGUILayout.TextField("后缀内容:", directTail);
        }
        
        // 编号位置
        using (new EditorGUI.DisabledScope(!enableDirectNumber))
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("编号位置:", GUILayout.Width(80));
            numberPosition = (NumberPosition)EditorGUILayout.Popup((int)numberPosition, new string[] { "最前", "中间", "最后" });
            EditorGUILayout.EndHorizontal();
        }
        
        // 编号设置（直接重命名模式下的简化版）
        if (enableDirectNumber)
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("起始值:", GUILayout.Width(50));
            sequenceStart = EditorGUILayout.IntField(sequenceStart, GUILayout.Width(50));
            GUILayout.Label("步长:", GUILayout.Width(35));
            sequenceStep = Mathf.Max(1, EditorGUILayout.IntField(sequenceStep, GUILayout.Width(50)));
            GUILayout.Label("位数:", GUILayout.Width(35));
            sequenceDigits = Mathf.Max(0, EditorGUILayout.IntField(sequenceDigits, GUILayout.Width(50)));
            EditorGUILayout.EndHorizontal();
        }
        
        // 实时预览示例
        EditorGUILayout.Space(5);
        string exampleName = BuildDirectRenameName(sequenceStart);
        EditorGUILayout.HelpBox($"示例预览: {exampleName}", MessageType.None);
        
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 构建直接重命名的名称
    /// </summary>
    private string BuildDirectRenameName(int sequenceNumber)
    {
        List<string> parts = new List<string>();
        string formattedNumber = FormatSequenceNumber(sequenceNumber);
        
        // 根据编号位置插入各部分
        switch (numberPosition)
        {
            case NumberPosition.Front:
                if (enableDirectNumber) parts.Add(formattedNumber);
                if (enableDirectHead && !string.IsNullOrEmpty(directHead)) parts.Add(directHead);
                if (enableDirectMiddle && !string.IsNullOrEmpty(directMiddle)) parts.Add(directMiddle);
                if (enableDirectTail && !string.IsNullOrEmpty(directTail)) parts.Add(directTail);
                break;
                
            case NumberPosition.Middle:
                if (enableDirectHead && !string.IsNullOrEmpty(directHead)) parts.Add(directHead);
                if (enableDirectNumber) parts.Add(formattedNumber);
                if (enableDirectMiddle && !string.IsNullOrEmpty(directMiddle)) parts.Add(directMiddle);
                if (enableDirectTail && !string.IsNullOrEmpty(directTail)) parts.Add(directTail);
                break;
                
            case NumberPosition.Back:
            default:
                if (enableDirectHead && !string.IsNullOrEmpty(directHead)) parts.Add(directHead);
                if (enableDirectMiddle && !string.IsNullOrEmpty(directMiddle)) parts.Add(directMiddle);
                if (enableDirectTail && !string.IsNullOrEmpty(directTail)) parts.Add(directTail);
                if (enableDirectNumber) parts.Add(formattedNumber);
                break;
        }
        
        if (parts.Count == 0)
        {
            return "unnamed";
        }
        
        return string.Join(partSeparator, parts);
    }

    /// <summary>
    /// 显示重命名预览
    /// </summary>
    private void DrawRenamePreviewSection()
    {
        if (renamePreviewEntries == null || renamePreviewEntries.Count == 0)
            return;

        GUILayout.Label("重命名预览", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("以下为当前配置生成的预览，执行操作前请确认。", MessageType.None);

        previewScrollPosition = EditorGUILayout.BeginScrollView(previewScrollPosition, GUILayout.Height(180));
        foreach (var entry in renamePreviewEntries)
        {
            EditorGUILayout.LabelField(entry, EditorStyles.helpBox);
        }
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("清空预览"))
        {
            renamePreviewEntries.Clear();
        }

        EditorGUILayout.Space();
    }

    /// <summary>
    /// 批量重命名选中的文件（Project窗口）
    /// </summary>
    private void BatchRenameSelectedFiles()
    {
        List<FileInfoData> fileInfos;
        
        if (sourceMode == SourceMode.Folders)
        {
            // 从指定文件夹收集文件
            if (targetFolders.Count == 0)
            {
                AddError("请先添加目标文件夹！");
                return;
            }
            fileInfos = CollectAllFilesFromFolders();
        }
        else
        {
            // 从选中文件收集
            var selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                AddError("请先在Project窗口中选择要重命名的文件！");
                return;
            }

            var normalizedFilters = BuildNormalizedFileTypeFilters();
            fileInfos = selectedObjects
                .Select(obj => new FileInfoData {
                    Path = AssetDatabase.GetAssetPath(obj),
                    Name = obj.name
                })
                .Where(x => !string.IsNullOrEmpty(x.Path))
                .Where(x => IsAssetAllowedByFilter(x.Path, normalizedFilters))
                .ToList();
        }

        if (fileInfos.Count == 0)
        {
            AddError("当前条件下没有可处理的资源。");
            return;
        }

        // 应用排序
        fileInfos = ApplySortToFiles(fileInfos);

        int successCount = 0;
        int sequenceValue = sequenceStart;
        int step = Mathf.Max(1, sequenceStep);
        
        foreach (var fileInfo in fileInfos)
        {
            // 直接重命名模式始终传递序号，修改模式根据开关决定
            bool useSequence = renameMode == RenameMode.Direct || enableSequentialNumbering;
            string newName = ProcessRename(fileInfo.Name, useSequence ? sequenceValue : (int?)null);
            
            if (newName == fileInfo.Name)
            {
                AddLog(string.Format("文件 '{0}' 无需重命名，已跳过", fileInfo.Name));
                sequenceValue = AdvanceSequence(sequenceValue, step);
                continue;
            }

            string result = AssetDatabase.RenameAsset(fileInfo.Path, newName);
            if (string.IsNullOrEmpty(result))
            {
                successCount++;
                AddLog(string.Format("成功重命名: {0} → {1}", fileInfo.Name, newName));
            }
            else
            {
                AddError(string.Format("重命名失败: {0} - {1}", fileInfo.Path, result));
            }

            sequenceValue = AdvanceSequence(sequenceValue, step);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        AddLog(string.Format("Project重命名完成！成功重命名 {0} 个文件", successCount));
    }

    /// <summary>
    /// 批量重命名Hierarchy中的对象
    /// </summary>
    private void BatchRenameHierarchyObjects()
    {
        var selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            AddError("请先在Hierarchy面板中选择要重命名的GameObject！");
            return;
        }

        // 收集所有要重命名的对象（包括子对象）
        List<GameObject> allObjects = CollectHierarchyObjects(selectedObjects);

        // 应用排序
        allObjects = ApplySortToGameObjects(allObjects);

        int successCount = 0;
        int skippedCount = 0;
        int sequenceValue = sequenceStart;
        int step = Mathf.Max(1, sequenceStep);

        // 开始记录Undo操作
        Undo.RecordObjects(allObjects.ToArray(), "Batch Rename Hierarchy Objects");

        foreach (var gameObject in allObjects)
        {
            string originalName = gameObject.name;
            // 直接重命名模式始终传递序号，修改模式根据开关决定
            bool useSequence = renameMode == RenameMode.Direct || enableSequentialNumbering;
            string newName = ProcessRename(originalName, useSequence ? sequenceValue : (int?)null);
            
            if (newName == originalName)
            {
                skippedCount++;
            }
            else
            {
                gameObject.name = newName;
                successCount++;
                AddLog(string.Format("Hierarchy: {0} → {1}", originalName, newName));
            }

            sequenceValue = AdvanceSequence(sequenceValue, step);
        }

        AddLog(string.Format("Hierarchy重命名完成！成功: {0}, 跳过: {1}, 总计: {2}", 
            successCount, skippedCount, allObjects.Count));
    }

    /// <summary>
    /// 处理重命名逻辑（统一处理Project和Hierarchy）
    /// </summary>
    private string ProcessRename(string originalName, int? sequenceNumber = null)
    {
        // 直接重命名模式
        if (renameMode == RenameMode.Direct)
        {
            return BuildDirectRenameName(sequenceNumber ?? sequenceStart);
        }
        
        // 修改模式
        string newName = originalName;

        // 应用基本替换
        if (!string.IsNullOrEmpty(renameSearch) && newName.Contains(renameSearch))
        {
            newName = newName.Replace(renameSearch, renameReplace);
        }

        // 应用抬头名
        if (!string.IsNullOrEmpty(prefix))
        {
            newName = prefix + newName;
        }

        // 应用后缀名
        if (!string.IsNullOrEmpty(suffix))
        {
            newName = newName + suffix;
        }

        if (enableSequentialNumbering && sequenceNumber.HasValue)
        {
            newName = ApplySequentialNumber(newName, sequenceNumber.Value);
        }

        return newName;
    }

    private string ApplySequentialNumber(string baseName, int sequenceNumber)
    {
        string formatted = FormatSequenceNumber(sequenceNumber);
        if (!string.IsNullOrEmpty(NumberingPlaceholder) && baseName.Contains(NumberingPlaceholder))
        {
            return baseName.Replace(NumberingPlaceholder, formatted);
        }

        return baseName + formatted;
    }

    private string FormatSequenceNumber(int sequenceNumber)
    {
        if (sequenceDigits <= 0)
        {
            return sequenceNumber.ToString();
        }

        string format = new string('0', Mathf.Clamp(sequenceDigits, 1, 10));
        return sequenceNumber.ToString(format);
    }

    private int AdvanceSequence(int currentValue, int step)
    {
        // 直接重命名模式或修改模式启用编号时，序号递增
        if (renameMode == RenameMode.Direct || enableSequentialNumbering)
        {
            return currentValue + Mathf.Max(1, step);
        }

        return currentValue;
    }

    /// <summary>
    /// 生成Project选择的重命名预览
    /// </summary>
    private void GenerateProjectRenamePreview()
    {
        renamePreviewEntries.Clear();

        List<FileInfoData> fileInfos;
        
        if (sourceMode == SourceMode.Folders)
        {
            // 从指定文件夹收集文件
            if (targetFolders.Count == 0)
            {
                renamePreviewEntries.Add("Project: 请先添加目标文件夹。");
                return;
            }
            fileInfos = CollectAllFilesFromFolders();
        }
        else
        {
            // 从选中文件收集
            var selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                renamePreviewEntries.Add("Project: 请先选择需要预览的文件。");
                return;
            }

            var normalizedFilters = BuildNormalizedFileTypeFilters();
            fileInfos = selectedObjects
                .Select(obj => new FileInfoData {
                    Path = AssetDatabase.GetAssetPath(obj),
                    Name = obj.name
                })
                .Where(x => !string.IsNullOrEmpty(x.Path))
                .Where(x => IsAssetAllowedByFilter(x.Path, normalizedFilters))
                .ToList();
        }

        if (fileInfos.Count == 0)
        {
            renamePreviewEntries.Add("Project: 没有符合条件的资源生成预览。");
            return;
        }

        // 应用排序
        fileInfos = ApplySortToFiles(fileInfos);

        int sequenceValue = sequenceStart;
        int step = Mathf.Max(1, sequenceStep);

        foreach (var fileInfo in fileInfos)
        {
            bool useSequence = renameMode == RenameMode.Direct || enableSequentialNumbering;
            string newName = ProcessRename(fileInfo.Name, useSequence ? sequenceValue : (int?)null);
            renamePreviewEntries.Add(string.Format("Project | {0} → {1}", fileInfo.Name, newName));
            sequenceValue = AdvanceSequence(sequenceValue, step);
        }
    }

    /// <summary>
    /// 生成Hierarchy对象的预览
    /// </summary>
    private void GenerateHierarchyRenamePreview()
    {
        renamePreviewEntries.Clear();

        var selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            renamePreviewEntries.Add("Hierarchy: 请先选择需要预览的对象。");
            return;
        }

        List<GameObject> allObjects = CollectHierarchyObjects(selectedObjects);

        // 应用排序
        allObjects = ApplySortToGameObjects(allObjects);

        int sequenceValue = sequenceStart;
        int step = Mathf.Max(1, sequenceStep);

        foreach (var gameObject in allObjects)
        {
            bool useSequence = renameMode == RenameMode.Direct || enableSequentialNumbering;
            string newName = ProcessRename(gameObject.name, useSequence ? sequenceValue : (int?)null);
            renamePreviewEntries.Add(string.Format("Hierarchy | {0} → {1}", gameObject.name, newName));
            sequenceValue = AdvanceSequence(sequenceValue, step);
        }
    }

    private List<GameObject> CollectHierarchyObjects(GameObject[] selectedObjects)
    {
        List<GameObject> allObjects = new List<GameObject>();

        foreach (var obj in selectedObjects)
        {
            allObjects.Add(obj);
            if (includeHierarchyChildren)
            {
                allObjects.AddRange(obj.GetComponentsInChildren<Transform>(true)
                    .Select(t => t.gameObject)
                    .Where(go => go != obj));
            }
        }

        return allObjects.Distinct().ToList();
    }

    private List<string> BuildNormalizedFileTypeFilters()
    {
        if (string.IsNullOrWhiteSpace(fileTypeFilter))
        {
            return null;
        }

        return fileTypeFilter
            .Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .Where(token => !string.IsNullOrEmpty(token))
            .Select(token => token.StartsWith(".") ? token.ToLowerInvariant() : "." + token.ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    private bool IsAssetAllowedByFilter(string assetPath, List<string> normalizedFilters)
    {
        if (normalizedFilters == null || normalizedFilters.Count == 0)
        {
            return true;
        }

        string extension = Path.GetExtension(assetPath).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        return normalizedFilters.Contains(extension);
    }
    
    /// <summary>
    /// 对文件列表应用排序
    /// </summary>
    private List<FileInfoData> ApplySortToFiles(List<FileInfoData> files)
    {
        switch (projectSortMode)
        {
            case SortMode.ByName:
                return files.OrderBy(x => x.Name).ToList();
            case SortMode.ByNaturalName:
                return files.OrderBy(x => x.Name, naturalComparer).ToList();
            case SortMode.None:
            default:
                return files;
        }
    }
    
    /// <summary>
    /// 对GameObject列表应用排序
    /// </summary>
    private List<GameObject> ApplySortToGameObjects(List<GameObject> objects)
    {
        switch (hierarchySortMode)
        {
            case SortMode.ByName:
                return objects.OrderBy(obj => obj.name).ToList();
            case SortMode.ByNaturalName:
                return objects.OrderBy(obj => obj.name, naturalComparer).ToList();
            case SortMode.ByHierarchy:
                return SortByHierarchyOrder(objects);
            case SortMode.None:
            default:
                return objects;
        }
    }
    
    /// <summary>
    /// 按层级顺序排序（从上到下）
    /// </summary>
    private List<GameObject> SortByHierarchyOrder(List<GameObject> objects)
    {
        return objects.OrderBy(go => GetHierarchyPath(go)).ToList();
    }
    
    /// <summary>
    /// 获取GameObject的层级路径（用于排序）
    /// </summary>
    private string GetHierarchyPath(GameObject go)
    {
        StringBuilder path = new StringBuilder();
        Transform current = go.transform;
        List<string> indices = new List<string>();
        
        while (current != null)
        {
            // 使用5位数字格式化sibling index，确保正确排序
            indices.Insert(0, current.GetSiblingIndex().ToString("D5"));
            current = current.parent;
        }
        
        return string.Join("/", indices);
    }

    /// <summary>
    /// 显示操作日志
    /// </summary>
    private void DisplayOperationLog()
    {
        if (operationLog.Count > 0 || errorLog.Count > 0)
        {
            GUILayout.Label("操作日志:", EditorStyles.boldLabel);
            
            foreach (var log in operationLog)
            {
                EditorGUILayout.HelpBox(log, MessageType.Info);
            }
            
            foreach (var error in errorLog)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
            
            if (GUILayout.Button("清空日志"))
            {
                operationLog.Clear();
                errorLog.Clear();
            }
        }
    }

    private void AddLog(string message)
    {
        operationLog.Add(message);
    }

    private void AddError(string message)
    {
        errorLog.Add(message);
    }
}
#endif
