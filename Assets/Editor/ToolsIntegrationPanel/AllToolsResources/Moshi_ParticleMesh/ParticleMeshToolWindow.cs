using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace ParticleMeshTools
{
    /// <summary>
    /// 粒子系统Mesh工具主窗口
    /// </summary>
    public class Moshi_ParticleMesh : EditorWindow
    {
        private const string TOOL_NAME = "粒子Mesh工具";
        private Dictionary<Mesh, MeshStatistics> meshStatistics = new Dictionary<Mesh, MeshStatistics>();
        private Dictionary<Material, MaterialStatistics> materialStatistics = new Dictionary<Material, MaterialStatistics>();
        private Vector2 statisticsScrollPos;
        private Vector2 replacementScrollPos;
        private Vector2 historyScrollPos;
        
        // 替换功能相关
        private Mesh selectedOriginalMesh;
        private Mesh selectedNewMesh;
        private Material selectedOriginalMaterial;
        private Material selectedNewMaterial;
        private List<Mesh> availableMeshes = new List<Mesh>();
        private List<Material> availableMaterials = new List<Material>();
        private string meshSearchTerm = "";
        private string materialSearchTerm = "";
        private bool showMeshSelector = false;
        private bool showMaterialSelector = false;
        private Dictionary<ParticleMeshReference, bool> replaceSelections = new Dictionary<ParticleMeshReference, bool>();
        private Dictionary<MaterialReference, bool> materialReplaceSelections = new Dictionary<MaterialReference, bool>();
        
        // Mesh分页加载相关
        private int meshPageIndex = 0;
        private const int MESH_PAGE_SIZE = 50;
        private int meshTotalCount = 0;
        private int meshTotalPages = 0;
        
        // 材质复制功能相关
        private string materialCopyTargetFolder = "Assets/"; // 目标文件夹路径
        private bool lockMaterialCopyFolder = false; // 锁定文件夹
        private string textureCopyTargetFolder = "Assets/"; // 贴图预览的目标文件夹
        private bool lockTextureCopyFolder = false; // 贴图预览的文件夹锁定
        
        // Mesh复制功能相关
        private string meshCopyTargetFolder = "Assets/"; // Mesh目标文件夹路径
        private bool lockMeshCopyFolder = false; // Mesh文件夹锁定
        
        // 界面状态
        private int selectedTab = 0;
        private readonly string[] tabNames = { "引用统计", "批量替换", "操作历史", "设置", "Mesh预览", "Material预览", "贴图预览" };
        
        // Mesh预览面板相关
        private Vector2 meshPreviewScrollPos;
        private List<MeshPreviewInfo> allMeshes = new List<MeshPreviewInfo>();
        private bool showMeshFilters = true;
        private bool showParticleSystems = true;
        private bool showParticleSystemDetails = true;
        private bool showOnlyExternalMeshes = false; // 只显示外部Mesh（带警告标记的）
        private bool needsRefreshMeshPreview = false; // 延迟刷新标记
        
        // Material预览面板相关
        private Vector2 materialPreviewScrollPos;
        private List<MaterialPreviewInfo> allMaterials = new List<MaterialPreviewInfo>();
        private bool showMaterialRenderers = true;
        private bool showMaterialTrails = true;
        private bool showOnlyExternalMaterials = false; // 只显示外部材质（带警告标记的）
        private bool needsRefreshMaterialPreview = false; // 延迟刷新标记
        
        // 贴图预览面板相关
        private Vector2 texturePreviewScrollPos;
        private List<TexturePreviewItem> allTextures = new List<TexturePreviewItem>();
        private Dictionary<Texture, List<TexturePreviewItem>> textureGroups = new Dictionary<Texture, List<TexturePreviewItem>>();
        private bool showMainTextures = true;
        private bool showNormalMaps = true;
        private bool showEmissionMaps = true;
        private bool showOtherTextures = true;
        private bool showMissingTextures = true;
        private bool showOnlyExternalResources = false; // 只显示外部资源（带警告标记的）
        private bool groupByTexture = true;
        private bool showTextureDetails = true;
        private string textureSearchFilter = "";
        private TextureSortMode textureSortMode = TextureSortMode.ByName;
        private static Texture _missingTextureKey; // 用于处理缺失贴图的特殊键值
        private bool needsRefreshTexturePreview = false; // 延迟刷新标记
        
        // 数据类型切换
        private bool showMaterialType = true;  // 显示Material类型数据
        private bool showTextureType = true;   // 显示贴图类型数据
        private bool lockAllData = false;  // 锁定所有数据
        private GameObject[] lockedSelectionObjects = Array.Empty<GameObject>();
        private string lockedSelectionSummary = string.Empty;
        
        // 设置
        private bool preserveProperties = true;
        private bool showDetailedInfo = true;
        private bool autoRefresh = true;
        
        // 引用统计面板的外部资源过滤
        private bool showOnlyExternalReferences = false; // 只显示外部资源的引用
        
        // 性能优化：缓存的统计数据（避免OnGUI中LINQ计算）
        private int _cachedMeshFilterCount = 0;
        private int _cachedParticleSystemCount = 0;
        private int _cachedMaterialRendererCount = 0;
        private int _cachedMaterialTrailCount = 0;
        private int _cachedMeshRendererCount = 0;
        private int _cachedSkinnedMeshRendererCount = 0;
        private int _cachedMissingTextureCount = 0;
        
        // 引用统计面板的缓存数据
        private int _cachedStatsMeshPSCount = 0;      // 引用统计中Mesh的粒子系统引用数
        private int _cachedStatsMeshMFCount = 0;      // 引用统计中Mesh的MeshFilter引用数
        private int _cachedStatsMatRendererCount = 0; // 引用统计中Material的渲染器引用数
        private int _cachedStatsMatTrailCount = 0;    // 引用统计中Material的拖尾引用数
        private List<KeyValuePair<Mesh, MeshStatistics>> _cachedSortedMeshStats = new List<KeyValuePair<Mesh, MeshStatistics>>();
        private List<KeyValuePair<Material, MaterialStatistics>> _cachedSortedMaterialStats = new List<KeyValuePair<Material, MaterialStatistics>>();
        
        /// <summary>
        /// 收集的组件数据（用于统一数据收集）
        /// </summary>
        private class CollectedComponentData
        {
            public List<ParticleSystem> particleSystems = new List<ParticleSystem>();
            public List<MeshFilter> meshFilters = new List<MeshFilter>();
            public List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
            public List<SkinnedMeshRenderer> skinnedMeshRenderers = new List<SkinnedMeshRenderer>();
            public Dictionary<GameObject, string> hierarchyPaths = new Dictionary<GameObject, string>();
        }
        
        [MenuItem("工具/Moshi/" + TOOL_NAME)]
        public static void ShowWindow()
        {
            var window = GetWindow<Moshi_ParticleMesh>(TOOL_NAME);
            window.minSize = new Vector2(600, 400);
            window.Show();
        }
        
        private void OnEnable()
        {
            // 从EditorPrefs加载开关状态
            showMeshFilters = EditorPrefs.GetBool("ParticleMeshTool_ShowMeshFilters", true);
            showParticleSystems = EditorPrefs.GetBool("ParticleMeshTool_ShowParticleSystems", true);
            showMaterialRenderers = EditorPrefs.GetBool("ParticleMeshTool_ShowMaterialRenderers", true);
            showMaterialTrails = EditorPrefs.GetBool("ParticleMeshTool_ShowMaterialTrails", true);
            showMaterialType = EditorPrefs.GetBool("ParticleMeshTool_ShowMaterialType", true);
            showTextureType = EditorPrefs.GetBool("ParticleMeshTool_ShowTextureType", true);
            lockAllData = EditorPrefs.GetBool("ParticleMeshTool_LockAllData", false);
            autoRefresh = EditorPrefs.GetBool("ParticleMeshTool_AutoRefresh", true);
            
            // 统一刷新所有数据（单次遍历）
            RefreshAllDataUnified();
            Selection.selectionChanged += OnSelectionChanged;
        }
        
        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }
        
        private void OnSelectionChanged()
        {
            if (autoRefresh && !lockAllData)
            {
                RefreshAllDataUnified();
            }
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space();
            
            // 标题
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("粒子系统Mesh引用工具", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            
            // 工具栏
            DrawToolbar();
            EditorGUILayout.Space();
            
            // 标签页 - 检测标签页切换
            int previousTab = selectedTab;
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);
            
            // 如果切换到引用统计面板，自动刷新数据
            if (selectedTab != previousTab && selectedTab == 0)
            {
                RefreshAllDataUnified();
            }
            
            EditorGUILayout.Space();
            
            switch (selectedTab)
            {
                case 0:
                    DrawStatisticsTab();
                    break;
                case 1:
                    DrawReplacementTab();
                    break;
                case 2:
                    DrawHistoryTab();
                    break;
                case 3:
                    DrawSettingsTab();
                    break;
                case 4:
                    DrawMeshPreviewTab();
                    break;
                case 5:
                    DrawMaterialPreviewTab();
                    break;
                case 6:
                    DrawTexturePreviewTab();
                    break;
            }
        }
        
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            
            var selectedObjects = lockAllData && lockedSelectionObjects.Length > 0
                ? lockedSelectionObjects
                : Selection.gameObjects;
            var hasSelection = selectedObjects != null && selectedObjects.Length > 0;
            
            EditorGUI.BeginDisabledGroup(!hasSelection);
            if (GUILayout.Button("刷新统计", GUILayout.Width(80)))
            {
                RefreshAllDataUnified();
            }
            EditorGUI.EndDisabledGroup();
            
            if (GUILayout.Button("刷新Mesh列表", GUILayout.Width(100)))
            {
                RefreshAvailableMeshes();
            }
            
            // 添加分隔符
            GUILayout.Space(10);
            
            // 粒子系统开关按钮
            var originalBgColor = GUI.backgroundColor;
            GUI.backgroundColor = showParticleSystems ? Color.green : Color.gray;
            var newShowParticleSystems = GUILayout.Toggle(showParticleSystems, "粒子系统", "Button", GUILayout.Width(80));
            GUI.backgroundColor = originalBgColor;
            
            // MeshFilter开关按钮
            GUI.backgroundColor = showMeshFilters ? Color.cyan : Color.gray;
            var newShowMeshFilters = GUILayout.Toggle(showMeshFilters, "MeshFilter", "Button", GUILayout.Width(80));
            GUI.backgroundColor = originalBgColor;
            
            // Material类型开关按钮
            GUI.backgroundColor = showMaterialType ? Color.magenta : Color.gray;
            var newShowMaterialType = GUILayout.Toggle(showMaterialType, "Material类型", "Button", GUILayout.Width(90));
            GUI.backgroundColor = originalBgColor;
            
            // 贴图类型开关按钮
            GUI.backgroundColor = showTextureType ? new Color(1f, 0.5f, 0f) : Color.gray; // 橙色
            var newShowTextureType = GUILayout.Toggle(showTextureType, "贴图类型", "Button", GUILayout.Width(80));
            GUI.backgroundColor = originalBgColor;
            
            // 自动刷新开关按钮
            GUI.backgroundColor = autoRefresh ? Color.yellow : Color.gray;
            var newAutoRefresh = GUILayout.Toggle(autoRefresh, "自动刷新", "Button", GUILayout.Width(80));
            GUI.backgroundColor = originalBgColor;
            
            // 检查是否有变化，如果有则刷新数据
            if (newShowParticleSystems != showParticleSystems || 
                newShowMeshFilters != showMeshFilters ||
                newShowMaterialType != showMaterialType ||
                newShowTextureType != showTextureType)
            {
                showParticleSystems = newShowParticleSystems;
                showMeshFilters = newShowMeshFilters;
                showMaterialType = newShowMaterialType;
                showTextureType = newShowTextureType;
                
                // 保存到EditorPrefs
                EditorPrefs.SetBool("ParticleMeshTool_ShowParticleSystems", showParticleSystems);
                EditorPrefs.SetBool("ParticleMeshTool_ShowMeshFilters", showMeshFilters);
                EditorPrefs.SetBool("ParticleMeshTool_ShowMaterialType", showMaterialType);
                EditorPrefs.SetBool("ParticleMeshTool_ShowTextureType", showTextureType);
                
                RefreshAll();
            }
            
            // 更新自动刷新状态
            if (newAutoRefresh != autoRefresh)
            {
                autoRefresh = newAutoRefresh;
                EditorPrefs.SetBool("ParticleMeshTool_AutoRefresh", autoRefresh);
            }
            
            GUILayout.FlexibleSpace();
            
            // 锁定开关按钮
            GUI.backgroundColor = lockAllData ? new Color(1f, 0.3f, 0.3f) : Color.gray; // 红色表示锁定
            var newLockAllData = GUILayout.Toggle(lockAllData, "锁定", "Button", GUILayout.Width(60));
            GUI.backgroundColor = originalBgColor;
            
            if (newLockAllData != lockAllData)
            {
                lockAllData = newLockAllData;
                EditorPrefs.SetBool("ParticleMeshTool_LockAllData", lockAllData);

                if (lockAllData)
                {
                    lockedSelectionObjects = Selection.gameObjects != null
                        ? Selection.gameObjects.ToArray()
                        : Array.Empty<GameObject>();
                    lockedSelectionSummary = lockedSelectionObjects.Length > 0
                        ? string.Join(", ", lockedSelectionObjects.Take(3).Select(obj => obj.name)) + (lockedSelectionObjects.Length > 3 ? " ..." : string.Empty)
                        : string.Empty;
                    
                    // 锁定时立即刷新所有数据，保存当前状态
                    RefreshAllDataUnified();
                }
                else
                {
                    lockedSelectionObjects = Array.Empty<GameObject>();
                    lockedSelectionSummary = string.Empty;
                }
            }
            
            if (GUILayout.Button("清空历史", GUILayout.Width(80)))
            {
                ParticleMeshReplacer.ClearOperationHistory();
            }
            
            if (GUILayout.Button("清空选择", GUILayout.Width(80)))
            {
                replaceSelections.Clear();
                materialReplaceSelections.Clear();
                Debug.Log("已清空所有替换选择状态");
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 显示选中对象的提示信息
            if (!hasSelection)
            {
                EditorGUILayout.HelpBox("请在Hierarchy面板中选择要操作的GameObject或Prefab", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"当前选中: {selectedObjects.Length} 个对象", EditorStyles.miniLabel);
            }
        }
        
        private void DrawStatisticsTab()
        {
            // 显示锁定状态
            if (lockAllData)
            {
                EditorGUILayout.HelpBox("数据已锁定。当前显示的是锁定时的数据，不会随选中对象变化而更新。点击工具栏中的\"锁定\"按钮可解除锁定。", MessageType.Info);
                Color previousColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.6f, 0f);
                if (GUILayout.Button("强制刷新", GUILayout.Width(120)))
                {
                    ForceRefreshAll();
                    RefreshAllDataUnified();
                }
                GUI.backgroundColor = previousColor;
            }
            
            // 显示当前选中的对象信息
            var activeSelection = lockAllData && lockedSelectionObjects.Length > 0
                ? lockedSelectionObjects
                : Selection.gameObjects;
            if (activeSelection == null || activeSelection.Length == 0)
            {
                if (!lockAllData)
                {
                    EditorGUILayout.HelpBox("请在Hierarchy面板中选择要分析的GameObject或Prefab", MessageType.Warning);
                    return;
                }
                else
                {
                    EditorGUILayout.LabelField("数据已锁定 - 当前无选中对象", EditorStyles.helpBox);
                }
            }
            else
            {
                var summary = lockAllData && !string.IsNullOrEmpty(lockedSelectionSummary)
                    ? lockedSelectionSummary
                    : string.Join(", ", activeSelection.Take(3).Select(obj => obj.name)) + (activeSelection.Length > 3 ? " ..." : "");
                EditorGUILayout.LabelField($"分析对象: {summary}", EditorStyles.helpBox);
            }
            
            // 显示过滤状态
            var filterStatus = $"过滤状态: MeshFilter({(showMeshFilters ? "启用" : "禁用")}), 粒子系统({(showParticleSystems ? "启用" : "禁用")}), Material类型({(showMaterialType ? "启用" : "禁用")})";
            EditorGUILayout.LabelField(filterStatus, EditorStyles.miniLabel);
            
            // Mesh统计信息
            EditorGUILayout.LabelField($"检测到 {meshStatistics.Count} 个不同的Mesh被引用", EditorStyles.helpBox);
            
            // Material统计信息
            if (showMaterialType)
            {
                EditorGUILayout.LabelField($"检测到 {materialStatistics.Count} 个不同的Material被引用", EditorStyles.helpBox);
                
                // 带颜色的Material统计信息（使用缓存数据）
                EditorGUILayout.BeginHorizontal();
                
                var originalColor = GUI.color;
                var originalBgColor = GUI.backgroundColor;
                
                // Material渲染器统计（紫色）
                GUI.backgroundColor = new Color(1f, 0.7f, 1f);
                GUI.color = new Color(0.6f, 0.2f, 0.6f);
                EditorGUILayout.LabelField($"Material渲染器: {_cachedStatsMatRendererCount}", EditorStyles.helpBox, GUILayout.Width(120));
                
                // 拖尾Material统计（橙色）
                GUI.backgroundColor = new Color(1f, 0.9f, 0.7f);
                GUI.color = new Color(0.8f, 0.5f, 0.2f);
                EditorGUILayout.LabelField($"拖尾Material: {_cachedStatsMatTrailCount}", EditorStyles.helpBox, GUILayout.Width(120));
                
                GUI.color = originalColor;
                GUI.backgroundColor = originalBgColor;
                
                EditorGUILayout.EndHorizontal();
            }
            
            // 带颜色的Mesh统计信息（使用缓存数据）
            EditorGUILayout.BeginHorizontal();
            
            var originalColor2 = GUI.color;
            var originalBgColor2 = GUI.backgroundColor;
            
            // 粒子系统统计（绿色）
            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
            GUI.color = new Color(0.2f, 0.6f, 0.2f);
            EditorGUILayout.LabelField($"粒子系统: {_cachedStatsMeshPSCount}", EditorStyles.helpBox, GUILayout.Width(100));
            
            // MeshFilter统计（蓝色）
            GUI.backgroundColor = new Color(0.7f, 0.8f, 1f);
            GUI.color = new Color(0.2f, 0.3f, 0.8f);
            EditorGUILayout.LabelField($"MeshFilter: {_cachedStatsMeshMFCount}", EditorStyles.helpBox, GUILayout.Width(100));
            
            GUI.color = originalColor2;
            GUI.backgroundColor = originalBgColor2;
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            
            // 外部资源过滤选项
            EditorGUILayout.BeginHorizontal();
            
            var filterColor = GUI.color;
            GUI.color = showOnlyExternalReferences ? new Color(1f, 0.5f, 0.5f) : Color.white;
            showOnlyExternalReferences = EditorGUILayout.Toggle("⚠ 只显示外部资源", showOnlyExternalReferences, GUILayout.Width(150));
            GUI.color = filterColor;
            
            EditorGUILayout.EndHorizontal();
            
            if (showOnlyExternalReferences)
            {
                EditorGUILayout.HelpBox("已启用：只显示路径在预制体平级目录外的Mesh和Material", MessageType.Warning);
            }
            
            EditorGUILayout.Space();
            
            if (meshStatistics.Count == 0 && (!showMaterialType || materialStatistics.Count == 0))
            {
                EditorGUILayout.HelpBox("选中的对象中没有找到使用Mesh或Material的组件", MessageType.Info);
                return;
            }
            
            statisticsScrollPos = EditorGUILayout.BeginScrollView(statisticsScrollPos);
            
            // 显示Mesh统计（使用缓存的排序列表）
            foreach (var kvp in _cachedSortedMeshStats)
            {
                DrawMeshStatistics(kvp.Key, kvp.Value);
            }
            
            // 显示Material统计（使用缓存的排序列表）
            if (showMaterialType)
            {
                foreach (var kvp in _cachedSortedMaterialStats)
                {
                    DrawMaterialStatistics(kvp.Key, kvp.Value);
                }
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawMeshStatistics(Mesh mesh, MeshStatistics stats)
        {
            // 应用外部资源过滤
            if (showOnlyExternalReferences)
            {
                string meshPath = mesh != null ? AssetDatabase.GetAssetPath(mesh) : "";
                if (!IsResourceOutsidePrefabDirectory(meshPath))
                {
                    return; // 不是外部资源，不显示
                }
            }
            
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.BeginHorizontal();
            
            // Mesh信息（可编辑）
            var newMesh = (Mesh)EditorGUILayout.ObjectField("", mesh, typeof(Mesh), false, GUILayout.Width(200));
            
            // 检查Mesh是否被替换
            if (newMesh != mesh)
            {
                ReplaceMeshInReferences(mesh, newMesh, stats);
            }
            
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField($"引用次数: {stats.referenceCount}", EditorStyles.boldLabel);
            if (showDetailedInfo)
            {
                string currentMeshPath = mesh != null ? AssetDatabase.GetAssetPath(mesh) : "未知";
                
                // 检查资源是否在预制体目录外
                bool isOutsidePrefabDirectory = IsResourceOutsidePrefabDirectory(currentMeshPath);
                
                if (isOutsidePrefabDirectory)
                {
                    var originalColor = GUI.color;
                    GUI.color = Color.red;
                    var style = new GUIStyle(EditorStyles.miniLabel);
                    style.normal.textColor = Color.red;
                    EditorGUILayout.LabelField($"⚠ 路径: {currentMeshPath}", style);
                    GUI.color = originalColor;
                }
                else
                {
                    EditorGUILayout.LabelField($"路径: {currentMeshPath}");
                }
                
                EditorGUILayout.LabelField(ParticleMeshAnalyzer.GetMeshInfo(mesh));
            }
            EditorGUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("选择替换", GUILayout.Width(80)))
            {
                selectedOriginalMesh = mesh;
                selectedTab = 1;
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 引用详情
            if (showDetailedInfo)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("引用详情:");
                foreach (var reference in stats.references)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    if (reference.referenceType == MeshReferenceType.ParticleSystem)
                    {
                        EditorGUILayout.ObjectField("", reference.particleSystem, typeof(ParticleSystem), true, GUILayout.Width(200));
                    }
                    else
                    {
                        EditorGUILayout.ObjectField("", reference.meshFilter, typeof(MeshFilter), true, GUILayout.Width(200));
                    }
                    
                    // 为不同类型使用不同颜色
                    var originalColor = GUI.color;
                    if (reference.referenceType == MeshReferenceType.ParticleSystem)
                    {
                        GUI.color = new Color(0.8f, 1f, 0.8f); // 淡绿色
                    }
                    else
                    {
                        GUI.color = new Color(0.8f, 0.9f, 1f); // 淡蓝色
                    }
                    
                    EditorGUILayout.LabelField($"[{reference.GetReferenceTypeName()}] {reference.objectPath}");
                    GUI.color = originalColor;
                    
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawReplacementTab()
        {
            EditorGUILayout.LabelField("批量替换", EditorStyles.boldLabel);
            
            // 显示锁定状态
            if (lockAllData)
            {
                EditorGUILayout.HelpBox("数据已锁定。当前显示的是锁定时的数据，不会随选中对象变化而更新。点击工具栏中的\"锁定\"按钮可解除锁定。", MessageType.Info);
            }
            
            // 检查是否有选中对象
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                if (!lockAllData)
                {
                    EditorGUILayout.HelpBox("请在Hierarchy面板中选择要操作的GameObject或Prefab", MessageType.Warning);
                    return;
                }
                else
                {
                    EditorGUILayout.LabelField("数据已锁定 - 当前无选中对象", EditorStyles.helpBox);
                }
            }
            
            // 替换类型选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("替换类型:", GUILayout.Width(80));
            
            var replaceMesh = GUILayout.Toggle(selectedOriginalMesh != null || selectedOriginalMaterial == null, "Mesh", "Button", GUILayout.Width(60));
            var replaceMaterial = GUILayout.Toggle(selectedOriginalMaterial != null || selectedOriginalMesh == null, "Material", "Button", GUILayout.Width(80));
            
            if (replaceMesh && selectedOriginalMaterial != null)
            {
                selectedOriginalMaterial = null;
                selectedNewMaterial = null;
            }
            if (replaceMaterial && selectedOriginalMesh != null)
            {
                selectedOriginalMesh = null;
                selectedNewMesh = null;
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            
            // Mesh替换界面
            if (replaceMesh)
            {
                DrawMeshReplacementUI();
            }
            
            // Material替换界面
            if (replaceMaterial && showMaterialType)
            {
                DrawMaterialReplacementUI();
            }
        }
        
        private void DrawMeshReplacementUI()
        {
            EditorGUILayout.LabelField("Mesh替换", EditorStyles.boldLabel);
            
            // 原始Mesh选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("原始Mesh:", GUILayout.Width(80));
            var newSelectedOriginalMesh = (Mesh)EditorGUILayout.ObjectField(selectedOriginalMesh, typeof(Mesh), false);
            
            // 检查是否更换了原始Mesh，如果是则清空选择状态
            if (newSelectedOriginalMesh != selectedOriginalMesh)
            {
                selectedOriginalMesh = newSelectedOriginalMesh;
                replaceSelections.Clear(); // 清空之前的选择状态
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 新Mesh选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("新Mesh:", GUILayout.Width(80));
            selectedNewMesh = (Mesh)EditorGUILayout.ObjectField(selectedNewMesh, typeof(Mesh), false);
            
            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                showMeshSelector = !showMeshSelector;
            }
            EditorGUILayout.EndHorizontal();
            
            // Mesh选择器
            if (showMeshSelector)
            {
                DrawMeshSelector();
            }
            
            EditorGUILayout.Space();
            
            // Mesh复制功能
            EditorGUILayout.LabelField("Mesh复制", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            
            // 目标文件夹选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("目标文件夹:", GUILayout.Width(80));
            
            EditorGUI.BeginDisabledGroup(lockMeshCopyFolder);
            meshCopyTargetFolder = EditorGUILayout.TextField(meshCopyTargetFolder);
            EditorGUI.EndDisabledGroup();
            
            // 从Project选择文件夹按钮
            if (GUILayout.Button("从Project选择", GUILayout.Width(100)))
            {
                if (!lockMeshCopyFolder)
                {
                    var selectedFolder = GetSelectedFolderFromProject();
                    if (!string.IsNullOrEmpty(selectedFolder))
                    {
                        meshCopyTargetFolder = selectedFolder;
                    }
                }
            }
            
            // 锁定按钮
            Color originalBgColor = GUI.backgroundColor;
            GUI.backgroundColor = lockMeshCopyFolder ? new Color(1f, 0.3f, 0.3f) : Color.gray;
            if (GUILayout.Button(lockMeshCopyFolder ? "🔒" : "🔓", GUILayout.Width(30)))
            {
                lockMeshCopyFolder = !lockMeshCopyFolder;
            }
            GUI.backgroundColor = originalBgColor;
            
            EditorGUILayout.EndHorizontal();
            
            // 复制按钮
            EditorGUI.BeginDisabledGroup(selectedOriginalMesh == null || string.IsNullOrEmpty(meshCopyTargetFolder));
            if (GUILayout.Button("复制Mesh到指定文件夹", GUILayout.Height(25)))
            {
                CopyMeshToFolder(selectedOriginalMesh, meshCopyTargetFolder);
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // 替换选项
            preserveProperties = EditorGUILayout.Toggle("保持原有属性", preserveProperties);
            
            EditorGUILayout.Space();
            
            // 替换目标选择
            if (selectedOriginalMesh != null && meshStatistics.ContainsKey(selectedOriginalMesh))
            {
                DrawMeshReplacementTargets();
            }
            
            EditorGUILayout.Space();
            
            // 替换按钮
            EditorGUI.BeginDisabledGroup(selectedOriginalMesh == null || selectedNewMesh == null);
            if (GUILayout.Button("执行Mesh替换", GUILayout.Height(30)))
            {
                ExecuteMeshReplacement();
            }
            EditorGUI.EndDisabledGroup();
            
            // 预览信息
            if (selectedOriginalMesh != null && selectedNewMesh != null && meshStatistics.ContainsKey(selectedOriginalMesh))
            {
                var selectedReferences = GetSelectedReferences();
                if (selectedReferences.Count > 0)
                {
                    var preview = ParticleMeshReplacer.PreviewReplacement(selectedOriginalMesh, selectedNewMesh, selectedReferences);
                    EditorGUILayout.HelpBox(preview, MessageType.Info);
                }
            }
        }
        
        private void DrawMaterialReplacementUI()
        {
            EditorGUILayout.LabelField("Material替换", EditorStyles.boldLabel);
            
            // 原始Material选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("原始Material:", GUILayout.Width(100));
            var newSelectedOriginalMaterial = (Material)EditorGUILayout.ObjectField(selectedOriginalMaterial, typeof(Material), false);
            
            // 检查是否更换了原始Material，如果是则清空选择状态
            if (newSelectedOriginalMaterial != selectedOriginalMaterial)
            {
                selectedOriginalMaterial = newSelectedOriginalMaterial;
                materialReplaceSelections.Clear(); // 清空之前的选择状态
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 新Material选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("新Material:", GUILayout.Width(100));
            selectedNewMaterial = (Material)EditorGUILayout.ObjectField(selectedNewMaterial, typeof(Material), false);
            
            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                showMaterialSelector = !showMaterialSelector;
            }
            EditorGUILayout.EndHorizontal();
            
            // Material选择器
            if (showMaterialSelector)
            {
                DrawMaterialSelector();
            }
            
            EditorGUILayout.Space();
            
            // 材质复制功能
            EditorGUILayout.LabelField("材质复制", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            
            // 目标文件夹选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("目标文件夹:", GUILayout.Width(100));
            
            EditorGUI.BeginDisabledGroup(lockMaterialCopyFolder);
            materialCopyTargetFolder = EditorGUILayout.TextField(materialCopyTargetFolder);
            EditorGUI.EndDisabledGroup();
            
            // 从Project选择文件夹按钮
            if (GUILayout.Button("从Project选择", GUILayout.Width(100)))
            {
                if (!lockMaterialCopyFolder)
                {
                    var selectedFolder = GetSelectedFolderFromProject();
                    if (!string.IsNullOrEmpty(selectedFolder))
                    {
                        materialCopyTargetFolder = selectedFolder;
                    }
                }
            }
            
            // 锁定按钮
            Color originalBgColor = GUI.backgroundColor;
            GUI.backgroundColor = lockMaterialCopyFolder ? new Color(1f, 0.3f, 0.3f) : Color.gray;
            if (GUILayout.Button(lockMaterialCopyFolder ? "🔒" : "🔓", GUILayout.Width(30)))
            {
                lockMaterialCopyFolder = !lockMaterialCopyFolder;
            }
            GUI.backgroundColor = originalBgColor;
            
            EditorGUILayout.EndHorizontal();
            
            // 复制按钮
            EditorGUI.BeginDisabledGroup(selectedOriginalMaterial == null || string.IsNullOrEmpty(materialCopyTargetFolder));
            if (GUILayout.Button("复制材质到指定文件夹", GUILayout.Height(25)))
            {
                CopyMaterialToFolder(selectedOriginalMaterial, materialCopyTargetFolder);
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // 替换选项
            preserveProperties = EditorGUILayout.Toggle("保持原有属性", preserveProperties);
            
            EditorGUILayout.Space();
            
            // 替换目标选择
            if (selectedOriginalMaterial != null && materialStatistics.ContainsKey(selectedOriginalMaterial))
            {
                DrawMaterialReplacementTargets();
            }
            
            EditorGUILayout.Space();
            
            // 替换按钮
            EditorGUI.BeginDisabledGroup(selectedOriginalMaterial == null || selectedNewMaterial == null);
            if (GUILayout.Button("执行Material替换", GUILayout.Height(30)))
            {
                ExecuteMaterialReplacement();
            }
            EditorGUI.EndDisabledGroup();
            
            // 预览信息
            if (selectedOriginalMaterial != null && selectedNewMaterial != null && materialStatistics.ContainsKey(selectedOriginalMaterial))
            {
                var selectedReferences = GetSelectedMaterialReferences();
                var totalReferences = materialStatistics[selectedOriginalMaterial].references.Count;
                
                if (selectedReferences.Count > 0)
                {
                    EditorGUILayout.HelpBox($"将替换 {selectedReferences.Count} 个Material引用 (共 {totalReferences} 个可用)", MessageType.Info);
                }
                else if (totalReferences > 0)
                {
                    EditorGUILayout.HelpBox($"请选择要替换的目标 (共 {totalReferences} 个可用)", MessageType.Warning);
                }
                
                // 调试信息：显示选择字典的状态
                if (materialReplaceSelections.Count > 0)
                {
                    var debugInfo = $"选择字典中有 {materialReplaceSelections.Count} 个条目";
                    EditorGUILayout.LabelField(debugInfo, EditorStyles.miniLabel);
                }
            }
        }
        
        private void DrawMeshSelector()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Mesh选择器", EditorStyles.boldLabel);
            
            // 搜索框
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("搜索:", GUILayout.Width(50));
            var newSearchTerm = EditorGUILayout.TextField(meshSearchTerm);
            if (newSearchTerm != meshSearchTerm)
            {
                meshSearchTerm = newSearchTerm;
                meshPageIndex = 0; // 搜索时重置到第一页
                RefreshAvailableMeshes();
            }
            EditorGUILayout.EndHorizontal();
            
            // 分页信息和控件
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"共 {meshTotalCount} 个Mesh，第 {meshPageIndex + 1}/{Mathf.Max(1, meshTotalPages)} 页", EditorStyles.miniLabel);
            
            GUILayout.FlexibleSpace();
            
            EditorGUI.BeginDisabledGroup(meshPageIndex <= 0);
            if (GUILayout.Button("上一页", GUILayout.Width(60)))
            {
                meshPageIndex--;
                RefreshAvailableMeshes();
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.BeginDisabledGroup(meshPageIndex >= meshTotalPages - 1);
            if (GUILayout.Button("下一页", GUILayout.Width(60)))
            {
                meshPageIndex++;
                RefreshAvailableMeshes();
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();
            
            // Mesh列表（已经是当前页的数据，无需再过滤）
            replacementScrollPos = EditorGUILayout.BeginScrollView(replacementScrollPos, GUILayout.Height(200));
            
            foreach (var mesh in availableMeshes)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(mesh.name, EditorStyles.label))
                {
                    selectedNewMesh = mesh;
                    showMeshSelector = false;
                }
                EditorGUILayout.LabelField(ParticleMeshAnalyzer.GetMeshInfo(mesh));
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        
        private void DrawMeshReplacementTargets()
        {
            var stats = meshStatistics[selectedOriginalMesh];
            
            EditorGUILayout.LabelField($"替换目标 (共{stats.references.Count}个):", EditorStyles.boldLabel);
            
            // 颜色图例
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("颜色图例:", EditorStyles.miniLabel, GUILayout.Width(60));
            
            var originalColor = GUI.color;
            var originalBgColor = GUI.backgroundColor;
            
            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
            GUI.color = new Color(0.2f, 0.6f, 0.2f);
            EditorGUILayout.LabelField("[粒子系统]", EditorStyles.helpBox, GUILayout.Width(80));
            
            GUI.backgroundColor = new Color(0.7f, 0.8f, 1f);
            GUI.color = new Color(0.2f, 0.3f, 0.8f);
            EditorGUILayout.LabelField("[MeshFilter]", EditorStyles.helpBox, GUILayout.Width(80));
            
            GUI.color = originalColor;
            GUI.backgroundColor = originalBgColor;
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全选"))
            {
                foreach (var reference in stats.references)
                {
                    replaceSelections[reference] = true;
                }
            }
            if (GUILayout.Button("反选"))
            {
                foreach (var reference in stats.references)
                {
                    var currentValue = replaceSelections.ContainsKey(reference) ? replaceSelections[reference] : false;
                    replaceSelections[reference] = !currentValue;
                }
            }
            if (GUILayout.Button("清空"))
            {
                replaceSelections.Clear();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            foreach (var reference in stats.references)
            {
                EditorGUILayout.BeginHorizontal();
                
                var isSelected = replaceSelections.ContainsKey(reference) ? replaceSelections[reference] : false;
                var newSelection = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
                replaceSelections[reference] = newSelection;
                
                if (reference.referenceType == MeshReferenceType.ParticleSystem)
                {
                    EditorGUILayout.ObjectField("", reference.particleSystem, typeof(ParticleSystem), true, GUILayout.Width(150));
                }
                else
                {
                    EditorGUILayout.ObjectField("", reference.meshFilter, typeof(MeshFilter), true, GUILayout.Width(150));
                }
                
                // 为不同类型使用不同颜色的标签
                var labelOriginalColor = GUI.color;
                var labelOriginalBgColor = GUI.backgroundColor;
                
                if (reference.referenceType == MeshReferenceType.ParticleSystem)
                {
                    GUI.backgroundColor = new Color(0.7f, 1f, 0.7f); // 淡绿色背景
                    GUI.color = new Color(0.2f, 0.6f, 0.2f); // 深绿色文字
                }
                else
                {
                    GUI.backgroundColor = new Color(0.7f, 0.8f, 1f); // 淡蓝色背景
                    GUI.color = new Color(0.2f, 0.3f, 0.8f); // 深蓝色文字
                }
                
                EditorGUILayout.LabelField($"[{reference.GetReferenceTypeName()}]", EditorStyles.helpBox, GUILayout.Width(80));
                
                GUI.color = labelOriginalColor;
                GUI.backgroundColor = labelOriginalBgColor;
                
                EditorGUILayout.LabelField(reference.objectPath);
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void DrawHistoryTab()
        {
            var history = ParticleMeshReplacer.GetOperationHistory();
            
            EditorGUILayout.LabelField($"操作历史 (共{history.Count}条)", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            if (history.Count == 0)
            {
                EditorGUILayout.HelpBox("暂无操作历史", MessageType.Info);
                return;
            }
            
            historyScrollPos = EditorGUILayout.BeginScrollView(historyScrollPos);
            
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var operation = history[i];
                DrawOperationHistory(operation, i);
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space();
            if (GUILayout.Button("撤销最后操作") && history.Count > 0)
            {
                ParticleMeshReplacer.UndoLastOperation();
                RefreshAllDataUnified();
            }
        }
        
        private void DrawOperationHistory(ReplaceOperation operation, int index)
        {
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.LabelField($"操作 {index + 1} - {operation.timestamp:HH:mm:ss}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"替换: {operation.originalMesh?.name} → {operation.newMesh?.name}");
            EditorGUILayout.LabelField($"影响的粒子系统: {operation.affectedReferences.Count}个");
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSettingsTab()
        {
            EditorGUILayout.LabelField("设置", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // 基本设置
            EditorGUILayout.LabelField("基本设置", EditorStyles.boldLabel);
            preserveProperties = EditorGUILayout.Toggle("默认保持原有属性", preserveProperties);
            showDetailedInfo = EditorGUILayout.Toggle("显示详细信息", showDetailedInfo);
            autoRefresh = EditorGUILayout.Toggle("自动刷新", autoRefresh);
            
            EditorGUILayout.Space();
            
            // 组件过滤设置
            EditorGUILayout.LabelField("组件过滤设置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("这些设置会影响整个工具的所有功能模块", MessageType.Info);
            
            var newShowMeshFilters = EditorGUILayout.Toggle("启用MeshFilter组件", showMeshFilters);
            var newShowParticleSystems = EditorGUILayout.Toggle("启用粒子系统组件", showParticleSystems);
            var newShowDetails = EditorGUILayout.Toggle("显示粒子系统详细信息", showParticleSystemDetails);
            
            EditorGUILayout.Space();
            
            // 数据类型检测设置
            EditorGUILayout.LabelField("数据类型检测设置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("控制检测和显示的数据类型", MessageType.Info);
            
            var newShowMaterialType = EditorGUILayout.Toggle("Material参数检测", showMaterialType);
            var newShowTextureType = EditorGUILayout.Toggle("贴图数据类型检测", showTextureType);
            
            // 检查是否有变化，如果有则刷新所有数据
            if (newShowMeshFilters != showMeshFilters || 
                newShowParticleSystems != showParticleSystems || 
                newShowDetails != showParticleSystemDetails ||
                newShowMaterialType != showMaterialType ||
                newShowTextureType != showTextureType)
            {
                showMeshFilters = newShowMeshFilters;
                showParticleSystems = newShowParticleSystems;
                showParticleSystemDetails = newShowDetails;
                showMaterialType = newShowMaterialType;
                showTextureType = newShowTextureType;
                
                // 保存到EditorPrefs
                EditorPrefs.SetBool("ParticleMeshTool_ShowMeshFilters", showMeshFilters);
                EditorPrefs.SetBool("ParticleMeshTool_ShowParticleSystems", showParticleSystems);
                EditorPrefs.SetBool("ParticleMeshTool_ShowMaterialType", showMaterialType);
                EditorPrefs.SetBool("ParticleMeshTool_ShowTextureType", showTextureType);
                
                // 刷新所有相关数据
                RefreshAll();
            }
            
            EditorGUILayout.Space();
            
            // 重置按钮
            if (GUILayout.Button("重置所有设置"))
            {
                preserveProperties = true;
                showDetailedInfo = true;
                autoRefresh = true;
                showMeshFilters = true;
                showParticleSystems = true;
                showParticleSystemDetails = true;
                showMaterialType = true;
                
                // 保存到EditorPrefs
                EditorPrefs.SetBool("ParticleMeshTool_ShowMeshFilters", showMeshFilters);
                EditorPrefs.SetBool("ParticleMeshTool_ShowParticleSystems", showParticleSystems);
                EditorPrefs.SetBool("ParticleMeshTool_ShowMaterialType", showMaterialType);
                EditorPrefs.SetBool("ParticleMeshTool_AutoRefresh", autoRefresh);
                
                // 刷新所有数据
                RefreshAll();
            }
        }
        
        private void RefreshStatistics()
        {
            // 根据锁定状态使用正确的对象
            var selectedObjects = lockAllData && lockedSelectionObjects.Length > 0
                ? lockedSelectionObjects
                : Selection.gameObjects;
            
            // 如果没有选中对象且没有锁定对象，则清空统计
            if ((selectedObjects == null || selectedObjects.Length == 0) && !lockAllData)
            {
                meshStatistics.Clear();
                materialStatistics.Clear();
                replaceSelections.Clear();
                materialReplaceSelections.Clear();
                return;
            }
            
            // 临时设置选中对象为正确的对象
            var previousSelection = Selection.gameObjects;
            if (lockAllData && lockedSelectionObjects.Length > 0)
            {
                Selection.objects = lockedSelectionObjects;
            }
            
            try
            {
                meshStatistics = ParticleMeshAnalyzer.AnalyzeSelectedParticleSystems(showMeshFilters, showParticleSystems);
                replaceSelections.Clear();
                
                if (showMaterialType)
                {
                    materialStatistics = AnalyzeMaterialReferences();
                    materialReplaceSelections.Clear();
                }
            }
            finally
            {
                // 恢复原来的选中状态
                Selection.objects = previousSelection;
            }
        }
        
        /// <summary>
        /// 分析Material引用
        /// </summary>
        private Dictionary<Material, MaterialStatistics> AnalyzeMaterialReferences()
        {
            var statistics = new Dictionary<Material, MaterialStatistics>();
            var selectedObjects = Selection.gameObjects;
            
            if (selectedObjects == null || selectedObjects.Length == 0)
                return statistics;
            
            foreach (var obj in selectedObjects)
            {
                AnalyzeMaterialInGameObject(obj, statistics);
            }
            
            return statistics;
        }
        
        /// <summary>
        /// 分析GameObject中的Material引用
        /// </summary>
        private void AnalyzeMaterialInGameObject(GameObject obj, Dictionary<Material, MaterialStatistics> statistics)
        {
            // 分析ParticleSystemRenderer
            var particleSystem = obj.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    // 主Material
                    if (renderer.sharedMaterial != null)
                    {
                        AddMaterialReference(renderer.sharedMaterial, renderer, MaterialReferenceType.ParticleSystemRenderer, statistics);
                    }
                    
                    // 拖尾Material
                    if (renderer.trailMaterial != null)
                    {
                        AddMaterialReference(renderer.trailMaterial, renderer, MaterialReferenceType.TrailRenderer, statistics);
                    }
                }
            }
            
            // 分析MeshRenderer
            var meshRenderer = obj.GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                AddMaterialReference(meshRenderer.sharedMaterial, meshRenderer, MaterialReferenceType.MeshRenderer, statistics);
            }
            
            // 分析SkinnedMeshRenderer
            var skinnedMeshRenderer = obj.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMaterial != null)
            {
                AddMaterialReference(skinnedMeshRenderer.sharedMaterial, skinnedMeshRenderer, MaterialReferenceType.SkinnedMeshRenderer, statistics);
            }
            
            // 递归分析子对象
            foreach (Transform child in obj.transform)
            {
                AnalyzeMaterialInGameObject(child.gameObject, statistics);
            }
        }
        
        /// <summary>
        /// 添加Material引用
        /// </summary>
        private void AddMaterialReference(Material material, Component component, MaterialReferenceType referenceType, Dictionary<Material, MaterialStatistics> statistics)
        {
            if (material == null) return;
            
            if (!statistics.ContainsKey(material))
            {
                statistics[material] = new MaterialStatistics
                {
                    referenceCount = 0,
                    materialPath = AssetDatabase.GetAssetPath(material),
                    references = new List<MaterialReference>()
                };
            }
            
            var stats = statistics[material];
            stats.referenceCount++;
            
            var reference = new MaterialReference
            {
                component = component,
                objectPath = GetHierarchyPath(component.gameObject, ""),
                referenceType = referenceType
            };
            
            stats.references.Add(reference);
        }
        
        private void RefreshAvailableMeshes()
        {
            // 使用分页加载
            var result = ParticleMeshAnalyzer.GetMeshAssetsPage(meshPageIndex, MESH_PAGE_SIZE, meshSearchTerm);
            availableMeshes = result.meshes;
            meshTotalCount = result.totalCount;
            meshTotalPages = result.totalPages;
        }
        
        /// <summary>
        /// 刷新所有数据（统计信息、可用Mesh列表和Mesh预览）
        /// </summary>
        public void RefreshAll()
        {
            RefreshAllDataUnified();
        }
        
        private void ForceRefreshAll()
        {
            bool previousLockState = lockAllData;
            var previousSelection = Selection.gameObjects != null
                ? Selection.gameObjects.ToArray()
                : Array.Empty<GameObject>();
            
            // 获取需要刷新的对象
            var objectsToRefresh = previousLockState && lockedSelectionObjects.Length > 0
                ? lockedSelectionObjects
                : previousSelection;

            try
            {
                // 标记所有对象为dirty，确保Unity保存修改
                foreach (var obj in objectsToRefresh)
                {
                    if (obj != null)
                    {
                        EditorUtility.SetDirty(obj);
                        
                        // 标记所有组件为dirty
                        var components = obj.GetComponentsInChildren<Component>(true);
                        foreach (var comp in components)
                        {
                            if (comp != null)
                            {
                                EditorUtility.SetDirty(comp);
                            }
                        }
                    }
                }
                
                // 刷新资源数据库
                AssetDatabase.Refresh();
                
                // **重要**：AssetDatabase.Refresh()可能会导致锁定对象的引用失效
                // 需要重新验证和刷新锁定对象列表
                if (previousLockState && lockedSelectionObjects.Length > 0)
                {
                    // 过滤掉已销毁的对象，保留有效对象
                    lockedSelectionObjects = lockedSelectionObjects.Where(obj => obj != null).ToArray();
                    
                    // 更新锁定对象的摘要显示
                    if (lockedSelectionObjects.Length > 0)
                    {
                        lockedSelectionSummary = string.Join(", ", lockedSelectionObjects.Take(3).Select(obj => obj.name)) 
                            + (lockedSelectionObjects.Length > 3 ? " ..." : string.Empty);
                    }
                    else
                    {
                        // 如果所有对象都失效了，自动解除锁定
                        lockAllData = false;
                        lockedSelectionSummary = string.Empty;
                        EditorPrefs.SetBool("ParticleMeshTool_LockAllData", false);
                    }
                }
                
                // 刷新所有统计数据（保持锁定状态）
                RefreshAll();
                
                // 重绘界面
                Repaint();
            }
            finally
            {
                // 确保锁定状态保持不变
                // 不需要恢复Selection，因为RefreshAll已经正确处理了锁定状态
            }
        }

        private List<ParticleMeshReference> GetSelectedReferences()
        {
            return replaceSelections.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
        }
        
        private void ExecuteReplacement()
        {
            var selectedReferences = GetSelectedReferences();
            if (selectedReferences.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "请选择要替换的目标", "确定");
                return;
            }
            
            var operation = ParticleMeshReplacer.ReplaceMesh(selectedOriginalMesh, selectedNewMesh, selectedReferences, preserveProperties);
            if (operation != null)
            {
                EditorUtility.DisplayDialog("成功", $"已替换 {operation.affectedReferences.Count} 个粒子系统中的Mesh", "确定");
                RefreshAllDataUnified();
            }
        }
        
        // 性能优化：使用时间戳避免重复刷新
        private double _lastAutoRefreshTime = 0;
        private const double AUTO_REFRESH_INTERVAL = 5.0; // 5秒刷新间隔
        
        private void Update()
        {
            if (autoRefresh && !lockAllData)
            {
                double currentTime = EditorApplication.timeSinceStartup;
                if (currentTime - _lastAutoRefreshTime >= AUTO_REFRESH_INTERVAL)
                {
                    _lastAutoRefreshTime = currentTime;
                    RefreshAllDataUnified();
                }
            }
        }
        
        /// <summary>
        /// 统一刷新所有数据（单次遍历收集，避免重复遍历）
        /// </summary>
        private void RefreshAllDataUnified()
        {
            // 根据锁定状态获取目标对象
            var targetObjects = lockAllData && lockedSelectionObjects.Length > 0
                ? lockedSelectionObjects
                : Selection.gameObjects;
            
            if (targetObjects == null || targetObjects.Length == 0)
            {
                ClearAllData();
                return;
            }
            
            // 单次遍历收集所有组件数据
            var collectedData = CollectAllComponentData(targetObjects);
            
            // 从收集的数据更新各个模块（避免重复遍历）
            UpdateStatisticsFromCollectedData(collectedData);
            UpdateMeshPreviewFromCollectedData(collectedData);
            UpdateMaterialPreviewFromCollectedData(collectedData);
            UpdateTexturePreviewFromCollectedData(collectedData);
            
            // 刷新可用资源列表
            RefreshAvailableMeshes();
            RefreshAvailableMaterials();
            
            // 更新缓存统计
            UpdateCachedStatistics();
        }
        
        /// <summary>
        /// 清空所有数据
        /// </summary>
        private void ClearAllData()
        {
            meshStatistics.Clear();
            materialStatistics.Clear();
            replaceSelections.Clear();
            materialReplaceSelections.Clear();
            allMeshes.Clear();
            allMaterials.Clear();
            allTextures.Clear();
            textureGroups.Clear();
            _cachedMeshFilterCount = 0;
            _cachedParticleSystemCount = 0;
            _cachedMaterialRendererCount = 0;
            _cachedMaterialTrailCount = 0;
        }
        
        /// <summary>
        /// 统一收集所有组件数据（单次遍历优化）
        /// </summary>
        private CollectedComponentData CollectAllComponentData(GameObject[] targetObjects)
        {
            var data = new CollectedComponentData();
            
            foreach (var obj in targetObjects)
            {
                if (obj == null) continue;
                CollectComponentsRecursive(obj, obj.name, data);
            }
            
            return data;
        }
        
        /// <summary>
        /// 递归收集组件（单次遍历）
        /// </summary>
        private void CollectComponentsRecursive(GameObject obj, string rootName, CollectedComponentData data)
        {
            // 缓存层级路径
            string hierarchyPath = GetHierarchyPath(obj, rootName);
            data.hierarchyPaths[obj] = hierarchyPath;
            
            // 收集ParticleSystem
            var ps = obj.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                data.particleSystems.Add(ps);
            }
            
            // 收集MeshFilter
            var mf = obj.GetComponent<MeshFilter>();
            if (mf != null)
            {
                data.meshFilters.Add(mf);
            }
            
            // 收集MeshRenderer
            var mr = obj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                data.meshRenderers.Add(mr);
            }
            
            // 收集SkinnedMeshRenderer
            var smr = obj.GetComponent<SkinnedMeshRenderer>();
            if (smr != null)
            {
                data.skinnedMeshRenderers.Add(smr);
            }
            
            // 递归子对象
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                CollectComponentsRecursive(obj.transform.GetChild(i).gameObject, rootName, data);
            }
        }
        
        /// <summary>
        /// 从收集的数据更新统计信息
        /// </summary>
        private void UpdateStatisticsFromCollectedData(CollectedComponentData data)
        {
            meshStatistics.Clear();
            materialStatistics.Clear();
            replaceSelections.Clear();
            materialReplaceSelections.Clear();
            
            // 处理MeshFilter
            if (showMeshFilters)
            {
                foreach (var mf in data.meshFilters)
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    AddMeshToStatistics(mf.sharedMesh, mf, MeshReferenceType.MeshFilter, data);
                }
            }
            
            // 处理ParticleSystem
            if (showParticleSystems)
            {
                foreach (var ps in data.particleSystems)
                {
                    if (ps == null) continue;
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null && renderer.renderMode == ParticleSystemRenderMode.Mesh && renderer.mesh != null)
                    {
                        AddMeshToStatistics(renderer.mesh, ps, MeshReferenceType.ParticleSystem, data);
                    }
                }
            }
            
            // 处理Material统计
            if (showMaterialType)
            {
                foreach (var ps in data.particleSystems)
                {
                    if (ps == null) continue;
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        if (renderer.sharedMaterial != null)
                            AddMaterialToStatistics(renderer.sharedMaterial, renderer, MaterialReferenceType.ParticleSystemRenderer, data);
                        if (renderer.trailMaterial != null)
                            AddMaterialToStatistics(renderer.trailMaterial, renderer, MaterialReferenceType.TrailRenderer, data);
                    }
                }
                
                foreach (var mr in data.meshRenderers)
                {
                    if (mr != null && mr.sharedMaterial != null)
                        AddMaterialToStatistics(mr.sharedMaterial, mr, MaterialReferenceType.MeshRenderer, data);
                }
                
                foreach (var smr in data.skinnedMeshRenderers)
                {
                    if (smr != null && smr.sharedMaterial != null)
                        AddMaterialToStatistics(smr.sharedMaterial, smr, MaterialReferenceType.SkinnedMeshRenderer, data);
                }
            }
        }
        
        /// <summary>
        /// 添加Mesh到统计（辅助方法）
        /// </summary>
        private void AddMeshToStatistics(Mesh mesh, Component component, MeshReferenceType refType, CollectedComponentData data)
        {
            if (!meshStatistics.ContainsKey(mesh))
            {
                meshStatistics[mesh] = new MeshStatistics(mesh);
            }
            
            var stats = meshStatistics[mesh];
            
            string path = data.hierarchyPaths.ContainsKey(component.gameObject) 
                ? data.hierarchyPaths[component.gameObject] 
                : component.gameObject.name;
            
            ParticleMeshReference reference;
            if (refType == MeshReferenceType.ParticleSystem)
            {
                reference = new ParticleMeshReference(component as ParticleSystem, mesh);
            }
            else
            {
                reference = new ParticleMeshReference(component as MeshFilter, mesh);
            }
            reference.objectPath = path;
            
            stats.AddReference(reference);
        }
        
        /// <summary>
        /// 添加Material到统计（辅助方法）
        /// </summary>
        private void AddMaterialToStatistics(Material material, Component component, MaterialReferenceType refType, CollectedComponentData data)
        {
            if (!materialStatistics.ContainsKey(material))
            {
                materialStatistics[material] = new MaterialStatistics
                {
                    referenceCount = 0,
                    materialPath = AssetDatabase.GetAssetPath(material),
                    references = new List<MaterialReference>()
                };
            }
            
            var stats = materialStatistics[material];
            stats.referenceCount++;
            
            string path = data.hierarchyPaths.ContainsKey(component.gameObject) 
                ? data.hierarchyPaths[component.gameObject] 
                : component.gameObject.name;
            
            var reference = new MaterialReference
            {
                component = component,
                objectPath = path,
                referenceType = refType
            };
            
            stats.references.Add(reference);
        }
        
        /// <summary>
        /// 从收集的数据更新Mesh预览
        /// </summary>
        private void UpdateMeshPreviewFromCollectedData(CollectedComponentData data)
        {
            allMeshes.Clear();
            
            // 处理MeshFilter
            if (showMeshFilters)
            {
                foreach (var mf in data.meshFilters)
                {
                    if (mf == null) continue;
                    string path = data.hierarchyPaths.ContainsKey(mf.gameObject) 
                        ? data.hierarchyPaths[mf.gameObject] : mf.gameObject.name;
                    
                    allMeshes.Add(new MeshPreviewInfo
                    {
                        gameObject = mf.gameObject,
                        mesh = mf.sharedMesh,
                        componentType = "MeshFilter",
                        hierarchyPath = path,
                        additionalInfo = mf.sharedMesh != null ? "共享Mesh" : "无Mesh"
                    });
                }
            }
            
            // 处理ParticleSystem
            if (showParticleSystems)
            {
                foreach (var ps in data.particleSystems)
                {
                    if (ps == null) continue;
                    string path = data.hierarchyPaths.ContainsKey(ps.gameObject) 
                        ? data.hierarchyPaths[ps.gameObject] : ps.gameObject.name;
                    
                    // Shape模块
                    var shape = ps.shape;
                    if (shape.mesh != null)
                    {
                        allMeshes.Add(new MeshPreviewInfo
                        {
                            gameObject = ps.gameObject,
                            mesh = shape.mesh,
                            componentType = "ParticleSystem (Shape)",
                            hierarchyPath = path,
                            additionalInfo = $"Shape类型: {shape.shapeType}"
                        });
                    }
                    
                    // Renderer模块
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        var meshes = new Mesh[4];
                        var meshCount = renderer.GetMeshes(meshes);
                        
                        if (meshCount > 0)
                        {
                            for (int i = 0; i < meshCount; i++)
                            {
                                if (meshes[i] != null)
                                {
                                    allMeshes.Add(new MeshPreviewInfo
                                    {
                                        gameObject = ps.gameObject,
                                        mesh = meshes[i],
                                        componentType = $"ParticleSystem (Mesh {i + 1})",
                                        hierarchyPath = path,
                                        additionalInfo = $"多Mesh渲染 ({i + 1}/{meshCount})"
                                    });
                                }
                            }
                        }
                        else if (renderer.mesh != null)
                        {
                            allMeshes.Add(new MeshPreviewInfo
                            {
                                gameObject = ps.gameObject,
                                mesh = renderer.mesh,
                                componentType = "ParticleSystem (Renderer)",
                                hierarchyPath = path,
                                additionalInfo = $"渲染模式: {renderer.renderMode}"
                            });
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 从收集的数据更新Material预览
        /// </summary>
        private void UpdateMaterialPreviewFromCollectedData(CollectedComponentData data)
        {
            allMaterials.Clear();
            
            if (!showMaterialType) return;
            
            // 处理ParticleSystem材质
            foreach (var ps in data.particleSystems)
            {
                if (ps == null) continue;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer == null) continue;
                
                string path = data.hierarchyPaths.ContainsKey(ps.gameObject) 
                    ? data.hierarchyPaths[ps.gameObject] : ps.gameObject.name;
                
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null)
                    {
                        allMaterials.Add(new MaterialPreviewInfo
                        {
                            gameObject = ps.gameObject,
                            material = materials[i],
                            componentType = "粒子系统渲染器",
                            hierarchyPath = path,
                            additionalInfo = i == 0 ? "主材质" : $"材质[{i}]",
                            materialIndex = i
                        });
                    }
                }
                
                if (renderer.trailMaterial != null)
                {
                    allMaterials.Add(new MaterialPreviewInfo
                    {
                        gameObject = ps.gameObject,
                        material = renderer.trailMaterial,
                        componentType = "粒子系统拖尾",
                        hierarchyPath = path,
                        additionalInfo = "拖尾材质",
                        materialIndex = -1
                    });
                }
            }
            
            // 处理MeshRenderer材质
            foreach (var mr in data.meshRenderers)
            {
                if (mr == null) continue;
                string path = data.hierarchyPaths.ContainsKey(mr.gameObject) 
                    ? data.hierarchyPaths[mr.gameObject] : mr.gameObject.name;
                
                var materials = mr.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null)
                    {
                        allMaterials.Add(new MaterialPreviewInfo
                        {
                            gameObject = mr.gameObject,
                            material = materials[i],
                            componentType = "MeshRenderer",
                            hierarchyPath = path,
                            additionalInfo = materials.Length > 1 ? $"材质[{i}]" : "材质",
                            materialIndex = i
                        });
                    }
                }
            }
            
            // 处理SkinnedMeshRenderer材质
            foreach (var smr in data.skinnedMeshRenderers)
            {
                if (smr == null) continue;
                string path = data.hierarchyPaths.ContainsKey(smr.gameObject) 
                    ? data.hierarchyPaths[smr.gameObject] : smr.gameObject.name;
                
                var materials = smr.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null)
                    {
                        allMaterials.Add(new MaterialPreviewInfo
                        {
                            gameObject = smr.gameObject,
                            material = materials[i],
                            componentType = "SkinnedMeshRenderer",
                            hierarchyPath = path,
                            additionalInfo = materials.Length > 1 ? $"材质[{i}]" : "材质",
                            materialIndex = i
                        });
                    }
                }
            }
        }
        
        /// <summary>
        /// 从收集的数据更新贴图预览
        /// </summary>
        private void UpdateTexturePreviewFromCollectedData(CollectedComponentData data)
        {
            allTextures.Clear();
            textureGroups.Clear();
            
            if (!showTextureType) return;
            
            // 收集所有材质中的贴图
            var processedMaterials = new HashSet<Material>();
            
            // 从ParticleSystem收集
            foreach (var ps in data.particleSystems)
            {
                if (ps == null) continue;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer == null) continue;
                
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null && !processedMaterials.Contains(mat))
                    {
                        processedMaterials.Add(mat);
                        CollectTexturesFromMaterial(mat, ps.gameObject, data);
                    }
                }
                
                if (renderer.trailMaterial != null && !processedMaterials.Contains(renderer.trailMaterial))
                {
                    processedMaterials.Add(renderer.trailMaterial);
                    CollectTexturesFromMaterial(renderer.trailMaterial, ps.gameObject, data);
                }
            }
            
            // 从MeshRenderer收集
            foreach (var mr in data.meshRenderers)
            {
                if (mr == null) continue;
                foreach (var mat in mr.sharedMaterials)
                {
                    if (mat != null && !processedMaterials.Contains(mat))
                    {
                        processedMaterials.Add(mat);
                        CollectTexturesFromMaterial(mat, mr.gameObject, data);
                    }
                }
            }
            
            // 从SkinnedMeshRenderer收集
            foreach (var smr in data.skinnedMeshRenderers)
            {
                if (smr == null) continue;
                foreach (var mat in smr.sharedMaterials)
                {
                    if (mat != null && !processedMaterials.Contains(mat))
                    {
                        processedMaterials.Add(mat);
                        CollectTexturesFromMaterial(mat, smr.gameObject, data);
                    }
                }
            }
        }
        
        /// <summary>
        /// 从材质收集贴图（辅助方法）
        /// </summary>
        private void CollectTexturesFromMaterial(Material material, GameObject gameObject, CollectedComponentData data)
        {
            if (material == null) return;
            
            string path = data.hierarchyPaths.ContainsKey(gameObject) 
                ? data.hierarchyPaths[gameObject] : gameObject.name;
            
            var shader = material.shader;
            if (shader == null) return;
            
            int propertyCount = ShaderUtil.GetPropertyCount(shader);
            for (int i = 0; i < propertyCount; i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    string propertyName = ShaderUtil.GetPropertyName(shader, i);
                    var texture = material.GetTexture(propertyName);
                    
                    if (texture != null)
                    {
                        var textureItem = new TexturePreviewItem
                        {
                            texture = texture,
                            material = material,
                            gameObject = gameObject,
                            propertyName = propertyName,
                            hierarchyPath = path,
                            textureType = GetTextureType(propertyName)
                        };
                        
                        allTextures.Add(textureItem);
                        
                        // 添加到分组
                        if (!textureGroups.ContainsKey(texture))
                        {
                            textureGroups[texture] = new List<TexturePreviewItem>();
                        }
                        textureGroups[texture].Add(textureItem);
                    }
                }
            }
        }
        
        /// <summary>
        /// 更新缓存的统计数据
        /// </summary>
        private void UpdateCachedStatistics()
        {
            _cachedMeshFilterCount = 0;
            _cachedParticleSystemCount = 0;
            _cachedMaterialRendererCount = 0;
            _cachedMaterialTrailCount = 0;
            _cachedMeshRendererCount = 0;
            _cachedSkinnedMeshRendererCount = 0;
            _cachedMissingTextureCount = 0;
            
            // Mesh预览面板的统计
            foreach (var mesh in allMeshes)
            {
                if (mesh.componentType.Contains("MeshFilter"))
                    _cachedMeshFilterCount++;
                else if (mesh.componentType.Contains("ParticleSystem"))
                    _cachedParticleSystemCount++;
            }
            
            // Material预览面板的统计
            foreach (var mat in allMaterials)
            {
                if (mat.componentType.Contains("粒子系统"))
                    _cachedMaterialRendererCount++;
                else if (mat.componentType.Contains("MeshRenderer") && !mat.componentType.Contains("Skinned"))
                    _cachedMeshRendererCount++;
                else if (mat.componentType.Contains("SkinnedMeshRenderer"))
                    _cachedSkinnedMeshRendererCount++;
                else if (mat.componentType.Contains("拖尾"))
                    _cachedMaterialTrailCount++;
            }
            
            // 贴图预览面板的统计
            foreach (var tex in allTextures)
            {
                if (tex.texture == null)
                    _cachedMissingTextureCount++;
            }
            
            // 引用统计面板的缓存数据
            _cachedStatsMeshPSCount = 0;
            _cachedStatsMeshMFCount = 0;
            _cachedStatsMatRendererCount = 0;
            _cachedStatsMatTrailCount = 0;
            
            foreach (var stats in meshStatistics.Values)
            {
                foreach (var reference in stats.references)
                {
                    if (reference.referenceType == MeshReferenceType.ParticleSystem)
                        _cachedStatsMeshPSCount++;
                    else if (reference.referenceType == MeshReferenceType.MeshFilter)
                        _cachedStatsMeshMFCount++;
                }
            }
            
            foreach (var stats in materialStatistics.Values)
            {
                foreach (var reference in stats.references)
                {
                    if (reference.referenceType == MaterialReferenceType.ParticleSystemRenderer)
                        _cachedStatsMatRendererCount++;
                    else if (reference.referenceType == MaterialReferenceType.TrailRenderer)
                        _cachedStatsMatTrailCount++;
                }
            }
            
            // 缓存排序后的统计数据（避免OnGUI中OrderByDescending）
            _cachedSortedMeshStats.Clear();
            _cachedSortedMeshStats.AddRange(meshStatistics.OrderByDescending(x => x.Value.referenceCount));
            
            _cachedSortedMaterialStats.Clear();
            _cachedSortedMaterialStats.AddRange(materialStatistics.OrderByDescending(x => x.Value.referenceCount));
        }
        
        private void DrawMeshPreviewTab()
        {
            EditorGUILayout.LabelField("Mesh总预览", EditorStyles.boldLabel);
            
            // 显示锁定状态
            if (lockAllData)
            {
                EditorGUILayout.HelpBox("数据已锁定。当前显示的是锁定时的数据，不会随选中对象变化而更新。点击工具栏中的\"锁定\"按钮可解除锁定。", MessageType.Info);
            }
            
            // 检查是否有选中对象
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                if (!lockAllData)
                {
                    EditorGUILayout.HelpBox("请在Hierarchy面板中选择要预览的GameObject", MessageType.Warning);
                    return;
                }
                else
                {
                    EditorGUILayout.LabelField("数据已锁定 - 当前无选中对象", EditorStyles.helpBox);
                }
            }
            
            EditorGUILayout.Space();
            
            // 显示当前选中的对象信息
            EditorGUILayout.LabelField($"预览对象: {string.Join(", ", selectedObjects.Take(3).Select(obj => obj.name))}{(selectedObjects.Length > 3 ? " ..." : "")}", EditorStyles.helpBox);
            
            // 统计信息（使用缓存数据避免OnGUI中的LINQ计算）
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"发现 {allMeshes.Count} 个Mesh组件", EditorStyles.helpBox);
            EditorGUILayout.LabelField($"MeshFilter: {_cachedMeshFilterCount}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"粒子系统: {_cachedParticleSystemCount}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            if (allMeshes.Count == 0)
            {
                EditorGUILayout.HelpBox("根据当前过滤条件，没有找到匹配的Mesh组件", MessageType.Info);
                return;
            }
            
            // 外部资源过滤
            EditorGUILayout.BeginHorizontal();
            
            GUILayout.FlexibleSpace();
            
            var originalColor = GUI.color;
            GUI.color = showOnlyExternalMeshes ? new Color(1f, 0.5f, 0.5f) : Color.white;
            showOnlyExternalMeshes = EditorGUILayout.Toggle("⚠ 只显示外部资源", showOnlyExternalMeshes, GUILayout.Width(150));
            GUI.color = originalColor;
            
            EditorGUILayout.EndHorizontal();;
            
            if (showOnlyExternalMeshes)
            {
                EditorGUILayout.HelpBox("已启用：只显示路径在预制体平级目录外的Mesh", MessageType.Warning);
            }
            
            EditorGUILayout.Space();
            
            // Mesh列表
            meshPreviewScrollPos = EditorGUILayout.BeginScrollView(meshPreviewScrollPos);
            
            // 使用ToList()创建副本来避免集合修改异常
            var meshesToDraw = allMeshes.ToList();
            foreach (var meshInfo in meshesToDraw)
            {
                DrawMeshPreviewItem(meshInfo);
            }
            
            EditorGUILayout.EndScrollView();
            
            // 在GUI绘制完成后处理延迟刷新
            if (needsRefreshMeshPreview)
            {
                needsRefreshMeshPreview = false;
                RefreshAllDataUnified();
            }
        }
        
        private void DrawMeshPreviewItem(MeshPreviewInfo meshInfo)
        {
            // 应用外部资源过滤
            if (showOnlyExternalMeshes)
            {
                string meshPath = meshInfo.mesh != null ? AssetDatabase.GetAssetPath(meshInfo.mesh) : "";
                if (!IsResourceOutsidePrefabDirectory(meshPath))
                {
                    return; // 不是外部资源，不显示
                }
            }
            
            // 跳过详情信息条目，只显示实际的Mesh信息
            if (meshInfo.componentType.Contains("详情"))
            {
                return;
            }
            
            // 设置白色字体作为基础颜色
            var originalColor = GUI.color;
            GUI.color = Color.white;
            
            EditorGUILayout.BeginHorizontal();
            
            // 显示层级结构的缩进
            var indentLevel = GetIndentLevel(meshInfo.hierarchyPath);
            GUILayout.Space(indentLevel * 20);
            
            // GameObject选择按钮（保留跳转功能）
            EditorGUILayout.ObjectField("", meshInfo.gameObject, typeof(GameObject), true, GUILayout.Width(150));
            
            // 可编辑的Mesh字段
            var newMesh = (Mesh)EditorGUILayout.ObjectField("", meshInfo.mesh, typeof(Mesh), false, GUILayout.Width(150));
            if (newMesh != meshInfo.mesh)
            {
                ApplyMeshChange(meshInfo, newMesh);
                needsRefreshMeshPreview = true; // 标记延迟刷新
            }
            
            // 恢复背景色
            GUI.backgroundColor = Color.white;
            
            // 类型标签和操作按钮
            DrawComponentTypeLabel(meshInfo.componentType);
            
            EditorGUILayout.EndHorizontal();
            
            // 恢复原始颜色
            GUI.color = originalColor;
        }
        
        private void DrawComponentTypeLabel(string componentType)
        {
            var originalColor = GUI.color;
            var originalBgColor = GUI.backgroundColor;
            
            // 根据组件类型设置不同的颜色
            if (componentType.Contains("MeshFilter"))
            {
                GUI.backgroundColor = new Color(0.7f, 0.8f, 1f); // 淡蓝色背景
                GUI.color = new Color(0.2f, 0.3f, 0.8f); // 深蓝色文字
                EditorGUILayout.LabelField($"[MeshFilter]", EditorStyles.helpBox, GUILayout.Width(80));
            }
            else if (componentType.Contains("ParticleSystem"))
            {
                GUI.backgroundColor = new Color(0.7f, 1f, 0.7f); // 淡绿色背景
                GUI.color = new Color(0.2f, 0.6f, 0.2f); // 深绿色文字
                
                string displayText = "[粒子系统]";
                if (componentType.Contains("Shape"))
                    displayText = "[粒子-形状]";
                else if (componentType.Contains("Renderer") || componentType.Contains("Mesh"))
                    displayText = "[粒子-多网格]"; // 将渲染和多网格类别合并
                
                EditorGUILayout.LabelField(displayText, EditorStyles.helpBox, GUILayout.Width(80));
            }
            
            // 恢复原始颜色
            GUI.color = originalColor;
            GUI.backgroundColor = originalBgColor;
        }
        
        private int GetIndentLevel(string hierarchyPath)
        {
            if (string.IsNullOrEmpty(hierarchyPath))
                return 0;
            
            return hierarchyPath.Split('/').Length - 1;
        }
        
        private string GetComponentTypeShort(string componentType)
        {
            if (componentType.Contains("MeshFilter"))
                return "MF";
            if (componentType.Contains("ParticleSystem"))
            {
                if (componentType.Contains("Shape"))
                    return "PS-S";
                if (componentType.Contains("Renderer"))
                    return "PS-R";
                if (componentType.Contains("Mesh"))
                    return "PS-M";
                return "PS";
            }
            return "Unknown";
        }
        
        private void RefreshMeshPreview()
        {
            // 根据锁定状态使用正确的对象
            var selectedObjects = lockAllData && lockedSelectionObjects.Length > 0
                ? lockedSelectionObjects
                : Selection.gameObjects;
            
            // 如果没有选中对象且没有锁定对象，则清空预览
            if ((selectedObjects == null || selectedObjects.Length == 0) && !lockAllData)
            {
                allMeshes.Clear();
                UpdateCachedStatistics();
                return;
            }
            
            allMeshes.Clear();
            
            foreach (var selectedObj in selectedObjects)
            {
                CollectMeshesFromGameObject(selectedObj, selectedObj.name);
            }
            
            UpdateCachedStatistics();
        }
        
        private void CollectMeshesFromGameObject(GameObject obj, string rootName)
        {
            // 检查MeshFilter组件
            if (showMeshFilters)
            {
                var meshFilter = obj.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    var meshInfo = new MeshPreviewInfo
                    {
                        gameObject = obj,
                        mesh = meshFilter.sharedMesh,
                        componentType = "MeshFilter",
                        hierarchyPath = GetHierarchyPath(obj, rootName),
                        additionalInfo = meshFilter.sharedMesh != null ? $"共享Mesh" : "无Mesh"
                    };
                    allMeshes.Add(meshInfo);
                }
            }
            
            // 检查ParticleSystem组件
            if (showParticleSystems)
            {
                var particleSystem = obj.GetComponent<ParticleSystem>();
                if (particleSystem != null)
                {
                    // Shape模块的Mesh（无论是否启用或形状类型如何）
                    var shape = particleSystem.shape;
                    if (shape.mesh != null)
                    {
                        var meshInfo = new MeshPreviewInfo
                        {
                            gameObject = obj,
                            mesh = shape.mesh,
                            componentType = "ParticleSystem (Shape)",
                            hierarchyPath = GetHierarchyPath(obj, rootName),
                            additionalInfo = $"Shape类型: {shape.shapeType}, 启用: {shape.enabled}"
                        };
                        allMeshes.Add(meshInfo);
                    }
                    
                    // Renderer模块的Mesh - 合并单Mesh和多Mesh显示
                    var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        // 首先检查多个Mesh（用于随机渲染）
                        var meshes = new Mesh[4]; // Unity最多支持4个Mesh
                        var actualMeshCount = renderer.GetMeshes(meshes);
                        
                        bool hasMultiMesh = false;
                        if (actualMeshCount > 0)
                        {
                            for (int meshIndex = 0; meshIndex < actualMeshCount; meshIndex++)
                            {
                                if (meshes[meshIndex] != null)
                                {
                                    hasMultiMesh = true;
                                    var meshInfo = new MeshPreviewInfo
                                    {
                                        gameObject = obj,
                                        mesh = meshes[meshIndex],
                                        componentType = $"ParticleSystem (Mesh {meshIndex + 1})",
                                        hierarchyPath = GetHierarchyPath(obj, rootName),
                                        additionalInfo = $"多Mesh渲染 ({meshIndex + 1}/{actualMeshCount}), 渲染模式: {renderer.renderMode}"
                                    };
                                    allMeshes.Add(meshInfo);
                                }
                            }
                        }
                        
                        // 如果没有多Mesh，则检查单个Mesh引用
                        if (!hasMultiMesh && renderer.mesh != null)
                        {
                            var meshInfo = new MeshPreviewInfo
                            {
                                gameObject = obj,
                                mesh = renderer.mesh,
                                componentType = "ParticleSystem (Renderer)",
                                hierarchyPath = GetHierarchyPath(obj, rootName),
                                additionalInfo = $"渲染模式: {renderer.renderMode}, 材质数: {renderer.sharedMaterials?.Length ?? 0}"
                            };
                            allMeshes.Add(meshInfo);
                        }
                    }
                    
                    // 如果启用详细信息，添加粒子系统的基本信息
                    if (showParticleSystemDetails && (shape.enabled || renderer != null))
                    {
                        var detailInfo = new MeshPreviewInfo
                        {
                            gameObject = obj,
                            mesh = null, // 这是信息条目，不是实际的Mesh
                            componentType = "ParticleSystem (详情)",
                            hierarchyPath = GetHierarchyPath(obj, rootName),
                            additionalInfo = $"最大粒子数: {particleSystem.main.maxParticles}, 播放状态: {(particleSystem.isPlaying ? "播放中" : "已停止")}"
                        };
                        allMeshes.Add(detailInfo);
                    }
                }
            }
            
            // 递归检查子对象
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                CollectMeshesFromGameObject(obj.transform.GetChild(i).gameObject, rootName);
            }
        }
        
        private string GetHierarchyPath(GameObject obj, string rootName)
        {
            var path = obj.name;
            var parent = obj.transform.parent;
            
            while (parent != null && parent.name != rootName)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            if (parent != null && parent.name == rootName)
            {
                path = rootName + "/" + path;
            }
            
            return path;
        }
        
        /// <summary>
        /// 应用Mesh更改
        /// </summary>
        private void ApplyMeshChange(MeshPreviewInfo meshInfo, Mesh newMesh)
        {
            if (meshInfo.gameObject == null) return;
            
            Undo.RecordObject(meshInfo.gameObject, "修改Mesh引用");
            
            // 处理MeshFilter组件
            if (meshInfo.componentType.Contains("MeshFilter"))
            {
                var meshFilter = meshInfo.gameObject.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    meshFilter.sharedMesh = newMesh;
                    EditorUtility.SetDirty(meshFilter);
                }
            }
            // 处理ParticleSystem组件
            else if (meshInfo.componentType.Contains("ParticleSystem"))
            {
                var particleSystem = meshInfo.gameObject.GetComponent<ParticleSystem>();
                if (particleSystem != null)
                {
                    // Shape模块
                    if (meshInfo.componentType.Contains("Shape"))
                    {
                        var shape = particleSystem.shape;
                        shape.mesh = newMesh;
                    }
                    // Renderer模块和多Mesh处理 - 合并处理逻辑
                    else if (meshInfo.componentType.Contains("Renderer") || meshInfo.componentType.Contains("Mesh"))
                    {
                        var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                        if (renderer != null)
                        {
                            // 如果是清理操作（newMesh为null），直接清空所有相关引用
                            if (newMesh == null)
                            {
                                renderer.mesh = null; // 清空单个Mesh引用
                                renderer.SetMeshes(new Mesh[0]); // 清空多Mesh数组
                            }
                            else
                            {
                                // 如果是多Mesh条目，需要特殊处理
                                if (meshInfo.componentType.Contains("Mesh"))
                                {
                                    // 获取当前的多Mesh数组
                                    var meshes = new Mesh[4];
                                    var actualMeshCount = renderer.GetMeshes(meshes);
                                    
                                    // 找到对应的索引并替换
                                    var meshIndexText = meshInfo.componentType.Substring(meshInfo.componentType.LastIndexOf(' ') + 1);
                                    if (meshIndexText.EndsWith(")"))
                                    {
                                        meshIndexText = meshIndexText.Substring(0, meshIndexText.Length - 1);
                                    }
                                    
                                    if (int.TryParse(meshIndexText, out int meshIndex) && meshIndex > 0 && meshIndex <= actualMeshCount)
                                    {
                                        meshes[meshIndex - 1] = newMesh;
                                        renderer.SetMeshes(meshes.Take(actualMeshCount).ToArray());
                                    }
                                }
                                else
                                {
                                    // 单个Mesh引用
                                    renderer.mesh = newMesh;
                                }
                            }
                            EditorUtility.SetDirty(renderer);
                        }
                    }
                    
                    EditorUtility.SetDirty(particleSystem);
                }
            }
        }

        /// <summary>
        /// 绘制Material预览标签页
        /// </summary>
        private void DrawMaterialPreviewTab()
        {
            EditorGUILayout.LabelField("Material总预览", EditorStyles.boldLabel);
            
            // 显示锁定状态
            if (lockAllData)
            {
                EditorGUILayout.HelpBox("数据已锁定。当前显示的是锁定时的数据，不会随选中对象变化而更新。点击工具栏中的\"锁定\"按钮可解除锁定。", MessageType.Info);
            }
            
            // 检查是否有选中对象
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                if (!lockAllData)
                {
                    EditorGUILayout.HelpBox("请在Hierarchy面板中选择要预览的GameObject", MessageType.Warning);
                    return;
                }
                else
                {
                    EditorGUILayout.LabelField("数据已锁定 - 当前无选中对象", EditorStyles.helpBox);
                }
            }
            
            EditorGUILayout.Space();
            
            // 显示当前选中的对象信息
            EditorGUILayout.LabelField($"预览对象: {string.Join(", ", selectedObjects.Take(3).Select(obj => obj.name))}{(selectedObjects.Length > 3 ? " ..." : "")}", EditorStyles.helpBox);
            
            // 统计信息（使用缓存数据避免OnGUI中的LINQ计算）
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"发现 {allMaterials.Count} 个Material组件", EditorStyles.helpBox);
            EditorGUILayout.LabelField($"粒子系统: {_cachedMaterialRendererCount}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"MeshRenderer: {_cachedMeshRendererCount}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"SkinnedMeshRenderer: {_cachedSkinnedMeshRendererCount}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 过滤选项
            EditorGUILayout.BeginHorizontal();
            
            var filterColor = GUI.color;
            GUI.color = showOnlyExternalMaterials ? new Color(1f, 0.5f, 0.5f) : Color.white;
            showOnlyExternalMaterials = EditorGUILayout.Toggle("⚠ 只显示外部资源", showOnlyExternalMaterials, GUILayout.Width(150));
            GUI.color = filterColor;
            
            EditorGUILayout.EndHorizontal();
            
            if (showOnlyExternalMaterials)
            {
                EditorGUILayout.HelpBox("已启用：只显示路径在预制体平级目录外的材质", MessageType.Warning);
            }
            
            EditorGUILayout.Space();
            
            if (allMaterials.Count == 0)
            {
                EditorGUILayout.HelpBox("在选中的GameObject及其子对象中未发现Material组件", MessageType.Info);
                return;
            }
            
            // 控制按钮
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("刷新Material预览", GUILayout.Width(120)))
            {
                RefreshAllDataUnified();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Material预览列表
            materialPreviewScrollPos = EditorGUILayout.BeginScrollView(materialPreviewScrollPos);
            
            // 创建集合副本以避免遍历时修改集合的异常
            var materialsCopy = allMaterials.ToList();
            foreach (var materialInfo in materialsCopy)
            {
                DrawMaterialPreviewItem(materialInfo);
            }
            
            EditorGUILayout.EndScrollView();
            
            // 在GUI绘制完成后处理延迟刷新
            if (needsRefreshMaterialPreview)
            {
                needsRefreshMaterialPreview = false;
                RefreshAllDataUnified();
            }
        }

        /// <summary>
        /// 绘制单个Material预览项
        /// </summary>
        private void DrawMaterialPreviewItem(MaterialPreviewInfo materialInfo)
        {
            // 应用外部资源过滤
            if (showOnlyExternalMaterials)
            {
                string materialPath = materialInfo.material != null ? AssetDatabase.GetAssetPath(materialInfo.material) : "";
                if (!IsResourceOutsidePrefabDirectory(materialPath))
                {
                    return; // 不是外部资源，不显示
                }
            }
            
            // 设置白色字体作为基础颜色
            var originalColor = GUI.color;
            GUI.color = Color.white;
            
            EditorGUILayout.BeginHorizontal();
            
            // 显示层级结构的缩进
            var indentLevel = GetIndentLevel(materialInfo.hierarchyPath);
            GUILayout.Space(indentLevel * 20);
            
            // GameObject选择按钮（保留跳转功能）
            EditorGUILayout.ObjectField("", materialInfo.gameObject, typeof(GameObject), true, GUILayout.Width(150));
            
            // 可编辑的Material字段
            var newMaterial = (Material)EditorGUILayout.ObjectField("", materialInfo.material, typeof(Material), false, GUILayout.Width(150));
            if (newMaterial != materialInfo.material)
            {
                ApplyMaterialChange(materialInfo, newMaterial);
                needsRefreshMaterialPreview = true; // 标记需要延迟刷新
            }
            
            // 类型标签
            DrawMaterialComponentTypeLabel(materialInfo.componentType);
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 恢复原始颜色
            GUI.color = originalColor;
        }

        /// <summary>
        /// 绘制Material组件类型标签
        /// </summary>
        private void DrawMaterialComponentTypeLabel(string componentType)
        {
            Color labelColor = Color.white;
            
            switch (componentType)
            {
                case "粒子系统渲染器":
                    labelColor = new Color(0.7f, 1f, 0.7f); // 绿色
                    break;
                case "粒子系统拖尾":
                    labelColor = new Color(0.9f, 1f, 0.7f); // 淡黄绿色
                    break;
                case "MeshRenderer":
                    labelColor = new Color(0.7f, 0.8f, 1f); // 蓝色
                    break;
                case "SkinnedMeshRenderer":
                    labelColor = new Color(1f, 0.8f, 0.7f); // 橙色
                    break;
            }
            
            GUI.backgroundColor = labelColor;
            EditorGUILayout.LabelField(componentType, EditorStyles.miniButton, GUILayout.Width(120));
            GUI.backgroundColor = Color.white;
        }

        /// <summary>
        /// 刷新Material预览
        /// </summary>
        private void RefreshMaterialPreview()
        {
            // 根据锁定状态使用正确的对象
            var selectedObjects = lockAllData && lockedSelectionObjects.Length > 0
                ? lockedSelectionObjects
                : Selection.gameObjects;
            
            // 如果没有选中对象且没有锁定对象，则清空预览
            if ((selectedObjects == null || selectedObjects.Length == 0) && !lockAllData)
            {
                allMaterials.Clear();
                UpdateCachedStatistics();
                return;
            }
            
            allMaterials.Clear();
            
            if (!showMaterialType)
            {
                UpdateCachedStatistics();
                return;
            }
            
            foreach (var selectedObj in selectedObjects)
            {
                CollectMaterialsFromGameObject(selectedObj, selectedObj.name);
            }
            
            // 按GameObject名称排序
            allMaterials.Sort((a, b) => string.Compare(a.gameObject.name, b.gameObject.name));
            UpdateCachedStatistics();
        }
        
        /// <summary>
        /// 从GameObject及其子对象收集Material信息
        /// </summary>
        private void CollectMaterialsFromGameObject(GameObject obj, string rootName)
        {
            // 检查ParticleSystemRenderer组件
            if (showMaterialRenderers || showParticleSystems)
            {
                var particleSystem = obj.GetComponent<ParticleSystem>();
                if (particleSystem != null)
                {
                    var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        var materials = renderer.sharedMaterials;
                        var trailMaterial = renderer.trailMaterial;
                        
                        // 处理材质数组中的材质
                        for (int i = 0; i < materials.Length; i++)
                        {
                            if (materials[i] != null)
                            {
                                // 检查是否是拖尾材质
                                bool isTrailMaterial = showMaterialTrails && trailMaterial != null && materials[i] == trailMaterial;
                                
                                var materialInfo = new MaterialPreviewInfo
                                {
                                    gameObject = obj,
                                    material = materials[i],
                                    componentType = isTrailMaterial ? "粒子系统拖尾" : "粒子系统渲染器",
                                    hierarchyPath = GetHierarchyPath(obj, rootName),
                                    additionalInfo = isTrailMaterial ? "拖尾材质" : (i == 0 ? "主材质" : $"材质[{i}]"),
                                    materialIndex = isTrailMaterial ? -1 : i
                                };
                                allMaterials.Add(materialInfo);
                            }
                        }
                        
                        // 如果拖尾材质不在材质数组中，单独添加
                        if (showMaterialTrails && trailMaterial != null)
                        {
                            bool trailInMaterials = false;
                            for (int i = 0; i < materials.Length; i++)
                            {
                                if (materials[i] == trailMaterial)
                                {
                                    trailInMaterials = true;
                                    break;
                                }
                            }
                            
                            if (!trailInMaterials)
                            {
                                var materialInfo = new MaterialPreviewInfo
                                {
                                    gameObject = obj,
                                    material = trailMaterial,
                                    componentType = "粒子系统拖尾",
                                    hierarchyPath = GetHierarchyPath(obj, rootName),
                                    additionalInfo = "拖尾材质",
                                    materialIndex = -1
                                };
                                allMaterials.Add(materialInfo);
                            }
                        }
                    }
                }
            }
            
            // 检查MeshRenderer组件
            if (showMaterialRenderers || showMeshFilters)
            {
                var meshRenderer = obj.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    var materials = meshRenderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] != null)
                        {
                            var materialInfo = new MaterialPreviewInfo
                            {
                                gameObject = obj,
                                material = materials[i],
                                componentType = "MeshRenderer",
                                hierarchyPath = GetHierarchyPath(obj, rootName),
                                additionalInfo = materials.Length > 1 ? $"材质[{i}]" : "材质",
                                materialIndex = i
                            };
                            allMaterials.Add(materialInfo);
                        }
                    }
                }
            }
            
            // 检查SkinnedMeshRenderer组件
            if (showMaterialRenderers)
            {
                var skinnedMeshRenderer = obj.GetComponent<SkinnedMeshRenderer>();
                if (skinnedMeshRenderer != null)
                {
                    var materials = skinnedMeshRenderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] != null)
                        {
                            var materialInfo = new MaterialPreviewInfo
                            {
                                gameObject = obj,
                                material = materials[i],
                                componentType = "SkinnedMeshRenderer",
                                hierarchyPath = GetHierarchyPath(obj, rootName),
                                additionalInfo = materials.Length > 1 ? $"材质[{i}]" : "材质",
                                materialIndex = i
                            };
                            allMaterials.Add(materialInfo);
                        }
                    }
                }
            }
            
            // 递归处理子对象
            foreach (Transform child in obj.transform)
            {
                CollectMaterialsFromGameObject(child.gameObject, rootName);
            }
        }

        /// <summary>
        /// 应用Material变更
        /// </summary>
        private void ApplyMaterialChange(MaterialPreviewInfo materialInfo, Material newMaterial)
        {
            var gameObject = materialInfo.gameObject;
            if (gameObject == null) return;
            

            // 根据组件类型应用变更
            if (materialInfo.componentType.Contains("粒子系统"))
            {
                var particleSystem = gameObject.GetComponent<ParticleSystem>();
                var renderer = particleSystem?.GetComponent<ParticleSystemRenderer>();
                
                if (renderer != null)
                {
                    Undo.RecordObject(renderer, "Change Particle System Material");
                    
                    if (materialInfo.componentType.Contains("拖尾"))
                    {
                        renderer.trailMaterial = newMaterial;
                    }
                    else
                    {
                        // 处理材质数组
                        if (materialInfo.materialIndex >= 0)
                        {
                            var materials = renderer.sharedMaterials;
                            if (materialInfo.materialIndex < materials.Length)
                            {
                                // 创建材质数组的副本
                                var newMaterials = new Material[materials.Length];
                                for (int i = 0; i < materials.Length; i++)
                                {
                                    newMaterials[i] = materials[i];
                                }
                                newMaterials[materialInfo.materialIndex] = newMaterial;
                                renderer.sharedMaterials = newMaterials;
                            }
                        }
                        else
                        {
                            renderer.sharedMaterial = newMaterial;
                        }
                    }
                    
                    EditorUtility.SetDirty(renderer);
                    
                    // 更新预览信息中的材质引用
                    materialInfo.material = newMaterial;
                }
                else
                {
                    Debug.LogError("ParticleSystemRenderer is null!");
                }
            }
            else if (materialInfo.componentType.Contains("MeshRenderer"))
            {
                var meshRenderer = gameObject.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    Undo.RecordObject(meshRenderer, "Change MeshRenderer Material");
                    
                    // 处理材质数组
                    if (materialInfo.materialIndex >= 0)
                    {
                        var materials = meshRenderer.sharedMaterials;
                        if (materialInfo.materialIndex < materials.Length)
                        {
                            // 创建材质数组的副本
                            var newMaterials = new Material[materials.Length];
                            for (int i = 0; i < materials.Length; i++)
                            {
                                newMaterials[i] = materials[i];
                            }
                            newMaterials[materialInfo.materialIndex] = newMaterial;
                            meshRenderer.sharedMaterials = newMaterials;
                        }
                    }
                    else
                    {
                        meshRenderer.sharedMaterial = newMaterial;
                    }
                    
                    EditorUtility.SetDirty(meshRenderer);
                    
                    // 更新预览信息中的材质引用
                    materialInfo.material = newMaterial;
                }
            }
            else if (materialInfo.componentType.Contains("SkinnedMesh"))
            {
                var skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
                if (skinnedMeshRenderer != null)
                {
                    Undo.RecordObject(skinnedMeshRenderer, "Change SkinnedMeshRenderer Material");
                    
                    // 处理材质数组
                    if (materialInfo.materialIndex >= 0)
                    {
                        var materials = skinnedMeshRenderer.sharedMaterials;
                        if (materialInfo.materialIndex < materials.Length)
                        {
                            // 创建材质数组的副本
                            var newMaterials = new Material[materials.Length];
                            for (int i = 0; i < materials.Length; i++)
                            {
                                newMaterials[i] = materials[i];
                            }
                            newMaterials[materialInfo.materialIndex] = newMaterial;
                            skinnedMeshRenderer.sharedMaterials = newMaterials;
                        }
                    }
                    else
                    {
                        skinnedMeshRenderer.sharedMaterial = newMaterial;
                    }
                    
                    EditorUtility.SetDirty(skinnedMeshRenderer);
                    
                    // 更新预览信息中的材质引用
                    materialInfo.material = newMaterial;
                }
            }
        }


        
        /// <summary>
        /// 绘制Material统计信息
        /// </summary>
        private void DrawMaterialStatistics(Material material, MaterialStatistics stats)
        {
            // 应用外部资源过滤
            if (showOnlyExternalReferences)
            {
                string materialPath = material != null ? AssetDatabase.GetAssetPath(material) : "";
                if (!IsResourceOutsidePrefabDirectory(materialPath))
                {
                    return; // 不是外部资源，不显示
                }
            }
            
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.BeginHorizontal();
            
            // Material信息（可编辑）
            var newMaterial = (Material)EditorGUILayout.ObjectField("", material, typeof(Material), false, GUILayout.Width(200));
            
            // 检查Material是否被替换
            if (newMaterial != material)
            {
                ReplaceMaterialInReferences(material, newMaterial, stats);
            }
            
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField($"引用次数: {stats.referenceCount}", EditorStyles.boldLabel);
            if (showDetailedInfo)
            {
                string currentMaterialPath = material != null ? AssetDatabase.GetAssetPath(material) : "未知";
                
                // 检查资源是否在预制体目录外
                bool isOutsidePrefabDirectory = IsResourceOutsidePrefabDirectory(currentMaterialPath);
                
                if (isOutsidePrefabDirectory)
                {
                    var originalColor = GUI.color;
                    GUI.color = Color.red;
                    var style = new GUIStyle(EditorStyles.miniLabel);
                    style.normal.textColor = Color.red;
                    EditorGUILayout.LabelField($"⚠ 路径: {currentMaterialPath}", style);
                    GUI.color = originalColor;
                }
                else
                {
                    EditorGUILayout.LabelField($"路径: {currentMaterialPath}");
                }
                
                EditorGUILayout.LabelField(GetMaterialInfo(material));
            }
            EditorGUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("选择替换", GUILayout.Width(80)))
            {
                selectedOriginalMaterial = material;
                selectedTab = 1;
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 引用详情
            if (showDetailedInfo)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("引用详情:");
                foreach (var reference in stats.references)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    EditorGUILayout.ObjectField("", reference.component, typeof(Component), true, GUILayout.Width(200));
                    
                    // 为不同类型使用不同颜色
                    var originalColor = GUI.color;
                    if (reference.referenceType == MaterialReferenceType.ParticleSystemRenderer)
                    {
                        GUI.color = new Color(1f, 0.8f, 1f); // 淡紫色
                    }
                    else if (reference.referenceType == MaterialReferenceType.TrailRenderer)
                    {
                        GUI.color = new Color(1f, 0.9f, 0.8f); // 淡橙色
                    }
                    
                    EditorGUILayout.LabelField($"[{reference.GetReferenceTypeName()}] {reference.objectPath}");
                    GUI.color = originalColor;
                    
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// 获取Material信息字符串
        /// </summary>
        private string GetMaterialInfo(Material material)
        {
            if (material == null) return "Material为空";
            
            var parts = new System.Collections.Generic.List<string>();
            
            parts.Add("Shader: " + (material.shader != null ? material.shader.name : "无"));
            parts.Add("渲染队列: " + material.renderQueue.ToString());
            
            if (material.HasProperty("_MainTex"))
            {
                var mainTex = material.GetTexture("_MainTex");
                parts.Add("主纹理: " + (mainTex != null ? mainTex.name : "无"));
            }
            
            if (material.HasProperty("_Color"))
            {
                var color = material.GetColor("_Color");
                parts.Add("主颜色: " + color.ToString());
            }
            
            return string.Join(System.Environment.NewLine, parts);
        }
        
        /// <summary>
        /// 在引用统计中替换Mesh
        /// </summary>
        private void ReplaceMeshInReferences(Mesh oldMesh, Mesh newMesh, MeshStatistics stats)
        {
            if (stats == null || stats.references == null) return;
            
            int replacedCount = 0;
            
            foreach (var reference in stats.references)
            {
                try
                {
                    if (reference.referenceType == MeshReferenceType.ParticleSystem && reference.particleSystem != null)
                    {
                        var shapeModule = reference.particleSystem.shape;
                        if (shapeModule.mesh == oldMesh)
                        {
                            var serializedObject = new SerializedObject(reference.particleSystem);
                            var shapeProperty = serializedObject.FindProperty("ShapeModule");
                            var meshProperty = shapeProperty.FindPropertyRelative("m_Mesh");
                            meshProperty.objectReferenceValue = newMesh;
                            serializedObject.ApplyModifiedProperties();
                            EditorUtility.SetDirty(reference.particleSystem);
                            replacedCount++;
                        }
                    }
                    else if (reference.meshFilter != null)
                    {
                        if (reference.meshFilter.sharedMesh == oldMesh)
                        {
                            reference.meshFilter.sharedMesh = newMesh;
                            EditorUtility.SetDirty(reference.meshFilter);
                            replacedCount++;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"替换Mesh时出错: {e.Message}");
                }
            }
            
            if (replacedCount > 0)
            {
                var oldName = oldMesh != null ? oldMesh.name : "null";
                var newName = newMesh != null ? newMesh.name : "null";
                Debug.Log($"已将 {replacedCount} 个引用从 '{oldName}' 替换为 '{newName}'");
                
                // 强制刷新资源数据库（自动刷新，无需用户手动点击）
                // ForceRefreshAll已经包含了RefreshStatistics、RefreshMeshPreview和Repaint
                ForceRefreshAll();
            }
        }
        
        /// <summary>
        /// 在引用统计中替换Material
        /// </summary>
        private void ReplaceMaterialInReferences(Material oldMaterial, Material newMaterial, MaterialStatistics stats)
        {
            if (stats == null || stats.references == null) return;
            
            int replacedCount = 0;
            
            foreach (var reference in stats.references)
            {
                try
                {
                    if (reference.component != null)
                    {
                        Renderer renderer = reference.component as Renderer;
                        if (renderer != null)
                        {
                            var materials = renderer.sharedMaterials;
                            bool changed = false;
                            
                            for (int i = 0; i < materials.Length; i++)
                            {
                                if (materials[i] == oldMaterial)
                                {
                                    materials[i] = newMaterial;
                                    changed = true;
                                }
                            }
                            
                            if (changed)
                            {
                                renderer.sharedMaterials = materials;
                                EditorUtility.SetDirty(renderer);
                                replacedCount++;
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"替换Material时出错: {e.Message}");
                }
            }
            
            if (replacedCount > 0)
            {
                var oldName = oldMaterial != null ? oldMaterial.name : "null";
                var newName = newMaterial != null ? newMaterial.name : "null";
                Debug.Log($"已将 {replacedCount} 个引用从 '{oldName}' 替换为 '{newName}'");
                
                // 强制刷新资源数据库（自动刷新，无需用户手动点击）
                // ForceRefreshAll已经包含了RefreshStatistics、RefreshMaterialPreview、RefreshTexturePreview和Repaint
                ForceRefreshAll();
            }
        }
        
        /// <summary>
        /// 刷新可用Material列表
        /// </summary>
        private void RefreshAvailableMaterials()
        {
            availableMaterials.Clear();
            
            // 查找项目中的所有Material
            var guids = AssetDatabase.FindAssets("t:Material");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null)
                {
                    availableMaterials.Add(material);
                }
            }
            
            // 按名称排序
            availableMaterials.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// 绘制Material选择器
        /// </summary>
        private void DrawMaterialSelector()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Material选择器", EditorStyles.boldLabel);
            
            // 搜索框
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("搜索:", GUILayout.Width(50));
            var newSearchTerm = EditorGUILayout.TextField(materialSearchTerm);
            if (newSearchTerm != materialSearchTerm)
            {
                materialSearchTerm = newSearchTerm;
                RefreshAvailableMaterials();
            }
            EditorGUILayout.EndHorizontal();
            
            // Material列表
            replacementScrollPos = EditorGUILayout.BeginScrollView(replacementScrollPos, GUILayout.Height(200));
            
            var filteredMaterials = string.IsNullOrEmpty(materialSearchTerm) 
                ? availableMaterials 
                : availableMaterials.Where(m => m.name.ToLower().Contains(materialSearchTerm.ToLower())).ToList();
            
            foreach (var material in filteredMaterials)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(material.name, EditorStyles.label))
                {
                    selectedNewMaterial = material;
                    showMaterialSelector = false;
                }
                EditorGUILayout.LabelField(GetMaterialInfo(material));
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// 绘制Material替换目标
        /// </summary>
        private void DrawMaterialReplacementTargets()
        {
            var stats = materialStatistics[selectedOriginalMaterial];
            
            // 清理无效的选择状态（引用对象已不存在的情况）
            var validReferences = new HashSet<MaterialReference>();
            foreach (var reference in stats.references)
            {
                validReferences.Add(reference);
            }
            
            // 移除不在当前引用列表中的选择项
            var keysToRemove = new List<MaterialReference>();
            foreach (var kvp in materialReplaceSelections)
            {
                if (!validReferences.Contains(kvp.Key))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in keysToRemove)
            {
                materialReplaceSelections.Remove(key);
            }
            
            EditorGUILayout.LabelField($"替换目标 (共{stats.references.Count}个):", EditorStyles.boldLabel);
            
            // 颜色图例
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("颜色图例:", EditorStyles.miniLabel, GUILayout.Width(60));
            
            var originalColor = GUI.color;
            var originalBgColor = GUI.backgroundColor;
            
            GUI.backgroundColor = new Color(1f, 0.7f, 1f);
            GUI.color = new Color(0.6f, 0.2f, 0.6f);
            EditorGUILayout.LabelField("[粒子渲染器]", EditorStyles.helpBox, GUILayout.Width(90));
            
            GUI.backgroundColor = new Color(1f, 0.9f, 0.7f);
            GUI.color = new Color(0.8f, 0.5f, 0.2f);
            EditorGUILayout.LabelField("[拖尾渲染器]", EditorStyles.helpBox, GUILayout.Width(90));
            
            GUI.color = originalColor;
            GUI.backgroundColor = originalBgColor;
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全选"))
            {
                foreach (var reference in stats.references)
                {
                    materialReplaceSelections[reference] = true;
                }
            }
            if (GUILayout.Button("反选"))
            {
                foreach (var reference in stats.references)
                {
                    var currentValue = materialReplaceSelections.ContainsKey(reference) ? materialReplaceSelections[reference] : false;
                    materialReplaceSelections[reference] = !currentValue;
                }
            }
            if (GUILayout.Button("清空"))
            {
                materialReplaceSelections.Clear();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            
            // 显示目标列表
            foreach (var reference in stats.references)
            {
                EditorGUILayout.BeginHorizontal();
                
                var isSelected = materialReplaceSelections.ContainsKey(reference) ? materialReplaceSelections[reference] : false;
                var newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
                materialReplaceSelections[reference] = newSelected;
                
                EditorGUILayout.ObjectField("", reference.component, typeof(Component), true, GUILayout.Width(200));
                
                // 为不同类型使用不同颜色
                var originalColor2 = GUI.color;
                if (reference.referenceType == MaterialReferenceType.ParticleSystemRenderer)
                {
                    GUI.color = new Color(1f, 0.8f, 1f); // 淡紫色
                }
                else if (reference.referenceType == MaterialReferenceType.TrailRenderer)
                {
                    GUI.color = new Color(1f, 0.9f, 0.8f); // 淡橙色
                }
                
                EditorGUILayout.LabelField($"[{reference.GetReferenceTypeName()}] {reference.objectPath}");
                GUI.color = originalColor2;
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        /// <summary>
        /// 获取选中的Material引用
        /// </summary>
        private List<MaterialReference> GetSelectedMaterialReferences()
        {
            return materialReplaceSelections.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
        }
        
        /// <summary>
        /// 执行Mesh替换
        /// </summary>
        private void ExecuteMeshReplacement()
        {
            var selectedReferences = GetSelectedReferences();
            if (selectedReferences.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "请选择要替换的目标", "确定");
                return;
            }
            
            // 记录原始Mesh，用于后续从选择列表中移除
            var originalMesh = selectedOriginalMesh;
            
            var operation = ParticleMeshReplacer.ReplaceMesh(selectedOriginalMesh, selectedNewMesh, selectedReferences, preserveProperties);
            if (operation != null)
            {
                // 强制刷新资源数据库和统计数据（自动刷新，无需用户手动点击）
                // ForceRefreshAll已经包含了RefreshStatistics等所有刷新操作
                ForceRefreshAll();
                
                EditorUtility.DisplayDialog("成功", $"已替换 {operation.affectedReferences.Count} 个粒子系统中的Mesh", "确定");
                
                // 如果启用了"只显示外部资源"，检查原始Mesh是否还有外部引用
                // 如果没有外部引用了，从选择列表中清除
                if (showOnlyExternalReferences && meshStatistics.ContainsKey(originalMesh))
                {
                    var stats = meshStatistics[originalMesh];
                    bool hasExternalReferences = stats.references.Any(r => 
                    {
                        var meshPath = AssetDatabase.GetAssetPath(originalMesh);
                        return IsResourceOutsidePrefabDirectory(meshPath);
                    });
                    
                    if (!hasExternalReferences)
                    {
                        // 清除该Mesh的选择状态
                        replaceSelections.Clear();
                        selectedOriginalMesh = null;
                    }
                }
                else if (!meshStatistics.ContainsKey(originalMesh))
                {
                    // 如果Mesh已经没有引用了，清除选择
                    replaceSelections.Clear();
                    selectedOriginalMesh = null;
                }
                
                // 强制重绘界面
                Repaint();
            }
        }
        
        /// <summary>
        /// 执行Material替换
        /// </summary>
        private void ExecuteMaterialReplacement()
        {
            var selectedReferences = GetSelectedMaterialReferences();
            if (selectedReferences.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "请选择要替换的目标", "确定");
                return;
            }
            
            // 验证选择的引用是否仍然有效
            var validReferences = new List<MaterialReference>();
            if (materialStatistics.ContainsKey(selectedOriginalMaterial))
            {
                var currentReferences = materialStatistics[selectedOriginalMaterial].references;
                foreach (var selectedRef in selectedReferences)
                {
                    if (currentReferences.Any(r => r.component == selectedRef.component && 
                                                  r.referenceType == selectedRef.referenceType))
                    {
                        validReferences.Add(selectedRef);
                    }
                }
            }
            
            if (validReferences.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "选择的引用已失效，请重新选择", "确定");
                materialReplaceSelections.Clear();
                return;
            }
            
            if (validReferences.Count != selectedReferences.Count)
            {
                Debug.LogWarning($"部分引用已失效，将只替换 {validReferences.Count} 个有效引用");
            }
            
            int successCount = 0;
            
            foreach (var reference in validReferences)
            {
                try
                {
                    Undo.RecordObject(reference.component, "Replace Material");
                    
                    if (reference.referenceType == MaterialReferenceType.ParticleSystemRenderer)
                    {
                        var renderer = reference.component as ParticleSystemRenderer;
                        if (renderer != null)
                        {
                            renderer.material = selectedNewMaterial;
                            EditorUtility.SetDirty(renderer);
                            successCount++;
                        }
                    }
                    else if (reference.referenceType == MaterialReferenceType.TrailRenderer)
                    {
                        var renderer = reference.component as ParticleSystemRenderer;
                        if (renderer != null)
                        {
                            renderer.trailMaterial = selectedNewMaterial;
                            EditorUtility.SetDirty(renderer);
                            successCount++;
                        }
                    }
                    else if (reference.referenceType == MaterialReferenceType.MeshRenderer)
                    {
                        var renderer = reference.component as MeshRenderer;
                        if (renderer != null)
                        {
                            renderer.material = selectedNewMaterial;
                            EditorUtility.SetDirty(renderer);
                            successCount++;
                        }
                    }
                    else if (reference.referenceType == MaterialReferenceType.SkinnedMeshRenderer)
                    {
                        var renderer = reference.component as SkinnedMeshRenderer;
                        if (renderer != null)
                        {
                            renderer.material = selectedNewMaterial;
                            EditorUtility.SetDirty(renderer);
                            successCount++;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"替换Material时出现异常: {e.Message}");
                }
            }
            
            if (successCount > 0)
            {
                // 记录原始Material，用于后续从选择列表中移除
                var originalMaterial = selectedOriginalMaterial;
                
                // 强制刷新资源数据库和统计数据（自动刷新，无需用户手动点击）
                // ForceRefreshAll已经包含了RefreshStatistics等所有刷新操作
                ForceRefreshAll();
                
                EditorUtility.DisplayDialog("成功", $"已替换 {successCount} 个组件中的Material", "确定");
                
                // 如果启用了"只显示外部资源"，检查原始Material是否还有外部引用
                // 如果没有外部引用了，从选择列表中清除
                if (showOnlyExternalReferences && materialStatistics.ContainsKey(originalMaterial))
                {
                    var stats = materialStatistics[originalMaterial];
                    bool hasExternalReferences = stats.references.Any(r => 
                    {
                        var materialPath = AssetDatabase.GetAssetPath(originalMaterial);
                        return IsResourceOutsidePrefabDirectory(materialPath);
                    });
                    
                    if (!hasExternalReferences)
                    {
                        // 清除该Material的选择状态
                        materialReplaceSelections.Clear();
                        selectedOriginalMaterial = null;
                    }
                }
                else if (!materialStatistics.ContainsKey(originalMaterial))
                {
                    // 如果Material已经没有引用了，清除选择
                    materialReplaceSelections.Clear();
                    selectedOriginalMaterial = null;
                }
                
                // 强制重绘界面
                Repaint();
            }
        }
        
        /// <summary>
        /// 绘制贴图预览标签页
        /// </summary>
        private void DrawTexturePreviewTab()
        {
            EditorGUILayout.LabelField("贴图预览", EditorStyles.boldLabel);
            
            // 检查贴图类型检测开关
            if (!showTextureType)
            {
                EditorGUILayout.HelpBox("贴图类型检测已关闭。请在工具栏中启用\"贴图类型\"开关或在设置面板中启用\"贴图数据类型检测\"。", MessageType.Info);
                return;
            }
            
            // 获取选中对象
            var selectedObjects = lockAllData && lockedSelectionObjects.Length > 0
                ? lockedSelectionObjects
                : Selection.gameObjects;
            var hasSelection = selectedObjects != null && selectedObjects.Length > 0;
            
            // 显示锁定状态
            if (lockAllData)
            {
                EditorGUILayout.HelpBox("数据已锁定。当前显示的是锁定时的数据，不会随选中对象变化而更新。点击工具栏中的\"锁定\"按钮可解除锁定。", MessageType.Info);
            }
            else if (!hasSelection)
            {
                EditorGUILayout.HelpBox("请在Hierarchy面板中选择要预览的GameObject", MessageType.Warning);
                return;
            }
            
            EditorGUILayout.Space();
            
            // 显示当前选中的对象信息（锁定状态下也显示，但提示是锁定时的数据）
            if (hasSelection)
            {
                var summary = lockAllData && !string.IsNullOrEmpty(lockedSelectionSummary)
                    ? lockedSelectionSummary
                    : string.Join(", ", selectedObjects.Take(3).Select(obj => obj.name)) + (selectedObjects.Length > 3 ? " ..." : "");
                EditorGUILayout.LabelField($"预览对象: {summary}", EditorStyles.helpBox);
            }
            else if (lockAllData)
            {
                EditorGUILayout.LabelField("数据已锁定 - 当前无选中对象", EditorStyles.helpBox);
            }
            
            // 过滤选项
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("过滤选项", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            // 贴图类型过滤
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("贴图类型:", EditorStyles.miniLabel);
            showMainTextures = EditorGUILayout.Toggle("主纹理 (_MainTex)", showMainTextures);
            showNormalMaps = EditorGUILayout.Toggle("法线贴图 (_BumpMap)", showNormalMaps);
            showEmissionMaps = EditorGUILayout.Toggle("自发光贴图 (_EmissionMap)", showEmissionMaps);
            showOtherTextures = EditorGUILayout.Toggle("其他贴图", showOtherTextures);
            showMissingTextures = EditorGUILayout.Toggle("缺失贴图", showMissingTextures);
            
            GUILayout.Space(5);
            
            // 外部资源过滤
            var originalColor = GUI.color;
            GUI.color = showOnlyExternalResources ? new Color(1f, 0.5f, 0.5f) : Color.white;
            showOnlyExternalResources = EditorGUILayout.Toggle("⚠ 只显示外部资源", showOnlyExternalResources);
            GUI.color = originalColor;
            
            if (showOnlyExternalResources)
            {
                EditorGUILayout.HelpBox("已启用：只显示路径在预制体平级目录外的资源", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(20);
            
            // 显示选项
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("显示选项:", EditorStyles.miniLabel);
            var newGroupByTexture = EditorGUILayout.Toggle("按贴图分组", groupByTexture);
            var newShowTextureDetails = EditorGUILayout.Toggle("显示贴图详情", showTextureDetails);
            
            if (newGroupByTexture != groupByTexture || newShowTextureDetails != showTextureDetails)
            {
                groupByTexture = newGroupByTexture;
                showTextureDetails = newShowTextureDetails;
                needsRefreshTexturePreview = true;
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            // 搜索框
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("搜索:", GUILayout.Width(40));
            var newSearchFilter = EditorGUILayout.TextField(textureSearchFilter, GUILayout.Width(200));
            if (newSearchFilter != textureSearchFilter)
            {
                textureSearchFilter = newSearchFilter;
                needsRefreshTexturePreview = true;
            }
            
            // 排序选项
            EditorGUILayout.LabelField("排序:", GUILayout.Width(40));
            var newSortMode = (TextureSortMode)EditorGUILayout.EnumPopup(textureSortMode, GUILayout.Width(120));
            if (newSortMode != textureSortMode)
            {
                textureSortMode = newSortMode;
                needsRefreshTexturePreview = true;
            }
            
            if (GUILayout.Button("刷新", GUILayout.Width(60)))
            {
                RefreshAllDataUnified();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // 材质复制功能（贴图预览）
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("材质复制", EditorStyles.boldLabel);
            
            // 目标文件夹选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("目标文件夹:", GUILayout.Width(80));
            
            EditorGUI.BeginDisabledGroup(lockTextureCopyFolder);
            textureCopyTargetFolder = EditorGUILayout.TextField(textureCopyTargetFolder);
            EditorGUI.EndDisabledGroup();
            
            // 从Project选择文件夹按钮
            if (GUILayout.Button("从Project选择", GUILayout.Width(100)))
            {
                if (!lockTextureCopyFolder)
                {
                    var selectedFolder = GetSelectedFolderFromProject();
                    if (!string.IsNullOrEmpty(selectedFolder))
                    {
                        textureCopyTargetFolder = selectedFolder;
                    }
                }
            }
            
            // 锁定按钮
            Color originalBgColor = GUI.backgroundColor;
            GUI.backgroundColor = lockTextureCopyFolder ? new Color(1f, 0.3f, 0.3f) : Color.gray;
            if (GUILayout.Button(lockTextureCopyFolder ? "🔒" : "🔓", GUILayout.Width(30)))
            {
                lockTextureCopyFolder = !lockTextureCopyFolder;
            }
            GUI.backgroundColor = originalBgColor;
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox("在下方材质列表中点击\"复制\"按钮，可将材质复制到指定文件夹", MessageType.Info);
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // 统计信息（使用缓存数据避免OnGUI中的LINQ计算）
            var totalTextures = allTextures.Count;
            var uniqueTextures = textureGroups.Count;
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"总贴图引用: {totalTextures}", EditorStyles.helpBox);
            EditorGUILayout.LabelField($"唯一贴图: {uniqueTextures}", EditorStyles.helpBox);
            EditorGUILayout.LabelField($"缺失贴图: {_cachedMissingTextureCount}", EditorStyles.helpBox);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            if (allTextures.Count == 0)
            {
                EditorGUILayout.HelpBox("在选中的GameObject及其子对象中未发现贴图引用", MessageType.Info);
                return;
            }
            
            // 贴图列表
            texturePreviewScrollPos = EditorGUILayout.BeginScrollView(texturePreviewScrollPos);
            
            if (groupByTexture)
            {
                DrawGroupedTextureList();
            }
            else
            {
                DrawFlatTextureList();
            }
            
            EditorGUILayout.EndScrollView();
            
            // 处理延迟刷新
            if (needsRefreshTexturePreview)
            {
                needsRefreshTexturePreview = false;
                RefreshAllDataUnified();
            }
        }
        
        /// <summary>
        /// 绘制分组的贴图列表 - 按材质分组显示贴图
        /// </summary>
        private void DrawGroupedTextureList()
        {
            // 按材质分组
            var sourceItems = allTextures;
            var materialGroups = sourceItems.Where(ShouldShowTextureItem)
                .GroupBy(item => item.material)
                .OrderBy(group => group.Key != null ? group.Key.name : "缺失材质")
                .ToList();
            
            foreach (var materialGroup in materialGroups)
            {
                var material = materialGroup.Key;
                var textureItems = materialGroup.ToList();
                
                EditorGUILayout.BeginVertical("box");
                
                // 材质头部信息
                DrawMaterialGroupHeader(material, textureItems);
                
                // 贴图列表
                EditorGUI.indentLevel++;
                foreach (var item in textureItems)
                {
                    DrawTextureItem(item, true);
                }
                EditorGUI.indentLevel--;
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }
        
        /// <summary>
        /// 绘制平铺的贴图列表
        /// </summary>
        private void DrawFlatTextureList()
        {
            var sourceItems = allTextures;
            var filteredItems = sourceItems.Where(ShouldShowTextureItem).ToList();
            
            foreach (var item in filteredItems)
            {
                DrawTextureItem(item, true);
            }
        }
        
        /// <summary>
        /// 绘制材质组头部
        /// </summary>
        private void DrawMaterialGroupHeader(Material material, List<TexturePreviewItem> items)
        {
            EditorGUILayout.BeginHorizontal();
            
            // 材质预览
            if (material != null)
            {
                var rect = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));
                var previewTexture = AssetPreview.GetAssetPreview(material);
                if (previewTexture == null)
                {
                    var assetPath = AssetDatabase.GetAssetPath(material);
                    var linkedTexture = MaterialCopyUtility.TryGetPrimaryTexture(material);
                    previewTexture = linkedTexture as Texture2D;
                    if (previewTexture == null)
                    {
                        previewTexture = AssetPreview.GetMiniThumbnail(material);
                    }
                }
                EditorGUI.DrawPreviewTexture(rect, previewTexture ?? Texture2D.whiteTexture);
            }
            else
            {
                var rect = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));
                EditorGUI.DrawRect(rect, Color.gray);
                GUI.Label(rect, "缺失", EditorStyles.centeredGreyMiniLabel);
            }
            
            EditorGUILayout.BeginVertical();
            
            // 材质名称和路径
            var materialName = material != null ? material.name : "缺失材质";
            EditorGUILayout.LabelField(materialName, EditorStyles.boldLabel);
            
            if (material != null && showTextureDetails)
            {
                var assetPath = AssetDatabase.GetAssetPath(material);
                
                // 检查资源是否在预制体目录外
                bool isOutsidePrefabDirectory = IsResourceOutsidePrefabDirectory(assetPath);
                
                if (isOutsidePrefabDirectory)
                {
                    var originalColor = GUI.color;
                    GUI.color = Color.red;
                    var style = new GUIStyle(EditorStyles.miniLabel);
                    style.normal.textColor = Color.red;
                    EditorGUILayout.LabelField($"⚠ 路径: {assetPath}", style);
                    GUI.color = originalColor;
                }
                else
                {
                    EditorGUILayout.LabelField($"路径: {assetPath}", EditorStyles.miniLabel);
                }
                
                // 材质shader信息
                if (material.shader != null)
                {
                    EditorGUILayout.LabelField($"Shader: {material.shader.name}", EditorStyles.miniLabel);
                }
            }
            
            // 贴图统计
            EditorGUILayout.LabelField($"贴图数量: {items.Count}", EditorStyles.helpBox);
            
            EditorGUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
            
            // 操作按钮
            EditorGUILayout.BeginVertical();
            if (material != null)
            {
                if (GUILayout.Button("选择材质", GUILayout.Width(80)))
                {
                    Selection.activeObject = material;
                    EditorGUIUtility.PingObject(material);
                }
                
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制贴图组头部
        /// </summary>
        private void DrawTextureGroupHeader(Texture texture, List<TexturePreviewItem> items)
        {
            EditorGUILayout.BeginHorizontal();
            
            // 贴图预览
            if (texture != null)
            {
                var rect = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));
                EditorGUI.DrawPreviewTexture(rect, texture);
            }
            else
            {
                var rect = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));
                EditorGUI.DrawRect(rect, Color.gray);
                GUI.Label(rect, "缺失", EditorStyles.centeredGreyMiniLabel);
            }
            
            EditorGUILayout.BeginVertical();
            
            // 贴图名称和路径
            var textureName = texture != null ? texture.name : "缺失贴图";
            EditorGUILayout.LabelField(textureName, EditorStyles.boldLabel);
            
            if (texture != null && showTextureDetails)
            {
                var assetPath = AssetDatabase.GetAssetPath(texture);
                
                // 检查资源是否在预制体目录外
                bool isOutsidePrefabDirectory = IsResourceOutsidePrefabDirectory(assetPath);
                
                if (isOutsidePrefabDirectory)
                {
                    var originalColor = GUI.color;
                    GUI.color = Color.red;
                    var style = new GUIStyle(EditorStyles.miniLabel);
                    style.normal.textColor = Color.red;
                    EditorGUILayout.LabelField($"⚠ 路径: {assetPath}", style);
                    GUI.color = originalColor;
                }
                else
                {
                    EditorGUILayout.LabelField($"路径: {assetPath}", EditorStyles.miniLabel);
                }
                
                // 贴图详细信息
                var textureInfo = GetTextureInfo(texture);
                EditorGUILayout.LabelField(textureInfo, EditorStyles.miniLabel);
            }
            
            // 引用统计
            EditorGUILayout.LabelField($"引用次数: {items.Count}", EditorStyles.helpBox);
            
            EditorGUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
            
            // 操作按钮
            EditorGUILayout.BeginVertical();
            if (texture != null)
            {
                if (GUILayout.Button("选择贴图", GUILayout.Width(80)))
                {
                    Selection.activeObject = texture;
                    EditorGUIUtility.PingObject(texture);
                }
                
                EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(textureCopyTargetFolder));
                if (GUILayout.Button("复制贴图", GUILayout.Width(80)))
                {
                    // 创建临时贴图项用于替换
                    var tempItem = new TexturePreviewItem
                    {
                        material = null,
                        texture = texture,
                        propertyName = "_MainTex"
                    };
                    ReplaceTexture(tempItem, null);
                }
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
        }
        
        /// <summary>
        /// 绘制单个贴图项
        /// </summary>
        private void DrawTextureItem(TexturePreviewItem item, bool showTexturePreview)
        {
            EditorGUILayout.BeginHorizontal();
            
            if (showTexturePreview)
            {
                // 贴图预览
                if (item.texture != null)
                {
                    var rect = GUILayoutUtility.GetRect(32, 32, GUILayout.Width(32), GUILayout.Height(32));
                    EditorGUI.DrawPreviewTexture(rect, item.texture);
                }
                else
                {
                    var rect = GUILayoutUtility.GetRect(32, 32, GUILayout.Width(32), GUILayout.Height(32));
                    EditorGUI.DrawRect(rect, Color.gray);
                    GUI.Label(rect, "缺失", EditorStyles.centeredGreyMiniLabel);
                }
            }
            
            // 贴图对象引用（支持拖拽替换）
            var textureName = item.texture != null ? item.texture.name : "缺失贴图";
            var newTexture = (Texture)EditorGUILayout.ObjectField(textureName, item.texture, typeof(Texture), false, GUILayout.Width(200));
            
            // 检查贴图是否被替换
            if (newTexture != item.texture)
            {
                ReplaceTexture(item, newTexture);
            }
            
            // 属性名称和类型标签（显示UI名称）
            DrawTexturePropertyTypeLabel(item.propertyName, item.textureType, item.material);
            
            // 贴图详细信息（包含路径）
            if (showTextureDetails && item.texture != null)
            {
                var textureInfo = GetTextureInfo(item.texture);
                var assetPath = AssetDatabase.GetAssetPath(item.texture);
                
                // 检查资源是否在预制体目录外
                bool isOutsidePrefabDirectory = IsResourceOutsidePrefabDirectory(assetPath);
                
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(textureInfo, EditorStyles.miniLabel, GUILayout.Width(300));
                
                if (isOutsidePrefabDirectory)
                {
                    var originalColor = GUI.color;
                    GUI.color = Color.red;
                    var style = new GUIStyle(EditorStyles.miniLabel);
                    style.normal.textColor = Color.red;
                    EditorGUILayout.LabelField($"⚠ 路径: {assetPath}", style, GUILayout.Width(300));
                    GUI.color = originalColor;
                }
                else
                {
                    EditorGUILayout.LabelField($"路径: {assetPath}", EditorStyles.miniLabel, GUILayout.Width(300));
                }
                
                EditorGUILayout.EndVertical();
            }
            
            // 移除GameObject引用选择按钮（根据需求1）
            // EditorGUILayout.ObjectField("", item.gameObject, typeof(GameObject), true, GUILayout.Width(120));
            
            // 操作按钮
            if (item.texture != null)
            {
                if (GUILayout.Button("选择", GUILayout.Width(50)))
                {
                    Selection.activeObject = item.texture;
                    EditorGUIUtility.PingObject(item.texture);
                }
                
                EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(textureCopyTargetFolder));
                if (GUILayout.Button("复制贴图", GUILayout.Width(70)))
                {
                    ReplaceTexture(item, null); // 使用ReplaceTexture方法自动替换材质引用
                }
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                GUILayout.Button("选择", GUILayout.Width(50));
                GUILayout.Button("复制贴图", GUILayout.Width(70));
                EditorGUI.EndDisabledGroup();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        /// <summary>
        /// 绘制贴图属性类型标签
        /// </summary>
        private void DrawTexturePropertyTypeLabel(string propertyName, TextureType textureType, Material material)
        {
            var originalColor = GUI.color;
            var originalBgColor = GUI.backgroundColor;
            
            // 根据贴图类型设置颜色
            switch (textureType)
            {
                case TextureType.MainTexture:
                    GUI.backgroundColor = new Color(0.7f, 1f, 0.7f); // 绿色
                    GUI.color = new Color(0.2f, 0.6f, 0.2f);
                    break;
                case TextureType.NormalMap:
                    GUI.backgroundColor = new Color(0.7f, 0.8f, 1f); // 蓝色
                    GUI.color = new Color(0.2f, 0.3f, 0.8f);
                    break;
                case TextureType.EmissionMap:
                    GUI.backgroundColor = new Color(1f, 1f, 0.7f); // 黄色
                    GUI.color = new Color(0.8f, 0.6f, 0.1f);
                    break;
                case TextureType.Other:
                    GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f); // 灰色
                    GUI.color = new Color(0.4f, 0.4f, 0.4f);
                    break;
            }
            
            // 获取UI显示名称
            var displayName = GetPropertyDisplayName(propertyName, material);
            EditorGUILayout.LabelField($"[{displayName}]", EditorStyles.helpBox, GUILayout.Width(120));
            
            GUI.color = originalColor;
            GUI.backgroundColor = originalBgColor;
        }
        
        /// <summary>
        /// 获取属性的UI显示名称
        /// </summary>
        private string GetPropertyDisplayName(string propertyName, Material material)
        {
            if (material?.shader == null) return propertyName;
            
            var shader = material.shader;
            for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
            {
                if (ShaderUtil.GetPropertyName(shader, i) == propertyName)
                {
                    var description = ShaderUtil.GetPropertyDescription(shader, i);
                    if (!string.IsNullOrEmpty(description))
                    {
                        return $"{description} ({propertyName})";
                    }
                    break;
                }
            }
            
            return propertyName;
        }
        
        /// <summary>
        /// 替换贴图
        /// </summary>
        private void ReplaceTexture(TexturePreviewItem item, Texture newTexture)
        {
            if (item.material == null) return;
            
            try
            {
                var oldTexture = item.texture;
                var copiedTexture = newTexture;
                if (copiedTexture == null && item.texture != null && !string.IsNullOrEmpty(textureCopyTargetFolder))
                {
                    copiedTexture = MaterialCopyUtility.CopyTextureToFolder(item.texture, textureCopyTargetFolder);
                }

                item.material.SetTexture(item.propertyName, copiedTexture);
                EditorUtility.SetDirty(item.material);
                item.texture = copiedTexture;

                var oldName = oldTexture != null ? oldTexture.name : "缺失贴图";
                var newName = copiedTexture != null ? copiedTexture.name : "移除贴图";
                Debug.Log($"已将材质 '{item.material.name}' 中的 '{item.propertyName}' 从 '{oldName}' 替换为 '{newName}'");

                RefreshAllDataUnified();
                needsRefreshTexturePreview = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"替换贴图时出现错误: {e.Message}");
            }
        }
        
        /// <summary>
        /// 刷新贴图预览
        /// </summary>
        private void RefreshTexturePreview()
        {
            allTextures.Clear();
            textureGroups.Clear();
            
            // 检查贴图类型检测开关
            if (!showTextureType)
            {
                UpdateCachedStatistics();
                return;
            }
            
            var selectedObjects = lockAllData && lockedSelectionObjects.Length > 0
                ? lockedSelectionObjects
                : Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                UpdateCachedStatistics();
                return;
            }
            
            foreach (var obj in selectedObjects)
            {
                CollectTexturesFromGameObject(obj, obj.name);
            }
            
            // 重新构建分组
            ApplyTextureFilters();
            UpdateCachedStatistics();
        }
        
        /// <summary>
        /// 从GameObject及其子对象收集贴图信息
        /// </summary>
        private void CollectTexturesFromGameObject(GameObject obj, string rootName)
        {
            // 收集当前对象及其所有子对象
            var allGameObjects = new List<GameObject>();
            CollectGameObjectsRecursively(obj, allGameObjects);
            
            // 为每个GameObject分析其材质和贴图
            foreach (var gameObject in allGameObjects)
            {
                // 分析Renderer组件
                var renderers = gameObject.GetComponents<Renderer>();
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    
                    var materials = renderer.sharedMaterials;
                    for (int matIndex = 0; matIndex < materials.Length; matIndex++)
                    {
                        var material = materials[matIndex];
                        if (material == null) continue;
                        
                        AnalyzeMaterialTextures(gameObject, material, rootName, "Renderer");
                    }
                }
                
                // 分析ParticleSystem组件
                var particleSystem = gameObject.GetComponent<ParticleSystem>();
                if (particleSystem != null)
                {
                    var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        var materials = renderer.sharedMaterials;
                        foreach (var material in materials)
                        {
                            if (material == null) continue;
                            AnalyzeMaterialTextures(gameObject, material, rootName, "ParticleSystem");
                        }
                    }
                }
                
                // 分析TrailRenderer组件
                var trailRenderer = gameObject.GetComponent<TrailRenderer>();
                if (trailRenderer != null)
                {
                    var materials = trailRenderer.sharedMaterials;
                    foreach (var material in materials)
                    {
                        if (material == null) continue;
                        AnalyzeMaterialTextures(gameObject, material, rootName, "TrailRenderer");
                    }
                }
            }
        }
        
        /// <summary>
        /// 递归收集GameObject及其所有子对象
        /// </summary>
        private void CollectGameObjectsRecursively(GameObject obj, List<GameObject> result)
        {
            result.Add(obj);
            
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                var child = obj.transform.GetChild(i).gameObject;
                CollectGameObjectsRecursively(child, result);
            }
        }
        
        /// <summary>
        /// 分析材质中的贴图
        /// </summary>
        private void AnalyzeMaterialTextures(GameObject gameObject, Material material, string rootName, string componentType)
        {
            var shader = material.shader;
            if (shader == null) return;
            
            for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    var propertyName = ShaderUtil.GetPropertyName(shader, i);
                    var texture = material.GetTexture(propertyName);
                    
                    // 检查是否已存在相同的贴图项（去重）- 改进去重逻辑
                    var existingItem = allTextures.FirstOrDefault(item => 
                        item.material == material && 
                        item.propertyName == propertyName &&
                        item.texture == texture);
                    
                    if (existingItem == null)
                    {
                        var textureItem = new TexturePreviewItem
                        {
                            gameObject = gameObject,
                            material = material,
                            texture = texture,
                            propertyName = propertyName,
                            textureType = GetTextureType(propertyName),
                            hierarchyPath = GetHierarchyPath(gameObject, rootName),
                            componentType = componentType
                        };
                        
                        allTextures.Add(textureItem);
                    }
                }
            }
        }
        
        /// <summary>
        /// 创建缺失贴图的特殊键值
        /// </summary>
        private static Texture CreateMissingTextureKey()
        {
            if (_missingTextureKey == null)
            {
                _missingTextureKey = Texture2D.whiteTexture; // 使用一个现有的非null Texture对象
            }
            return _missingTextureKey;
        }
        
        /// <summary>
        /// 应用贴图过滤
        /// </summary>
        private void ApplyTextureFilters()
        {
            textureGroups.Clear();
            
            var filteredItems = allTextures.Where(ShouldShowTextureItem).ToList();
            
            foreach (var item in filteredItems)
            {
                var key = item.texture; // 可能为null，用于缺失贴图分组
                
                // 修复空引用异常：当key为null时使用特殊键值
                var safeKey = key ?? CreateMissingTextureKey();
                
                if (!textureGroups.ContainsKey(safeKey))
                {
                    textureGroups[safeKey] = new List<TexturePreviewItem>();
                }
                textureGroups[safeKey].Add(item);
            }
        }
        
        /// <summary>
        /// 检查是否应该显示贴图组
        /// </summary>
        private bool ShouldShowTextureGroup(Texture texture, List<TexturePreviewItem> items)
        {
            if (items.Count == 0) return false;
            
            // 检查搜索过滤
            if (!string.IsNullOrEmpty(textureSearchFilter))
            {
                var textureName = texture != null ? texture.name : "缺失贴图";
                if (!textureName.ToLower().Contains(textureSearchFilter.ToLower()))
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 检查是否应该显示贴图项
        /// </summary>
        private bool ShouldShowTextureItem(TexturePreviewItem item)
        {
            // 检查贴图类型过滤
            switch (item.textureType)
            {
                case TextureType.MainTexture:
                    if (!showMainTextures) return false;
                    break;
                case TextureType.NormalMap:
                    if (!showNormalMaps) return false;
                    break;
                case TextureType.EmissionMap:
                    if (!showEmissionMaps) return false;
                    break;
                case TextureType.Other:
                    if (!showOtherTextures) return false;
                    break;
            }
            
            // 检查缺失贴图过滤
            if (item.texture == null && !showMissingTextures)
                return false;
            
            // 检查搜索过滤
            if (!string.IsNullOrEmpty(textureSearchFilter))
            {
                var textureName = item.texture != null ? item.texture.name : "缺失贴图";
                var materialName = item.material != null ? item.material.name : "";
                var gameObjectName = item.gameObject != null ? item.gameObject.name : "";
                
                var searchText = $"{textureName} {materialName} {gameObjectName} {item.propertyName}".ToLower();
                if (!searchText.Contains(textureSearchFilter.ToLower()))
                    return false;
            }
            
            // 检查外部资源过滤
            if (showOnlyExternalResources)
            {
                // 获取贴图路径
                string texturePath = item.texture != null ? AssetDatabase.GetAssetPath(item.texture) : "";
                
                // 如果启用了"只显示外部资源"，则只显示在预制体平级目录外的资源
                if (!IsResourceOutsidePrefabDirectory(texturePath))
                {
                    return false; // 不是外部资源，不显示
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// 获取贴图排序键
        /// </summary>
        private object GetTextureSortKey(KeyValuePair<Texture, List<TexturePreviewItem>> kvp)
        {
            return textureSortMode switch
            {
                TextureSortMode.ByName => kvp.Key != null ? kvp.Key.name : "zzz_缺失贴图",
                TextureSortMode.ByUsageCount => -kvp.Value.Count, // 负数用于降序排列
                TextureSortMode.ByMemorySize => kvp.Key != null ? -GetTextureMemorySize(kvp.Key) : 0,
                _ => kvp.Key != null ? kvp.Key.name : "zzz_缺失贴图"
            };
        }
        
        /// <summary>
        /// 获取贴图类型
        /// </summary>
        private TextureType GetTextureType(string propertyName)
        {
            var lowerName = propertyName.ToLower();
            
            if (lowerName.Contains("maintex") || lowerName.Contains("_diffuse") || lowerName.Contains("_albedo"))
                return TextureType.MainTexture;
            
            if (lowerName.Contains("bump") || lowerName.Contains("normal"))
                return TextureType.NormalMap;
            
            if (lowerName.Contains("emission") || lowerName.Contains("emissive"))
                return TextureType.EmissionMap;
            
            return TextureType.Other;
        }
        
        /// <summary>
        /// 检查资源是否在预制体的平级目录外
        /// 例如：Prefab在 Assets/.../Win_man_Lvbu/Prefab/xxx.prefab
        /// 只要资源在 Win_man_Lvbu 目录下（包括所有子目录），就不标红
        /// 在 Win_man_Lvbu 目录外的资源，标红
        /// </summary>
        private bool IsResourceOutsidePrefabDirectory(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath) || resourcePath == "未知")
                return false;
            
            // 获取当前选中的预制体路径
            var selectedObjects = lockAllData && lockedSelectionObjects.Length > 0
                ? lockedSelectionObjects
                : Selection.gameObjects;
            
            if (selectedObjects == null || selectedObjects.Length == 0)
                return false;
            
            // 获取第一个选中对象的预制体路径
            var firstObject = selectedObjects[0];
            var prefabPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(firstObject);
            
            // 如果不是预制体实例，尝试获取场景对象的路径
            if (string.IsNullOrEmpty(prefabPath))
            {
                // 对于场景对象，我们无法确定其"目录"，所以不标记
                return false;
            }
            
            // 标准化路径（统一使用正斜杠）
            prefabPath = prefabPath.Replace("\\", "/");
            resourcePath = resourcePath.Replace("\\", "/");
            
            // 获取预制体所在目录
            // 例如：Assets/PufferResources/.../Win_man_Lvbu/Prefab/xxx.prefab
            var prefabDirectory = System.IO.Path.GetDirectoryName(prefabPath);
            if (string.IsNullOrEmpty(prefabDirectory))
                return false;
            
            prefabDirectory = prefabDirectory.Replace("\\", "/");
            
            // 获取预制体的平级根目录（Prefab的父目录）
            // 例如：Assets/PufferResources/.../Win_man_Lvbu
            var prefabParentDirectory = System.IO.Path.GetDirectoryName(prefabDirectory);
            if (string.IsNullOrEmpty(prefabParentDirectory))
                return false;
            
            prefabParentDirectory = prefabParentDirectory.Replace("\\", "/");
            
            // 检查资源路径是否以预制体的平级根目录开头
            // 如果是，说明资源在同一个平级目录下（包括所有子目录）
            if (resourcePath.StartsWith(prefabParentDirectory + "/"))
            {
                return false; // 在平级目录下，不标红
            }
            
            // 其他情况都认为是在预制体平级目录外
            return true;
        }
        
        /// <summary>
        /// 获取贴图信息
        /// </summary>
        private string GetTextureInfo(Texture texture)
        {
            if (texture == null) return "贴图为空";
            
            var info = $"尺寸: {texture.width}x{texture.height}";
            
            if (texture is Texture2D tex2D)
            {
                info += $", 格式: {tex2D.format}";
                info += $", Mipmap: {tex2D.mipmapCount > 1}";
            }
            
            info += $", 内存: {FormatMemorySize(GetTextureMemorySize(texture))}";
            
            return info;
        }
        
        /// <summary>
        /// 获取贴图内存占用
        /// </summary>
        private long GetTextureMemorySize(Texture texture)
        {
            if (texture == null) return 0;
            
            // 简单估算，实际可能需要更复杂的计算
            var pixelCount = texture.width * texture.height;
            
            if (texture is Texture2D tex2D)
            {
                // 根据格式估算每像素字节数
                var bytesPerPixel = tex2D.format switch
                {
                    TextureFormat.RGBA32 => 4,
                    TextureFormat.RGB24 => 3,
                    TextureFormat.ARGB32 => 4,
                    TextureFormat.DXT1 => 0.5f,
                    TextureFormat.DXT5 => 1f,
                    _ => 4f // 默认估算
                };
                
                var baseSize = (long)(pixelCount * bytesPerPixel);
                
                // 考虑Mipmap
                if (tex2D.mipmapCount > 1)
                {
                    baseSize = (long)(baseSize * 1.33f); // Mipmap大约增加33%内存
                }
                
                return baseSize;
            }
            
            return pixelCount * 4; // 默认RGBA32估算
        }
        
        /// <summary>
        /// 格式化内存大小
        /// </summary>
        private string FormatMemorySize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024f * 1024f):F1} MB";
            return $"{bytes / (1024f * 1024f * 1024f):F1} GB";
        }
        
        /// <summary>
        /// 从Project窗口获取选中的文件夹路径
        /// </summary>
        private string GetSelectedFolderFromProject()
        {
            // 获取Project窗口中选中的对象
            var selectedObjects = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets);
            
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("提示", "请在Project窗口中选择一个文件夹", "确定");
                return null;
            }
            
            var selectedObject = selectedObjects[0];
            var path = AssetDatabase.GetAssetPath(selectedObject);
            
            // 检查是否是文件夹
            if (System.IO.Directory.Exists(path))
            {
                return path;
            }
            else if (System.IO.File.Exists(path))
            {
                // 如果选中的是文件，返回其所在文件夹
                return System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            }
            
            EditorUtility.DisplayDialog("提示", "请选择一个有效的文件夹", "确定");
            return null;
        }
        
        /// <summary>
        /// 复制材质到指定文件夹
        /// </summary>
        private void CopyMaterialToFolder(Material sourceMaterial, string targetFolderPath)
        {
            if (sourceMaterial == null)
            {
                EditorUtility.DisplayDialog("错误", "源材质为空", "确定");
                return;
            }

            if (string.IsNullOrEmpty(targetFolderPath))
            {
                EditorUtility.DisplayDialog("错误", "目标文件夹路径为空", "确定");
                return;
            }

            var copiedMaterial = MaterialCopyUtility.CopyMaterialToFolder(sourceMaterial, targetFolderPath);
            if (copiedMaterial == null)
            {
                EditorUtility.DisplayDialog("错误", "复制材质失败", "确定");
                return;
            }

            selectedNewMaterial = copiedMaterial;
            var copiedMaterialPath = AssetDatabase.GetAssetPath(copiedMaterial);
            EditorUtility.DisplayDialog("成功", string.Format("材质已复制到: {0}并自动设置为新Material", copiedMaterialPath), "确定");

            EditorGUIUtility.PingObject(copiedMaterial);
            Selection.activeObject = copiedMaterial;
        }
        
        /// <summary>
        /// 复制Mesh到指定文件夹
        /// </summary>
        private void CopyMeshToFolder(Mesh sourceMesh, string targetFolderPath)
        {
            if (sourceMesh == null)
            {
                EditorUtility.DisplayDialog("错误", "源Mesh为空", "确定");
                return;
            }
            
            if (string.IsNullOrEmpty(targetFolderPath))
            {
                EditorUtility.DisplayDialog("错误", "目标文件夹路径为空", "确定");
                return;
            }
            
            // 确保目标文件夹存在
            if (!System.IO.Directory.Exists(targetFolderPath))
            {
                System.IO.Directory.CreateDirectory(targetFolderPath);
                AssetDatabase.Refresh();
            }
            
            // 获取源Mesh路径
            var sourcePath = AssetDatabase.GetAssetPath(sourceMesh);
            if (string.IsNullOrEmpty(sourcePath))
            {
                EditorUtility.DisplayDialog("错误", "无法获取源Mesh路径", "确定");
                return;
            }
            
            // 获取源文件扩展名
            var extension = System.IO.Path.GetExtension(sourcePath);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".asset";
            }
            
            // 生成目标路径
            var meshName = sourceMesh.name;
            var targetPath = $"{targetFolderPath}/{meshName}{extension}";
            
            // 如果目标路径已存在，生成新名称
            int counter = 1;
            while (System.IO.File.Exists(targetPath))
            {
                targetPath = $"{targetFolderPath}/{meshName}_{counter}{extension}";
                counter++;
            }
            
            try
            {
                // 复制文件
                System.IO.File.Copy(sourcePath, targetPath, false);
                
                // 获取相对于Assets的路径
                string relativePath = targetPath;
                if (targetPath.StartsWith(UnityEngine.Application.dataPath))
                {
                    relativePath = "Assets" + targetPath.Substring(UnityEngine.Application.dataPath.Length);
                }
                
                // 刷新资源数据库，但不重新导入
                AssetDatabase.Refresh(ImportAssetOptions.DontDownloadFromCacheServer);
                
                // 如果是模型文件（fbx, obj等），需要设置导入选项以避免生成子网格
                if (extension.ToLower() == ".obj" || extension.ToLower() == ".fbx")
                {
                    // 设置导入选项
                    ModelImporter importer = AssetImporter.GetAtPath(relativePath) as ModelImporter;
                    if (importer != null)
                    {
                        importer.importBlendShapes = false;
                        importer.importVisibility = false;
                        importer.importCameras = false;
                        importer.importLights = false;
                        importer.preserveHierarchy = false;
                        AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);
                    }
                }
                
                // 加载复制后的Mesh并自动设置为新Mesh
                var copiedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(relativePath);
                if (copiedMesh != null)
                {
                    selectedNewMesh = copiedMesh;
                    EditorUtility.DisplayDialog("成功", string.Format("Mesh已复制到: {0}，并自动设置为新Mesh", targetPath), "确定");
                    
                    // 在Project窗口中高亮显示
                    EditorGUIUtility.PingObject(copiedMesh);
                }
                else
                {
                    EditorUtility.DisplayDialog("成功", string.Format("Mesh已复制到: {0}", targetPath), "确定");
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("错误", $"复制Mesh时出错: {e.Message}", "确定");
                Debug.LogError($"复制Mesh失败: {e}");
            }
        }
    }

    /// <summary>
    /// Material统计信息
    /// </summary>
    public class MaterialStatistics
    {
        public int referenceCount;
        public string materialPath;
        public List<MaterialReference> references = new List<MaterialReference>();
    }

    /// <summary>
    /// Material引用信息
    /// </summary>
    public class MaterialReference
    {
        public Component component;
        public string objectPath;
        public MaterialReferenceType referenceType;
        
        public string GetReferenceTypeName()
        {
            switch (referenceType)
            {
                case MaterialReferenceType.ParticleSystemRenderer:
                    return "粒子系统渲染器";
                case MaterialReferenceType.TrailRenderer:
                    return "拖尾渲染器";
                case MaterialReferenceType.MeshRenderer:
                    return "网格渲染器";
                case MaterialReferenceType.SkinnedMeshRenderer:
                    return "蒙皮网格渲染器";
                default:
                    return "未知";
            }
        }
        
        // 重写Equals和GetHashCode以确保字典键的正确匹配
        public override bool Equals(object obj)
        {
            if (obj is MaterialReference other)
            {
                return component == other.component && 
                       referenceType == other.referenceType &&
                       objectPath == other.objectPath;
            }
            return false;
        }
        
        public override int GetHashCode()
        {
            return (component?.GetHashCode() ?? 0) ^ 
                   referenceType.GetHashCode() ^ 
                   (objectPath?.GetHashCode() ?? 0);
        }
    }

    /// <summary>
    /// Material引用类型
    /// </summary>
    public enum MaterialReferenceType
    {
        ParticleSystemRenderer,
        TrailRenderer,
        MeshRenderer,
        SkinnedMeshRenderer
    }

    /// <summary>
    /// Mesh预览信息
    /// </summary>
    public class MeshPreviewInfo
    {
        public GameObject gameObject;
        public Mesh mesh;
        public string componentType;
        public string hierarchyPath;
        public string additionalInfo;
    }

    /// <summary>
    /// Material预览信息
    /// </summary>
    public class MaterialPreviewInfo
    {
        public GameObject gameObject;
        public Material material;
        public string componentType;
        public string hierarchyPath;
        public string additionalInfo;
        public int materialIndex = -1; // 材质在数组中的索引，-1表示单个材质
    }
}