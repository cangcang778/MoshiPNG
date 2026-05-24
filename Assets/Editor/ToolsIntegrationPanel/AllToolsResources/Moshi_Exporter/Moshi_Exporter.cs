#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MoshiTools
{
    /// <summary>
    /// Moshi工具箱导出工具 - 一键打包所有Moshi工具为ZIP或UnityPackage
    /// Moshi Toolbox Exporter - One-click export all Moshi tools as ZIP or UnityPackage
    /// </summary>
    public class Moshi_Exporter : EditorWindow
    {
        private const string TOOL_NAME = "工具箱导出";

        // 导出格式枚举
        // Export format enumeration
        private enum ExportFormat { Zip, UnityPackage }
        private ExportFormat exportFormat = ExportFormat.Zip;

        // 导出范围枚举
        // Export scope enumeration
        private enum ExportScope { All, Individual }
        private ExportScope exportScope = ExportScope.All;

        // 导出内容开关
        // Export content toggles
        private bool includeEditorTools = true;
        private bool includeRuntimeScripts = true;
        private bool includeMetaFiles = false;

        // 源目录（相对 Assets 的路径）
        // Source directories (relative to Assets)
        private static readonly string EditorToolsPath = "Assets/Editor/ToolsIntegrationPanel";
        private static readonly string RuntimeScriptsPath = "Assets/Scripts/MoshiScripts";

        // 单独导出工具条目
        // Individual export tool entry
        [Serializable]
        private class ToolEntry
        {
            public string toolName;          // 工具名称（文件夹名或文件名，如 Moshi_ClipHelper）
            public string displayName;       // 中文显示名（如 "动画助手"，与面板按钮对应）
            public string editorPath;        // Editor 路径（全路径，可为null）
            public string runtimePath;       // Runtime 路径（全路径，可为null）
            public bool isFolder;            // 是否为文件夹型工具
            public int editorFileCount;      // Editor 文件数
            public int runtimeFileCount;     // Runtime 文件数
            public long totalSize;           // 总文件大小
        }

        // 工具列表与单选索引
        // Tool list and single selection index
        private List<ToolEntry> toolEntries;
        private int selectedToolIndex = -1;  // 当前选中工具索引（-1为未选）
        private bool includeSharedFiles = true; // 是否包含框架共享文件（ToolsIntegrationPanel根目录下的非Moshi_文件）

        // 个人数据条目定义
        // Personal data entry definition
        [Serializable]
        private class PersonalDataEntry
        {
            public string fileName;
            public string description;
            public bool includeInExport;
            public string fullPath;
            public long fileSize;
            public bool exists;
        }

        // 个人数据列表（默认全部不导出）
        // Personal data list (default: all excluded)
        private List<PersonalDataEntry> personalDataList;

        // 文件预览
        // File preview
        private List<string> cachedFileList;
        private long cachedTotalSize;
        private int cachedEditorFileCount;
        private int cachedRuntimeFileCount;
        private int cachedPersonalIncludedCount;
        private bool fileListDirty = true;
        private Vector2 previewScrollPos;
        private bool showPreviewList;

        // GUI样式
        // GUI styles
        private GUIStyle sectionHeaderStyle;
        private GUIStyle personalWarningStyle;
        private GUIStyle personalDescStyle;
        private GUIStyle statsStyle;
        private GUIStyle exportButtonStyle;
        private bool stylesInitialized;

        [MenuItem("工具/Moshi/" + TOOL_NAME)]
        public static void ShowWindow()
        {
            var window = GetWindow<Moshi_Exporter>(TOOL_NAME);
            window.minSize = new Vector2(420, 500);
        }

        private void OnEnable()
        {
            InitPersonalDataList();
            ScanToolEntries();
            fileListDirty = true;
        }

        /// <summary>
        /// 初始化个人数据列表，扫描实际文件（优化版：只递归扫描一次目录）
        /// Initialize personal data list, scan actual files (optimized: single directory scan)
        /// </summary>
        private void InitPersonalDataList()
        {
            personalDataList = new List<PersonalDataEntry>
            {
                new PersonalDataEntry
                {
                    fileName = "MyToolsConfig.asset",
                    description = "工具面板排列与快捷访问配置",
                    includeInExport = false
                },
                new PersonalDataEntry
                {
                    fileName = "Moshi_FavoritesConfig.asset",
                    description = "资源收藏夹列表",
                    includeInExport = false
                },
                new PersonalDataEntry
                {
                    fileName = "Moshi_ImgClassifier_Config.json",
                    description = "图片分类器学习数据",
                    includeInExport = false
                },
            };

            // 一次性扫描目录，建立文件名 → 路径映射（避免每个 entry 都递归扫描一次）
            // Single directory scan, build filename → path lookup (avoids per-entry recursive scan)
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string searchDir = Path.Combine(projectRoot, EditorToolsPath.Replace("/", Path.DirectorySeparatorChar.ToString()));

            var fileNameToPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(searchDir))
            {
                // 只搜索目标文件名的扩展名集合，减少遍历量
                // Search by known extensions to reduce traversal
                foreach (var f in Directory.GetFiles(searchDir, "*.*", SearchOption.AllDirectories))
                {
                    string fn = Path.GetFileName(f);
                    if (!fileNameToPath.ContainsKey(fn))
                        fileNameToPath[fn] = f;
                }
            }

            foreach (var entry in personalDataList)
            {
                if (fileNameToPath.TryGetValue(entry.fileName, out string foundPath))
                {
                    entry.fullPath = foundPath;
                    entry.fileSize = new FileInfo(foundPath).Length;
                    entry.exists = true;
                }
                else
                {
                    entry.exists = false;
                    entry.fullPath = null;
                    entry.fileSize = 0;
                }
            }
        }

        /// <summary>
        /// 扫描所有工具条目，建立 Editor/Runtime 对应关系
        /// Scan all tool entries, establish Editor/Runtime mapping
        /// </summary>
        private void ScanToolEntries()
        {
            toolEntries = new List<ToolEntry>();
            string projectRoot = Path.GetDirectoryName(Application.dataPath);

            string editorResDir = Path.Combine(projectRoot, "Assets", "Editor", "ToolsIntegrationPanel", "AllToolsResources");
            string runtimeDir = Path.Combine(projectRoot, "Assets", "Scripts", "MoshiScripts");

            var toolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. 扫描 Editor/AllToolsResources 下的文件夹型工具
            // 1. Scan folder-based tools under Editor/AllToolsResources
            if (Directory.Exists(editorResDir))
            {
                foreach (var dir in Directory.GetDirectories(editorResDir))
                {
                    string dirName = Path.GetFileName(dir);
                    if (!dirName.StartsWith("Moshi_", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var entry = new ToolEntry
                    {
                        toolName = dirName,
                        editorPath = dir,
                        isFolder = true
                    };

                    // 查找对应 Runtime 文件夹
                    // Find corresponding Runtime folder
                    string matchingRuntime = Path.Combine(runtimeDir, dirName);
                    if (Directory.Exists(matchingRuntime))
                    {
                        entry.runtimePath = matchingRuntime;
                    }

                    CountToolFiles(entry);
                    toolEntries.Add(entry);
                    toolNames.Add(dirName);
                }

                // 2. 扫描 Editor/AllToolsResources 下的单文件工具（.cs）
                // 2. Scan single-file tools (.cs) under Editor/AllToolsResources
                foreach (var file in Directory.GetFiles(editorResDir, "Moshi_*.cs"))
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    if (toolNames.Contains(fileName))
                        continue; // 已经作为文件夹工具处理

                    var entry = new ToolEntry
                    {
                        toolName = fileName,
                        editorPath = file,
                        isFolder = false
                    };

                    // 查找对应 Runtime 文件（同名.cs 或 同名文件夹）
                    // Find corresponding Runtime file or folder
                    string runtimeFile = Path.Combine(runtimeDir, fileName + ".cs");
                    string runtimeFolder = Path.Combine(runtimeDir, fileName);
                    if (File.Exists(runtimeFile))
                    {
                        entry.runtimePath = runtimeFile;
                    }
                    else if (Directory.Exists(runtimeFolder))
                    {
                        entry.runtimePath = runtimeFolder;
                    }

                    CountToolFiles(entry);
                    toolEntries.Add(entry);
                    toolNames.Add(fileName);
                }
            }

            // 3. 扫描 Runtime 独有工具（Editor 中没有对应的）
            // 3. Scan Runtime-only tools (no Editor counterpart)
            if (Directory.Exists(runtimeDir))
            {
                // 文件夹型
                // Folder-based
                foreach (var dir in Directory.GetDirectories(runtimeDir))
                {
                    string dirName = Path.GetFileName(dir);
                    if (!dirName.StartsWith("Moshi_", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (toolNames.Contains(dirName))
                        continue;

                    var entry = new ToolEntry
                    {
                        toolName = dirName,
                        runtimePath = dir,
                        isFolder = true
                    };
                    CountToolFiles(entry);
                    toolEntries.Add(entry);
                    toolNames.Add(dirName);
                }

                // 单文件型
                // Single-file
                foreach (var file in Directory.GetFiles(runtimeDir, "Moshi_*.cs"))
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    if (toolNames.Contains(fileName))
                        continue;

                    var entry = new ToolEntry
                    {
                        toolName = fileName,
                        runtimePath = file,
                        isFolder = false
                    };
                    CountToolFiles(entry);
                    toolEntries.Add(entry);
                    toolNames.Add(fileName);
                }
            }

            // 按名称排序
            // Sort by name
            toolEntries.Sort((a, b) => string.Compare(a.toolName, b.toolName, StringComparison.OrdinalIgnoreCase));

            // 从 MyToolsConfig 获取中文显示名（与面板按钮名称一致）
            // Get display names from MyToolsConfig (matching panel button names)
            BuildDisplayNames();

            // 重置选中索引（如果之前有选中，尝试保持）
            // Reset selection index
            if (selectedToolIndex >= toolEntries.Count)
                selectedToolIndex = -1;
        }

        /// <summary>
        /// 从 ToolsConfig 构建中文显示名映射（优化版：无 AssetDatabase.FindAssets 调用）
        /// Build display name mapping from ToolsConfig (optimized: no FindAssets calls)
        /// </summary>
        private void BuildDisplayNames()
        {
            // 从 ToolsConfig（MyToolsConfig.asset）读取面板按钮上实际显示的中文名
            // Read actual button display names from MyToolsConfig.asset
            var config = AssetDatabase.LoadAssetAtPath<ToolsConfig>(ToolsConfig.DefaultAssetPath);

            // 构建 bindingKey → toolName（面板显示名）的映射
            // Build bindingKey → toolName (panel display name) lookup
            var displayLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (config != null && config.AllTools != null)
            {
                foreach (var tool in config.AllTools)
                {
                    if (tool != null && !string.IsNullOrEmpty(tool.bindingKey))
                    {
                        displayLookup[tool.bindingKey] = tool.toolName;
                    }
                }
            }

            // 一次性构建 declaringTypeName → (bindingKey, configDisplayName) 映射
            // Build declaringTypeName → display name mapping in one pass (no FindAssets)
            var definitions = ToolBindingCatalog.Definitions;
            var typeNameToDisplayName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in definitions)
            {
                if (def.ExecuteAction == null) continue;
                var declaringType = def.ExecuteAction.Method.DeclaringType;
                if (declaringType == null) continue;

                string typeName = declaringType.Name;
                if (typeNameToDisplayName.ContainsKey(typeName)) continue;

                if (displayLookup.TryGetValue(def.BindingKey, out string configName))
                {
                    typeNameToDisplayName[typeName] = configName;
                }
                else
                {
                    typeNameToDisplayName[typeName] = def.ToolName;
                }
            }

            // 给每个 ToolEntry 匹配中文显示名（通过类型名直接匹配，无需查找脚本路径）
            // Match display names by type name (no script path lookup needed)
            foreach (var entry in toolEntries)
            {
                entry.displayName = null;

                // 策略1：工具名本身就是类名（如 Moshi_ClipHelper）
                // Strategy 1: tool name is the class name itself
                if (typeNameToDisplayName.TryGetValue(entry.toolName, out string displayName))
                {
                    entry.displayName = displayName;
                }

                // 如果没匹配到，使用工具名去掉 Moshi_ 前缀作为备选
                // Fallback: strip Moshi_ prefix
                if (string.IsNullOrEmpty(entry.displayName))
                {
                    entry.displayName = entry.toolName.StartsWith("Moshi_")
                        ? entry.toolName.Substring(6)
                        : entry.toolName;
                }
            }
        }

        /// <summary>
        /// 统计工具的文件数和大小（优化版：减少异常处理开销）
        /// Count tool files and size (optimized: reduced exception overhead)
        /// </summary>
        private void CountToolFiles(ToolEntry entry)
        {
            entry.editorFileCount = 0;
            entry.runtimeFileCount = 0;
            entry.totalSize = 0;

            // Editor 部分
            // Editor part
            if (!string.IsNullOrEmpty(entry.editorPath))
            {
                if (entry.isFolder && Directory.Exists(entry.editorPath))
                {
                    CountFilesInDirectory(entry.editorPath, ref entry.editorFileCount, ref entry.totalSize);
                }
                else if (File.Exists(entry.editorPath))
                {
                    entry.editorFileCount = 1;
                    entry.totalSize += GetFileSizeSafe(entry.editorPath);
                    // 加上对应的 Config 文件
                    // Include associated config files
                    string dir = Path.GetDirectoryName(entry.editorPath);
                    string baseName = Path.GetFileNameWithoutExtension(entry.editorPath);
                    foreach (var f in Directory.GetFiles(dir, baseName + "Config*"))
                    {
                        if (!f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                            entry.editorFileCount++;
                        entry.totalSize += GetFileSizeSafe(f);
                    }
                }
            }

            // Runtime 部分
            // Runtime part
            if (!string.IsNullOrEmpty(entry.runtimePath))
            {
                if (Directory.Exists(entry.runtimePath))
                {
                    CountFilesInDirectory(entry.runtimePath, ref entry.runtimeFileCount, ref entry.totalSize);
                }
                else if (File.Exists(entry.runtimePath))
                {
                    entry.runtimeFileCount = 1;
                    entry.totalSize += GetFileSizeSafe(entry.runtimePath);
                }
            }
        }

        /// <summary>
        /// 统计目录下的文件数和总大小
        /// Count files and total size in a directory
        /// </summary>
        private static void CountFilesInDirectory(string dirPath, ref int fileCount, ref long totalSize)
        {
            try
            {
                var files = Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    if (!files[i].EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        fileCount++;
                    totalSize += new FileInfo(files[i]).Length;
                }
            }
            catch { /* 忽略无法访问的目录 */ }
        }

        /// <summary>
        /// 安全获取文件大小
        /// Get file size safely
        /// </summary>
        private static long GetFileSizeSafe(string filePath)
        {
            try { return new FileInfo(filePath).Length; }
            catch { return 0; }
        }
        /// </summary>
        private bool IsExcludedPersonalData(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            foreach (var entry in personalDataList)
            {
                if (!entry.exists) continue;
                if (!entry.includeInExport)
                {
                    // 排除该文件及其 .meta
                    // Exclude the file and its .meta
                    if (string.Equals(fileName, entry.fileName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(fileName, entry.fileName + ".meta", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 判断是否应该包含该文件
        /// Determine if file should be included
        /// </summary>
        private bool ShouldIncludeFile(string filePath, ExportFormat format)
        {
            string fileName = Path.GetFileName(filePath);

            // 1. 排除未勾选的个人数据
            // 1. Exclude unchecked personal data
            if (IsExcludedPersonalData(filePath))
                return false;

            // 2. 根据开关决定是否排除 .meta 文件
            // 2. Exclude .meta files based on toggle
            if (!includeMetaFiles && fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        /// <summary>
        /// 收集所有待导出文件
        /// Collect all files to export
        /// </summary>
        private List<string> CollectFiles(ExportFormat format, out int editorCount, out int runtimeCount, out int personalCount)
        {
            if (exportScope == ExportScope.Individual)
                return CollectFilesIndividual(format, out editorCount, out runtimeCount, out personalCount);

            return CollectFilesAll(format, out editorCount, out runtimeCount, out personalCount);
        }

        /// <summary>
        /// 全部导出模式 - 收集所有文件
        /// All export mode - collect all files
        /// </summary>
        private List<string> CollectFilesAll(ExportFormat format, out int editorCount, out int runtimeCount, out int personalCount)
        {
            var files = new List<string>();
            editorCount = 0;
            runtimeCount = 0;
            personalCount = 0;

            string projectRoot = Path.GetDirectoryName(Application.dataPath);

            // 收集编辑器工具文件
            // Collect editor tool files
            if (includeEditorTools)
            {
                string editorDir = Path.Combine(projectRoot, EditorToolsPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (Directory.Exists(editorDir))
                {
                    var editorFiles = Directory.GetFiles(editorDir, "*.*", SearchOption.AllDirectories);
                    foreach (var f in editorFiles)
                    {
                        if (ShouldIncludeFile(f, format))
                        {
                            files.Add(f);
                            editorCount++;
                        }
                    }
                }
            }

            // 收集运行时脚本
            // Collect runtime scripts
            if (includeRuntimeScripts)
            {
                string runtimeDir = Path.Combine(projectRoot, RuntimeScriptsPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (Directory.Exists(runtimeDir))
                {
                    var runtimeFiles = Directory.GetFiles(runtimeDir, "*.*", SearchOption.AllDirectories);
                    foreach (var f in runtimeFiles)
                    {
                        if (ShouldIncludeFile(f, format))
                        {
                            files.Add(f);
                            runtimeCount++;
                        }
                    }
                }
            }

            // 统计已勾选的个人数据
            // Count checked personal data
            personalCount = personalDataList.Count(p => p.exists && p.includeInExport);

            return files;
        }

        /// <summary>
        /// 单独导出模式 - 只收集选中工具的文件
        /// Individual export mode - collect only the selected tool's files
        /// </summary>
        private List<string> CollectFilesIndividual(ExportFormat format, out int editorCount, out int runtimeCount, out int personalCount)
        {
            var files = new List<string>();
            editorCount = 0;
            runtimeCount = 0;
            personalCount = 0;

            if (toolEntries == null || selectedToolIndex < 0 || selectedToolIndex >= toolEntries.Count)
                return files;

            var tool = toolEntries[selectedToolIndex];
            string projectRoot = Path.GetDirectoryName(Application.dataPath);

            // 收集 Editor 文件
            // Collect editor files
            if (!string.IsNullOrEmpty(tool.editorPath))
            {
                var editorFiles = GetToolFiles(tool.editorPath, tool.isFolder, tool.toolName);
                foreach (var f in editorFiles)
                {
                    if (ShouldIncludeFile(f, format))
                    {
                        files.Add(f);
                        editorCount++;
                    }
                }
            }

            // 收集 Runtime 文件
            // Collect runtime files
            if (!string.IsNullOrEmpty(tool.runtimePath))
            {
                var runtimeFiles = GetToolFiles(tool.runtimePath, Directory.Exists(tool.runtimePath), tool.toolName);
                foreach (var f in runtimeFiles)
                {
                    if (ShouldIncludeFile(f, format))
                    {
                        files.Add(f);
                        runtimeCount++;
                    }
                }
            }

            // 如果勾选了包含框架共享文件，添加 ToolsIntegrationPanel 根目录下的非 Moshi_ 工具文件
            // Include shared framework files from ToolsIntegrationPanel root
            if (includeSharedFiles)
            {
                string panelRoot = Path.Combine(projectRoot, EditorToolsPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (Directory.Exists(panelRoot))
                {
                    // 只收集根目录文件（不递归，排除 AllToolsResources 和 子目录）
                    // Only collect root files (non-recursive)
                    foreach (var f in Directory.GetFiles(panelRoot))
                    {
                        if (ShouldIncludeFile(f, format))
                        {
                            files.Add(f);
                            editorCount++;
                        }
                    }

                    // 包含 Documentation 目录
                    // Include Documentation directory
                    string docDir = Path.Combine(panelRoot, "Documentation");
                    if (Directory.Exists(docDir))
                    {
                        foreach (var f in Directory.GetFiles(docDir, "*.*", SearchOption.AllDirectories))
                        {
                            if (ShouldIncludeFile(f, format))
                            {
                                files.Add(f);
                                editorCount++;
                            }
                        }
                    }

                    // 包含 DesignDocs 目录
                    // Include DesignDocs directory
                    string designDir = Path.Combine(panelRoot, "DesignDocs");
                    if (Directory.Exists(designDir))
                    {
                        foreach (var f in Directory.GetFiles(designDir, "*.*", SearchOption.AllDirectories))
                        {
                            if (ShouldIncludeFile(f, format))
                            {
                                files.Add(f);
                                editorCount++;
                            }
                        }
                    }
                }
            }

            personalCount = 0;
            return files;
        }

        /// <summary>
        /// 获取工具的所有文件（支持文件夹和单文件）
        /// Get all files for a tool (supports folder and single file)
        /// </summary>
        private List<string> GetToolFiles(string path, bool isFolder, string toolName)
        {
            var result = new List<string>();

            if (isFolder && Directory.Exists(path))
            {
                result.AddRange(Directory.GetFiles(path, "*.*", SearchOption.AllDirectories));
            }
            else if (File.Exists(path))
            {
                result.Add(path);
                // 添加 .meta
                // Add .meta
                string metaPath = path + ".meta";
                if (File.Exists(metaPath))
                    result.Add(metaPath);

                // 同目录下关联的 Config 文件
                // Associated Config files in same directory
                string dir = Path.GetDirectoryName(path);
                string baseName = Path.GetFileNameWithoutExtension(path);
                foreach (var f in Directory.GetFiles(dir, baseName + "Config*"))
                {
                    if (!result.Contains(f))
                        result.Add(f);
                }

                // 同目录下同名的 .asset 文件（配置数据）
                // Same-name .asset files in same directory (config data)
                foreach (var f in Directory.GetFiles(dir, baseName + "*.asset"))
                {
                    if (!result.Contains(f))
                        result.Add(f);
                }
            }

            return result;
        }

        /// <summary>
        /// 刷新文件列表缓存（优化版：批量获取文件信息）
        /// Refresh file list cache (optimized: batch file info retrieval)
        /// </summary>
        private void RefreshFileListCache()
        {
            cachedFileList = CollectFiles(exportFormat, out cachedEditorFileCount, out cachedRuntimeFileCount, out cachedPersonalIncludedCount);
            cachedTotalSize = 0;

            // 批量统计大小，用 try-catch 包裹外层减少异常处理开销
            // Batch size calculation with outer try-catch for reduced exception overhead
            try
            {
                for (int i = 0; i < cachedFileList.Count; i++)
                {
                    var fi = new FileInfo(cachedFileList[i]);
                    if (fi.Exists)
                        cachedTotalSize += fi.Length;
                }
            }
            catch { /* 忽略无法访问的文件 */ }

            fileListDirty = false;
        }

        private void OnGUI()
        {
            InitStyles();

            if (fileListDirty)
                RefreshFileListCache();

            EditorGUILayout.BeginVertical();

            // 标题栏
            // Title bar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(TOOL_NAME, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            MoshiHelpButton.DrawHelpButton(TOOL_NAME);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // 导出格式选择
            // Export format selection
            DrawFormatSection();

            EditorGUILayout.Space(4);

            // 导出范围选择
            // Export scope selection
            DrawScopeSection();

            EditorGUILayout.Space(4);

            if (exportScope == ExportScope.All)
            {
                // 全部导出模式
                // All export mode

                // 导出内容选择
                // Export content selection
                DrawContentSection();

                EditorGUILayout.Space(4);

                // 个人数据勾选区
                // Personal data toggle section
                DrawPersonalDataSection();
            }
            else
            {
                // 单独导出模式
                // Individual export mode
                DrawToolSelectionSection();
            }

            EditorGUILayout.Space(4);

            // .meta 开关（两种模式共用）
            // .meta toggle (shared by both modes)
            DrawMetaToggle();

            EditorGUILayout.Space(4);

            // 文件预览
            // File preview
            DrawPreviewSection();

            GUILayout.FlexibleSpace();

            // 导出按钮
            // Export button
            DrawExportButton();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制导出格式选择区
        /// Draw export format selection section
        /// </summary>
        private void DrawFormatSection()
        {
            EditorGUILayout.LabelField("导出格式", sectionHeaderStyle);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("格式", GUILayout.Width(60));

            ExportFormat prevFormat = exportFormat;

            if (GUILayout.Toggle(exportFormat == ExportFormat.Zip, "ZIP", EditorStyles.miniButtonLeft))
                exportFormat = ExportFormat.Zip;
            if (GUILayout.Toggle(exportFormat == ExportFormat.UnityPackage, "UnityPackage", EditorStyles.miniButtonRight))
                exportFormat = ExportFormat.UnityPackage;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 格式说明
            // Format description
            if (exportFormat == ExportFormat.Zip)
            {
                EditorGUILayout.HelpBox("ZIP 格式：通用压缩格式，适合代码分享与备份。\n导入方式：解压后将 Assets/ 内容合并到目标项目。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("UnityPackage 格式：Unity标准格式，建议勾选 .meta 以保持GUID引用。\n导入方式：Assets → Import Package → Custom Package", MessageType.Info);
            }

            if (prevFormat != exportFormat)
                fileListDirty = true;
        }

        /// <summary>
        /// 绘制导出范围选择区
        /// Draw export scope selection section
        /// </summary>
        private void DrawScopeSection()
        {
            EditorGUILayout.LabelField("导出范围", sectionHeaderStyle);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("范围", GUILayout.Width(60));

            ExportScope prevScope = exportScope;

            if (GUILayout.Toggle(exportScope == ExportScope.All, "全部导出", EditorStyles.miniButtonLeft))
                exportScope = ExportScope.All;
            if (GUILayout.Toggle(exportScope == ExportScope.Individual, "单独导出", EditorStyles.miniButtonRight))
                exportScope = ExportScope.Individual;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (prevScope != exportScope)
                fileListDirty = true;
        }

        /// <summary>
        /// 绘制导出内容选择区（全部模式）
        /// Draw export content selection section (All mode)
        /// </summary>
        private void DrawContentSection()
        {
            EditorGUILayout.LabelField("导出内容", sectionHeaderStyle);

            bool prevEditor = includeEditorTools;
            bool prevRuntime = includeRuntimeScripts;

            includeEditorTools = EditorGUILayout.ToggleLeft(
                $"  编辑器工具 ({EditorToolsPath}/)", includeEditorTools);
            includeRuntimeScripts = EditorGUILayout.ToggleLeft(
                $"  运行时脚本 ({RuntimeScriptsPath}/)", includeRuntimeScripts);

            if (prevEditor != includeEditorTools || prevRuntime != includeRuntimeScripts)
                fileListDirty = true;
        }

        /// <summary>
        /// 绘制 .meta 文件开关（两种模式共用）
        /// Draw .meta files toggle (shared by both modes)
        /// </summary>
        private void DrawMetaToggle()
        {
            bool prevMeta = includeMetaFiles;

            EditorGUILayout.BeginHorizontal();
            includeMetaFiles = EditorGUILayout.ToggleLeft(
                "  包含 .meta 文件（保留GUID引用，便于Unity导入）", includeMetaFiles);
            EditorGUILayout.EndHorizontal();

            if (prevMeta != includeMetaFiles)
                fileListDirty = true;
        }

        /// <summary>
        /// 绘制工具选择区（单独导出模式 - 单选Popup）
        /// Draw tool selection section (Individual mode - single select Popup)
        /// </summary>
        private void DrawToolSelectionSection()
        {
            EditorGUILayout.LabelField("选择工具", sectionHeaderStyle);

            if (toolEntries == null || toolEntries.Count == 0)
            {
                EditorGUILayout.HelpBox("未扫描到任何 Moshi 工具。", MessageType.Warning);
                if (GUILayout.Button("刷新扫描", EditorStyles.miniButton, GUILayout.Width(70)))
                {
                    ScanToolEntries();
                    fileListDirty = true;
                }
                return;
            }

            // Popup 选择工具
            // Popup tool selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("工具", GUILayout.Width(60));

            // 构建 Popup 显示名称列表（+1 因为第0项是"请选择..."）
            // 格式：中文名 (英文名) [E] [R]，与面板按钮对应
            // Build popup display name array (+1 for placeholder at index 0)
            // Format: DisplayName (ToolName) [E] [R], matching panel buttons
            var displayNames = new string[toolEntries.Count + 1];
            displayNames[0] = "— 请选择工具 —";
            for (int i = 0; i < toolEntries.Count; i++)
            {
                string tags = "";
                if (!string.IsNullOrEmpty(toolEntries[i].editorPath))
                    tags += " [E]";
                if (!string.IsNullOrEmpty(toolEntries[i].runtimePath))
                    tags += " [R]";

                // 显示格式：中文名 (英文名) [E][R]
                // Display format: Chinese name (English name) [E][R]
                string dn = toolEntries[i].displayName;
                string tn = toolEntries[i].toolName;
                displayNames[i + 1] = $"{dn}  ({tn}){tags}";
            }

            int popupIndex = selectedToolIndex + 1; // Popup 索引偏移 +1
            EditorGUI.BeginChangeCheck();
            popupIndex = EditorGUILayout.Popup(popupIndex, displayNames);
            if (EditorGUI.EndChangeCheck())
            {
                selectedToolIndex = popupIndex - 1;
                fileListDirty = true;
            }

            if (GUILayout.Button("刷新", EditorStyles.miniButton, GUILayout.Width(40)))
            {
                ScanToolEntries();
                fileListDirty = true;
            }

            EditorGUILayout.EndHorizontal();

            // 显示选中工具详情
            // Show selected tool details
            if (selectedToolIndex >= 0 && selectedToolIndex < toolEntries.Count)
            {
                var tool = toolEntries[selectedToolIndex];

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // 工具名称（中文名 + 英文名）
                // Tool name (display name + tool name)
                EditorGUILayout.LabelField($"{tool.displayName}  ({tool.toolName})", EditorStyles.boldLabel);

                // Editor 路径
                // Editor path
                if (!string.IsNullOrEmpty(tool.editorPath))
                {
                    string editorRelative = tool.editorPath;
                    string projectRoot = Path.GetDirectoryName(Application.dataPath);
                    if (editorRelative.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                        editorRelative = editorRelative.Substring(projectRoot.Length + 1);

                    EditorGUILayout.LabelField($"  📁 Editor:  {editorRelative.Replace("\\", "/")}", statsStyle);
                    EditorGUILayout.LabelField($"       文件数: {tool.editorFileCount}", EditorStyles.miniLabel);
                }

                // Runtime 路径
                // Runtime path
                if (!string.IsNullOrEmpty(tool.runtimePath))
                {
                    string runtimeRelative = tool.runtimePath;
                    string projectRoot = Path.GetDirectoryName(Application.dataPath);
                    if (runtimeRelative.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                        runtimeRelative = runtimeRelative.Substring(projectRoot.Length + 1);

                    EditorGUILayout.LabelField($"  📁 Runtime: {runtimeRelative.Replace("\\", "/")}", statsStyle);
                    EditorGUILayout.LabelField($"       文件数: {tool.runtimeFileCount}", EditorStyles.miniLabel);
                }

                // 仅 Runtime（无 Editor 部分）
                // Runtime-only
                if (string.IsNullOrEmpty(tool.editorPath) && !string.IsNullOrEmpty(tool.runtimePath))
                {
                    EditorGUILayout.LabelField("  ⚠ 仅 Runtime 脚本（无 Editor 部分）", EditorStyles.miniLabel);
                }

                // 总大小
                // Total size
                EditorGUILayout.LabelField($"  总大小: {FormatFileSize(tool.totalSize)}", EditorStyles.miniLabel);

                EditorGUILayout.EndVertical();
            }

            // 包含框架共享文件开关
            // Include shared framework files toggle
            EditorGUILayout.Space(2);
            bool prevShared = includeSharedFiles;
            includeSharedFiles = EditorGUILayout.ToggleLeft(
                "  包含框架共享文件（面板脚本、文档等）", includeSharedFiles);
            if (prevShared != includeSharedFiles)
                fileListDirty = true;
        }

        /// <summary>
        /// 绘制个人数据勾选区
        /// Draw personal data toggle section
        /// </summary>
        private void DrawPersonalDataSection()
        {
            int detectedCount = personalDataList.Count(p => p.exists);
            EditorGUILayout.LabelField($"个人数据 (检测到 {detectedCount} 项)", sectionHeaderStyle);

            if (detectedCount == 0)
            {
                EditorGUILayout.LabelField("  未检测到个人数据文件", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (var entry in personalDataList)
            {
                if (!entry.exists) continue;

                EditorGUILayout.BeginHorizontal();

                // 勾选框
                // Toggle
                bool prevValue = entry.includeInExport;
                entry.includeInExport = EditorGUILayout.Toggle(entry.includeInExport, GUILayout.Width(16));

                // 警告图标 + 文件名 + 大小
                // Warning icon + filename + size
                string sizeText = FormatFileSize(entry.fileSize);
                EditorGUILayout.LabelField(
                    new GUIContent($"⚠ {entry.fileName}  ({sizeText})", "个人数据，勾选后一同导出"),
                    personalWarningStyle);

                EditorGUILayout.EndHorizontal();

                // 说明文字（缩进）
                // Description (indented)
                EditorGUI.indentLevel += 2;
                EditorGUILayout.LabelField(entry.description, personalDescStyle);
                EditorGUI.indentLevel -= 2;

                if (prevValue != entry.includeInExport)
                    fileListDirty = true;
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制文件预览区
        /// Draw file preview section
        /// </summary>
        private void DrawPreviewSection()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("文件预览", sectionHeaderStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("刷新", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                fileListDirty = true;
                RefreshFileListCache();
            }
            EditorGUILayout.EndHorizontal();

            // 统计信息
            // Statistics
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (exportScope == ExportScope.All)
            {
                if (includeEditorTools)
                    EditorGUILayout.LabelField($"  📁 Editor/ToolsIntegrationPanel/  ({cachedEditorFileCount} 文件)", statsStyle);
                if (includeRuntimeScripts)
                    EditorGUILayout.LabelField($"  📁 Scripts/MoshiScripts/  ({cachedRuntimeFileCount} 文件)", statsStyle);
            }
            else
            {
                // 单独导出模式显示选中工具信息
                // Individual mode: show selected tool info
                if (selectedToolIndex >= 0 && selectedToolIndex < toolEntries.Count)
                {
                    var selectedTool = toolEntries[selectedToolIndex];
                    EditorGUILayout.LabelField($"  🔧 {selectedTool.displayName}  ({selectedTool.toolName})", statsStyle);
                    if (cachedEditorFileCount > 0)
                        EditorGUILayout.LabelField($"     Editor: {cachedEditorFileCount} 文件", EditorStyles.miniLabel);
                    if (cachedRuntimeFileCount > 0)
                        EditorGUILayout.LabelField($"     Runtime: {cachedRuntimeFileCount} 文件", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("  未选择工具", EditorStyles.miniLabel);
                }
            }

            int personalChecked = personalDataList.Count(p => p.exists && p.includeInExport);
            int personalTotal = personalDataList.Count(p => p.exists);
            if (personalTotal > 0)
            {
                string personalInfo = personalChecked > 0
                    ? $"  ⚠ 个人数据: {personalChecked} / {personalTotal} 项已勾选"
                    : $"  ✅ 个人数据: 全部排除 ({personalTotal} 项)";
                EditorGUILayout.LabelField(personalInfo, statsStyle);
            }

            string metaInfo = includeMetaFiles ? " (含 .meta)" : " (不含 .meta)";
            EditorGUILayout.LabelField($"  总计: {cachedFileList?.Count ?? 0} 个文件{metaInfo}, 约 {FormatFileSize(cachedTotalSize)}", statsStyle);

            EditorGUILayout.EndVertical();

            // 文件列表展开
            // File list expand
            showPreviewList = EditorGUILayout.Foldout(showPreviewList, "查看文件列表", true);
            if (showPreviewList && cachedFileList != null)
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                previewScrollPos = EditorGUILayout.BeginScrollView(previewScrollPos, GUILayout.MaxHeight(200));
                foreach (var f in cachedFileList)
                {
                    string relativePath = f;
                    if (f.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        relativePath = f.Substring(projectRoot.Length + 1);
                    }
                    EditorGUILayout.LabelField(relativePath.Replace("\\", "/"), EditorStyles.miniLabel);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>
        /// 绘制导出按钮
        /// Draw export button
        /// </summary>
        private void DrawExportButton()
        {
            EditorGUILayout.Space(8);
            bool canExport;
            if (exportScope == ExportScope.All)
            {
                canExport = (includeEditorTools || includeRuntimeScripts) && cachedFileList != null && cachedFileList.Count > 0;
            }
            else
            {
                canExport = toolEntries != null && selectedToolIndex >= 0 && selectedToolIndex < toolEntries.Count
                    && cachedFileList != null && cachedFileList.Count > 0;
            }

            EditorGUI.BeginDisabledGroup(!canExport);
            if (GUILayout.Button("导出打包", exportButtonStyle, GUILayout.Height(40)))
            {
                DoExport();
            }
            EditorGUI.EndDisabledGroup();

            if (!canExport)
            {
                string hint = exportScope == ExportScope.All
                    ? "请至少勾选一项导出内容。"
                    : "请从下拉框选择一个工具。";
                EditorGUILayout.HelpBox(hint, MessageType.Warning);
            }

            EditorGUILayout.Space(6);
        }

        /// <summary>
        /// 执行导出
        /// Execute export
        /// </summary>
        private void DoExport()
        {
            // 刷新文件列表
            // Refresh file list
            RefreshFileListCache();

            if (cachedFileList == null || cachedFileList.Count == 0)
            {
                EditorUtility.DisplayDialog("导出失败", "没有可导出的文件。", "确定");
                return;
            }

            // 个人数据二次确认
            // Personal data confirmation
            int personalChecked = personalDataList.Count(p => p.exists && p.includeInExport);
            if (personalChecked > 0)
            {
                string personalNames = string.Join("\n", personalDataList
                    .Where(p => p.exists && p.includeInExport)
                    .Select(p => $"  • {p.fileName} ({FormatFileSize(p.fileSize)})"));

                bool confirm = EditorUtility.DisplayDialog(
                    "确认导出",
                    $"本次导出包含 {personalChecked} 项个人数据：\n\n{personalNames}\n\n确认继续？",
                    "确认导出", "取消");

                if (!confirm) return;
            }

            // UnityPackage 不含 .meta 时提醒
            // Warn when UnityPackage export without .meta
            if (exportFormat == ExportFormat.UnityPackage && !includeMetaFiles)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "提示",
                    "当前未勾选「包含 .meta 文件」。\n\nUnityPackage 不含 .meta 会导致导入后 GUID 重新生成，可能丢失引用关系。\n\n是否继续？",
                    "继续导出", "返回勾选");

                if (!proceed) return;
            }

            // 选择保存路径
            // Choose save path
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            // 生成默认文件名
            // Generate default filename
            string baseName;
            if (exportScope == ExportScope.Individual && selectedToolIndex >= 0 && selectedToolIndex < toolEntries.Count)
            {
                baseName = $"{toolEntries[selectedToolIndex].toolName}_{dateStr}";
            }
            else
            {
                baseName = $"MoshiTools_{dateStr}";
            }

            if (exportFormat == ExportFormat.Zip)
            {
                string defaultName = $"{baseName}.zip";
                string savePath = EditorUtility.SaveFilePanel("导出 ZIP", desktopPath, defaultName, "zip");
                if (string.IsNullOrEmpty(savePath)) return;

                ExportAsZip(savePath);
            }
            else
            {
                string defaultName = $"{baseName}.unitypackage";
                string savePath = EditorUtility.SaveFilePanel("导出 UnityPackage", desktopPath, defaultName, "unitypackage");
                if (string.IsNullOrEmpty(savePath)) return;

                ExportAsUnityPackage(savePath);
            }
        }

        /// <summary>
        /// 导出为 ZIP 格式
        /// Export as ZIP format
        /// </summary>
        private void ExportAsZip(string outputPath)
        {
            try
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);

                // 如果文件已存在则删除
                // Delete if exists
                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                using (var fs = new FileStream(outputPath, FileMode.Create))
                using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    int totalFiles = cachedFileList.Count;
                    for (int i = 0; i < totalFiles; i++)
                    {
                        string filePath = cachedFileList[i];

                        // 计算相对路径作为 ZIP 内路径
                        // Calculate relative path as ZIP entry path
                        string relativePath = filePath;
                        if (filePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                        {
                            relativePath = filePath.Substring(projectRoot.Length + 1);
                        }
                        // 统一使用正斜杠
                        // Use forward slashes
                        relativePath = relativePath.Replace("\\", "/");

                        EditorUtility.DisplayProgressBar("导出 ZIP",
                            $"正在压缩... ({i + 1}/{totalFiles})\n{Path.GetFileName(filePath)}",
                            (float)i / totalFiles);

                        // 手动创建条目并写入文件内容（避免依赖 ZipFile 扩展类）
                        // Manually create entry and write file bytes (avoids ZipFile extension dependency)
                        var entry = archive.CreateEntry(relativePath, System.IO.Compression.CompressionLevel.Optimal);
                        using (var entryStream = entry.Open())
                        using (var fileStream = File.OpenRead(filePath))
                        {
                            fileStream.CopyTo(entryStream);
                        }
                    }
                }

                EditorUtility.ClearProgressBar();

                long zipSize = new FileInfo(outputPath).Length;
                bool openFolder = EditorUtility.DisplayDialog(
                    "导出成功",
                    $"已导出 {cachedFileList.Count} 个文件到：\n\n{outputPath}\n\n" +
                    $"压缩包大小: {FormatFileSize(zipSize)}\n原始大小: {FormatFileSize(cachedTotalSize)}",
                    "打开目录", "确定");

                if (openFolder)
                {
                    EditorUtility.RevealInFinder(outputPath);
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("导出失败", $"ZIP 导出时发生错误：\n\n{e.Message}", "确定");
                Debug.LogError($"[Moshi_Exporter] ZIP导出失败: {e}");
            }
        }

        /// <summary>
        /// 导出为 UnityPackage 格式
        /// Export as UnityPackage format
        /// </summary>
        private void ExportAsUnityPackage(string outputPath)
        {
            try
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);

                // 收集 Assets 相对路径
                // Collect asset relative paths
                var assetPaths = new List<string>();
                foreach (var filePath in cachedFileList)
                {
                    string relativePath = filePath;
                    if (filePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        relativePath = filePath.Substring(projectRoot.Length + 1);
                    }
                    relativePath = relativePath.Replace("\\", "/");

                    // UnityPackage 需要 Assets/ 开头的路径
                    // UnityPackage requires Assets/ prefix
                    if (relativePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    {
                        assetPaths.Add(relativePath);
                    }
                }

                if (assetPaths.Count == 0)
                {
                    EditorUtility.DisplayDialog("导出失败", "没有找到有效的 Assets 路径。", "确定");
                    return;
                }

                EditorUtility.DisplayProgressBar("导出 UnityPackage", "正在打包...", 0.5f);

                AssetDatabase.ExportPackage(
                    assetPaths.ToArray(),
                    outputPath,
                    ExportPackageOptions.Default);

                EditorUtility.ClearProgressBar();

                long pkgSize = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;
                bool openFolder = EditorUtility.DisplayDialog(
                    "导出成功",
                    $"已导出 {assetPaths.Count} 个资源到：\n\n{outputPath}\n\n" +
                    $"包体大小: {FormatFileSize(pkgSize)}",
                    "打开目录", "确定");

                if (openFolder)
                {
                    EditorUtility.RevealInFinder(outputPath);
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("导出失败", $"UnityPackage 导出时发生错误：\n\n{e.Message}", "确定");
                Debug.LogError($"[Moshi_Exporter] UnityPackage导出失败: {e}");
            }
        }

        /// <summary>
        /// 格式化文件大小显示
        /// Format file size for display
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024f:F1} KB";
            if (bytes < 1024L * 1024 * 1024)
                return $"{bytes / (1024f * 1024f):F1} MB";
            return $"{bytes / (1024f * 1024f * 1024f):F2} GB";
        }

        /// <summary>
        /// 初始化 GUI 样式
        /// Initialize GUI styles
        /// </summary>
        private void InitStyles()
        {
            if (stylesInitialized) return;

            sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(4, 4, 6, 2)
            };

            personalWarningStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(1f, 0.7f, 0.3f) }
            };

            personalDescStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            statsStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                richText = true
            };

            exportButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                fixedHeight = 40
            };

            stylesInitialized = true;
        }
    }
}
#endif
