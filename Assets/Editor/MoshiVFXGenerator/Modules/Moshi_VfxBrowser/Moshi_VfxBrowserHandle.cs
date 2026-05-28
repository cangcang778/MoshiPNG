#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator.VfxBrowser
{
    public partial class Moshi_VfxBrowserWindow : EditorWindow
    {
        /************************************************************************************************************************/
        #region 事件处理 / Event Handling
        /************************************************************************************************************************/

        /// <summary>
        /// 处理全局事件
        /// Handle global events
        /// </summary>
        private void HandleGlobalEvents()
        {
            Event e = Event.current;

            // 左键状态
            // Left button state
            if (e.type == EventType.MouseDown && e.button == 0) _IsLeftMouseHeld = true;
            else if (e.type == EventType.MouseDrag && e.button == 0) _IsLeftMouseHeld = true;
            else if ((e.type == EventType.MouseUp || e.rawType == EventType.MouseUp) && e.button == 0) _IsLeftMouseHeld = false;
            else if (e.type == EventType.DragExited || e.type == EventType.DragPerform) _IsLeftMouseHeld = false;
            else if (e.type == EventType.MouseLeaveWindow) _IsLeftMouseHeld = false;

            // 右键状态
            // Right button state
            if (e.type == EventType.MouseDown && e.button == 1) _IsRightMouseHeld = true;
            else if (e.type == EventType.MouseDrag && e.button == 1) _IsRightMouseHeld = true;
            else if ((e.type == EventType.MouseUp || e.rawType == EventType.MouseUp) && e.button == 1) _IsRightMouseHeld = false;
            else if (e.type == EventType.MouseLeaveWindow) _IsRightMouseHeld = false;

            // 全局兜底：中/右键释放被其他控件消耗时强制重置拖拽状态
            // Global fallback: force reset drag/pan state if MouseUp is consumed by other controls
            if (e.rawType == EventType.MouseUp && e.type != EventType.MouseUp)
            {
                if (e.button == 2 && _IsPanning) { _IsPanning = false; _HasPanned = false; _DragTargetIdx = -1; }
                if (e.button == 1 && _IsDragging) { _IsDragging = false; _HasDragged = false; _DragTargetIdx = -1; }
            }
            if (e.type == EventType.MouseLeaveWindow)
            {
                if (_IsPanning) { _IsPanning = false; _HasPanned = false; _DragTargetIdx = -1; }
                if (_IsDragging) { _IsDragging = false; _HasDragged = false; _DragTargetIdx = -1; }
            }

            // 左键按下：取消 Unity 当前选中
            // Left-click: deselect Unity selection
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Selection.activeObject = null;
                _SelectedIdx = -1;
            }

            // 左键释放：刷新所有特效
            // Left-click release: refresh all effects
            if (e.type == EventType.MouseUp && e.button == 0 && !_IsDragging)
            {
                RefreshAllEffects();
                Repaint();
            }

            // Ctrl/右键 + 滚轮：全局调整镜头距离
            // Ctrl/Right + Scroll: adjust global camera distance
            if (e.type == EventType.ScrollWheel && (e.control || _IsRightMouseHeld))
            {
                _CamDistMul = Mathf.Clamp(_CamDistMul - e.delta.y * 0.05f, 0.1f, 3f);
                foreach (var ctx in _Contexts.Values)
                {
                    ctx.SetDistanceMultiplier(_CamDistMul);
                }
                Repaint();
                e.Use();
            }

            // Ctrl / 左键按住：临时暂停特效移动
            // Ctrl / Left held: temporarily pause effect movement
            bool ctrlPressed = e.control;
            bool leftMousePressed = _IsLeftMouseHeld;
            bool shouldPauseMove = ctrlPressed || leftMousePressed;
            if (shouldPauseMove != _MovePausedByCtrl)
            {
                _MovePausedByCtrl = shouldPauseMove;
                foreach (var ctx in _Contexts.Values)
                {
                    ctx.SetMovePaused(_MovePausedByCtrl);
                }
            }
        }

        /// <summary>
        /// 处理单个格子的鼠标事件
        /// Handle mouse events for a single cell
        /// </summary>
        private void HandleCellEvents(Rect rect, Moshi_VfxBrowserContext context, int index)
        {
            Event e = Event.current;

            // Shift + 滚轮：单格缩放（优先处理，避免被拖拽分支 return 吞掉）
            // Shift + Scroll: per-cell zoom (handle first to avoid being swallowed by drag branches)
            if (e.type == EventType.ScrollWheel && e.shift && rect.Contains(e.mousePosition))
            {
                context.AdjustDistanceMultiplier(-e.delta.y);
                Repaint();
                e.Use();
                return;
            }

            // 右键拖拽旋转（即使鼠标移出格子）
            // Right-button drag rotation (even when mouse leaves cell)
            if (_IsDragging && index == _DragTargetIdx)
            {
                if (e.type == EventType.MouseDrag && e.button == 1)
                {
                    Vector2 delta = e.mousePosition - _DragStart;
                    context.AdjustRotation(delta.x * 0.5f, delta.y * 0.5f);
                    _DragStart = e.mousePosition;
                    _HasDragged = true;
                    Repaint();
                    e.Use();
                }
                else if (e.type == EventType.MouseUp && e.button == 1)
                {
                    if (!_HasDragged)
                    {
                        if (index >= 0 && index < _FolderPrefabCache.Count && _FolderPrefabCache[index] != null)
                        {
                            Selection.activeObject = _FolderPrefabCache[index];
                            EditorGUIUtility.PingObject(_FolderPrefabCache[index]);
                        }
                    }
                    _IsDragging = false;
                    _HasDragged = false;
                    _DragTargetIdx = -1;
                    e.Use();
                }
                return;
            }

            // 中键拖拽平移
            // Middle-button drag pan
            if (_IsPanning && index == _DragTargetIdx)
            {
                if (e.type == EventType.MouseDrag && e.button == 2)
                {
                    Vector2 delta = e.mousePosition - _DragStart;
                    context.AdjustPan(-delta.x * 0.003f, delta.y * 0.003f);
                    _DragStart = e.mousePosition;
                    _HasPanned = true;
                    Repaint();
                    e.Use();
                }
                else if ((e.type == EventType.MouseUp || e.rawType == EventType.MouseUp) && e.button == 2)
                {
                    _IsPanning = false;
                    _HasPanned = false;
                    _DragTargetIdx = -1;
                    if (e.type != EventType.Used) e.Use();
                }
                return;
            }

            if (!rect.Contains(e.mousePosition)) return;

            // 左键拖拽：拖入场景
            // Left-click drag: drag into scene
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                GameObject prefab = _FolderPrefabCache[index];
                if (prefab != null)
                {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new Object[] { prefab };
                    DragAndDrop.StartDrag(prefab.name);
                    e.Use();
                }
            }
            // 右键按下：选中 + 开始拖拽旋转 + 右键菜单准备
            // Right-click: select + start drag rotation + context menu prep
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                _SelectedIdx = index;
                _DragTargetIdx = index;
                _IsDragging = true;
                _HasDragged = false;
                _DragStart = e.mousePosition;
                Repaint();
                e.Use();
            }
            // 中键按下：开始平移
            // Middle-click: start pan
            else if (e.type == EventType.MouseDown && e.button == 2)
            {
                _DragTargetIdx = index;
                _IsPanning = true;
                _HasPanned = false;
                _DragStart = e.mousePosition;
                Repaint();
                e.Use();
            }
            // 右键上下文菜单（未拖拽时）：收藏 / 在 TLVideo 打开（E4 E5）
            // Right-click context menu (when not dragged): Favorite / Open in TLVideo (E4 E5)
            else if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
            {
                GameObject prefab = (index >= 0 && index < _FolderPrefabCache.Count) ? _FolderPrefabCache[index] : null;
                if (prefab != null)
                {
                    ShowCellContextMenu(prefab);
                    e.Use();
                }
            }
        }

        /// <summary>
        /// 显示格子右键菜单（E4 收藏 + E5 TLVideo 联动）
        /// Show cell context menu (E4 Favorite + E5 TLVideo integration)
        /// </summary>
        private void ShowCellContextMenu(GameObject prefab)
        {
            var menu = new GenericMenu();
            var folder = Moshi_VfxBrowserFolder.Instance;

            bool isFav = folder.IsFavorite(prefab);
            menu.AddItem(new GUIContent(isFav ? "从收藏中移除" : "添加到收藏"), false, () =>
            {
                folder.ToggleFavorite(prefab);
                // 如果当前正在看收藏夹页，强制刷新
                // If currently viewing Favorites page, force refresh
                if (folder.IsFavoritesPage(_CurrentPageID))
                {
                    _LastPageID = -2;
                }
                Repaint();
            });

            menu.AddSeparator("");

            // 重置单格缩放：把当前格子的缩放还原回全局 _CamDistMul
            // Reset per-cell zoom: restore cell zoom back to global _CamDistMul
            menu.AddItem(new GUIContent("重置此格缩放"), false, () =>
            {
                if (_Contexts != null && _Contexts.TryGetValue(prefab, out var ctx) && ctx != null)
                {
                    ctx.SetDistanceMultiplier(_CamDistMul);
                    Repaint();
                }
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("在时间线视频中打开"), false, () =>
            {
                TryOpenInTlVideo(prefab);
            });

            menu.AddItem(new GUIContent("在 Project 中定位"), false, () =>
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            });

            menu.ShowAsContext();
        }

        /// <summary>
        /// 尝试在 Moshi_TLVideo 中打开该 Prefab（E5，通过反射避免强依赖）
        /// Try to open prefab in Moshi_TLVideo (E5, via reflection to avoid hard dependency)
        /// </summary>
        private void TryOpenInTlVideo(GameObject prefab)
        {
            if (prefab == null) return;

            // 反射查找 Moshi_TLVideo 类（可能位于全局命名空间或 MoshiTools 命名空间）
            // Reflect Moshi_TLVideo type (may be in global or MoshiTools namespace)
            System.Type tlVideoType = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                tlVideoType = asm.GetType("Moshi_TLVideo")
                           ?? asm.GetType("MoshiTools.Moshi_TLVideo");
                if (tlVideoType != null) break;
            }

            if (tlVideoType == null)
            {
                EditorUtility.DisplayDialog("时间线视频工具未安装",
                    "未找到 Moshi_TLVideo 工具，请确保该工具已正确安装。",
                    "确定");
                return;
            }

            // 先打开 TLVideo 窗口
            // Open TLVideo window first
            var showMethod = tlVideoType.GetMethod("ShowWindow",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            showMethod?.Invoke(null, null);

            // 查找 TimelineAsset
            // Find TimelineAsset
            var timelineAsset = FindTimelineInPrefab(prefab);
            if (timelineAsset == null)
            {
                EditorUtility.DisplayDialog("未找到时间线",
                    $"Prefab \"{prefab.name}\" 中未找到 TimelineAsset，已打开 TLVideo 窗口。",
                    "确定");
                return;
            }

            // 尝试调用 LoadTimeline(Object) 方法（若 TLVideo 工具已实现）；
            // 否则回退到在 Project 面板中 Ping 该 TimelineAsset
            // Try LoadTimeline(Object) method if TLVideo implements it;
            // otherwise fallback to pinging the TimelineAsset in Project panel
            var loadMethod = tlVideoType.GetMethod("LoadTimeline",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool invoked = false;
            if (loadMethod != null)
            {
                try
                {
                    if (loadMethod.IsStatic)
                    {
                        loadMethod.Invoke(null, new object[] { timelineAsset });
                        invoked = true;
                    }
                    else
                    {
                        var window = EditorWindow.GetWindow(tlVideoType);
                        if (window != null)
                        {
                            loadMethod.Invoke(window, new object[] { timelineAsset });
                            invoked = true;
                        }
                    }
                }
                catch { /* 参数不匹配等，回退 / Fallback on mismatch */ }
            }

            if (!invoked)
            {
                // 回退：在 Project 面板中 Ping TimelineAsset
                // Fallback: ping TimelineAsset in Project panel
                Selection.activeObject = timelineAsset;
                EditorGUIUtility.PingObject(timelineAsset);
            }
        }

        /// <summary>
        /// 从 Prefab 中查找关联的 TimelineAsset
        /// Find associated TimelineAsset from prefab
        /// </summary>
        private static Object FindTimelineInPrefab(GameObject prefab)
        {
            if (prefab == null) return null;

            // 先查找 PlayableDirector 组件引用的 Timeline
            // First find Timeline referenced by PlayableDirector
            var director = prefab.GetComponentInChildren<UnityEngine.Playables.PlayableDirector>(true);
            if (director != null && director.playableAsset != null)
            {
                return director.playableAsset;
            }

            // 再查找 Prefab 所在目录下的 .playable 文件
            // Then find .playable file in prefab's directory
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                string dir = System.IO.Path.GetDirectoryName(prefabPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    // 同目录
                    // Same directory
                    var guids = AssetDatabase.FindAssets("t:TimelineAsset", new[] { dir.Replace('\\', '/') });
                    if (guids.Length > 0)
                    {
                        string tlPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                        return AssetDatabase.LoadAssetAtPath<Object>(tlPath);
                    }
                    // 搜 Timeline 子目录
                    // Search Timeline subdirectory
                    string tlDir = System.IO.Path.Combine(dir, "Timeline").Replace('\\', '/');
                    if (AssetDatabase.IsValidFolder(tlDir))
                    {
                        guids = AssetDatabase.FindAssets("t:TimelineAsset", new[] { tlDir });
                        if (guids.Length > 0)
                        {
                            string tlPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                            return AssetDatabase.LoadAssetAtPath<Object>(tlPath);
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 处理文件夹拖放到窗口
        /// Handle folder drag-drop onto window
        /// </summary>
        private void HandleFolderDragDrop()
        {
            Event current = Event.current;
            if (current == null) return;
            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform) return;

            bool hasFolder = false;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (AssetDatabase.IsValidFolder(path))
                {
                    hasFolder = true;
                    break;
                }
            }

            if (!hasFolder) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                var folder = Moshi_VfxBrowserFolder.Instance;
                bool changed = false;

                foreach (var obj in DragAndDrop.objectReferences)
                {
                    string path = AssetDatabase.GetAssetPath(obj);
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        if (!folder.SimpleFolderPages.Contains(obj))
                        {
                            folder.SimpleFolderPages.Add(obj);
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(folder);
                    _LastPageID = -2;
                }
            }

            current.Use();
        }

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/
    }
}
#endif
