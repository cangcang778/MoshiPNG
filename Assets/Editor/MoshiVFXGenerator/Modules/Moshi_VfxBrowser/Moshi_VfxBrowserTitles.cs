#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator.VfxBrowser
{
    public partial class Moshi_VfxBrowserWindow : EditorWindow
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制工具栏（滑动条 + 搜索框 E1）
        /// Draw toolbar (sliders + search box E1)
        /// </summary>
        private void DrawToolbar()
        {
            // 占位 Toggle，转移默认焦点
            // Dummy toggle to capture default focus
            GUI.SetNextControlName("FocusDummy");
            EditorGUI.Toggle(new Rect(-10, -10, 0, 0), false);

            if (_NeedFocusDummy)
            {
                _NeedFocusDummy = false;
                GUI.FocusControl("FocusDummy");
            }

            // 搜索框（E1）
            // Search box (E1)
            DrawSearchBox();

            bool hasSliders = Moshi_VfxBrowserSetting.Instance.ShowTapSpeed
                           || Moshi_VfxBrowserSetting.Instance.ShowTapGrid
                           || Moshi_VfxBrowserSetting.Instance.ShowTapColor
                           || Moshi_VfxBrowserSetting.Instance.ShowTapScale;

            if (hasSliders)
            {
                GUILayout.BeginHorizontal(EditorStyles.helpBox);
                {
                    var splitRow = Moshi_VfxBrowserSetting.Instance.TitleSplitRow;
                    var offsetFix = splitRow ? 0 : 1;

                    var fixedSlider = new GUIStyle(GUI.skin.horizontalSlider);
                    var fixedLabel = new GUIStyle(EditorStyles.label);
                    fixedSlider.margin.top += 1 + offsetFix;
                    fixedLabel.margin.top += 2 + offsetFix;

                    DrawSliderControls(fixedSlider, fixedLabel);
                    GUILayout.Space(1);
                }
                GUILayout.EndHorizontal();
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制搜索框（E1）
        /// Draw search box (E1)
        /// </summary>
        private void DrawSearchBox()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label(EditorGUIUtility.IconContent("Search Icon"), EditorStyles.label, GUILayout.Width(20), GUILayout.Height(18));

            GUI.SetNextControlName("Moshi_VfxBrowser_SearchField");
            string newSearch = EditorGUILayout.TextField(_SearchText, EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true));
            if (newSearch != _SearchText)
            {
                _SearchText = newSearch;
                // 重新应用搜索过滤
                // Re-apply search filter
                ApplySearchFilter();
                Repaint();
            }

            if (!string.IsNullOrEmpty(_SearchText))
            {
                if (GUILayout.Button("清除", EditorStyles.toolbarButton, GUILayout.Width(40)))
                {
                    _SearchText = string.Empty;
                    ApplySearchFilter();
                    GUI.FocusControl("FocusDummy");
                }
            }

            GUILayout.Label($"{_CachedTotalCount}", EditorStyles.miniLabel, GUILayout.Width(40));

            GUILayout.EndHorizontal();
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制滑动条控件
        /// Draw slider controls
        /// </summary>
        private void DrawSliderControls(GUIStyle fixedSlider, GUIStyle fixedLabel)
        {
            if (Moshi_VfxBrowserSetting.Instance.ShowTapSpeed)
            {
                GUILayout.Space(4);
                DrawClickableLabel("速度", "点击恢复默认值 1.0", 36, () => _Speed = 1f);
                _Speed = GUILayout.HorizontalSlider(_Speed, 0.1f, 3f, fixedSlider, GUI.skin.horizontalSliderThumb, GUILayout.ExpandWidth(true), GUILayout.MinWidth(60));
                GUILayout.Label(_Speed.ToString("F1"), fixedLabel, GUILayout.Width(25));
                DrawToolbarSeparator();
            }

            if (Moshi_VfxBrowserSetting.Instance.ShowTapGrid)
            {
                DrawClickableLabel("格数", "点击恢复默认值 16", 36, () => _GridCount = 16);
                _GridCount = (int)GUILayout.HorizontalSlider(_GridCount, 1, 50, fixedSlider, GUI.skin.horizontalSliderThumb, GUILayout.ExpandWidth(true), GUILayout.MinWidth(60));
                GUILayout.Label(_GridCount.ToString(), fixedLabel, GUILayout.Width(20));
                DrawToolbarSeparator();
            }

            if (Moshi_VfxBrowserSetting.Instance.ShowTapColor)
            {
                DrawClickableLabel("背景", "点击恢复默认值", 36, () => _BgGray = 1f);
                _BgGray = GUILayout.HorizontalSlider(_BgGray, 0f, 2f, fixedSlider, GUI.skin.horizontalSliderThumb, GUILayout.ExpandWidth(true), GUILayout.MinWidth(60));
                GUILayout.Label(_BgGray.ToString("F2"), fixedLabel, GUILayout.Width(30));
                Color bgColor = new Color(_BgGray * Moshi_VfxBrowserSetting.Instance.DefaultColor, _BgGray * Moshi_VfxBrowserSetting.Instance.DefaultColor, _BgGray * Moshi_VfxBrowserSetting.Instance.DefaultColor, 1f);
                foreach (var ctx in _Contexts.Values)
                {
                    ctx.SetBackgroundColor(bgColor);
                }
                DrawToolbarSeparator();
            }

            if (Moshi_VfxBrowserSetting.Instance.ShowTapScale)
            {
                // 点击"缩放"标签：恢复默认值并同步到所有格子
                // Click "缩放" label: reset to default and sync to all cells
                DrawClickableLabel("缩放", "点击恢复默认值 1.0", 36, () =>
                {
                    _CamDistMul = 1.0f;
                    foreach (var ctx in _Contexts.Values)
                    {
                        ctx.SetDistanceMultiplier(_CamDistMul);
                    }
                });

                // 拖动滑块：仅在值真正变化时才同步到所有格子（避免每帧覆盖单格缩放）
                // Drag slider: only sync when value actually changes (preserve per-cell zoom)
                EditorGUI.BeginChangeCheck();
                _CamDistMul = GUILayout.HorizontalSlider(_CamDistMul, 0.1f, 3f, fixedSlider, GUI.skin.horizontalSliderThumb, GUILayout.ExpandWidth(true), GUILayout.MinWidth(60));
                GUILayout.Label(_CamDistMul.ToString("F1"), fixedLabel, GUILayout.Width(25));
                if (EditorGUI.EndChangeCheck())
                {
                    foreach (var ctx in _Contexts.Values)
                    {
                        ctx.SetDistanceMultiplier(_CamDistMul);
                    }
                }
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 保存 Toolbar 设置到 EditorPrefs
        /// Save toolbar settings to EditorPrefs
        /// </summary>
        private void SaveToolbarSettings()
        {
            EditorPrefs.SetFloat(Moshi_VfxBrowserDefine.PREF_SPEED, _Speed);
            EditorPrefs.SetFloat(Moshi_VfxBrowserDefine.PREF_BG_GRAY, _BgGray);
            EditorPrefs.SetInt(Moshi_VfxBrowserDefine.PREF_GRID_COUNT, _GridCount);
            EditorPrefs.SetFloat(Moshi_VfxBrowserDefine.PREF_CAM_DIST_MUL, _CamDistMul);
            EditorPrefs.SetBool(Moshi_VfxBrowserDefine.PREF_HIGH_FPS, Moshi_VfxBrowserContext.HighFrameRateMode);
            EditorPrefs.SetBool(Moshi_VfxBrowserDefine.PREF_SHOW_HELP_TIPS, _ShowHelpTips);
            EditorPrefs.SetBool(Moshi_VfxBrowserDefine.PREF_SHOW_NAME, _ShowName);
            EditorPrefs.SetInt(Moshi_VfxBrowserDefine.PREF_LAST_PAGE_ID, _CurrentPageID);
            EditorPrefs.SetString(Moshi_VfxBrowserDefine.PREF_SEARCH_TEXT, _SearchText ?? "");

            var keys = new System.Text.StringBuilder();
            foreach (var kv in _PageScrollPos)
            {
                if (keys.Length > 0) keys.Append(',');
                keys.Append(kv.Key);
                EditorPrefs.SetFloat(Moshi_VfxBrowserDefine.PREF_SCROLL_X_PREFIX + kv.Key, kv.Value.x);
                EditorPrefs.SetFloat(Moshi_VfxBrowserDefine.PREF_SCROLL_Y_PREFIX + kv.Key, kv.Value.y);
            }
            EditorPrefs.SetString(Moshi_VfxBrowserDefine.PREF_SCROLL_KEYS, keys.ToString());
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 从 EditorPrefs 加载 Toolbar 设置
        /// Load toolbar settings from EditorPrefs
        /// </summary>
        private void LoadToolbarSettings()
        {
            _Speed = EditorPrefs.GetFloat(Moshi_VfxBrowserDefine.PREF_SPEED, 1f);
            _BgGray = EditorPrefs.GetFloat(Moshi_VfxBrowserDefine.PREF_BG_GRAY, 1f);
            _GridCount = EditorPrefs.GetInt(Moshi_VfxBrowserDefine.PREF_GRID_COUNT, 16);
            _CamDistMul = EditorPrefs.GetFloat(Moshi_VfxBrowserDefine.PREF_CAM_DIST_MUL, 1f);
            _ShowHelpTips = EditorPrefs.GetBool(Moshi_VfxBrowserDefine.PREF_SHOW_HELP_TIPS, true);
            _ShowName = EditorPrefs.GetBool(Moshi_VfxBrowserDefine.PREF_SHOW_NAME, false);
            Moshi_VfxBrowserContext.HighFrameRateMode = EditorPrefs.GetBool(Moshi_VfxBrowserDefine.PREF_HIGH_FPS, true);
            _CurrentPageID = EditorPrefs.GetInt(Moshi_VfxBrowserDefine.PREF_LAST_PAGE_ID, -1);
            _SearchText = EditorPrefs.GetString(Moshi_VfxBrowserDefine.PREF_SEARCH_TEXT, "");

            _PageScrollPos.Clear();
            string scrollKeys = EditorPrefs.GetString(Moshi_VfxBrowserDefine.PREF_SCROLL_KEYS, "");
            if (!string.IsNullOrEmpty(scrollKeys))
            {
                foreach (var part in scrollKeys.Split(','))
                {
                    if (int.TryParse(part, out int pageID))
                    {
                        float x = EditorPrefs.GetFloat(Moshi_VfxBrowserDefine.PREF_SCROLL_X_PREFIX + pageID, 0f);
                        float y = EditorPrefs.GetFloat(Moshi_VfxBrowserDefine.PREF_SCROLL_Y_PREFIX + pageID, 0f);
                        _PageScrollPos[pageID] = new Vector2(x, y);
                    }
                }
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制可点击的标签
        /// Draw clickable label
        /// </summary>
        private void DrawClickableLabel(string text, string tooltip, float width, System.Action onClick)
        {
            bool splitRow = Moshi_VfxBrowserSetting.Instance.TitleSplitRow;

            float contentOffsetFix = splitRow ? 1f : 2f;
            float singleLineHeightFix = splitRow ? 2f : 4f;
            float labelRectHeightFix = splitRow ? 0.5f : 2f;

            var centeredStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
            centeredStyle.contentOffset = new Vector2(0, contentOffsetFix);
            GUILayout.Label(new GUIContent(text, tooltip), centeredStyle, GUILayout.Width(width));
            Rect labelRect = GUILayoutUtility.GetLastRect();

            float toolbarHeight = EditorGUIUtility.singleLineHeight + singleLineHeightFix;
            float yCenter = labelRect.y + labelRect.height * 0.5f + labelRectHeightFix;
            Rect hitRect = new Rect(labelRect.x, yCenter - toolbarHeight * 0.5f, width, toolbarHeight);

            EditorGUIUtility.AddCursorRect(hitRect, MouseCursor.Link);

            bool isHovering = hitRect.Contains(Event.current.mousePosition);
            if (isHovering)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    var highlightColor = EditorGUIUtility.isProSkin
                        ? new Color(1f, 1f, 1f, 0.08f)
                        : new Color(0f, 0f, 0f, 0.08f);
                    EditorGUI.DrawRect(hitRect, highlightColor);
                }
                Repaint();
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hitRect.Contains(Event.current.mousePosition))
            {
                Undo.RecordObject(this, "Reset " + text);
                onClick?.Invoke();
                Event.current.Use();
                ShowNotification(new GUIContent($"已恢复 {text} 为默认值"));
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制工具栏分割线
        /// Draw toolbar separator
        /// </summary>
        private void DrawToolbarSeparator()
        {
            GUILayout.Space(1);
            GUILayout.Label(GUIContent.none, EditorStyles.toolbarButton, GUILayout.Width(0.5f), GUILayout.Height(16));
            GUILayout.Space(1);
        }
    }
}
#endif
