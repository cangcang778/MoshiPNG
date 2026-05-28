#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using MoshiVFXGenerator.Blueprint;
using MoshiVFXGenerator.Cloner;
using MoshiVFXGenerator.Factory;
using MoshiVFXGenerator.VfxBrowser;

namespace MoshiVFXGenerator
{
    /// <summary>
    /// Moshi特效生成器主入口窗口
    /// Main entry window for Moshi VFX Generator
    /// </summary>
    public class Moshi_VFXGen : EditorWindow
    {
        private const string TOOL_NAME = "Moshi特效生成器";
        private const string PRESET_ROOT = "Assets/Editor/MoshiVFXGenerator/Presets";
        private const string RECIPE_PRESET_DIR = PRESET_ROOT + "/Recipes";
        private const string CONFIG_PRESET_DIR = PRESET_ROOT + "/Configs";

        private enum MainTab { Overview, Assets, Cloner, Generator, Blueprint, Quality, History }

        private MainTab currentTab = MainTab.Overview;
        private Vector2 scrollPos;
        private Vector2 resultScroll;
        private Vector2 layerScroll;

        private Moshi_VFXGenSettings settings;
        private Moshi_VFXAssetDB assetDB;
        private Moshi_VFXHistory history;

        private VFXCloneConfig cloneConfig = new VFXCloneConfig();
        private VFXPrefabInfo sourceInfo;
        private List<VFXCloneResult> cloneResults = new List<VFXCloneResult>();
        private VFXQualityReport selectedQualityReport;
        private GameObject qualityTargetPrefab;

        private List<VFXRecipe> factoryRecipes = new List<VFXRecipe>();
        private VFXFactoryConfig factoryConfig = new VFXFactoryConfig();
        private VFXCloneResult factoryResult;
        private VFXBlueprint blueprint = new VFXBlueprint();
        private VFXCloneResult blueprintResult;
        private Moshi_VFXGenPreset activePreset;
        private GameObject previewPrefab;
        private VFXAssetKind assetKindFilter = VFXAssetKind.Unknown;
        private string assetSearchText = string.Empty;
        private string assetTagFilter = string.Empty;
        private string assetBatchTag = string.Empty;
        private bool assetFavoritesOnly = false;
        private Moshi_VfxBrowserContext previewContext;
        private GameObject previewContextPrefab;
        private bool previewIsPlaying = true;
        private Color previewBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        private float previewSpeed = 1f;
        private double previewLastTime;

        [MenuItem("工具/Moshi特效生成器/打开生成器")]
        public static void ShowWindow()
        {
            var window = GetWindow<Moshi_VFXGen>(TOOL_NAME);
            window.minSize = new Vector2(760, 520);
        }

        private void OnEnable()
        {
            settings = Moshi_VFXGenSettings.Instance;
            assetDB = Moshi_VFXAssetDB.Instance;
            history = Moshi_VFXHistory.Instance;
            EnsurePresetFolders();
            factoryRecipes = VFXPresetLibrary.GetDefaultRecipes();
            LoadRecipePresetsFromDisk();
            ApplySettingsToCloneConfig(false);
            factoryConfig.outputRoot = settings.defaultOutputRoot;
            EnsureDefaultBlueprint();
            previewLastTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            CleanupPreviewContext();
        }

        private void OnGUI()
        {
            if (settings == null) settings = Moshi_VFXGenSettings.Instance;
            if (assetDB == null) assetDB = Moshi_VFXAssetDB.Instance;
            if (history == null) history = Moshi_VFXHistory.Instance;

            DrawHeader();
            DrawTabs();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            switch (currentTab)
            {
                case MainTab.Overview:
                    DrawOverviewTab();
                    break;
                case MainTab.Assets:
                    DrawAssetsTab();
                    break;
                case MainTab.Cloner:
                    DrawClonerTab();
                    break;
                case MainTab.Generator:
                    DrawGeneratorTab();
                    break;
                case MainTab.Blueprint:
                    DrawBlueprintTab();
                    break;
                case MainTab.Quality:
                    DrawQualityTab();
                    break;
                case MainTab.History:
                    DrawHistoryTab();
                    break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(TOOL_NAME, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("独立于 Moshi工具箱 的特效自动制作平台", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("打开分析器", GUILayout.Width(90), GUILayout.Height(24)))
                MoshiVFXGenerator.Analyzer.MoshiToolsVFXAnalyzer.ShowWindow();
            if (GUILayout.Button("打开浏览器", GUILayout.Width(90), GUILayout.Height(24)))
                Moshi_VfxBrowserCreater.ShowWindow();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6);
        }

        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();
            DrawTabButton(MainTab.Overview, "总览", EditorStyles.miniButtonLeft);
            DrawTabButton(MainTab.Assets, "素材库", EditorStyles.miniButtonMid);
            DrawTabButton(MainTab.Cloner, "克隆器", EditorStyles.miniButtonMid);
            DrawTabButton(MainTab.Generator, "生成", EditorStyles.miniButtonMid);
            DrawTabButton(MainTab.Blueprint, "蓝图", EditorStyles.miniButtonMid);
            DrawTabButton(MainTab.Quality, "质检", EditorStyles.miniButtonMid);
            DrawTabButton(MainTab.History, "历史", EditorStyles.miniButtonRight);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6);
        }

        private void DrawTabButton(MainTab tab, string label, GUIStyle style)
        {
            if (GUILayout.Toggle(currentTab == tab, label, style, GUILayout.Width(70)))
                currentTab = tab;
        }

        private void DrawOverviewTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("平台状态", EditorStyles.boldLabel);
            DrawStage("Phase 0", "独立目录 / 分析器 / 浏览器 / 主入口", true);
            DrawStage("Phase 1", "素材库 + 安全克隆 MVP", true);
            DrawStage("Phase 2", "配方一键生成 MVP", true);
            DrawStage("Phase 3", "蓝图画布 / Mesh / Trail / 材质槽", true);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("快速操作", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("扫描素材", GUILayout.Height(30)))
            {
                assetDB.Scan(settings.scanFolders);
                EditorUtility.DisplayDialog(TOOL_NAME, "素材库扫描完成", "确定");
            }
            if (GUILayout.Button("进入克隆", GUILayout.Height(30)))
                currentTab = MainTab.Cloner;
            if (GUILayout.Button("查看历史", GUILayout.Height(30)))
                currentTab = MainTab.History;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            DrawAssetSummary();
            DrawEmbeddedPreview();
        }

        private void DrawAssetsTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("扫描目录", EditorStyles.boldLabel);
            for (int i = 0; i < settings.scanFolders.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                settings.scanFolders[i] = EditorGUILayout.TextField(settings.scanFolders[i]);
                if (GUILayout.Button("删除", GUILayout.Width(50)))
                {
                    settings.scanFolders.RemoveAt(i);
                    settings.Save();
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("添加目录", GUILayout.Width(90)))
            {
                settings.scanFolders.Add("Assets/");
                settings.Save();
            }
            if (GUILayout.Button("扫描素材", GUILayout.Width(90)))
            {
                assetDB.Scan(settings.scanFolders);
                settings.Save();
            }
            if (GUILayout.Button("保存设置", GUILayout.Width(90)))
            {
                settings.Save();
                assetDB.Save();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            DrawAssetSummary();
            DrawAssetList();
        }

        private void DrawClonerTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("源特效", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            cloneConfig.sourcePrefab = (GameObject)EditorGUILayout.ObjectField("源 Prefab", cloneConfig.sourcePrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
                sourceInfo = cloneConfig.sourcePrefab != null ? VFXPrefabScanner.Scan(cloneConfig.sourcePrefab) : null;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("扫描结构", GUILayout.Width(90)))
                sourceInfo = cloneConfig.sourcePrefab != null ? VFXPrefabScanner.Scan(cloneConfig.sourcePrefab) : null;
            if (GUILayout.Button("分析器打开", GUILayout.Width(90)))
                MoshiVFXGenerator.Analyzer.MoshiToolsVFXAnalyzer.ShowWindow();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            if (sourceInfo != null)
                DrawSourceInfo(sourceInfo);

            EditorGUILayout.Space(6);
            DrawCloneConfig();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(cloneConfig.sourcePrefab == null);
            if (GUILayout.Button("生成预检", GUILayout.Height(32)))
            {
                selectedQualityReport = Moshi_VFXPreflightCheck.CheckCloneConfig(cloneConfig);
            }
            if (GUILayout.Button("批量生成", GUILayout.Height(32)))
            {
                cloneResults = VFXVariantEngine.GenerateVariants(cloneConfig);
                selectedQualityReport = cloneResults.Count > 0 ? cloneResults[0].qualityReport : null;
                previewPrefab = GetFirstGeneratedPrefab(cloneResults);
            }
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("清空结果", GUILayout.Height(32)))
                cloneResults.Clear();
            EditorGUILayout.EndHorizontal();

            DrawCloneResults();
        }

        private void DrawGeneratorTab()
        {
            if (factoryRecipes == null || factoryRecipes.Count == 0)
            {
                factoryRecipes = VFXPresetLibrary.GetDefaultRecipes();
                LoadRecipePresetsFromDisk();
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("配方选择", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < factoryRecipes.Count; i++)
            {
                GUIStyle style = i == 0 ? EditorStyles.miniButtonLeft : i == factoryRecipes.Count - 1 ? EditorStyles.miniButtonRight : EditorStyles.miniButtonMid;
                if (GUILayout.Toggle(factoryConfig.selectedRecipeIndex == i, factoryRecipes[i].recipeName, style))
                    factoryConfig.selectedRecipeIndex = i;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            VFXRecipe recipe = factoryRecipes[Mathf.Clamp(factoryConfig.selectedRecipeIndex, 0, factoryRecipes.Count - 1)];
            EditorGUILayout.HelpBox(recipe.description, MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("导入配方", GUILayout.Width(80)))
                ImportRecipe();
            if (GUILayout.Button("导出配方", GUILayout.Width(80)))
                ExportRecipe(recipe);
            if (GUILayout.Button("复制配方", GUILayout.Width(80)))
                DuplicateRecipe(recipe);
            if (GUILayout.Button("刷新预设", GUILayout.Width(80)))
            {
                factoryRecipes = VFXPresetLibrary.GetDefaultRecipes();
                LoadRecipePresetsFromDisk();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            DrawRecipeEditor(recipe);

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("生成配置", EditorStyles.boldLabel);
            factoryConfig.effectName = EditorGUILayout.TextField("特效名称", factoryConfig.effectName);
            factoryConfig.outputRoot = EditorGUILayout.TextField("输出目录", string.IsNullOrEmpty(factoryConfig.outputRoot) ? settings.defaultOutputRoot : factoryConfig.outputRoot);
            factoryConfig.themeColor = EditorGUILayout.ColorField("主题颜色", factoryConfig.themeColor);
            factoryConfig.runQualityCheck = EditorGUILayout.Toggle("生成后质检", factoryConfig.runQualityCheck);
            factoryConfig.useAssetLibraryMaterial = EditorGUILayout.Toggle("素材库材质", factoryConfig.useAssetLibraryMaterial);
            if (factoryConfig.useAssetLibraryMaterial)
                factoryConfig.materialTag = EditorGUILayout.TextField("材质标签", factoryConfig.materialTag);
            factoryConfig.useAssetLibraryTexture = EditorGUILayout.Toggle("素材库贴图", factoryConfig.useAssetLibraryTexture);
            if (factoryConfig.useAssetLibraryTexture)
                factoryConfig.textureTag = EditorGUILayout.TextField("贴图标签", factoryConfig.textureTag);
            factoryConfig.useAssetLibraryMesh = EditorGUILayout.Toggle("素材库Mesh", factoryConfig.useAssetLibraryMesh);
            if (factoryConfig.useAssetLibraryMesh)
                factoryConfig.meshTag = EditorGUILayout.TextField("Mesh标签", factoryConfig.meshTag);
            DrawPresetButtons();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            if (GUILayout.Button("一键生成", GUILayout.Height(32)))
            {
                factoryResult = VFXAssembler.GeneratePrefab(recipe, factoryConfig);
                selectedQualityReport = factoryResult.qualityReport;
                previewPrefab = LoadPrefab(factoryResult.prefabPath);
            }

            if (factoryResult != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(factoryResult.success ? "生成成功" : "生成失败", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(factoryResult.message, EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(factoryResult.prefabPath))
                {
                    EditorGUILayout.SelectableLabel(factoryResult.prefabPath, EditorStyles.textField, GUILayout.Height(18));
                    if (GUILayout.Button("定位Prefab", GUILayout.Width(90)))
                    {
                        Object obj = AssetDatabase.LoadAssetAtPath<Object>(factoryResult.prefabPath);
                        if (obj != null) EditorGUIUtility.PingObject(obj);
                    }
                }
                EditorGUILayout.EndVertical();
                DrawQualityReport(factoryResult.qualityReport);
            }
        }

        private void DrawRecipeEditor(VFXRecipe recipe)
        {
            if (recipe == null) return;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("配方编辑", EditorStyles.boldLabel);
            recipe.recipeName = EditorGUILayout.TextField("配方名", recipe.recipeName);
            recipe.description = EditorGUILayout.TextField("描述", recipe.description);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("添加粒子", GUILayout.Width(80)))
                recipe.layers.Add(new VFXRecipeLayer { name = "Particle" + (recipe.layers.Count + 1), kind = VFXRecipeLayerKind.Particle });
            if (GUILayout.Button("添加网格", GUILayout.Width(80)))
                recipe.layers.Add(new VFXRecipeLayer { name = "Mesh" + (recipe.layers.Count + 1), kind = VFXRecipeLayerKind.Mesh });
            if (GUILayout.Button("添加拖尾", GUILayout.Width(80)))
                recipe.layers.Add(new VFXRecipeLayer { name = "Trail" + (recipe.layers.Count + 1), kind = VFXRecipeLayerKind.Trail });
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < recipe.layers.Count; i++)
            {
                VFXRecipeLayer layer = recipe.layers[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                layer.name = EditorGUILayout.TextField("层名", layer.name);
                if (GUILayout.Button("删除", GUILayout.Width(50)))
                {
                    recipe.layers.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();

                DrawRecipeLayerKindButtons(layer);
                layer.parentName = EditorGUILayout.TextField("父级", layer.parentName);
                layer.subEmitterParentName = EditorGUILayout.TextField("子发源", layer.subEmitterParentName);
                layer.materialSourceName = EditorGUILayout.TextField("材质源", layer.materialSourceName);
                layer.color = EditorGUILayout.ColorField("颜色", layer.color);
                layer.sortingOrder = EditorGUILayout.IntField("排序", layer.sortingOrder);
                layer.renderQueue = EditorGUILayout.IntField("队列", layer.renderQueue);
                if (!string.IsNullOrEmpty(layer.subEmitterParentName))
                    DrawSubEmitterTypeButtons(layer);

                if (layer.kind == VFXRecipeLayerKind.Mesh)
                {
                    layer.meshSize = EditorGUILayout.Vector2Field("尺寸", layer.meshSize);
                }
                else if (layer.kind == VFXRecipeLayerKind.Trail)
                {
                    layer.trailTime = EditorGUILayout.FloatField("时间", layer.trailTime);
                    layer.trailWidth = EditorGUILayout.FloatField("宽度", layer.trailWidth);
                }
                else if (layer.kind == VFXRecipeLayerKind.Particle)
                {
                    layer.duration = EditorGUILayout.FloatField("持续", layer.duration);
                    layer.startLifetime = EditorGUILayout.FloatField("生命", layer.startLifetime);
                    layer.startSpeed = EditorGUILayout.FloatField("速度", layer.startSpeed);
                    layer.startSize = EditorGUILayout.FloatField("大小", layer.startSize);
                    layer.maxParticles = EditorGUILayout.IntField("粒子", layer.maxParticles);
                    layer.emissionRate = EditorGUILayout.FloatField("发射", layer.emissionRate);
                    layer.looping = EditorGUILayout.Toggle("循环", layer.looping);
                    layer.startDelay = EditorGUILayout.FloatField("延迟", layer.startDelay);
                    DrawShapeTypeButtons(layer);
                    layer.shapeRadius = EditorGUILayout.FloatField("半径", layer.shapeRadius);
                    layer.useColorOverLifetime = EditorGUILayout.Toggle("颜色曲线", layer.useColorOverLifetime);
                    if (layer.useColorOverLifetime)
                        layer.colorOverLifetime = EditorGUILayout.GradientField("生命周期", layer.colorOverLifetime);
                    layer.useSizeOverLifetime = EditorGUILayout.Toggle("大小曲线", layer.useSizeOverLifetime);
                    if (layer.useSizeOverLifetime)
                    {
                        layer.sizeOverLifetime = EditorGUILayout.CurveField("生命周期", layer.sizeOverLifetime);
                        layer.sizeOverLifetimeMultiplier = EditorGUILayout.FloatField("曲线倍率", layer.sizeOverLifetimeMultiplier);
                    }
                    layer.useTextureSheetAnimation = EditorGUILayout.Toggle("序列帧", layer.useTextureSheetAnimation);
                    if (layer.useTextureSheetAnimation)
                    {
                        layer.textureSheetTilesX = EditorGUILayout.IntField("横向格", layer.textureSheetTilesX);
                        layer.textureSheetTilesY = EditorGUILayout.IntField("纵向格", layer.textureSheetTilesY);
                        layer.textureSheetFrameOverTime = EditorGUILayout.FloatField("帧进度", layer.textureSheetFrameOverTime);
                    }
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawShapeTypeButtons(VFXRecipeLayer layer)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("形状", GUILayout.Width(60));
            if (GUILayout.Toggle(layer.shapeType == ParticleSystemShapeType.Sphere, "球", EditorStyles.miniButtonLeft))
                layer.shapeType = ParticleSystemShapeType.Sphere;
            if (GUILayout.Toggle(layer.shapeType == ParticleSystemShapeType.Circle, "圆", EditorStyles.miniButtonMid))
                layer.shapeType = ParticleSystemShapeType.Circle;
            if (GUILayout.Toggle(layer.shapeType == ParticleSystemShapeType.Cone, "锥", EditorStyles.miniButtonMid))
                layer.shapeType = ParticleSystemShapeType.Cone;
            if (GUILayout.Toggle(layer.shapeType == ParticleSystemShapeType.Box, "盒", EditorStyles.miniButtonRight))
                layer.shapeType = ParticleSystemShapeType.Box;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBlueprintShapeTypeButtons(VFXBlueprintNode node)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("形状", GUILayout.Width(60));
            if (GUILayout.Toggle(node.shapeType == ParticleSystemShapeType.Sphere, "球", EditorStyles.miniButtonLeft))
                node.shapeType = ParticleSystemShapeType.Sphere;
            if (GUILayout.Toggle(node.shapeType == ParticleSystemShapeType.Circle, "圆", EditorStyles.miniButtonMid))
                node.shapeType = ParticleSystemShapeType.Circle;
            if (GUILayout.Toggle(node.shapeType == ParticleSystemShapeType.Cone, "锥", EditorStyles.miniButtonMid))
                node.shapeType = ParticleSystemShapeType.Cone;
            if (GUILayout.Toggle(node.shapeType == ParticleSystemShapeType.Box, "盒", EditorStyles.miniButtonRight))
                node.shapeType = ParticleSystemShapeType.Box;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSubEmitterTypeButtons(VFXRecipeLayer layer)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("触发", GUILayout.Width(60));
            if (GUILayout.Toggle(layer.subEmitterType == ParticleSystemSubEmitterType.Death, "死亡", EditorStyles.miniButtonLeft))
                layer.subEmitterType = ParticleSystemSubEmitterType.Death;
            if (GUILayout.Toggle(layer.subEmitterType == ParticleSystemSubEmitterType.Birth, "出生", EditorStyles.miniButtonMid))
                layer.subEmitterType = ParticleSystemSubEmitterType.Birth;
            if (GUILayout.Toggle(layer.subEmitterType == ParticleSystemSubEmitterType.Collision, "碰撞", EditorStyles.miniButtonRight))
                layer.subEmitterType = ParticleSystemSubEmitterType.Collision;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRecipeLayerKindButtons(VFXRecipeLayer layer)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("类型", GUILayout.Width(60));
            if (GUILayout.Toggle(layer.kind == VFXRecipeLayerKind.Particle, "粒子", EditorStyles.miniButtonLeft))
                layer.kind = VFXRecipeLayerKind.Particle;
            if (GUILayout.Toggle(layer.kind == VFXRecipeLayerKind.Mesh, "网格", EditorStyles.miniButtonMid))
                layer.kind = VFXRecipeLayerKind.Mesh;
            if (GUILayout.Toggle(layer.kind == VFXRecipeLayerKind.Trail, "拖尾", EditorStyles.miniButtonMid))
                layer.kind = VFXRecipeLayerKind.Trail;
            if (GUILayout.Toggle(layer.kind == VFXRecipeLayerKind.MaterialSlot, "材质", EditorStyles.miniButtonRight))
                layer.kind = VFXRecipeLayerKind.MaterialSlot;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void ImportRecipe()
        {
            EnsurePresetFolders();
            string path = EditorUtility.OpenFilePanel("导入配方", GetAbsoluteAssetPath(RECIPE_PRESET_DIR), "json");
            VFXRecipe recipe = VFXRecipeSerializer.Load(path);
            if (recipe == null) return;
            factoryRecipes.Add(recipe);
            factoryConfig.selectedRecipeIndex = factoryRecipes.Count - 1;
        }

        private void ExportRecipe(VFXRecipe recipe)
        {
            if (recipe == null) return;
            EnsurePresetFolders();
            string path = EditorUtility.SaveFilePanel("导出配方", GetAbsoluteAssetPath(RECIPE_PRESET_DIR), recipe.recipeName, "json");
            if (!string.IsNullOrEmpty(path))
            {
                VFXRecipeSerializer.Save(path, recipe);
                AssetDatabase.Refresh();
            }
        }

        private void DuplicateRecipe(VFXRecipe recipe)
        {
            if (recipe == null) return;
            VFXRecipe copy = JsonUtility.FromJson<VFXRecipe>(JsonUtility.ToJson(recipe));
            copy.recipeName = recipe.recipeName + "_Copy";
            factoryRecipes.Add(copy);
            factoryConfig.selectedRecipeIndex = factoryRecipes.Count - 1;
        }

        private void LoadRecipePresetsFromDisk()
        {
            EnsurePresetFolders();
            string dir = GetAbsoluteAssetPath(RECIPE_PRESET_DIR);
            if (!Directory.Exists(dir)) return;
            foreach (string path in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    VFXRecipe recipe = VFXRecipeSerializer.Load(path);
                    if (recipe == null || string.IsNullOrEmpty(recipe.recipeName)) continue;
                    if (factoryRecipes.Exists(r => r != null && r.recipeName == recipe.recipeName)) continue;
                    factoryRecipes.Add(recipe);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("加载配方预设失败：" + path + "\n" + ex.Message);
                }
            }
        }

        private void EnsurePresetFolders()
        {
            Moshi_VFXPrefabUtil.EnsureFolder(PRESET_ROOT);
            Moshi_VFXPrefabUtil.EnsureFolder(RECIPE_PRESET_DIR);
            Moshi_VFXPrefabUtil.EnsureFolder(CONFIG_PRESET_DIR);
        }

        private string GetAbsoluteAssetPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private void DrawBlueprintNodeTypeButtons(VFXBlueprintNode node)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("类型", GUILayout.Width(60));
            if (GUILayout.Toggle(node.nodeType == VFXBlueprintNodeType.ParticleLayer, "粒子", EditorStyles.miniButtonLeft))
                node.nodeType = VFXBlueprintNodeType.ParticleLayer;
            if (GUILayout.Toggle(node.nodeType == VFXBlueprintNodeType.MeshLayer, "网格", EditorStyles.miniButtonMid))
                node.nodeType = VFXBlueprintNodeType.MeshLayer;
            if (GUILayout.Toggle(node.nodeType == VFXBlueprintNodeType.TrailLayer, "拖尾", EditorStyles.miniButtonMid))
                node.nodeType = VFXBlueprintNodeType.TrailLayer;
            if (GUILayout.Toggle(node.nodeType == VFXBlueprintNodeType.MaterialSlot, "材质", EditorStyles.miniButtonRight))
                node.nodeType = VFXBlueprintNodeType.MaterialSlot;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBlueprintTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("轻量蓝图", EditorStyles.boldLabel);
            blueprint.blueprintName = EditorGUILayout.TextField("蓝图名称", blueprint.blueprintName);
            EditorGUILayout.HelpBox("当前支持轻量列表编辑，也可以打开节点画布进行可视化编辑和生成。", MessageType.Info);

            if (GUILayout.Button("打开画布", GUILayout.Width(90)))
                Moshi_VFXBlueprintWindow.ShowWindow();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("添加粒子", GUILayout.Width(80)))
                blueprint.nodes.Add(VFXNodePresets.CreateCore(Vector2.zero));
            if (GUILayout.Button("添加网格", GUILayout.Width(80)))
                blueprint.nodes.Add(VFXNodePresets.CreateMesh(Vector2.zero));
            if (GUILayout.Button("添加拖尾", GUILayout.Width(80)))
                blueprint.nodes.Add(VFXNodePresets.CreateTrail(Vector2.zero));
            if (GUILayout.Button("添加材质", GUILayout.Width(80)))
                blueprint.nodes.Add(VFXNodePresets.CreateMaterialSlot(Vector2.zero));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < blueprint.nodes.Count; i++)
            {
                VFXBlueprintNode node = blueprint.nodes[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                node.name = EditorGUILayout.TextField("层名", node.name);
                if (GUILayout.Button("删除", GUILayout.Width(50)))
                {
                    blueprint.nodes.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
                DrawBlueprintNodeTypeButtons(node);
                node.color = EditorGUILayout.ColorField("颜色", node.color);
                node.sortingOrder = EditorGUILayout.IntField("排序", node.sortingOrder);
                node.renderQueue = EditorGUILayout.IntField("队列", node.renderQueue);
                if (node.nodeType == VFXBlueprintNodeType.MeshLayer)
                {
                    node.meshSize = EditorGUILayout.Vector2Field("尺寸", node.meshSize);
                }
                else if (node.nodeType == VFXBlueprintNodeType.TrailLayer)
                {
                    node.trailTime = EditorGUILayout.FloatField("时间", node.trailTime);
                    node.trailWidth = EditorGUILayout.FloatField("宽度", node.trailWidth);
                }
                else if (node.nodeType != VFXBlueprintNodeType.MaterialSlot)
                {
                    node.duration = EditorGUILayout.FloatField("持续", node.duration);
                    node.startLifetime = EditorGUILayout.FloatField("生命", node.startLifetime);
                    node.startSpeed = EditorGUILayout.FloatField("速度", node.startSpeed);
                    node.startSize = EditorGUILayout.FloatField("大小", node.startSize);
                    node.maxParticles = EditorGUILayout.IntField("粒子", node.maxParticles);
                    node.emissionRate = EditorGUILayout.FloatField("发射", node.emissionRate);
                    node.looping = EditorGUILayout.Toggle("循环", node.looping);
                    node.startDelay = EditorGUILayout.FloatField("延迟", node.startDelay);
                    DrawBlueprintShapeTypeButtons(node);
                    node.shapeRadius = EditorGUILayout.FloatField("半径", node.shapeRadius);
                    node.useColorOverLifetime = EditorGUILayout.Toggle("颜色曲线", node.useColorOverLifetime);
                    if (node.useColorOverLifetime)
                        node.colorOverLifetime = EditorGUILayout.GradientField("生命周期", node.colorOverLifetime);
                    node.useSizeOverLifetime = EditorGUILayout.Toggle("大小曲线", node.useSizeOverLifetime);
                    if (node.useSizeOverLifetime)
                    {
                        node.sizeOverLifetime = EditorGUILayout.CurveField("生命周期", node.sizeOverLifetime);
                        node.sizeOverLifetimeMultiplier = EditorGUILayout.FloatField("曲线倍率", node.sizeOverLifetimeMultiplier);
                    }
                    node.useTextureSheetAnimation = EditorGUILayout.Toggle("序列帧", node.useTextureSheetAnimation);
                    if (node.useTextureSheetAnimation)
                    {
                        node.textureSheetTilesX = EditorGUILayout.IntField("横向格", node.textureSheetTilesX);
                        node.textureSheetTilesY = EditorGUILayout.IntField("纵向格", node.textureSheetTilesY);
                        node.textureSheetFrameOverTime = EditorGUILayout.FloatField("帧进度", node.textureSheetFrameOverTime);
                    }
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("蓝图生成", EditorStyles.boldLabel);
            factoryConfig.effectName = EditorGUILayout.TextField("输出名称", string.IsNullOrEmpty(factoryConfig.effectName) ? blueprint.blueprintName : factoryConfig.effectName);
            factoryConfig.outputRoot = EditorGUILayout.TextField("输出目录", string.IsNullOrEmpty(factoryConfig.outputRoot) ? settings.defaultOutputRoot : factoryConfig.outputRoot);
            EditorGUI.BeginDisabledGroup(blueprint.nodes.Count == 0);
            if (GUILayout.Button("蓝图生成", GUILayout.Height(32)))
            {
                blueprintResult = VFXBlueprintGenerator.Generate(blueprint, factoryConfig);
                selectedQualityReport = blueprintResult.qualityReport;
                previewPrefab = LoadPrefab(blueprintResult.prefabPath);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();

            if (blueprintResult != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(blueprintResult.success ? "生成成功" : "生成失败", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(blueprintResult.message, EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(blueprintResult.prefabPath) && GUILayout.Button("定位Prefab", GUILayout.Width(90)))
                {
                    Object obj = AssetDatabase.LoadAssetAtPath<Object>(blueprintResult.prefabPath);
                    if (obj != null) EditorGUIUtility.PingObject(obj);
                }
                EditorGUILayout.EndVertical();
                DrawQualityReport(blueprintResult.qualityReport);
            }
        }

        private void DrawQualityTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("单独质检", EditorStyles.boldLabel);
            DrawQualityBudgetSettings();
            qualityTargetPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", qualityTargetPrefab, typeof(GameObject), false);
            EditorGUI.BeginDisabledGroup(qualityTargetPrefab == null);
            if (GUILayout.Button("执行质检", GUILayout.Height(28)))
            {
                string path = AssetDatabase.GetAssetPath(qualityTargetPrefab);
                selectedQualityReport = Moshi_VFXQualityCheck.CheckPrefab(path, settings);
            }
            if (GUILayout.Button("自动修复", GUILayout.Height(28)))
            {
                string path = AssetDatabase.GetAssetPath(qualityTargetPrefab);
                if (EditorUtility.DisplayDialog(TOOL_NAME, "自动修复会修改当前 Prefab 的粒子量和明显异常参数，是否继续？", "修复", "取消"))
                    selectedQualityReport = Moshi_VFXQualityCheck.AutoFixPrefab(path, settings);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();

            DrawQualityReport(selectedQualityReport);
        }

        private void DrawQualityBudgetSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("预算设置", EditorStyles.boldLabel);
            settings.warningMaxParticles = EditorGUILayout.IntField("粒子警告", settings.warningMaxParticles);
            settings.criticalMaxParticles = EditorGUILayout.IntField("粒子严重", settings.criticalMaxParticles);
            settings.warningParticleSystems = EditorGUILayout.IntField("系统警告", settings.warningParticleSystems);
            settings.warningMaterials = EditorGUILayout.IntField("材质警告", settings.warningMaterials);
            settings.warningShaders = EditorGUILayout.IntField("Shader警告", settings.warningShaders);
            settings.warningLoopingParticles = EditorGUILayout.IntField("循环警告", settings.warningLoopingParticles);
            settings.autoFixTargetMaxParticles = EditorGUILayout.IntField("修复总量", settings.autoFixTargetMaxParticles);
            settings.autoFixMaxParticlesPerSystem = EditorGUILayout.IntField("单层上限", settings.autoFixMaxParticlesPerSystem);
            if (GUILayout.Button("保存预算", GUILayout.Width(80)))
                settings.Save();
            EditorGUILayout.EndVertical();
        }

        private void DrawHistoryTab()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"历史记录：{history.records.Count}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("清空历史", GUILayout.Width(80)))
                history.Clear();
            EditorGUILayout.EndHorizontal();

            foreach (VFXHistoryRecord record in history.records)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{record.time}  {record.operation}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"源：{record.sourcePath}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"输出：{record.outputPath}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"成功：{record.successCount}  失败：{record.failCount}  Seed：{record.randomSeed}", EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                if (!string.IsNullOrEmpty(record.outputPath) && GUILayout.Button("定位输出", GUILayout.Width(80)))
                {
                    Object obj = AssetDatabase.LoadAssetAtPath<Object>(record.outputPath);
                    if (obj != null)
                    {
                        EditorGUIUtility.PingObject(obj);
                        previewPrefab = obj as GameObject;
                    }
                }
                if (!string.IsNullOrEmpty(record.reportPath) && GUILayout.Button("打开报告", GUILayout.Width(80)))
                {
                    Object report = AssetDatabase.LoadAssetAtPath<Object>(record.reportPath);
                    if (report != null) AssetDatabase.OpenAsset(report);
                }
                if (GUILayout.Button("删除输出", GUILayout.Width(80)))
                {
                    DeleteHistoryOutput(record);
                    GUIUtility.ExitGUI();
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawStage(string name, string desc, bool done)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(done ? "完成" : "待做", GUILayout.Width(42));
            EditorGUILayout.LabelField(name, GUILayout.Width(70));
            EditorGUILayout.LabelField(desc);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEmbeddedPreview()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("内嵌预览", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            previewPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", previewPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
                CleanupPreviewContext();
            if (previewPrefab == null)
            {
                EditorGUILayout.HelpBox("生成或从历史定位 Prefab 后，这里会显示实时预览。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EnsurePreviewContext();
            Rect rect = GUILayoutUtility.GetRect(220f, 220f, GUILayout.ExpandWidth(true));
            if (previewContext != null)
            {
                previewContext.SetBackgroundColor(previewBackgroundColor);
                double now = EditorApplication.timeSinceStartup;
                float delta = Mathf.Clamp((float)(now - previewLastTime), 0f, 0.05f);
                previewLastTime = now;
                if (previewIsPlaying)
                {
                    float scaledDelta = delta * Mathf.Clamp(previewSpeed, 0.05f, 5f);
                    previewContext.StepSimulation(scaledDelta, delta, 1f);
                    Repaint();
                }
                HandlePreviewInput(rect);
                Texture texture = previewContext.Render(Mathf.Max(1, Mathf.RoundToInt(rect.width)), Mathf.Max(1, Mathf.RoundToInt(rect.height)));
                if (texture != null)
                    GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
                else
                    EditorGUI.LabelField(rect, "预览初始化中...");
            }
            else
            {
                Texture preview = AssetPreview.GetAssetPreview(previewPrefab) ?? AssetPreview.GetMiniThumbnail(previewPrefab);
                if (preview != null)
                    GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
                else
                    EditorGUI.LabelField(rect, "预览生成中...");
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(previewIsPlaying ? "暂停" : "播放", GUILayout.Width(60)))
                previewIsPlaying = !previewIsPlaying;
            if (GUILayout.Button("重播", GUILayout.Width(60)))
            {
                previewContext?.Restart();
                previewLastTime = EditorApplication.timeSinceStartup;
            }
            if (GUILayout.Button("重置", GUILayout.Width(60)))
                previewContext?.ResetCamera();
            previewSpeed = EditorGUILayout.Slider(previewSpeed, 0.1f, 3f, GUILayout.Width(120));
            previewBackgroundColor = EditorGUILayout.ColorField(previewBackgroundColor, GUILayout.Width(60));
            if (GUILayout.Button("定位Prefab", GUILayout.Width(90)))
                EditorGUIUtility.PingObject(previewPrefab);
            if (GUILayout.Button("浏览器查看", GUILayout.Width(90)))
                Moshi_VfxBrowserCreater.ShowWindow();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("预览区：左键拖拽旋转，右键拖拽平移，滚轮缩放。", MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        private void EnsurePreviewContext()
        {
            if (previewPrefab == null) return;
            if (previewContext != null && previewContextPrefab == previewPrefab) return;
            CleanupPreviewContext();
            previewContextPrefab = previewPrefab;
            previewLastTime = EditorApplication.timeSinceStartup;
            try
            {
                previewContext = new Moshi_VfxBrowserContext(previewPrefab);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Moshi特效生成器预览初始化失败：" + ex.Message);
                previewContext = null;
                previewContextPrefab = null;
            }
        }

        private void CleanupPreviewContext()
        {
            if (previewContext != null)
            {
                previewContext.Cleanup();
                previewContext = null;
            }
            previewContextPrefab = null;
        }

        private void HandlePreviewInput(Rect rect)
        {
            Event e = Event.current;
            if (previewContext == null || !rect.Contains(e.mousePosition)) return;
            if (e.type == EventType.ScrollWheel)
            {
                previewContext.AdjustDistanceMultiplier(-e.delta.y);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0)
            {
                previewContext.AdjustRotation(e.delta.x, -e.delta.y);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 1)
            {
                previewContext.AdjustPan(-e.delta.x / Mathf.Max(1f, rect.width), e.delta.y / Mathf.Max(1f, rect.height));
                e.Use();
            }
        }

        private GameObject GetFirstGeneratedPrefab(List<VFXCloneResult> results)
        {
            if (results == null) return null;
            foreach (VFXCloneResult result in results)
            {
                GameObject prefab = LoadPrefab(result.prefabPath);
                if (prefab != null) return prefab;
            }
            return null;
        }

        private GameObject LoadPrefab(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath)) return null;
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private void DeleteHistoryOutput(VFXHistoryRecord record)
        {
            if (record == null) return;
            if (!EditorUtility.DisplayDialog(TOOL_NAME, "确定删除本次生成的输出资源吗？", "删除", "取消")) return;

            string folder = GetGeneratedRootFolder(record.outputPath);
            if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder))
                AssetDatabase.DeleteAsset(folder);
            else if (!string.IsNullOrEmpty(record.outputPath))
                AssetDatabase.DeleteAsset(record.outputPath);

            if (previewPrefab != null && AssetDatabase.GetAssetPath(previewPrefab) == record.outputPath)
                previewPrefab = null;
            history.RemoveRecord(record);
            AssetDatabase.Refresh();
        }

        private string GetGeneratedRootFolder(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath)) return string.Empty;
            outputPath = outputPath.Replace('\\', '/');
            int prefabIndex = outputPath.IndexOf("/Prefab/", System.StringComparison.Ordinal);
            if (prefabIndex > 0) return outputPath.Substring(0, prefabIndex);
            return System.IO.Path.GetDirectoryName(outputPath)?.Replace('\\', '/');
        }

        private void DrawAssetSummary()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("素材库摘要", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"最后扫描：{(string.IsNullOrEmpty(assetDB.lastScanTime) ? "未扫描" : assetDB.lastScanTime)}");
            EditorGUILayout.LabelField($"Shader：{assetDB.CountByKind(VFXAssetKind.Shader)}    材质：{assetDB.CountByKind(VFXAssetKind.Material)}    贴图：{assetDB.CountByKind(VFXAssetKind.Texture)}    Mesh：{assetDB.CountByKind(VFXAssetKind.Mesh)}    Prefab：{assetDB.CountByKind(VFXAssetKind.Prefab)}");
            EditorGUILayout.EndVertical();
        }

        private void DrawAssetList()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("素材筛选", EditorStyles.boldLabel);
            assetSearchText = EditorGUILayout.TextField("关键词", assetSearchText);
            assetTagFilter = EditorGUILayout.TextField("标签", assetTagFilter);
            assetFavoritesOnly = EditorGUILayout.Toggle("仅收藏", assetFavoritesOnly);
            DrawAssetKindButtons();
            EditorGUILayout.BeginHorizontal();
            assetBatchTag = EditorGUILayout.TextField("批量标签", assetBatchTag);
            if (GUILayout.Button("追加", GUILayout.Width(50)))
                EditorUtility.DisplayDialog(TOOL_NAME, $"已处理 {assetDB.BatchAppendTag(assetKindFilter, assetSearchText, assetTagFilter, assetFavoritesOnly, assetBatchTag)} 个素材", "确定");
            if (GUILayout.Button("设主", GUILayout.Width(50)))
                EditorUtility.DisplayDialog(TOOL_NAME, $"已处理 {assetDB.BatchSetMainTag(assetKindFilter, assetSearchText, assetTagFilter, assetFavoritesOnly, assetBatchTag)} 个素材", "确定");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField("素材列表", EditorStyles.boldLabel);
            int shown = 0;
            int matched = 0;
            const int maxCount = 160;
            foreach (VFXAssetInfo info in assetDB.assets)
            {
                if (!IsAssetMatched(info)) continue;
                matched++;
                if (shown >= maxCount) continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                bool newFavorite = GUILayout.Toggle(info.favorite, "★", GUILayout.Width(24));
                if (newFavorite != info.favorite)
                    assetDB.SetFavorite(info, newFavorite);
                EditorGUILayout.LabelField(GetAssetKindLabel(info.kind), GUILayout.Width(45));
                EditorGUI.BeginChangeCheck();
                string mainTagText = EditorGUILayout.TextField(info.mainTag, GUILayout.Width(70));
                string tagsText = EditorGUILayout.TextField(string.Join(",", info.tags), GUILayout.Width(140));
                if (EditorGUI.EndChangeCheck())
                {
                    assetDB.SetTags(info, tagsText);
                    assetDB.SetMainTag(info, mainTagText);
                }
                EditorGUILayout.LabelField(info.name, GUILayout.Width(160));
                EditorGUILayout.LabelField(info.path, EditorStyles.miniLabel);
                if (GUILayout.Button("预览", GUILayout.Width(45)))
                    PreviewAsset(info);
                if (GUILayout.Button("定位", GUILayout.Width(45)))
                {
                    Object obj = AssetDatabase.LoadAssetAtPath<Object>(info.path);
                    if (obj != null) EditorGUIUtility.PingObject(obj);
                }
                EditorGUILayout.EndHorizontal();
                shown++;
            }
            EditorGUILayout.HelpBox($"匹配 {matched} 条，显示 {shown} 条。", MessageType.Info);
        }

        private void DrawAssetKindButtons()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("类型", GUILayout.Width(60));
            if (GUILayout.Toggle(assetKindFilter == VFXAssetKind.Unknown, "全部", EditorStyles.miniButtonLeft))
                assetKindFilter = VFXAssetKind.Unknown;
            if (GUILayout.Toggle(assetKindFilter == VFXAssetKind.Material, "材质", EditorStyles.miniButtonMid))
                assetKindFilter = VFXAssetKind.Material;
            if (GUILayout.Toggle(assetKindFilter == VFXAssetKind.Texture, "贴图", EditorStyles.miniButtonMid))
                assetKindFilter = VFXAssetKind.Texture;
            if (GUILayout.Toggle(assetKindFilter == VFXAssetKind.Mesh, "网格", EditorStyles.miniButtonMid))
                assetKindFilter = VFXAssetKind.Mesh;
            if (GUILayout.Toggle(assetKindFilter == VFXAssetKind.Prefab, "预制", EditorStyles.miniButtonMid))
                assetKindFilter = VFXAssetKind.Prefab;
            if (GUILayout.Toggle(assetKindFilter == VFXAssetKind.Shader, "着色", EditorStyles.miniButtonRight))
                assetKindFilter = VFXAssetKind.Shader;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private bool IsAssetMatched(VFXAssetInfo info)
        {
            if (info == null) return false;
            if (assetFavoritesOnly && !info.favorite) return false;
            if (assetKindFilter != VFXAssetKind.Unknown && info.kind != assetKindFilter) return false;
            if (!string.IsNullOrEmpty(assetSearchText))
            {
                string lower = assetSearchText.ToLowerInvariant();
                string target = (info.name + " " + info.path + " " + info.shaderName).ToLowerInvariant();
                if (!target.Contains(lower)) return false;
            }
            if (!string.IsNullOrEmpty(assetTagFilter) && !IsAssetTagMatched(info, assetTagFilter)) return false;
            return true;
        }

        private bool IsAssetTagMatched(VFXAssetInfo info, string tag)
        {
            if (info == null || string.IsNullOrEmpty(tag)) return true;
            if (!string.IsNullOrEmpty(info.mainTag) && info.mainTag.Contains(tag)) return true;
            foreach (string item in info.tags)
            {
                if (!string.IsNullOrEmpty(item) && item.Contains(tag)) return true;
            }
            return false;
        }

        private string GetAssetKindLabel(VFXAssetKind kind)
        {
            switch (kind)
            {
                case VFXAssetKind.Material: return "材质";
                case VFXAssetKind.Texture: return "贴图";
                case VFXAssetKind.Mesh: return "网格";
                case VFXAssetKind.Prefab: return "预制";
                case VFXAssetKind.Shader: return "着色";
                default: return "其他";
            }
        }

        private void PreviewAsset(VFXAssetInfo info)
        {
            if (info == null) return;
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(info.path);
            if (obj == null) return;
            if (obj is GameObject gameObject)
            {
                previewPrefab = gameObject;
                currentTab = MainTab.Overview;
            }
            else
            {
                EditorGUIUtility.PingObject(obj);
            }
        }

        private void DrawSourceInfo(VFXPrefabInfo info)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("源结构", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Prefab：{info.prefabName}");
            EditorGUILayout.LabelField($"粒子系统：{info.particleSystemCount}    Renderer：{info.rendererCount}    材质：{info.materialCount}    总粒子量：{info.totalMaxParticles}");
            EditorGUILayout.LabelField("层级控制：勾选表示该层参与对应操作", EditorStyles.miniBoldLabel);
            layerScroll = EditorGUILayout.BeginScrollView(layerScroll, GUILayout.Height(160));
            foreach (VFXLayerInfo layer in info.layers)
            {
                EditorGUILayout.BeginHorizontal();
                bool colorEnabled = !cloneConfig.excludedColorLayerPaths.Contains(layer.path);
                bool textureEnabled = !cloneConfig.excludedTextureLayerPaths.Contains(layer.path);
                bool paramEnabled = !cloneConfig.excludedParamLayerPaths.Contains(layer.path);
                bool newColor = GUILayout.Toggle(colorEnabled, "色", GUILayout.Width(32));
                bool newTexture = GUILayout.Toggle(textureEnabled, "图", GUILayout.Width(32));
                bool newParam = GUILayout.Toggle(paramEnabled, "参", GUILayout.Width(32));
                SetLayerEnabled(cloneConfig.excludedColorLayerPaths, layer.path, newColor);
                SetLayerEnabled(cloneConfig.excludedTextureLayerPaths, layer.path, newTexture);
                SetLayerEnabled(cloneConfig.excludedParamLayerPaths, layer.path, newParam);
                EditorGUILayout.LabelField($"[{layer.componentType}] {layer.path}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void SetLayerEnabled(List<string> excludedList, string path, bool enabled)
        {
            if (excludedList == null || string.IsNullOrEmpty(path)) return;
            if (enabled)
                excludedList.Remove(path);
            else if (!excludedList.Contains(path))
                excludedList.Add(path);
        }

        private void DrawCloneConfig()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("变体配置", EditorStyles.boldLabel);
            cloneConfig.outputRoot = EditorGUILayout.TextField("输出目录", string.IsNullOrEmpty(cloneConfig.outputRoot) ? settings.defaultOutputRoot : cloneConfig.outputRoot);
            cloneConfig.variantCount = Mathf.Max(1, EditorGUILayout.IntField("变体数量", cloneConfig.variantCount));
            cloneConfig.namePattern = EditorGUILayout.TextField("命名规则", cloneConfig.namePattern);
            cloneConfig.hueStep = EditorGUILayout.Slider("色相步进", cloneConfig.hueStep, 0f, 1f);
            if (cloneConfig.colorMode == VFXCloneColorMode.Palette)
                cloneConfig.paletteColor = EditorGUILayout.ColorField("调色颜色", cloneConfig.paletteColor);
            cloneConfig.parameterMultiplier = EditorGUILayout.Slider("参数倍率", cloneConfig.parameterMultiplier, 0.1f, 5f);
            cloneConfig.randomSeed = EditorGUILayout.IntField("随机种子", cloneConfig.randomSeed);
            cloneConfig.cloneMaterials = EditorGUILayout.Toggle("复制材质", cloneConfig.cloneMaterials);
            cloneConfig.runQualityCheck = EditorGUILayout.Toggle("生成后质检", cloneConfig.runQualityCheck);
            DrawPresetButtons();

            DrawColorModeButtons();
            DrawParamModeButtons();
            DrawTextureModeButtons();
            if (cloneConfig.textureMode == VFXCloneTextureMode.Manual)
                cloneConfig.replacementTexture = (Texture2D)EditorGUILayout.ObjectField("替换贴图", cloneConfig.replacementTexture, typeof(Texture2D), false);
            else if (cloneConfig.textureMode == VFXCloneTextureMode.ByTag)
                cloneConfig.textureTag = EditorGUILayout.TextField("贴图标签", cloneConfig.textureTag);
            EditorGUILayout.EndVertical();
        }

        private void DrawPresetButtons()
        {
            EditorGUILayout.BeginHorizontal();
            activePreset = (Moshi_VFXGenPreset)EditorGUILayout.ObjectField("配置预设", activePreset, typeof(Moshi_VFXGenPreset), false);
            if (GUILayout.Button("保存", GUILayout.Width(50)))
                SavePreset();
            EditorGUI.BeginDisabledGroup(activePreset == null);
            if (GUILayout.Button("加载", GUILayout.Width(50)))
                LoadPreset(activePreset);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private void SavePreset()
        {
            EnsurePresetFolders();
            string path = EditorUtility.SaveFilePanelInProject("保存生成器配置", "Moshi_VFXGenPreset", "asset", "保存当前克隆器和配方生成配置", CONFIG_PRESET_DIR);
            if (string.IsNullOrEmpty(path)) return;

            var preset = CreateInstance<Moshi_VFXGenPreset>();
            CopyCloneConfig(cloneConfig, preset.cloneConfig);
            CopyFactoryConfig(factoryConfig, preset.factoryConfig);
            AssetDatabase.CreateAsset(preset, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            activePreset = preset;
            EditorGUIUtility.PingObject(preset);
        }

        private void LoadPreset(Moshi_VFXGenPreset preset)
        {
            if (preset == null) return;
            CopyCloneConfig(preset.cloneConfig, cloneConfig);
            CopyFactoryConfig(preset.factoryConfig, factoryConfig);
            sourceInfo = cloneConfig.sourcePrefab != null ? VFXPrefabScanner.Scan(cloneConfig.sourcePrefab) : null;
        }

        private void CopyCloneConfig(VFXCloneConfig source, VFXCloneConfig target)
        {
            if (source == null || target == null) return;
            target.sourcePrefab = source.sourcePrefab;
            target.outputRoot = source.outputRoot;
            target.variantCount = source.variantCount;
            target.namePattern = source.namePattern;
            target.colorMode = source.colorMode;
            target.paramMode = source.paramMode;
            target.textureMode = source.textureMode;
            target.hueStep = source.hueStep;
            target.paletteColor = source.paletteColor;
            target.parameterMultiplier = source.parameterMultiplier;
            target.replacementTexture = source.replacementTexture;
            target.textureTag = source.textureTag;
            target.randomSeed = source.randomSeed;
            target.cloneMaterials = source.cloneMaterials;
            target.runQualityCheck = source.runQualityCheck;
            target.excludedColorLayerPaths = new List<string>(source.excludedColorLayerPaths);
            target.excludedTextureLayerPaths = new List<string>(source.excludedTextureLayerPaths);
            target.excludedParamLayerPaths = new List<string>(source.excludedParamLayerPaths);
        }

        private void CopyFactoryConfig(VFXFactoryConfig source, VFXFactoryConfig target)
        {
            if (source == null || target == null) return;
            target.effectName = source.effectName;
            target.outputRoot = source.outputRoot;
            target.themeColor = source.themeColor;
            target.selectedRecipeIndex = source.selectedRecipeIndex;
            target.runQualityCheck = source.runQualityCheck;
            target.useAssetLibraryMaterial = source.useAssetLibraryMaterial;
            target.materialTag = source.materialTag;
            target.useAssetLibraryTexture = source.useAssetLibraryTexture;
            target.textureTag = source.textureTag;
            target.useAssetLibraryMesh = source.useAssetLibraryMesh;
            target.meshTag = source.meshTag;
        }

        private void DrawColorModeButtons()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("颜色", GUILayout.Width(60));
            if (GUILayout.Toggle(cloneConfig.colorMode == VFXCloneColorMode.HueShift, "色相", EditorStyles.miniButtonLeft))
                cloneConfig.colorMode = VFXCloneColorMode.HueShift;
            if (GUILayout.Toggle(cloneConfig.colorMode == VFXCloneColorMode.Palette, "调色", EditorStyles.miniButtonMid))
                cloneConfig.colorMode = VFXCloneColorMode.Palette;
            if (GUILayout.Toggle(cloneConfig.colorMode == VFXCloneColorMode.Random, "随机", EditorStyles.miniButtonRight))
                cloneConfig.colorMode = VFXCloneColorMode.Random;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawParamModeButtons()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("参数", GUILayout.Width(60));
            if (GUILayout.Toggle(cloneConfig.paramMode == VFXCloneParamMode.None, "不调", EditorStyles.miniButtonLeft))
                cloneConfig.paramMode = VFXCloneParamMode.None;
            if (GUILayout.Toggle(cloneConfig.paramMode == VFXCloneParamMode.Multiplier, "倍率", EditorStyles.miniButtonMid))
                cloneConfig.paramMode = VFXCloneParamMode.Multiplier;
            if (GUILayout.Toggle(cloneConfig.paramMode == VFXCloneParamMode.Random, "随机", EditorStyles.miniButtonRight))
                cloneConfig.paramMode = VFXCloneParamMode.Random;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTextureModeButtons()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("贴图", GUILayout.Width(60));
            if (GUILayout.Toggle(cloneConfig.textureMode == VFXCloneTextureMode.None, "不换", EditorStyles.miniButtonLeft))
                cloneConfig.textureMode = VFXCloneTextureMode.None;
            if (GUILayout.Toggle(cloneConfig.textureMode == VFXCloneTextureMode.Manual, "手动", EditorStyles.miniButtonMid))
                cloneConfig.textureMode = VFXCloneTextureMode.Manual;
            if (GUILayout.Toggle(cloneConfig.textureMode == VFXCloneTextureMode.ByTag, "标签", EditorStyles.miniButtonRight))
                cloneConfig.textureMode = VFXCloneTextureMode.ByTag;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCloneResults()
        {
            if (cloneResults == null || cloneResults.Count == 0) return;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("生成结果", EditorStyles.boldLabel);
            resultScroll = EditorGUILayout.BeginScrollView(resultScroll, GUILayout.Height(180));
            foreach (VFXCloneResult result in cloneResults)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(result.success ? "成功" : "失败", GUILayout.Width(42));
                EditorGUILayout.LabelField(result.variantName, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(result.prefabPath));
                if (GUILayout.Button("定位", GUILayout.Width(50)))
                {
                    Object obj = AssetDatabase.LoadAssetAtPath<Object>(result.prefabPath);
                    if (obj != null) EditorGUIUtility.PingObject(obj);
                }
                if (GUILayout.Button("质检", GUILayout.Width(50)))
                    selectedQualityReport = result.qualityReport ?? Moshi_VFXQualityCheck.CheckPrefab(result.prefabPath);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(result.message, EditorStyles.miniLabel);
                if (result.qualityReport != null)
                    EditorGUILayout.LabelField($"严重：{result.qualityReport.GetCriticalCount()}  警告：{result.qualityReport.GetWarningCount()}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();

            DrawQualityReport(selectedQualityReport);
        }

        private void DrawQualityReport(VFXQualityReport report)
        {
            if (report == null) return;

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("质检报告", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Prefab：{report.prefabPath}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"粒子系统：{report.particleSystemCount}  Renderer：{report.rendererCount}  材质：{report.materialCount}  Shader：{report.shaderCount}  总粒子量：{report.totalMaxParticles}");
            if (report.issues.Count == 0)
            {
                EditorGUILayout.HelpBox("未发现问题。", MessageType.Info);
            }
            else
            {
                foreach (VFXQualityIssue issue in report.issues)
                {
                    MessageType type = issue.severity == VFXIssueSeverity.Critical ? MessageType.Error : issue.severity == VFXIssueSeverity.Warning ? MessageType.Warning : MessageType.Info;
                    EditorGUILayout.HelpBox($"[{issue.severity}] {issue.title}\n{issue.message}\n{issue.objectPath}", type);
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawPlaceholderTab(string title, string desc, string note)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(note, MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        private void EnsureDefaultBlueprint()
        {
            if (blueprint == null)
                blueprint = new VFXBlueprint();
            if (blueprint.nodes.Count > 0) return;

            blueprint.blueprintName = "BlueprintVFX";
            blueprint.nodes.Add(new VFXBlueprintNode
            {
                id = System.Guid.NewGuid().ToString("N"),
                name = "Core",
                nodeType = VFXBlueprintNodeType.ParticleLayer,
                color = new Color(1f, 0.85f, 0.35f, 1f),
                duration = 0.8f,
                startLifetime = 0.45f,
                startSpeed = 1.2f,
                startSize = 0.35f,
                maxParticles = 80,
                emissionRate = 80f
            });
        }

        private void ApplySettingsToCloneConfig(bool overwriteSource)
        {
            if (settings == null) return;
            if (overwriteSource) cloneConfig.sourcePrefab = null;
            cloneConfig.outputRoot = settings.defaultOutputRoot;
            cloneConfig.variantCount = settings.defaultVariantCount;
            cloneConfig.namePattern = settings.defaultNamePattern;
            cloneConfig.hueStep = settings.defaultHueStep;
        }
    }
}
#endif
