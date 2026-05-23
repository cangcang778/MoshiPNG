using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace MoshiTools
{
    /// <summary>
    /// 资源清理工具 - 贴图检查模块
    /// Asset Cleaner - Texture resolution check module
    /// 扫描指定文件夹下的贴图，筛选出不能被4整除、超过1024x1024、超过2MB的贴图
    /// </summary>
    public partial class Moshi_AssetClean
    {
        #region 贴图检查数据结构

        /// <summary>
        /// 贴图问题信息
        /// Texture issue info
        /// </summary>
        [Serializable]
        internal class TexCheckEntry
        {
            public string path;                 // 资源路径
            public int width;                   // 宽度
            public int height;                  // 高度
            public int suggestedWidth;          // 建议宽度（向上取整到4的倍数）
            public int suggestedHeight;         // 建议高度（向上取整到4的倍数）
            public string issue;                // 问题描述
            public Texture2D texture;           // 贴图引用
            public bool isFixed = false;        // 是否已修复
            public long fileSize = 0;           // 文件大小（字节）
            public bool isOversized = false;    // 是否超过1024x1024

            public TexCheckEntry(string path, int width, int height)
            {
                this.path = path;
                this.width = width;
                this.height = height;
                this.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

                // 计算建议尺寸（向上取整到最近的4的倍数）
                // Calculate suggested size (round up to nearest multiple of 4)
                this.suggestedWidth = ((width + 3) / 4) * 4;
                this.suggestedHeight = ((height + 3) / 4) * 4;

                issue = "";
            }
        }

        #endregion

        #region 贴图检查字段

        // 文件夹路径
        // Folder path
        private string texCheckFolderPath = "";

        // 扫描结果
        // Scan results
        private List<TexCheckEntry> texCheckIssues = new List<TexCheckEntry>();
        private List<TexCheckEntry> texCheckOversized = new List<TexCheckEntry>();
        private bool texCheckHasScanned = false;

        // UI状态
        // UI state
        private Vector2 texCheckScrollPos;
        private Vector2 texCheckPreviewScrollPos;
        private bool texCheckShowDetails = true;
        private TexCheckEntry texCheckSelectedEntry = null;
        private int texCheckSelectedIndex = -1;

        // 拖拽相关
        // Drag & drop
        private bool texCheckIsDraggingOver = false;

        // 列表/预览区域动态高度
        // Dynamic height for list/preview area
        private float texCheckListHeight = 300f;

        // 文件大小限制
        // File size limit
        private const long TEX_CHECK_MAX_FILE_SIZE = 2 * 1024 * 1024; // 2MB

        #endregion

        #region 初始化

        partial void InitTexCheckModule()
        {
            // 初始化完成
            // Initialization complete
        }

        #endregion

        #region 贴图检查标签页绘制

        /// <summary>
        /// 绘制贴图检查标签页
        /// Draw texture check tab
        /// </summary>
        private void DrawTexCheckTab()
        {
            // 在最开始处理拖拽事件（优先级最高）
            // Handle drag & drop first (highest priority)
            TexCheck_HandleDragDrop();

            // 说明区域
            // Description area
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("功能说明", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("扫描指定文件夹下的贴图，检查以下问题：", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("• 分辨率不能被4整除 → 可一键修复（自动调整到最近的4倍数）", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• 分辨率超过1024x1024 → 标记为超大贴图，建议压缩", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• 文件大小超过2MB → 可打开压缩窗口调整分辨率", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• 支持预览贴图、复制路径、定位到Project面板", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("💡 提示：可以直接拖拽文件夹到此窗口，支持强制重导后扫描", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 文件夹选择
            // Folder selection
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("扫描设置", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("扫描文件夹", GUILayout.Width(60));
            texCheckFolderPath = EditorGUILayout.TextField(texCheckFolderPath);
            if (GUILayout.Button("浏览", GUILayout.Width(50)))
            {
                string selected = EditorUtility.OpenFolderPanel("选择要扫描的文件夹", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                {
                    if (selected.StartsWith(Application.dataPath))
                    {
                        texCheckFolderPath = "Assets" + selected.Substring(Application.dataPath.Length);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // 拖拽区域提示
            // Drag & drop area hint
            TexCheck_DrawDragDropAreaUI();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 扫描按钮（未输入路径时禁用）
            // Scan buttons (disabled when no folder path)
            bool hasFolder = !string.IsNullOrEmpty(texCheckFolderPath);
            EditorGUILayout.BeginHorizontal();
            var originalColor = GUI.backgroundColor;

            EditorGUI.BeginDisabledGroup(!hasFolder);
            GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            if (GUILayout.Button("开始扫描", GUILayout.Height(30)))
            {
                TexCheck_ScanTextures();
            }
            GUI.backgroundColor = new Color(1f, 0.85f, 0.6f);
            if (GUILayout.Button("强制重导后扫描", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog(TOOL_NAME,
                    $"将重新导入 {texCheckFolderPath} 文件夹中的所有资源，这可能需要一些时间。是否继续？",
                    "是", "否"))
                {
                    AssetDatabase.ImportAsset(texCheckFolderPath, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
                    EditorUtility.DisplayProgressBar("重新导入", "正在导入资源...", 1);
                    System.Threading.Thread.Sleep(1000);
                    EditorUtility.ClearProgressBar();
                    TexCheck_ScanTextures();
                }
            }
            EditorGUI.EndDisabledGroup();

            GUI.backgroundColor = originalColor;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 选项
            // Options
            texCheckShowDetails = EditorGUILayout.ToggleLeft("显示详细信息", texCheckShowDetails);

            EditorGUILayout.Space(5);

            // 结果显示
            // Results display
            TexCheck_DrawResults();
        }

        /// <summary>
        /// 绘制贴图检查结果
        /// Draw texture check results
        /// </summary>
        private void TexCheck_DrawResults()
        {
            if (!texCheckHasScanned)
            {
                EditorGUILayout.HelpBox("点击\"开始扫描\"按钮开始检测", MessageType.Info);
                return;
            }

            // 结果统计
            // Result statistics
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("扫描结果", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  发现问题贴图: {texCheckIssues.Count}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"  超过1024x1024: {texCheckOversized.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            if (texCheckIssues.Count == 0)
            {
                EditorGUILayout.HelpBox("✅ 所有贴图正常！", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox($"共发现 {texCheckIssues.Count} 个分辨率问题的贴图", MessageType.Warning);

            EditorGUILayout.Space(3);

            // 问题列表和预览区域并排显示
            // Issue list and preview panel side by side
            // 固定最大高度，确保底部按钮始终可见
            // Fixed max height to ensure bottom buttons are always visible
            float listHeight = 550f;
            texCheckListHeight = listHeight;

            EditorGUILayout.BeginHorizontal();

            // 左侧：问题列表
            // Left: issue list
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.6f - 10));
            EditorGUILayout.LabelField("问题贴图列表", EditorStyles.boldLabel);
            texCheckScrollPos = EditorGUILayout.BeginScrollView(texCheckScrollPos, GUILayout.Height(listHeight));

            for (int i = 0; i < texCheckIssues.Count; i++)
            {
                TexCheck_DrawIssueItem(i, texCheckIssues[i]);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // 右侧：预览窗口
            // Right: preview panel
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(position.width * 0.4f - 10));
            TexCheck_DrawPreviewPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // 超大贴图警告
            // Oversized texture warning
            if (texCheckOversized.Count > 0)
            {
                EditorGUILayout.HelpBox($"⚠️ 检测到 {texCheckOversized.Count} 个超过1024x1024的贴图，建议压缩", MessageType.Warning);
            }

            EditorGUILayout.Space(3);

            // 批量操作
            // Batch operations
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("复制路径", EditorStyles.miniButtonLeft, GUILayout.Width(70)))
            {
                TexCheck_CopyAllPathsToClipboard();
            }
            if (GUILayout.Button("定位首个", EditorStyles.miniButtonMid, GUILayout.Width(70)))
            {
                if (texCheckIssues.Count > 0 && texCheckIssues[0].texture != null)
                {
                    EditorGUIUtility.PingObject(texCheckIssues[0].texture);
                }
            }
            if (GUILayout.Button("修复全部", EditorStyles.miniButtonRight, GUILayout.Width(70)))
            {
                TexCheck_FixAllTextures();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制单条问题贴图项
        /// Draw single texture issue item
        /// </summary>
        private void TexCheck_DrawIssueItem(int index, TexCheckEntry info)
        {
            bool isOverSize = info.fileSize > TEX_CHECK_MAX_FILE_SIZE;
            bool isSelected = texCheckSelectedIndex == index;

            Color originalBgColor = GUI.backgroundColor;

            if (isSelected)
                GUI.backgroundColor = new Color(0.3f, 0.5f, 1f, 1f);
            else if (isOverSize)
                GUI.backgroundColor = new Color(1f, 0.3f, 0.3f, 1f);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = originalBgColor;

            // 头部：路径和问题
            // Header: path and issue
            EditorGUILayout.BeginHorizontal();

            if (isOverSize)
            {
                GUIStyle redStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.red } };
                EditorGUILayout.LabelField($"[{index + 1}] {info.path}", redStyle, GUILayout.MaxWidth(250));
            }
            else
            {
                EditorGUILayout.LabelField($"[{index + 1}] {info.path}", EditorStyles.label, GUILayout.MaxWidth(250));
            }

            if (isOverSize)
            {
                GUIStyle redBoldStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.red } };
                EditorGUILayout.LabelField(info.issue, redBoldStyle);
            }
            else
            {
                EditorGUILayout.LabelField(info.issue, EditorStyles.boldLabel);
            }
            EditorGUILayout.EndHorizontal();

            // 详细信息
            // Details
            if (texCheckShowDetails)
            {
                EditorGUILayout.LabelField($"分辨率: {info.width}x{info.height}", EditorStyles.miniLabel);

                if (isOverSize)
                {
                    GUIStyle redStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = Color.red },
                        fontStyle = FontStyle.Bold
                    };
                    EditorGUILayout.LabelField($"⚠️ 文件大小: {TexCheck_FormatFileSize(info.fileSize)} (超过2MB限制!)", redStyle);
                }
                else
                {
                    EditorGUILayout.LabelField($"文件大小: {TexCheck_FormatFileSize(info.fileSize)}", EditorStyles.miniLabel);
                }

                EditorGUILayout.LabelField($"建议修改为: {info.suggestedWidth}x{info.suggestedHeight}", EditorStyles.miniLabel);

                if (info.isFixed)
                {
                    EditorGUILayout.HelpBox("✅ 已修复", MessageType.Info);
                }
            }

            // 操作按钮
            // Action buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("定位", GUILayout.Width(50)))
            {
                if (info.texture != null)
                    EditorGUIUtility.PingObject(info.texture);
            }
            if (GUILayout.Button("复制路径", GUILayout.Width(60)))
            {
                EditorGUIUtility.systemCopyBuffer = info.path;
            }
            if (!info.isFixed && GUILayout.Button("修复", GUILayout.Width(50)))
            {
                TexCheck_FixTexture(info);
                Repaint();
            }
            if (isOverSize && !info.isFixed)
            {
                if (GUILayout.Button("压缩", GUILayout.Width(50)))
                {
                    TexCheck_CompressTexture(info);
                    Repaint();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // 点击选中
            // Click to select
            Rect itemRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
            {
                texCheckSelectedIndex = index;
                texCheckSelectedEntry = info;
                Event.current.Use();
                Repaint();
            }
        }

        /// <summary>
        /// 绘制预览面板
        /// Draw preview panel
        /// </summary>
        private void TexCheck_DrawPreviewPanel()
        {
            EditorGUILayout.LabelField("贴图预览", EditorStyles.boldLabel);

            if (texCheckSelectedEntry == null || texCheckSelectedEntry.texture == null)
            {
                EditorGUILayout.HelpBox("点击左侧列表中的贴图项可预览", MessageType.Info);
                return;
            }

            texCheckPreviewScrollPos = EditorGUILayout.BeginScrollView(texCheckPreviewScrollPos, GUILayout.Height(texCheckListHeight));

            EditorGUILayout.LabelField("点击预览图可定位到Project", EditorStyles.miniLabel);

            float previewSize = Mathf.Min(position.width * 0.35f, 300f);
            Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize, GUILayout.MaxWidth(previewSize), GUILayout.MaxHeight(previewSize));

            EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 1f));

            if (texCheckSelectedEntry.texture != null)
            {
                float texAspect = (float)texCheckSelectedEntry.texture.width / texCheckSelectedEntry.texture.height;
                float rectAspect = previewRect.width / previewRect.height;

                Rect texRect = previewRect;
                if (texAspect > rectAspect)
                {
                    float newHeight = previewRect.width / texAspect;
                    texRect.y += (previewRect.height - newHeight) / 2;
                    texRect.height = newHeight;
                }
                else
                {
                    float newWidth = previewRect.height * texAspect;
                    texRect.x += (previewRect.width - newWidth) / 2;
                    texRect.width = newWidth;
                }

                GUI.DrawTexture(texRect, texCheckSelectedEntry.texture, ScaleMode.ScaleToFit);

                if (Event.current.type == EventType.MouseDown && previewRect.Contains(Event.current.mousePosition))
                {
                    Selection.activeObject = texCheckSelectedEntry.texture;
                    EditorGUIUtility.PingObject(texCheckSelectedEntry.texture);
                    Event.current.Use();
                }

                EditorGUIUtility.AddCursorRect(previewRect, MouseCursor.Link);
            }

            EditorGUILayout.Space(5);

            // 贴图信息
            // Texture info
            EditorGUILayout.LabelField("贴图信息", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"路径: {texCheckSelectedEntry.path}", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField($"分辨率: {texCheckSelectedEntry.width} x {texCheckSelectedEntry.height}", EditorStyles.miniLabel);

            bool isOverSize = texCheckSelectedEntry.fileSize > TEX_CHECK_MAX_FILE_SIZE;
            if (isOverSize)
            {
                GUIStyle redStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = Color.red },
                    fontStyle = FontStyle.Bold
                };
                EditorGUILayout.LabelField($"文件大小: {TexCheck_FormatFileSize(texCheckSelectedEntry.fileSize)} (超过2MB!)", redStyle);
            }
            else
            {
                EditorGUILayout.LabelField($"文件大小: {TexCheck_FormatFileSize(texCheckSelectedEntry.fileSize)}", EditorStyles.miniLabel);
            }

            EditorGUILayout.LabelField($"问题: {texCheckSelectedEntry.issue}", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(5);

            // 快捷操作
            // Quick actions
            if (GUILayout.Button("定位到Project", EditorStyles.miniButton))
            {
                Selection.activeObject = texCheckSelectedEntry.texture;
                EditorGUIUtility.PingObject(texCheckSelectedEntry.texture);
            }
            if (!texCheckSelectedEntry.isFixed && GUILayout.Button("修复分辨率", EditorStyles.miniButton))
            {
                TexCheck_FixTexture(texCheckSelectedEntry);
                Repaint();
            }
            if (isOverSize && !texCheckSelectedEntry.isFixed && GUILayout.Button("压缩贴图", EditorStyles.miniButton))
            {
                TexCheck_CompressTexture(texCheckSelectedEntry);
                Repaint();
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制拖拽区域UI
        /// Draw drag & drop area UI
        /// </summary>
        private void TexCheck_DrawDragDropAreaUI()
        {
            Color originalColor = GUI.color;

            if (texCheckIsDraggingOver)
                GUI.color = new Color(0.2f, 0.5f, 1f, 0.3f);
            else
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.1f);

            GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(50));
            Rect dragArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));

            string hintText = texCheckIsDraggingOver
                ? "📂 释放鼠标设置此文件夹"
                : "📂 拖拽文件夹到此区域";

            EditorGUI.LabelField(dragArea, hintText,
                new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    fontSize = 11
                });

            GUILayout.EndVertical();
            GUI.color = originalColor;
        }

        #endregion

        #region 贴图检查扫描逻辑

        /// <summary>
        /// 扫描贴图
        /// Scan textures
        /// </summary>
        private void TexCheck_ScanTextures()
        {
            texCheckIssues.Clear();
            texCheckOversized.Clear();
            texCheckSelectedEntry = null;
            texCheckSelectedIndex = -1;
            texCheckHasScanned = true;

            if (!Directory.Exists(texCheckFolderPath))
            {
                EditorUtility.DisplayDialog(TOOL_NAME, $"文件夹不存在: {texCheckFolderPath}", "确定");
                texCheckHasScanned = false;
                return;
            }

            string fullPath = Path.Combine(Application.dataPath, texCheckFolderPath.Substring("Assets/".Length));
            if (!Directory.Exists(fullPath))
            {
                EditorUtility.DisplayDialog(TOOL_NAME, $"文件夹不存在: {fullPath}", "确定");
                texCheckHasScanned = false;
                return;
            }

            // 扫描贴图文件
            // Scan texture files
            string[] supportedExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".bmp", ".gif" };
            List<string> textureFiles = new List<string>();

            string[] allFiles = Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories);
            foreach (var file in allFiles)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (supportedExtensions.Contains(ext))
                {
                    textureFiles.Add(file);
                }
            }

            if (textureFiles.Count == 0)
            {
                EditorUtility.DisplayDialog(TOOL_NAME, $"在 {texCheckFolderPath} 中没有找到贴图文件", "确定");
                return;
            }

            EditorUtility.DisplayProgressBar(TOOL_NAME, "正在扫描贴图...", 0);

            int issueCount = 0;

            for (int i = 0; i < textureFiles.Count; i++)
            {
                TexCheck_ProcessRawTextureFile(textureFiles[i], ref issueCount);

                if ((i + 1) % 10 == 0)
                {
                    EditorUtility.DisplayProgressBar(TOOL_NAME,
                        $"正在扫描... ({i + 1}/{textureFiles.Count})",
                        (float)(i + 1) / textureFiles.Count);
                }
            }

            EditorUtility.ClearProgressBar();

            Debug.Log($"[{TOOL_NAME}] 贴图扫描完成 - 总计:{textureFiles.Count} 问题:{texCheckIssues.Count} 超大:{texCheckOversized.Count}");

            string message = $"扫描完成！\n总贴图数: {textureFiles.Count}\n问题贴图数: {texCheckIssues.Count}\n超过1024x1024: {texCheckOversized.Count}";
            EditorUtility.DisplayDialog(TOOL_NAME, message, "确定");
        }

        /// <summary>
        /// 处理原始贴图文件
        /// Process raw texture file
        /// </summary>
        private void TexCheck_ProcessRawTextureFile(string filePath, ref int issueCount)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                long fileSize = fileInfo.Length;

                string assetPath = "Assets" + filePath.Substring(Application.dataPath.Length).Replace("\\", "/");

                List<string> problemsList = new List<string>();
                bool hasIssue = false;
                bool isOversized = false;

                // 检查文件大小
                // Check file size
                if (fileSize > TEX_CHECK_MAX_FILE_SIZE)
                {
                    problemsList.Add($"文件大小 {TexCheck_FormatFileSize(fileSize)} 超过2MB限制");
                    hasIssue = true;
                }

                // 读取图像分辨率
                // Read image resolution
                int width = 0;
                int height = 0;

                try
                {
                    Texture2D tempTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    byte[] imageData = File.ReadAllBytes(filePath);

                    if (tempTexture.LoadImage(imageData))
                    {
                        width = tempTexture.width;
                        height = tempTexture.height;

                        if (width % 4 != 0)
                        {
                            problemsList.Add($"宽度 {width} 不能被4整除");
                            hasIssue = true;
                        }
                        if (height % 4 != 0)
                        {
                            problemsList.Add($"高度 {height} 不能被4整除");
                            hasIssue = true;
                        }

                        if (width > 1024 || height > 1024)
                        {
                            problemsList.Add("分辨率超过1024x1024");
                            hasIssue = true;
                            isOversized = true;
                        }
                    }

                    DestroyImmediate(tempTexture);
                }
                catch (Exception imgEx)
                {
                    Debug.LogWarning($"读取贴图分辨率失败: {filePath}, 错误: {imgEx.Message}");
                }

                if (hasIssue)
                {
                    TexCheckEntry entry = new TexCheckEntry(assetPath, width, height);
                    entry.fileSize = fileSize;
                    entry.isOversized = isOversized;
                    entry.issue = string.Join(", ", problemsList);

                    texCheckIssues.Add(entry);
                    issueCount++;

                    if (isOversized)
                    {
                        texCheckOversized.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"处理贴图文件失败: {filePath}\n错误: {ex.Message}");
            }
        }

        #endregion

        #region 贴图检查修复逻辑

        /// <summary>
        /// 修复所有问题贴图
        /// Fix all issue textures
        /// </summary>
        private void TexCheck_FixAllTextures()
        {
            if (texCheckIssues.Count == 0)
            {
                EditorUtility.DisplayDialog(TOOL_NAME, "没有发现问题贴图", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog(TOOL_NAME,
                $"将修复 {texCheckIssues.Count} 个贴图。\n此操作无法撤销，是否继续？",
                "是", "否"))
                return;

            int fixedCount = 0;
            int failedCount = 0;

            for (int i = 0; i < texCheckIssues.Count; i++)
            {
                EditorUtility.DisplayProgressBar(TOOL_NAME,
                    $"正在修复... ({i + 1}/{texCheckIssues.Count})",
                    (float)i / texCheckIssues.Count);

                if (TexCheck_FixTexture(texCheckIssues[i]))
                    fixedCount++;
                else
                    failedCount++;
            }

            EditorUtility.ClearProgressBar();

            EditorUtility.DisplayDialog(TOOL_NAME,
                $"修复完成！\n成功: {fixedCount}\n失败: {failedCount}",
                "确定");

            TexCheck_ScanTextures();
        }

        /// <summary>
        /// 修复单个贴图
        /// Fix single texture
        /// </summary>
        private bool TexCheck_FixTexture(TexCheckEntry info)
        {
            try
            {
                if (info.width % 4 == 0 && info.height % 4 == 0)
                {
                    info.isFixed = true;
                    return true;
                }

                string fullPath = Path.Combine(Application.dataPath, info.path.Substring("Assets/".Length));
                if (!File.Exists(fullPath))
                {
                    Debug.LogError($"❌ 贴图文件不存在: {fullPath}");
                    return false;
                }

                return TexCheck_FixTextureFromFile(fullPath, info);
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ 修复贴图失败: {info.path}\n错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从文件直接修复贴图
        /// Fix texture directly from file
        /// </summary>
        private bool TexCheck_FixTextureFromFile(string fullPath, TexCheckEntry info)
        {
            try
            {
                // 备份
                // Backup
                string backupPath = fullPath + ".backup";
                if (!File.Exists(backupPath))
                {
                    File.Copy(fullPath, backupPath, true);
                }

                byte[] imageData = File.ReadAllBytes(fullPath);
                Texture2D tempTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tempTexture.LoadImage(imageData))
                {
                    DestroyImmediate(tempTexture);
                    return false;
                }

                int sourceWidth = tempTexture.width;
                int sourceHeight = tempTexture.height;
                Color[] pixels = tempTexture.GetPixels(0);

                Texture2D newTexture = new Texture2D(info.suggestedWidth, info.suggestedHeight, TextureFormat.RGBA32, false);
                newTexture.filterMode = FilterMode.Bilinear;

                TexCheck_ResizeTexture(pixels, sourceWidth, sourceHeight, newTexture, info.suggestedWidth, info.suggestedHeight);

                byte[] newImageData = newTexture.EncodeToPNG();
                File.WriteAllBytes(fullPath, newImageData);

                DestroyImmediate(tempTexture);
                DestroyImmediate(newTexture);

                string assetPath = info.path;
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                        info.isFixed = true;
                        Debug.Log($"✅ 贴图修复成功: {assetPath} ({info.width}x{info.height}) → ({info.suggestedWidth}x{info.suggestedHeight})");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"⚠ 导入资源时出错: {ex.Message}");
                    }
                };

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ 文件修复失败: {fullPath}\n错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 压缩贴图（打开压缩弹窗）
        /// Compress texture (open compress window)
        /// </summary>
        private void TexCheck_CompressTexture(TexCheckEntry info)
        {
            try
            {
                string fullPath = Path.Combine(Application.dataPath, info.path.Substring("Assets/".Length));
                if (!File.Exists(fullPath))
                {
                    Debug.LogError($"❌ 贴图文件不存在: {fullPath}");
                    return;
                }

                Moshi_AssetClean_TexCompressWindow.ShowWindow(info, fullPath, this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ 压缩贴图失败: {info.path}\n错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行贴图压缩（供弹窗调用）
        /// Execute compress (called by compress window)
        /// </summary>
        internal void TexCheck_ExecuteCompress(TexCheckEntry info, string fullPath, int targetWidth, int targetHeight)
        {
            TexCheck_CompressTextureToSize(fullPath, info, targetWidth, targetHeight);
        }

        /// <summary>
        /// 将贴图压缩到指定尺寸
        /// Compress texture to target size
        /// </summary>
        private void TexCheck_CompressTextureToSize(string fullPath, TexCheckEntry info, int targetWidth, int targetHeight)
        {
            try
            {
                // 备份
                // Backup
                string backupPath = fullPath + ".backup";
                if (!File.Exists(backupPath))
                {
                    File.Copy(fullPath, backupPath, true);
                }

                // 尝试读取贴图像素数据
                // Try to read texture pixel data
                Color[] pixels = null;
                int sourceWidth = info.width;
                int sourceHeight = info.height;

                // 先尝试通过 AssetDatabase 加载
                // First try loading via AssetDatabase
                TextureImporter importer = AssetImporter.GetAtPath(info.path) as TextureImporter;
                bool wasReadable = false;

                if (importer != null)
                {
                    wasReadable = importer.isReadable;
                    if (!wasReadable)
                    {
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                    }

                    Texture2D sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(info.path);
                    if (sourceTexture != null)
                    {
                        try
                        {
                            sourceWidth = sourceTexture.width;
                            sourceHeight = sourceTexture.height;
                            pixels = sourceTexture.GetPixels(0);
                        }
                        catch (Exception)
                        {
                            // 尝试 RenderTexture 方式
                            // Try RenderTexture approach
                            pixels = TexCheck_GetPixelsUsingRenderTexture(sourceTexture);
                            if (pixels != null)
                            {
                                sourceWidth = sourceTexture.width;
                                sourceHeight = sourceTexture.height;
                            }
                        }
                    }
                }

                // 如果 AssetDatabase 方式失败，直接从文件读取
                // If AssetDatabase failed, read directly from file
                if (pixels == null)
                {
                    byte[] imageData = File.ReadAllBytes(fullPath);
                    Texture2D tempTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tempTexture.LoadImage(imageData))
                    {
                        sourceWidth = tempTexture.width;
                        sourceHeight = tempTexture.height;
                        pixels = tempTexture.GetPixels(0);
                    }
                    DestroyImmediate(tempTexture);
                }

                // 恢复原来的可读设置
                // Restore original readable setting
                if (importer != null && !wasReadable)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }

                if (pixels == null)
                {
                    EditorUtility.DisplayDialog(TOOL_NAME, "无法读取贴图数据，请确保贴图格式正确", "确定");
                    return;
                }

                // 创建新贴图并缩放
                // Create new texture and resize
                Texture2D newTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
                newTexture.filterMode = FilterMode.Bilinear;

                TexCheck_ResizeTexture(pixels, sourceWidth, sourceHeight, newTexture, targetWidth, targetHeight);

                byte[] newImageData = newTexture.EncodeToPNG();
                File.WriteAllBytes(fullPath, newImageData);

                DestroyImmediate(newTexture);

                long newFileSize = new FileInfo(fullPath).Length;

                string assetPath = info.path;
                int origWidth = info.width;
                int origHeight = info.height;
                long origFileSize = info.fileSize;

                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                        info.width = targetWidth;
                        info.height = targetHeight;
                        info.fileSize = newFileSize;
                        info.isFixed = newFileSize <= TEX_CHECK_MAX_FILE_SIZE;
                        if (info.isFixed) info.issue = "已压缩";

                        Debug.Log($"✅ 贴图压缩成功: {assetPath}\n" +
                                  $"   分辨率: ({origWidth}x{origHeight}) → ({targetWidth}x{targetHeight})\n" +
                                  $"   文件大小: {TexCheck_FormatFileSize(origFileSize)} → {TexCheck_FormatFileSize(newFileSize)}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"⚠ 导入资源时出错: {ex.Message}");
                    }
                };

                EditorUtility.DisplayDialog(TOOL_NAME,
                    $"贴图压缩成功!\n\n" +
                    $"分辨率: {info.width}x{info.height} → {targetWidth}x{targetHeight}\n" +
                    $"文件大小: {TexCheck_FormatFileSize(info.fileSize)} → {TexCheck_FormatFileSize(newFileSize)}",
                    "确定");
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ 压缩贴图失败: {fullPath}\n错误: {ex.Message}");
                EditorUtility.DisplayDialog(TOOL_NAME, $"压缩贴图时发生错误:\n{ex.Message}", "确定");
            }
        }

        #endregion

        #region 贴图检查辅助方法

        /// <summary>
        /// 处理拖拽事件
        /// Handle drag & drop events
        /// </summary>
        private void TexCheck_HandleDragDrop()
        {
            Event evt = Event.current;

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (currentTab != TabMode.TexCheck) return;

                if (TexCheck_ValidateDragData())
                {
                    if (evt.type == EventType.DragUpdated)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        texCheckIsDraggingOver = true;
                    }
                    else if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        TexCheck_ProcessDragDropData();
                        texCheckIsDraggingOver = false;
                        evt.Use();
                    }
                }
                else
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    texCheckIsDraggingOver = false;
                }
            }
            else if (evt.type == EventType.DragExited)
            {
                texCheckIsDraggingOver = false;
            }
        }

        /// <summary>
        /// 验证拖拽数据
        /// Validate drag data
        /// </summary>
        private bool TexCheck_ValidateDragData()
        {
            if (DragAndDrop.objectReferences == null || DragAndDrop.objectReferences.Length == 0)
                return false;

            foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
            {
                if (draggedObject == null) continue;
                string path = AssetDatabase.GetAssetPath(draggedObject);
                if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets"))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 处理拖拽数据
        /// Process drag data
        /// </summary>
        private void TexCheck_ProcessDragDropData()
        {
            if (DragAndDrop.objectReferences == null || DragAndDrop.objectReferences.Length == 0)
                return;

            foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
            {
                if (draggedObject == null) continue;
                string path = AssetDatabase.GetAssetPath(draggedObject);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets")) continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    texCheckFolderPath = path;
                    TexCheck_ScanTextures();
                    return;
                }

                string folderPath = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    texCheckFolderPath = folderPath.Replace("\\", "/");
                    TexCheck_ScanTextures();
                    return;
                }
            }
        }

        /// <summary>
        /// 复制所有路径到剪贴板
        /// Copy all paths to clipboard
        /// </summary>
        private void TexCheck_CopyAllPathsToClipboard()
        {
            if (texCheckIssues.Count == 0) return;

            var sb = new System.Text.StringBuilder();
            foreach (var issue in texCheckIssues)
            {
                sb.AppendLine(issue.path);
            }

            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log($"[{TOOL_NAME}] 已复制 {texCheckIssues.Count} 个路径到剪贴板");
        }

        /// <summary>
        /// 贴图缩放（双线性插值）
        /// Texture resize (bilinear interpolation)
        /// </summary>
        private void TexCheck_ResizeTexture(Color[] sourcePixels, int sourceWidth, int sourceHeight,
                                             Texture2D targetTexture, int targetWidth, int targetHeight)
        {
            Color[] targetPixels = new Color[targetWidth * targetHeight];

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    float srcX = (x / (float)targetWidth) * sourceWidth;
                    float srcY = (y / (float)targetHeight) * sourceHeight;

                    Color pixel = TexCheck_GetBilinearPixel(sourcePixels, sourceWidth, sourceHeight, srcX, srcY);
                    targetPixels[y * targetWidth + x] = pixel;
                }
            }

            targetTexture.SetPixels(targetPixels);
            targetTexture.Apply();
        }

        /// <summary>
        /// 双线性插值获取像素
        /// Get bilinear interpolated pixel
        /// </summary>
        private Color TexCheck_GetBilinearPixel(Color[] pixels, int width, int height, float x, float y)
        {
            x = Mathf.Clamp(x, 0, width - 1);
            y = Mathf.Clamp(y, 0, height - 1);

            int x0 = (int)x;
            int y0 = (int)y;
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int y1 = Mathf.Min(y0 + 1, height - 1);

            float fx = x - x0;
            float fy = y - y0;

            Color p00 = pixels[y0 * width + x0];
            Color p10 = pixels[y0 * width + x1];
            Color p01 = pixels[y1 * width + x0];
            Color p11 = pixels[y1 * width + x1];

            Color p0 = Color.Lerp(p00, p10, fx);
            Color p1 = Color.Lerp(p01, p11, fx);

            return Color.Lerp(p0, p1, fy);
        }

        /// <summary>
        /// 使用RenderTexture方式读取贴图像素
        /// Get pixels using RenderTexture approach
        /// </summary>
        private Color[] TexCheck_GetPixelsUsingRenderTexture(Texture2D source)
        {
            try
            {
                RenderTexture renderTex = RenderTexture.GetTemporary(
                    source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);

                Graphics.Blit(source, renderTex);

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = renderTex;

                Texture2D readableTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                readableTexture.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
                readableTexture.Apply();

                Color[] pixels = readableTexture.GetPixels();

                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTex);
                DestroyImmediate(readableTexture);

                return pixels;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"⚠ RenderTexture 方式读取失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 格式化文件大小
        /// Format file size
        /// </summary>
        private string TexCheck_FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// 从Project选择跳转到问题列表（在OnSelectionChange中调用）
        /// Jump to issue list from Project selection
        /// </summary>
        private void TexCheck_OnSelectionChange()
        {
            if (currentTab != TabMode.TexCheck) return;

            if (Selection.activeObject is Texture2D selectedTexture)
            {
                string selectedPath = AssetDatabase.GetAssetPath(selectedTexture);

                for (int i = 0; i < texCheckIssues.Count; i++)
                {
                    if (texCheckIssues[i].path == selectedPath)
                    {
                        texCheckSelectedIndex = i;
                        texCheckSelectedEntry = texCheckIssues[i];

                        float itemHeight = texCheckShowDetails ? 120f : 50f;
                        texCheckScrollPos.y = i * itemHeight;

                        Repaint();
                        break;
                    }
                }
            }
        }

        #endregion
    }
}
