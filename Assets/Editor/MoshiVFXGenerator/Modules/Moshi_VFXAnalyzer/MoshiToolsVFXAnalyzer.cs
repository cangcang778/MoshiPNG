#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator.Analyzer
{
    /// <summary>
    /// VFX特效分析工具 - 分析/对比/规则发现
    /// VFX Effect Analysis Tool - Analyze / Compare / Rule Discovery
    /// </summary>
    public class MoshiToolsVFXAnalyzer : EditorWindow
    {
        // ==================== 常量 ====================
        // ==================== Constants ====================

        private const string TOOL_NAME = "特效分析器";
        private const string DATA_DIR = "UserSettings/MoshiVFXGenerator/VFXAnalysis";
        private const string PREFS_ENABLED_KEY = "MoshiVFXGenerator_VFXAnalyzer_Enabled";

        // ==================== 标签页 ====================
        // ==================== Tabs ====================

        private enum Tab
        {
            Analyze,    // 单个分析 / Single analysis
            Compare,    // 双个对比 / Dual comparison
            Optimize,   // 性能优化 / Performance optimization
            Rules       // 规则发现 / Rule discovery
        }

        private Tab currentTab = Tab.Analyze;

        // ==================== 分析页状态 ====================
        // ==================== Analyze Tab State ====================

        private VFXRecipe analyzedRecipe;
        private Vector2 analyzeScroll;
        private int selectedLayerIndex = -1;

        // ==================== 对比页状态 ====================
        // ==================== Compare Tab State ====================

        private VFXRecipe compareRecipeA;
        private VFXRecipe compareRecipeB;
        private VFXComparison comparisonResult;
        private Vector2 compareScroll;
        private bool showMatchedFoldout = true;        // 匹配层折叠 / Matched layers foldout
        private bool showOnlyInFoldout = true;         // 独有层折叠 / Only-in layers foldout
        private bool showCompRulesFoldout = true;      // 对比规则折叠 / Comparison rules foldout

        // ==================== 优化页状态 ====================
        // ==================== Optimize Tab State ====================

        private Vector2 optimizeScroll;
        private VFXSeverity optimizeSeverityFilter = VFXSeverity.Info;  // 最低显示级别 / Minimum display severity
        private bool showScoreBreakdown = true;       // 得分明细折叠 / Score breakdown foldout
        private bool showSuggestions = true;           // 优化建议折叠 / Suggestions foldout

        // ==================== 规则页状态 ====================
        // ==================== Rules Tab State ====================

        private List<VFXRecipe> allAnalyzedRecipes = new List<VFXRecipe>();
        private List<VFXRule> discoveredRules = new List<VFXRule>();
        private List<VFXTemplate> discoveredTemplates = new List<VFXTemplate>();
        private List<VFXRule> detectedAnomalies = new List<VFXRule>();
        private string rulesReport = "";
        private Vector2 rulesScroll;
        private VFXRuleType rulesFilter = VFXRuleType.Composition;
        private Vector2 recipesListScroll;           // 规则库特效列表滚动 / Recipe list scroll
        private Vector2 selectedRecipeDetailScroll;  // 选中特效详情滚动 / Selected recipe detail scroll
        private bool showRecipesFoldout = true;      // 已分析特效列表折叠 / Recipes list foldout
        private bool showDetailFoldout = true;       // 特效详情折叠 / Recipe detail foldout
        private bool showRulesFoldout = true;        // 规则显示折叠 / Rules display foldout
        private int selectedRecipeIndex = -1;        // 当前选中的规则库特效索引 / Selected rule library recipe index

        // ==================== 样式 ====================
        // ==================== Styles ====================

        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;
        private GUIStyle layerStyle;
        private GUIStyle selectedLayerStyle;
        private GUIStyle roleTagStyle;
        private GUIStyle ratingBadgeStyle;       // 性能评级徽章 / Performance rating badge
        private GUIStyle suggestionCardStyle;    // 优化建议卡片 / Optimization suggestion card
        private bool stylesInitialized;

        // 安全获取自定义样式，防止域重载后为null
        // Safe style getters to prevent null after domain reload
        private GUIStyle RatingBadgeStyle => ratingBadgeStyle ?? (ratingBadgeStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(4, 4, 2, 2),
            normal = { textColor = Color.white }
        });

        private GUIStyle SuggestionCardStyle => suggestionCardStyle ?? (suggestionCardStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 11,
            padding = new RectOffset(8, 6, 6, 6),
            margin = new RectOffset(0, 0, 2, 2)
        });

        private GUIStyle RoleTagStyle => roleTagStyle ?? (roleTagStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 9,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(2, 0, 2, 0)
        });

        // 安全获取标题样式，防止域重载后为null
        // Safe getter for header style, prevent null after domain reload
        private GUIStyle HeaderStyle => headerStyle ?? (headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            fixedHeight = 22
        });

        // 安全获取分节样式，防止域重载后为null
        // Safe getter for section style, prevent null after domain reload
        private GUIStyle SectionStyle => sectionStyle ?? (sectionStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            fixedHeight = 22,
            padding = new RectOffset(4, 0, 2, 2)
        });

        // 安全获取层级样式，防止域重载后为null
        // Safe getter for layer style, prevent null after domain reload
        private GUIStyle LayerStyle => layerStyle ?? (layerStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11,
            padding = new RectOffset(6, 2, 4, 4),
            alignment = TextAnchor.MiddleLeft,
            fixedHeight = 22
        });

        // 安全获取选中层级样式，防止域重载后为null
        // Safe getter for selected layer style, prevent null after domain reload
        private GUIStyle SelectedLayerStyle => selectedLayerStyle ?? (selectedLayerStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11,
            padding = new RectOffset(6, 2, 4, 4),
            alignment = TextAnchor.MiddleLeft,
            fixedHeight = 22
        });

        // ==================== 搜索 ====================
        // ==================== Search ====================

        private string searchFilter = "";
#pragma warning disable CS0414
        private VFXCategory categoryFilter = VFXCategory.Unknown;
#pragma warning restore CS0414

        // ==================== 数据持久化 ====================
        // ==================== Data Persistence ====================

        private const string RECIPES_KEY = "MoshiVFXGenerator_VFXAnalyzer_Recipes";

        // ==================== 初始化 ====================
        // ==================== Initialization ====================

        [MenuItem("工具/Moshi特效生成器/特效分析器")]
        public static void ShowWindow()
        {
            var window = GetWindow<MoshiToolsVFXAnalyzer>(TOOL_NAME);
            window.minSize = new Vector2(700, 500);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(TOOL_NAME);
            LoadData();
        }

        private void OnDisable()
        {
            SaveData();
        }

        // ==================== 主GUI ====================
        // ==================== Main GUI ====================

        private void OnGUI()
        {
            InitializeStyles();

            // 标签页切换
            // Tab switching
            GUILayout.BeginHorizontal();
            DrawTabButton(Tab.Analyze, "分析");
            DrawTabButton(Tab.Compare, "对比");
            DrawTabButton(Tab.Optimize, "优化");
            DrawTabButton(Tab.Rules, "规则库");
            GUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(GUI.skin.box);

            switch (currentTab)
            {
                case Tab.Analyze:
                    DrawAnalyzeTab();
                    break;
                case Tab.Compare:
                    DrawCompareTab();
                    break;
                case Tab.Optimize:
                    DrawOptimizeTab();
                    break;
                case Tab.Rules:
                    DrawRulesTab();
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTabButton(Tab tab, string label)
        {
            bool isActive = currentTab == tab;
            var style = isActive ? EditorStyles.toolbarButton : GUI.skin.button;
            if (GUILayout.Button(label, style, GUILayout.Height(28)))
                currentTab = tab;
        }

        // ==================== 分析标签页 ====================
        // ==================== Analyze Tab ====================

        private void DrawAnalyzeTab()
        {
            // Prefab选择区
            // Prefab selection area
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("选择特效Prefab:", GUILayout.Width(110));
            if (GUILayout.Button("选择文件...", GUILayout.Width(90)))
            {
                string path = EditorUtility.OpenFilePanel("选择VFX Prefab", "Assets", "prefab");
                if (!string.IsNullOrEmpty(path))
                {
                    string relativePath = FileUtil.GetProjectRelativePath(path);
                    AnalyzeSinglePrefab(relativePath);
                }
            }

            // 拖拽区域
            // Drag area
            var dropRect = GUILayoutUtility.GetRect(120, 24, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "或拖拽Prefab到此处", EditorStyles.helpBox);
            HandleDragAndDrop(dropRect, AnalyzeSinglePrefab);
            EditorGUILayout.EndHorizontal();

            // 快速选择最近分析的Prefab
            // Quick select recently analyzed prefabs
            if (GUILayout.Button("扫描目录下所有FX Prefab", GUILayout.Height(26)))
            {
                ScanAndAnalyzeAll();
            }

            // 显示分析结果
            // Show analysis result
            if (analyzedRecipe != null)
            {
                EditorGUILayout.Space(6);
                DrawAnalyzedRecipe();
            }
        }

        private void DrawAnalyzedRecipe()
        {
            var recipe = analyzedRecipe;

            // 摘要卡片
            // Summary card
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("特效摘要", EditorStyles.boldLabel);

            // 性能评级徽章 / Performance rating badge
            if (recipe.performanceReport != null)
            {
                var report = recipe.performanceReport;
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = report.GetRatingColor();
                GUILayout.Label($"  {report.rating}  ", RatingBadgeStyle, GUILayout.Width(36));
                GUI.backgroundColor = prevBg;
                GUILayout.Label($"({report.score:F0}分)", GUILayout.Width(55));
            }

            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("名称:", recipe.prefabName);
            EditorGUILayout.LabelField("分类:", recipe.category.ToString());
            EditorGUILayout.LabelField("文件大小:", $"{recipe.fileSizeBytes / 1024f:F1} KB");
            EditorGUILayout.LabelField("总层数:", recipe.statistics.totalLayers.ToString());
            EditorGUILayout.LabelField("粒子系统:", recipe.statistics.particleSystemCount.ToString());
            EditorGUILayout.LabelField("MeshRenderer:", recipe.statistics.meshRendererCount.ToString());
            EditorGUILayout.LabelField("最大粒子数:", recipe.statistics.totalMaxParticles.ToString("F0"));
            EditorGUILayout.LabelField("平均持续:", $"{recipe.statistics.avgDuration:F2}s");
            EditorGUILayout.LabelField("层级深度:", recipe.statistics.hierarchyDepth.ToString());
            EditorGUILayout.LabelField("材质数:", recipe.statistics.uniqueMaterialCount.ToString());
            EditorGUILayout.LabelField("Shader数:", recipe.statistics.uniqueShaderCount.ToString());

            if (recipe.tags.Count > 0)
                EditorGUILayout.LabelField("标签:", string.Join(", ", recipe.tags));

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();

            // AI描述
            // AI description
            if (!string.IsNullOrEmpty(recipe.aiDescription))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(recipe.aiDescription, MessageType.Info);
            }

            // 层级列表
            // Layer list
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("层级列表", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(180));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            analyzeScroll = EditorGUILayout.BeginScrollView(analyzeScroll);

            var filteredLayers = string.IsNullOrEmpty(searchFilter)
                ? recipe.layers
                : recipe.layers.Where(l => l.name.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0
                    || l.role.ToString().IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            for (int i = 0; i < filteredLayers.Count; i++)
            {
                var layer = filteredLayers[i];
                bool isSelected = selectedLayerIndex == i;

                EditorGUILayout.BeginHorizontal(isSelected ? SelectedLayerStyle : LayerStyle);

                // 角色标签
                // Role tag
                var roleColor = GetRoleColor(layer.role);
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = roleColor;
                GUILayout.Label(layer.role.ToString().Substring(0, Math.Min(4, layer.role.ToString().Length)), RoleTagStyle, GUILayout.Width(36));
                GUI.backgroundColor = prevBg;

                // 层名和类型
                // Layer name and type
                EditorGUI.indentLevel++;
                string indent = layer.path.Contains("/") ? "  " : "";
                EditorGUILayout.LabelField($"{indent}{layer.name}", layer.type.ToString(), GUILayout.ExpandWidth(true));
                EditorGUI.indentLevel--;

                // 展开详情按钮
                // Expand detail button
                if (GUILayout.Button(isSelected ? "▲" : "▼", GUILayout.Width(28)))
                    selectedLayerIndex = isSelected ? -1 : i;

                EditorGUILayout.EndHorizontal();

                // 层详情
                // Layer details
                if (isSelected && layer.mainParams != null)
                {
                    EditorGUI.indentLevel += 2;
                    DrawLayerDetails(layer);
                    EditorGUI.indentLevel -= 2;
                    EditorGUILayout.Space(4);
                }
            }

            EditorGUILayout.EndScrollView();

            // 操作按钮
            // Action buttons
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("导出JSON", GUILayout.Height(28)))
                ExportRecipeJSON(recipe);

            if (GUILayout.Button("添加到规则库", GUILayout.Height(28)))
            {
                if (!allAnalyzedRecipes.Any(r => r.prefabPath == recipe.prefabPath))
                {
                    allAnalyzedRecipes.Add(recipe);
                    SaveData();
                    Debug.Log($"[VFX Analyzer] 已添加到规则库: {recipe.prefabName}");
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLayerDetails(VFXLayer layer)
        {
            var mp = layer.mainParams;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            // 基本信息
            // Basic info
            EditorGUILayout.LabelField("路径:", layer.path);
            EditorGUILayout.LabelField("位置:", $"({layer.localPosition.x:F0}, {layer.localPosition.y:F0}, {layer.localPosition.z:F0})");
            EditorGUILayout.LabelField("缩放:", $"({layer.localScale.x:F2}, {layer.localScale.y:F2}, {layer.localScale.z:F2})");

            if (!string.IsNullOrEmpty(layer.materialGuid))
                EditorGUILayout.LabelField("材质GUID:", layer.materialGuid.Substring(0, Math.Min(12, layer.materialGuid.Length)));
            if (!string.IsNullOrEmpty(layer.shaderName))
                EditorGUILayout.LabelField("Shader:", layer.shaderName.Split('/').Last());

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("参数:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Duration:", $"{mp.duration:F2}s");
            EditorGUILayout.LabelField("SimulationSpeed:", $"{mp.simulationSpeed:F1}x");
            EditorGUILayout.LabelField("Looping:", mp.looping ? "Yes" : "No");
            EditorGUILayout.LabelField("StartLifetime:", $"{mp.startLifetime:F2}s");
            EditorGUILayout.LabelField("StartSpeed:", $"{mp.startSpeed:F2}");
            EditorGUILayout.LabelField("StartSize:", $"{mp.startSize:F2}");
            EditorGUILayout.LabelField("StartColor:", $"({mp.startColor.r:F2}, {mp.startColor.g:F2}, {mp.startColor.b:F2}, {mp.startColor.a:F2})");
            EditorGUILayout.LabelField("EmissionRate:", $"{mp.emissionRate:F1}/s");
            EditorGUILayout.LabelField("MaxParticles:", mp.maxParticles.ToString());
            EditorGUILayout.LabelField("GravityModifier:", $"{mp.gravityModifier:F2}");
            EditorGUILayout.LabelField("ShapeType:", mp.shapeType.ToString());
            EditorGUILayout.LabelField("ShapeAngle:", $"{mp.shapeAngle:F1}°");

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);

            // 启用模块
            // Enabled modules
            EditorGUILayout.LabelField("启用模块:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            if (layer.modules != null)
            {
                var names = layer.modules.GetEnabledNames();
                if (names.Count > 0)
                    EditorGUILayout.LabelField("", string.Join(", ", names));
                else
                    EditorGUILayout.LabelField("", "(无额外模块)");
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        // ==================== 对比标签页 ====================
        // ==================== Compare Tab ====================

        private void DrawCompareTab()
        {
            EditorGUILayout.Space(4);

            // 选择两个Prefab
            // Select two Prefabs
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Prefab A:", GUILayout.Width(65));
            EditorGUILayout.LabelField(compareRecipeA != null ? compareRecipeA.prefabName : "(未选择)", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFilePanel("选择Prefab A", "Assets", "prefab");
                if (!string.IsNullOrEmpty(path))
                    compareRecipeA = VFXPrefabAnalyzer.AnalyzePrefab(FileUtil.GetProjectRelativePath(path));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Prefab B:", GUILayout.Width(65));
            EditorGUILayout.LabelField(compareRecipeB != null ? compareRecipeB.prefabName : "(未选择)", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFilePanel("选择Prefab B", "Assets", "prefab");
                if (!string.IsNullOrEmpty(path))
                    compareRecipeB = VFXPrefabAnalyzer.AnalyzePrefab(FileUtil.GetProjectRelativePath(path));
            }
            EditorGUILayout.EndHorizontal();

            // 从规则库选择
            // Select from rule library
            if (allAnalyzedRecipes.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("从规则库:", GUILayout.Width(65));
                var names = new string[allAnalyzedRecipes.Count + 1];
                names[0] = "(无)";
                for (int i = 0; i < allAnalyzedRecipes.Count; i++)
                    names[i + 1] = allAnalyzedRecipes[i].prefabName;
                int idxA = EditorGUILayout.Popup(0, names, GUILayout.ExpandWidth(true));
                int idxB = EditorGUILayout.Popup(0, names, GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();

                if (idxA > 0) compareRecipeA = allAnalyzedRecipes[idxA - 1];
                if (idxB > 0) compareRecipeB = allAnalyzedRecipes[idxB - 1];
            }

            // 对比按钮
            // Compare button
            if (GUILayout.Button("开始对比", GUILayout.Height(28)))
            {
                if (compareRecipeA != null && compareRecipeB != null)
                    comparisonResult = VFXComparator.Compare(compareRecipeA, compareRecipeB);
                else
                    EditorUtility.DisplayDialog("提示", "请先选择两个Prefab", "确定");
            }

            // 显示对比结果
            // Show comparison result
            if (comparisonResult != null)
            {
                EditorGUILayout.Space(6);
                DrawComparisonResult();
            }
        }

        private void DrawComparisonResult()
        {
            var comp = comparisonResult;

            // 相似度
            // Similarity
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("对比结果", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("整体相似度:", $"{comp.overallSimilarity:P1}");

            // 相似度颜色条
            // Similarity color bar
            Rect barRect = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));
            float width = barRect.width * comp.overallSimilarity;
            Color barColor = comp.overallSimilarity > 0.7f ? new Color(0.3f, 0.8f, 0.3f) :
                            comp.overallSimilarity > 0.4f ? new Color(0.8f, 0.8f, 0.3f) :
                            new Color(0.8f, 0.3f, 0.3f);
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, width, barRect.height), barColor);

            // 性能对比摘要
            // Performance comparison summary
            if (compareRecipeA?.performanceReport != null && compareRecipeB?.performanceReport != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("性能对比:", EditorStyles.boldLabel);
                var pA = compareRecipeA.performanceReport;
                var pB = compareRecipeB.performanceReport;

                EditorGUILayout.BeginHorizontal();
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = pA.GetRatingColor();
                GUILayout.Label($" {pA.rating} ", RatingBadgeStyle, GUILayout.Width(30));
                GUI.backgroundColor = prevBg;
                EditorGUILayout.LabelField($"{compareRecipeA.prefabName} ({pA.score:F0}分)", GUILayout.Width(180));

                EditorGUILayout.LabelField("vs", GUILayout.Width(24));

                GUI.backgroundColor = pB.GetRatingColor();
                GUILayout.Label($" {pB.rating} ", RatingBadgeStyle, GUILayout.Width(30));
                GUI.backgroundColor = prevBg;
                EditorGUILayout.LabelField($"{compareRecipeB.prefabName} ({pB.score:F0}分)", GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();

            // 统计
            // Statistics
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"匹配层对: {comp.matchedLayers.Count}   仅在A: {comp.onlyInA.Count}   仅在B: {comp.onlyInB.Count}   参数差异: {comp.paramDiffs.Count}   规则: {comp.discoveredRules.Count}");

            // 滚动区域
            // Scroll area
            compareScroll = EditorGUILayout.BeginScrollView(compareScroll);

            // 匹配的层（可折叠）
            // Matched layers (foldable)
            if (comp.matchedLayers.Count > 0)
            {
                showMatchedFoldout = EditorGUILayout.Foldout(showMatchedFoldout, $"匹配的层 ({comp.matchedLayers.Count})", true, EditorStyles.boldLabel);
                if (showMatchedFoldout)
                {
                    EditorGUI.indentLevel++;
                    foreach (var match in comp.matchedLayers)
                    {
                        float sim = match.similarity;
                        Color simColor = sim > 0.8f ? new Color(0.3f, 0.7f, 0.3f) :
                                         sim > 0.5f ? new Color(0.7f, 0.7f, 0.3f) :
                                         new Color(0.7f, 0.3f, 0.3f);
                        var prevBg = GUI.backgroundColor;
                        GUI.backgroundColor = simColor;
                        EditorGUILayout.BeginHorizontal(GUI.skin.box);
                        GUI.backgroundColor = prevBg;
                        EditorGUILayout.LabelField($"{match.layerA.name} ↔ {match.layerB.name}", GUILayout.Width(220));
                        EditorGUILayout.LabelField($"{sim:P0}", GUILayout.Width(50));

                        if (match.paramDiffs.Count > 0)
                            EditorGUILayout.LabelField($"({match.paramDiffs[0]})", GUILayout.ExpandWidth(true));

                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUI.indentLevel--;
                }
            }

            // 仅在A/B中（可折叠）
            // Only in A/B (foldable)
            if (comp.onlyInA.Count > 0 || comp.onlyInB.Count > 0)
            {
                showOnlyInFoldout = EditorGUILayout.Foldout(showOnlyInFoldout, $"独有层 (A:{comp.onlyInA.Count} B:{comp.onlyInB.Count})", true, EditorStyles.boldLabel);
                if (showOnlyInFoldout)
                {
                    EditorGUI.indentLevel++;
                    if (comp.onlyInA.Count > 0)
                    {
                        EditorGUILayout.LabelField($"仅在 A 中:", EditorStyles.miniLabel);
                        foreach (var layer in comp.onlyInA)
                            EditorGUILayout.LabelField($"  {layer.ToBrief()}");
                    }
                    if (comp.onlyInB.Count > 0)
                    {
                        EditorGUILayout.LabelField($"仅在 B 中:", EditorStyles.miniLabel);
                        foreach (var layer in comp.onlyInB)
                            EditorGUILayout.LabelField($"  {layer.ToBrief()}");
                    }
                    EditorGUI.indentLevel--;
                }
            }

            // 发现的规则（可折叠）
            // Discovered rules (foldable)
            if (comp.discoveredRules.Count > 0)
            {
                showCompRulesFoldout = EditorGUILayout.Foldout(showCompRulesFoldout, $"发现的规则 ({comp.discoveredRules.Count})", true, EditorStyles.boldLabel);
                if (showCompRulesFoldout)
                {
                    EditorGUI.indentLevel++;
                    foreach (var rule in comp.discoveredRules)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"[{rule.type}] {rule.description}", EditorStyles.wordWrappedLabel);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.Space(2);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndScrollView();

            // 导出报告
            // Export report
            if (GUILayout.Button("导出对比报告", GUILayout.Height(26)))
            {
                string report = VFXComparator.GenerateComparisonReport(comp);
                string path = EditorUtility.SaveFilePanel("保存对比报告", "", "VFX_Comparison", "txt");
                if (!string.IsNullOrEmpty(path))
                    File.WriteAllText(path, report);
            }
        }

        // ==================== 优化标签页 ====================
        // ==================== Optimize Tab ====================

        private void DrawOptimizeTab()
        {
            EditorGUILayout.Space(4);

            if (analyzedRecipe == null || analyzedRecipe.performanceReport == null)
            {
                EditorGUILayout.HelpBox("请先在「分析」页中选择并分析一个特效Prefab", MessageType.Info);
                return;
            }

            var report = analyzedRecipe.performanceReport;

            // === 综合评级卡片 ===
            // === Overall rating card ===
            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("性能评级", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 评级徽章（大号）
            // Rating badge (large)
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = report.GetRatingColor();
            GUILayout.Label($"  {report.rating}  ", RatingBadgeStyle, GUILayout.Width(44), GUILayout.Height(28));
            GUI.backgroundColor = prevBg;

            EditorGUILayout.LabelField($"{report.score:F0}/100", EditorStyles.largeLabel, GUILayout.Width(65));
            EditorGUILayout.EndHorizontal();

            // 评级描述
            // Rating description
            EditorGUILayout.LabelField(report.GetRatingDescription(), EditorStyles.wordWrappedLabel);

            // 评级颜色条
            // Rating color bar
            Rect scoreBarRect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.DrawRect(scoreBarRect, new Color(0.15f, 0.15f, 0.15f));
            float scoreWidth = scoreBarRect.width * (report.score / 100f);
            Color scoreColor = report.GetRatingColor();
            EditorGUI.DrawRect(new Rect(scoreBarRect.x, scoreBarRect.y, scoreWidth, scoreBarRect.height), scoreColor);

            // S/A/B/C/D 刻度线
            // S/A/B/C/D scale marks
            string[] marks = { "D", "C", "B", "A", "S" };
            float[] thresholds = { 0.30f, 0.50f, 0.70f, 0.85f, 1.0f };
            for (int m = 0; m < thresholds.Length; m++)
            {
                float x = scoreBarRect.x + scoreBarRect.width * thresholds[m];
                EditorGUI.DrawRect(new Rect(x - 1, scoreBarRect.y, 2, scoreBarRect.height), new Color(0.5f, 0.5f, 0.5f, 0.5f));
            }

            EditorGUILayout.EndVertical();

            // === 得分明细（可折叠）===
            // === Score breakdown (foldable) ===
            EditorGUILayout.Space(6);
            showScoreBreakdown = EditorGUILayout.Foldout(showScoreBreakdown, "得分明细", true, EditorStyles.boldLabel);

            if (showScoreBreakdown)
            {
                EditorGUI.indentLevel++;
                DrawScoreBar("粒子得分", report.particleScore, 0.35f);
                DrawScoreBar("层级得分", report.layerScore, 0.20f);
                DrawScoreBar("材质得分", report.materialScore, 0.20f);
                DrawScoreBar("复杂度得分", report.complexityScore, 0.25f);
                EditorGUI.indentLevel--;
            }

            // === 优化建议（可折叠 + 可滚动）===
            // === Optimization suggestions (foldable + scrollable) ===
            EditorGUILayout.Space(6);

            int criticalCount = report.suggestions.Count(s => s.severity == VFXSeverity.Critical);
            int warningCount = report.suggestions.Count(s => s.severity == VFXSeverity.Warning);
            int infoCount = report.suggestions.Count(s => s.severity == VFXSeverity.Info);
            showSuggestions = EditorGUILayout.Foldout(showSuggestions,
                $"优化建议 ({criticalCount} 严重 / {warningCount} 警告 / {infoCount} 信息)", true, EditorStyles.boldLabel);

            if (showSuggestions)
            {
                // 严重级别筛选
                // Severity filter
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("筛选:", GUILayout.Width(40));
                if (GUILayout.Button(optimizeSeverityFilter == VFXSeverity.Info ? "→ 全部" : "全部", EditorStyles.miniButtonLeft))
                    optimizeSeverityFilter = VFXSeverity.Info;
                if (GUILayout.Button(optimizeSeverityFilter == VFXSeverity.Warning ? "→ 警告+" : "警告+", EditorStyles.miniButtonMid))
                    optimizeSeverityFilter = VFXSeverity.Warning;
                if (GUILayout.Button(optimizeSeverityFilter == VFXSeverity.Critical ? "→ 严重" : "严重", EditorStyles.miniButtonRight))
                    optimizeSeverityFilter = VFXSeverity.Critical;
                EditorGUILayout.EndHorizontal();

                optimizeScroll = EditorGUILayout.BeginScrollView(optimizeScroll, GUILayout.Height(300f));

                var filteredSuggestions = report.suggestions
                    .Where(s => (int)s.severity >= (int)optimizeSeverityFilter)
                    .ToList();

                foreach (var suggestion in filteredSuggestions)
                {
                    DrawOptimizationSuggestion(suggestion);
                }

                if (filteredSuggestions.Count == 0)
                {
                    EditorGUILayout.HelpBox("没有符合筛选条件的建议", MessageType.Info);
                }

                EditorGUILayout.EndScrollView();
            }

            // === 批量优化建议导出 ===
            // === Batch optimization suggestions export ===
            EditorGUILayout.Space(6);
            if (report.suggestions.Count > 0)
            {
                if (GUILayout.Button("导出优化报告", GUILayout.Height(26)))
                {
                    ExportOptimizationReport(analyzedRecipe, report);
                }
            }
        }

        /// <summary>
        /// 绘制单项得分条
        /// Draw a single score bar
        /// </summary>
        private void DrawScoreBar(string label, float score, float weight)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(80));

            Rect barRect = GUILayoutUtility.GetRect(200, 16, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f));

            float fillWidth = barRect.width * (score / 100f);
            Color fillColor = score >= 85f ? new Color(0.2f, 0.8f, 0.3f) :
                              score >= 70f ? new Color(0.4f, 0.7f, 0.4f) :
                              score >= 50f ? new Color(0.8f, 0.7f, 0.2f) :
                              score >= 30f ? new Color(0.8f, 0.5f, 0.2f) :
                              new Color(0.8f, 0.2f, 0.2f);
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, fillWidth, barRect.height), fillColor);

            // 分数文字
            // Score text
            var scoreStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            EditorGUI.LabelField(barRect, $"{score:F0}", scoreStyle);

            EditorGUILayout.LabelField($"x{weight:P0}", GUILayout.Width(45));
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制单条优化建议
        /// Draw a single optimization suggestion
        /// </summary>
        private void DrawOptimizationSuggestion(VFXOptimizationSuggestion suggestion)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = suggestion.GetSeverityColor() * 0.3f;

            EditorGUILayout.BeginVertical(SuggestionCardStyle);
            GUI.backgroundColor = prevBg;

            // 标题行：图标 + 标题 + 严重级别 + 预估节省
            // Title row: icon + title + severity + estimated saving
            EditorGUILayout.BeginHorizontal();

            // 严重级别图标
            // Severity icon
            prevBg = GUI.backgroundColor;
            GUI.backgroundColor = suggestion.GetSeverityColor();
            GUILayout.Label($" {suggestion.GetSeverityIcon()} ", RoleTagStyle, GUILayout.Width(24));
            GUI.backgroundColor = prevBg;

            // 标题
            // Title
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            EditorGUILayout.LabelField(suggestion.title, titleStyle, GUILayout.ExpandWidth(true));

            // 预估节省
            // Estimated saving
            if (suggestion.estimatedSaving > 0)
                EditorGUILayout.LabelField($"↓{suggestion.estimatedSaving:F0}%", GUILayout.Width(40));

            EditorGUILayout.EndHorizontal();

            // 描述
            // Description
            if (!string.IsNullOrEmpty(suggestion.description))
            {
                var descStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 10 };
                EditorGUILayout.LabelField(suggestion.description, descStyle);
            }

            // 分类标签 + 目标层
            // Category tag + target layer
            EditorGUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(suggestion.category))
            {
                prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.3f, 0.5f, 0.7f, 0.4f);
                GUILayout.Label($" {suggestion.category} ", RoleTagStyle, GUILayout.Width(40));
                GUI.backgroundColor = prevBg;
            }
            if (!string.IsNullOrEmpty(suggestion.targetLayerName))
                EditorGUILayout.LabelField($"→ {suggestion.targetLayerName}", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        /// <summary>
        /// 导出优化报告
        /// Export optimization report
        /// </summary>
        private void ExportOptimizationReport(VFXRecipe recipe, VFXPerformanceReport report)
        {
            string path = EditorUtility.SaveFilePanel("保存优化报告", "", $"{recipe.prefabName}_Optimization", "txt");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"===== VFX 优化报告 =====");
            sb.AppendLine($"特效: {recipe.prefabName}");
            sb.AppendLine($"评级: {report.rating} ({report.score:F0}/100)");
            sb.AppendLine($"评级说明: {report.GetRatingDescription()}");
            sb.AppendLine();
            sb.AppendLine($"--- 得分明细 ---");
            sb.AppendLine($"  粒子得分: {report.particleScore:F0} (权重 35%)");
            sb.AppendLine($"  层级得分: {report.layerScore:F0} (权重 20%)");
            sb.AppendLine($"  材质得分: {report.materialScore:F0} (权重 20%)");
            sb.AppendLine($"  复杂度得分: {report.complexityScore:F0} (权重 25%)");
            sb.AppendLine();
            sb.AppendLine($"--- 优化建议 ({report.suggestions.Count} 条) ---");
            foreach (var s in report.suggestions)
            {
                sb.AppendLine($"  [{s.severity}] {s.title}");
                if (!string.IsNullOrEmpty(s.description))
                    sb.AppendLine($"    {s.description}");
                if (s.estimatedSaving > 0)
                    sb.AppendLine($"    预估节省: {s.estimatedSaving:F0}%");
                sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[VFX Analyzer] 优化报告已导出: {path}");
        }

        // ==================== 规则标签页 ====================
        // ==================== Rules Tab ====================

        private void DrawRulesTab()
        {
            EditorGUILayout.Space(4);

            // 规则库信息
            // Rule library info
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"规则库中有 {allAnalyzedRecipes.Count} 个特效", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("清空规则库", GUILayout.Width(80)))
            {
                allAnalyzedRecipes.Clear();
                discoveredRules.Clear();
                rulesReport = "";
                selectedRecipeIndex = -1;
                SaveData();
            }
            EditorGUILayout.EndHorizontal();

            // 扫描按钮
            // Scan button
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("扫描目录:", GUILayout.Width(65));
            EditorGUILayout.LabelField("Assets/CommonRes/RawResources", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("扫描FX Prefab", GUILayout.Width(110)))
                ScanAndAnalyzeAll();
            EditorGUILayout.EndHorizontal();

            // 发现规则按钮
            // Discover rules button
            if (allAnalyzedRecipes.Count >= 2)
            {
                if (GUILayout.Button($"发现规则 (基于 {allAnalyzedRecipes.Count} 个特效)", GUILayout.Height(28)))
                {
                    discoveredRules = VFXRuleDiscovery.DiscoverAllRules(allAnalyzedRecipes);
                    rulesReport = VFXRuleDiscovery.GenerateRuleReport(discoveredRules);

                    // 同时提炼模板和检测异常
                    // Also distill templates and detect anomalies
                    discoveredTemplates = VFXRuleDiscovery.DiscoverTemplates(allAnalyzedRecipes);
                    detectedAnomalies = VFXRuleDiscovery.DetectAnomalies(allAnalyzedRecipes, discoveredRules);

                    Debug.Log($"[VFX Analyzer] 发现 {discoveredRules.Count} 条规则, {discoveredTemplates.Count} 个模板, {detectedAnomalies.Count} 个异常");
                }
            }
            else
            {
                EditorGUILayout.HelpBox($"需要至少2个特效才能发现规则（当前 {allAnalyzedRecipes.Count} 个）\n请先在「分析」页中分析特效并添加到规则库", MessageType.Info);
            }

            // ==================== 已分析的特效列表（可折叠 + 滚动）====================
            // === Analyzed recipes list (foldable + scrollable) ===
            if (allAnalyzedRecipes.Count > 0)
            {
                EditorGUILayout.Space(6);
                showRecipesFoldout = EditorGUILayout.Foldout(showRecipesFoldout, $"已分析的特效 ({allAnalyzedRecipes.Count})", true, EditorStyles.boldLabel);

                if (showRecipesFoldout)
                {
                    EditorGUI.indentLevel++;
                    recipesListScroll = EditorGUILayout.BeginScrollView(recipesListScroll, GUILayout.Height(Math.Min(allAnalyzedRecipes.Count * 26f, 200f)));

                    for (int i = 0; i < allAnalyzedRecipes.Count; i++)
                    {
                        var recipe = allAnalyzedRecipes[i];
                        bool isSelected = selectedRecipeIndex == i;
                        var prevBg = GUI.backgroundColor;
                        GUI.backgroundColor = isSelected ? new Color(0.25f, 0.45f, 0.7f) : GUI.backgroundColor;

                        EditorGUILayout.BeginHorizontal(isSelected ? selectedLayerStyle : layerStyle);

                        if (GUILayout.Button(isSelected ? "▼" : "▶", GUILayout.Width(22)))
                            selectedRecipeIndex = isSelected ? -1 : i;

                        EditorGUILayout.LabelField(recipe.prefabName, GUILayout.Width(180));
                        EditorGUILayout.LabelField(recipe.category.ToString(), GUILayout.Width(70));
                        EditorGUILayout.LabelField($"{recipe.statistics.totalLayers}层", GUILayout.Width(36));
                        EditorGUILayout.LabelField($"{recipe.statistics.totalMaxParticles:F0}粒子", GUILayout.Width(65));

                        var tagStr = string.Join(", ", recipe.tags);
                        EditorGUILayout.LabelField(tagStr, GUILayout.ExpandWidth(true));

                        if (GUILayout.Button("×", GUILayout.Width(24)))
                        {
                            if (selectedRecipeIndex == i) selectedRecipeIndex = -1;
                            allAnalyzedRecipes.RemoveAt(i);
                            SaveData();
                            break;
                        }

                        EditorGUILayout.EndHorizontal();

                        // 选中时展开详情
                        // Expand detail when selected
                        if (isSelected && showDetailFoldout)
                        {
                            EditorGUI.indentLevel += 1;
                            selectedRecipeDetailScroll = EditorGUILayout.BeginScrollView(selectedRecipeDetailScroll, GUILayout.Height(120f));

                            EditorGUILayout.BeginVertical(GUI.skin.box);

                            // 基础统计
                            // Basic statistics
                            var stats = recipe.statistics;
                            EditorGUILayout.LabelField($"文件: {recipe.fileSizeBytes / 1024f:F1} KB | 粒子系统: {stats.particleSystemCount} | MeshRenderer: {stats.meshRendererCount}");
                            EditorGUILayout.LabelField($"最大粒子: {stats.totalMaxParticles:F0} | 平均持续: {stats.avgDuration:F2}s | 层级深度: {stats.hierarchyDepth}");
                            EditorGUILayout.LabelField($"材质数: {stats.uniqueMaterialCount} | Shader数: {stats.uniqueShaderCount}");

                            // 层级预览
                            // Layer preview
                            EditorGUILayout.Space(4);
                            EditorGUILayout.LabelField("层级列表:", EditorStyles.boldLabel);
                            foreach (var layer in recipe.layers)
                            {
                                var roleColor = GetRoleColor(layer.role);
                                prevBg = GUI.backgroundColor;
                                GUI.backgroundColor = roleColor;
                                EditorGUILayout.BeginHorizontal(GUI.skin.box);
                                GUI.backgroundColor = prevBg;
                                GUILayout.Label(layer.role.ToString().Substring(0, Math.Min(3, layer.role.ToString().Length)), RoleTagStyle, GUILayout.Width(28));
                                EditorGUILayout.LabelField(layer.name, GUILayout.Width(180));
                                EditorGUILayout.LabelField(layer.type.ToString(), GUILayout.Width(90));

                                string info = "";
                                if (layer.mainParams != null)
                                    info = $"粒子:{layer.mainParams.maxParticles} | {layer.mainParams.emissionRate:F0}/s";
                                else
                                    info = "静态/非粒子";
                                EditorGUILayout.LabelField(info, GUILayout.ExpandWidth(true));
                                EditorGUILayout.EndHorizontal();
                            }

                            EditorGUILayout.EndVertical();
                            EditorGUILayout.EndScrollView();
                            EditorGUI.indentLevel -= 1;

                            EditorGUILayout.Space(4);
                        }
                    }

                    EditorGUILayout.EndScrollView();
                    EditorGUI.indentLevel--;
                }
            }

            // ==================== 规则显示（可折叠 + 滚动）====================
            // === Rules display (foldable + scrollable) ===
            if (discoveredRules.Count > 0)
            {
                EditorGUILayout.Space(6);
                showRulesFoldout = EditorGUILayout.Foldout(showRulesFoldout, $"发现规则 ({discoveredRules.Count})", true, EditorStyles.boldLabel);

                if (showRulesFoldout)
                {
                    EditorGUI.indentLevel++;

                    // 筛选
                    // Filter
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("筛选:", GUILayout.Width(40));
                    if (GUILayout.Button(rulesFilter == VFXRuleType.Composition ? "→ 组成" : "组成", EditorStyles.miniButtonLeft))
                        rulesFilter = VFXRuleType.Composition;
                    if (GUILayout.Button(rulesFilter == VFXRuleType.Parameter ? "→ 参数" : "参数", EditorStyles.miniButtonMid))
                        rulesFilter = VFXRuleType.Parameter;
                    if (GUILayout.Button(rulesFilter == VFXRuleType.Performance ? "→ 性能" : "性能", EditorStyles.miniButtonMid))
                        rulesFilter = VFXRuleType.Performance;
                    if (GUILayout.Button(rulesFilter == VFXRuleType.Naming ? "→ 命名" : "命名", EditorStyles.miniButtonMid))
                        rulesFilter = VFXRuleType.Naming;
                    if (GUILayout.Button(rulesFilter == VFXRuleType.Structure ? "→ 结构" : "结构", EditorStyles.miniButtonRight))
                        rulesFilter = VFXRuleType.Structure;
                    EditorGUILayout.EndHorizontal();

                    rulesScroll = EditorGUILayout.BeginScrollView(rulesScroll, GUILayout.Height(220f));

                    var filtered = rulesFilter == VFXRuleType.Composition ? discoveredRules : discoveredRules.Where(r => r.type == rulesFilter).ToList();

                    foreach (var rule in filtered)
                    {
                        EditorGUILayout.BeginVertical(GUI.skin.box);
                        EditorGUILayout.BeginHorizontal();

                        // 置信度条
                        // Confidence bar
                        Rect confRect = EditorGUILayout.GetControlRect(GUILayout.Width(60), GUILayout.Height(18));
                        EditorGUI.DrawRect(confRect, new Color(0.2f, 0.2f, 0.2f));
                        float confWidth = confRect.width * rule.confidence;
                        Color confColor = rule.confidence > 0.7f ? new Color(0.2f, 0.6f, 0.9f) :
                                          rule.confidence > 0.4f ? new Color(0.2f, 0.6f, 0.5f) :
                                          new Color(0.6f, 0.4f, 0.2f);
                        EditorGUI.DrawRect(new Rect(confRect.x, confRect.y, confWidth, confRect.height), confColor);

                        // 规则内容
                        // Rule content
                        var style = new GUIStyle(EditorStyles.wordWrappedLabel);
                        style.fontSize = 11;
                        EditorGUILayout.LabelField($"[{rule.type}] {rule.description}", style, GUILayout.ExpandWidth(true));
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(2);
                    }

                    EditorGUILayout.EndScrollView();
                    EditorGUI.indentLevel--;

                    // 导出
                    // Export
                    EditorGUILayout.Space(4);
                    if (GUILayout.Button("导出规则报告", GUILayout.Height(26)))
                    {
                        string path = EditorUtility.SaveFilePanel("保存规则报告", "", "VFX_Rules", "txt");
                        if (!string.IsNullOrEmpty(path))
                            File.WriteAllText(path, rulesReport);
                    }

                    if (GUILayout.Button("导出JSON (所有配方)", GUILayout.Height(26)))
                    {
                        string path = EditorUtility.SaveFilePanel("保存JSON", "", "VFX_Recipes", "json");
                        if (!string.IsNullOrEmpty(path))
                        {
                            var wrapper = new { recipes = allAnalyzedRecipes, rules = discoveredRules };
                            string json = JsonUtility.ToJson(wrapper, true);
                            File.WriteAllText(path, json);
                        }
                    }
                }
            }

            // ==================== 模板推荐（可折叠）====================
            // === Template recommendations (foldable) ===
            if (discoveredTemplates.Count > 0)
            {
                EditorGUILayout.Space(6);
                bool showTemplates = EditorGUILayout.Foldout(true, $"推荐模板 ({discoveredTemplates.Count})", true, EditorStyles.boldLabel);
                if (showTemplates)
                {
                    EditorGUI.indentLevel++;
                    foreach (var template in discoveredTemplates)
                    {
                        EditorGUILayout.BeginVertical(GUI.skin.box);

                        // 模板标题
                        // Template title
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(template.templateName, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                        var prevBg = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f, 0.6f);
                        GUILayout.Label($" 置信度:{template.confidence:P0} ", RoleTagStyle, GUILayout.Width(90));
                        GUI.backgroundColor = prevBg;
                        EditorGUILayout.EndHorizontal();

                        // 模板描述
                        // Template description
                        EditorGUILayout.LabelField(template.description, EditorStyles.wordWrappedLabel);
                        EditorGUILayout.LabelField($"样本数: {template.sampleCount} | 来源: {string.Join(", ", template.sourcePrefabNames.Take(3))}", EditorStyles.miniLabel);

                        // 推荐层级列表
                        // Recommended layer list
                        EditorGUILayout.Space(2);
                        foreach (var tl in template.layers)
                        {
                            EditorGUILayout.BeginHorizontal();

                            // 角色标签
                            // Role tag
                            var roleColor = GetRoleColor(tl.role);
                            prevBg = GUI.backgroundColor;
                            GUI.backgroundColor = roleColor;
                            GUILayout.Label(tl.role.ToString().Substring(0, Math.Min(4, tl.role.ToString().Length)), RoleTagStyle, GUILayout.Width(36));
                            GUI.backgroundColor = prevBg;

                            EditorGUILayout.LabelField(tl.suggestedName, GUILayout.Width(80));
                            EditorGUILayout.LabelField(tl.type.ToString(), GUILayout.Width(90));

                            if (tl.isOptional)
                                EditorGUILayout.LabelField($"(可选 {tl.occurrenceRate:P0})", GUILayout.Width(90));
                            else
                                EditorGUILayout.LabelField($"(必须 {tl.occurrenceRate:P0})", GUILayout.Width(90));

                            // 推荐参数摘要
                            // Recommended parameters summary
                            if (tl.recommendedParams != null)
                            {
                                EditorGUILayout.LabelField($"粒子:{tl.recommendedParams.maxParticles} 寿命:{tl.recommendedParams.startLifetime:F1}s", GUILayout.ExpandWidth(true));
                            }

                            EditorGUILayout.EndHorizontal();
                        }

                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(2);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            // ==================== 异常检测（可折叠）====================
            // === Anomaly detection (foldable) ===
            if (detectedAnomalies.Count > 0)
            {
                EditorGUILayout.Space(6);
                bool showAnomalies = EditorGUILayout.Foldout(true, $"异常检测 ({detectedAnomalies.Count})", true, EditorStyles.boldLabel);
                if (showAnomalies)
                {
                    EditorGUI.indentLevel++;
                    foreach (var anomaly in detectedAnomalies)
                    {
                        var prevBg = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f, 0.3f);
                        EditorGUILayout.BeginVertical(GUI.skin.box);
                        GUI.backgroundColor = prevBg;

                        EditorGUILayout.BeginHorizontal();
                        GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f, 0.6f);
                        GUILayout.Label(" ! ", RoleTagStyle, GUILayout.Width(24));
                        GUI.backgroundColor = prevBg;
                        EditorGUILayout.LabelField(anomaly.description, EditorStyles.wordWrappedLabel);
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(2);
                    }
                    EditorGUI.indentLevel--;
                }
            }
        }

        // ==================== 功能方法 ====================
        // ==================== Functional Methods ====================

        private void AnalyzeSinglePrefab(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return;

            analyzedRecipe = VFXPrefabAnalyzer.AnalyzePrefab(relativePath);
            selectedLayerIndex = -1;
            searchFilter = "";

            if (analyzedRecipe != null)
                Debug.Log($"[VFX Analyzer] 分析完成: {analyzedRecipe.prefabName} ({analyzedRecipe.layers.Count} 层)");
        }

        private void ScanAndAnalyzeAll()
        {
            string[] dirs = {
                "Assets/CommonRes/RawResources/Effect",
                "Assets/CommonRes/RawResources/PlayingDLC"
            };

            var allPrefabPaths = new List<string>();
            foreach (var dir in dirs)
            {
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { dir });
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.Contains("/Effect/") || path.Contains("/FX_") || path.Contains("/fx_"))
                        allPrefabPaths.Add(path);
                }
            }

            if (allPrefabPaths.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "未找到特效Prefab", "确定");
                return;
            }

            var recipes = VFXPrefabAnalyzer.AnalyzeBatch(allPrefabPaths);

            // 设置第一个为当前分析结果
            // Set first as current analysis result
            if (recipes.Count > 0)
            {
                analyzedRecipe = recipes[0];
                selectedLayerIndex = -1;
                searchFilter = "";

                // 全部添加到规则库
                // Add all to rule library
                allAnalyzedRecipes = recipes;
                SaveData();
            }

            Debug.Log($"[VFX Analyzer] 扫描完成: 发现 {allPrefabPaths.Count} 个特效Prefab，成功分析 {recipes.Count} 个");
        }

        private void ExportRecipeJSON(VFXRecipe recipe)
        {
            string path = EditorUtility.SaveFilePanel("保存JSON", "", recipe.prefabName, "json");
            if (!string.IsNullOrEmpty(path))
            {
                string json = JsonUtility.ToJson(recipe, true);
                File.WriteAllText(path, json);
                Debug.Log($"[VFX Analyzer] 已导出: {path}");
            }
        }

        // ==================== 拖拽支持 ====================
        // ==================== Drag & Drop Support ====================

        private void HandleDragAndDrop(Rect dropRect, Action<string> onDrop)
        {
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.DragUpdated)
            {
                if (DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences[0] is GameObject)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                }
            }
            else if (currentEvent.type == EventType.DragPerform && dropRect.Contains(currentEvent.mousePosition))
            {
                DragAndDrop.AcceptDrag();

                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is GameObject go)
                    {
                        string path = AssetDatabase.GetAssetPath(go);
                        if (!string.IsNullOrEmpty(path))
                            onDrop(path);
                    }
                }
            }
        }

        // ==================== 数据持久化 ====================
        // ==================== Data Persistence ====================

        private void SaveData()
        {
            try
            {
                Directory.CreateDirectory(DATA_DIR);
                string json = JsonUtility.ToJson(new RecipeListWrapper { recipes = allAnalyzedRecipes }, true);
                File.WriteAllText(Path.Combine(DATA_DIR, "recipes.json"), json);
                EditorPrefs.SetBool(PREFS_ENABLED_KEY, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VFX Analyzer] Save failed: {e.Message}");
            }
        }

        private void LoadData()
        {
            try
            {
                string filePath = Path.Combine(DATA_DIR, "recipes.json");
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var wrapper = JsonUtility.FromJson<RecipeListWrapper>(json);
                    allAnalyzedRecipes = wrapper.recipes ?? new List<VFXRecipe>();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VFX Analyzer] Load failed: {e.Message}");
                allAnalyzedRecipes = new List<VFXRecipe>();
            }
        }

        [Serializable]
        private class RecipeListWrapper
        {
            public List<VFXRecipe> recipes;
        }

        // ==================== 样式 ====================
        // ==================== Styles ====================

        private void InitializeStyles()
        {
            // 域重载后样式可能丢失，需要检查是否为null
            // Styles may be lost after domain reload, check for null
            if (stylesInitialized && ratingBadgeStyle != null && suggestionCardStyle != null && roleTagStyle != null) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                fixedHeight = 22
            };

            sectionStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                fixedHeight = 22,
                padding = new RectOffset(4, 0, 2, 2)
            };

            layerStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                padding = new RectOffset(6, 2, 4, 4),
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 22
            };

            selectedLayerStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                padding = new RectOffset(6, 2, 4, 4),
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 22
            };

            roleTagStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(2, 0, 2, 0)
            };

            ratingBadgeStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(4, 4, 2, 2),
                normal = { textColor = Color.white }
            };

            suggestionCardStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 11,
                padding = new RectOffset(8, 6, 6, 6),
                margin = new RectOffset(0, 0, 2, 2)
            };

            stylesInitialized = true;
        }

        private void DrawSectionHeader(string text)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(text, SectionStyle);
        }

        private static Color GetRoleColor(VFXLayerRole role)
        {
            switch (role)
            {
                case VFXLayerRole.Glow: return new Color(0.9f, 0.7f, 0.2f, 0.4f);
                case VFXLayerRole.Particle: return new Color(0.3f, 0.7f, 0.9f, 0.4f);
                case VFXLayerRole.Ribbon: return new Color(0.9f, 0.3f, 0.7f, 0.4f);
                case VFXLayerRole.Trail: return new Color(0.5f, 0.9f, 0.5f, 0.4f);
                case VFXLayerRole.Core: return new Color(0.9f, 0.5f, 0.2f, 0.4f);
                case VFXLayerRole.Background: return new Color(0.5f, 0.5f, 0.5f, 0.3f);
                case VFXLayerRole.Text: return new Color(0.7f, 0.7f, 0.3f, 0.4f);
                case VFXLayerRole.Sparkle: return new Color(1f, 0.85f, 0.3f, 0.4f);
                case VFXLayerRole.Smoke: return new Color(0.5f, 0.5f, 0.6f, 0.4f);
                case VFXLayerRole.Ring: return new Color(0.4f, 0.8f, 0.8f, 0.4f);
                case VFXLayerRole.Shockwave: return new Color(0.9f, 0.3f, 0.3f, 0.4f);
                case VFXLayerRole.Debris: return new Color(0.6f, 0.4f, 0.3f, 0.4f);
                default: return new Color(0.5f, 0.5f, 0.5f, 0.3f);
            }
        }
    }
}
#endif
