#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class Moshi_Favorites : EditorWindow
{
    private const string TOOL_NAME = "资源收藏夹";
    
    private Moshi_FavoritesConfig config;
    private Vector2 projectScrollPosition;
    private Vector2 hierarchyScrollPosition;
    private string searchKeyword = string.Empty;
    private UnityEngine.Object manualAsset;
    private string manualDisplayName = string.Empty;
    private string manualNote = string.Empty;
    private SearchField searchField;
    
    // Project 收藏编辑状态
    private int editingProjectIndex = -1;
    private string editingProjectName = string.Empty;
    private string editingProjectNote = string.Empty;
    private UnityEngine.Object editingProjectAsset;
    
    // Hierarchy 收藏编辑状态
    private int editingHierarchyIndex = -1;
    private string editingHierarchyName = string.Empty;
    private string editingHierarchyNote = string.Empty;

    private static readonly Color RowEvenColor = new Color(0.21f, 0.21f, 0.21f, 1f);
    private static readonly Color RowOddColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    private static readonly Color HeaderColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    private static GUIContent locateIcon;
    
    // 性能优化：关闭时保存
    private bool isDirty = false;
    
    // 双击检测
    private double lastClickTime = 0;
    private int lastClickIndex = -1;
    private const double DoubleClickTime = 0.3;
    
    // 性能优化：缓存
    private static GUIStyle cachedLabelStyle;
    private static GUIStyle cachedHeaderStyle;
    private Dictionary<UnityEngine.Object, Texture> iconCache = new Dictionary<UnityEngine.Object, Texture>();
    private Dictionary<UnityEngine.Object, string> pathCache = new Dictionary<UnityEngine.Object, string>();
    
    // 左右分栏宽度比例
    private float splitRatio = 0.5f;
    private bool isResizing = false;
    private const float MinPanelWidth = 200f;
    
    // 拖拽排序状态
    private int draggingProjectIndex = -1;
    private int draggingHierarchyIndex = -1;
    private int dropTargetProjectIndex = -1;
    private int dropTargetHierarchyIndex = -1;
    private static GUIContent dragHandleIcon;
    private const float RowHeight = 28f;
    private const float DragHandleWidth = 18f;
    
    // 点击/拖拽延迟判断状态
    private int pendingClickProjectIndex = -1;
    private int pendingClickHierarchyIndex = -1;
    private bool isDraggingOutAsset = false;
    
    // 幽灵行拖拽状态
    private string ghostRowName = string.Empty;
    private Texture ghostRowIcon = null;
    private bool isGhostRowProject = true; // true=Project, false=Hierarchy
    private Rect lastProjectScrollArea;
    private Rect lastHierarchyScrollArea;
    
    // 资源类型过滤
    private int projectTypeFilterIndex = 0;
    private static readonly string[] ProjectTypeFilterNames = new string[]
    {
        "全部类型",
        "AnimationClip",
        "AudioClip",
        "AudioMixer",
        "ComputeShader",
        "Font",
        "GUISkin",
        "Material",
        "Mesh",
        "Model",
        "PhysicMaterial",
        "Prefab",
        "Scene",
        "Script",
        "Shader",
        "Sprite",
        "Texture",
        "VideoClip",
        "Folder"
    };
    private static readonly System.Type[] ProjectTypeFilters = new System.Type[]
    {
        null, // 全部类型
        typeof(AnimationClip),
        typeof(AudioClip),
        typeof(UnityEngine.Audio.AudioMixer),
        typeof(ComputeShader),
        typeof(Font),
        typeof(GUISkin),
        typeof(Material),
        typeof(Mesh),
        typeof(GameObject), // Model - 特殊处理
        typeof(PhysicMaterial),
        typeof(GameObject), // Prefab - 特殊处理
        typeof(SceneAsset),
        typeof(MonoScript),
        typeof(Shader),
        typeof(Sprite),
        typeof(Texture),
        typeof(UnityEngine.Video.VideoClip),
        typeof(DefaultAsset) // Folder - 特殊处理
    };

    [MenuItem("工具/Moshi/" + TOOL_NAME, priority = 520)]
    public static void ShowWindow()
    {
        Moshi_Favorites window = GetWindow<Moshi_Favorites>(TOOL_NAME);
        window.minSize = new Vector2(600f, 400f);
        window.LoadConfig();
        window.Show();
    }

    /// <summary>
    /// 获取 GameObject 在 Hierarchy 中的完整路径
    /// </summary>
    private static string GetGameObjectPath(GameObject go)
    {
        if (go == null) return string.Empty;
        
        string path = go.name;
        Transform parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    /// <summary>
    /// 通过路径查找 GameObject
    /// </summary>
    private static GameObject FindGameObjectByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        
        string[] parts = path.Split('/');
        if (parts.Length == 0) return null;
        
        // 查找根对象
        GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        GameObject current = null;
        
        foreach (GameObject root in rootObjects)
        {
            if (root.name == parts[0])
            {
                current = root;
                break;
            }
        }
        
        if (current == null) return null;
        
        // 遍历子对象
        for (int i = 1; i < parts.Length; i++)
        {
            Transform child = current.transform.Find(parts[i]);
            if (child == null) return null;
            current = child.gameObject;
        }
        
        return current;
    }

    private void OnEnable()
    {
        LoadConfig();
        searchField ??= new SearchField();
        EnsureLocateIconLoaded();
        EnsureDragHandleIconLoaded();
    }

    private void OnDisable()
    {
        if (isDirty && config != null && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            try
            {
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception)
            {
                // 忽略编辑器状态不稳定时的保存错误
            }
            isDirty = false;
        }
    }

    private void LoadConfig()
    {
        config = Moshi_FavoritesConfig.LoadOrCreate();
        InvalidateCache();
    }
    
    private void InvalidateCache()
    {
        iconCache.Clear();
        pathCache.Clear();
    }

    private void OnGUI()
    {
        if (config == null)
        {
            EditorGUILayout.HelpBox("未能加载收藏配置，请重新创建。", MessageType.Error);
            if (GUILayout.Button("重新创建配置"))
            {
                LoadConfig();
            }
            return;
        }

        DrawToolbar();
        EditorGUILayout.Space(4);
        DrawSplitPanels();
        
        // 绘制幽灵行（在最顶层）
        DrawGhostRow();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("收藏夹", EditorStyles.miniLabel);
        GUILayout.Space(8f);
        searchKeyword = (searchField ??= new SearchField()).OnToolbarGUI(searchKeyword ?? string.Empty);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("清理失效", EditorStyles.toolbarButton))
        {
            RemoveAllMissingEntries();
        }
        if (GUILayout.Button("定位配置", EditorStyles.toolbarButton))
        {
            EditorGUIUtility.PingObject(config);
        }
        MoshiHelpButton.DrawHelpButton(TOOL_NAME);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSplitPanels()
    {
        // 使用纯 EditorGUILayout 布局，避免 BeginArea 的绝对坐标问题
        EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
        
        // 计算宽度
        float totalWidth = position.width - 10f;
        float splitterWidth = 6f;
        float leftWidth = (totalWidth - splitterWidth) * splitRatio;
        float rightWidth = totalWidth - leftWidth - splitterWidth;
        
        // 限制最小宽度
        if (leftWidth < MinPanelWidth)
        {
            leftWidth = MinPanelWidth;
            rightWidth = totalWidth - leftWidth - splitterWidth;
        }
        if (rightWidth < MinPanelWidth)
        {
            rightWidth = MinPanelWidth;
            leftWidth = totalWidth - rightWidth - splitterWidth;
        }
        
        // 左侧面板 - Project 收藏
        EditorGUILayout.BeginVertical(GUILayout.Width(leftWidth), GUILayout.ExpandHeight(true));
        DrawProjectPanel();
        EditorGUILayout.EndVertical();
        
        // 分隔线
        DrawSplitter();
        
        // 右侧面板 - Hierarchy 收藏
        EditorGUILayout.BeginVertical(GUILayout.Width(rightWidth), GUILayout.ExpandHeight(true));
        DrawHierarchyPanel();
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSplitter()
    {
        Rect splitterRect = GUILayoutUtility.GetRect(6f, 6f, GUILayout.Width(6f), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(splitterRect, new Color(0.1f, 0.1f, 0.1f, 1f));
        EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);
        HandleSplitterDrag(splitterRect);
    }

    private void HandleSplitterDrag(Rect splitterRect)
    {
        Event e = Event.current;
        
        if (e.type == EventType.MouseDown && splitterRect.Contains(e.mousePosition))
        {
            isResizing = true;
            e.Use();
        }
        
        if (isResizing)
        {
            if (e.type == EventType.MouseDrag)
            {
                float totalWidth = position.width - 10f;
                float newRatio = e.mousePosition.x / totalWidth;
                splitRatio = Mathf.Clamp(newRatio, 0.2f, 0.8f);
                Repaint();
                e.Use();
            }
            
            if (e.type == EventType.MouseUp)
            {
                isResizing = false;
                e.Use();
            }
        }
    }

    // ==================== Project 收藏面板 ====================
    
    private void DrawProjectPanel()
    {
        // 标题栏
        DrawProjectPanelHeader();
        
        // 收藏列表
        IReadOnlyList<FavoriteAssetEntry> entries = config.FavoriteEntries;
        if (entries == null || entries.Count == 0)
        {
            // 空面板时显示拖拽提示区域
            Rect emptyDropArea = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandHeight(true));
            HandleProjectPanelDragAndDrop(emptyDropArea);
            
            EditorGUI.HelpBox(new Rect(emptyDropArea.x + 10, emptyDropArea.y + 10, emptyDropArea.width - 20, 60),
                "暂无 Project 收藏\n从 Project 面板拖拽资源到此处，或选中资源后点击上方按钮添加", MessageType.Info);
            return;
        }

        // 先统计过滤后的条目数量
        int filteredCount = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && MatchesProjectFilter(entries[i]))
                filteredCount++;
        }

        // 获取滚动区域用于拖拽检测
        Rect scrollAreaRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandHeight(true));
        lastProjectScrollArea = scrollAreaRect; // 记录滚动区域位置
        
        // 处理拖拽
        HandleProjectPanelDragAndDrop(scrollAreaRect);
        
        // 绘制滚动列表（使用过滤后的数量计算高度）
        GUI.BeginGroup(scrollAreaRect);
        Rect innerRect = new Rect(0, 0, scrollAreaRect.width, scrollAreaRect.height);
        projectScrollPosition = GUI.BeginScrollView(innerRect, projectScrollPosition, 
            new Rect(0, 0, scrollAreaRect.width - 16, filteredCount * RowHeight));
        
        int displayIndex = 0; // 显示索引（连续的）
        for (int i = 0; i < entries.Count; i++)
        {
            FavoriteAssetEntry entry = entries[i];
            if (entry == null || !MatchesProjectFilter(entry)) continue;
            DrawProjectEntryInScrollView(entry, i, displayIndex, scrollAreaRect.width - 16);
            displayIndex++;
        }
        
        // 绘制拖拽到末尾的高亮线（使用过滤后的数量）
        if (draggingProjectIndex >= 0 && dropTargetProjectIndex == entries.Count)
        {
            float bottomY = filteredCount * RowHeight;
            Color lineColor = new Color(0.3f, 0.6f, 0.9f, 1f);
            float lineWidth = scrollAreaRect.width - 16;
            EditorGUI.DrawRect(new Rect(0, bottomY - 2f, lineWidth, 4f), lineColor);
            EditorGUI.DrawRect(new Rect(0, bottomY - 4f, 8f, 8f), lineColor);
            EditorGUI.DrawRect(new Rect(lineWidth - 8f, bottomY - 4f, 8f, 8f), lineColor);
        }
        
        GUI.EndScrollView();
        GUI.EndGroup();
    }

    private void HandleProjectPanelDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;
        
        if (!dropArea.Contains(evt.mousePosition)) return;
        
        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            // 检查是否有有效的 Project 资源
            bool hasValidAsset = false;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj != null && AssetDatabase.Contains(obj))
                {
                    hasValidAsset = true;
                    break;
                }
            }
            
            if (hasValidAsset)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    
                    int added = 0;
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj != null && AssetDatabase.Contains(obj))
                        {
                            if (TryAddProjectFavorite(obj, obj.name, string.Empty))
                                added++;
                        }
                    }
                    
                    if (added > 0)
                        ShowNotification(new GUIContent($"已添加 {added} 项到 Project 收藏"));
                }
                
                evt.Use();
            }
        }
        
        // 绘制拖拽提示
        if (evt.type == EventType.Repaint && DragAndDrop.visualMode == DragAndDropVisualMode.Copy && dropArea.Contains(evt.mousePosition))
        {
            EditorGUI.DrawRect(dropArea, new Color(0.3f, 0.6f, 0.9f, 0.15f));
            // 绘制边框
            Handles.BeginGUI();
            Handles.color = new Color(0.3f, 0.6f, 0.9f, 0.8f);
            Handles.DrawLine(new Vector3(dropArea.x, dropArea.y), new Vector3(dropArea.xMax, dropArea.y));
            Handles.DrawLine(new Vector3(dropArea.xMax, dropArea.y), new Vector3(dropArea.xMax, dropArea.yMax));
            Handles.DrawLine(new Vector3(dropArea.xMax, dropArea.yMax), new Vector3(dropArea.x, dropArea.yMax));
            Handles.DrawLine(new Vector3(dropArea.x, dropArea.yMax), new Vector3(dropArea.x, dropArea.y));
            Handles.EndGUI();
        }
    }

    private void DrawProjectEntryInScrollView(FavoriteAssetEntry entry, int dataIndex, int displayIndex, float width)
    {
        EnsureLocateIconLoaded();
        EnsureDragHandleIconLoaded();
        
        // 使用 displayIndex 计算行位置
        Rect rowRect = new Rect(0, displayIndex * RowHeight, width, RowHeight - 2f);
        Color rowColor = displayIndex % 2 == 0 ? RowEvenColor : RowOddColor;
        
        // 正在拖拽的行：半透明蓝色高亮（使用 dataIndex 比较）
        if (draggingProjectIndex == dataIndex)
        {
            EditorGUI.DrawRect(rowRect, new Color(0.3f, 0.6f, 0.9f, 0.4f));
        }
        else
        {
            EditorGUI.DrawRect(rowRect, rowColor);
        }
        
        // 拖拽目标高亮线（绘制在行背景之后，更明显）
        if (dropTargetProjectIndex == dataIndex && draggingProjectIndex >= 0 && draggingProjectIndex != dataIndex)
        {
            Color lineColor = new Color(0.3f, 0.6f, 0.9f, 1f);
            // 第一行时高亮线绘制在行顶部内侧（y=2），其他行绘制在行顶部外侧
            float lineY = displayIndex == 0 ? 2f : rowRect.y - 2f;
            float lineHeight = displayIndex == 0 ? 6f : 4f; // 第一行加粗
            EditorGUI.DrawRect(new Rect(rowRect.x, lineY, rowRect.width, lineHeight), lineColor);
            // 左端圆点
            float dotY = displayIndex == 0 ? 0f : lineY - 2f;
            EditorGUI.DrawRect(new Rect(rowRect.x, dotY, 10f, 10f), lineColor);
            // 右端圆点
            EditorGUI.DrawRect(new Rect(rowRect.xMax - 10f, dotY, 10f, 10f), lineColor);
        }

        // 拖拽手柄（左侧）
        Rect handleRect = new Rect(rowRect.x + 2f, rowRect.y + 4f, DragHandleWidth, rowRect.height - 8f);
        
        // 手柄背景反馈
        if (draggingProjectIndex == dataIndex)
        {
            // 正在拖拽：深蓝色背景
            EditorGUI.DrawRect(handleRect, new Color(0.2f, 0.5f, 0.8f, 0.8f));
        }
        else if (handleRect.Contains(Event.current.mousePosition))
        {
            // hover：浅灰色背景
            EditorGUI.DrawRect(handleRect, new Color(0.4f, 0.4f, 0.4f, 0.5f));
            Repaint(); // 确保 hover 状态实时更新
        }
        
        EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.Pan);
        GUI.Label(handleRect, dragHandleIcon);
        
        // 处理拖拽手柄的拖拽事件
        HandleProjectDragReorder(handleRect, dataIndex, rowRect);

        // 图标（向右偏移）
        Rect iconRect = new Rect(handleRect.xMax + 2f, rowRect.y + 4f, 18f, 18f);
        Texture icon = GetCachedIcon(entry);
        if (icon != null)
        {
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
        }

        // 名称
        Rect nameRect = new Rect(iconRect.xMax + 4f, rowRect.y, rowRect.width - 90f, rowRect.height);
        EnsureLabelStyleCached();
        GUI.Label(nameRect, new GUIContent(GetProjectEntryDisplayName(entry), GetCachedPath(entry?.assetReference)), cachedLabelStyle);

        // 菜单按钮
        Rect moreRect = new Rect(rowRect.xMax - 48f, rowRect.y + 4f, 20f, rowRect.height - 8f);
        if (GUI.Button(moreRect, EditorGUIUtility.IconContent("_Popup"), GUIStyle.none))
        {
            ShowProjectEntryMenu(entry, dataIndex);
        }

        // 定位按钮
        Rect locateRect = new Rect(rowRect.xMax - 24f, rowRect.y + 4f, 20f, rowRect.height - 8f);
        using (new EditorGUI.DisabledScope(entry.assetReference == null))
        {
            if (GUI.Button(locateRect, locateIcon, GUIStyle.none))
            {
                SelectInProject(entry.assetReference);
            }
        }

        // 拖拽开始 - 支持从收藏夹拖出资源到其他地方赋值（排除拖拽手柄区域）
        if (Event.current.type == EventType.MouseDrag && rowRect.Contains(Event.current.mousePosition) 
            && !handleRect.Contains(Event.current.mousePosition) && draggingProjectIndex < 0)
        {
            if (entry.assetReference != null)
            {
                // 取消待处理的点击
                pendingClickProjectIndex = -1;
                isDraggingOutAsset = true;
                
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new UnityEngine.Object[] { entry.assetReference };
                DragAndDrop.paths = new string[] { AssetDatabase.GetAssetPath(entry.assetReference) };
                DragAndDrop.StartDrag(entry.displayName ?? entry.assetReference.name);
                Event.current.Use();
            }
        }

        // 点击事件（排除拖拽手柄区域）- MouseDown 只记录状态
        if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition)
            && !handleRect.Contains(Event.current.mousePosition))
        {
            if (Event.current.button == 0)
            {
                // 只记录待处理的点击，不立即执行
                pendingClickProjectIndex = dataIndex;
                isDraggingOutAsset = false;
            }
            else if (Event.current.button == 1)
            {
                ShowProjectEntryMenu(entry, dataIndex);
            }
            Event.current.Use();
        }
        
        // MouseUp 时判断是否执行点击
        if (Event.current.type == EventType.MouseUp && pendingClickProjectIndex == dataIndex && !isDraggingOutAsset)
        {
            double currentTime = EditorApplication.timeSinceStartup;
            if (lastClickIndex == dataIndex && (currentTime - lastClickTime) < DoubleClickTime)
            {
                // 双击：打开资源
                OpenAsset(entry.assetReference);
                lastClickIndex = -1;
            }
            else
            {
                // 单击：选中并定位
                SelectInProject(entry.assetReference);
                lastClickTime = currentTime;
                lastClickIndex = dataIndex;
            }
            pendingClickProjectIndex = -1;
            Event.current.Use();
        }
        
        // 鼠标离开窗口或其他情况重置状态
        if (Event.current.type == EventType.MouseUp)
        {
            isDraggingOutAsset = false;
        }

        // 编辑模式 - 需要在外部处理
    }

    private void DrawProjectInlineEditor(FavoriteAssetEntry entry, int index)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("编辑收藏", EditorStyles.boldLabel);

        editingProjectAsset = EditorGUILayout.ObjectField("资源", editingProjectAsset, typeof(UnityEngine.Object), false);
        editingProjectName = EditorGUILayout.TextField("名称", editingProjectName);
        editingProjectNote = EditorGUILayout.TextField("备注", editingProjectNote);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("取消", GUILayout.Width(50f)))
        {
            CancelProjectEditing();
        }
        if (GUILayout.Button("保存", GUILayout.Width(50f)))
        {
            ApplyProjectEditing(entry);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void ShowProjectEntryMenu(FavoriteAssetEntry entry, int index)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("选中并定位"), false, () => SelectInProject(entry?.assetReference));
        menu.AddItem(new GUIContent("复制路径"), false, () => CopyProjectEntryPath(entry));
        menu.AddSeparator(string.Empty);
        menu.AddItem(new GUIContent("编辑"), editingProjectIndex == index, () => BeginProjectEdit(entry, index));

        if (index == 0)
            menu.AddDisabledItem(new GUIContent("上移"));
        else
            menu.AddItem(new GUIContent("上移"), false, () => MoveProjectEntry(index, -1));

        if (index >= config.EditableEntries.Count - 1)
            menu.AddDisabledItem(new GUIContent("下移"));
        else
            menu.AddItem(new GUIContent("下移"), false, () => MoveProjectEntry(index, 1));

        menu.AddSeparator(string.Empty);
        menu.AddItem(new GUIContent("移除"), false, () => RemoveProjectEntryAt(index));
        menu.ShowAsContext();
    }

    private void BeginProjectEdit(FavoriteAssetEntry entry, int index)
    {
        editingProjectIndex = index;
        editingProjectAsset = entry?.assetReference;
        editingProjectName = entry?.displayName ?? string.Empty;
        editingProjectNote = entry?.note ?? string.Empty;
    }

    private void ApplyProjectEditing(FavoriteAssetEntry entry)
    {
        if (editingProjectAsset != null && !AssetDatabase.Contains(editingProjectAsset))
        {
            EditorUtility.DisplayDialog("无法保存", "只能引用 Project 中的资源或文件夹。", "好的");
            return;
        }

        entry.assetReference = editingProjectAsset;
        entry.displayName = editingProjectName?.Trim();
        entry.note = editingProjectNote ?? string.Empty;
        MarkConfigDirty();
        CancelProjectEditing();
    }

    private void CancelProjectEditing()
    {
        editingProjectIndex = -1;
        editingProjectName = string.Empty;
        editingProjectNote = string.Empty;
        editingProjectAsset = null;
    }

    private void CopyProjectEntryPath(FavoriteAssetEntry entry)
    {
        string path = GetCachedPath(entry?.assetReference);
        if (!string.IsNullOrEmpty(path))
        {
            EditorGUIUtility.systemCopyBuffer = path;
            ShowNotification(new GUIContent("路径已复制"));
        }
    }

    private void MoveProjectEntry(int index, int offset)
    {
        int newIndex = Mathf.Clamp(index + offset, 0, config.EditableEntries.Count - 1);
        if (newIndex == index) return;

        FavoriteAssetEntry entry = config.EditableEntries[index];
        config.EditableEntries.RemoveAt(index);
        config.EditableEntries.Insert(newIndex, entry);
        CancelProjectEditing();
        MarkConfigDirty();
    }

    private void RemoveProjectEntryAt(int index)
    {
        if (index < 0 || index >= config.EditableEntries.Count) return;
        config.EditableEntries.RemoveAt(index);
        CancelProjectEditing();
        MarkConfigDirty();
        Repaint();
    }

    private void AddProjectSelection()
    {
        UnityEngine.Object[] selection = Selection.objects;
        if (selection == null || selection.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先在 Project 视图选择文件或文件夹。", "好的");
            return;
        }

        int added = 0;
        foreach (UnityEngine.Object obj in selection)
        {
            if (TryAddProjectFavorite(obj, obj.name, string.Empty))
            {
                added++;
            }
        }

        if (added > 0)
        {
            ShowNotification(new GUIContent($"已添加 {added} 项"));
        }
    }

    private void ClearProjectFavorites()
    {
        if (config.EditableEntries.Count == 0)
        {
            ShowNotification(new GUIContent("列表已为空"));
            return;
        }
        
        if (EditorUtility.DisplayDialog("确认清空", "确定要清空所有 Project 收藏吗？", "清空", "取消"))
        {
            config.EditableEntries.Clear();
            CancelProjectEditing();
            MarkConfigDirty();
            ShowNotification(new GUIContent("已清空 Project 收藏"));
        }
    }

    private bool TryAddProjectFavorite(UnityEngine.Object asset, string displayName, string note)
    {
        if (asset == null) return false;

        if (!AssetDatabase.Contains(asset))
        {
            EditorUtility.DisplayDialog("无法添加", "只能收藏 Project 中的资源或文件夹。", "好的");
            return false;
        }

        if (config.ContainsAsset(asset))
        {
            ShowNotification(new GUIContent("已存在该资源"));
            return false;
        }

        FavoriteAssetEntry entry = new FavoriteAssetEntry
        {
            assetReference = asset,
            displayName = string.IsNullOrWhiteSpace(displayName) ? asset.name : displayName.Trim(),
            note = note ?? string.Empty
        };

        config.EditableEntries.Add(entry);
        MarkConfigDirty();
        return true;
    }

    private bool MatchesProjectFilter(FavoriteAssetEntry entry)
    {
        // 类型过滤
        if (projectTypeFilterIndex > 0 && entry?.assetReference != null)
        {
            string assetPath = GetCachedPath(entry.assetReference);
            System.Type filterType = ProjectTypeFilters[projectTypeFilterIndex];
            string filterName = ProjectTypeFilterNames[projectTypeFilterIndex];
            
            bool matchesType = false;
            
            if (filterName == "Folder")
            {
                // 文件夹特殊处理
                matchesType = AssetDatabase.IsValidFolder(assetPath);
            }
            else if (filterName == "Model")
            {
                // 模型特殊处理（.fbx, .obj 等）
                string ext = System.IO.Path.GetExtension(assetPath).ToLower();
                matchesType = ext == ".fbx" || ext == ".obj" || ext == ".dae" || ext == ".3ds" || ext == ".blend";
            }
            else if (filterName == "Prefab")
            {
                // Prefab 特殊处理
                matchesType = assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
            }
            else if (filterName == "AnimationClip")
            {
                // AnimationClip 扩展：包含 AnimatorController、AnimatorOverrideController、Timeline
                System.Type assetType = entry.assetReference.GetType();
                matchesType = typeof(AnimationClip).IsAssignableFrom(assetType)
                    || typeof(UnityEditor.Animations.AnimatorController).IsAssignableFrom(assetType)
                    || typeof(AnimatorOverrideController).IsAssignableFrom(assetType)
                    || assetType.FullName == "UnityEngine.Timeline.TimelineAsset";
            }
            else if (filterType != null)
            {
                // 普通类型匹配
                matchesType = filterType.IsAssignableFrom(entry.assetReference.GetType());
            }
            
            if (!matchesType) return false;
        }
        
        // 搜索关键字过滤
        if (string.IsNullOrWhiteSpace(searchKeyword)) return true;

        string keyword = searchKeyword.Trim();
        if (!string.IsNullOrEmpty(entry.displayName) &&
            entry.displayName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (entry.assetReference != null)
        {
            string path = GetCachedPath(entry.assetReference);
            if (!string.IsNullOrEmpty(path) &&
                path.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static string GetProjectEntryDisplayName(FavoriteAssetEntry entry)
    {
        if (entry == null) return "(空)";
        if (!string.IsNullOrWhiteSpace(entry.displayName)) return entry.displayName;
        return entry.assetReference != null ? entry.assetReference.name : "未命名";
    }

    private static void SelectInProject(UnityEngine.Object asset)
    {
        if (asset == null) return;
        
        // 检查是否为文件夹
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            // 使用反射调用 ProjectBrowser.ShowFolderContents 打开文件夹
            var projectBrowserType = System.Type.GetType("UnityEditor.ProjectBrowser,UnityEditor");
            if (projectBrowserType != null)
            {
                var window = EditorWindow.GetWindow(projectBrowserType);
                if (window != null)
                {
                    int folderInstanceID = asset.GetInstanceID();
                    var showFolderContentsMethod = projectBrowserType.GetMethod(
                        "ShowFolderContents",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (showFolderContentsMethod != null)
                    {
                        showFolderContentsMethod.Invoke(window, new object[] { folderInstanceID, true });
                        return;
                    }
                }
            }
        }
        
        // 非文件夹或反射失败时使用默认行为
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        EditorUtility.FocusProjectWindow();
    }

    /// <summary>
    /// 双击打开资源（场景文件打开场景，其他资源用默认方式打开）
    /// </summary>
    private static void OpenAsset(UnityEngine.Object asset)
    {
        if (asset == null) return;
        
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(assetPath)) return;
        
        // 场景文件：打开场景
        if (assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(assetPath);
            }
            return;
        }
        
        // 文件夹：在 Project 窗口中打开
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            SelectInProject(asset);
            return;
        }
        
        // 其他资源：使用 AssetDatabase.OpenAsset 打开
        AssetDatabase.OpenAsset(asset);
    }

    // ==================== Hierarchy 收藏面板 ====================

    private void DrawHierarchyPanel()
    {
        // 标题栏
        DrawPanelHeader("🎮 Hierarchy 收藏", "添加选中", AddHierarchySelection, ClearHierarchyFavorites);
        
        // 收藏列表
        IReadOnlyList<HierarchyFavoriteEntry> entries = config.HierarchyEntries;
        if (entries == null || entries.Count == 0)
        {
            // 空面板时显示拖拽提示区域
            Rect emptyDropArea = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandHeight(true));
            HandleHierarchyPanelDragAndDrop(emptyDropArea);
            
            EditorGUI.HelpBox(new Rect(emptyDropArea.x + 10, emptyDropArea.y + 10, emptyDropArea.width - 20, 60),
                "暂无 Hierarchy 收藏\n从 Hierarchy 面板拖拽对象到此处，或选中对象后点击上方按钮添加", MessageType.Info);
            return;
        }

        // 获取滚动区域用于拖拽检测
        Rect scrollAreaRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandHeight(true));
        lastHierarchyScrollArea = scrollAreaRect; // 记录滚动区域位置
        
        // 处理拖拽
        HandleHierarchyPanelDragAndDrop(scrollAreaRect);
        
        // 绘制滚动列表
        GUI.BeginGroup(scrollAreaRect);
        Rect innerRect = new Rect(0, 0, scrollAreaRect.width, scrollAreaRect.height);
        hierarchyScrollPosition = GUI.BeginScrollView(innerRect, hierarchyScrollPosition, 
            new Rect(0, 0, scrollAreaRect.width - 16, entries.Count * RowHeight));
        
        for (int i = 0; i < entries.Count; i++)
        {
            HierarchyFavoriteEntry entry = entries[i];
            if (entry == null || !MatchesHierarchyFilter(entry)) continue;
            DrawHierarchyEntryInScrollView(entry, i, scrollAreaRect.width - 16);
        }
        
        // 绘制拖拽到末尾的高亮线
        if (draggingHierarchyIndex >= 0 && dropTargetHierarchyIndex == entries.Count)
        {
            float bottomY = entries.Count * RowHeight;
            Color lineColor = new Color(0.3f, 0.8f, 0.5f, 1f);
            float lineWidth = scrollAreaRect.width - 16;
            EditorGUI.DrawRect(new Rect(0, bottomY - 2f, lineWidth, 4f), lineColor);
            EditorGUI.DrawRect(new Rect(0, bottomY - 4f, 8f, 8f), lineColor);
            EditorGUI.DrawRect(new Rect(lineWidth - 8f, bottomY - 4f, 8f, 8f), lineColor);
        }
        
        GUI.EndScrollView();
        GUI.EndGroup();
    }

    private void HandleHierarchyPanelDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;
        
        if (!dropArea.Contains(evt.mousePosition)) return;
        
        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            // 检查是否有有效的 Hierarchy GameObject
            bool hasValidGO = false;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                GameObject go = obj as GameObject;
                if (go != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(go)))
                {
                    hasValidGO = true;
                    break;
                }
            }
            
            if (hasValidGO)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    AddHierarchyFromDrag(DragAndDrop.objectReferences);
                }
                
                evt.Use();
            }
        }
        
        // 绘制拖拽提示
        if (evt.type == EventType.Repaint && DragAndDrop.visualMode == DragAndDropVisualMode.Copy && dropArea.Contains(evt.mousePosition))
        {
            EditorGUI.DrawRect(dropArea, new Color(0.3f, 0.8f, 0.5f, 0.15f));
            // 绘制边框
            Handles.BeginGUI();
            Handles.color = new Color(0.3f, 0.8f, 0.5f, 0.8f);
            Handles.DrawLine(new Vector3(dropArea.x, dropArea.y), new Vector3(dropArea.xMax, dropArea.y));
            Handles.DrawLine(new Vector3(dropArea.xMax, dropArea.y), new Vector3(dropArea.xMax, dropArea.yMax));
            Handles.DrawLine(new Vector3(dropArea.xMax, dropArea.yMax), new Vector3(dropArea.x, dropArea.yMax));
            Handles.DrawLine(new Vector3(dropArea.x, dropArea.yMax), new Vector3(dropArea.x, dropArea.y));
            Handles.EndGUI();
        }
    }

    private void AddHierarchyFromDrag(UnityEngine.Object[] draggedObjects)
    {
        Scene currentScene = SceneManager.GetActiveScene();
        string scenePath = currentScene.path;
        
        if (string.IsNullOrEmpty(scenePath))
        {
            EditorUtility.DisplayDialog("提示", "请先保存当前场景。", "好的");
            return;
        }

        int added = 0;
        foreach (var obj in draggedObjects)
        {
            GameObject go = obj as GameObject;
            if (go == null) continue;
            
            // 跳过 Project 中的资源
            string assetPath = AssetDatabase.GetAssetPath(go);
            if (!string.IsNullOrEmpty(assetPath)) continue;

            string hierarchyPath = GetGameObjectPath(go);
            if (config.ContainsHierarchyEntry(scenePath, hierarchyPath))
            {
                continue;
            }

            HierarchyFavoriteEntry entry = new HierarchyFavoriteEntry
            {
                displayName = go.name,
                scenePath = scenePath,
                gameObjectPath = hierarchyPath,
                note = string.Empty
            };
            config.EditableHierarchyEntries.Add(entry);
            added++;
        }

        if (added > 0)
        {
            MarkConfigDirty();
            ShowNotification(new GUIContent($"已添加 {added} 项到 Hierarchy 收藏"));
        }
        else
        {
            ShowNotification(new GUIContent("所选对象已存在或无效"));
        }
    }

    private void DrawHierarchyEntryInScrollView(HierarchyFavoriteEntry entry, int index, float width)
    {
        EnsureLocateIconLoaded();
        EnsureDragHandleIconLoaded();
        
        Rect rowRect = new Rect(0, index * RowHeight, width, RowHeight - 2f);
        Color rowColor = index % 2 == 0 ? RowEvenColor : RowOddColor;
        
        // 正在拖拽的行：半透明绿色高亮
        if (draggingHierarchyIndex == index)
        {
            EditorGUI.DrawRect(rowRect, new Color(0.3f, 0.8f, 0.5f, 0.4f));
        }
        else
        {
            EditorGUI.DrawRect(rowRect, rowColor);
        }
        
        // 拖拽目标高亮线（绘制在行背景之后，更明显）
        if (dropTargetHierarchyIndex == index && draggingHierarchyIndex >= 0 && draggingHierarchyIndex != index)
        {
            Color lineColor = new Color(0.3f, 0.8f, 0.5f, 1f);
            // 第一行时高亮线绘制在行顶部内侧（y=2），其他行绘制在行顶部外侧
            float lineY = index == 0 ? 2f : rowRect.y - 2f;
            float lineHeight = index == 0 ? 6f : 4f; // 第一行加粗
            EditorGUI.DrawRect(new Rect(rowRect.x, lineY, rowRect.width, lineHeight), lineColor);
            // 左端圆点
            float dotY = index == 0 ? 0f : lineY - 2f;
            EditorGUI.DrawRect(new Rect(rowRect.x, dotY, 10f, 10f), lineColor);
            // 右端圆点
            EditorGUI.DrawRect(new Rect(rowRect.xMax - 10f, dotY, 10f, 10f), lineColor);
        }

        // 拖拽手柄（左侧）
        Rect handleRect = new Rect(rowRect.x + 2f, rowRect.y + 4f, DragHandleWidth, rowRect.height - 8f);
        
        // 手柄背景反馈
        if (draggingHierarchyIndex == index)
        {
            // 正在拖拽：深绿色背景
            EditorGUI.DrawRect(handleRect, new Color(0.2f, 0.6f, 0.4f, 0.8f));
        }
        else if (handleRect.Contains(Event.current.mousePosition))
        {
            // hover：浅灰色背景
            EditorGUI.DrawRect(handleRect, new Color(0.4f, 0.4f, 0.4f, 0.5f));
            Repaint(); // 确保 hover 状态实时更新
        }
        
        EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.Pan);
        GUI.Label(handleRect, dragHandleIcon);
        
        // 处理拖拽手柄的拖拽事件
        HandleHierarchyDragReorder(handleRect, index, rowRect);

        // 图标（向右偏移）
        Rect iconRect = new Rect(handleRect.xMax + 2f, rowRect.y + 4f, 18f, 18f);
        Texture goIcon = EditorGUIUtility.IconContent("GameObject Icon").image;
        if (goIcon != null)
        {
            GUI.DrawTexture(iconRect, goIcon, ScaleMode.ScaleToFit);
        }

        // 名称和场景信息
        Rect nameRect = new Rect(iconRect.xMax + 4f, rowRect.y, rowRect.width - 90f, rowRect.height);
        EnsureLabelStyleCached();
        string sceneName = System.IO.Path.GetFileNameWithoutExtension(entry.scenePath);
        string tooltip = $"场景: {entry.scenePath}\n路径: {entry.gameObjectPath}";
        GUI.Label(nameRect, new GUIContent($"{entry.displayName} ({sceneName})", tooltip), cachedLabelStyle);

        // 菜单按钮
        Rect moreRect = new Rect(rowRect.xMax - 48f, rowRect.y + 4f, 20f, rowRect.height - 8f);
        if (GUI.Button(moreRect, EditorGUIUtility.IconContent("_Popup"), GUIStyle.none))
        {
            ShowHierarchyEntryMenu(entry, index);
        }

        // 定位按钮
        Rect locateRect = new Rect(rowRect.xMax - 24f, rowRect.y + 4f, 20f, rowRect.height - 8f);
        if (GUI.Button(locateRect, locateIcon, GUIStyle.none))
        {
            SelectInHierarchy(entry);
        }

        // 拖拽开始 - 支持从收藏夹拖出 GameObject 到其他地方赋值（排除拖拽手柄区域）
        if (Event.current.type == EventType.MouseDrag && rowRect.Contains(Event.current.mousePosition)
            && !handleRect.Contains(Event.current.mousePosition) && draggingHierarchyIndex < 0)
        {
            // 取消待处理的点击
            pendingClickHierarchyIndex = -1;
            isDraggingOutAsset = true;
            
            // 尝试获取缓存的 GameObject，或通过路径查找
            GameObject go = entry.cachedObject;
            if (go == null)
            {
                Scene currentScene = SceneManager.GetActiveScene();
                if (currentScene.path == entry.scenePath)
                {
                    go = FindGameObjectByPath(entry.gameObjectPath);
                    entry.cachedObject = go;
                }
            }
            
            if (go != null)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new UnityEngine.Object[] { go };
                DragAndDrop.StartDrag(entry.displayName ?? go.name);
                Event.current.Use();
            }
        }

        // 点击事件（排除拖拽手柄区域）- MouseDown 只记录状态
        if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition)
            && !handleRect.Contains(Event.current.mousePosition))
        {
            if (Event.current.button == 0)
            {
                // 只记录待处理的点击，不立即执行
                pendingClickHierarchyIndex = index;
                isDraggingOutAsset = false;
            }
            else if (Event.current.button == 1)
            {
                ShowHierarchyEntryMenu(entry, index);
            }
            Event.current.Use();
        }
        
        // MouseUp 时判断是否执行点击
        int hierarchyClickIndex = index + 10000; // 区分 Project 和 Hierarchy 的索引
        if (Event.current.type == EventType.MouseUp && pendingClickHierarchyIndex == index && !isDraggingOutAsset)
        {
            double currentTime = EditorApplication.timeSinceStartup;
            if (lastClickIndex == hierarchyClickIndex && (currentTime - lastClickTime) < DoubleClickTime)
            {
                // 双击：预制体实例进入编辑模式，普通对象聚焦到 Scene 视图
                DoubleClickHierarchyObject(entry);
                lastClickIndex = -1;
            }
            else
            {
                // 单击：选中并聚焦到 Scene 视图
                SelectAndFrameInHierarchy(entry);
                lastClickTime = currentTime;
                lastClickIndex = hierarchyClickIndex;
            }
            pendingClickHierarchyIndex = -1;
            Event.current.Use();
        }
        
        // 鼠标离开窗口或其他情况重置状态
        if (Event.current.type == EventType.MouseUp)
        {
            isDraggingOutAsset = false;
        }
    }

    private void DrawHierarchyInlineEditor(HierarchyFavoriteEntry entry, int index)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("编辑收藏", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("场景", entry.scenePath);
        EditorGUILayout.LabelField("路径", entry.gameObjectPath);
        editingHierarchyName = EditorGUILayout.TextField("名称", editingHierarchyName);
        editingHierarchyNote = EditorGUILayout.TextField("备注", editingHierarchyNote);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("取消", GUILayout.Width(50f)))
        {
            CancelHierarchyEditing();
        }
        if (GUILayout.Button("保存", GUILayout.Width(50f)))
        {
            ApplyHierarchyEditing(entry);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void ShowHierarchyEntryMenu(HierarchyFavoriteEntry entry, int index)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("选中并定位"), false, () => SelectInHierarchy(entry));
        menu.AddItem(new GUIContent("复制路径"), false, () => CopyHierarchyEntryPath(entry));
        menu.AddSeparator(string.Empty);
        menu.AddItem(new GUIContent("编辑"), editingHierarchyIndex == index, () => BeginHierarchyEdit(entry, index));

        if (index == 0)
            menu.AddDisabledItem(new GUIContent("上移"));
        else
            menu.AddItem(new GUIContent("上移"), false, () => MoveHierarchyEntry(index, -1));

        if (index >= config.EditableHierarchyEntries.Count - 1)
            menu.AddDisabledItem(new GUIContent("下移"));
        else
            menu.AddItem(new GUIContent("下移"), false, () => MoveHierarchyEntry(index, 1));

        menu.AddSeparator(string.Empty);
        menu.AddItem(new GUIContent("移除"), false, () => RemoveHierarchyEntryAt(index));
        menu.ShowAsContext();
    }

    private void BeginHierarchyEdit(HierarchyFavoriteEntry entry, int index)
    {
        editingHierarchyIndex = index;
        editingHierarchyName = entry?.displayName ?? string.Empty;
        editingHierarchyNote = entry?.note ?? string.Empty;
    }

    private void ApplyHierarchyEditing(HierarchyFavoriteEntry entry)
    {
        entry.displayName = editingHierarchyName?.Trim();
        entry.note = editingHierarchyNote ?? string.Empty;
        MarkConfigDirty();
        CancelHierarchyEditing();
    }

    private void CancelHierarchyEditing()
    {
        editingHierarchyIndex = -1;
        editingHierarchyName = string.Empty;
        editingHierarchyNote = string.Empty;
    }

    private void CopyHierarchyEntryPath(HierarchyFavoriteEntry entry)
    {
        if (entry != null && !string.IsNullOrEmpty(entry.gameObjectPath))
        {
            EditorGUIUtility.systemCopyBuffer = entry.gameObjectPath;
            ShowNotification(new GUIContent("路径已复制"));
        }
    }

    private void MoveHierarchyEntry(int index, int offset)
    {
        int newIndex = Mathf.Clamp(index + offset, 0, config.EditableHierarchyEntries.Count - 1);
        if (newIndex == index) return;

        HierarchyFavoriteEntry entry = config.EditableHierarchyEntries[index];
        config.EditableHierarchyEntries.RemoveAt(index);
        config.EditableHierarchyEntries.Insert(newIndex, entry);
        CancelHierarchyEditing();
        MarkConfigDirty();
    }

    private void RemoveHierarchyEntryAt(int index)
    {
        if (index < 0 || index >= config.EditableHierarchyEntries.Count) return;
        config.EditableHierarchyEntries.RemoveAt(index);
        CancelHierarchyEditing();
        MarkConfigDirty();
        Repaint();
    }

    private void AddHierarchySelection()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先在 Hierarchy 视图选择对象。", "好的");
            return;
        }

        Scene currentScene = SceneManager.GetActiveScene();
        string scenePath = currentScene.path;
        
        if (string.IsNullOrEmpty(scenePath))
        {
            EditorUtility.DisplayDialog("提示", "请先保存当前场景。", "好的");
            return;
        }

        int added = 0;
        foreach (GameObject go in selectedObjects)
        {
            if (go == null) continue;
            
            // 跳过 Project 中的资源
            string assetPath = AssetDatabase.GetAssetPath(go);
            if (!string.IsNullOrEmpty(assetPath)) continue;

            string hierarchyPath = GetGameObjectPath(go);
            if (config.ContainsHierarchyEntry(scenePath, hierarchyPath))
            {
                continue;
            }

            HierarchyFavoriteEntry entry = new HierarchyFavoriteEntry
            {
                displayName = go.name,
                scenePath = scenePath,
                gameObjectPath = hierarchyPath,
                note = string.Empty
            };
            config.EditableHierarchyEntries.Add(entry);
            added++;
        }

        if (added > 0)
        {
            MarkConfigDirty();
            ShowNotification(new GUIContent($"已添加 {added} 项"));
        }
        else
        {
            ShowNotification(new GUIContent("所选对象已存在或无效"));
        }
    }

    private void ClearHierarchyFavorites()
    {
        if (config.EditableHierarchyEntries.Count == 0)
        {
            ShowNotification(new GUIContent("列表已为空"));
            return;
        }
        
        if (EditorUtility.DisplayDialog("确认清空", "确定要清空所有 Hierarchy 收藏吗？", "清空", "取消"))
        {
            config.EditableHierarchyEntries.Clear();
            CancelHierarchyEditing();
            MarkConfigDirty();
            ShowNotification(new GUIContent("已清空 Hierarchy 收藏"));
        }
    }

    private void SelectInHierarchy(HierarchyFavoriteEntry entry)
    {
        if (entry == null) return;

        Scene currentScene = SceneManager.GetActiveScene();
        
        // 检查场景是否匹配
        if (currentScene.path != entry.scenePath)
        {
            // 提示用户打开对应场景
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(entry.scenePath);
            bool openScene = EditorUtility.DisplayDialog(
                "场景不匹配",
                $"该对象位于场景 \"{sceneName}\"，是否打开该场景？",
                "打开场景",
                "取消"
            );
            
            if (openScene)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(entry.scenePath);
                    // 延迟选中对象
                    EditorApplication.delayCall += () => SelectGameObjectByPath(entry);
                }
            }
            return;
        }

        SelectGameObjectByPath(entry);
    }

    private void SelectGameObjectByPath(HierarchyFavoriteEntry entry)
    {
        GameObject go = FindGameObjectByPath(entry.gameObjectPath);
        if (go != null)
        {
            entry.cachedObject = go;
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }
        else
        {
            ShowNotification(new GUIContent($"未找到对象: {entry.gameObjectPath}"));
        }
    }

    /// <summary>
    /// 单击：选中并聚焦到 Scene 视图
    /// </summary>
    private void SelectAndFrameInHierarchy(HierarchyFavoriteEntry entry)
    {
        if (entry == null) return;

        Scene currentScene = SceneManager.GetActiveScene();
        
        // 检查场景是否匹配
        if (currentScene.path != entry.scenePath)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(entry.scenePath);
            bool openScene = EditorUtility.DisplayDialog(
                "场景不匹配",
                $"该对象位于场景 \"{sceneName}\"，是否打开该场景？",
                "打开场景",
                "取消"
            );
            
            if (openScene && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(entry.scenePath);
                EditorApplication.delayCall += () => SelectAndFrameGameObject(entry);
            }
            return;
        }

        SelectAndFrameGameObject(entry);
    }

    private void SelectAndFrameGameObject(HierarchyFavoriteEntry entry)
    {
        GameObject go = FindGameObjectByPath(entry.gameObjectPath);
        if (go == null)
        {
            ShowNotification(new GUIContent($"未找到对象: {entry.gameObjectPath}"));
            return;
        }

        entry.cachedObject = go;
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        
        // 聚焦到 Scene 视图
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }
    }

    /// <summary>
    /// 双击：预制体实例进入编辑模式，普通对象聚焦到 Scene 视图
    /// </summary>
    private void DoubleClickHierarchyObject(HierarchyFavoriteEntry entry)
    {
        if (entry == null) return;

        Scene currentScene = SceneManager.GetActiveScene();
        
        // 检查场景是否匹配
        if (currentScene.path != entry.scenePath)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(entry.scenePath);
            bool openScene = EditorUtility.DisplayDialog(
                "场景不匹配",
                $"该对象位于场景 \"{sceneName}\"，是否打开该场景？",
                "打开场景",
                "取消"
            );
            
            if (openScene && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(entry.scenePath);
                EditorApplication.delayCall += () => DoubleClickGameObject(entry);
            }
            return;
        }

        DoubleClickGameObject(entry);
    }

    private void DoubleClickGameObject(HierarchyFavoriteEntry entry)
    {
        GameObject go = FindGameObjectByPath(entry.gameObjectPath);
        if (go == null)
        {
            ShowNotification(new GUIContent($"未找到对象: {entry.gameObjectPath}"));
            return;
        }

        entry.cachedObject = go;
        Selection.activeGameObject = go;

        // 检查是否为预制体实例
        if (PrefabUtility.IsPartOfPrefabInstance(go))
        {
            // 进入预制体编辑模式
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
                return;
            }
        }

        // 普通对象：聚焦到 Scene 视图
        EditorGUIUtility.PingObject(go);
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }
    }

    private bool MatchesHierarchyFilter(HierarchyFavoriteEntry entry)
    {
        if (string.IsNullOrWhiteSpace(searchKeyword)) return true;

        string keyword = searchKeyword.Trim();
        if (!string.IsNullOrEmpty(entry.displayName) &&
            entry.displayName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (!string.IsNullOrEmpty(entry.gameObjectPath) &&
            entry.gameObjectPath.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (!string.IsNullOrEmpty(entry.scenePath) &&
            entry.scenePath.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    // ==================== 通用方法 ====================

    /// <summary>
    /// 绘制 Project 面板标题栏（含类型过滤）
    /// </summary>
    private void DrawProjectPanelHeader()
    {
        Rect headerRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(24f));
        EditorGUI.DrawRect(headerRect, HeaderColor);
        
        EnsureHeaderStyleCached();
        GUILayout.Space(8f);
        GUILayout.Label("📁 Project 收藏", cachedHeaderStyle);
        GUILayout.FlexibleSpace();
        
        // 类型过滤下拉框
        EditorGUI.BeginChangeCheck();
        projectTypeFilterIndex = EditorGUILayout.Popup(projectTypeFilterIndex, ProjectTypeFilterNames, 
            EditorStyles.toolbarPopup, GUILayout.Width(90f));
        if (EditorGUI.EndChangeCheck())
        {
            Repaint();
        }
        
        GUILayout.Space(4f);
        
        if (GUILayout.Button("添加选中", EditorStyles.miniButton, GUILayout.Width(60f)))
        {
            AddProjectSelection();
        }
        if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(40f)))
        {
            ClearProjectFavorites();
        }
        GUILayout.Space(4f);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPanelHeader(string title, string buttonText, Action buttonAction, Action clearAction)
    {
        // 使用 EditorGUILayout 水平布局确保按钮始终可见
        Rect headerRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(24f));
        EditorGUI.DrawRect(headerRect, HeaderColor);
        
        EnsureHeaderStyleCached();
        GUILayout.Space(8f);
        GUILayout.Label(title, cachedHeaderStyle);
        GUILayout.FlexibleSpace();
        
        if (GUILayout.Button(buttonText, EditorStyles.miniButton, GUILayout.Width(60f)))
        {
            buttonAction?.Invoke();
        }
        if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(40f)))
        {
            clearAction?.Invoke();
        }
        GUILayout.Space(4f);
        EditorGUILayout.EndHorizontal();
    }

    private void RemoveAllMissingEntries()
    {
        int removedProject = config.EditableEntries.RemoveAll(entry => entry == null || entry.assetReference == null);
        int removedHierarchy = config.EditableHierarchyEntries.RemoveAll(entry => entry == null || string.IsNullOrEmpty(entry.gameObjectPath));
        
        int total = removedProject + removedHierarchy;
        if (total > 0)
        {
            CancelProjectEditing();
            CancelHierarchyEditing();
            MarkConfigDirty();
            ShowNotification(new GUIContent($"已清理 {total} 条失效记录"));
        }
        else
        {
            ShowNotification(new GUIContent("没有失效记录"));
        }
    }

    private string GetCachedPath(UnityEngine.Object asset)
    {
        if (asset == null) return string.Empty;
        
        if (!pathCache.TryGetValue(asset, out string path))
        {
            path = AssetDatabase.GetAssetPath(asset);
            pathCache[asset] = path;
        }
        return path;
    }

    private Texture GetCachedIcon(FavoriteAssetEntry entry)
    {
        if (entry?.assetReference == null)
        {
            return EditorGUIUtility.IconContent("Folder Icon").image;
        }
        
        if (!iconCache.TryGetValue(entry.assetReference, out Texture icon))
        {
            icon = AssetPreview.GetMiniThumbnail(entry.assetReference);
            if (icon == null)
            {
                icon = EditorGUIUtility.IconContent("DefaultAsset Icon").image;
            }
            iconCache[entry.assetReference] = icon;
        }
        return icon;
    }

    private static void EnsureLocateIconLoaded()
    {
        if (locateIcon != null) return;
        locateIcon = EditorGUIUtility.IconContent("ViewToolOrbit");
        if (locateIcon == null || locateIcon.image == null)
        {
            locateIcon = new GUIContent("●");
        }
    }

    private static void EnsureDragHandleIconLoaded()
    {
        if (dragHandleIcon != null) return;
        // 使用 Unity 内置的拖拽手柄图标
        dragHandleIcon = EditorGUIUtility.IconContent("d_align_vertically_center");
        if (dragHandleIcon == null || dragHandleIcon.image == null)
        {
            dragHandleIcon = new GUIContent("≡");
        }
    }

    private void HandleProjectDragReorder(Rect handleRect, int index, Rect rowRect)
    {
        Event e = Event.current;
        
        // 鼠标按下开始拖拽
        if (e.type == EventType.MouseDown && handleRect.Contains(e.mousePosition) && e.button == 0)
        {
            draggingProjectIndex = index;
            dropTargetProjectIndex = -1;
            
            // 记录幽灵行信息
            var entry = config.FavoriteEntries[index];
            ghostRowName = GetProjectEntryDisplayName(entry);
            ghostRowIcon = GetCachedIcon(entry);
            isGhostRowProject = true;
            
            e.Use();
        }
        
        // 拖拽中
        if (draggingProjectIndex >= 0)
        {
            if (e.type == EventType.MouseDrag)
            {
                // 计算目标位置（基于滚动视图内的坐标）
                float localY = e.mousePosition.y;
                int targetIndex = Mathf.FloorToInt(localY / RowHeight);
                targetIndex = Mathf.Clamp(targetIndex, 0, config.EditableEntries.Count);
                dropTargetProjectIndex = targetIndex;
                Repaint();
                e.Use();
            }
            
            if (e.type == EventType.MouseUp)
            {
                if (dropTargetProjectIndex >= 0 && dropTargetProjectIndex != draggingProjectIndex)
                {
                    // 执行排序
                    var entry = config.EditableEntries[draggingProjectIndex];
                    config.EditableEntries.RemoveAt(draggingProjectIndex);
                    int insertIndex = dropTargetProjectIndex > draggingProjectIndex ? dropTargetProjectIndex - 1 : dropTargetProjectIndex;
                    insertIndex = Mathf.Clamp(insertIndex, 0, config.EditableEntries.Count);
                    config.EditableEntries.Insert(insertIndex, entry);
                    MarkConfigDirty();
                }
                
                // 清除幽灵行状态
                draggingProjectIndex = -1;
                dropTargetProjectIndex = -1;
                ghostRowName = string.Empty;
                ghostRowIcon = null;
                Repaint();
                e.Use();
            }
        }
    }

    private void HandleHierarchyDragReorder(Rect handleRect, int index, Rect rowRect)
    {
        Event e = Event.current;
        
        // 鼠标按下开始拖拽
        if (e.type == EventType.MouseDown && handleRect.Contains(e.mousePosition) && e.button == 0)
        {
            draggingHierarchyIndex = index;
            dropTargetHierarchyIndex = -1;
            
            // 记录幽灵行信息
            var entry = config.HierarchyEntries[index];
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(entry.scenePath);
            ghostRowName = $"{entry.displayName} ({sceneName})";
            ghostRowIcon = EditorGUIUtility.IconContent("GameObject Icon").image;
            isGhostRowProject = false;
            
            e.Use();
        }
        
        // 拖拽中
        if (draggingHierarchyIndex >= 0)
        {
            if (e.type == EventType.MouseDrag)
            {
                // 计算目标位置（基于滚动视图内的坐标）
                float localY = e.mousePosition.y;
                int targetIndex = Mathf.FloorToInt(localY / RowHeight);
                targetIndex = Mathf.Clamp(targetIndex, 0, config.EditableHierarchyEntries.Count);
                dropTargetHierarchyIndex = targetIndex;
                Repaint();
                e.Use();
            }
            
            if (e.type == EventType.MouseUp)
            {
                if (dropTargetHierarchyIndex >= 0 && dropTargetHierarchyIndex != draggingHierarchyIndex)
                {
                    // 执行排序
                    var entry = config.EditableHierarchyEntries[draggingHierarchyIndex];
                    config.EditableHierarchyEntries.RemoveAt(draggingHierarchyIndex);
                    int insertIndex = dropTargetHierarchyIndex > draggingHierarchyIndex ? dropTargetHierarchyIndex - 1 : dropTargetHierarchyIndex;
                    insertIndex = Mathf.Clamp(insertIndex, 0, config.EditableHierarchyEntries.Count);
                    config.EditableHierarchyEntries.Insert(insertIndex, entry);
                    MarkConfigDirty();
                }
                
                // 清除幽灵行状态
                draggingHierarchyIndex = -1;
                dropTargetHierarchyIndex = -1;
                ghostRowName = string.Empty;
                ghostRowIcon = null;
                Repaint();
                e.Use();
            }
        }
    }

    private static void EnsureLabelStyleCached()
    {
        if (cachedLabelStyle == null)
        {
            cachedLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft
            };
        }
    }

    private static void EnsureHeaderStyleCached()
    {
        if (cachedHeaderStyle == null)
        {
            cachedHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12
            };
        }
    }

    /// <summary>
    /// 绘制拖拽时的幽灵行（跟随鼠标）
    /// </summary>
    private void DrawGhostRow()
    {
        bool isDragging = (isGhostRowProject && draggingProjectIndex >= 0) || (!isGhostRowProject && draggingHierarchyIndex >= 0);
        if (!isDragging || string.IsNullOrEmpty(ghostRowName)) return;
        
        Event e = Event.current;
        if (e.type != EventType.Repaint) return;
        
        // 计算幽灵行位置（跟随鼠标，略微偏移）
        float ghostWidth = 200f;
        float ghostHeight = RowHeight - 2f;
        float offsetX = 15f;
        float offsetY = -ghostHeight * 0.5f;
        
        Rect ghostRect = new Rect(
            e.mousePosition.x + offsetX,
            e.mousePosition.y + offsetY,
            ghostWidth,
            ghostHeight
        );
        
        // 确保不超出窗口边界
        if (ghostRect.xMax > position.width - 5f)
            ghostRect.x = position.width - ghostWidth - 5f;
        if (ghostRect.yMax > position.height - 5f)
            ghostRect.y = position.height - ghostHeight - 5f;
        if (ghostRect.x < 5f)
            ghostRect.x = 5f;
        if (ghostRect.y < 5f)
            ghostRect.y = 5f;
        
        // 绘制阴影
        Color shadowColor = new Color(0f, 0f, 0f, 0.4f);
        EditorGUI.DrawRect(new Rect(ghostRect.x + 3f, ghostRect.y + 3f, ghostRect.width, ghostRect.height), shadowColor);
        
        // 绘制幽灵行背景
        Color bgColor = isGhostRowProject ? new Color(0.25f, 0.45f, 0.7f, 0.95f) : new Color(0.25f, 0.6f, 0.45f, 0.95f);
        EditorGUI.DrawRect(ghostRect, bgColor);
        
        // 绘制边框
        Color borderColor = isGhostRowProject ? new Color(0.4f, 0.7f, 1f, 1f) : new Color(0.4f, 0.9f, 0.6f, 1f);
        float borderWidth = 2f;
        EditorGUI.DrawRect(new Rect(ghostRect.x, ghostRect.y, ghostRect.width, borderWidth), borderColor);
        EditorGUI.DrawRect(new Rect(ghostRect.x, ghostRect.yMax - borderWidth, ghostRect.width, borderWidth), borderColor);
        EditorGUI.DrawRect(new Rect(ghostRect.x, ghostRect.y, borderWidth, ghostRect.height), borderColor);
        EditorGUI.DrawRect(new Rect(ghostRect.xMax - borderWidth, ghostRect.y, borderWidth, ghostRect.height), borderColor);
        
        // 绘制图标
        if (ghostRowIcon != null)
        {
            Rect iconRect = new Rect(ghostRect.x + 6f, ghostRect.y + (ghostRect.height - 18f) * 0.5f, 18f, 18f);
            GUI.DrawTexture(iconRect, ghostRowIcon, ScaleMode.ScaleToFit);
        }
        
        // 绘制名称
        Rect nameRect = new Rect(ghostRect.x + 28f, ghostRect.y, ghostRect.width - 34f, ghostRect.height);
        GUIStyle ghostLabelStyle = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white },
            fontStyle = FontStyle.Bold
        };
        GUI.Label(nameRect, ghostRowName, ghostLabelStyle);
    }

    private void MarkConfigDirty()
    {
        if (config == null) return;
        
        EditorUtility.SetDirty(config);
        isDirty = true;
        InvalidateCache();
        Repaint();
    }
}
#endif
