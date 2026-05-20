#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[Serializable]
public sealed class FavoriteAssetEntry
{
    [Tooltip("面板中显示的名称，留空则使用资源名")]
    public string displayName;

    [Tooltip("收藏的文件或文件夹引用")]
    public UnityEngine.Object assetReference;

    [Tooltip("给自己的备注")]
    [TextArea(1, 4)]
    public string note;
}

/// <summary>
/// Hierarchy 收藏条目 - 记录场景中的 GameObject
/// </summary>
[Serializable]
public sealed class HierarchyFavoriteEntry
{
    [Tooltip("显示名称")]
    public string displayName;

    [Tooltip("场景路径 (如 Assets/Scenes/Main.unity)")]
    public string scenePath;

    [Tooltip("GameObject 层级路径 (如 Canvas/Panel/Button)")]
    public string gameObjectPath;

    [Tooltip("备注")]
    [TextArea(1, 4)]
    public string note;

    // 运行时缓存（不序列化）
    [NonSerialized]
    public GameObject cachedObject;
}

public sealed class Moshi_FavoritesConfig : ScriptableObject
{
    public const string DefaultAssetPath = "Assets/Editor/ToolsIntegrationPanel/AllToolsResources/Moshi_FavoritesConfig.asset";

    [SerializeField]
    private List<FavoriteAssetEntry> favoriteEntries = new List<FavoriteAssetEntry>();

    [SerializeField]
    private List<HierarchyFavoriteEntry> hierarchyEntries = new List<HierarchyFavoriteEntry>();

    public IReadOnlyList<FavoriteAssetEntry> FavoriteEntries => favoriteEntries;
    public List<FavoriteAssetEntry> EditableEntries => favoriteEntries;

    public IReadOnlyList<HierarchyFavoriteEntry> HierarchyEntries => hierarchyEntries;
    public List<HierarchyFavoriteEntry> EditableHierarchyEntries => hierarchyEntries;

    public static Moshi_FavoritesConfig LoadOrCreate()
    {
        Moshi_FavoritesConfig config = AssetDatabase.LoadAssetAtPath<Moshi_FavoritesConfig>(DefaultAssetPath);
        if (config != null)
        {
            return config;
        }

        config = CreateInstance<Moshi_FavoritesConfig>();
        config.name = Path.GetFileNameWithoutExtension(DefaultAssetPath);
        EnsureDirectoryExists(DefaultAssetPath);
        AssetDatabase.CreateAsset(config, DefaultAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return config;
    }

    public bool ContainsAsset(UnityEngine.Object asset)
    {
        if (asset == null)
        {
            return false;
        }

        string targetPath = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(targetPath))
        {
            return false;
        }

        foreach (FavoriteAssetEntry entry in favoriteEntries)
        {
            if (entry?.assetReference == null)
            {
                continue;
            }

            string path = AssetDatabase.GetAssetPath(entry.assetReference);
            if (!string.IsNullOrEmpty(path) &&
                string.Equals(path, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 检查是否已包含指定的 Hierarchy 对象
    /// </summary>
    public bool ContainsHierarchyEntry(string scenePath, string gameObjectPath)
    {
        if (string.IsNullOrEmpty(scenePath) || string.IsNullOrEmpty(gameObjectPath))
        {
            return false;
        }

        foreach (HierarchyFavoriteEntry entry in hierarchyEntries)
        {
            if (entry == null) continue;
            
            if (string.Equals(entry.scenePath, scenePath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.gameObjectPath, gameObjectPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
