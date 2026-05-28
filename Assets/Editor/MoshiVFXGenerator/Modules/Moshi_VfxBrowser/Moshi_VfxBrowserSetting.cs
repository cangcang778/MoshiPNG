#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator.VfxBrowser
{
    /// <summary>
    /// 特效浏览器配置
    /// VFX Browser configuration
    /// </summary>
    public class Moshi_VfxBrowserSetting : ScriptableObject
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private static Moshi_VfxBrowserSetting _Instance;
        public static Moshi_VfxBrowserSetting Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = Moshi_VfxBrowserTools.LoadSettingAsset<Moshi_VfxBrowserSetting>(Moshi_VfxBrowserDefine.SETTINGS_PATH);
                    if (_Instance == null)
                    {
                        _Instance = CreateInstance<Moshi_VfxBrowserSetting>();
                        _Instance.Reset();
                        Moshi_VfxBrowserTools.SaveSettingAsset(_Instance, Moshi_VfxBrowserDefine.SETTINGS_PATH);
                    }
                }
                return _Instance;
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        [Header("通用设置 / General Settings")]
        [Tooltip("默认背景颜色强度\nDefault background color intensity")]
        public float DefaultColor = 0.3f;
        [Tooltip("播放时长补偿（秒）\nTime compensation for playback (seconds)")]
        [Min(0)] public float DefaulInterval = 0f;
        public bool ShowTapSpeed = true;
        public bool ShowTapGrid = true;
        public bool ShowTapColor = true;
        public bool ShowTapScale = true;
        public bool ShowTapFPS = true;
        public bool ShowTapGroup = true;
        public bool ShowTapFolder = true;
        [HideInInspector] public bool TitleSplitRow = true;
        [HideInInspector] public bool ShowRotateAxis = true;

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        [Header("分页按钮 / Page Button")]
        public int PageButtonHeight = 28;
        public int PageButtonTextSize = 12;
        public int PageButtonRowCount = 5;
        public Color PageButtonSelectedColor = new Color(0.2f, 0.6f, 1f, 1f);

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        [Header("拖尾设置 / Tail Settings")]
        public float TailMoveSpeed = 10f;
        [Tooltip("从 0 加速到目标速度的时长（秒）\nDuration to accelerate from 0 to target speed (seconds)")]
        public float TailAccelDuration = 1.0f;

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        [Header("子目录分组 / Subdirectory Grouping")]
        [Tooltip("分组标题文字颜色\nSection header title text color")]
        public Color SubdirectoryTitleColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        [Tooltip("分组标题背景色\nSection header background color")]
        public Color SubdirectorySectionColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        /// <summary>
        /// 是否启用子目录分组（EditorPrefs 持久化）
        /// Whether subdirectory grouping is enabled
        /// </summary>
        public bool UseSubdirectory
        {
            get => EditorPrefs.GetBool(Moshi_VfxBrowserDefine.PREF_USE_SUBDIRECTORY, false);
            set => EditorPrefs.SetBool(Moshi_VfxBrowserDefine.PREF_USE_SUBDIRECTORY, value);
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        [Header("圆角设置 / Rounded Corners")]
        [Tooltip("启用预览格子的圆角\nEnable rounded corners on preview cells")]
        public bool RoundedCorners = false;
        [Tooltip("圆角半径占格子尺寸的比例（0.02~0.2）\nCorner radius as proportion of cell size")]
        [Range(0.02f, 0.2f)]
        public float CellCornerRatio = 0.08f;

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        [Header("URP 渲染选项 / URP Render Options")]
        [Tooltip("启用后，扰曲/折射类特效可正确显示（推荐开启）\nEnable for correct distortion/refraction VFX rendering (recommended)")]
        public bool EnableUrpColorTexture = true;
        [Tooltip("启用后，软粒子/深度雾等依赖深度的特效可正确显示（推荐开启）\nEnable for correct soft-particle/depth-fog VFX rendering (recommended)")]
        public bool EnableUrpDepthTexture = true;

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        [Header("预设根目录 / Preset Root Folders")]
        [Tooltip("拖入常用的特效根目录，启动时会自动作为分页加入（E7）\nDrag in common VFX root folders, auto-added as pages on startup (E7)")]
        public DefaultAsset[] PresetRootFolders = new DefaultAsset[0];

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 重置为默认值
        /// Reset to default values
        /// </summary>
        public void Reset()
        {
            DefaultColor = 0.3f;
            DefaulInterval = 0f;
            ShowTapSpeed = true;
            ShowTapGrid = true;
            ShowTapColor = true;
            ShowTapScale = true;

            ShowTapFPS = true;
            ShowTapGroup = true;
            ShowTapFolder = true;
            TitleSplitRow = true;
            ShowRotateAxis = true;

            PageButtonHeight = 28;
            PageButtonTextSize = 12;
            PageButtonRowCount = 5;
            PageButtonSelectedColor = new Color(0.2f, 0.6f, 1f, 1f);

            TailMoveSpeed = 10f;
            TailAccelDuration = 1.0f;

            RoundedCorners = false;
            CellCornerRatio = 0.08f;

            UseSubdirectory = false;
            SubdirectoryTitleColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            SubdirectorySectionColor = new Color(0.15f, 0.15f, 0.15f, 1f);

            EnableUrpColorTexture = true;
            EnableUrpDepthTexture = true;
        }
    }

    /************************************************************************************************************************/

    [CustomEditor(typeof(Moshi_VfxBrowserSetting))]
    public class Moshi_VfxBrowserSettingEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 250f;
            base.OnInspectorGUI();
            EditorGUIUtility.labelWidth = originalLabelWidth;

            Moshi_VfxBrowserSetting settings = (Moshi_VfxBrowserSetting)target;

            GUILayout.Space(20);

            if (GUILayout.Button("恢复所有默认设置", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("恢复默认设置？",
                    "确定要将所有设置恢复到默认值吗？此操作可通过 Undo 撤销。",
                    "确定", "取消"))
                {
                    Undo.RecordObject(settings, "Reset Settings");
                    settings.Reset();
                    EditorUtility.SetDirty(settings);
                }
            }
        }
    }

    /************************************************************************************************************************/

    public class Moshi_VfxBrowserSettingsProvider : SettingsProvider
    {
        public Moshi_VfxBrowserSettingsProvider(string path, SettingsScope scopes, System.Collections.Generic.IEnumerable<string> keywords = null) : base(path, scopes, keywords) { }

        private Editor _editor;
        public override void OnGUI(string searchContext)
        {
            if (_editor == null)
            {
                _editor = Editor.CreateEditor(Moshi_VfxBrowserSetting.Instance);
            }
            _editor.OnInspectorGUI();
        }

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            var provider = new Moshi_VfxBrowserSettingsProvider(
                "Project/Moshi特效生成器/" + Moshi_VfxBrowserDefine.TOOL_NAME,
                SettingsScope.Project,
                new[] { "Moshi", "VFX", "Browser", "特效浏览器" });
            return provider;
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            if (_editor != null)
            {
                Object.DestroyImmediate(_editor);
                _editor = null;
            }
        }
    }
}
#endif
