#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static MoshiVFXGenerator.VfxBrowser.Moshi_VfxBrowserDefine;

namespace MoshiVFXGenerator.VfxBrowser
{
    /// <summary>
    /// 特效浏览器子目录分组绘制器
    /// VFX Browser subfolder group drawer
    /// </summary>
    public static class Moshi_VfxBrowserGrouper
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        public static List<Moshi_SubfolderGroup> GroupedCache;
        public static HashSet<string> CollapsedSubfolders = new HashSet<string>();

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        public static void ClearCache()
        {
            GroupedCache = null;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 构建按子目录分组的缓存数据
        /// Build grouped cache by subdirectories
        /// </summary>
        public static void BuildGroupedCache(List<GameObject> prefabs, bool enabled)
        {
            if (prefabs == null || prefabs.Count == 0 || !enabled)
            {
                GroupedCache = null;
                return;
            }

            var dirOrder = new List<string>();
            var dirItems = new Dictionary<string, List<GameObject>>();
            for (int i = 0; i < prefabs.Count; i++)
            {
                var obj = prefabs[i];
                string assetPath = obj != null ? AssetDatabase.GetAssetPath(obj) : "";
                string dir = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "";
                if (!dirItems.ContainsKey(dir))
                {
                    dirOrder.Add(dir);
                    dirItems[dir] = new List<GameObject>();
                }
                dirItems[dir].Add(obj);
            }

            prefabs.Clear();
            GroupedCache = new List<Moshi_SubfolderGroup>();
            foreach (var dir in dirOrder)
            {
                var items = dirItems[dir];
                string displayName = System.IO.Path.GetFileName(dir);
                if (string.IsNullOrEmpty(displayName)) displayName = dir;
                var group = new Moshi_SubfolderGroup
                {
                    FolderPath = dir,
                    DisplayName = displayName,
                    StartIndex = prefabs.Count,
                    Count = items.Count
                };
                GroupedCache.Add(group);
                prefabs.AddRange(items);
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private static GUIStyle _FoldoutStyle;
        private static Color _CachedTitleColor;

        public static bool DrawSubfolderSectionHeader(string displayName, string folderPath)
        {
            float height = 22f;
            float padding = 4f;

            Rect rect = GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true));
            rect.x += padding;
            rect.width -= padding * 2;

            bool repaint = DrawSubfolderSectionHeaderCore(rect, displayName, folderPath);
            GUILayout.Space(4f);
            return repaint;
        }

        public static bool DrawSubfolderSectionHeaderAbsolute(Rect rect, string displayName, string folderPath)
        {
            return DrawSubfolderSectionHeaderCore(rect, displayName, folderPath);
        }

        private static bool DrawSubfolderSectionHeaderCore(Rect rect, string displayName, string folderPath)
        {
            bool repaint = false;

            EditorGUI.DrawRect(rect, Moshi_VfxBrowserSetting.Instance.SubdirectorySectionColor);

            bool isCollapsed = CollapsedSubfolders.Contains(folderPath);

            EnsureFoldoutStyle();

            Rect foldoutRect = new Rect(rect.x + 6, rect.y + 1, rect.width - 12, rect.height - 2);
            bool expanded = EditorGUI.Foldout(foldoutRect, !isCollapsed, " " + displayName, true, _FoldoutStyle);

            if (expanded && isCollapsed)
            {
                CollapsedSubfolders.Remove(folderPath);
                repaint = true;
            }
            else if (!expanded && !isCollapsed)
            {
                CollapsedSubfolders.Add(folderPath);
                repaint = true;
            }

            return repaint;
        }

        private static void EnsureFoldoutStyle()
        {
            var titleColor = Moshi_VfxBrowserSetting.Instance.SubdirectoryTitleColor;
            if (_FoldoutStyle == null || _CachedTitleColor != titleColor)
            {
                _FoldoutStyle = new GUIStyle(EditorStyles.foldout);
                _FoldoutStyle.fontStyle = FontStyle.Bold;
                _FoldoutStyle.fontSize = 11;
                _FoldoutStyle.normal.textColor = titleColor;
                _FoldoutStyle.onNormal.textColor = titleColor;
                _FoldoutStyle.active.textColor = titleColor;
                _FoldoutStyle.onActive.textColor = titleColor;
                _FoldoutStyle.focused.textColor = titleColor;
                _FoldoutStyle.onFocused.textColor = titleColor;
                _CachedTitleColor = titleColor;
            }
        }
    }
}
#endif
