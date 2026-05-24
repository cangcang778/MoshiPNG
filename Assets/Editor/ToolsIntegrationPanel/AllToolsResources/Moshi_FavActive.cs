#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class Moshi_FavActiveEntry
{
    public string displayName;
    public string scenePath;
    public string gameObjectPath;
    public string globalObjectId;
    public int runtimeInstanceId;

    [NonSerialized]
    public GameObject cachedObject;
}

public sealed class Moshi_FavActiveConfig : ScriptableObject
{
    public const string DefaultAssetPath = "Assets/Editor/ToolsIntegrationPanel/AllToolsResources/Moshi_FavActiveConfig.asset";

    [SerializeField]
    private List<Moshi_FavActiveEntry> entries = new List<Moshi_FavActiveEntry>();

    public List<Moshi_FavActiveEntry> Entries => entries;

    public static Moshi_FavActiveConfig LoadOrCreate()
    {
        Moshi_FavActiveConfig config = AssetDatabase.LoadAssetAtPath<Moshi_FavActiveConfig>(DefaultAssetPath);
        if (config != null) return config;

        config = CreateInstance<Moshi_FavActiveConfig>();
        config.name = "Moshi_FavActiveConfig";
        AssetDatabase.CreateAsset(config, DefaultAssetPath);
        AssetDatabase.SaveAssets();
        return config;
    }

    public bool Contains(GameObject go)
    {
        if (go == null) return false;
        string objectId = Moshi_FavActive.GetGlobalObjectId(go);
        string scenePath = GetSceneKey(go.scene);
        string objectPath = Moshi_FavActive.GetGameObjectPath(go);
        foreach (Moshi_FavActiveEntry entry in entries)
        {
            if (entry == null) continue;
            if (!string.IsNullOrEmpty(objectId) && string.Equals(entry.globalObjectId, objectId, StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(entry.scenePath, scenePath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.gameObjectPath, objectPath, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public void Add(GameObject go)
    {
        if (go == null || EditorUtility.IsPersistent(go) || Contains(go)) return;
        entries.Add(new Moshi_FavActiveEntry
        {
            displayName = go.name,
            scenePath = GetSceneKey(go.scene),
            gameObjectPath = Moshi_FavActive.GetGameObjectPath(go),
            globalObjectId = Moshi_FavActive.GetGlobalObjectId(go),
            runtimeInstanceId = go.GetInstanceID(),
            cachedObject = go
        });
        EditorUtility.SetDirty(this);
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= entries.Count) return;
        entries.RemoveAt(index);
        EditorUtility.SetDirty(this);
    }

    public void ClearMissing()
    {
        entries.RemoveAll(entry => entry == null || Moshi_FavActive.ResolveGameObject(entry) == null);
        EditorUtility.SetDirty(this);
    }

    private static string GetSceneKey(Scene scene)
    {
        return string.IsNullOrEmpty(scene.path) ? scene.name : scene.path;
    }
}

public sealed class Moshi_FavActive : EditorWindow
{
    private const string TOOL_NAME = "收藏物体开关";
    private Moshi_FavActiveConfig config;
    private Vector2 scrollPosition;
    private GameObject manualObject;
    private bool autoRepairEnabled = true;
    private bool isAutoRepairing = false;

    [global::ToolAttribute(
        "工具Moshi资源收藏夹收藏物体开关",
        TOOL_NAME,
        "管理一组 Hierarchy 物体，并一键切换它们的激活状态。",
        "工具 / Moshi / 资源收藏夹",
        Order = 521)]
    [MenuItem("工具/Moshi/资源收藏夹/收藏物体开关")]
    public static void ShowWindow()
    {
        Moshi_FavActive window = GetWindow<Moshi_FavActive>(TOOL_NAME);
        window.minSize = new Vector2(360f, 420f);
        window.LoadConfig();
        window.Show();
    }

    [MenuItem("工具/Moshi/资源收藏夹/切换收藏物体开关")]
    public static void ToggleAllByMenu()
    {
        Moshi_FavActiveConfig cfg = Moshi_FavActiveConfig.LoadOrCreate();
        ToggleEntries(cfg, true);
    }

    public static string GetGameObjectPath(GameObject go)
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

    public static string GetGlobalObjectId(GameObject go)
    {
        if (go == null) return string.Empty;
        GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(go);
        return id.ToString();
    }

    public static GameObject ResolveGameObject(Moshi_FavActiveEntry entry)
    {
        if (entry == null) return null;
        if (entry.cachedObject != null)
        {
            RefreshEntryFromObject(entry, entry.cachedObject);
            return entry.cachedObject;
        }

        GameObject byInstanceId = ResolveByInstanceId(entry.runtimeInstanceId);
        if (byInstanceId != null)
        {
            RefreshEntryFromObject(entry, byInstanceId);
            return byInstanceId;
        }

        GameObject byId = ResolveByGlobalObjectId(entry.globalObjectId);
        if (byId != null)
        {
            RefreshEntryFromObject(entry, byId);
            return byId;
        }

        if (string.IsNullOrEmpty(entry.gameObjectPath)) return null;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            string sceneKey = string.IsNullOrEmpty(scene.path) ? scene.name : scene.path;
            if (!string.IsNullOrEmpty(entry.scenePath) &&
                !string.Equals(entry.scenePath, sceneKey, StringComparison.OrdinalIgnoreCase))
                continue;

            GameObject found = FindInScene(scene, entry.gameObjectPath);
            if (found != null)
            {
                RefreshEntryFromObject(entry, found);
                return found;
            }
        }

        GameObject fallback = FindByNameOrPathTail(entry);
        if (fallback != null)
        {
            RefreshEntryFromObject(entry, fallback);
            return fallback;
        }
        return null;
    }

    private static GameObject ResolveByInstanceId(int instanceId)
    {
        if (instanceId == 0) return null;
        return EditorUtility.InstanceIDToObject(instanceId) as GameObject;
    }

    private static GameObject ResolveByGlobalObjectId(string objectId)
    {
        if (string.IsNullOrEmpty(objectId)) return null;
        if (!GlobalObjectId.TryParse(objectId, out GlobalObjectId id)) return null;
        UnityEngine.Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
        return obj as GameObject;
    }

    private static GameObject FindByNameOrPathTail(Moshi_FavActiveEntry entry)
    {
        if (entry == null) return null;
        string targetName = GetFallbackName(entry);
        if (string.IsNullOrEmpty(targetName)) return null;

        GameObject singleMatch = null;
        int matchCount = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform transform in transforms)
                {
                    if (transform == null) continue;
                    GameObject go = transform.gameObject;
                    string currentPath = GetGameObjectPath(go);
                    bool nameMatched = string.Equals(go.name, targetName, StringComparison.OrdinalIgnoreCase);
                    bool tailMatched = !string.IsNullOrEmpty(entry.gameObjectPath) && currentPath.EndsWith("/" + entry.gameObjectPath, StringComparison.OrdinalIgnoreCase);
                    bool suffixMatched = !string.IsNullOrEmpty(entry.gameObjectPath) && currentPath.EndsWith("/" + targetName, StringComparison.OrdinalIgnoreCase);
                    if (!nameMatched && !tailMatched && !suffixMatched) continue;

                    singleMatch = go;
                    matchCount++;
                    if (matchCount > 1)
                        return null;
                }
            }
        }
        return matchCount == 1 ? singleMatch : null;
    }

    private static string GetFallbackName(Moshi_FavActiveEntry entry)
    {
        if (entry == null) return string.Empty;
        if (!string.IsNullOrEmpty(entry.displayName)) return entry.displayName;
        if (string.IsNullOrEmpty(entry.gameObjectPath)) return string.Empty;
        string[] parts = entry.gameObjectPath.Split('/');
        return parts.Length > 0 ? parts[parts.Length - 1] : string.Empty;
    }

    private static void RefreshEntryFromObject(Moshi_FavActiveEntry entry, GameObject go)
    {
        if (entry == null || go == null) return;
        entry.cachedObject = go;
        entry.scenePath = string.IsNullOrEmpty(go.scene.path) ? go.scene.name : go.scene.path;
        entry.gameObjectPath = GetGameObjectPath(go);
        entry.globalObjectId = GetGlobalObjectId(go);
        entry.runtimeInstanceId = go.GetInstanceID();
        if (string.IsNullOrEmpty(entry.displayName) || entry.displayName.StartsWith("Missing:", StringComparison.Ordinal))
            entry.displayName = go.name;
    }

    private static GameObject FindInScene(Scene scene, string objectPath)
    {
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(objectPath)) return null;
        string[] parts = objectPath.Split('/');
        if (parts.Length == 0) return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;
            Transform current = root.transform;
            for (int i = 1; i < parts.Length; i++)
            {
                current = current.Find(parts[i]);
                if (current == null) break;
            }
            if (current != null) return current.gameObject;
        }
        return null;
    }

    private void OnEnable()
    {
        LoadConfig();
        EditorApplication.hierarchyChanged += HandleHierarchyChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
    }

    private void HandleHierarchyChanged()
    {
        if (!autoRepairEnabled || config == null) return;
        AutoRepairEntries(false);
        Repaint();
    }

    private void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (!autoRepairEnabled || config == null) return;
        AutoRepairEntries(false);
        Repaint();
    }

    private void LoadConfig()
    {
        config = Moshi_FavActiveConfig.LoadOrCreate();
    }

    private void OnGUI()
    {
        if (config == null) LoadConfig();
        HandleLocalShortcut();

        DrawHeader();
        DrawAddArea();
        DrawToggleButton();
        DrawObjectList();
    }

    private void HandleLocalShortcut()
    {
        Event e = Event.current;
        if (e == null || e.type != EventType.KeyDown) return;
        if (e.keyCode != KeyCode.A || !e.alt || !e.shift) return;

        ToggleEntries(config, true);
        e.Use();
        Repaint();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label(TOOL_NAME, EditorStyles.miniBoldLabel);
        GUILayout.FlexibleSpace();
        autoRepairEnabled = GUILayout.Toggle(autoRepairEnabled, "自动修复", EditorStyles.toolbarButton);
        if (GUILayout.Button("立即修复", EditorStyles.toolbarButton))
        {
            int repaired = AutoRepairEntries(true);
            EditorUtility.DisplayDialog(TOOL_NAME, $"自动修复完成：已刷新 {repaired} 个引用。", "确定");
        }
        if (GUILayout.Button("清理失效", EditorStyles.toolbarButton))
        {
            config.ClearMissing();
            AssetDatabase.SaveAssets();
        }
        if (GUILayout.Button("定位配置", EditorStyles.toolbarButton))
        {
            EditorGUIUtility.PingObject(config);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox("把 Hierarchy 里的物体拖到下方，或选中物体后点击添加。自动修复开启时，播放/层级变化后会优先用 GlobalObjectId 追踪，再用唯一物体名/路径尾部找回并刷新记录。Alt+Shift+A 只在本窗口聚焦时生效。", MessageType.Info);
    }

    private void DrawAddArea()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("添加物体", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        manualObject = (GameObject)EditorGUILayout.ObjectField("Hierarchy物体", manualObject, typeof(GameObject), true);
        EditorGUI.BeginDisabledGroup(manualObject == null || EditorUtility.IsPersistent(manualObject));
        if (GUILayout.Button("添加", GUILayout.Width(60)))
        {
            AddObject(manualObject);
            manualObject = null;
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("添加选中", GUILayout.Height(24)))
            AddSelectionObjects();
        if (GUILayout.Button("清空列表", GUILayout.Height(24)))
        {
            if (EditorUtility.DisplayDialog(TOOL_NAME, "确定清空当前开关列表吗？", "清空", "取消"))
            {
                config.Entries.Clear();
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
        }
        EditorGUILayout.EndHorizontal();

        Rect dropRect = GUILayoutUtility.GetRect(0f, 54f, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, "拖拽 Hierarchy 物体到这里", EditorStyles.helpBox);
        HandleDragAndDrop(dropRect);
        EditorGUILayout.EndVertical();
    }

    private void DrawToggleButton()
    {
        bool allActive = AreAllResolvedObjectsActive();
        GUI.backgroundColor = allActive ? new Color(0.85f, 0.35f, 0.25f) : new Color(0.2f, 0.75f, 0.25f);
        if (GUILayout.Button(allActive ? "■ 关闭全部  (Alt+Shift+A)" : "▶ 开启全部  (Alt+Shift+A)", GUILayout.Height(42)))
            ToggleEntries(config, true);
        GUI.backgroundColor = Color.white;
    }

    private void DrawObjectList()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField($"物体列表：{config.Entries.Count}", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        for (int i = 0; i < config.Entries.Count; i++)
        {
            Moshi_FavActiveEntry entry = config.Entries[i];
            GameObject go = ResolveGameObject(entry);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            EditorGUI.BeginDisabledGroup(go == null);
            bool active = go != null && go.activeInHierarchy;
            bool newActive = GUILayout.Toggle(active, GUIContent.none, GUILayout.Width(22));
            if (go != null && newActive != active)
            {
                List<UnityEngine.Object> undoObjects = new List<UnityEngine.Object> { go };
                if (newActive)
                    CollectInactiveParents(new List<GameObject> { go }, undoObjects);
                Undo.RecordObjects(undoObjects.ToArray(), "切换物体开关");
                if (newActive)
                    EnsureParentsActive(go, true);
                go.SetActive(newActive);
                EditorSceneManager.MarkSceneDirty(go.scene);
            }
            EditorGUI.EndDisabledGroup();

            string label = go != null ? (string.IsNullOrEmpty(entry.displayName) ? go.name : entry.displayName) : "Missing: " + entry.gameObjectPath;
            EditorGUILayout.LabelField(label, go != null ? EditorStyles.label : EditorStyles.miniLabel);

            EditorGUI.BeginDisabledGroup(go == null);
            if (GUILayout.Button("选中", GUILayout.Width(45)))
            {
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
            }
            EditorGUI.EndDisabledGroup();

            if (go == null)
            {
                EditorGUI.BeginDisabledGroup(Selection.activeGameObject == null || EditorUtility.IsPersistent(Selection.activeGameObject));
                if (GUILayout.Button("修复", GUILayout.Width(45)))
                {
                    RepairEntryWithSelection(entry);
                    AssetDatabase.SaveAssets();
                    GUIUtility.ExitGUI();
                }
                EditorGUI.EndDisabledGroup();
            }

            if (GUILayout.Button("移除", GUILayout.Width(45)))
            {
                config.RemoveAt(i);
                AssetDatabase.SaveAssets();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void HandleDragAndDrop(Rect dropRect)
    {
        Event e = Event.current;
        if (!dropRect.Contains(e.mousePosition)) return;

        if (e.type == EventType.DragUpdated || e.type == EventType.DragPerform)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
                {
                    if (obj is GameObject go && !EditorUtility.IsPersistent(go))
                        AddObject(go);
                }
            }
            e.Use();
        }
    }

    private void AddSelectionObjects()
    {
        foreach (GameObject go in Selection.gameObjects)
            AddObject(go);
        AssetDatabase.SaveAssets();
    }

    private void RepairEntryWithSelection(Moshi_FavActiveEntry entry)
    {
        GameObject selected = Selection.activeGameObject;
        if (entry == null || selected == null || EditorUtility.IsPersistent(selected)) return;
        Undo.RecordObject(config, "修复收藏物体引用");
        RefreshEntryFromObject(entry, selected);
        EditorUtility.SetDirty(config);
    }

    private int AutoRepairEntries(bool saveAssets)
    {
        if (config == null || isAutoRepairing) return 0;
        isAutoRepairing = true;
        int repaired = 0;
        try
        {
            foreach (Moshi_FavActiveEntry entry in config.Entries)
            {
                if (entry == null) continue;
                string oldPath = entry.gameObjectPath;
                string oldId = entry.globalObjectId;
                int oldInstanceId = entry.runtimeInstanceId;
                GameObject go = ResolveGameObject(entry);
                if (go == null)
                    go = FindBestSelectionMatch(entry);
                if (go == null) continue;

                RefreshEntryFromObject(entry, go);
                if (!string.Equals(oldPath, entry.gameObjectPath, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(oldId, entry.globalObjectId, StringComparison.OrdinalIgnoreCase) ||
                    oldInstanceId != entry.runtimeInstanceId)
                {
                    repaired++;
                }
            }

            if (repaired > 0)
            {
                EditorUtility.SetDirty(config);
                if (saveAssets) AssetDatabase.SaveAssets();
            }
        }
        finally
        {
            isAutoRepairing = false;
        }
        return repaired;
    }

    private static GameObject FindBestSelectionMatch(Moshi_FavActiveEntry entry)
    {
        if (entry == null || Selection.gameObjects == null || Selection.gameObjects.Length == 0) return null;
        string fallbackName = GetFallbackName(entry);
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go == null || EditorUtility.IsPersistent(go)) continue;
            if (string.Equals(go.name, fallbackName, StringComparison.OrdinalIgnoreCase)) return go;
            string path = GetGameObjectPath(go);
            if (!string.IsNullOrEmpty(entry.gameObjectPath) && path.EndsWith("/" + entry.gameObjectPath, StringComparison.OrdinalIgnoreCase)) return go;
        }
        return null;
    }

    private void AddObject(GameObject go)
    {
        if (go == null || EditorUtility.IsPersistent(go)) return;
        config.Add(go);
        AssetDatabase.SaveAssets();
    }

    private bool AreAllResolvedObjectsActive()
    {
        bool hasObject = false;
        foreach (Moshi_FavActiveEntry entry in config.Entries)
        {
            GameObject go = ResolveGameObject(entry);
            if (go == null)
                go = FindBestSelectionMatch(entry);
            if (go == null) continue;
            hasObject = true;
            if (!go.activeInHierarchy) return false;
        }
        return hasObject;
    }

    private static void ToggleEntries(Moshi_FavActiveConfig cfg, bool markDirty)
    {
        if (cfg == null) return;
        bool hasObject = false;
        bool allActive = true;
        List<GameObject> targets = new List<GameObject>();
        foreach (Moshi_FavActiveEntry entry in cfg.Entries)
        {
            GameObject go = ResolveGameObject(entry);
            if (go == null)
                go = FindBestSelectionMatch(entry);
            if (go == null) continue;

            RefreshEntryFromObject(entry, go);
            if (!targets.Contains(go)) targets.Add(go);
            hasObject = true;
            if (!go.activeInHierarchy) allActive = false;
        }

        if (!hasObject) return;
        bool targetState = !allActive;
        List<UnityEngine.Object> undoObjects = new List<UnityEngine.Object>(targets);
        if (targetState)
            CollectInactiveParents(targets, undoObjects);

        Undo.RecordObjects(undoObjects.ToArray(), targetState ? "开启收藏物体" : "关闭收藏物体");
        foreach (GameObject go in targets)
        {
            if (targetState)
                EnsureParentsActive(go, markDirty);
            go.SetActive(targetState);
            if (markDirty)
                EditorSceneManager.MarkSceneDirty(go.scene);
        }
        EditorUtility.SetDirty(cfg);
    }

    private static void CollectInactiveParents(List<GameObject> targets, List<UnityEngine.Object> undoObjects)
    {
        foreach (GameObject go in targets)
        {
            Transform parent = go != null ? go.transform.parent : null;
            while (parent != null)
            {
                if (!parent.gameObject.activeSelf && !undoObjects.Contains(parent.gameObject))
                    undoObjects.Add(parent.gameObject);
                parent = parent.parent;
            }
        }
    }

    private static void EnsureParentsActive(GameObject go, bool markDirty)
    {
        Transform parent = go != null ? go.transform.parent : null;
        while (parent != null)
        {
            if (!parent.gameObject.activeSelf)
            {
                parent.gameObject.SetActive(true);
                if (markDirty)
                    EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
            }
            parent = parent.parent;
        }
    }
}
#endif
