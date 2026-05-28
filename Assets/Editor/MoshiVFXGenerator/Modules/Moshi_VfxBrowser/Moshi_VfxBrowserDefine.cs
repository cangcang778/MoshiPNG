#if UNITY_EDITOR
namespace MoshiVFXGenerator.VfxBrowser
{
    /// <summary>
    /// 特效浏览器全局常量定义
    /// VFX Browser global constants
    /// </summary>
    public class Moshi_VfxBrowserDefine
    {
        // 工具显示名（中文UI）
        // Tool display name (Chinese UI)
        public const string TOOL_NAME = "特效浏览器";

        // 配置文件路径（相对于当前脚本所在目录）
        // Config file paths (relative to current script directory)
        public const string SETTINGS_PATH = "Settings/Moshi_VfxBrowserSetting.asset";
        public const string FOLDERS_PATH = "Settings/Moshi_VfxBrowserFolder.asset";

        // 编辑器持久化键（Toolbar 设置）
        // EditorPrefs keys (Toolbar settings)
        public const string PREF_SPEED = "Moshi_VfxBrowser_Speed";
        public const string PREF_BG_GRAY = "Moshi_VfxBrowser_BgGray";
        public const string PREF_GRID_COUNT = "Moshi_VfxBrowser_GridCount";
        public const string PREF_CAM_DIST_MUL = "Moshi_VfxBrowser_CamDistMul";
        public const string PREF_HIGH_FPS = "Moshi_VfxBrowser_HighFps";
        public const string PREF_SHOW_HELP_TIPS = "Moshi_VfxBrowser_ShowHelpTips";
        public const string PREF_SHOW_NAME = "Moshi_VfxBrowser_ShowName";
        public const string PREF_USE_SUBDIRECTORY = "Moshi_VfxBrowser_UseSubdirectory";
        public const string PREF_LAST_PAGE_ID = "Moshi_VfxBrowser_LastPageID";
        public const string PREF_SEARCH_TEXT = "Moshi_VfxBrowser_SearchText";
        public const string PREF_ORTHO_MODE = "Moshi_VfxBrowser_OrthoMode";
        public const string PREF_SCROLL_KEYS = "Moshi_VfxBrowser_ScrollKeys";
        public const string PREF_SCROLL_X_PREFIX = "Moshi_VfxBrowser_ScrollX_";
        public const string PREF_SCROLL_Y_PREFIX = "Moshi_VfxBrowser_ScrollY_";

        /// <summary>
        /// 子目录分组数据结构
        /// Subfolder group data structure
        /// </summary>
        [System.Serializable]
        public class Moshi_SubfolderGroup
        {
            public string DisplayName; // 显示名称 / Display name
            public string FolderPath;  // 文件夹路径 / Folder path
            public int StartIndex;     // 起始索引 / Start index
            public int Count;          // 资源数量 / Item count
        }
    }
}
#endif
