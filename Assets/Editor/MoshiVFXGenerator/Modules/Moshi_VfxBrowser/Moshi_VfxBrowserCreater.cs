#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator.VfxBrowser
{
    /// <summary>
    /// 特效浏览器菜单入口
    /// VFX Browser menu entry
    /// </summary>
    public static class Moshi_VfxBrowserCreater
    {
        /// <summary>
        /// 打开特效浏览器窗口
        /// Open VFX Browser window
        /// </summary>
        [MenuItem("工具/Moshi特效生成器/特效浏览器")]
        public static void ShowWindow()
        {
            // 关闭现有窗口
            // Close existing window
            var existingWindows = Resources.FindObjectsOfTypeAll<Moshi_VfxBrowserWindow>();
            foreach (var existingWindow in existingWindows)
            {
                if (existingWindow != null) existingWindow.Close();
            }
            if (existingWindows.Length > 0) return;

            var window = EditorWindow.GetWindow<Moshi_VfxBrowserWindow>(Moshi_VfxBrowserDefine.TOOL_NAME);
            var icon = EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_Particle Effect" : "Particle Effect").image;
            window.titleContent = new GUIContent(Moshi_VfxBrowserDefine.TOOL_NAME, icon);
            window.minSize = new Vector2(400, 300);
        }
    }
}
#endif
