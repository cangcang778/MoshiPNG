using UnityEngine;
#if UNITY_TIMELINE
using UnityEngine.Timeline;
#endif
using UnityEngine.U2D;
using UnityEngine.Video;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using System.Text;
using System.Text.RegularExpressions;

public class Moshi_RefCounter : EditorWindow
{
    private const string TOOL_NAME = "引用计数统计";
    
    private Vector2 scrollPosition;
    private Dictionary<string, int> referenceCounts = new Dictionary<string, int>();
    private List<string> zeroReferenceAssets = new List<string>();
    private bool showZeroReferenceAssets = true;
    private DefaultAsset selectedFolder;
    
    // 资源类型分组开关（方案C：6组）
    private bool includeAnimationGroup = true;      // 动画组：AnimationClip, AnimatorController, AnimatorOverrideController, Avatar, Timeline
    private bool includeTextureMaterialGroup = true; // 贴图材质组：Texture2D, Material, Shader, Sprite, RenderTexture
    private bool includeMeshGroup = true;           // 模型组：Mesh, FBX/OBJ
    private bool includeAudioVideoGroup = false;    // 音视频组：AudioClip, VideoClip
    private bool includePrefabGroup = false;        // 预制体组：Prefab
    private bool includeOtherGroup = false;         // 其他组：Font, SpriteAtlas, ScriptableObject, PhysicMaterial, TerrainData
    
    private Dictionary<string, Texture2D> previewCache = new Dictionary<string, Texture2D>();
    private Dictionary<string, bool> assetSelection = new Dictionary<string, bool>();
    private Vector2 zeroReferenceScrollPosition;  // 零引用资源列表的滚动位置
    
    // 资源整合工具字段
    private DefaultAsset consolidationMainFolder;
    private List<DefaultAsset> whitelistFolderAssets = new List<DefaultAsset>();

    private const string WhitelistPrefsKey = "TextureMaterialReferenceCounter_WhitelistFolders";
    private const string CustomFolderNamesPrefsKey = "TextureMaterialReferenceCounter_CustomFolderNames";

    // 自定义文件夹名称配置
    private Dictionary<DependencyAssetType, string> customFolderNames;
    private bool showFolderNameConfig;

    private readonly List<string> autoCollectedSecondaryFolders = new List<string>();
    private readonly HashSet<string> autoSecondaryFolderLookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, DependencyRecord> dependencyRecordLookup = new Dictionary<string, DependencyRecord>(StringComparer.OrdinalIgnoreCase);
    private readonly List<DependencyRecord> internalDependencyRecords = new List<DependencyRecord>();
    private readonly List<DependencyRecord> externalDependencyRecords = new List<DependencyRecord>();
    private readonly List<DependencyRecord> whitelistedDependencyRecords = new List<DependencyRecord>();
    private bool hasDependencyScanResults;
    private string lastScanMainFolderPath;
    private Vector2 dependencyResultScrollPosition;

    private bool showConsolidationSection = true;
    private bool showAutoCollectedFolders = true;
    private bool showExternalDependenciesFoldout = true;
    private bool showWhitelistDependenciesFoldout = true;
    private bool showInternalDependenciesFoldout;

    private bool isConsolidating;
    private string consolidationStatusMessage;
    private bool showConsolidationLogFoldout;
    private readonly List<string> consolidationLogs = new List<string>();

    // 一键资源整理相关字段
    private bool isOneClickOrganizing;
    private int oneClickCurrentPhase;
    private string oneClickStatusMessage;
    private OrganizationReport lastOrganizationReport;

    private const int MaxConsolidationLogEntries = 200;
    private static readonly Dictionary<DependencyAssetType, string> AssetTypeToFolderName = new Dictionary<DependencyAssetType, string>
    {
        { DependencyAssetType.Texture, "Texture" },
        { DependencyAssetType.Material, "Material" },
        { DependencyAssetType.Mesh, "Mesh" },
        { DependencyAssetType.Animation, "Animation" },
        { DependencyAssetType.Prefab, "Prefab" },
        { DependencyAssetType.Shader, "Shader" },
        { DependencyAssetType.Audio, "Audio" },
        { DependencyAssetType.Scriptable, "ScriptableObject" },
        { DependencyAssetType.Timeline, "Timeline" },
        { DependencyAssetType.Font, "Font" },
        { DependencyAssetType.Video, "Video" },
        { DependencyAssetType.SpriteAtlas, "SpriteAtlas" },
        { DependencyAssetType.PhysicMaterial, "PhysicMaterial" },
        { DependencyAssetType.Terrain, "Terrain" },
        { DependencyAssetType.Other, "Other" }
    };

    private static readonly HashSet<string> TextSerializedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".prefab",
        ".unity",
        ".mat",
        ".asset",
        ".controller",
        ".anim",
        ".overrideController",
        ".playable"
    };

    private static readonly Regex GuidTokenRegex = new Regex("guid:\\s*([a-fA-F0-9]{32})", RegexOptions.Compiled);
    
    [MenuItem("工具/Moshi/" + TOOL_NAME)]
    public static void ShowWindow()
    {
        GetWindow<Moshi_RefCounter>(TOOL_NAME);
    }

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(TOOL_NAME, EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("资源依赖整合 + 零引用检测 + 清理的一体化工具", MessageType.Info);

        EditorGUILayout.Space();
        
        // ========== 统一的文件夹设置区域 ==========
        GUILayout.Label("📁 文件夹设置", EditorStyles.boldLabel);
        
        // 使用统一的目标文件夹
        EditorGUI.BeginChangeCheck();
        consolidationMainFolder = (DefaultAsset)EditorGUILayout.ObjectField("目标文件夹", consolidationMainFolder, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            // 同步到引用统计模块
            selectedFolder = consolidationMainFolder;
        }
        
        if (consolidationMainFolder != null)
        {
            string folderPath = AssetDatabase.GetAssetPath(consolidationMainFolder);
            EditorGUILayout.HelpBox($"工作目录: {folderPath}\n• 依赖整合：外部资源将复制到此目录\n• 零引用检测：检测此目录内的资源", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("请设置目标文件夹（整合目标 & 检测范围）", MessageType.Warning);
        }
        
        // 白名单文件夹
        EnsureFolderListInitialized(ref whitelistFolderAssets);
        DrawFolderList("白名单文件夹", whitelistFolderAssets, "添加白名单", SaveWhitelistToPrefs, true);
        
        int validWhitelistCount = whitelistFolderAssets != null ? whitelistFolderAssets.Count(f => f != null) : 0;
        if (validWhitelistCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"白名单 ({validWhitelistCount} 个):\n" +
                "• 作为引用来源参与统计\n" +
                "• 不参与零引用检测（避免公共库被误删）", 
                MessageType.None);
        }

        EditorGUILayout.Space();
        
        // ========== 资源类型分组 ==========
        GUILayout.Label("📦 资源类型分组", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        includeAnimationGroup = EditorGUILayout.ToggleLeft("动画", includeAnimationGroup, GUILayout.Width(80));
        includeTextureMaterialGroup = EditorGUILayout.ToggleLeft("贴图材质", includeTextureMaterialGroup, GUILayout.Width(80));
        includeMeshGroup = EditorGUILayout.ToggleLeft("模型", includeMeshGroup, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        includeAudioVideoGroup = EditorGUILayout.ToggleLeft("音视频", includeAudioVideoGroup, GUILayout.Width(80));
        includePrefabGroup = EditorGUILayout.ToggleLeft("预制体", includePrefabGroup, GUILayout.Width(80));
        includeOtherGroup = EditorGUILayout.ToggleLeft("其他", includeOtherGroup, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        bool hasValidResourceTypes = includeAnimationGroup || includeTextureMaterialGroup || includeMeshGroup || 
                                      includeAudioVideoGroup || includePrefabGroup || includeOtherGroup;
        bool hasMainFolder = consolidationMainFolder != null;

        if (!hasValidResourceTypes)
        {
            EditorGUILayout.HelpBox("请至少选择一个资源分组", MessageType.Warning);
        }

        EditorGUILayout.Space();
        
        // ========== 一键资源整理（突出显示） ==========
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.enabled = hasMainFolder && hasValidResourceTypes && !isOneClickOrganizing;
        GUI.backgroundColor = new Color(0.3f, 0.7f, 0.3f);
        if (GUILayout.Button("🚀 一键资源整理（扫描 → 整合 → 检测 → 报告）", GUILayout.Height(36)))
        {
            if (EditorUtility.DisplayDialog("一键资源整理",
                "即将执行以下操作：\n\n" +
                "1. 扫描目标文件夹的所有依赖关系\n" +
                "2. 将外部依赖资源复制到目标文件夹\n" +
                "3. 更新所有资源的引用关系\n" +
                "4. 检测零引用资源\n" +
                "5. 生成整理报告\n\n" +
                "确定要开始吗？",
                "开始整理", "取消"))
            {
                RunOneClickOrganization();
            }
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
        
        // 显示一键整理状态
        if (!string.IsNullOrEmpty(oneClickStatusMessage))
        {
            EditorGUILayout.HelpBox(oneClickStatusMessage, isOneClickOrganizing ? MessageType.Info : 
                (oneClickStatusMessage.StartsWith("✅") ? MessageType.Info : MessageType.Warning));
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();
        
        // ========== 分步操作（可折叠） ==========
        showConsolidationSection = EditorGUILayout.Foldout(showConsolidationSection, "⚙️ 分步操作 & 高级设置", true);
        if (showConsolidationSection)
        {
            EditorGUI.indentLevel++;
            
            // 文件夹名称配置
            DrawFolderNameConfigSection();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("分步操作按钮", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = hasMainFolder && hasValidResourceTypes;
            if (GUILayout.Button("扫描依赖", GUILayout.Height(24)))
            {
                RunDependencyScan();
            }
            GUI.enabled = hasDependencyScanResults && !isConsolidating;
            if (GUILayout.Button("整合资源", GUILayout.Height(24)))
            {
                RunOneClickConsolidation();
            }
            GUI.enabled = hasMainFolder && hasValidResourceTypes;
            if (GUILayout.Button("检测零引用", GUILayout.Height(24)))
            {
                CountAllReferencesAsync();
            }
            GUI.enabled = lastOrganizationReport != null || zeroReferenceAssets.Count > 0;
            if (GUILayout.Button("导出报告", GUILayout.Height(24)))
            {
                if (lastOrganizationReport != null)
                {
                    ExportOrganizationReport();
                }
                else
                {
                    ExportCleanupReport();
                }
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = hasDependencyScanResults;
            if (GUILayout.Button("清空扫描结果", GUILayout.Height(20)))
            {
                ClearDependencyScanResults();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            if (!string.IsNullOrEmpty(consolidationStatusMessage))
            {
                EditorGUILayout.HelpBox(consolidationStatusMessage, MessageType.Info);
            }
            
            // 整合日志
            if (consolidationLogs.Count > 0)
            {
                showConsolidationLogFoldout = EditorGUILayout.Foldout(showConsolidationLogFoldout, $"整合日志 ({consolidationLogs.Count})", true);
                if (showConsolidationLogFoldout)
                {
                    EditorGUI.indentLevel++;
                    foreach (string logEntry in consolidationLogs)
                    {
                        EditorGUILayout.LabelField(logEntry, EditorStyles.wordWrappedLabel);
                    }
                    EditorGUI.indentLevel--;
                }
            }
            
            EditorGUILayout.Space();
            
            // 图片查重区域
            DrawDuplicateTextureSection();

            EditorGUILayout.Space();

            // Timeline残留清理区域
            DrawTimelineCleanupSection();
            
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        
        // ========== 零引用资源列表 ==========
        showZeroReferenceAssets = EditorGUILayout.Foldout(showZeroReferenceAssets, 
            $"🗑️ 零引用资源 ({zeroReferenceAssets.Count})", true);
        
        if (showZeroReferenceAssets && zeroReferenceAssets.Count > 0)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全选", GUILayout.Width(60)))
            {
                SelectAllZeroReferenceAssets(true);
            }
            if (GUILayout.Button("取消全选", GUILayout.Width(80)))
            {
                SelectAllZeroReferenceAssets(false);
            }
            if (GUILayout.Button("删除选中", GUILayout.Width(80)))
            {
                DeleteSelectedZeroReferenceAssets();
            }
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("一键删除全部", GUILayout.Width(100)))
            {
                DeleteAllZeroReferenceAssets();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            zeroReferenceScrollPosition = EditorGUILayout.BeginScrollView(
                zeroReferenceScrollPosition, 
                GUILayout.Height(250));
            
            string assetToDelete = null;  // 记录待删除的资源，避免在 foreach 中修改集合
            foreach (string assetPath in zeroReferenceAssets)
            {
                EditorGUILayout.BeginHorizontal();
                
                Texture2D preview = GetAssetPreview(assetPath);
                if (preview != null)
                {
                    GUILayout.Label(preview, GUILayout.Width(24), GUILayout.Height(24));
                }
                else
                {
                    GUILayout.Label("", GUILayout.Width(24), GUILayout.Height(24));
                }
                
                if (!assetSelection.ContainsKey(assetPath))
                {
                    assetSelection[assetPath] = false;
                }
                assetSelection[assetPath] = EditorGUILayout.Toggle(assetSelection[assetPath], GUILayout.Width(20));
                
                EditorGUILayout.LabelField(Path.GetFileName(assetPath), GUILayout.Width(150));
                EditorGUILayout.LabelField(assetPath, GUILayout.Width(250));
                
                if (GUILayout.Button("定位", GUILayout.Width(50)))
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    EditorGUIUtility.PingObject(Selection.activeObject);
                }
                
                if (GUILayout.Button("删除", GUILayout.Width(50)))
                {
                    if (EditorUtility.DisplayDialog("确认删除", 
                        $"确定要删除 {Path.GetFileName(assetPath)} 吗？", "删除", "取消"))
                    {
                        assetToDelete = assetPath;  // 标记待删除，稍后处理
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            
            // 在 foreach 外部执行删除操作
            if (!string.IsNullOrEmpty(assetToDelete))
            {
                if (AssetDatabase.DeleteAsset(assetToDelete))
                {
                    zeroReferenceAssets.Remove(assetToDelete);
                    referenceCounts.Remove(assetToDelete);
                    previewCache.Remove(assetToDelete);
                    assetSelection.Remove(assetToDelete);
                    Debug.Log($"已删除: {assetToDelete}");
                }
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        
        // ========== 扫描结果（依赖统计） ==========
        if (hasDependencyScanResults)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📊 扫描统计", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"内部依赖: {internalDependencyRecords.Count} | 外部依赖: {externalDependencyRecords.Count} | 白名单依赖: {whitelistedDependencyRecords.Count}");

            if (autoCollectedSecondaryFolders.Count > 0)
            {
                showAutoCollectedFolders = EditorGUILayout.Foldout(showAutoCollectedFolders, $"自动收集的副文件夹 ({autoCollectedSecondaryFolders.Count})", true);
                if (showAutoCollectedFolders)
                {
                    EditorGUI.indentLevel++;
                    foreach (string folder in autoCollectedSecondaryFolders)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(folder);
                        // 检查该路径是否已在白名单中
                        // Check if this path is already in the whitelist
                        bool alreadyInWhitelist = IsFolderInWhitelist(folder);
                        GUI.enabled = !alreadyInWhitelist;
                        if (GUILayout.Button(alreadyInWhitelist ? "已添加" : "白名单", GUILayout.Width(60)))
                        {
                            AddFolderToWhitelist(folder);
                        }
                        GUI.enabled = true;
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUI.indentLevel--;
                }
            }

            dependencyResultScrollPosition = EditorGUILayout.BeginScrollView(dependencyResultScrollPosition, GUILayout.Height(400));
            DrawDependencyBucketFoldout("🔴 外部依赖", ref showExternalDependenciesFoldout, externalDependencyRecords);
            DrawDependencyBucketFoldout("⚪ 白名单依赖", ref showWhitelistDependenciesFoldout, whitelistedDependencyRecords);
            DrawDependencyBucketFoldout("✅ 内部依赖", ref showInternalDependenciesFoldout, internalDependencyRecords);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ========== 引用统计结果 ==========
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        if (referenceCounts.Count > 0)
        {
            GUILayout.Label($"📋 引用统计结果 ({referenceCounts.Count} 个资源):", EditorStyles.boldLabel);
            
            foreach (var pair in referenceCounts.OrderByDescending(p => p.Value))
            {
                EditorGUILayout.BeginHorizontal();
                
                if (pair.Value == 0)
                {
                    GUI.color = Color.red;
                }
                
                Texture2D preview = GetAssetPreview(pair.Key);
                if (preview != null)
                {
                    GUILayout.Label(preview, GUILayout.Width(24), GUILayout.Height(24));
                }
                else
                {
                    GUILayout.Label("", GUILayout.Width(24), GUILayout.Height(24));
                }
                
                EditorGUILayout.LabelField(Path.GetFileName(pair.Key), GUILayout.Width(130));
                EditorGUILayout.LabelField(pair.Key, GUILayout.Width(230));
                
                GUI.color = Color.white;
                
                EditorGUILayout.LabelField($"引用次数: {pair.Value}", GUILayout.Width(100));
                
                if (GUILayout.Button("定位", GUILayout.Width(60)))
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(pair.Key);
                    EditorGUIUtility.PingObject(Selection.activeObject);
                }
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        EditorGUILayout.EndScrollView();
    }

    // 其他方法保持不变...
    private void SelectAllZeroReferenceAssets(bool select)
    {
        foreach (string assetPath in zeroReferenceAssets)
        {
            assetSelection[assetPath] = select;
        }
        Repaint();
    }

    private void DeleteSelectedZeroReferenceAssets()
    {
        List<string> assetsToDelete = new List<string>();
        
        foreach (var pair in assetSelection)
        {
            if (pair.Value && zeroReferenceAssets.Contains(pair.Key))
            {
                assetsToDelete.Add(pair.Key);
            }
        }
        
        if (assetsToDelete.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先选择要删除的资源", "确定");
            return;
        }
        
        if (EditorUtility.DisplayDialog("确认删除", 
            $"确定要删除选中的 {assetsToDelete.Count} 个资源吗？此操作不可撤销！", "删除", "取消"))
        {
            int successCount = 0;
            foreach (string assetPath in assetsToDelete)
            {
                if (AssetDatabase.DeleteAsset(assetPath))
                {
                    zeroReferenceAssets.Remove(assetPath);
                    referenceCounts.Remove(assetPath);
                    previewCache.Remove(assetPath);
                    assetSelection.Remove(assetPath);
                    successCount++;
                }
                else
                {
                    Debug.LogError($"删除失败: {assetPath}");
                }
            }
            
            Debug.Log($"已成功删除 {successCount} 个资源");
            ShowNotification(new GUIContent($"已删除 {successCount} 个资源"));
        }
    }

    private void DeleteAllZeroReferenceAssets()
    {
        if (zeroReferenceAssets.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有零引用资源可删除", "确定");
            return;
        }
        
        if (EditorUtility.DisplayDialog("彻底清理", 
            $"当前发现 {zeroReferenceAssets.Count} 个零引用资源。\n\n" +
            "将执行彻底清理：\n" +
            "• 删除所有零引用资源\n" +
            "• 自动重新检测\n" +
            "• 循环删除直到完全清理干净\n\n" +
            "此操作不可撤销！", "彻底清理", "取消"))
        {
            DeleteAllZeroReferenceAssetsDeep();
        }
    }

    /// <summary>
    /// 彻底清理 - 循环删除零引用资源直到完全清理干净
    /// 删除 → 重新检测 → 继续删除，直到没有新的零引用
    /// </summary>
    private void DeleteAllZeroReferenceAssetsDeep()
    {
        int totalDeletedCount = 0;
        int round = 0;
        int maxRounds = 10; // 防止无限循环

        try
        {
            while (round < maxRounds)
            {
                round++;

                // 如果没有零引用资源，退出循环
                if (zeroReferenceAssets.Count == 0)
                {
                    break;
                }

                int currentRoundCount = zeroReferenceAssets.Count;
                EditorUtility.DisplayProgressBar("彻底清理", 
                    $"第 {round} 轮：删除 {currentRoundCount} 个资源...", 
                    (float)round / maxRounds);

                // 批量删除当前所有零引用资源
                List<string> assetsToDelete = new List<string>(zeroReferenceAssets);
                int successCount = 0;

                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (string assetPath in assetsToDelete)
                    {
                        if (AssetDatabase.DeleteAsset(assetPath))
                        {
                            successCount++;
                        }
                        else
                        {
                            Debug.LogWarning($"[彻底清理] 删除失败: {assetPath}");
                        }
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                totalDeletedCount += successCount;
                Debug.Log($"[彻底清理] 第 {round} 轮：删除 {successCount}/{currentRoundCount} 个资源");

                // 如果没有成功删除任何资源，退出循环（避免死循环）
                if (successCount == 0)
                {
                    Debug.LogWarning("[彻底清理] 本轮没有成功删除任何资源，停止清理");
                    break;
                }

                // 刷新资源数据库
                AssetDatabase.Refresh();

                // 重新检测零引用资源（静默模式）
                EditorUtility.DisplayProgressBar("彻底清理", 
                    $"第 {round} 轮：重新检测零引用...", 
                    (float)round / maxRounds + 0.5f / maxRounds);

                CountAllReferencesAsyncSilent();

                // 如果没有新的零引用资源，退出循环
                if (zeroReferenceAssets.Count == 0)
                {
                    Debug.Log($"[彻底清理] 第 {round} 轮后没有新的零引用资源，清理完成");
                    break;
                }

                Debug.Log($"[彻底清理] 第 {round} 轮后发现 {zeroReferenceAssets.Count} 个新的零引用资源，继续清理...");
            }

            if (round >= maxRounds && zeroReferenceAssets.Count > 0)
            {
                Debug.LogWarning($"[彻底清理] 达到最大轮次 {maxRounds}，仍有 {zeroReferenceAssets.Count} 个零引用资源未清理");
            }

            ShowNotification(new GUIContent($"彻底清理完成！共删除 {totalDeletedCount} 个资源（{round}轮）"));
            Debug.Log($"[彻底清理] 完成！共执行 {round} 轮，删除 {totalDeletedCount} 个资源");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Repaint();
        }
    }

    /// <summary>
    /// 静默模式的零引用检测（不显示通知，用于彻底清理的循环检测）
    /// </summary>
    private void CountAllReferencesAsyncSilent()
    {
        referenceCounts.Clear();
        zeroReferenceAssets.Clear();
        previewCache.Clear();
        assetSelection.Clear();

        string[] allTargetPaths = GetAssetsInSelectedFolder();
        if (allTargetPaths.Length == 0)
        {
            return;
        }

        string[] allSourcePaths = GetFilteredAssetPaths();
        Dictionary<string, HashSet<string>> referencedByMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> targetSet = new HashSet<string>(allTargetPaths, StringComparer.OrdinalIgnoreCase);

        // 构建依赖关系图
        foreach (string sourcePath in allSourcePaths)
        {
            string[] dependencies;
            try
            {
                dependencies = AssetDatabase.GetDependencies(sourcePath, false);
            }
            catch
            {
                continue;
            }

            string normalizedSource = NormalizeAssetPath(sourcePath);

            foreach (string dep in dependencies)
            {
                string normalizedDep = NormalizeAssetPath(dep);
                if (targetSet.Contains(normalizedDep))
                {
                    if (!referencedByMap.ContainsKey(normalizedDep))
                    {
                        referencedByMap[normalizedDep] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }
                    referencedByMap[normalizedDep].Add(normalizedSource);
                }
            }
        }

        // 递归检测零引用
        HashSet<string> allZeroRefAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> simulatedDeleted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int round = 0;
        int maxRounds = 50;
        int newZeroRefCount;

        do
        {
            round++;
            newZeroRefCount = 0;

            foreach (string targetPath in allTargetPaths)
            {
                if (allZeroRefAssets.Contains(targetPath))
                    continue;

                int effectiveRefCount = 0;
                if (referencedByMap.TryGetValue(targetPath, out HashSet<string> referencers))
                {
                    foreach (string refPath in referencers)
                    {
                        if (!simulatedDeleted.Contains(refPath))
                        {
                            effectiveRefCount++;
                        }
                    }
                }

                if (effectiveRefCount == 0)
                {
                    allZeroRefAssets.Add(targetPath);
                    simulatedDeleted.Add(targetPath);
                    newZeroRefCount++;
                }
            }
        } while (newZeroRefCount > 0 && round < maxRounds);

        // 填充结果
        foreach (string path in allTargetPaths)
        {
            int refCount = 0;
            if (referencedByMap.TryGetValue(path, out HashSet<string> refs))
            {
                refCount = refs.Count(r => !allZeroRefAssets.Contains(r));
            }
            referenceCounts[path] = refCount;

            if (allZeroRefAssets.Contains(path))
            {
                zeroReferenceAssets.Add(path);
            }
        }
    }

    private Texture2D GetAssetPreview(string assetPath)
    {
        if (previewCache.TryGetValue(assetPath, out Texture2D cachedPreview))
        {
            return cachedPreview;
        }

        // 避免在 Layout 阶段调用预览生成，防止 CommandBuffer 错误
        if (Event.current != null && Event.current.type == EventType.Layout)
        {
            return null;
        }

        // 延迟加载预览，避免 CommandBuffer 错误
        try
        {
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
            {
                previewCache[assetPath] = null;
                return null;
            }

            // 优先使用 MiniThumbnail，更稳定且不触发 CommandBuffer 问题
            Texture2D preview = AssetPreview.GetMiniThumbnail(asset);
            previewCache[assetPath] = preview;
            return preview;
        }
        catch (System.Exception ex)
        {
            // 记录具体错误信息，便于调试
            Debug.LogWarning($"[RefCounter] 获取资源预览失败: {assetPath}, 错误: {ex.Message}");
            previewCache[assetPath] = null;
            return null;
        }
    }

    /// <summary>
    /// 获取被检测的资源列表（仅目标文件夹，不包含白名单）
    /// 白名单文件夹的资源不参与零引用检测，避免公共库被误删
    /// </summary>
    private string[] GetAssetsInSelectedFolder()
    {
        List<string> searchFolders = new List<string>();
        
        // 只添加主目标文件夹，不添加白名单
        // 白名单文件夹的资源不应该被检测是否零引用
        if (selectedFolder != null)
        {
            string folderPath = AssetDatabase.GetAssetPath(selectedFolder);
            if (!string.IsNullOrEmpty(folderPath) && AssetDatabase.IsValidFolder(folderPath))
            {
                searchFolders.Add(folderPath);
            }
        }
        
        // 必须设置目标文件夹才能执行零引用检测
        if (searchFolders.Count == 0)
        {
            Debug.LogWarning("请设置目标文件夹才能执行零引用检测（白名单文件夹不参与被检测）");
            return new string[0];
        }
        
        List<string> assetPaths = new List<string>();
        string[] foldersArray = searchFolders.ToArray();
        
        // 动画组：AnimationClip, AnimatorController, AnimatorOverrideController, Avatar, Timeline
        if (includeAnimationGroup)
        {
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:AnimationClip", foldersArray));
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:AnimatorController", foldersArray));
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:AnimatorOverrideController", foldersArray));
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:Avatar", foldersArray));
#if UNITY_TIMELINE
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:TimelineAsset", foldersArray));
#endif
        }
        
        // 贴图材质组：Texture2D, Material, Shader, Sprite, RenderTexture
        if (includeTextureMaterialGroup)
        {
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:Texture2D", foldersArray));
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:Material", foldersArray));
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:Shader", foldersArray));
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:Sprite", foldersArray));
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:RenderTexture", foldersArray));
        }
        
        // 模型组：Mesh
        if (includeMeshGroup)
        {
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:Mesh", foldersArray));
        }
        
        // 音视频组：AudioClip, VideoClip
        if (includeAudioVideoGroup)
        {
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:AudioClip", foldersArray));
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:VideoClip", foldersArray));
        }
        
        // 预制体组：Prefab
        if (includePrefabGroup)
        {
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:Prefab", foldersArray));
        }
        
        // 其他组：Font, SpriteAtlas, ScriptableObject, PhysicMaterial, TerrainData
        if (includeOtherGroup)
        {
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:Font", foldersArray));
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:SpriteAtlas", foldersArray));
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:ScriptableObject", foldersArray));
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:PhysicMaterial", foldersArray));
            assetPaths.AddRange(GetAssetsOfTypeInFolders("t:TerrainData", foldersArray));
        }
        
        return assetPaths.Distinct().ToArray();
    }

    private string[] GetAssetsOfType(string filter, string folderPath)
    {
        return AssetDatabase.FindAssets(filter, new[] { folderPath })
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .ToArray();
    }
    
    private string[] GetAssetsOfTypeInFolders(string filter, string[] folderPaths)
    {
        return AssetDatabase.FindAssets(filter, folderPaths)
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .ToArray();
    }

    private string[] GetAssetsByType()
    {
        List<string> filters = new List<string>();
        
        // 动画组
        if (includeAnimationGroup)
        {
            filters.Add("t:AnimationClip");
            filters.Add("t:AnimatorController");
            filters.Add("t:AnimatorOverrideController");
            filters.Add("t:Avatar");
#if UNITY_TIMELINE
            filters.Add("t:TimelineAsset");
#endif
        }
        
        // 贴图材质组
        if (includeTextureMaterialGroup)
        {
            filters.Add("t:Texture2D");
            filters.Add("t:Material");
            filters.Add("t:Shader");
            filters.Add("t:Sprite");
            filters.Add("t:RenderTexture");
        }
        
        // 模型组
        if (includeMeshGroup)
        {
            filters.Add("t:Mesh");
        }
        
        // 音视频组
        if (includeAudioVideoGroup)
        {
            filters.Add("t:AudioClip");
            filters.Add("t:VideoClip");
        }
        
        // 预制体组
        if (includePrefabGroup)
        {
            filters.Add("t:Prefab");
        }
        
        // 其他组
        if (includeOtherGroup)
        {
            filters.Add("t:Font");
            filters.Add("t:SpriteAtlas");
            filters.Add("t:ScriptableObject");
            filters.Add("t:PhysicMaterial");
            filters.Add("t:TerrainData");
        }
        
        return filters.Count == 0 ? new string[0] : 
            AssetDatabase.FindAssets(string.Join(" ", filters))
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .ToArray();
    }

    /// <summary>
    /// 获取引用来源文件列表（目标文件夹 + 白名单文件夹）
    /// 白名单文件夹里的资源可以作为引用来源，这样白名单里的 Prefab 
    /// 如果引用了目标文件夹的贴图，这个贴图的引用计数会正确 +1
    /// </summary>
    private string[] GetFilteredAssetPaths()
    {
        List<string> searchFolders = new List<string>();
        
        // 添加主目标文件夹
        if (selectedFolder != null)
        {
            string folderPath = AssetDatabase.GetAssetPath(selectedFolder);
            if (!string.IsNullOrEmpty(folderPath))
            {
                searchFolders.Add(folderPath);
            }
        }
        
        // 添加白名单文件夹到引用来源范围
        // 这样白名单里的资源如果引用了目标文件夹的资源，会被正确计数
        if (whitelistFolderAssets != null)
        {
            foreach (DefaultAsset folderAsset in whitelistFolderAssets)
            {
                string path = GetFolderPath(folderAsset);
                if (!string.IsNullOrEmpty(path) && !searchFolders.Contains(path))
                {
                    searchFolders.Add(path);
                }
            }
        }
        
        // 必须设置至少一个有效文件夹
        if (searchFolders.Count == 0)
        {
            Debug.LogWarning("没有有效的搜索文件夹");
            return new string[0];
        }
        
        return AssetDatabase.FindAssets("", searchFolders.ToArray())
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Where(p => p.StartsWith("Assets/") && !p.EndsWith(".cs") && !p.Contains("/Editor/"))
            .ToArray();
    }

    private void CountReferencesForSelected()
    {
        if (selectedFolder == null)
        {
            EditorUtility.DisplayDialog("错误", "请先指定目标文件夹（被检测的资源范围）", "确定");
            return;
        }
        
        referenceCounts.Clear();
        zeroReferenceAssets.Clear();
        previewCache.Clear();
        assetSelection.Clear();
        
        UnityEngine.Object[] selectedObjects = Selection.objects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            Debug.LogWarning("请先在Project窗口中选择资源");
            return;
        }

        var filteredSelection = selectedObjects
            .Where(obj => IsObjectInSelectedGroups(obj))
            .ToArray();

        if (filteredSelection.Length == 0)
        {
            Debug.LogWarning("选中的资源中没有符合类型的资源");
            return;
        }

        foreach (UnityEngine.Object obj in filteredSelection)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;
            
            int count = GetReferenceCount(path);
            referenceCounts[path] = count;
            if (count == 0) zeroReferenceAssets.Add(path);
        }
        
        Debug.Log($"已统计 {filteredSelection.Length} 个资源的引用情况");
    }

    /// <summary>
    /// 深度零引用检测 - 递归检测所有孤立依赖链
    /// 模拟删除零引用资源后，继续检测新产生的零引用，直到没有新的零引用
    /// 一次性找出所有需要清理的资源，无需多次执行
    /// </summary>
    private void CountAllReferencesAsync()
    {
        if (selectedFolder == null)
        {
            EditorUtility.DisplayDialog("错误", "请先指定目标文件夹（被检测的资源范围）", "确定");
            return;
        }
        
        referenceCounts.Clear();
        zeroReferenceAssets.Clear();
        previewCache.Clear();
        assetSelection.Clear();
        
        // 获取目标资源（被检测的资源）
        string[] allTargetPaths = GetAssetsInSelectedFolder();
        if (allTargetPaths.Length == 0)
        {
            Debug.LogWarning("在指定范围内未找到符合类型的资源");
            return;
        }

        // 获取引用来源（目标文件夹 + 白名单）
        string[] allSourcePaths = GetFilteredAssetPaths();
        int totalSources = allSourcePaths.Length;

        // 构建完整的依赖关系图
        // Key: 资源路径, Value: 引用它的资源列表
        Dictionary<string, HashSet<string>> referencedByMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        // 目标资源集合（用于快速查找）
        HashSet<string> targetSet = new HashSet<string>(allTargetPaths, StringComparer.OrdinalIgnoreCase);

        try
        {
            // ========== 阶段1：构建依赖关系图 ==========
            for (int i = 0; i < allSourcePaths.Length; i++)
            {
                string sourcePath = allSourcePaths[i];

                if (i % 20 == 0 || i == 0 || i == allSourcePaths.Length - 1)
                {
                    float progress = (float)i / totalSources * 0.5f;
                    if (EditorUtility.DisplayCancelableProgressBar("深度零引用检测",
                        $"构建依赖图 ({i + 1}/{totalSources}): {Path.GetFileName(sourcePath)}", progress))
                    {
                        Debug.Log("[资源引用工具] 用户取消了检测");
                        EditorUtility.ClearProgressBar();
                        return;
                    }
                }

                string[] dependencies;
                try
                {
                    dependencies = AssetDatabase.GetDependencies(sourcePath, false);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[资源引用工具] 获取依赖失败: {sourcePath}, 错误: {ex.Message}");
                    continue;
                }

                string normalizedSource = NormalizeAssetPath(sourcePath);

                foreach (string dep in dependencies)
                {
                    string normalizedDep = NormalizeAssetPath(dep);

                    // 只统计目标资源的被引用情况
                    if (targetSet.Contains(normalizedDep))
                    {
                        if (!referencedByMap.ContainsKey(normalizedDep))
                        {
                            referencedByMap[normalizedDep] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        }
                        referencedByMap[normalizedDep].Add(normalizedSource);
                    }
                }
            }

            // ========== 阶段2：递归检测零引用 ==========
            HashSet<string> allZeroRefAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> simulatedDeleted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int round = 0;
            int maxRounds = 50; // 防止无限循环
            int newZeroRefCount;

            do
            {
                round++;
                newZeroRefCount = 0;

                float baseProgress = 0.5f + (float)round / maxRounds * 0.4f;
                if (EditorUtility.DisplayCancelableProgressBar("深度零引用检测",
                    $"递归检测第 {round} 轮...", baseProgress))
                {
                    Debug.Log("[资源引用工具] 用户取消了检测");
                    EditorUtility.ClearProgressBar();
                    return;
                }

                // 检测当前轮次的零引用资源
                foreach (string targetPath in allTargetPaths)
                {
                    // 跳过已经被标记为零引用的
                    if (allZeroRefAssets.Contains(targetPath))
                        continue;

                    // 计算有效引用数（排除已模拟删除的资源）
                    int effectiveRefCount = 0;
                    if (referencedByMap.TryGetValue(targetPath, out HashSet<string> referencers))
                    {
                        foreach (string refPath in referencers)
                        {
                            if (!simulatedDeleted.Contains(refPath))
                            {
                                effectiveRefCount++;
                            }
                        }
                    }

                    if (effectiveRefCount == 0)
                    {
                        allZeroRefAssets.Add(targetPath);
                        simulatedDeleted.Add(targetPath);
                        newZeroRefCount++;
                    }
                }

            } while (newZeroRefCount > 0 && round < maxRounds);

            // ========== 阶段3：填充结果 ==========
            foreach (string path in allTargetPaths)
            {
                int refCount = 0;
                if (referencedByMap.TryGetValue(path, out HashSet<string> refs))
                {
                    // 计算最终有效引用数（排除所有零引用资源）
                    refCount = refs.Count(r => !allZeroRefAssets.Contains(r));
                }
                referenceCounts[path] = refCount;

                if (allZeroRefAssets.Contains(path))
                {
                    zeroReferenceAssets.Add(path);
                }
            }

            ShowNotification(new GUIContent($"深度检测完成！找到 {zeroReferenceAssets.Count} 个孤立资源（{round}轮）"));
            Debug.Log($"[深度零引用检测] 完成！递归 {round} 轮，找到 {zeroReferenceAssets.Count} 个孤立资源");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Repaint();
    }

    private void MarkZeroReferenceAssets()
    {
        zeroReferenceAssets.Clear();
        foreach (var pair in referenceCounts)
        {
            if (pair.Value == 0) zeroReferenceAssets.Add(pair.Key);
        }
        
        ShowNotification(new GUIContent($"找到 {zeroReferenceAssets.Count} 个零引用资源"));
        Debug.Log($"标记完成！找到 {zeroReferenceAssets.Count} 个零引用资源");
    }

    private void ExportCleanupReport()
    {
        string reportPath = EditorUtility.SaveFilePanel("保存清理报告", "", "ZeroReferenceReport", "csv");
        if (string.IsNullOrEmpty(reportPath)) return;

        try
        {
            using (StreamWriter sw = new StreamWriter(reportPath))
            {
                sw.WriteLine("Path,Type,Size,LastModified");
                foreach (string assetPath in zeroReferenceAssets)
                {
                    FileInfo fileInfo = new FileInfo(assetPath);
                    string assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath)?.ToString() ?? "Unknown";
                    long fileSize = fileInfo.Exists ? fileInfo.Length : 0;
                    DateTime lastModified = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.Now;
                    
                    sw.WriteLine($"\"{assetPath}\",{assetType},{fileSize},{lastModified:yyyy-MM-dd HH:mm:ss}");
                }
            }
            Debug.Log($"清理报告已导出到: {reportPath}");
            EditorUtility.RevealInFinder(reportPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"导出报告失败: {e.Message}");
        }
    }

    private int GetReferenceCount(string targetPath)
    {
        int count = 0;
        foreach (string assetPath in GetFilteredAssetPaths())
        {
            if (AssetDatabase.GetDependencies(assetPath, false).Contains(targetPath))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// 判断对象是否属于当前选中的资源分组
    /// </summary>
    private bool IsObjectInSelectedGroups(UnityEngine.Object obj)
    {
        if (obj == null) return false;
        
        // 动画组：AnimationClip, AnimatorController, AnimatorOverrideController, Avatar, Timeline
        if (includeAnimationGroup)
        {
            if (obj is AnimationClip) return true;
            if (obj is UnityEditor.Animations.AnimatorController) return true;
            if (obj is AnimatorOverrideController) return true;
            if (obj is Avatar) return true;
#if UNITY_TIMELINE
            if (obj is TimelineAsset) return true;
#endif
        }
        
        // 贴图材质组：Texture2D, Material, Shader, Sprite, RenderTexture
        if (includeTextureMaterialGroup)
        {
            if (obj is Texture2D) return true;
            if (obj is Material) return true;
            if (obj is Shader) return true;
            if (obj is Sprite) return true;
            if (obj is RenderTexture) return true;
        }
        
        // 模型组：Mesh
        if (includeMeshGroup)
        {
            if (obj is Mesh) return true;
        }
        
        // 音视频组：AudioClip, VideoClip
        if (includeAudioVideoGroup)
        {
            if (obj is AudioClip) return true;
            if (obj is VideoClip) return true;
        }
        
        // 预制体组：Prefab (GameObject)
        if (includePrefabGroup)
        {
            if (obj is GameObject) return true;
        }
        
        // 其他组：Font, SpriteAtlas, ScriptableObject, PhysicMaterial, TerrainData
        if (includeOtherGroup)
        {
            if (obj is Font) return true;
            if (obj is SpriteAtlas) return true;
            if (obj is PhysicMaterial) return true;
            if (obj is TerrainData) return true;
            // ScriptableObject 需要排除其他已归类的类型
            if (obj is ScriptableObject && 
                !(obj is AnimationClip) && 
                !(obj is UnityEditor.Animations.AnimatorController) &&
                !(obj is AnimatorOverrideController))
            {
                return true;
            }
        }
        
        return false;
    }



    private void DrawFolderList(string title, List<DefaultAsset> folders, string addButtonLabel, Action onChangedCallback = null, bool showClearButton = false)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        bool listChanged = false;
        for (int i = 0; i < folders.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            DefaultAsset updatedFolder = (DefaultAsset)EditorGUILayout.ObjectField(folders[i], typeof(DefaultAsset), false);
            if (updatedFolder != folders[i])
            {
                folders[i] = updatedFolder;
                listChanged = true;
            }
            if (GUILayout.Button("移除", GUILayout.Width(50)))
            {
                folders.RemoveAt(i);
                GUI.FocusControl(null);
                listChanged = true;
                EditorGUILayout.EndHorizontal();
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
        
        // 按钮水平排列
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(addButtonLabel, GUILayout.Height(20)))
        {
            folders.Add(null);
            listChanged = true;
        }
        if (showClearButton && folders.Count > 0)
        {
            if (GUILayout.Button("清空白名单", GUILayout.Height(20)))
            {
                folders.Clear();
                onChangedCallback?.Invoke();
                GUI.FocusControl(null);
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUI.indentLevel--;
        if (listChanged)
        {
            onChangedCallback?.Invoke();
        }
    }

    private void EnsureFolderListInitialized(ref List<DefaultAsset> folderList)
    {
        if (folderList == null)
        {
            folderList = new List<DefaultAsset>();
        }
    }

    /// <summary>
    /// 检查指定文件夹路径是否已在白名单中
    /// Check if a folder path is already in the whitelist
    /// </summary>
    private bool IsFolderInWhitelist(string folderPath)
    {
        if (whitelistFolderAssets == null || string.IsNullOrEmpty(folderPath)) return false;
        foreach (DefaultAsset folderAsset in whitelistFolderAssets)
        {
            if (folderAsset == null) continue;
            string path = AssetDatabase.GetAssetPath(folderAsset);
            if (string.Equals(path, folderPath, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 将文件夹路径加入白名单
    /// Add a folder path to the whitelist
    /// </summary>
    private void AddFolderToWhitelist(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath)) return;
        if (IsFolderInWhitelist(folderPath)) return;

        EnsureFolderListInitialized(ref whitelistFolderAssets);
        DefaultAsset folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
        if (folderAsset != null)
        {
            whitelistFolderAssets.Add(folderAsset);
            SaveWhitelistToPrefs();
            Debug.Log($"[资源引用工具] 已将 {folderPath} 加入白名单");
            Repaint();
        }
    }

    private void SaveWhitelistToPrefs()
    {
        EnsureFolderListInitialized(ref whitelistFolderAssets);
        List<string> whitelistPaths = new List<string>();
        foreach (DefaultAsset folderAsset in whitelistFolderAssets)
        {
            string folderPath = GetFolderPath(folderAsset);
            if (!string.IsNullOrEmpty(folderPath))
            {
                whitelistPaths.Add(folderPath);
            }
        }

        if (whitelistPaths.Count == 0)
        {
            EditorPrefs.DeleteKey(WhitelistPrefsKey);
            return;
        }

        StringListWrapper wrapper = new StringListWrapper { Items = whitelistPaths };
        EditorPrefs.SetString(WhitelistPrefsKey, JsonUtility.ToJson(wrapper));
    }

    private void LoadWhitelistFromPrefs()
    {
        EnsureFolderListInitialized(ref whitelistFolderAssets);
        whitelistFolderAssets.Clear();
        
        // 安全检查：延迟加载，避免在 AssetDatabase 初始化时卡死
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += LoadWhitelistFromPrefsDelayed;
        }
    }
    
    private void LoadWhitelistFromPrefsDelayed()
    {
        if (whitelistFolderAssets == null)
        {
            whitelistFolderAssets = new List<DefaultAsset>();
        }
        
        string storedJson = EditorPrefs.GetString(WhitelistPrefsKey, string.Empty);
        if (string.IsNullOrEmpty(storedJson))
        {
            return;
        }

        try
        {
            StringListWrapper wrapper = JsonUtility.FromJson<StringListWrapper>(storedJson);
            if (wrapper?.Items == null)
            {
                return;
            }

            foreach (string folderPath in wrapper.Items)
            {
                if (string.IsNullOrEmpty(folderPath))
                {
                    continue;
                }

                // 安全检查：先验证路径是否有效
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    Debug.LogWarning($"[资源引用工具] 白名单文件夹路径无效，已跳过: {folderPath}");
                    continue;
                }

                DefaultAsset folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
                if (folderAsset != null)
                {
                    whitelistFolderAssets.Add(folderAsset);
                }
            }
            
            Repaint();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[资源引用工具] 白名单文件夹加载失败: {exception.Message}");
            // 清除损坏的数据
            EditorPrefs.DeleteKey(WhitelistPrefsKey);
        }
    }

    private void RunOneClickConsolidation()
    {
        if (!hasDependencyScanResults)
        {
            EditorUtility.DisplayDialog("提示", "请先完成依赖扫描", "确定");
            return;
        }

        string mainFolderPath = lastScanMainFolderPath;
        if (string.IsNullOrEmpty(mainFolderPath))
        {
            EditorUtility.DisplayDialog("提示", "主文件夹信息缺失，请重新扫描", "确定");
            return;
        }

        List<DependencyRecord> targets = externalDependencyRecords.ToList();
        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有可整合的外部依赖", "确定");
            return;
        }

        isConsolidating = true;
        consolidationStatusMessage = null;
        consolidationLogs.Clear();

        int copiedCount = 0;
        int referenceUpdates = 0;
        int skippedCount = 0;

        // 两阶段整合：先复制所有资源并记录映射，再统一更新引用
        // 这样可以确保在更新引用时，所有新资源的 GUID 都已正确生成
        var copyMappings = new List<CopyMapping>();

        try
        {
            // ========== 阶段一：复制所有资源 ==========
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < targets.Count; i++)
            {
                DependencyRecord record = targets[i];
                EditorUtility.DisplayProgressBar("整合资源 - 阶段1/2 复制", $"({i + 1}/{targets.Count}): {Path.GetFileName(record.AssetPath)}", (float)(i + 1) / targets.Count);

                string absoluteSourcePath = ConvertAssetPathToAbsolutePath(record.AssetPath);
                if (string.IsNullOrEmpty(absoluteSourcePath) || !File.Exists(absoluteSourcePath))
                {
                    skippedCount++;
                    AppendConsolidationLog($"[跳过] 无法访问资源 {record.AssetPath}");
                    continue;
                }

                string targetFolder = BuildTypedTargetFolder(mainFolderPath, record.AssetType);
                EnsureAssetFolderExists(targetFolder);
                string desiredTargetPath = NormalizeAssetPath($"{targetFolder}/{Path.GetFileName(record.AssetPath)}");
                string finalTargetPath = GenerateUniqueAssetPath(desiredTargetPath);

                // 记录原始 GUID（在复制前获取，确保准确）
                string oldGuid = AssetDatabase.AssetPathToGUID(record.AssetPath);

                if (!AssetDatabase.CopyAsset(record.AssetPath, finalTargetPath))
                {
                    skippedCount++;
                    AppendConsolidationLog($"[失败] 复制 {record.AssetPath} -> {finalTargetPath}");
                    continue;
                }

                copiedCount++;
                AppendConsolidationLog($"[复制] {record.AssetPath} -> {finalTargetPath}");

                // 记录映射，用于阶段二更新引用
                copyMappings.Add(new CopyMapping
                {
                    OldPath = record.AssetPath,
                    NewPath = finalTargetPath,
                    OldGuid = oldGuid,
                    ReferencedBy = record.ReferencedBy != null ? new List<string>(record.ReferencedBy) : new List<string>(),
                    NestedExternalDependencies = record.NestedExternalDependencies != null ? new List<string>(record.NestedExternalDependencies) : new List<string>()
                });
            }
        }
        catch (Exception exception)
        {
            AppendConsolidationLog($"[阶段1错误] {exception.Message}");
            Debug.LogError($"一键整合资源阶段1失败: {exception}");
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        // 刷新资源数据库，确保所有新资源的 GUID 都已正确生成
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        try
        {
            // ========== 阶段二：更新引用 ==========
            Dictionary<string, CopyMapping> pathToMapping = copyMappings
                .Where(mapping => !string.IsNullOrEmpty(mapping.OldPath))
                .ToDictionary(mapping => mapping.OldPath, mapping => mapping, StringComparer.OrdinalIgnoreCase);

            Dictionary<string, string> guidReplacementMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (CopyMapping mapping in copyMappings)
            {
                mapping.NewGuid = AssetDatabase.AssetPathToGUID(mapping.NewPath);
                if (string.IsNullOrEmpty(mapping.OldGuid) || string.IsNullOrEmpty(mapping.NewGuid))
                {
                    AppendConsolidationLog($"[跳过引用更新] GUID 获取失败: old={mapping.OldGuid}, new={mapping.NewGuid}, path={mapping.NewPath}");
                    continue;
                }

                guidReplacementMap[mapping.OldGuid] = mapping.NewGuid;
                AppendConsolidationLog($"[GUID映射] {mapping.OldGuid} -> {mapping.NewGuid} ({Path.GetFileName(mapping.NewPath)})");
            }

            AppendConsolidationLog($"[GUID映射总计] 共 {guidReplacementMap.Count} 个 GUID 映射");

            Dictionary<string, HashSet<string>> assetRewriteLookup = BuildAssetRewriteLookup(copyMappings, pathToMapping);

            List<string> rewriteTargets = assetRewriteLookup.Keys
                .Where(assetPath => !string.IsNullOrEmpty(assetPath))
                .OrderBy(assetPath => assetPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < rewriteTargets.Count; i++)
            {
                string assetPath = rewriteTargets[i];
                EditorUtility.DisplayProgressBar("整合资源 - 阶段2/2 更新引用", $"({i + 1}/{rewriteTargets.Count}): {Path.GetFileName(assetPath)}", (float)(i + 1) / rewriteTargets.Count);

                if (!IsTextSerializedAsset(assetPath))
                {
                    AppendConsolidationLog($"[跳过引用] {assetPath} 非文本序列化");
                    continue;
                }

                if (!assetRewriteLookup.TryGetValue(assetPath, out HashSet<string> oldGuids) || oldGuids.Count == 0)
                {
                    continue;
                }

                Dictionary<string, string> replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string oldGuid in oldGuids)
                {
                    if (guidReplacementMap.TryGetValue(oldGuid, out string newGuid))
                    {
                        replacements[oldGuid] = newGuid;
                    }
                }

                if (replacements.Count == 0)
                {
                    continue;
                }

                AppendConsolidationLog($"[准备替换] {Path.GetFileName(assetPath)}: 需要替换 {replacements.Count} 个 GUID");
                if (ReplaceMultipleGuidsInTextAsset(assetPath, replacements))
                {
                    referenceUpdates++;
                    AppendConsolidationLog($"[更新引用成功] {Path.GetFileName(assetPath)}: {string.Join(", ", replacements.Select(pair => $"{pair.Key} -> {pair.Value}"))}");
                }
                else
                {
                    AppendConsolidationLog($"[更新引用失败] {Path.GetFileName(assetPath)}: 替换未生效");
                }
            }
        }
        catch (Exception exception)
        {
            AppendConsolidationLog($"[阶段2错误] {exception.Message}");
            Debug.LogError($"一键整合资源阶段2失败: {exception}");
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
            isConsolidating = false;
        }

        consolidationStatusMessage = $"复制 {copiedCount} 个资源，更新 {referenceUpdates} 个资产引用（含嵌套），跳过 {skippedCount} 个。建议重新扫描以确认依赖状态。";
        ShowNotification(new GUIContent("整合完成"));
    }

    /// <summary>
    /// 复制映射记录，用于两阶段整合
    /// </summary>
    private class CopyMapping
    {
        public string OldPath;
        public string NewPath;
        public string OldGuid;
        public string NewGuid;
        public List<string> ReferencedBy;
        public List<string> NestedExternalDependencies;
    }

    private Dictionary<string, HashSet<string>> BuildAssetRewriteLookup(IEnumerable<CopyMapping> mappings, Dictionary<string, CopyMapping> pathLookup)
    {
        Dictionary<string, HashSet<string>> lookup = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        
        // 收集所有旧 GUID，用于后续检查新复制资源之间的相互引用
        HashSet<string> allOldGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<CopyMapping> mappingList = new List<CopyMapping>();
        foreach (CopyMapping mapping in mappings)
        {
            if (mapping != null)
            {
                mappingList.Add(mapping);
                if (!string.IsNullOrEmpty(mapping.OldGuid))
                {
                    allOldGuids.Add(mapping.OldGuid);
                }
            }
        }
        
        AppendConsolidationLog($"[GUID收集] 共收集 {allOldGuids.Count} 个旧 GUID，共 {mappingList.Count} 个复制映射");
        
        // 输出所有 GUID 映射详情，便于调试
        foreach (CopyMapping mapping in mappingList)
        {
            Debug.Log($"[RefCounter GUID详情] OldPath={mapping.OldPath}, OldGuid={mapping.OldGuid}, NewPath={mapping.NewPath}");
        }
        
        foreach (CopyMapping mapping in mappingList)
        {
            // 1. 处理主文件夹内引用外部资源的文件（原有逻辑）
            if (mapping.ReferencedBy != null)
            {
                foreach (string referencingAsset in mapping.ReferencedBy)
                {
                    RegisterGuidRewriteTarget(lookup, referencingAsset, mapping.OldGuid);
                }
            }

            // 2. 【核心修复】处理新复制的资源本身 - 它可能引用了其他被复制的外部资源
            // 例如：复制的 Material 引用了复制的 Texture，Material 内的 Texture GUID 需要更新
            if (!string.IsNullOrEmpty(mapping.NewPath) && IsTextSerializedAsset(mapping.NewPath))
            {
                string absolutePath = ConvertAssetPathToAbsolutePath(mapping.NewPath);
                Debug.Log($"[RefCounter 检测] 检查新复制资源: {mapping.NewPath}");
                if (!string.IsNullOrEmpty(absolutePath) && File.Exists(absolutePath))
                {
                    try
                    {
                        string content = File.ReadAllText(absolutePath);
                        Debug.Log($"[RefCounter 文件内容长度] {Path.GetFileName(mapping.NewPath)}: {content.Length} 字符");
                        
                        // 输出文件前500字符用于调试
                        if (content.Length > 0)
                        {
                            string preview = content.Length > 500 ? content.Substring(0, 500) : content;
                            Debug.Log($"[RefCounter 文件预览] {Path.GetFileName(mapping.NewPath)}:\n{preview}");
                        }
                        
                        int foundCount = 0;
                        foreach (string oldGuid in allOldGuids)
                        {
                            // 跳过自身的 GUID（不需要自己替换自己）
                            if (string.Equals(oldGuid, mapping.OldGuid, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                            
                            // 直接搜索 GUID（大小写不敏感）
                            if (content.IndexOf(oldGuid, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                RegisterGuidRewriteTarget(lookup, mapping.NewPath, oldGuid);
                                Debug.Log($"[RefCounter 发现引用] {Path.GetFileName(mapping.NewPath)} 包含 GUID: {oldGuid}");
                                AppendConsolidationLog($"[外部引用检测] {Path.GetFileName(mapping.NewPath)} 引用了外部资源 GUID: {oldGuid}");
                                foundCount++;
                            }
                        }
                        if (foundCount == 0)
                        {
                            Debug.Log($"[RefCounter 无引用] {Path.GetFileName(mapping.NewPath)} 未发现其他外部资源引用（检查了 {allOldGuids.Count - 1} 个 GUID）");
                            AppendConsolidationLog($"[外部引用检测] {Path.GetFileName(mapping.NewPath)} 未发现其他外部资源引用（检查了 {allOldGuids.Count - 1} 个 GUID）");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[RefCounter] 检查资源引用失败: {mapping.NewPath}, 错误: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[RefCounter] 无法读取新复制的资源: {mapping.NewPath}, absolutePath={absolutePath}, exists={File.Exists(absolutePath ?? "")}");
                    AppendConsolidationLog($"[警告] 无法读取新复制的资源: {mapping.NewPath}");
                }
            }
            else if (!string.IsNullOrEmpty(mapping.NewPath))
            {
                Debug.Log($"[RefCounter 跳过] {mapping.NewPath} 非文本序列化资源，扩展名: {Path.GetExtension(mapping.NewPath)}");
            }

            // 3. 处理嵌套外部依赖（原有逻辑保留）
            if (mapping.NestedExternalDependencies != null && mapping.NestedExternalDependencies.Count > 0)
            {
                AppendConsolidationLog($"[嵌套依赖] {mapping.OldPath} 有 {mapping.NestedExternalDependencies.Count} 个嵌套依赖");
                foreach (string nestedPath in mapping.NestedExternalDependencies)
                {
                    if (pathLookup != null && pathLookup.TryGetValue(nestedPath, out CopyMapping nestedMapping))
                    {
                        RegisterGuidRewriteTarget(lookup, mapping.NewPath, nestedMapping.OldGuid);
                        AppendConsolidationLog($"[嵌套依赖注册] {mapping.NewPath} 需要更新 {nestedPath} 的 GUID: {nestedMapping.OldGuid}");
                    }
                }
            }
        }

        AppendConsolidationLog($"[重写目标] 共 {lookup.Count} 个资源需要更新引用");
        Debug.Log($"[RefCounter 重写目标] 共 {lookup.Count} 个资源需要更新引用");
        foreach (var kvp in lookup)
        {
            Debug.Log($"[RefCounter 重写目标详情] {kvp.Key}: 需更新 {kvp.Value.Count} 个 GUID ({string.Join(", ", kvp.Value)})");
        }
        return lookup;
    }

    private void RegisterGuidRewriteTarget(Dictionary<string, HashSet<string>> lookup, string assetPath, string oldGuid)
    {
        if (lookup == null || string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(oldGuid))
        {
            return;
        }

        if (IsShaderAsset(assetPath))
        {
            return;
        }

        string normalizedPath = NormalizeAssetPath(assetPath);
        if (string.IsNullOrEmpty(normalizedPath) || !normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!lookup.TryGetValue(normalizedPath, out HashSet<string> guidSet))
        {
            guidSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            lookup.Add(normalizedPath, guidSet);
        }

        guidSet.Add(oldGuid);
    }

    private bool ReplaceMultipleGuidsInTextAsset(string assetPath, IReadOnlyDictionary<string, string> guidMap)
    {
        if (guidMap == null || guidMap.Count == 0)
        {
            return false;
        }

        string absolutePath = ConvertAssetPathToAbsolutePath(assetPath);
        if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
        {
            AppendConsolidationLog($"[替换失败] 文件不存在: {assetPath}");
            return false;
        }

        string content = File.ReadAllText(absolutePath);
        bool hasChanges = false;

        foreach (KeyValuePair<string, string> pair in guidMap)
        {
            if (TryReplaceGuidTokens(ref content, pair.Key, pair.Value))
            {
                hasChanges = true;
                AppendConsolidationLog($"[GUID替换] {Path.GetFileName(assetPath)}: {pair.Key} -> {pair.Value}");
            }
            else
            {
                AppendConsolidationLog($"[GUID未找到] {Path.GetFileName(assetPath)}: 未找到 {pair.Key}");
            }
        }

        if (!hasChanges)
        {
            AppendConsolidationLog($"[替换跳过] {Path.GetFileName(assetPath)}: 无任何 GUID 被替换");
            return false;
        }

        File.WriteAllText(absolutePath, content);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        return true;
    }

    private bool TryReplaceGuidTokens(ref string content, string oldGuid, string newGuid)
    {
        if (string.IsNullOrEmpty(oldGuid) || string.IsNullOrEmpty(newGuid) || string.IsNullOrEmpty(content))
        {
            return false;
        }
        
        // 使用大小写不敏感的检查
        int foundIndex = content.IndexOf(oldGuid, StringComparison.OrdinalIgnoreCase);
        if (foundIndex < 0)
        {
            Debug.Log($"[RefCounter 替换] 在内容中未找到 GUID: {oldGuid}");
            return false;
        }
        
        Debug.Log($"[RefCounter 替换] 在内容位置 {foundIndex} 找到 GUID: {oldGuid}，准备替换为: {newGuid}");
        
        // 显示 GUID 周围的上下文
        int contextStart = Math.Max(0, foundIndex - 50);
        int contextEnd = Math.Min(content.Length, foundIndex + oldGuid.Length + 50);
        string context = content.Substring(contextStart, contextEnd - contextStart);
        Debug.Log($"[RefCounter 替换上下文] ...{context}...");

        bool replaced = false;
        string escapedOldGuid = Regex.Escape(oldGuid);

        // 模式1: fileID: xxx, guid: xxx, type: x 格式
        string pattern1 = $"(fileID:\\s*-?\\d+,\\s*guid:\\s*){escapedOldGuid}(\\s*,\\s*type:\\s*\\d+)";
        content = Regex.Replace(
            content,
            pattern1,
            match =>
            {
                replaced = true;
                Debug.Log($"[RefCounter 替换成功] 模式1匹配: {match.Value}");
                return $"{match.Groups[1].Value}{newGuid}{match.Groups[2].Value}";
            },
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        if (replaced)
        {
            return true;
        }

        // 模式2: guid: xxx 格式（更宽松）
        string pattern2 = $"(guid:\\s*){escapedOldGuid}";
        content = Regex.Replace(
            content,
            pattern2,
            match =>
            {
                replaced = true;
                Debug.Log($"[RefCounter 替换成功] 模式2匹配: {match.Value}");
                return $"{match.Groups[1].Value}{newGuid}";
            },
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        if (replaced)
        {
            return true;
        }
        
        // 模式3: 直接替换所有出现的 GUID（最宽松，作为后备）
        Debug.Log($"[RefCounter 替换] 模式1和2均未匹配，尝试直接替换");
        string newContent = Regex.Replace(content, escapedOldGuid, newGuid, RegexOptions.IgnoreCase);
        if (newContent != content)
        {
            content = newContent;
            replaced = true;
            Debug.Log($"[RefCounter 替换成功] 模式3（直接替换）成功");
        }

        return replaced;
    }

    private string BuildTypedTargetFolder(string mainFolderPath, DependencyAssetType assetType)
    {
        if (string.IsNullOrEmpty(mainFolderPath))
        {
            return mainFolderPath;
        }

        string folderName = GetFolderNameForType(assetType);
        return NormalizeAssetPath($"{mainFolderPath}/{folderName}");
    }

    private string GetFolderNameForType(DependencyAssetType assetType)
    {
        // 优先使用自定义名称
        if (customFolderNames != null && customFolderNames.TryGetValue(assetType, out string customName) && !string.IsNullOrWhiteSpace(customName))
        {
            return customName;
        }

        // 回退到默认名称
        if (AssetTypeToFolderName.TryGetValue(assetType, out string defaultName))
        {
            return defaultName;
        }

        return AssetTypeToFolderName[DependencyAssetType.Other];
    }

    private void DrawFolderNameConfigSection()
    {
        showFolderNameConfig = EditorGUILayout.Foldout(showFolderNameConfig, "资源类型文件夹名称配置", true);
        if (!showFolderNameConfig)
        {
            return;
        }

        if (customFolderNames == null)
        {
            customFolderNames = new Dictionary<DependencyAssetType, string>();
        }

        EditorGUI.indentLevel++;
        EditorGUILayout.HelpBox("可自定义各资源类型的目标文件夹名称。留空则使用默认名称。", MessageType.Info);

        bool configChanged = false;
        foreach (DependencyAssetType assetType in Enum.GetValues(typeof(DependencyAssetType)))
        {
            EditorGUILayout.BeginHorizontal();
            string defaultName = AssetTypeToFolderName.TryGetValue(assetType, out string dn) ? dn : "Other";
            string currentCustom = customFolderNames.TryGetValue(assetType, out string cn) ? cn : string.Empty;

            EditorGUILayout.LabelField(assetType.ToString(), GUILayout.Width(100));
            EditorGUILayout.LabelField($"(默认: {defaultName})", EditorStyles.miniLabel, GUILayout.Width(120));

            string newValue = EditorGUILayout.TextField(currentCustom);
            if (newValue != currentCustom)
            {
                if (string.IsNullOrWhiteSpace(newValue))
                {
                    customFolderNames.Remove(assetType);
                }
                else
                {
                    customFolderNames[assetType] = newValue.Trim();
                }
                configChanged = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("重置为默认", GUILayout.Width(100)))
        {
            customFolderNames.Clear();
            configChanged = true;
        }
        EditorGUILayout.EndHorizontal();

        if (configChanged)
        {
            SaveCustomFolderNamesToPrefs();
        }

        EditorGUI.indentLevel--;
    }

    private void SaveCustomFolderNamesToPrefs()
    {
        if (customFolderNames == null || customFolderNames.Count == 0)
        {
            EditorPrefs.DeleteKey(CustomFolderNamesPrefsKey);
            return;
        }

        List<FolderNameEntry> entries = new List<FolderNameEntry>();
        foreach (var kvp in customFolderNames)
        {
            entries.Add(new FolderNameEntry { TypeName = kvp.Key.ToString(), FolderName = kvp.Value });
        }

        FolderNameListWrapper wrapper = new FolderNameListWrapper { Entries = entries };
        EditorPrefs.SetString(CustomFolderNamesPrefsKey, JsonUtility.ToJson(wrapper));
    }

    private void LoadCustomFolderNamesFromPrefs()
    {
        if (customFolderNames == null)
        {
            customFolderNames = new Dictionary<DependencyAssetType, string>();
        }
        customFolderNames.Clear();
        string storedJson = EditorPrefs.GetString(CustomFolderNamesPrefsKey, string.Empty);
        if (string.IsNullOrEmpty(storedJson))
        {
            return;
        }

        try
        {
            FolderNameListWrapper wrapper = JsonUtility.FromJson<FolderNameListWrapper>(storedJson);
            if (wrapper?.Entries == null)
            {
                return;
            }

            foreach (FolderNameEntry entry in wrapper.Entries)
            {
                if (string.IsNullOrEmpty(entry.TypeName) || string.IsNullOrEmpty(entry.FolderName))
                {
                    continue;
                }

                if (Enum.TryParse(entry.TypeName, out DependencyAssetType assetType))
                {
                    customFolderNames[assetType] = entry.FolderName;
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"自定义文件夹名称加载失败: {exception.Message}");
        }
    }

    private void EnsureAssetFolderExists(string assetFolderPath)
    {
        string absolutePath = ConvertAssetPathToAbsolutePath(assetFolderPath);
        if (string.IsNullOrEmpty(absolutePath))
        {
            return;
        }

        if (!Directory.Exists(absolutePath))
        {
            Directory.CreateDirectory(absolutePath);
        }
    }

    private string ConvertAssetPathToAbsolutePath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            string projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
            return NormalizeAssetPath(Path.Combine(projectRoot, assetPath));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[RefCounter] 路径转换失败: {assetPath}, 错误: {ex.Message}");
            return null;
        }
    }

    private string GenerateUniqueAssetPath(string desiredAssetPath)
    {
        if (string.IsNullOrEmpty(desiredAssetPath))
        {
            return desiredAssetPath;
        }

        string directory = NormalizeAssetPath(Path.GetDirectoryName(desiredAssetPath));
        string fileName = Path.GetFileNameWithoutExtension(desiredAssetPath);
        string extension = Path.GetExtension(desiredAssetPath);

        string candidatePath = desiredAssetPath;
        int counter = 1;
        while (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(candidatePath) != null)
        {
            candidatePath = NormalizeAssetPath($"{directory}/{fileName}_{counter:D3}{extension}");
            counter++;
        }

        return candidatePath;
    }

    private bool IsTextSerializedAsset(string assetPath)
    {
        if (IsShaderAsset(assetPath))
        {
            return false;
        }

        string extension = Path.GetExtension(assetPath);
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        if (TextSerializedExtensions.Contains(extension))
        {
            return true;
        }

        // 广义判定：大多数 ScriptableObject 以 .asset 结尾已覆盖
        return false;
    }

    private bool ReplaceGuidInTextAsset(string assetPath, string oldGuid, string newGuid)
    {
        if (string.IsNullOrEmpty(oldGuid) || string.IsNullOrEmpty(newGuid))
        {
            return false;
        }

        string absolutePath = ConvertAssetPathToAbsolutePath(assetPath);
        if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
        {
            return false;
        }

        string content = File.ReadAllText(absolutePath);
        if (!TryReplaceGuidTokens(ref content, oldGuid, newGuid))
        {
            return false;
        }

        File.WriteAllText(absolutePath, content);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        return true;
    }

    private void AppendConsolidationLog(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        consolidationLogs.Add(message);
        if (consolidationLogs.Count > MaxConsolidationLogEntries)
        {
            int overflow = consolidationLogs.Count - MaxConsolidationLogEntries;
            consolidationLogs.RemoveRange(0, overflow);
        }
    }

    private void DrawDependencyBucketFoldout(string title, ref bool foldoutState, List<DependencyRecord> records)
    {
        foldoutState = EditorGUILayout.Foldout(foldoutState, $"{title} ({records.Count})", true);
        if (!foldoutState)
        {
            return;
        }

        EditorGUI.indentLevel++;
        foreach (DependencyRecord record in records)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // 标题行：名称 + 定位按钮
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{Path.GetFileName(record.AssetPath)} [{record.AssetType}]", EditorStyles.boldLabel);
            if (GUILayout.Button("定位", GUILayout.Width(50)))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(record.AssetPath);
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                    EditorUtility.FocusProjectWindow();
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.LabelField("路径", record.AssetPath);
            if (record.ReferencedBy != null && record.ReferencedBy.Count > 0)
            {
                EditorGUILayout.LabelField("被引用", string.Join(", ", record.ReferencedBy.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)));
            }
            if (record.NestedExternalDependencies != null && record.NestedExternalDependencies.Count > 0)
            {
                EditorGUILayout.LabelField("嵌套依赖", string.Join(", ", record.NestedExternalDependencies.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)));
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUI.indentLevel--;
    }

    private void RunDependencyScan()
    {
        string mainFolderPath = GetFolderPath(consolidationMainFolder);
        if (string.IsNullOrEmpty(mainFolderPath))
        {
            EditorUtility.DisplayDialog("错误", "请先选择一个有效的主文件夹", "确定");
            return;
        }

        HashSet<string> whitelistPaths = BuildWhitelistPathSet();
        
        // 验证白名单是否与目标文件夹存在包含关系
        foreach (string whitelistPath in whitelistPaths)
        {
            if (IsPathInsideFolder(whitelistPath, mainFolderPath))
            {
                Debug.LogWarning($"[资源引用工具] 白名单文件夹 '{whitelistPath}' 是目标文件夹的子目录，已自动忽略");
            }
            else if (IsPathInsideFolder(mainFolderPath, whitelistPath))
            {
                Debug.LogWarning($"[资源引用工具] 目标文件夹是白名单文件夹 '{whitelistPath}' 的子目录，可能导致扫描范围过大");
            }
        }
        
        lastScanMainFolderPath = mainFolderPath;

        dependencyRecordLookup.Clear();
        internalDependencyRecords.Clear();
        externalDependencyRecords.Clear();
        whitelistedDependencyRecords.Clear();
        autoCollectedSecondaryFolders.Clear();
        autoSecondaryFolderLookup.Clear();

        string[] assetGuids = AssetDatabase.FindAssets(string.Empty, new[] { mainFolderPath });
        int totalAssets = assetGuids.Length;
        
        // 使用全局已处理集合，避免重复扫描同一资源
        HashSet<string> globalProcessedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            bool userCancelled = false;  // 标记用户是否取消
            for (int i = 0; i < assetGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
                if (!IsAssetPathProcessable(assetPath) || AssetDatabase.IsValidFolder(assetPath))
                {
                    continue;
                }

                // 每次都更新进度条，支持取消
                float progress = Mathf.Clamp01((float)(i + 1) / totalAssets);
                if (EditorUtility.DisplayCancelableProgressBar("扫描依赖", 
                    $"({i + 1}/{totalAssets}): {Path.GetFileName(assetPath)}", progress))
                {
                    Debug.Log("[资源引用工具] 用户取消了扫描");
                    userCancelled = true;
                    break;
                }

                CollectDependenciesRecursive(assetPath, whitelistPaths, globalProcessedAssets, assetPath, null);
            }
            
            // 用户取消时清空已收集的部分数据，避免显示不完整结果
            if (userCancelled)
            {
                ClearDependencyScanResults();
                return;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        BuildDependencyBuckets();
        hasDependencyScanResults = dependencyRecordLookup.Count > 0;
        Repaint();

        Debug.Log($"[资源引用工具] 依赖扫描完成：内部 {internalDependencyRecords.Count}、外部 {externalDependencyRecords.Count}、白名单 {whitelistedDependencyRecords.Count}");
    }

    private void ClearDependencyScanResults()
    {
        dependencyRecordLookup.Clear();
        internalDependencyRecords.Clear();
        externalDependencyRecords.Clear();
        whitelistedDependencyRecords.Clear();
        autoCollectedSecondaryFolders.Clear();
        autoSecondaryFolderLookup.Clear();
        hasDependencyScanResults = false;
        lastScanMainFolderPath = null;
        dependencyResultScrollPosition = Vector2.zero;
        Repaint();
    }

    private void CollectDependenciesRecursive(string currentAssetPath, HashSet<string> whitelistPaths, HashSet<string> processedAssets, string originRootAsset, string parentExternalPath)
    {
        currentAssetPath = NormalizeAssetPath(currentAssetPath);
        if (string.IsNullOrEmpty(currentAssetPath) || processedAssets.Contains(currentAssetPath))
        {
            return;
        }

        processedAssets.Add(currentAssetPath);

        List<string> dependencies;
        try
        {
            dependencies = new List<string>(AssetDatabase.GetDependencies(currentAssetPath, false));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[资源引用工具] 获取依赖失败: {currentAssetPath}, 错误: {ex.Message}");
            return;
        }
        
        if (IsTimelineAsset(currentAssetPath))
        {
            dependencies.AddRange(ExtractTimelineNestedDependencies(currentAssetPath));
        }

        foreach (string dependency in dependencies)
        {
            string normalizedDependency = NormalizeAssetPath(dependency);
            if (string.IsNullOrEmpty(normalizedDependency) || normalizedDependency == currentAssetPath)
            {
                continue;
            }

            if (!IsAssetPathProcessable(normalizedDependency))
            {
                continue;
            }

            DependencyCategory category = DetermineDependencyCategory(normalizedDependency, whitelistPaths);
            DependencyRecord record = GetOrCreateDependencyRecord(normalizedDependency, category);
            record.ReferencedBy.Add(originRootAsset);
            
            // 【关键修复】如果当前资源是白名单资源，且它引用了外部资源，需要额外记录白名单资源自身
            // 这样在整合资源时，白名单资源的引用也会被更新为新复制资源的 GUID
            DependencyCategory currentCategory = DetermineDependencyCategory(currentAssetPath, whitelistPaths);
            if (currentCategory == DependencyCategory.Whitelisted && 
                category == DependencyCategory.External && 
                currentAssetPath != originRootAsset)
            {
                record.ReferencedBy.Add(currentAssetPath);
                AppendConsolidationLog($"[白名单引用] {Path.GetFileName(currentAssetPath)} 引用外部资源 {Path.GetFileName(normalizedDependency)}");
            }

            if (category == DependencyCategory.External)
            {
                RegisterAutoSecondaryFolder(record.SourceFolder);
                if (!string.IsNullOrEmpty(parentExternalPath))
                {
                    RegisterNestedRelationship(parentExternalPath, normalizedDependency);
                }

                CollectDependenciesRecursive(normalizedDependency, whitelistPaths, processedAssets, originRootAsset, normalizedDependency);
            }
            // 白名单资源也需要递归扫描其依赖，以便发现白名单资源引用的外部资源
            else if (category == DependencyCategory.Whitelisted)
            {
                CollectDependenciesRecursive(normalizedDependency, whitelistPaths, processedAssets, originRootAsset, parentExternalPath);
            }
        }
        
        // 不再移除，保持全局已处理状态，避免重复扫描
    }

    private DependencyCategory DetermineDependencyCategory(string dependencyPath, HashSet<string> whitelistPaths)
    {
        if (IsPathInsideFolder(dependencyPath, lastScanMainFolderPath))
        {
            return DependencyCategory.Internal;
        }

        if (IsPathInsideAnyFolder(dependencyPath, whitelistPaths))
        {
            return DependencyCategory.Whitelisted;
        }

        return DependencyCategory.External;
    }

    private void BuildDependencyBuckets()
    {
        internalDependencyRecords.Clear();
        externalDependencyRecords.Clear();
        whitelistedDependencyRecords.Clear();

        foreach (DependencyRecord record in dependencyRecordLookup.Values)
        {
            switch (record.Category)
            {
                case DependencyCategory.Internal:
                    internalDependencyRecords.Add(record);
                    break;
                case DependencyCategory.External:
                    externalDependencyRecords.Add(record);
                    break;
                case DependencyCategory.Whitelisted:
                    whitelistedDependencyRecords.Add(record);
                    break;
            }
        }

        internalDependencyRecords.Sort((a, b) => string.CompareOrdinal(a.AssetPath, b.AssetPath));
        externalDependencyRecords.Sort((a, b) => string.CompareOrdinal(a.AssetPath, b.AssetPath));
        whitelistedDependencyRecords.Sort((a, b) => string.CompareOrdinal(a.AssetPath, b.AssetPath));
    }

    private HashSet<string> BuildWhitelistPathSet()
    {
        HashSet<string> whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (whitelistFolderAssets == null) return whitelist;  // 空指针保护
        foreach (DefaultAsset folderAsset in whitelistFolderAssets)
        {
            string path = GetFolderPath(folderAsset);
            if (!string.IsNullOrEmpty(path))
            {
                whitelist.Add(path);
            }
        }
        return whitelist;
    }

    private void RegisterAutoSecondaryFolder(string folderPath)
    {
        folderPath = NormalizeAssetPath(folderPath);
        if (string.IsNullOrEmpty(folderPath))
        {
            return;
        }

        if (IsPathInsideFolder(folderPath, lastScanMainFolderPath))
        {
            return;
        }

        if (autoSecondaryFolderLookup.Add(folderPath))
        {
            autoCollectedSecondaryFolders.Add(folderPath);
        }
    }

    private void RegisterNestedRelationship(string parentPath, string childPath)
    {
        if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(childPath))
        {
            return;
        }

        if (dependencyRecordLookup.TryGetValue(parentPath, out DependencyRecord record))
        {
            record.NestedExternalDependencies.Add(childPath);
        }
    }

    private IEnumerable<string> ExtractTimelineNestedDependencies(string timelineAssetPath)
    {
        List<string> dependencies = new List<string>();
        string absolutePath = ConvertAssetPathToAbsolutePath(timelineAssetPath);
        if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
        {
            return dependencies;
        }

        string content;
        try
        {
            content = File.ReadAllText(absolutePath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"读取 Timeline 文件失败: {timelineAssetPath}. {exception.Message}");
            return dependencies;
        }

        if (string.IsNullOrEmpty(content))
        {
            return dependencies;
        }

        HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in GuidTokenRegex.Matches(content))
        {
            if (!match.Success)
            {
                continue;
            }

            string guid = match.Groups[1].Value;
            if (string.IsNullOrEmpty(guid))
            {
                continue;
            }

            string referencedPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(referencedPath))
            {
                continue;
            }

            referencedPath = NormalizeAssetPath(referencedPath);
            if (!IsAssetPathProcessable(referencedPath))
            {
                continue;
            }

            if (seenPaths.Add(referencedPath))
            {
                dependencies.Add(referencedPath);
            }
        }

        return dependencies;
    }

    private bool IsTimelineAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

#if UNITY_TIMELINE
        Type assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
        return assetType != null && typeof(TimelineAsset).IsAssignableFrom(assetType);
#else
        // Timeline 包未安装，通过扩展名判断
        return assetPath.EndsWith(".playable", StringComparison.OrdinalIgnoreCase);
#endif
    }

    private DependencyRecord GetOrCreateDependencyRecord(string assetPath, DependencyCategory category)
    {
        if (!dependencyRecordLookup.TryGetValue(assetPath, out DependencyRecord record))
        {
            record = new DependencyRecord
            {
                AssetPath = assetPath,
                Category = category,
                SourceFolder = NormalizeAssetPath(Path.GetDirectoryName(assetPath)),
                AssetType = ResolveAssetType(assetPath)
            };
            dependencyRecordLookup.Add(assetPath, record);
        }
        return record;
    }

    private static readonly HashSet<string> ModelFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".fbx",
        ".obj",
        ".dae",
        ".dxf",
        ".3ds",
        ".blend",
        ".gltf",
        ".glb"
    };

    private static readonly HashSet<string> ShaderFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".shader",
        ".shadergraph",
        ".shadersubgraph",
        ".hlsl",
        ".cginc",
        ".cg",
        ".compute",
        ".glslinc"
    };

    private DependencyAssetType ResolveAssetType(string assetPath)
    {
        if (IsModelFileExtension(assetPath))
        {
            return DependencyAssetType.Mesh;
        }

        Type assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
        if (assetType == null)
        {
            return DependencyAssetType.Other;
        }

        if (typeof(Texture).IsAssignableFrom(assetType)) return DependencyAssetType.Texture;
        if (typeof(Material).IsAssignableFrom(assetType)) return DependencyAssetType.Material;
        if (typeof(Mesh).IsAssignableFrom(assetType)) return DependencyAssetType.Mesh;
        if (typeof(AnimationClip).IsAssignableFrom(assetType)) return DependencyAssetType.Animation;
#if UNITY_TIMELINE
        if (typeof(TimelineAsset).IsAssignableFrom(assetType)) return DependencyAssetType.Timeline;
#endif
        if (typeof(Font).IsAssignableFrom(assetType)) return DependencyAssetType.Font;
        if (typeof(VideoClip).IsAssignableFrom(assetType)) return DependencyAssetType.Video;
        if (typeof(SpriteAtlas).IsAssignableFrom(assetType)) return DependencyAssetType.SpriteAtlas;
        if (typeof(PhysicMaterial).IsAssignableFrom(assetType)) return DependencyAssetType.PhysicMaterial;
        if (typeof(TerrainData).IsAssignableFrom(assetType)) return DependencyAssetType.Terrain;
        if (typeof(GameObject).IsAssignableFrom(assetType)) return DependencyAssetType.Prefab;
        if (typeof(Shader).IsAssignableFrom(assetType)) return DependencyAssetType.Shader;
        if (typeof(AudioClip).IsAssignableFrom(assetType)) return DependencyAssetType.Audio;
        if (typeof(ScriptableObject).IsAssignableFrom(assetType)) return DependencyAssetType.Scriptable;
        return DependencyAssetType.Other;
    }

    private bool IsModelFileExtension(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        string extension = Path.GetExtension(assetPath);
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        return ModelFileExtensions.Contains(extension);
    }

    private bool IsShaderAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        string extension = Path.GetExtension(assetPath);
        if (!string.IsNullOrEmpty(extension) && ShaderFileExtensions.Contains(extension))
        {
            return true;
        }

        Type assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
        return assetType != null && typeof(Shader).IsAssignableFrom(assetType);
    }

    private bool IsPathInsideFolder(string assetPath, string folderPath)
    {
        if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(folderPath))
        {
            return false;
        }

        if (assetPath.Equals(folderPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string normalizedFolder = folderPath.EndsWith("/") ? folderPath : folderPath + "/";
        return assetPath.StartsWith(normalizedFolder, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsPathInsideAnyFolder(string assetPath, HashSet<string> folders)
    {
        if (folders == null || folders.Count == 0)
        {
            return false;
        }

        foreach (string folder in folders)
        {
            if (IsPathInsideFolder(assetPath, folder))
            {
                return true;
            }
        }

        return false;
    }

    private string NormalizeAssetPath(string assetPath)
    {
        return string.IsNullOrEmpty(assetPath) ? string.Empty : assetPath.Replace("\\", "/");
    }

    private string GetFolderPath(DefaultAsset folderAsset)
    {
        if (folderAsset == null)
        {
            return null;
        }

        string path = AssetDatabase.GetAssetPath(folderAsset);
        if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
        {
            return null;
        }

        return NormalizeAssetPath(path);
    }

    private bool IsAssetPathProcessable(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
            assetPath.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase) ||
            assetPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
            assetPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
            assetPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        if (IsShaderAsset(assetPath))
        {
            return false;
        }

        return true;
    }

    private enum DependencyCategory
    {
        Internal,
        External,
        Whitelisted
    }

    private enum DependencyAssetType
    {
        Texture,
        Material,
        Mesh,
        Animation,
        Prefab,
        Shader,
        Audio,
        Scriptable,
        Timeline,
        Font,
        Video,
        SpriteAtlas,
        PhysicMaterial,
        Terrain,
        Other
    }

    private class DependencyRecord
    {
        public string AssetPath;
        public DependencyCategory Category;
        public string SourceFolder;
        public DependencyAssetType AssetType;
        public HashSet<string> ReferencedBy = new HashSet<string>();
        public HashSet<string> NestedExternalDependencies = new HashSet<string>();
    }

    [Serializable]
    private class StringListWrapper
    {
        public List<string> Items = new List<string>();
    }

    [Serializable]
    private class FolderNameEntry
    {
        public string TypeName;
        public string FolderName;
    }

    [Serializable]
    private class FolderNameListWrapper
    {
        public List<FolderNameEntry> Entries = new List<FolderNameEntry>();
    }

    /// <summary>
    /// 一键资源整理报告
    /// </summary>
    private class OrganizationReport
    {
        public int ScannedAssetCount;
        public int ExternalDependencyCount;
        public int ConsolidatedAssetCount;
        public int ReferenceUpdateCount;
        public int SkippedAssetCount;
        public int ZeroReferenceCount;
        public long EstimatedCleanupSize;
        public DateTime GeneratedTime;
        public List<string> ZeroReferenceAssets = new List<string>();
        public List<string> ConsolidatedAssets = new List<string>();
    }

    void OnEnable()
    {
        EnsureFolderListInitialized(ref whitelistFolderAssets);
        if (customFolderNames == null)
        {
            customFolderNames = new Dictionary<DependencyAssetType, string>();
        }
        LoadWhitelistFromPrefs();
        LoadCustomFolderNamesFromPrefs();
    }

    void OnDisable()
    {
        SaveWhitelistToPrefs();
        SaveCustomFolderNamesToPrefs();
    }

    #region 图片查重与引用统一

    // 图片查重数据结构
    private class TextureHashRecord
    {
        public string AssetPath;      // 资源路径
        public string Hash;           // MD5 哈希
        public long FileSize;         // 文件大小
        public int ReferenceCount;    // 被引用次数
        public bool IsSelected;       // 是否选为保留
    }

    private class DuplicateTextureGroup
    {
        public string Hash;                         // 哈希值（组标识）
        public List<TextureHashRecord> Textures;    // 组内所有图片
        public string SelectedTexturePath;          // 被选中保留的图片路径
        public bool IsFoldout;                      // UI折叠状态
    }

    // 图片查重字段
    private List<DuplicateTextureGroup> duplicateTextureGroups = new List<DuplicateTextureGroup>();
    private bool showDuplicateTextureSection = true;
    private Vector2 duplicateTextureScrollPosition;
    private int duplicateTextureGroupCount;
    private int duplicateTextureCount;
    private long duplicateTextureSize;

    // Timeline残留清理字段
    private bool showTimelineCleanupSection = true;
    private List<TimelineCleanupRecord> timelineCleanupResults = new List<TimelineCleanupRecord>();
    private Vector2 timelineCleanupScrollPos;
    private int timelineCleanupTotalBindings;
    private int timelineCleanupTotalExposedRefs;

    private class TimelineCleanupRecord
    {
        public string PrefabPath;
        public int EmptyBindingCount;
        public int EmptyExposedRefCount;
        public bool IsSelected;
    }

    private static readonly HashSet<string> TextureFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".tga", ".psd", 
        ".tif", ".tiff", ".bmp", ".gif", ".exr", ".hdr"
    };

    /// <summary>
    /// 扫描重复图片
    /// </summary>
    private void ScanDuplicateTextures()
    {
        duplicateTextureGroups.Clear();
        duplicateTextureGroupCount = 0;
        duplicateTextureCount = 0;
        duplicateTextureSize = 0;

        if (consolidationMainFolder == null)
        {
            EditorUtility.DisplayDialog("提示", "请先设置目标文件夹", "确定");
            return;
        }

        string mainFolderPath = GetFolderPath(consolidationMainFolder);
        if (string.IsNullOrEmpty(mainFolderPath))
        {
            EditorUtility.DisplayDialog("提示", "目标文件夹路径无效", "确定");
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("扫描重复图片", "正在收集图片文件...", 0f);

            // 收集所有图片文件
            string[] allAssets = AssetDatabase.FindAssets("t:Texture2D", new[] { mainFolderPath });
            var textureRecords = new List<TextureHashRecord>();

            for (int i = 0; i < allAssets.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(allAssets[i]);
                string ext = Path.GetExtension(assetPath);
                
                if (!TextureFileExtensions.Contains(ext))
                    continue;

                EditorUtility.DisplayProgressBar("扫描重复图片", 
                    $"计算哈希 ({i + 1}/{allAssets.Length}): {Path.GetFileName(assetPath)}", 
                    (float)i / allAssets.Length);

                string absolutePath = ConvertAssetPathToAbsolutePath(assetPath);
                if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
                    continue;

                string hash = CalculateFileHash(absolutePath);
                if (string.IsNullOrEmpty(hash))
                    continue;

                long fileSize = new FileInfo(absolutePath).Length;
                int refCount = GetTextureReferenceCount(assetPath);

                textureRecords.Add(new TextureHashRecord
                {
                    AssetPath = assetPath,
                    Hash = hash,
                    FileSize = fileSize,
                    ReferenceCount = refCount,
                    IsSelected = false
                });
            }

            EditorUtility.DisplayProgressBar("扫描重复图片", "分析重复组...", 0.9f);

            // 按哈希分组
            var hashGroups = textureRecords
                .GroupBy(r => r.Hash)
                .Where(g => g.Count() > 1)  // 只保留重复的
                .ToList();

            foreach (var group in hashGroups)
            {
                var textures = group.ToList();
                
                // 按引用次数降序，然后按路径长度升序排序
                textures = textures
                    .OrderByDescending(t => t.ReferenceCount)
                    .ThenBy(t => t.AssetPath.Length)
                    .ToList();

                // 默认选中第一个（引用最多且路径最短）
                textures[0].IsSelected = true;

                var dupGroup = new DuplicateTextureGroup
                {
                    Hash = group.Key,
                    Textures = textures,
                    SelectedTexturePath = textures[0].AssetPath,
                    IsFoldout = true
                };

                duplicateTextureGroups.Add(dupGroup);
                duplicateTextureGroupCount++;
                duplicateTextureCount += textures.Count - 1;  // 不计算保留的那个
                duplicateTextureSize += textures.Skip(1).Sum(t => t.FileSize);
            }

            EditorUtility.ClearProgressBar();

            if (duplicateTextureGroupCount == 0)
            {
                EditorUtility.DisplayDialog("扫描完成", "未发现重复图片", "确定");
            }
            else
            {
                ShowNotification(new GUIContent($"发现 {duplicateTextureGroupCount} 组重复图片"));
            }
        }
        catch (Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"扫描重复图片失败: {e.Message}");
            EditorUtility.DisplayDialog("扫描失败", e.Message, "确定");
        }

        Repaint();
    }

    /// <summary>
    /// 计算文件MD5哈希值
    /// </summary>
    private string CalculateFileHash(string absolutePath)
    {
        try
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
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
    /// 获取贴图被引用次数（支持材质球、Prefab、场景、ScriptableObject等）
    /// </summary>
    private int GetTextureReferenceCount(string texturePath)
    {
        if (string.IsNullOrEmpty(texturePath))
            return 0;

        string textureGuid = AssetDatabase.AssetPathToGUID(texturePath);
        if (string.IsNullOrEmpty(textureGuid))
            return 0;

        string mainFolderPath = GetFolderPath(consolidationMainFolder);
        if (string.IsNullOrEmpty(mainFolderPath))
            return 0;

        int count = 0;
        
        // 查找材质球引用
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { mainFolderPath });
        foreach (string matGuid in materialGuids)
        {
            string matPath = AssetDatabase.GUIDToAssetPath(matGuid);
            string[] deps = AssetDatabase.GetDependencies(matPath, false);
            if (deps.Contains(texturePath))
            {
                count++;
            }
        }
        
        // 查找Prefab引用
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { mainFolderPath });
        foreach (string prefabGuid in prefabGuids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            string[] deps = AssetDatabase.GetDependencies(prefabPath, false);
            if (deps.Contains(texturePath))
            {
                count++;
            }
        }
        
        // 查找场景引用
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { mainFolderPath });
        foreach (string sceneGuid in sceneGuids)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
            string[] deps = AssetDatabase.GetDependencies(scenePath, false);
            if (deps.Contains(texturePath))
            {
                count++;
            }
        }
        
        // 查找ScriptableObject引用
        string[] soGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { mainFolderPath });
        foreach (string soGuid in soGuids)
        {
            string soPath = AssetDatabase.GUIDToAssetPath(soGuid);
            string[] deps = AssetDatabase.GetDependencies(soPath, false);
            if (deps.Contains(texturePath))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 重新计算重复图片统计数据（可清理数量和大小）
    /// </summary>
    private void RecalculateDuplicateStats()
    {
        duplicateTextureCount = 0;
        duplicateTextureSize = 0;
        
        foreach (var group in duplicateTextureGroups)
        {
            foreach (var texture in group.Textures)
            {
                // 非保留项计入可清理统计
                if (texture.AssetPath != group.SelectedTexturePath)
                {
                    duplicateTextureCount++;
                    duplicateTextureSize += texture.FileSize;
                }
            }
        }
    }

    /// <summary>
    /// 统一材质球引用（GUID替换）
    /// </summary>
    private void UnifyTextureReferences()
    {
        if (duplicateTextureGroups.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先扫描重复图片", "确定");
            return;
        }

        if (consolidationMainFolder == null)
        {
            EditorUtility.DisplayDialog("提示", "请先设置目标文件夹", "确定");
            return;
        }

        string mainFolderPath = GetFolderPath(consolidationMainFolder);
        if (string.IsNullOrEmpty(mainFolderPath))
            return;

        int totalReplacements = 0;

        try
        {
            // 收集所有需要替换的GUID映射
            var allGuidMappings = new Dictionary<string, string>();

            foreach (var group in duplicateTextureGroups)
            {
                string selectedPath = group.SelectedTexturePath;
                string selectedGuid = AssetDatabase.AssetPathToGUID(selectedPath);
                
                if (string.IsNullOrEmpty(selectedGuid))
                    continue;

                foreach (var texture in group.Textures)
                {
                    if (texture.AssetPath == selectedPath)
                        continue;

                    string oldGuid = AssetDatabase.AssetPathToGUID(texture.AssetPath);
                    if (!string.IsNullOrEmpty(oldGuid) && !allGuidMappings.ContainsKey(oldGuid))
                    {
                        allGuidMappings[oldGuid] = selectedGuid;
                    }
                }
            }

            if (allGuidMappings.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有需要替换的引用", "确定");
                return;
            }

            // 查找所有可能引用贴图的资源（材质球、Prefab、场景、ScriptableObject）
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { mainFolderPath });
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { mainFolderPath });
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { mainFolderPath });
            string[] soGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { mainFolderPath });
            
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

                if (!IsTextSerializedAsset(assetPath))
                    continue;

                if (ReplaceMultipleGuidsInTextAsset(assetPath, allGuidMappings))
                {
                    totalReplacements++;
                    AppendConsolidationLog($"[图片查重] 更新引用: {Path.GetFileName(assetPath)}");
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 重新计算引用次数
            foreach (var group in duplicateTextureGroups)
            {
                foreach (var texture in group.Textures)
                {
                    texture.ReferenceCount = GetTextureReferenceCount(texture.AssetPath);
                }
            }
            
            // 重新计算统计数据
            RecalculateDuplicateStats();

            EditorUtility.DisplayDialog("统一引用完成", 
                $"已更新 {totalReplacements} 个资源的引用\n\n" +
                "现在可以安全删除重复图片", "确定");
        }
        catch (Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"统一引用失败: {e.Message}");
            EditorUtility.DisplayDialog("统一引用失败", e.Message, "确定");
        }

        Repaint();
    }

    /// <summary>
    /// 删除重复图片（保留选中项）
    /// </summary>
    private void DeleteDuplicateTextures()
    {
        if (duplicateTextureGroups.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有重复图片可删除", "确定");
            return;
        }

        // 统计要删除的文件
        int deleteCount = 0;
        long deleteSize = 0;
        var toDelete = new List<string>();

        foreach (var group in duplicateTextureGroups)
        {
            foreach (var texture in group.Textures)
            {
                if (texture.AssetPath != group.SelectedTexturePath)
                {
                    toDelete.Add(texture.AssetPath);
                    deleteCount++;
                    deleteSize += texture.FileSize;
                }
            }
        }

        if (deleteCount == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有可删除的重复图片", "确定");
            return;
        }

        if (!EditorUtility.DisplayDialog("确认删除", 
            $"即将删除 {deleteCount} 张重复图片\n" +
            $"预计释放 {FormatFileSize(deleteSize)}\n\n" +
            "此操作不可撤销！", "删除", "取消"))
        {
            return;
        }

        int successCount = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (string path in toDelete)
            {
                if (AssetDatabase.DeleteAsset(path))
                {
                    successCount++;
                    AppendConsolidationLog($"[图片查重] 已删除: {path}");
                }
                else
                {
                    Debug.LogWarning($"删除失败: {path}");
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        // 清空已处理的组
        duplicateTextureGroups.Clear();
        duplicateTextureGroupCount = 0;
        duplicateTextureCount = 0;
        duplicateTextureSize = 0;

        EditorUtility.DisplayDialog("删除完成", 
            $"已删除 {successCount}/{deleteCount} 张重复图片", "确定");
        
        Repaint();
    }

    /// <summary>
    /// 绘制图片查重UI区域
    /// </summary>
    private void DrawDuplicateTextureSection()
    {
        showDuplicateTextureSection = EditorGUILayout.Foldout(showDuplicateTextureSection, 
            "🔍 图片查重", true);

        if (!showDuplicateTextureSection)
            return;

        EditorGUI.indentLevel++;

        // 操作按钮
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = consolidationMainFolder != null;
        if (GUILayout.Button("扫描重复", GUILayout.Height(24)))
        {
            ScanDuplicateTextures();
        }
        GUI.enabled = duplicateTextureGroups.Count > 0;
        if (GUILayout.Button("统一引用", GUILayout.Height(24)))
        {
            UnifyTextureReferences();
        }
        if (GUILayout.Button("删除重复", GUILayout.Height(24)))
        {
            DeleteDuplicateTextures();
        }
        if (GUILayout.Button("清空结果", GUILayout.Height(24)))
        {
            duplicateTextureGroups.Clear();
            duplicateTextureGroupCount = 0;
            duplicateTextureCount = 0;
            duplicateTextureSize = 0;
            Repaint();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        // 统计信息
        if (duplicateTextureGroupCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"发现 {duplicateTextureGroupCount} 组重复，共 {duplicateTextureCount} 张可清理，预计释放 {FormatFileSize(duplicateTextureSize)}", 
                MessageType.Info);

            // 重复图片列表
            duplicateTextureScrollPosition = EditorGUILayout.BeginScrollView(
                duplicateTextureScrollPosition, GUILayout.Height(200));

            foreach (var group in duplicateTextureGroups)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // 组标题
                group.IsFoldout = EditorGUILayout.Foldout(group.IsFoldout, 
                    $"{Path.GetFileName(group.SelectedTexturePath)} ({group.Textures.Count}个重复)", true);

                if (group.IsFoldout)
                {
                    foreach (var texture in group.Textures)
                    {
                        EditorGUILayout.BeginHorizontal();
                        
                        // 选择单选按钮（只处理从未选中变为选中的情况，防止取消选择）
                        bool isSelected = texture.AssetPath == group.SelectedTexturePath;
                        EditorGUI.BeginChangeCheck();
                        bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
                        if (EditorGUI.EndChangeCheck())
                        {
                            // 只处理点击未选中项的情况（选中新项），忽略取消选择的操作
                            if (newSelected && !isSelected)
                            {
                                // 更新选中项
                                group.SelectedTexturePath = texture.AssetPath;
                                foreach (var t in group.Textures)
                                    t.IsSelected = t.AssetPath == texture.AssetPath;
                                
                                // 重新计算统计数据
                                RecalculateDuplicateStats();
                            }
                        }

                        // 图标
                        if (isSelected)
                        {
                            GUILayout.Label("●", GUILayout.Width(15));
                        }
                        else
                        {
                            GUILayout.Label("○", GUILayout.Width(15));
                        }

                        // 文件名和路径
                        string displayPath = texture.AssetPath;
                        if (displayPath.Length > 50)
                            displayPath = "..." + displayPath.Substring(displayPath.Length - 47);
                        
                        if (isSelected)
                        {
                            EditorGUILayout.LabelField($"{displayPath} (保留)", EditorStyles.boldLabel);
                        }
                        else
                        {
                            EditorGUILayout.LabelField(displayPath);
                        }

                        // 引用次数
                        GUILayout.Label($"[{texture.ReferenceCount}次引用]", GUILayout.Width(70));

                        // 定位按钮
                        if (GUILayout.Button("定位", GUILayout.Width(40)))
                        {
                            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(texture.AssetPath);
                            EditorGUIUtility.PingObject(Selection.activeObject);
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.HelpBox("点击「扫描重复」查找目标文件夹内的重复图片", MessageType.None);
        }

        EditorGUI.indentLevel--;
    }

    #endregion

    #region 一键资源整理

    /// <summary>
    /// 一键资源整理 - 整合依赖扫描、资源整合、零引用检测的完整流程
    /// </summary>
    private void RunOneClickOrganization()
    {
        // 验证配置
        if (consolidationMainFolder == null)
        {
            EditorUtility.DisplayDialog("配置错误", "请先设置主文件夹", "确定");
            return;
        }

        string mainFolderPath = GetFolderPath(consolidationMainFolder);
        if (string.IsNullOrEmpty(mainFolderPath))
        {
            EditorUtility.DisplayDialog("配置错误", "主文件夹路径无效", "确定");
            return;
        }

        // 同步目标文件夹设置（用于零引用检测）
        selectedFolder = consolidationMainFolder;

        isOneClickOrganizing = true;
        oneClickCurrentPhase = 0;
        oneClickStatusMessage = "";
        lastOrganizationReport = new OrganizationReport { GeneratedTime = DateTime.Now };
        consolidationLogs.Clear();

        try
        {
            // ========== 阶段 1: 依赖扫描 ==========
            oneClickCurrentPhase = 1;
            oneClickStatusMessage = $"阶段 {oneClickCurrentPhase}/4: 扫描依赖关系...";
            Repaint();

            RunDependencyScan();
            lastOrganizationReport.ScannedAssetCount = dependencyRecordLookup.Count;
            lastOrganizationReport.ExternalDependencyCount = externalDependencyRecords.Count;

            AppendConsolidationLog($"[阶段1完成] 扫描到 {dependencyRecordLookup.Count} 个依赖，其中外部依赖 {externalDependencyRecords.Count} 个");

            // ========== 阶段 2: 资源整合 ==========
            oneClickCurrentPhase = 2;
            if (externalDependencyRecords.Count > 0)
            {
                oneClickStatusMessage = $"阶段 {oneClickCurrentPhase}/4: 整合 {externalDependencyRecords.Count} 个外部资源...";
                Repaint();

                var consolidationResult = RunOneClickConsolidationInternal();
                lastOrganizationReport.ConsolidatedAssetCount = consolidationResult.CopiedCount;
                lastOrganizationReport.ReferenceUpdateCount = consolidationResult.ReferenceUpdateCount;
                lastOrganizationReport.SkippedAssetCount = consolidationResult.SkippedCount;
                lastOrganizationReport.ConsolidatedAssets = consolidationResult.ConsolidatedPaths;

                AppendConsolidationLog($"[阶段2完成] 整合 {consolidationResult.CopiedCount} 个资源，更新 {consolidationResult.ReferenceUpdateCount} 个引用");
            }
            else
            {
                oneClickStatusMessage = $"阶段 {oneClickCurrentPhase}/4: 无外部依赖，跳过整合...";
                Repaint();
                AppendConsolidationLog("[阶段2跳过] 没有外部依赖需要整合");
            }

            // ========== 阶段 3: 零引用检测 ==========
            oneClickCurrentPhase = 3;
            oneClickStatusMessage = $"阶段 {oneClickCurrentPhase}/4: 检测零引用资源...";
            Repaint();

            RunZeroReferenceDetection();
            lastOrganizationReport.ZeroReferenceCount = zeroReferenceAssets.Count;
            lastOrganizationReport.ZeroReferenceAssets = new List<string>(zeroReferenceAssets);
            lastOrganizationReport.EstimatedCleanupSize = CalculateTotalFileSize(zeroReferenceAssets);

            AppendConsolidationLog($"[阶段3完成] 发现 {zeroReferenceAssets.Count} 个零引用资源，预计可清理 {FormatFileSize(lastOrganizationReport.EstimatedCleanupSize)}");

            // ========== 阶段 4: 生成报告 ==========
            oneClickCurrentPhase = 4;
            oneClickStatusMessage = $"阶段 {oneClickCurrentPhase}/4: 生成整理报告...";
            Repaint();

            ShowOrganizationReportDialog(lastOrganizationReport);
            AppendConsolidationLog("[阶段4完成] 整理报告已生成");

            oneClickStatusMessage = "✅ 一键资源整理完成！";
            ShowNotification(new GUIContent("一键资源整理完成"));
        }
        catch (Exception ex)
        {
            oneClickStatusMessage = $"❌ 整理过程出错: {ex.Message}";
            AppendConsolidationLog($"[错误] {ex.Message}");
            Debug.LogError($"一键资源整理失败: {ex}");
            EditorUtility.DisplayDialog("整理失败", $"整理过程中发生错误:\n{ex.Message}", "确定");
        }
        finally
        {
            isOneClickOrganizing = false;
            EditorUtility.ClearProgressBar();
            Repaint();
        }
    }

    /// <summary>
    /// 整合结果数据结构
    /// </summary>
    private class ConsolidationResult
    {
        public int CopiedCount;
        public int ReferenceUpdateCount;
        public int SkippedCount;
        public List<string> ConsolidatedPaths = new List<string>();
    }

    /// <summary>
    /// 内部整合方法，返回整合结果
    /// </summary>
    private ConsolidationResult RunOneClickConsolidationInternal()
    {
        var result = new ConsolidationResult();

        if (!hasDependencyScanResults)
        {
            return result;
        }

        string mainFolderPath = lastScanMainFolderPath;
        if (string.IsNullOrEmpty(mainFolderPath))
        {
            return result;
        }

        List<DependencyRecord> targets = externalDependencyRecords.ToList();
        if (targets.Count == 0)
        {
            return result;
        }

        var copyMappings = new List<CopyMapping>();

        try
        {
            // 阶段一：复制所有资源
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < targets.Count; i++)
            {
                DependencyRecord record = targets[i];
                EditorUtility.DisplayProgressBar("一键整理 - 复制资源", $"({i + 1}/{targets.Count}): {Path.GetFileName(record.AssetPath)}", (float)(i + 1) / targets.Count);

                string absoluteSourcePath = ConvertAssetPathToAbsolutePath(record.AssetPath);
                if (string.IsNullOrEmpty(absoluteSourcePath) || !File.Exists(absoluteSourcePath))
                {
                    result.SkippedCount++;
                    AppendConsolidationLog($"[跳过] 无法访问资源 {record.AssetPath}");
                    continue;
                }

                string targetFolder = BuildTypedTargetFolder(mainFolderPath, record.AssetType);
                EnsureAssetFolderExists(targetFolder);
                string desiredTargetPath = NormalizeAssetPath($"{targetFolder}/{Path.GetFileName(record.AssetPath)}");
                string finalTargetPath = GenerateUniqueAssetPath(desiredTargetPath);

                string oldGuid = AssetDatabase.AssetPathToGUID(record.AssetPath);

                if (!AssetDatabase.CopyAsset(record.AssetPath, finalTargetPath))
                {
                    result.SkippedCount++;
                    AppendConsolidationLog($"[失败] 复制 {record.AssetPath} -> {finalTargetPath}");
                    continue;
                }

                result.CopiedCount++;
                result.ConsolidatedPaths.Add(finalTargetPath);
                AppendConsolidationLog($"[复制] {record.AssetPath} -> {finalTargetPath}");

                copyMappings.Add(new CopyMapping
                {
                    OldPath = record.AssetPath,
                    NewPath = finalTargetPath,
                    OldGuid = oldGuid,
                    ReferencedBy = record.ReferencedBy != null ? new List<string>(record.ReferencedBy) : new List<string>(),
                    NestedExternalDependencies = record.NestedExternalDependencies != null ? new List<string>(record.NestedExternalDependencies) : new List<string>()
                });
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        try
        {
            // 阶段二：更新引用
            Dictionary<string, CopyMapping> pathToMapping = copyMappings
                .Where(mapping => !string.IsNullOrEmpty(mapping.OldPath))
                .ToDictionary(mapping => mapping.OldPath, mapping => mapping, StringComparer.OrdinalIgnoreCase);

            Dictionary<string, string> guidReplacementMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (CopyMapping mapping in copyMappings)
            {
                mapping.NewGuid = AssetDatabase.AssetPathToGUID(mapping.NewPath);
                if (string.IsNullOrEmpty(mapping.OldGuid) || string.IsNullOrEmpty(mapping.NewGuid))
                {
                    continue;
                }
                guidReplacementMap[mapping.OldGuid] = mapping.NewGuid;
            }

            Dictionary<string, HashSet<string>> assetRewriteLookup = BuildAssetRewriteLookup(copyMappings, pathToMapping);

            List<string> rewriteTargets = assetRewriteLookup.Keys
                .Where(assetPath => !string.IsNullOrEmpty(assetPath))
                .OrderBy(assetPath => assetPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < rewriteTargets.Count; i++)
            {
                string assetPath = rewriteTargets[i];
                EditorUtility.DisplayProgressBar("一键整理 - 更新引用", $"({i + 1}/{rewriteTargets.Count}): {Path.GetFileName(assetPath)}", (float)(i + 1) / rewriteTargets.Count);

                if (!IsTextSerializedAsset(assetPath))
                {
                    continue;
                }

                if (!assetRewriteLookup.TryGetValue(assetPath, out HashSet<string> oldGuids) || oldGuids.Count == 0)
                {
                    continue;
                }

                Dictionary<string, string> replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string oldGuid in oldGuids)
                {
                    if (guidReplacementMap.TryGetValue(oldGuid, out string newGuid))
                    {
                        replacements[oldGuid] = newGuid;
                    }
                }

                if (replacements.Count == 0)
                {
                    continue;
                }

                if (ReplaceMultipleGuidsInTextAsset(assetPath, replacements))
                {
                    result.ReferenceUpdateCount++;
                }
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        return result;
    }

    /// <summary>
    /// 同步执行零引用检测（统一使用递归模拟删除算法，确保与手动检测结果一致）
    /// </summary>
    private void RunZeroReferenceDetection()
    {
        referenceCounts.Clear();
        zeroReferenceAssets.Clear();
        previewCache.Clear();
        assetSelection.Clear();

        string[] allTargetPaths = GetAssetsInSelectedFolder();
        if (allTargetPaths.Length == 0)
        {
            return;
        }

        // 获取引用来源（目标文件夹 + 白名单）
        string[] allSourcePaths = GetFilteredAssetPaths();
        int totalSources = allSourcePaths.Length;

        // 构建完整的依赖关系图
        // Key: 资源路径, Value: 引用它的资源列表
        Dictionary<string, HashSet<string>> referencedByMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        // 目标资源集合（用于快速查找）
        HashSet<string> targetSet = new HashSet<string>(allTargetPaths, StringComparer.OrdinalIgnoreCase);

        // ========== 阶段1：构建依赖关系图 ==========
        for (int i = 0; i < allSourcePaths.Length; i++)
        {
            string sourcePath = allSourcePaths[i];

            if (i % 10 == 0)
            {
                float progress = (float)i / totalSources * 0.5f;
                if (EditorUtility.DisplayCancelableProgressBar("一键整理 - 检测零引用",
                    $"构建依赖图 ({i + 1}/{totalSources}): {Path.GetFileName(sourcePath)}", progress))
                {
                    Debug.Log("[资源引用工具] 用户取消了零引用检测");
                    return;
                }
            }

            string[] dependencies;
            try
            {
                dependencies = AssetDatabase.GetDependencies(sourcePath, false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[资源引用工具] 获取依赖失败: {sourcePath}, 错误: {ex.Message}");
                continue;
            }

            string normalizedSource = NormalizeAssetPath(sourcePath);

            foreach (string dep in dependencies)
            {
                string normalizedDep = NormalizeAssetPath(dep);

                // 只统计目标资源的被引用情况
                if (targetSet.Contains(normalizedDep))
                {
                    if (!referencedByMap.ContainsKey(normalizedDep))
                    {
                        referencedByMap[normalizedDep] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }
                    referencedByMap[normalizedDep].Add(normalizedSource);
                }
            }
        }

        // ========== 阶段2：递归检测零引用 ==========
        HashSet<string> allZeroRefAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> simulatedDeleted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int round = 0;
        int maxRounds = 50; // 防止无限循环
        int newZeroRefCount;

        do
        {
            round++;
            newZeroRefCount = 0;

            float baseProgress = 0.5f + (float)round / maxRounds * 0.4f;
            if (EditorUtility.DisplayCancelableProgressBar("一键整理 - 检测零引用",
                $"递归检测第 {round} 轮...", baseProgress))
            {
                Debug.Log("[资源引用工具] 用户取消了零引用检测");
                return;
            }

            // 检测当前轮次的零引用资源
            foreach (string targetPath in allTargetPaths)
            {
                // 跳过已经被标记为零引用的
                if (allZeroRefAssets.Contains(targetPath))
                    continue;

                // 计算有效引用数（排除已模拟删除的资源）
                int effectiveRefCount = 0;
                if (referencedByMap.TryGetValue(targetPath, out HashSet<string> referencers))
                {
                    foreach (string refPath in referencers)
                    {
                        if (!simulatedDeleted.Contains(refPath))
                        {
                            effectiveRefCount++;
                        }
                    }
                }

                if (effectiveRefCount == 0)
                {
                    allZeroRefAssets.Add(targetPath);
                    simulatedDeleted.Add(targetPath);
                    newZeroRefCount++;
                }
            }

        } while (newZeroRefCount > 0 && round < maxRounds);

        // ========== 阶段3：填充结果 ==========
        foreach (string path in allTargetPaths)
        {
            int refCount = 0;
            if (referencedByMap.TryGetValue(path, out HashSet<string> refs))
            {
                // 计算最终有效引用数（排除所有零引用资源）
                refCount = refs.Count(r => !allZeroRefAssets.Contains(r));
            }
            referenceCounts[path] = refCount;

            if (allZeroRefAssets.Contains(path))
            {
                zeroReferenceAssets.Add(path);
            }
        }

        Debug.Log($"[一键整理-零引用检测] 完成！递归 {round} 轮，找到 {zeroReferenceAssets.Count} 个孤立资源");
    }

    /// <summary>
    /// 计算文件列表的总大小
    /// </summary>
    private long CalculateTotalFileSize(List<string> assetPaths)
    {
        if (assetPaths == null) return 0;  // 空指针保护
        long totalSize = 0;
        foreach (string assetPath in assetPaths)
        {
            string absolutePath = ConvertAssetPathToAbsolutePath(assetPath);
            if (!string.IsNullOrEmpty(absolutePath) && File.Exists(absolutePath))
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(absolutePath);
                    totalSize += fileInfo.Length;
                }
                catch
                {
                    // 忽略无法访问的文件
                }
            }
        }
        return totalSize;
    }

    /// <summary>
    /// 格式化文件大小显示
    /// </summary>
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    /// <summary>
    /// 显示整理报告对话框
    /// </summary>
    private void ShowOrganizationReportDialog(OrganizationReport report)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("═══════════════ 资源整理报告 ═══════════════");
        sb.AppendLine();
        sb.AppendLine($"📊 扫描统计:");
        sb.AppendLine($"   • 扫描资源数: {report.ScannedAssetCount}");
        sb.AppendLine($"   • 外部依赖数: {report.ExternalDependencyCount}");
        sb.AppendLine();
        sb.AppendLine($"📦 整合结果:");
        sb.AppendLine($"   • 已整合资源: {report.ConsolidatedAssetCount}");
        sb.AppendLine($"   • 更新引用数: {report.ReferenceUpdateCount}");
        sb.AppendLine($"   • 跳过资源数: {report.SkippedAssetCount}");
        sb.AppendLine();
        sb.AppendLine($"🗑️ 清理建议:");
        sb.AppendLine($"   • 零引用资源: {report.ZeroReferenceCount}");
        sb.AppendLine($"   • 预计可清理: {FormatFileSize(report.EstimatedCleanupSize)}");
        sb.AppendLine();
        sb.AppendLine($"⏰ 生成时间: {report.GeneratedTime:yyyy-MM-dd HH:mm:ss}");

        if (report.ZeroReferenceCount > 0)
        {
            bool deleteAll = EditorUtility.DisplayDialog("资源整理报告",
                sb.ToString() + "\n\n是否彻底清理所有零引用资源？\n（将循环删除直到完全清理干净）",
                "彻底清理", "稍后处理");

            if (deleteAll)
            {
                // 直接执行彻底清理，不再弹出二次确认
                DeleteAllZeroReferenceAssetsDeep();
            }
        }
        else
        {
            EditorUtility.DisplayDialog("资源整理报告", sb.ToString(), "确定");
        }
    }

    /// <summary>
    /// 导出完整的整理报告到文件
    /// </summary>
    private void ExportOrganizationReport()
    {
        if (lastOrganizationReport == null)
        {
            EditorUtility.DisplayDialog("提示", "没有可导出的报告，请先执行一键资源整理", "确定");
            return;
        }

        string reportPath = EditorUtility.SaveFilePanel("保存整理报告", "", $"OrganizationReport_{DateTime.Now:yyyyMMdd_HHmmss}", "csv");
        if (string.IsNullOrEmpty(reportPath))
        {
            return;
        }

        try
        {
            using (StreamWriter sw = new StreamWriter(reportPath, false, System.Text.Encoding.UTF8))
            {
                sw.WriteLine("类型,路径,状态,大小(字节)");

                // 写入整合的资源
                foreach (string path in lastOrganizationReport.ConsolidatedAssets)
                {
                    string absolutePath = ConvertAssetPathToAbsolutePath(path);
                    long size = 0;
                    if (!string.IsNullOrEmpty(absolutePath) && File.Exists(absolutePath))
                    {
                        size = new FileInfo(absolutePath).Length;
                    }
                    sw.WriteLine($"已整合,\"{path}\",成功,{size}");
                }

                // 写入零引用资源
                foreach (string path in lastOrganizationReport.ZeroReferenceAssets)
                {
                    string absolutePath = ConvertAssetPathToAbsolutePath(path);
                    long size = 0;
                    if (!string.IsNullOrEmpty(absolutePath) && File.Exists(absolutePath))
                    {
                        size = new FileInfo(absolutePath).Length;
                    }
                    sw.WriteLine($"零引用,\"{path}\",待清理,{size}");
                }
            }

            Debug.Log($"整理报告已导出到: {reportPath}");
            EditorUtility.RevealInFinder(reportPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"导出报告失败: {e.Message}");
            EditorUtility.DisplayDialog("导出失败", e.Message, "确定");
        }
    }

    #endregion

    #region Timeline残留清理

    private void DrawTimelineCleanupSection()
    {
        showTimelineCleanupSection = EditorGUILayout.Foldout(showTimelineCleanupSection,
            "🧹 Timeline残留清理", true);

        if (!showTimelineCleanupSection)
            return;

        EditorGUI.indentLevel++;

        // 操作按钮
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = consolidationMainFolder != null;
        if (GUILayout.Button("扫描残留", GUILayout.Height(24)))
        {
            ScanTimelineResiduals();
        }
        GUI.enabled = timelineCleanupResults.Count > 0 && timelineCleanupResults.Any(r => r.IsSelected);
        if (GUILayout.Button("清理选中", GUILayout.Height(24)))
        {
            CleanTimelineResiduals(false);
        }
        GUI.enabled = timelineCleanupResults.Count > 0;
        if (GUILayout.Button("全部清理", GUILayout.Height(24)))
        {
            CleanTimelineResiduals(true);
        }
        if (GUILayout.Button("清空结果", GUILayout.Height(24)))
        {
            timelineCleanupResults.Clear();
            timelineCleanupTotalBindings = 0;
            timelineCleanupTotalExposedRefs = 0;
            Repaint();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        // 统计信息
        if (timelineCleanupResults.Count > 0)
        {
            EditorGUILayout.HelpBox(
                $"发现 {timelineCleanupResults.Count} 个Prefab有残留，" +
                $"共 {timelineCleanupTotalBindings} 个空绑定，{timelineCleanupTotalExposedRefs} 个空引用",
                MessageType.Info);

            // 全选/取消全选
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全选", GUILayout.Width(60)))
            {
                foreach (var r in timelineCleanupResults) r.IsSelected = true;
            }
            if (GUILayout.Button("取消全选", GUILayout.Width(80)))
            {
                foreach (var r in timelineCleanupResults) r.IsSelected = false;
            }
            EditorGUILayout.EndHorizontal();

            // 残留列表
            timelineCleanupScrollPos = EditorGUILayout.BeginScrollView(
                timelineCleanupScrollPos, GUILayout.Height(200));

            foreach (var record in timelineCleanupResults)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                record.IsSelected = EditorGUILayout.Toggle(record.IsSelected, GUILayout.Width(20));

                string fileName = Path.GetFileName(record.PrefabPath);
                EditorGUILayout.LabelField(fileName, GUILayout.Width(250));
                EditorGUILayout.LabelField($"空绑定:{record.EmptyBindingCount}", GUILayout.Width(80));
                EditorGUILayout.LabelField($"空引用:{record.EmptyExposedRefCount}", GUILayout.Width(80));

                if (GUILayout.Button("定位", GUILayout.Width(40)))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(record.PrefabPath);
                    if (obj != null)
                    {
                        EditorGUIUtility.PingObject(obj);
                        Selection.activeObject = obj;
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        EditorGUI.indentLevel--;
    }

    private static readonly Regex SceneBindingEmptyEntryRegex = new Regex(
        @"  - key: \{fileID: 0\}\r?\n    value: \{fileID: 0\}\r?\n",
        RegexOptions.Compiled);

    private static readonly Regex ExposedRefEmptyEntryRegex = new Regex(
        @"    - [a-fA-F0-9]{32}: \{fileID: 0\}\r?\n",
        RegexOptions.Compiled);

    private void ScanTimelineResiduals()
    {
        timelineCleanupResults.Clear();
        timelineCleanupTotalBindings = 0;
        timelineCleanupTotalExposedRefs = 0;

        if (consolidationMainFolder == null)
            return;

        string folderPath = AssetDatabase.GetAssetPath(consolidationMainFolder);
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });

        int totalPrefabs = prefabGuids.Length;
        int processed = 0;

        try
        {
            foreach (string guid in prefabGuids)
            {
                processed++;
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (processed % 20 == 0)
                {
                    EditorUtility.DisplayProgressBar("扫描Timeline残留",
                        $"正在扫描 ({processed}/{totalPrefabs}): {Path.GetFileName(assetPath)}",
                        (float)processed / totalPrefabs);
                }

                string absolutePath = Path.GetFullPath(assetPath);
                if (!File.Exists(absolutePath))
                    continue;

                string content = File.ReadAllText(absolutePath);

                // 检查是否包含 PlayableDirector
                if (!content.Contains("PlayableDirector:"))
                    continue;

                int emptyBindings = SceneBindingEmptyEntryRegex.Matches(content).Count;
                int emptyExposedRefs = ExposedRefEmptyEntryRegex.Matches(content).Count;

                // 只有在 m_ExposedReferences 区域内的空引用才计算
                // 重新精确计算 ExposedRef 空引用
                emptyExposedRefs = CountEmptyExposedRefs(content);

                if (emptyBindings > 0 || emptyExposedRefs > 0)
                {
                    timelineCleanupResults.Add(new TimelineCleanupRecord
                    {
                        PrefabPath = assetPath,
                        EmptyBindingCount = emptyBindings,
                        EmptyExposedRefCount = emptyExposedRefs,
                        IsSelected = true
                    });
                    timelineCleanupTotalBindings += emptyBindings;
                    timelineCleanupTotalExposedRefs += emptyExposedRefs;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (timelineCleanupResults.Count == 0)
        {
            ShowNotification(new GUIContent("未发现Timeline残留"));
        }
        else
        {
            ShowNotification(new GUIContent($"发现 {timelineCleanupResults.Count} 个Prefab有残留"));
        }

        Repaint();
    }

    /// <summary>
    /// 精确统计 m_ExposedReferences.m_References 区域内 value 为 {fileID: 0} 的条目数
    /// </summary>
    private int CountEmptyExposedRefs(string content)
    {
        int count = 0;
        int searchStart = 0;

        while (true)
        {
            int refSectionStart = content.IndexOf("m_ExposedReferences:", searchStart, StringComparison.Ordinal);
            if (refSectionStart < 0) break;

            int mReferencesStart = content.IndexOf("m_References:", refSectionStart, StringComparison.Ordinal);
            if (mReferencesStart < 0) break;

            // 找到 m_References 的列表区域结束位置（下一个不以空格+'-'开头的非空行）
            int lineStart = content.IndexOf('\n', mReferencesStart);
            if (lineStart < 0) break;
            lineStart++;

            while (lineStart < content.Length)
            {
                int lineEnd = content.IndexOf('\n', lineStart);
                if (lineEnd < 0) lineEnd = content.Length;

                string line = content.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r');

                // 列表项以 "    - " 开头（4空格+破折号）
                if (line.StartsWith("    - "))
                {
                    if (line.Contains("{fileID: 0}"))
                    {
                        count++;
                    }
                }
                else if (line.Length > 0 && !string.IsNullOrWhiteSpace(line))
                {
                    // 不再是列表项，退出
                    break;
                }

                lineStart = lineEnd + 1;
            }

            searchStart = lineStart;
        }

        return count;
    }

    private void CleanTimelineResiduals(bool cleanAll)
    {
        var toClean = cleanAll
            ? timelineCleanupResults
            : timelineCleanupResults.Where(r => r.IsSelected).ToList();

        if (toClean.Count == 0)
        {
            ShowNotification(new GUIContent("没有选中要清理的Prefab"));
            return;
        }

        if (!EditorUtility.DisplayDialog("确认清理",
            $"即将清理 {toClean.Count} 个Prefab的Timeline残留：\n\n" +
            $"• 移除空的 SceneBindings 条目\n" +
            $"• 移除值为空的 ExposedReferences 条目\n\n" +
            "此操作不可撤销，是否继续？",
            "开始清理", "取消"))
        {
            return;
        }

        int cleanedCount = 0;
        int totalRemovedBindings = 0;
        int totalRemovedExposedRefs = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < toClean.Count; i++)
            {
                var record = toClean[i];

                EditorUtility.DisplayProgressBar("清理Timeline残留",
                    $"正在清理 ({i + 1}/{toClean.Count}): {Path.GetFileName(record.PrefabPath)}",
                    (float)(i + 1) / toClean.Count);

                string absolutePath = Path.GetFullPath(record.PrefabPath);
                if (!File.Exists(absolutePath))
                    continue;

                string content = File.ReadAllText(absolutePath);
                string originalContent = content;

                // 1. 清理空的 SceneBindings 条目
                content = SceneBindingEmptyEntryRegex.Replace(content, "");

                // 2. 清理 m_ExposedReferences 中 value 为 {fileID: 0} 的条目
                content = CleanEmptyExposedRefs(content);

                if (content != originalContent)
                {
                    File.WriteAllText(absolutePath, content);
                    totalRemovedBindings += record.EmptyBindingCount;
                    totalRemovedExposedRefs += record.EmptyExposedRefCount;
                    cleanedCount++;
                    Debug.Log($"[Timeline残留清理] 已清理: {record.PrefabPath} " +
                              $"(移除 {record.EmptyBindingCount} 个空绑定, {record.EmptyExposedRefCount} 个空引用)");
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        // 刷新资源
        AssetDatabase.Refresh();

        // 从结果列表中移除已清理的项
        if (cleanAll)
        {
            timelineCleanupResults.Clear();
        }
        else
        {
            timelineCleanupResults.RemoveAll(r => r.IsSelected);
        }

        // 重新计算统计
        timelineCleanupTotalBindings = timelineCleanupResults.Sum(r => r.EmptyBindingCount);
        timelineCleanupTotalExposedRefs = timelineCleanupResults.Sum(r => r.EmptyExposedRefCount);

        string message = $"已清理 {cleanedCount} 个Prefab\n移除 {totalRemovedBindings} 个空绑定，{totalRemovedExposedRefs} 个空引用";
        EditorUtility.DisplayDialog("清理完成", message, "确定");
        Debug.Log($"[Timeline残留清理] {message}");
        Repaint();
    }

    /// <summary>
    /// 精确清理 m_ExposedReferences.m_References 区域内 value 为 {fileID: 0} 的条目
    /// 保留有效引用的条目
    /// </summary>
    private string CleanEmptyExposedRefs(string content)
    {
        var sb = new StringBuilder();
        int searchStart = 0;

        while (true)
        {
            int refSectionStart = content.IndexOf("m_ExposedReferences:", searchStart, StringComparison.Ordinal);
            if (refSectionStart < 0)
            {
                sb.Append(content, searchStart, content.Length - searchStart);
                break;
            }

            int mReferencesStart = content.IndexOf("m_References:", refSectionStart, StringComparison.Ordinal);
            if (mReferencesStart < 0)
            {
                sb.Append(content, searchStart, content.Length - searchStart);
                break;
            }

            // 输出 m_References: 这一行（含换行）
            int lineAfterMRef = content.IndexOf('\n', mReferencesStart);
            if (lineAfterMRef < 0)
            {
                sb.Append(content, searchStart, content.Length - searchStart);
                break;
            }
            lineAfterMRef++;

            sb.Append(content, searchStart, lineAfterMRef - searchStart);

            // 遍历列表项
            int lineStart = lineAfterMRef;

            while (lineStart < content.Length)
            {
                int lineEnd = content.IndexOf('\n', lineStart);
                if (lineEnd < 0) lineEnd = content.Length;

                string line = content.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r');

                if (line.StartsWith("    - "))
                {
                    if (!line.Contains("{fileID: 0}"))
                    {
                        // 保留有效条目
                        sb.Append(content, lineStart, (lineEnd < content.Length ? lineEnd + 1 : lineEnd) - lineStart);
                    }
                    // 跳过 value 为 {fileID: 0} 的条目
                }
                else
                {
                    // 如果没有有效条目，列表变成空的，需要输出 " []" 或者保持空列表格式
                    // Unity 对于空的 m_References 使用 "m_References: []" 但这里列表被清空了
                    // 不需要额外处理，因为 YAML 列表为空时就是没有 "-" 行
                    searchStart = lineStart;
                    break;
                }

                lineStart = lineEnd < content.Length ? lineEnd + 1 : lineEnd;
                if (lineStart >= content.Length)
                {
                    searchStart = lineStart;
                    break;
                }
            }

            if (lineStart >= content.Length)
            {
                searchStart = content.Length;
                break;
            }
        }

        return sb.ToString();
    }

    #endregion

}