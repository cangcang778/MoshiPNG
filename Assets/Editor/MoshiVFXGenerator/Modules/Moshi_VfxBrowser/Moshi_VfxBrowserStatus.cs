#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator.VfxBrowser
{
    public partial class Moshi_VfxBrowserWindow : EditorWindow
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 帮助提示内容（中文 / 英文并存）
        // Help tips content (Chinese / English)
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private static readonly string[] _HelpTips = new string[]
        {
            "左键：重播特效 / Left-Click: Replay",
            "按住左键：暂停移动 / Left-Hold: Stop movement",
            "右键：选中资源 / Right-Click: Select asset",
            "右键拖拽：旋转相机 / Right-Drag: Rotate camera",
            "中键拖拽：平移特效 / Middle-Drag: Pan effect",
            "Ctrl + 滚轮：整体缩放 / Ctrl + Scroll: Zoom all",
            "Shift + 滚轮：单格缩放 / Shift + Scroll: Zoom this cell",
            "右键悬停：打开菜单（收藏 / 时间线） / Hover Right-Click Menu: Favorite / Timeline",
            "点击状态栏：显示 / 隐藏此帮助 / Click Status Bar: Toggle help",
        };

        private float _StatusBtnStartX;

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制底部状态栏
        /// Draw bottom status bar
        /// </summary>
        private void DrawStatusBar()
        {
            if (_StatusLeftStyle == null)
            {
                _StatusLeftStyle = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip };
                _StatusRightStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip };
            }

            GUILayout.BeginHorizontal(EditorStyles.helpBox);

            var helpIcon = EditorGUIUtility.IconContent("d__Help");
            GUILayout.Label(helpIcon, GUILayout.ExpandWidth(false));

            string selectedInfo;
            if (_SelectedIdx >= 0 && _SelectedIdx < _FolderPrefabCache.Count && _FolderPrefabCache[_SelectedIdx] != null)
            {
                selectedInfo = $"{_SelectedIdx + 1}/{_CachedTotalCount} | {_FolderPrefabCache[_SelectedIdx].name}";
            }
            else
            {
                selectedInfo = $"总数: {_CachedTotalCount}";
            }
            EditorGUILayout.LabelField(selectedInfo, _StatusLeftStyle, GUILayout.MinWidth(0));

            GUILayout.FlexibleSpace();
            if (Event.current.type == EventType.Repaint) _StatusBtnStartX = GUILayoutUtility.GetLastRect().xMax;
            DrawStatusBarButtons();
            GUILayout.Space(1);

            GUILayout.EndHorizontal();

            var statusRect = GUILayoutUtility.GetLastRect();
            var clickWidth = _StatusBtnStartX > statusRect.x ? _StatusBtnStartX - statusRect.x : statusRect.width;
            var helpClickRect = new Rect(statusRect.x, statusRect.y, clickWidth, statusRect.height);
            var isHover = helpClickRect.Contains(Event.current.mousePosition);

            if (isHover && Event.current.type == EventType.Repaint)
            {
                var prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.08f);
                GUI.DrawTexture(helpClickRect, EditorGUIUtility.whiteTexture);
                GUI.color = prevColor;
            }

            EditorGUIUtility.AddCursorRect(helpClickRect, MouseCursor.Link);
            if (Event.current.type == EventType.MouseDown && isHover)
            {
                if (_CurrentPageID >= 0)
                {
                    _ShowHelpTips = !_ShowHelpTips;
                    Event.current.Use();
                }
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制状态栏右侧的功能按钮
        /// Draw function buttons on status bar right side
        /// </summary>
        private void DrawStatusBarButtons()
        {
            // 高帧率模式切换
            // High FPS toggle
            if (Moshi_VfxBrowserSetting.Instance.ShowTapFPS)
            {
                var modeIcon = EditorGUIUtility.IconContent(Moshi_VfxBrowserContext.HighFrameRateMode ? "IN foldout act" : "ArrowNavigationRight").image;
                var modeTooltip = Moshi_VfxBrowserContext.HighFrameRateMode ? "当前：高帧率" : "当前：默认";
                if (GUILayout.Button(new GUIContent("", modeIcon, modeTooltip), EditorStyles.toolbarButton, GUILayout.Width(30)))
                {
                    Moshi_VfxBrowserContext.HighFrameRateMode = !Moshi_VfxBrowserContext.HighFrameRateMode;
                    ShowNotification(new GUIContent(Moshi_VfxBrowserContext.HighFrameRateMode ? "已切换到高帧率模式" : "已切换到默认模式"));
                }
            }

            // 名称标签开关
            // Name label toggle
            Rect titleToggleRect = EditorGUILayout.GetControlRect(GUILayout.Width(16));
            titleToggleRect.y += 1f;
            _ShowName = EditorGUI.Toggle(titleToggleRect, _ShowName);
            Rect titleLabelRect = EditorGUILayout.GetControlRect(GUILayout.Width(32));
            titleLabelRect.y += 0.5f;
            GUI.Label(titleLabelRect, "名称", EditorStyles.label);

            // 子目录分组开关
            // Subdirectory grouping toggle
            if (Moshi_VfxBrowserSetting.Instance.ShowTapGroup)
            {
                Rect groupToggleRect = EditorGUILayout.GetControlRect(GUILayout.Width(16));
                groupToggleRect.y += 1f;
                bool oldGroupValue = Moshi_VfxBrowserSetting.Instance.UseSubdirectory;
                Moshi_VfxBrowserSetting.Instance.UseSubdirectory = EditorGUI.Toggle(groupToggleRect, oldGroupValue);
                Rect groupLabelRect = EditorGUILayout.GetControlRect(GUILayout.Width(42));
                groupLabelRect.y += 0.5f;
                GUI.Label(groupLabelRect, "分组", EditorStyles.label);
                if (oldGroupValue != Moshi_VfxBrowserSetting.Instance.UseSubdirectory)
                {
                    Moshi_VfxBrowserGrouper.BuildGroupedCache(_FolderPrefabCache, Moshi_VfxBrowserSetting.Instance.UseSubdirectory);
                    CleanupAll();
                    _SelectedIdx = -1;
                }
            }

            // Folder 配置按钮
            // Folder config button
            if (Moshi_VfxBrowserSetting.Instance.ShowTapFolder)
            {
                var folderIcon = EditorGUIUtility.isProSkin ? "d_Project" : "Project";
                var folderContent = new GUIContent(" 目录", EditorGUIUtility.IconContent(folderIcon).image, "打开分页目录配置");
                if (GUILayout.Button(folderContent, EditorStyles.toolbarButton, GUILayout.MaxWidth(80)))
                {
                    Selection.activeObject = Moshi_VfxBrowserFolder.Instance;
                    EditorGUIUtility.PingObject(Moshi_VfxBrowserFolder.Instance);
                }
            }

            // Setting 配置按钮
            // Setting config button
            var settingsIcon = EditorGUIUtility.isProSkin ? "d_Settings" : "Settings";
            var settingsContent = new GUIContent("", EditorGUIUtility.IconContent(settingsIcon).image, "打开配置文件");
            if (GUILayout.Button(settingsContent, EditorStyles.toolbarButton, GUILayout.MaxWidth(35)))
            {
                Selection.activeObject = Moshi_VfxBrowserSetting.Instance;
                EditorGUIUtility.PingObject(Moshi_VfxBrowserSetting.Instance);
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制帮助提示条
        /// Draw help tips bar
        /// </summary>
        private void DrawHelpTipsBar()
        {
            if (_CurrentPageID < 0) return;
            if (!_ShowHelpTips) return;

            if (_HelpTipsStyle == null)
            {
                _HelpTipsStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    wordWrap = false,
                    richText = true,
                    normal = { textColor = new Color(0.85f, 0.85f, 0.85f, 1f) },
                    padding = new RectOffset(6, 6, 2, 2),
                };
            }

            float lineHeight = _HelpTipsStyle.lineHeight + _HelpTipsStyle.padding.vertical;
            float totalHeight = _HelpTips.Length * lineHeight + 10f;

            var bgRect = GUILayoutUtility.GetRect(0f, totalHeight, GUILayout.ExpandWidth(true));
            bgRect.x += 4f;
            bgRect.width -= 8f;

            var prevColor = GUI.color;
            GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
            GUI.Box(bgRect, GUIContent.none, EditorStyles.helpBox);
            GUI.color = prevColor;

            float y = bgRect.y + 5f;
            for (int i = 0; i < _HelpTips.Length; i++)
            {
                var lineRect = new Rect(bgRect.x + 8f, y, bgRect.width - 16f, lineHeight);
                GUI.Label(lineRect, $"<b>·</b>  {_HelpTips[i]}", _HelpTipsStyle);
                y += lineHeight;
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制预览纹理（忽略 alpha 通道）
        /// Draw preview texture (ignore alpha channel)
        /// </summary>
        private void DrawTextureOpaque(Rect rect, Texture tex)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawPreviewTexture(rect, tex, null, ScaleMode.ScaleToFit);
        }
    }
}
#endif
