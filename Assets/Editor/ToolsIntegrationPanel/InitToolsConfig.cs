#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 工具配置初始化器
/// Tools config initializer
/// 
/// [InitializeOnLoad] 使编辑器启动/脚本重编译后自动检测 MyToolsConfig.asset 是否存在，
/// 若不存在则静默自动创建（适用于工具包导入新项目的场景）。
/// [InitializeOnLoad] auto-detects MyToolsConfig.asset on editor startup/recompile.
/// If missing, creates it silently (handles the case of importing into a new project).
/// </summary>
[InitializeOnLoad]
public static class InitToolsConfig
{
    private enum UpdateMode
    {
        ReplaceAll,
        SyncAndPrune
    }

    // 编辑器启动/脚本重编译时自动运行
    // Runs automatically on editor startup / script recompilation
    static InitToolsConfig()
    {
        // 延迟执行，确保 AssetDatabase 已就绪
        // Delay to ensure AssetDatabase is ready
        EditorApplication.delayCall += AutoSyncConfig;
    }

    /// <summary>
    /// 自动同步配置（静默，无弹窗）：
    ///   - 配置不存在 → 自动创建
    ///   - 配置已存在 → 自动同步（补充项目中新增的工具 / 清理失效条目），保留原有排列和快捷访问设置
    /// Auto-sync config (silent, no dialog):
    ///   - Config missing → auto create
    ///   - Config exists  → auto sync (add new tools / prune dead entries), preserve order & quick-access flags
    /// </summary>
    private static void AutoSyncConfig()
    {
        string assetPath = ToolsConfig.DefaultAssetPath;

        try
        {
            ToolBindingCatalog.InvalidateCache();
            Dictionary<string, ToolItem> templateLookup = BuildTemplateLookup(ToolBindingCatalog.CreateToolItemsSnapshot());

            if (templateLookup.Count == 0)
            {
                // ToolBindingCatalog 尚未就绪，延迟重试
                // ToolBindingCatalog not ready yet, retry
                EditorApplication.delayCall += AutoSyncConfig;
                return;
            }

            ToolsConfig config = AssetDatabase.LoadAssetAtPath<ToolsConfig>(assetPath);
            bool existed = config != null;

            if (!existed)
            {
                // 不存在：全量创建
                // Not found: create from scratch
                config = ScriptableObject.CreateInstance<ToolsConfig>();
                config.name = Path.GetFileNameWithoutExtension(assetPath);
                config.ReplaceAllTools(CloneTools(templateLookup.Values));
                EnsureDirectoryExists(assetPath);
                AssetDatabase.CreateAsset(config, assetPath);
                Debug.Log($"[MoshiTools] MyToolsConfig.asset 已自动创建：{assetPath}");
            }
            else
            {
                // 已存在：静默同步（补充新工具、清理失效条目，保留用户的排列/快捷访问设置）
                // Exists: silently sync (add new tools, prune dead ones, keep user's order & flags)
                int addedCount;
                int removedCount;
                List<ToolItem> merged = MergeExistingTools(config, templateLookup, out addedCount, out removedCount);

                if (addedCount == 0 && removedCount == 0)
                    return; // 无变化，跳过写入 / No changes, skip write

                config.ReplaceAllTools(merged);
                Debug.Log($"[MoshiTools] MyToolsConfig.asset 已自动同步：新增 {addedCount} 个工具，移除 {removedCount} 个失效条目");
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ToolsIntegrationPanel.RequestConfigRefresh();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[MoshiTools] 自动同步 MyToolsConfig.asset 失败，请手动点击「配置刷新」按钮：{ex.Message}");
        }
    }

    public static void CreateOrResetToolsConfig()
    {
        ApplyUpdate(UpdateMode.ReplaceAll);
    }

    public static void SyncAndPruneToolsConfig()
    {
        ApplyUpdate(UpdateMode.SyncAndPrune);
    }

    private static void ApplyUpdate(UpdateMode mode)
    {
        string assetPath = ToolsConfig.DefaultAssetPath;
        ToolsConfig config = AssetDatabase.LoadAssetAtPath<ToolsConfig>(assetPath);
        bool existedBefore = config != null;

        if (!existedBefore)
        {
            config = ScriptableObject.CreateInstance<ToolsConfig>();
            config.name = Path.GetFileNameWithoutExtension(assetPath);
        }
        else if (mode == UpdateMode.ReplaceAll)
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "覆盖现有配置?",
                $"已在 {assetPath} 找到 ToolsConfig。\n是否覆盖为示例配置?",
                "覆盖",
                "取消");

            if (!overwrite)
            {
                Selection.activeObject = config;
                return;
            }
        }

        ToolBindingCatalog.InvalidateCache();
        Dictionary<string, ToolItem> templateLookup = BuildTemplateLookup(ToolBindingCatalog.CreateToolItemsSnapshot());

        if (mode == UpdateMode.ReplaceAll || !existedBefore)
        {
            config.ReplaceAllTools(CloneTools(templateLookup.Values));
            SaveConfigAsset(config, assetPath, existedBefore);
            Selection.activeObject = config;
            ToolsIntegrationPanel.RequestConfigRefresh();
            EditorUtility.DisplayDialog("完成", $"ToolsConfig 已更新\n{assetPath}", "好的");
            return;
        }

        int addedCount;
        int removedCount;
        List<ToolItem> merged = MergeExistingTools(config, templateLookup, out addedCount, out removedCount);
        config.ReplaceAllTools(merged);
        SaveConfigAsset(config, assetPath, existedBefore);
        Selection.activeObject = config;
        ToolsIntegrationPanel.RequestConfigRefresh();

        EditorUtility.DisplayDialog(
            "完成",
            $"配置刷新完成。\n新增 {addedCount} 个工具，移除 {removedCount} 个失效 Binding。",
            "好的");
    }

    private static Dictionary<string, ToolItem> BuildTemplateLookup(IEnumerable<ToolItem> templates)
    {
        Dictionary<string, ToolItem> lookup = new Dictionary<string, ToolItem>();
        if (templates == null)
        {
            return lookup;
        }

        foreach (ToolItem tool in templates)
        {
            if (tool == null || string.IsNullOrEmpty(tool.bindingKey))
            {
                continue;
            }

            if (!lookup.ContainsKey(tool.bindingKey))
            {
                lookup.Add(tool.bindingKey, tool);
            }
        }

        return lookup;
    }

    private static List<ToolItem> MergeExistingTools(
        ToolsConfig config,
        IReadOnlyDictionary<string, ToolItem> templateLookup,
        out int addedCount,
        out int removedCount)
    {
        List<ToolItem> merged = new List<ToolItem>();
        HashSet<string> retainedKeys = new HashSet<string>();
        addedCount = 0;
        removedCount = 0;

        foreach (ToolItem existing in config.AllTools)
        {
            if (existing == null || string.IsNullOrEmpty(existing.bindingKey))
            {
                removedCount++;
                continue;
            }

            if (!templateLookup.ContainsKey(existing.bindingKey))
            {
                removedCount++;
                continue;
            }

            merged.Add(existing);
            retainedKeys.Add(existing.bindingKey);
        }

        foreach (KeyValuePair<string, ToolItem> pair in templateLookup)
        {
            if (retainedKeys.Contains(pair.Key))
            {
                continue;
            }

            merged.Add(CloneTool(pair.Value));
            addedCount++;
        }

        return merged;
    }

    private static List<ToolItem> CloneTools(IEnumerable<ToolItem> source)
    {
        List<ToolItem> cloned = new List<ToolItem>();
        if (source == null)
        {
            return cloned;
        }

        foreach (ToolItem tool in source)
        {
            ToolItem copy = CloneTool(tool);
            if (copy != null)
            {
                cloned.Add(copy);
            }
        }

        return cloned;
    }

    private static ToolItem CloneTool(ToolItem source)
    {
        if (source == null)
        {
            return null;
        }

        return new ToolItem
        {
            toolName = ToolNameOptimizer.GetShortName(source.toolName),
            toolDescription = source.toolDescription,
            category = source.category,
            showInQuickAccess = source.showInQuickAccess,
            bindingKey = source.bindingKey
        };
    }

    private static void SaveConfigAsset(ToolsConfig config, string assetPath, bool existedBefore)
    {
        if (!existedBefore)
        {
            EnsureDirectoryExists(assetPath);
            AssetDatabase.CreateAsset(config, assetPath);
        }

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureDirectoryExists(string assetPath)
    {
        string directory = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
#endif
