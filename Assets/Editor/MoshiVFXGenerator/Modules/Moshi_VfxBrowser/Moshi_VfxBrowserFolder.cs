#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#if MOSHI_VFX_GRAPH_INSTALLED
using UnityEngine.VFX;
#endif

namespace MoshiVFXGenerator.VfxBrowser
{
    /// <summary>
    /// 特效浏览器的文件夹分页配置
    /// VFX Browser folder page configuration
    /// </summary>
    public class Moshi_VfxBrowserFolder : ScriptableObject
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private static Moshi_VfxBrowserFolder _Instance;
        public static Moshi_VfxBrowserFolder Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = Moshi_VfxBrowserTools.LoadSettingAsset<Moshi_VfxBrowserFolder>(Moshi_VfxBrowserDefine.FOLDERS_PATH);
                    if (_Instance == null)
                    {
                        _Instance = CreateInstance<Moshi_VfxBrowserFolder>();
                        Moshi_VfxBrowserTools.SaveSettingAsset(_Instance, Moshi_VfxBrowserDefine.FOLDERS_PATH);
                    }
                }
                return _Instance;
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 简单文件夹分页：直接拖入文件夹即可
        // Simple folder pages: just drag folders in
        public List<Object> SimpleFolderPages = new List<Object>();

        // 收藏夹：收藏的单个 Prefab 列表（E4）
        // Favorites: list of favorited prefabs (E4)
        public List<GameObject> FavoritePrefabs = new List<GameObject>();

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 获取有效的文件夹列表（过滤 null）
        /// Get valid folder list (filter null)
        /// </summary>
        private List<Object> GetValidFolders()
        {
            List<Object> valid = new List<Object>();
            foreach (var folder in SimpleFolderPages)
            {
                if (folder != null) valid.Add(folder);
            }
            return valid;
        }

        /// <summary>
        /// 获取所有文件夹分页的名称列表（含 "收藏" 虚拟分页）
        /// Get all folder page names (includes virtual "Favorites" page)
        /// </summary>
        public List<string> GetPageNames()
        {
            List<string> names = new List<string>();
            // 虚拟分页：收藏夹（始终在第 0 位）
            // Virtual page: Favorites (always at index 0)
            names.Add("★ 收藏");
            foreach (var folder in GetValidFolders())
            {
                names.Add(folder.name);
            }
            return names;
        }

        /// <summary>
        /// 判断某个 pageIndex 是否为收藏夹虚拟分页
        /// Check if pageIndex is the Favorites virtual page
        /// </summary>
        public bool IsFavoritesPage(int pageIndex)
        {
            return pageIndex == 0;
        }

        /// <summary>
        /// 获取指定分页内的所有特效 Prefab
        /// Get all VFX prefabs in the specified folder page
        /// </summary>
        public List<GameObject> GetPrefabs(int pageIndex)
        {
            // 收藏夹虚拟分页
            // Favorites virtual page
            if (IsFavoritesPage(pageIndex))
            {
                List<GameObject> favs = new List<GameObject>();
                foreach (var p in FavoritePrefabs)
                {
                    if (p != null) favs.Add(p);
                }
                return favs;
            }

            List<GameObject> prefabs = new List<GameObject>();
            List<Object> validFolders = GetValidFolders();
            int realIndex = pageIndex - 1; // 偏移掉收藏夹 / offset Favorites
            if (realIndex < 0 || realIndex >= validFolders.Count) return prefabs;

            Object folder = validFolders[realIndex];
            if (folder == null) return prefabs;

            string folderPath = AssetDatabase.GetAssetPath(folder);
            if (!AssetDatabase.IsValidFolder(folderPath)) return prefabs;

            // 搜索文件夹内所有 Prefab（包含子目录）
            // Search all prefabs inside the folder (including subdirectories)
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) continue;

                if (Moshi_VfxBrowserTools.HasVfxComponent(prefab))
                {
                    prefabs.Add(prefab);
                }
            }

            return prefabs;
        }

        /// <summary>
        /// 移除指定分页（不支持移除收藏夹虚拟分页）
        /// Remove folder page (can't remove Favorites virtual page)
        /// </summary>
        public void RemovePage(int pageIndex)
        {
            if (IsFavoritesPage(pageIndex)) return;

            List<Object> validFolders = GetValidFolders();
            int realIndex = pageIndex - 1;
            if (realIndex < 0 || realIndex >= validFolders.Count) return;

            Object target = validFolders[realIndex];
            SimpleFolderPages.Remove(target);

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 判断 Prefab 是否已收藏
        /// Check if prefab is favorited
        /// </summary>
        public bool IsFavorite(GameObject prefab)
        {
            return prefab != null && FavoritePrefabs.Contains(prefab);
        }

        /// <summary>
        /// 切换收藏状态（E4）
        /// Toggle favorite state (E4)
        /// </summary>
        public void ToggleFavorite(GameObject prefab)
        {
            if (prefab == null) return;
            if (FavoritePrefabs.Contains(prefab))
            {
                FavoritePrefabs.Remove(prefab);
            }
            else
            {
                FavoritePrefabs.Add(prefab);
            }
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
