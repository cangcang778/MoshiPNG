#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;

namespace MoshiTools
{
    /// <summary>
    /// 图片分类归档工具 - 重复查清模块（partial）
    /// Image Classification Tool - Duplicate Clean Module (partial)
    /// </summary>
    public partial class Moshi_ImgClassifier
    {
        #region 重复查清 - 数据类

        /// <summary>
        /// 查重模式
        /// Duplicate scan mode
        /// </summary>
        private enum DupScanMode
        {
            MD5,        // 精确查重（MD5哈希）
            Visual      // 视觉查重（画面指纹）
        }

        /// <summary>
        /// 重复文件信息
        /// Duplicate file info
        /// </summary>
        private class DupFileInfo
        {
            public string filePath;             // 完整路径（绝对路径）
            public string assetPath;            // 项目内相对路径（项目外为null）
            public string fileName;             // 文件名
            public long fileSize;               // 文件大小（字节）
            public int referenceCount;          // 引用次数（项目内才有效）
            public bool isProjectFile;          // 是否在项目内
            public bool isSelected;             // 是否被选为保留项
            public ImageFingerprint fingerprint; // 画面指纹（视觉查重模式用）
            public Texture2D thumbnail;         // 缩略图缓存
        }

        /// <summary>
        /// 重复文件组
        /// Duplicate file group
        /// </summary>
        private class DupGroup
        {
            public string groupKey;                     // 分组键（MD5哈希值 或 首个文件路径）
            public List<DupFileInfo> files = new List<DupFileInfo>();  // 组内文件列表
            public string selectedFilePath;             // 保留项的路径
            public bool isFoldout = true;               // 是否展开
            public float similarity;                    // 相似度（视觉查重模式）

            /// <summary>
            /// 是否包含项目内文件
            /// </summary>
            public bool HasProjectFile => files.Any(f => f.isProjectFile);
        }

        #endregion

        #region 重复查清 - 变量

        // 扫描设置
        // Scan settings
        private string dupScanFolder = "";
        private bool dupIncludeSubfolders = true;
        private DupScanMode dupScanMode = DupScanMode.MD5;
        private float dupVisualThreshold = 0.95f;

        // 引用范围文件夹（用于统计引用次数和统一引用）
        // Reference scope folder (for counting references and unifying references)
        private string dupRefScopeFolder = "";

        // 扫描结果
        // Scan results
        private List<DupGroup> dupGroups = new List<DupGroup>();
        private int dupGroupCount = 0;
        private int dupCleanableCount = 0;
        private long dupCleanableSize = 0;

        // UI状态
        // UI state
        private Vector2 dupScrollPosition;
        private bool dupIsScanning = false;

        #endregion

        #region 重复查清 - GUI绘制

        /// <summary>
        /// 绘制重复查清标签页
        /// Draw duplicate clean tab
        /// </summary>
        private void DrawDuplicateCleanTab()
        {
            EditorGUILayout.LabelField("精确哈希+视觉指纹双模式查重，支持项目内外文件", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            // 扫描设置区域
            // Scan settings area
            DrawDupScanSettings();

            EditorGUILayout.Space(5);

            // 操作按钮区域
            // Action buttons area
            DrawDupActionButtons();

            EditorGUILayout.Space(5);

            // 统计信息
            // Statistics
            if (dupGroupCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"发现 {dupGroupCount} 组重复，共 {dupCleanableCount} 张可清理，预计释放 {DupFormatFileSize(dupCleanableSize)}",
                    MessageType.Info);
            }

            // 结果列表
            // Result list
            DrawDupResults();
        }

        /// <summary>
        /// 绘制扫描设置
        /// Draw scan settings
        /// </summary>
        private void DrawDupScanSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("扫描设置", headerStyle);

            // 目标文件夹
            // Target folder
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("扫描目录", GUILayout.Width(60));
            dupScanFolder = EditorGUILayout.TextField(dupScanFolder);
            if (GUILayout.Button("浏览", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("选择扫描文件夹", 
                    string.IsNullOrEmpty(dupScanFolder) ? "Assets" : dupScanFolder, "");
                if (!string.IsNullOrEmpty(path))
                {
                    string dataPath = Application.dataPath;
                    if (path.StartsWith(dataPath))
                        dupScanFolder = "Assets" + path.Substring(dataPath.Length);
                    else
                        dupScanFolder = path;
                }
            }
            EditorGUILayout.EndHorizontal();

            // 引用范围文件夹
            // Reference scope folder
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("引用范围", GUILayout.Width(60));
            dupRefScopeFolder = EditorGUILayout.TextField(dupRefScopeFolder);
            if (GUILayout.Button("浏览", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("选择引用范围文件夹", 
                    string.IsNullOrEmpty(dupRefScopeFolder) ? "Assets" : dupRefScopeFolder, "");
                if (!string.IsNullOrEmpty(path))
                {
                    string dataPath = Application.dataPath;
                    if (path.StartsWith(dataPath))
                        dupRefScopeFolder = "Assets" + path.Substring(dataPath.Length);
                    else
                        dupRefScopeFolder = path;
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("  统一引用和引用次数的搜索范围（留空则用扫描目录）", EditorStyles.miniLabel);

            EditorGUILayout.Space(3);

            // 递归子目录
            // Include subfolders
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("包含子目录", GUILayout.Width(60));
            dupIncludeSubfolders = EditorGUILayout.Toggle(dupIncludeSubfolders);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 查重模式
            // Scan mode
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("查重模式", GUILayout.Width(60));
            if (GUILayout.Toggle(dupScanMode == DupScanMode.MD5, "精确哈希", EditorStyles.miniButtonLeft))
                dupScanMode = DupScanMode.MD5;
            if (GUILayout.Toggle(dupScanMode == DupScanMode.Visual, "视觉指纹", EditorStyles.miniButtonRight))
                dupScanMode = DupScanMode.Visual;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 视觉查重阈值
            // Visual threshold
            if (dupScanMode == DupScanMode.Visual)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("相似阈值", GUILayout.Width(60));
                dupVisualThreshold = EditorGUILayout.Slider(dupVisualThreshold, 0.80f, 1.00f);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制操作按钮
        /// Draw action buttons
        /// </summary>
        private void DrawDupActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            // 扫描按钮
            GUI.enabled = !string.IsNullOrEmpty(dupScanFolder) && !dupIsScanning;
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("扫描重复", GUILayout.Height(28)))
            {
                DupScanDuplicates();
            }
            GUI.backgroundColor = Color.white;

            // 统一引用按钮
            GUI.enabled = dupGroups.Count > 0 && dupGroups.Any(g => g.HasProjectFile);
            if (GUILayout.Button("统一引用", GUILayout.Height(28)))
            {
                DupUnifyReferences();
            }

            // 删除重复按钮
            GUI.enabled = dupGroups.Count > 0;
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("删除重复", GUILayout.Height(28)))
            {
                DupDeleteDuplicates();
            }
            GUI.backgroundColor = Color.white;

            // 清空结果按钮
            if (GUILayout.Button("清空结果", GUILayout.Height(28)))
            {
                DupClearResults();
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制扫描结果列表
        /// Draw scan result list
        /// </summary>
        private void DrawDupResults()
        {
            if (dupGroups.Count == 0) return;

            dupScrollPosition = EditorGUILayout.BeginScrollView(dupScrollPosition);

            foreach (var group in dupGroups)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // 组标题
                // Group header
                string headerLabel = dupScanMode == DupScanMode.Visual
                    ? $"{Path.GetFileName(group.selectedFilePath)} ({group.files.Count}个重复, 相似度:{group.similarity:F2})"
                    : $"{Path.GetFileName(group.selectedFilePath)} ({group.files.Count}个重复)";

                group.isFoldout = EditorGUILayout.Foldout(group.isFoldout, headerLabel, true);

                if (group.isFoldout)
                {
                    foreach (var file in group.files)
                    {
                        EditorGUILayout.BeginHorizontal();

                        // 保留单选
                        // Keep radio button
                        bool isKeep = file.filePath == group.selectedFilePath;
                        EditorGUI.BeginChangeCheck();
                        bool newKeep = EditorGUILayout.Toggle(isKeep, GUILayout.Width(20));
                        if (EditorGUI.EndChangeCheck())
                        {
                            if (newKeep && !isKeep)
                            {
                                group.selectedFilePath = file.filePath;
                                foreach (var f in group.files)
                                    f.isSelected = f.filePath == file.filePath;
                                DupRecalculateStats();
                            }
                        }

                        // 保留/待删标识
                        GUILayout.Label(isKeep ? "●" : "○", GUILayout.Width(15));

                        // 缩略图
                        // Thumbnail
                        if (file.thumbnail != null)
                        {
                            GUILayout.Label(new GUIContent(file.thumbnail), GUILayout.Width(32), GUILayout.Height(32));
                        }

                        // 文件路径（截断显示）
                        // File path (truncated)
                        EditorGUILayout.BeginVertical();
                        string displayPath = file.isProjectFile ? file.assetPath : file.filePath;
                        if (displayPath != null && displayPath.Length > 55)
                            displayPath = "..." + displayPath.Substring(displayPath.Length - 52);

                        if (isKeep)
                            EditorGUILayout.LabelField(displayPath + " (保留)", EditorStyles.boldLabel);
                        else
                            EditorGUILayout.LabelField(displayPath);

                        // 文件信息行
                        // File info row
                        string infoText = DupFormatFileSize(file.fileSize);
                        if (file.isProjectFile)
                            infoText += $"  [{file.referenceCount}次引用]";
                        else
                            infoText += "  [外部]";
                        EditorGUILayout.LabelField(infoText, EditorStyles.miniLabel);
                        EditorGUILayout.EndVertical();

                        // 定位按钮
                        // Locate button
                        if (file.isProjectFile)
                        {
                            if (GUILayout.Button("定位", GUILayout.Width(40), GUILayout.Height(30)))
                            {
                                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(file.assetPath);
                                if (obj != null)
                                {
                                    Selection.activeObject = obj;
                                    EditorGUIUtility.PingObject(obj);
                                }
                            }
                        }
                        else
                        {
                            if (GUILayout.Button("打开", GUILayout.Width(40), GUILayout.Height(30)))
                            {
                                // 在资源管理器中打开并选中文件
                                // Open in explorer and select file
                                if (File.Exists(file.filePath))
                                {
                                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file.filePath.Replace("/", "\\")}\"");
                                }
                            }
                        }

                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.Space(2);
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region 重复查清 - 扫描逻辑

        /// <summary>
        /// 执行重复扫描
        /// Execute duplicate scan
        /// </summary>
        private void DupScanDuplicates()
        {
            if (string.IsNullOrEmpty(dupScanFolder))
            {
                EditorUtility.DisplayDialog("提示", "请先选择扫描目录", "确定");
                return;
            }

            // 解析扫描路径（支持项目内外）
            // Resolve scan path (supports project internal/external)
            string fullPath = DupResolveFullPath(dupScanFolder);

            if (!Directory.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("错误", $"目录不存在: {dupScanFolder}", "确定");
                return;
            }

            dupIsScanning = true;
            DupClearResults();

            try
            {
                // 收集图片文件
                // Collect image files
                SearchOption searchOption = dupIncludeSubfolders 
                    ? SearchOption.AllDirectories 
                    : SearchOption.TopDirectoryOnly;
                string[] allFiles = Directory.GetFiles(fullPath, "*.*", searchOption);

                var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tga", ".psd", ".tiff", ".tif"
                };

                var imageFiles = allFiles
                    .Where(f => !f.EndsWith(".meta") && imageExtensions.Contains(Path.GetExtension(f)))
                    .ToList();

                if (imageFiles.Count == 0)
                {
                    EditorUtility.DisplayDialog("提示", "扫描目录中没有找到图片文件", "确定");
                    dupIsScanning = false;
                    return;
                }

                EditorUtility.DisplayProgressBar("重复查清", "正在扫描...", 0f);

                if (dupScanMode == DupScanMode.MD5)
                {
                    DupScanByMD5(imageFiles);
                }
                else
                {
                    DupScanByVisual(imageFiles);
                }

                // 为每组自动选择默认保留项
                // Auto-select default keep item for each group
                foreach (var group in dupGroups)
                {
                    DupAutoSelectKeepItem(group);
                }

                // 加载缩略图
                // Load thumbnails
                DupLoadThumbnails();

                // 统计
                // Statistics
                dupGroupCount = dupGroups.Count;
                DupRecalculateStats();

                EditorUtility.ClearProgressBar();

                if (dupGroupCount == 0)
                {
                    ShowNotification(new GUIContent("未发现重复图片"));
                }
                else
                {
                    ShowNotification(new GUIContent($"发现 {dupGroupCount} 组重复图片"));
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"扫描重复图片失败: {e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog("扫描失败", e.Message, "确定");
            }

            dupIsScanning = false;
            Repaint();
        }

        /// <summary>
        /// MD5精确查重
        /// MD5 exact duplicate scan
        /// </summary>
        private void DupScanByMD5(List<string> imageFiles)
        {
            // 文件路径 -> MD5 映射
            var hashMap = new Dictionary<string, List<DupFileInfo>>();

            for (int i = 0; i < imageFiles.Count; i++)
            {
                string filePath = imageFiles[i];
                EditorUtility.DisplayProgressBar("精确查重",
                    $"({i + 1}/{imageFiles.Count}) {Path.GetFileName(filePath)}",
                    (float)i / imageFiles.Count);

                string hash = DupCalculateFileHash(filePath);
                if (string.IsNullOrEmpty(hash)) continue;

                var fileInfo = DupCreateFileInfo(filePath);
                if (fileInfo == null) continue;

                if (!hashMap.ContainsKey(hash))
                    hashMap[hash] = new List<DupFileInfo>();
                hashMap[hash].Add(fileInfo);
            }

            // 只保留有重复的组
            // Only keep groups with duplicates
            foreach (var kvp in hashMap)
            {
                if (kvp.Value.Count > 1)
                {
                    var group = new DupGroup
                    {
                        groupKey = kvp.Key,
                        files = kvp.Value,
                        similarity = 1.0f
                    };
                    dupGroups.Add(group);
                }
            }
        }

        /// <summary>
        /// 视觉指纹查重
        /// Visual fingerprint duplicate scan
        /// </summary>
        private void DupScanByVisual(List<string> imageFiles)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName
                .Replace("\\", "/") + "/";

            // 第一遍：提取所有指纹
            // First pass: extract all fingerprints
            var fileInfos = new List<DupFileInfo>();

            for (int i = 0; i < imageFiles.Count; i++)
            {
                string filePath = imageFiles[i];

                if (EditorUtility.DisplayCancelableProgressBar("视觉查重 - 提取指纹",
                    $"({i + 1}/{imageFiles.Count}) {Path.GetFileName(filePath)}",
                    (float)i / imageFiles.Count))
                {
                    EditorUtility.ClearProgressBar();
                    ShowNotification(new GUIContent("扫描已取消"));
                    return;
                }

                var fileInfo = DupCreateFileInfo(filePath);
                if (fileInfo == null) continue;

                // 加载纹理并提取指纹
                // Load texture and extract fingerprint
                Texture2D texture = null;
                bool needDestroy = false;

                // 尝试项目内加载
                if (fileInfo.isProjectFile && !string.IsNullOrEmpty(fileInfo.assetPath))
                {
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(fileInfo.assetPath);
                }

                // 项目内加载失败或项目外文件，从文件系统读取
                if (texture == null && File.Exists(filePath))
                {
                    try
                    {
                        byte[] fileData = File.ReadAllBytes(filePath);
                        texture = new Texture2D(2, 2);
                        if (texture.LoadImage(fileData))
                        {
                            needDestroy = true;
                        }
                        else
                        {
                            UnityEngine.Object.DestroyImmediate(texture);
                            texture = null;
                        }
                    }
                    catch { texture = null; }
                }

                if (texture == null) continue;

                fileInfo.fingerprint = ExtractFingerprint(texture);

                if (needDestroy)
                    UnityEngine.Object.DestroyImmediate(texture);

                if (fileInfo.fingerprint != null)
                    fileInfos.Add(fileInfo);
            }

            // 第二遍：两两比较，聚类分组
            // Second pass: pairwise comparison, cluster grouping
            var used = new HashSet<int>();

            for (int i = 0; i < fileInfos.Count; i++)
            {
                if (used.Contains(i)) continue;

                if (EditorUtility.DisplayCancelableProgressBar("视觉查重 - 聚类分组",
                    $"({i + 1}/{fileInfos.Count}) 分组中...",
                    (float)i / fileInfos.Count))
                {
                    EditorUtility.ClearProgressBar();
                    ShowNotification(new GUIContent("扫描已取消"));
                    return;
                }

                var group = new DupGroup();
                group.files.Add(fileInfos[i]);
                used.Add(i);

                float minSim = 1.0f;

                for (int j = i + 1; j < fileInfos.Count; j++)
                {
                    if (used.Contains(j)) continue;

                    float sim = fileInfos[i].fingerprint.CompareTo(fileInfos[j].fingerprint);
                    if (sim >= dupVisualThreshold)
                    {
                        group.files.Add(fileInfos[j]);
                        used.Add(j);
                        if (sim < minSim) minSim = sim;
                    }
                }

                if (group.files.Count > 1)
                {
                    group.groupKey = fileInfos[i].filePath;
                    group.similarity = minSim;
                    dupGroups.Add(group);
                }
            }
        }

        #endregion

        #region 重复查清 - 操作功能

        /// <summary>
        /// 统一引用（GUID替换）
        /// Unify references (GUID replacement)
        /// </summary>
        private void DupUnifyReferences()
        {
            if (dupGroups.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "请先扫描重复图片", "确定");
                return;
            }

            // 获取引用范围
            string refScope = string.IsNullOrEmpty(dupRefScopeFolder) ? dupScanFolder : dupRefScopeFolder;
            string refScopePath = DupResolveAssetPath(refScope);
            if (string.IsNullOrEmpty(refScopePath) || !refScopePath.StartsWith("Assets"))
            {
                EditorUtility.DisplayDialog("提示", "引用范围必须在项目内（Assets下）", "确定");
                return;
            }

            // 收集 GUID 替换映射
            // Collect GUID replacement mapping
            var guidMappings = new Dictionary<string, string>();

            foreach (var group in dupGroups)
            {
                // 找到保留项的 GUID
                var keepFile = group.files.FirstOrDefault(f => f.filePath == group.selectedFilePath);
                if (keepFile == null || !keepFile.isProjectFile || string.IsNullOrEmpty(keepFile.assetPath))
                    continue;

                string keepGuid = AssetDatabase.AssetPathToGUID(keepFile.assetPath);
                if (string.IsNullOrEmpty(keepGuid)) continue;

                foreach (var file in group.files)
                {
                    if (file.filePath == group.selectedFilePath) continue;
                    if (!file.isProjectFile || string.IsNullOrEmpty(file.assetPath)) continue;

                    string oldGuid = AssetDatabase.AssetPathToGUID(file.assetPath);
                    if (!string.IsNullOrEmpty(oldGuid) && !guidMappings.ContainsKey(oldGuid))
                    {
                        guidMappings[oldGuid] = keepGuid;
                    }
                }
            }

            if (guidMappings.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有需要替换的引用", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认统一引用",
                $"将在 {refScopePath} 范围内搜索并替换 {guidMappings.Count} 组 GUID\n" +
                "修改会影响材质球、Prefab、场景、ScriptableObject",
                "执行", "取消"))
            {
                return;
            }

            int totalReplacements = 0;

            try
            {
                // 查找所有可能引用贴图的资源
                // Find all assets that may reference textures
                string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { refScopePath });
                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { refScopePath });
                string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { refScopePath });
                string[] soGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { refScopePath });

                var allAssetPaths = new List<string>();
                foreach (string guid in matGuids)
                    allAssetPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
                foreach (string guid in prefabGuids)
                    allAssetPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
                foreach (string guid in sceneGuids)
                    allAssetPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
                foreach (string guid in soGuids)
                    allAssetPaths.Add(AssetDatabase.GUIDToAssetPath(guid));

                for (int i = 0; i < allAssetPaths.Count; i++)
                {
                    string assetPath = allAssetPaths[i];
                    EditorUtility.DisplayProgressBar("统一引用",
                        $"({i + 1}/{allAssetPaths.Count}): {Path.GetFileName(assetPath)}",
                        (float)i / allAssetPaths.Count);

                    if (!DupIsTextSerializedAsset(assetPath))
                        continue;

                    if (DupReplaceGuidsInFile(assetPath, guidMappings))
                    {
                        totalReplacements++;
                    }
                }

                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // 重新计算引用次数
                // Recalculate reference counts
                DupRefreshReferenceCounts();

                EditorUtility.DisplayDialog("统一引用完成",
                    $"已更新 {totalReplacements} 个资源的引用\n\n" +
                    "现在可以安全删除重复图片", "确定");
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"统一引用失败: {e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog("统一引用失败", e.Message, "确定");
            }

            Repaint();
        }

        /// <summary>
        /// 删除重复文件
        /// Delete duplicate files
        /// </summary>
        private void DupDeleteDuplicates()
        {
            if (dupGroups.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有重复图片可删除", "确定");
                return;
            }

            // 统计要删除的文件
            // Count files to delete
            int deleteCount = 0;
            long deleteSize = 0;
            var toDeleteProject = new List<string>();     // 项目内文件
            var toDeleteExternal = new List<string>();    // 项目外文件

            foreach (var group in dupGroups)
            {
                foreach (var file in group.files)
                {
                    if (file.filePath == group.selectedFilePath) continue;

                    deleteCount++;
                    deleteSize += file.fileSize;

                    if (file.isProjectFile && !string.IsNullOrEmpty(file.assetPath))
                        toDeleteProject.Add(file.assetPath);
                    else
                        toDeleteExternal.Add(file.filePath);
                }
            }

            if (deleteCount == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有可删除的重复图片", "确定");
                return;
            }

            string confirmMsg = $"即将删除 {deleteCount} 张重复图片\n" +
                                $"  项目内: {toDeleteProject.Count} 张\n" +
                                $"  项目外: {toDeleteExternal.Count} 张\n" +
                                $"预计释放 {DupFormatFileSize(deleteSize)}\n\n" +
                                "此操作不可撤销！";

            if (!EditorUtility.DisplayDialog("确认删除", confirmMsg, "删除", "取消"))
                return;

            int successCount = 0;

            try
            {
                // 删除项目内文件
                // Delete project files
                if (toDeleteProject.Count > 0)
                {
                    AssetDatabase.StartAssetEditing();
                    foreach (string path in toDeleteProject)
                    {
                        if (AssetDatabase.DeleteAsset(path))
                        {
                            successCount++;
                            Debug.Log($"[重复查清] 已删除项目内文件: {path}");
                        }
                        else
                        {
                            Debug.LogWarning($"[重复查清] 删除失败: {path}");
                        }
                    }
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.Refresh();
                }

                // 删除项目外文件
                // Delete external files
                foreach (string path in toDeleteExternal)
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                            successCount++;
                            Debug.Log($"[重复查清] 已删除外部文件: {path}");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[重复查清] 删除外部文件失败: {path}, {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"删除过程出错: {e.Message}");
            }

            // 清空已处理的组
            // Clear processed groups
            DupClearResults();

            EditorUtility.DisplayDialog("删除完成",
                $"已删除 {successCount}/{deleteCount} 张重复图片", "确定");

            Repaint();
        }

        /// <summary>
        /// 清空扫描结果
        /// Clear scan results
        /// </summary>
        private void DupClearResults()
        {
            // 清理缩略图纹理
            // Clean up thumbnail textures
            foreach (var group in dupGroups)
            {
                foreach (var file in group.files)
                {
                    if (file.thumbnail != null && !file.isProjectFile)
                    {
                        UnityEngine.Object.DestroyImmediate(file.thumbnail);
                        file.thumbnail = null;
                    }
                }
            }

            dupGroups.Clear();
            dupGroupCount = 0;
            dupCleanableCount = 0;
            dupCleanableSize = 0;
            Repaint();
        }

        #endregion

        #region 重复查清 - 工具方法

        /// <summary>
        /// 创建文件信息对象
        /// Create file info object
        /// </summary>
        private DupFileInfo DupCreateFileInfo(string absolutePath)
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName
                    .Replace("\\", "/");

                var info = new DupFileInfo
                {
                    filePath = absolutePath.Replace("\\", "/"),
                    fileName = Path.GetFileName(absolutePath),
                    fileSize = new FileInfo(absolutePath).Length,
                    isProjectFile = false,
                    referenceCount = 0,
                    isSelected = false
                };

                // 判断是否在项目内
                // Check if file is inside the project
                string normalizedPath = absolutePath.Replace("\\", "/");
                string normalizedRoot = projectRoot + "/";

                if (normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    info.isProjectFile = true;
                    info.assetPath = normalizedPath.Substring(normalizedRoot.Length);
                }

                return info;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"创建文件信息失败: {absolutePath}, {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 计算文件MD5哈希值
        /// Calculate file MD5 hash
        /// </summary>
        private string DupCalculateFileHash(string absolutePath)
        {
            try
            {
                using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(absolutePath))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"计算哈希失败: {absolutePath}, {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 自动选择保留项
        /// Auto-select keep item
        /// 精确查重：按引用次数降序 → 路径长度升序
        /// 视觉查重：按文件大小降序（保留高清版本）
        /// </summary>
        private void DupAutoSelectKeepItem(DupGroup group)
        {
            if (group.files.Count == 0) return;

            // 先计算引用次数
            foreach (var file in group.files)
            {
                if (file.isProjectFile && !string.IsNullOrEmpty(file.assetPath))
                {
                    file.referenceCount = DupGetReferenceCount(file.assetPath);
                }
            }

            DupFileInfo bestFile;

            if (dupScanMode == DupScanMode.MD5)
            {
                // 精确查重：引用次数优先 → 路径最短
                bestFile = group.files
                    .OrderByDescending(f => f.referenceCount)
                    .ThenBy(f => f.filePath.Length)
                    .First();
            }
            else
            {
                // 视觉查重：文件大小优先（保留高清版本）
                bestFile = group.files
                    .OrderByDescending(f => f.fileSize)
                    .ThenByDescending(f => f.referenceCount)
                    .First();
            }

            group.selectedFilePath = bestFile.filePath;
            bestFile.isSelected = true;
        }

        /// <summary>
        /// 获取贴图被引用次数
        /// Get texture reference count
        /// </summary>
        private int DupGetReferenceCount(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return 0;

            string textureGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(textureGuid)) return 0;

            // 确定搜索范围
            string refScope = string.IsNullOrEmpty(dupRefScopeFolder) ? dupScanFolder : dupRefScopeFolder;
            string refScopePath = DupResolveAssetPath(refScope);
            if (string.IsNullOrEmpty(refScopePath) || !refScopePath.StartsWith("Assets"))
                return 0;

            int count = 0;

            string[] searchTypes = { "t:Material", "t:Prefab", "t:Scene", "t:ScriptableObject" };
            foreach (string searchType in searchTypes)
            {
                string[] guids = AssetDatabase.FindAssets(searchType, new[] { refScopePath });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string[] deps = AssetDatabase.GetDependencies(path, false);
                    if (deps.Contains(assetPath))
                        count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 刷新所有引用次数
        /// Refresh all reference counts
        /// </summary>
        private void DupRefreshReferenceCounts()
        {
            foreach (var group in dupGroups)
            {
                foreach (var file in group.files)
                {
                    if (file.isProjectFile && !string.IsNullOrEmpty(file.assetPath))
                        file.referenceCount = DupGetReferenceCount(file.assetPath);
                }
            }
            DupRecalculateStats();
        }

        /// <summary>
        /// 重新计算统计数据
        /// Recalculate statistics
        /// </summary>
        private void DupRecalculateStats()
        {
            dupCleanableCount = 0;
            dupCleanableSize = 0;

            foreach (var group in dupGroups)
            {
                foreach (var file in group.files)
                {
                    if (file.filePath != group.selectedFilePath)
                    {
                        dupCleanableCount++;
                        dupCleanableSize += file.fileSize;
                    }
                }
            }
        }

        /// <summary>
        /// 加载缩略图
        /// Load thumbnails
        /// </summary>
        private void DupLoadThumbnails()
        {
            foreach (var group in dupGroups)
            {
                foreach (var file in group.files)
                {
                    try
                    {
                        if (file.isProjectFile && !string.IsNullOrEmpty(file.assetPath))
                        {
                            // 项目内使用 AssetPreview
                            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(file.assetPath);
                            if (asset != null)
                            {
                                file.thumbnail = AssetPreview.GetMiniThumbnail(asset);
                            }
                        }
                        else if (File.Exists(file.filePath))
                        {
                            // 项目外从文件加载
                            byte[] fileData = File.ReadAllBytes(file.filePath);
                            var tex = new Texture2D(2, 2);
                            if (tex.LoadImage(fileData))
                            {
                                // 缩放到缩略图尺寸
                                var thumb = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                                RenderTexture rt = RenderTexture.GetTemporary(32, 32);
                                Graphics.Blit(tex, rt);
                                RenderTexture.active = rt;
                                thumb.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
                                thumb.Apply();
                                RenderTexture.active = null;
                                RenderTexture.ReleaseTemporary(rt);
                                UnityEngine.Object.DestroyImmediate(tex);
                                file.thumbnail = thumb;
                            }
                            else
                            {
                                UnityEngine.Object.DestroyImmediate(tex);
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// 解析完整路径（支持项目相对路径和绝对路径）
        /// Resolve full path (supports project-relative and absolute paths)
        /// </summary>
        private string DupResolveFullPath(string inputPath)
        {
            if (string.IsNullOrEmpty(inputPath)) return "";

            // 如果是绝对路径（包含盘符或以/开头），直接返回
            if (Path.IsPathRooted(inputPath))
                return inputPath.Replace("\\", "/");

            // 如果以 Assets 开头，转换为绝对路径
            if (inputPath.StartsWith("Assets"))
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                return Path.Combine(projectRoot, inputPath).Replace("\\", "/");
            }

            return inputPath.Replace("\\", "/");
        }

        /// <summary>
        /// 解析为 Assets/ 开头的路径（仅项目内有效）
        /// Resolve to Assets/ path (only valid for project files)
        /// </summary>
        private string DupResolveAssetPath(string inputPath)
        {
            if (string.IsNullOrEmpty(inputPath)) return "";

            if (inputPath.StartsWith("Assets"))
                return inputPath;

            string dataPath = Application.dataPath;
            string normalizedInput = inputPath.Replace("\\", "/");
            string normalizedData = dataPath.Replace("\\", "/");

            if (normalizedInput.StartsWith(normalizedData))
                return "Assets" + normalizedInput.Substring(normalizedData.Length);

            return inputPath;
        }

        /// <summary>
        /// 检查资源是否是文本序列化资源
        /// Check if asset is text serialized
        /// </summary>
        private bool DupIsTextSerializedAsset(string assetPath)
        {
            string ext = Path.GetExtension(assetPath).ToLowerInvariant();
            return ext == ".mat" || ext == ".prefab" || ext == ".unity" 
                || ext == ".asset" || ext == ".controller" || ext == ".anim"
                || ext == ".overrideController";
        }

        /// <summary>
        /// 替换文件中的多个GUID
        /// Replace multiple GUIDs in a file
        /// </summary>
        private bool DupReplaceGuidsInFile(string assetPath, Dictionary<string, string> guidMappings)
        {
            try
            {
                string fullPath = Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName, assetPath);

                if (!File.Exists(fullPath)) return false;

                string content = File.ReadAllText(fullPath);
                string originalContent = content;

                foreach (var kvp in guidMappings)
                {
                    content = content.Replace(kvp.Key, kvp.Value);
                }

                if (content != originalContent)
                {
                    File.WriteAllText(fullPath, content);
                    Debug.Log($"[重复查清] 更新引用: {Path.GetFileName(assetPath)}");
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"替换GUID失败: {assetPath}, {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 格式化文件大小
        /// Format file size
        /// </summary>
        private string DupFormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }

        #endregion
    }
}
#endif
