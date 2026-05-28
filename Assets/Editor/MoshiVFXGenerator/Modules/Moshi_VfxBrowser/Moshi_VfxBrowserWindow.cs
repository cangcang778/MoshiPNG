#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static MoshiVFXGenerator.VfxBrowser.Moshi_VfxBrowserDefine;

namespace MoshiVFXGenerator.VfxBrowser
{
    /// <summary>
    /// 特效浏览器主窗口（基于 PreviewRenderUtility 实现 3D 实时预览，URP 兼容 + UGUI 支持）
    /// VFX Browser main window (real-time 3D preview with URP + UGUI support)
    /// </summary>
    public partial class Moshi_VfxBrowserWindow : EditorWindow
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 常量 / Constants
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private const float CELL_PADDING = 4f;
        private const float DEFAULT_MOVE_SPEED = 5f;
        private const float SCROLLBAR_WIDTH = 14f;

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 设置项 / Settings
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private int _GridCount = 16;
        private float _BgGray = 0.3f;
        private float _CamDistMul = 1.0f;
        private float _Fps = 60f;
        private float _LoopInterval = 1.0f;
        private float _MoveSpeed = 5f;
        private float _Speed = 1f;
        private bool _MoveEnabled = true;
        private bool _MovePausedByCtrl = false;
        private bool _IsLeftMouseHeld = false;
        private bool _IsRightMouseHeld = false;

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 搜索（E1） / Search
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private string _SearchText = "";
        private List<GameObject> _UnfilteredPrefabCache = new List<GameObject>(); // 未过滤的完整列表 / Unfiltered full list

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 计时 / Timing
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private double _LastFrameTime;
        private float _DeltaTime;

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // UI 状态 / UI State
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private int _SelectedIdx = -1;
        private float _LastScrollY = -1f;
        private bool _ShowDebug = false;
        private bool _ShowName = false;
        private bool _ShowHelpTips = true;
        private GUIStyle _BoxStyle;
        private GUIStyle _PageBtnStyle;
        private GUIStyle _StatusLeftStyle;
        private GUIStyle _StatusRightStyle;
        private GUIStyle _HelpTipsStyle;
        private GUIStyle _NameLabelStyle;
        private GUIStyle _PlaceholderStyle;
        private static Dictionary<int, Vector2> _PageScrollPos = new Dictionary<int, Vector2>();

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 拖拽状态 / Drag State
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private int _DragTargetIdx = -1;
        private Vector2 _DragStart;
        private bool _HasDragged = false;
        private bool _HasPanned = false;
        private bool _IsDragging = false;
        private bool _IsPanning = false;

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 分页状态 / Page State
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private int _CurrentPageID = -1;
        private int _LastPageID = -2;
        private bool _ShowFolderButtons = true;
        private List<GameObject> _FolderPrefabCache = new List<GameObject>();

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 运行时缓存 / Runtime Cache
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private int _CachedContextCount;
        private int _CachedTotalCount;
        private int _CachedTotalRows;
        private int _CachedVisibleCount;
        private HashSet<GameObject> _VisibleSet = new HashSet<GameObject>();
        private List<GameObject> _RemoveBuffer = new List<GameObject>();
        private Dictionary<GameObject, Moshi_VfxBrowserContext> _Contexts = new Dictionary<GameObject, Moshi_VfxBrowserContext>();

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 内部状态 / Internal State
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private bool _IsCleanedUp = false;
        private bool _NeedFocusDummy = false;

        /************************************************************************************************************************/

        private void OnEnable()
        {
            _LastFrameTime = EditorApplication.timeSinceStartup;
            _DeltaTime = 0f;
            _IsCleanedUp = false;

            LoadToolbarSettings();
            ImportPresetRootFolders();

            EditorApplication.update += OnEditorUpdate;
            EditorApplication.quitting += OnEditorQuitting;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private void OnDisable() => PerformCleanup();
        private void OnDestroy() => PerformCleanup();
        private void OnEditorQuitting() => PerformCleanup();
        private void OnBeforeAssemblyReload() => PerformCleanup();

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            _CurrentPageID = -1;
            _LastPageID = -2;
            _FolderPrefabCache.Clear();
            _UnfilteredPrefabCache.Clear();
            _SelectedIdx = -1;
            Moshi_VfxBrowserGrouper.ClearCache();
            CleanupAll();
        }

        private void PerformCleanup()
        {
            if (_IsCleanedUp) return;
            _IsCleanedUp = true;

            SaveToolbarSettings();

            _CurrentPageID = -1;
            _LastPageID = -2;
            _SelectedIdx = -1;

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;

            Moshi_VfxBrowserContext.ClearCachedWindow();

            CleanupAll();

            _FolderPrefabCache.Clear();
            _UnfilteredPrefabCache.Clear();
            Moshi_VfxBrowserGrouper.ClearCache();
            Moshi_VfxBrowserMasker.CleanupCornerMasks();

            EditorUtility.UnloadUnusedAssetsImmediate();
            System.GC.Collect();
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 导入配置中的预设根目录（E7，自动合并到 Folder.SimpleFolderPages）
        /// Import preset root folders from config (E7, auto-merge to Folder.SimpleFolderPages)
        /// </summary>
        private void ImportPresetRootFolders()
        {
            var setting = Moshi_VfxBrowserSetting.Instance;
            var folder = Moshi_VfxBrowserFolder.Instance;
            if (setting == null || folder == null) return;
            if (setting.PresetRootFolders == null || setting.PresetRootFolders.Length == 0) return;

            bool changed = false;
            foreach (var preset in setting.PresetRootFolders)
            {
                if (preset == null) continue;
                string path = AssetDatabase.GetAssetPath(preset);
                if (!AssetDatabase.IsValidFolder(path)) continue;
                if (folder.SimpleFolderPages.Contains(preset)) continue;

                folder.SimpleFolderPages.Add(preset);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(folder);
                AssetDatabase.SaveAssets();
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private void OnEditorUpdate()
        {
            double currentTime = EditorApplication.timeSinceStartup;
            double timePerFrame = 1.0 / _Fps;

            if (currentTime - _LastFrameTime >= timePerFrame)
            {
                _DeltaTime = (float)(currentTime - _LastFrameTime);
                _DeltaTime = Mathf.Clamp(_DeltaTime, 0f, 0.1f);
                _LastFrameTime = currentTime;
                Repaint();
            }
        }

        private void OnFocus()
        {
            _NeedFocusDummy = true;
        }

        /************************************************************************************************************************/

        private void OnGUI()
        {
            if (_BoxStyle == null)
            {
                _BoxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(0, 0, 0, 0) };
            }

            HandleGlobalEvents();
            DrawToolbar();
            DrawFolderButtons();
            DrawGridArea();
            DrawHelpTipsBar();
            DrawStatusBar();
            HandleFolderDragDrop();
        }

        /************************************************************************************************************************/
        #region 绘制方法 / Drawing Methods
        /************************************************************************************************************************/

        /// <summary>
        /// 绘制文件夹分页按钮区域
        /// Draw folder page buttons area
        /// </summary>
        private void DrawFolderButtons()
        {
            if (!_ShowFolderButtons) return;
            var folder = Moshi_VfxBrowserFolder.Instance;
            var setting = Moshi_VfxBrowserSetting.Instance;
            List<string> pageNames = folder.GetPageNames();

            if (pageNames.Count <= 0) return;

            if (_PageBtnStyle == null)
            {
                _PageBtnStyle = new GUIStyle(GUI.skin.button);
            }
            _PageBtnStyle.wordWrap = false;
            _PageBtnStyle.clipping = TextClipping.Clip;
            _PageBtnStyle.fontSize = setting.PageButtonTextSize;
            _PageBtnStyle.alignment = TextAnchor.MiddleCenter;

            EditorGUILayout.BeginVertical();

            float availableWidth = position.width - 30;
            if (availableWidth < 1f) availableWidth = 1f;

            int rowCount = Mathf.Max(1, setting.PageButtonRowCount);
            float buttonWidth = availableWidth / rowCount;
            float buttonHeight = setting.PageButtonHeight;
            if (buttonWidth < 1f) buttonWidth = 1f;

            int newSelection = _CurrentPageID;
            int totalButtons = pageNames.Count;
            if (_CurrentPageID >= totalButtons) { _CurrentPageID = -1; newSelection = -1; }
            int rows = Mathf.CeilToInt((float)totalButtons / rowCount);

            for (int r = 0; r < rows; r++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < rowCount; c++)
                {
                    int index = r * rowCount + c;
                    if (index >= totalButtons)
                    {
                        GUILayout.Space(buttonWidth);
                        continue;
                    }

                    Rect btnRect = GUILayoutUtility.GetRect(new GUIContent(pageNames[index]), _PageBtnStyle, GUILayout.Height(buttonHeight), GUILayout.Width(buttonWidth));

                    if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && btnRect.Contains(Event.current.mousePosition))
                    {
                        Event.current.Use();
                    }

                    if (Event.current.type == EventType.ContextClick && btnRect.Contains(Event.current.mousePosition))
                    {
                        // 收藏夹页不允许删除
                        // Favorites page cannot be deleted
                        if (!folder.IsFavoritesPage(index))
                        {
                            int capturedIndex = index;
                            var menu = new GenericMenu();
                            menu.AddItem(new GUIContent("删除此分页"), false, () =>
                            {
                                Moshi_VfxBrowserFolder.Instance.RemovePage(capturedIndex);
                                _CurrentPageID = -1;
                                _LastPageID = -2;
                                _FolderPrefabCache.Clear();
                                _UnfilteredPrefabCache.Clear();
                                _SelectedIdx = -1;
                                Moshi_VfxBrowserGrouper.ClearCache();
                                CleanupAll();
                                Repaint();
                            });
                            menu.ShowAsContext();
                            Event.current.Use();
                        }
                    }

                    if (GUI.Button(btnRect, pageNames[index], _PageBtnStyle))
                    {
                        newSelection = (_CurrentPageID == index) ? -1 : index;
                    }

                    if (_CurrentPageID == index)
                    {
                        Moshi_VfxBrowserMasker.DrawInnerBorder(btnRect, setting.PageButtonSelectedColor, 2f);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            if (_CurrentPageID != newSelection)
            {
                _CurrentPageID = newSelection;
                _LastPageID = -2;
                CleanupAll();
                _SelectedIdx = -1;
                Moshi_VfxBrowserGrouper.ClearCache();
                if (_CurrentPageID < 0)
                {
                    _FolderPrefabCache.Clear();
                    _UnfilteredPrefabCache.Clear();
                }
            }

            if (_CurrentPageID >= 0 && _LastPageID != _CurrentPageID)
            {
                _LastPageID = _CurrentPageID;
                _UnfilteredPrefabCache = folder.GetPrefabs(_CurrentPageID);
                ApplySearchFilter();
            }
        }

        /// <summary>
        /// 应用搜索过滤（E1）
        /// Apply search filter (E1)
        /// </summary>
        private void ApplySearchFilter()
        {
            // 把未过滤列表拷贝到 _FolderPrefabCache
            // Copy unfiltered list to _FolderPrefabCache
            _FolderPrefabCache.Clear();

            if (string.IsNullOrWhiteSpace(_SearchText))
            {
                _FolderPrefabCache.AddRange(_UnfilteredPrefabCache);
            }
            else
            {
                // 不区分大小写，支持多关键词（空格分隔，AND）
                // Case-insensitive, multi-keyword support (space-separated, AND logic)
                var keywords = _SearchText.Trim().ToLowerInvariant().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                foreach (var prefab in _UnfilteredPrefabCache)
                {
                    if (prefab == null) continue;
                    string name = prefab.name.ToLowerInvariant();
                    bool match = true;
                    foreach (var kw in keywords)
                    {
                        if (!name.Contains(kw)) { match = false; break; }
                    }
                    if (match) _FolderPrefabCache.Add(prefab);
                }
            }

            Moshi_VfxBrowserGrouper.BuildGroupedCache(_FolderPrefabCache, Moshi_VfxBrowserSetting.Instance.UseSubdirectory);
            CleanupAll();
            _SelectedIdx = -1;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制网格预览区域
        /// Draw grid preview area
        /// </summary>
        private void DrawGridArea()
        {
            if (_FolderPrefabCache == null || _FolderPrefabCache.Count == 0)
            {
                // 有选中分页但结果为空（搜索未命中）
                // Page selected but results empty (search miss)
                if (_CurrentPageID >= 0 && !string.IsNullOrEmpty(_SearchText))
                {
                    EditorGUILayout.HelpBox($"未找到匹配 \"{_SearchText}\" 的特效", MessageType.Info);
                }
                return;
            }

            float availableWidth = position.width - SCROLLBAR_WIDTH;
            float scrollViewHeight = Mathf.Max(100f, position.height - 120f);

            int targetCount = Mathf.Max(1, _GridCount);
            float aspectRatio = availableWidth / scrollViewHeight;

            int columns = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(targetCount * aspectRatio)));
            columns = Mathf.Min(columns, targetCount);

            float actualCellSize = availableWidth / columns;
            actualCellSize = Mathf.Max(actualCellSize, 50f);

            float dt = 0f;
            float rawDt = 0f;
            if (Event.current.type == EventType.Repaint)
            {
                float ratio = 1f;
                dt = _DeltaTime * _Speed * ratio;
                rawDt = _DeltaTime;
                _DeltaTime = 0f;
            }

            var groupedCache = Moshi_VfxBrowserGrouper.GroupedCache;
            bool isGroupedMode = Moshi_VfxBrowserSetting.Instance.UseSubdirectory && groupedCache != null && groupedCache.Count > 0;

            if (isGroupedMode)
            {
                DrawGridAreaGrouped(availableWidth, scrollViewHeight, actualCellSize, columns, dt, rawDt, groupedCache);
            }
            else
            {
                DrawGridAreaFlat(availableWidth, scrollViewHeight, actualCellSize, columns, dt, rawDt);
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private void DrawGridAreaFlat(float availableWidth, float scrollViewHeight, float actualCellSize, int columns, float dt, float rawDt)
        {
            int totalCount = _FolderPrefabCache.Count;
            int rows = Mathf.CeilToInt((float)totalCount / columns);
            float totalHeight = rows * actualCellSize;

            int pageKey = Mathf.Max(0, _CurrentPageID);
            _PageScrollPos.TryGetValue(pageKey, out var scrollPos);
            scrollPos = GUILayout.BeginScrollView(scrollPos, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
            _PageScrollPos[pageKey] = scrollPos;
            {
                GUILayoutUtility.GetRect(availableWidth, totalHeight);

                int firstTrueVisibleRow = Mathf.Max(0, Mathf.FloorToInt(scrollPos.y / actualCellSize));
                int lastTrueVisibleRow = Mathf.Min(rows - 1, Mathf.CeilToInt((scrollPos.y + scrollViewHeight) / actualCellSize));

                int firstVisibleRow = Mathf.Max(0, firstTrueVisibleRow - 1);
                int lastVisibleRow = Mathf.Min(rows - 1, lastTrueVisibleRow + 1);

                _VisibleSet.Clear();
                int trueVisibleCount = 0;

                for (int row = firstVisibleRow; row <= lastVisibleRow; row++)
                {
                    bool isTrueVisible = (row >= firstTrueVisibleRow && row <= lastTrueVisibleRow);

                    for (int col = 0; col < columns; col++)
                    {
                        int index = row * columns + col;
                        if (index >= totalCount) break;

                        GameObject prefab = _FolderPrefabCache[index];
                        if (prefab == null) continue;

                        _VisibleSet.Add(prefab);
                        if (isTrueVisible) trueVisibleCount++;

                        Rect cellRect = new Rect(
                            col * actualCellSize + CELL_PADDING / 2,
                            row * actualCellSize + CELL_PADDING / 2,
                            actualCellSize - CELL_PADDING,
                            actualCellSize - CELL_PADDING
                        );

                        DrawSingleCell(cellRect, prefab, dt, rawDt, _LoopInterval, index, isTrueVisible);
                    }
                }

                CleanupInvisibleContexts();

                _CachedTotalCount = totalCount;
                _CachedVisibleCount = trueVisibleCount;
                _CachedContextCount = _Contexts.Count;
                _CachedTotalRows = rows;

                if (_ShowDebug)
                {
                    string debugInfo = $"Scroll: {scrollPos.y:F0} | Rows: {firstTrueVisibleRow}-{lastTrueVisibleRow}/{rows}";
                    GUI.Label(new Rect(10, scrollPos.y + 5, 400, 20), debugInfo, EditorStyles.helpBox);
                }
            }
            GUILayout.EndScrollView();
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private void DrawGridAreaGrouped(float availableWidth, float scrollViewHeight, float actualCellSize, int columns, float dt, float rawDt, List<Moshi_SubfolderGroup> groupedCache)
        {
            const float HEADER_HEIGHT = 22f;
            const float HEADER_SPACE = 4f;
            const float HEADER_PADDING = 4f;
            float sectionHeight = HEADER_HEIGHT + HEADER_SPACE;

            int totalCount = _FolderPrefabCache.Count;
            int segCount = groupedCache.Count;

            float totalHeight = 0f;
            float[] segYOffsets = new float[segCount];
            for (int seg = 0; seg < segCount; seg++)
            {
                segYOffsets[seg] = totalHeight;
                totalHeight += sectionHeight;
                if (!Moshi_VfxBrowserGrouper.CollapsedSubfolders.Contains(groupedCache[seg].FolderPath))
                {
                    int segRows = Mathf.CeilToInt((float)groupedCache[seg].Count / columns);
                    totalHeight += segRows * actualCellSize;
                }
            }

            int pageKey = Mathf.Max(0, _CurrentPageID);
            _PageScrollPos.TryGetValue(pageKey, out var scrollPos);
            scrollPos = GUILayout.BeginScrollView(scrollPos, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
            _PageScrollPos[pageKey] = scrollPos;
            {
                GUILayoutUtility.GetRect(availableWidth, totalHeight);

                float viewTop = scrollPos.y;
                float viewBottom = scrollPos.y + scrollViewHeight;

                _VisibleSet.Clear();
                int trueVisibleCount = 0;
                bool needRepaint = false;

                for (int seg = 0; seg < segCount; seg++)
                {
                    var group = groupedCache[seg];
                    float segY = segYOffsets[seg];
                    bool isCollapsed = Moshi_VfxBrowserGrouper.CollapsedSubfolders.Contains(group.FolderPath);

                    float segHeight = sectionHeight;
                    if (!isCollapsed)
                    {
                        int segRowsCalc = Mathf.CeilToInt((float)group.Count / columns);
                        segHeight += segRowsCalc * actualCellSize;
                    }

                    if (segY + segHeight < viewTop - actualCellSize * 2 || segY > viewBottom + actualCellSize * 2)
                        continue;

                    Rect headerRect = new Rect(HEADER_PADDING, segY, availableWidth - HEADER_PADDING * 2, HEADER_HEIGHT);
                    if (Moshi_VfxBrowserGrouper.DrawSubfolderSectionHeaderAbsolute(headerRect, group.DisplayName, group.FolderPath))
                    {
                        needRepaint = true;
                    }

                    if (isCollapsed) continue;

                    float contentStartY = segY + sectionHeight;
                    int segStart = group.StartIndex;
                    int segItemCount = group.Count;
                    int segRows = Mathf.CeilToInt((float)segItemCount / columns);

                    int firstRow = Mathf.Max(0, Mathf.FloorToInt((viewTop - contentStartY - actualCellSize) / actualCellSize));
                    int lastRow = Mathf.Min(segRows - 1, Mathf.CeilToInt((viewBottom - contentStartY + actualCellSize) / actualCellSize));

                    for (int r = firstRow; r <= lastRow; r++)
                    {
                        float rowY = contentStartY + r * actualCellSize;
                        bool isTrueVisible = (rowY + actualCellSize >= viewTop) && (rowY <= viewBottom);

                        for (int col = 0; col < columns; col++)
                        {
                            int localIdx = r * columns + col;
                            if (localIdx >= segItemCount) break;
                            int globalIdx = segStart + localIdx;
                            if (globalIdx >= totalCount) break;

                            GameObject prefab = _FolderPrefabCache[globalIdx];
                            if (prefab == null) continue;

                            _VisibleSet.Add(prefab);
                            if (isTrueVisible) trueVisibleCount++;

                            Rect cellRect = new Rect(
                                col * actualCellSize + CELL_PADDING / 2,
                                rowY + CELL_PADDING / 2,
                                actualCellSize - CELL_PADDING,
                                actualCellSize - CELL_PADDING
                            );

                            DrawSingleCell(cellRect, prefab, dt, rawDt, _LoopInterval, globalIdx, isTrueVisible);
                        }
                    }
                }

                CleanupInvisibleContexts();

                _CachedTotalCount = totalCount;
                _CachedVisibleCount = trueVisibleCount;
                _CachedContextCount = _Contexts.Count;
                _CachedTotalRows = 0;

                if (needRepaint)
                {
                    Repaint();
                }
            }
            GUILayout.EndScrollView();
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private void CleanupInvisibleContexts()
        {
            _RemoveBuffer.Clear();
            foreach (var kvp in _Contexts)
            {
                if (!_VisibleSet.Contains(kvp.Key))
                {
                    kvp.Value.Cleanup();
                    _RemoveBuffer.Add(kvp.Key);
                }
            }
            foreach (var key in _RemoveBuffer)
            {
                _Contexts.Remove(key);
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制单个预览格子
        /// Draw single preview cell
        /// </summary>
        private void DrawSingleCell(Rect rect, GameObject prefab, float deltaTime, float rawDeltaTime, float loopInterval, int index, bool shouldSimulate)
        {
            if (!_Contexts.TryGetValue(prefab, out Moshi_VfxBrowserContext context))
            {
                context = new Moshi_VfxBrowserContext(prefab);
                _Contexts[prefab] = context;

                context.SetMoveSpeed(_MoveEnabled ? Moshi_VfxBrowserSetting.Instance.TailMoveSpeed : 0f);
            }

            HandleCellEvents(rect, context, index);

            if (Event.current.type == EventType.Repaint)
            {
                bool isPaused = (index == _DragTargetIdx) && (_IsDragging || _IsPanning);
                if (shouldSimulate && !isPaused)
                {
                    context.StepSimulation(deltaTime, rawDeltaTime, loopInterval);
                }

                Texture tex = context.Render((int)rect.width, (int)rect.height);

                // 用预览背景灰填充格子，避免 GUI.Box 默认皮肤出现刺眼白底
                // Fill cell with preview gray to avoid GUI.Box default skin white background
                float bgValue = _BgGray * Moshi_VfxBrowserSetting.Instance.DefaultColor;
                EditorGUI.DrawRect(rect, new Color(bgValue, bgValue, bgValue, 1f));

                if (tex != null)
                {
                    DrawTextureOpaque(rect, tex);
                }
                else
                {
                    // 渲染目标未就绪的占位提示（空 Prefab / 首帧 / UGUI 未初始化等）
                    // Placeholder when render target not ready (empty prefab / first frame / UGUI not ready)
                    if (_PlaceholderStyle == null)
                    {
                        _PlaceholderStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontSize = 11,
                        };
                    }
                    GUI.Label(rect, "（无预览）", _PlaceholderStyle);
                }

                if (index == _SelectedIdx)
                {
                    Moshi_VfxBrowserMasker.DrawInnerBorder(rect, new Color(0.2f, 0.6f, 1f, 1f), 3f);
                }

                // 收藏星标（E4 视觉反馈）
                // Favorite star indicator (E4 visual feedback)
                if (Moshi_VfxBrowserFolder.Instance.IsFavorite(prefab))
                {
                    var oldColor = GUI.color;
                    GUI.color = new Color(1f, 0.85f, 0.1f, 1f);
                    GUI.Label(new Rect(rect.x + 4, rect.y + 2, 20, 20), "★", EditorStyles.boldLabel);
                    GUI.color = oldColor;
                }

                if (_ShowName)
                {
                    if (_NameLabelStyle == null)
                    {
                        _NameLabelStyle = new GUIStyle(EditorStyles.label)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontSize = 11,
                            normal = { textColor = Color.white },
                        };
                    }
                    float labelHeight = EditorGUIUtility.singleLineHeight;
                    Rect labelRect = new Rect(rect.x, rect.y + rect.height - labelHeight, rect.width, labelHeight);
                    EditorGUI.DrawRect(labelRect, new Color(0, 0, 0, 0.25f));
                    GUI.Label(labelRect, prefab.name, _NameLabelStyle);
                }

                if (_IsDragging && index == _DragTargetIdx)
                {
                    if (Moshi_VfxBrowserSetting.Instance.ShowRotateAxis)
                        Moshi_VfxBrowserAxis.DrawAxisGizmo(rect, context);
                }

                if (_ShowDebug)
                {
                    Rect iconRect = new Rect(rect.x + rect.width - 24, rect.y + 4, 20, 20);
                    Color oldColor = GUI.color;
                    GUI.color = tex != null ? Color.green : Color.red;
                    GUI.Label(iconRect, tex != null ? "●" : "○", EditorStyles.boldLabel);
                    GUI.color = oldColor;

                    Rect debugRect = new Rect(rect.x + 4, rect.y + 4, 80, 18);
                    GUI.Label(debugRect, $"Y:{rect.y:F0}", EditorStyles.miniLabel);
                }

                if (Moshi_VfxBrowserSetting.Instance.RoundedCorners)
                {
                    float cornerRadius = Mathf.Min(rect.width, rect.height) * Moshi_VfxBrowserSetting.Instance.CellCornerRatio;
                    Moshi_VfxBrowserMasker.DrawRoundedCornerMask(rect, cornerRadius);
                }
            }
        }

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/

        /************************************************************************************************************************/
        #region 辅助方法 / Helper Methods
        /************************************************************************************************************************/

        private void RefreshAllEffects()
        {
            int pageKey = Mathf.Max(0, _CurrentPageID);
            _PageScrollPos.TryGetValue(pageKey, out var scrollPos);

            if (Mathf.Abs(scrollPos.y - _LastScrollY) > 1f)
            {
                CleanupAll();
                _VisibleSet.Clear();
            }
            else
            {
                foreach (var ctx in _Contexts.Values)
                {
                    ctx.Restart();
                }
            }

            _LastScrollY = scrollPos.y;
            Repaint();
        }

        private void CleanupAll()
        {
            foreach (var kvp in _Contexts)
            {
                kvp.Value.Cleanup();
            }
            _Contexts.Clear();

            _VisibleSet.Clear();
            _RemoveBuffer.Clear();
        }

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/
    }
}
#endif
