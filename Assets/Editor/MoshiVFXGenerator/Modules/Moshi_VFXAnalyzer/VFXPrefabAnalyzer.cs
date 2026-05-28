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
    /// VFX Prefab核心分析器 - 解析Prefab文件并提取特效数据
    /// VFX Prefab Core Analyzer - Parse Prefab files and extract VFX data
    /// </summary>
    public static class VFXPrefabAnalyzer
    {
        // ==================== 名称→角色推断字典 ====================
        // ==================== Name-to-Role Inference Dictionary ====================

        // 名称关键词→角色的映射（优先级从高到低）
        // Name keyword → role mapping (higher priority first)
        private static readonly (string keyword, VFXLayerRole role)[] k_NameRoleRules = {
            ("glow", VFXLayerRole.Glow),
            ("light", VFXLayerRole.Glow),
            ("光晕", VFXLayerRole.Glow),
            ("光", VFXLayerRole.Glow),
            ("lizi", VFXLayerRole.Particle),
            ("particle", VFXLayerRole.Particle),
            ("part_", VFXLayerRole.Particle),
            ("粒子", VFXLayerRole.Particle),
            ("ps_", VFXLayerRole.Particle),
            ("tuowei", VFXLayerRole.Trail),
            ("trail", VFXLayerRole.Trail),
            ("拖尾", VFXLayerRole.Trail),
            ("ribbon", VFXLayerRole.Ribbon),
            ("飘带", VFXLayerRole.Ribbon),
            ("tiaodai", VFXLayerRole.Ribbon),
            ("带", VFXLayerRole.Ribbon),
            ("core", VFXLayerRole.Core),
            ("核心", VFXLayerRole.Core),
            ("center", VFXLayerRole.Core),
            ("中心", VFXLayerRole.Core),
            ("obj", VFXLayerRole.Core),
            ("bg", VFXLayerRole.Background),
            ("background", VFXLayerRole.Background),
            ("背景", VFXLayerRole.Background),
            ("below", VFXLayerRole.Background),
            ("text", VFXLayerRole.Text),
            ("txt", VFXLayerRole.Text),
            ("文字", VFXLayerRole.Text),
            ("font", VFXLayerRole.Text),
            ("sparkle", VFXLayerRole.Sparkle),
            ("闪光", VFXLayerRole.Sparkle),
            ("star", VFXLayerRole.Sparkle),
            ("星", VFXLayerRole.Sparkle),
            ("caidai", VFXLayerRole.Sparkle),
            ("smoke", VFXLayerRole.Smoke),
            ("烟", VFXLayerRole.Smoke),
            ("yan", VFXLayerRole.Smoke),
            ("ring", VFXLayerRole.Ring),
            ("环", VFXLayerRole.Ring),
            ("shockwave", VFXLayerRole.Shockwave),
            ("冲击波", VFXLayerRole.Shockwave),
            ("boom", VFXLayerRole.Shockwave),
            ("爆炸", VFXLayerRole.Shockwave),
            ("debris", VFXLayerRole.Debris),
            ("碎片", VFXLayerRole.Debris),
            ("碎", VFXLayerRole.Debris),
            ("sz", VFXLayerRole.Sparkle),   // 数字缩写通常为闪光/数字特效
        };

        // 分类关键词映射
        // Category keyword mapping
        private static readonly (string keyword, VFXCategory category)[] k_CategoryRules = {
            ("jiesuan", VFXCategory.UI_Result),
            ("结算", VFXCategory.UI_Result),
            ("result", VFXCategory.UI_Result),
            ("win", VFXCategory.UI_Result),
            ("lose", VFXCategory.UI_Result),
            ("fail", VFXCategory.UI_Result),
            ("胜利", VFXCategory.UI_Result),
            ("失败", VFXCategory.UI_Result),
            ("congratulation", VFXCategory.UI_Congratulate),
            ("恭喜", VFXCategory.UI_Congratulate),
            ("恭贺", VFXCategory.UI_Congratulate),
            ("gaoji", VFXCategory.UI_Congratulate),
            ("高级", VFXCategory.UI_Congratulate),
            ("diancang", VFXCategory.UI_Congratulate),
            ("典藏", VFXCategory.UI_Congratulate),
            ("hit", VFXCategory.Combat_Hit),
            ("打击", VFXCategory.Combat_Hit),
            ("受击", VFXCategory.Combat_Hit),
            ("damage", VFXCategory.Combat_Hit),
            ("buff", VFXCategory.Combat_Buff),
            ("shield", VFXCategory.Combat_Buff),
            ("护盾", VFXCategory.Combat_Buff),
            ("heal", VFXCategory.Combat_Buff),
            ("治疗", VFXCategory.Combat_Buff),
            ("skill", VFXCategory.Combat_Skill),
            ("技能", VFXCategory.Combat_Skill),
            ("ultimate", VFXCategory.Combat_Skill),
            ("大招", VFXCategory.Combat_Skill),
            ("ambient", VFXCategory.Scene_Ambient),
            ("氛围", VFXCategory.Scene_Ambient),
            ("fog", VFXCategory.Scene_Weather),
            ("rain", VFXCategory.Scene_Weather),
            ("snow", VFXCategory.Scene_Weather),
            ("天气", VFXCategory.Scene_Weather),
        };

        // ==================== 公共接口 ====================
        // ==================== Public API ====================

        /// <summary>
        /// 分析Prefab，返回完整配方
        /// Analyze a Prefab and return a complete recipe
        /// </summary>
        public static VFXRecipe AnalyzePrefab(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath) || !File.Exists(prefabPath))
            {
                Debug.LogWarning($"[VFXAnalyzer] Prefab not found: {prefabPath}");
                return null;
            }

            var recipe = new VFXRecipe
            {
                prefabPath = prefabPath,
                prefabName = Path.GetFileNameWithoutExtension(prefabPath),
                layers = new List<VFXLayer>(),
                tags = new List<string>(),
                statistics = new VFXStatistics(),
                analyzeTime = DateTime.Now,
                fileSizeBytes = new FileInfo(prefabPath).Length
            };

            GameObject prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[VFXAnalyzer] Failed to load prefab: {e.Message}");
                return null;
            }

            if (prefabRoot == null)
            {
                Debug.LogWarning($"[VFXAnalyzer] Prefab loaded as null: {prefabPath}");
                return null;
            }

            try
            {
                // 1. 分析所有层级 / Analyze all layers
                AnalyzeLayers(prefabRoot, recipe);

                // 2. 推断分类 / Infer category
                recipe.category = InferCategory(prefabRoot.name, recipe.layers);

                // 3. 推断标签 / Infer tags
                recipe.tags = InferTags(prefabRoot.name, recipe);

                // 4. 计算统计信息 / Calculate statistics
                recipe.statistics = CalculateStatistics(recipe);

                // 5. 生成AI描述 / Generate AI description
                recipe.aiDescription = GenerateDescription(recipe);

                // 6. 性能评估 / Performance evaluation
                recipe.performanceReport = EvaluatePerformance(recipe);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            return recipe;
        }

        /// <summary>
        /// 批量分析Prefab列表
        /// Batch analyze a list of Prefabs
        /// </summary>
        public static List<VFXRecipe> AnalyzeBatch(List<string> prefabPaths)
        {
            var recipes = new List<VFXRecipe>();
            int count = 0;
            int total = prefabPaths.Count;

            foreach (var path in prefabPaths)
            {
                count++;
                EditorUtility.DisplayProgressBar(
                    "VFX Analyzer",
                    $"Analyzing ({count}/{total}): {Path.GetFileName(path)}",
                    (float)count / total);

                var recipe = AnalyzePrefab(path);
                if (recipe != null)
                    recipes.Add(recipe);
            }

            EditorUtility.ClearProgressBar();
            return recipes;
        }

        // ==================== 层级分析 ====================
        // ==================== Layer Analysis ====================

        private static void AnalyzeLayers(GameObject root, VFXRecipe recipe)
        {
            var allTransforms = root.GetComponentsInChildren<Transform>(true);

            foreach (var t in allTransforms)
            {
                GameObject go = t.gameObject;
                var layer = AnalyzeSingleGameObject(go);
                if (layer != null)
                {
                    layer.path = GetHierarchyPath(go, root);
                    recipe.layers.Add(layer);
                }
            }

            // 按路径排序（根节点优先）
            // Sort by path (root first)
            recipe.layers.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.Ordinal));
        }

        /// <summary>
        /// 分析单个GameObject，提取特效层信息
        /// Analyze a single GameObject, extract VFX layer info
        /// </summary>
        private static VFXLayer AnalyzeSingleGameObject(GameObject go)
        {
            // 检查是否含有特效相关组件
            // Check if it has VFX-related components
            var ps = go.GetComponent<ParticleSystem>();
            var mr = go.GetComponent<MeshRenderer>();
            var tr = go.GetComponent<TrailRenderer>();
            var lr = go.GetComponent<LineRenderer>();
            var anim = go.GetComponent<Animator>();

            bool hasVFXComponent = ps != null || mr != null || tr != null || lr != null;
            // 也分析Animator（通常用于特效动画）
            // Also analyze Animator (usually for VFX animations)
            bool isRootWithAnimator = anim != null && go.transform.parent == null;

            if (!hasVFXComponent && !isRootWithAnimator)
                return null;

            var layer = new VFXLayer
            {
                name = go.name,
                localPosition = go.transform.localPosition,
                localScale = go.transform.localScale,
                modules = new VFXEnabledModules()
            };

            // 确定类型 / Determine type
            if (ps != null)
            {
                layer.type = VFXLayerType.ParticleSystem;
                ExtractParticleSystemParams(ps, layer);
            }
            else if (tr != null)
            {
                layer.type = VFXLayerType.TrailRenderer;
            }
            else if (mr != null)
            {
                layer.type = VFXLayerType.MeshRenderer;
                ExtractMaterialInfo(mr, layer);
            }
            else if (lr != null)
            {
                layer.type = VFXLayerType.LineRenderer;
            }
            else if (anim != null)
            {
                layer.type = VFXLayerType.Animator;
            }

            // 推断角色 / Infer role
            layer.role = InferLayerRole(go.name, layer);

            // 提取材质信息（如果尚未提取）
            // Extract material info (if not yet extracted)
            if (ps != null && ps.GetComponent<Renderer>() is Renderer psRenderer)
            {
                ExtractMaterialInfo(psRenderer, layer);
            }

            return layer;
        }

        /// <summary>
        /// 提取粒子系统参数
        /// Extract ParticleSystem parameters
        /// </summary>
        private static void ExtractParticleSystemParams(ParticleSystem ps, VFXLayer layer)
        {
            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;

            layer.mainParams = new VFXMainParams
            {
                duration = main.duration,
                simulationSpeed = main.simulationSpeed,
                looping = main.loop,
                startLifetime = main.startLifetime.constant,
                startSpeed = main.startSpeed.constant,
                startSize = main.startSize.constant,
                gravityModifier = main.gravityModifier.constant,
                maxParticles = main.maxParticles,
                emissionRate = emission.rateOverTime.constant,
                startDelay = main.startDelay.constant,
                startColor = main.startColor.color,
                startRotation = main.startRotation.constant,
                rotationSpeed = main.startRotationZ.constant,
                shapeType = shape.shapeType,
                shapeAngle = shape.angle,
                shapeRadius = shape.radius,
                shapeScale = shape.scale
            };

            // 检测启用的模块 / Detect enabled modules
            layer.modules.colorOverLifetime = ps.colorOverLifetime.enabled;
            layer.modules.sizeOverLifetime = ps.sizeOverLifetime.enabled;
            layer.modules.rotationOverLifetime = ps.rotationOverLifetime.enabled;
            layer.modules.velocityOverLifetime = ps.velocityOverLifetime.enabled;
            layer.modules.forceOverLifetime = ps.forceOverLifetime.enabled;
            layer.modules.limitVelocityOverLifetime = ps.limitVelocityOverLifetime.enabled;
            layer.modules.noise = ps.noise.enabled;
            layer.modules.collision = ps.collision.enabled;
            layer.modules.subEmitters = ps.subEmitters.enabled;
            layer.modules.textureSheetAnimation = ps.textureSheetAnimation.enabled;
            layer.modules.lights = ps.lights.enabled;
            layer.modules.customData = ps.customData.enabled;
            layer.modules.externalForces = ps.externalForces.enabled;

            // Trails模块需要通过SerializedObject检查
            // Trails module needs to be checked via SerializedObject
            try
            {
                var so = new SerializedObject(ps);
                var trailsProp = so.FindProperty("m_Trails");
                if (trailsProp != null && trailsProp.isArray)
                    layer.modules.trails = trailsProp.arraySize > 0;
            }
            catch { }

            layer.isPlaying = ps.isPlaying;
        }

        /// <summary>
        /// 提取材质信息
        /// Extract material information
        /// </summary>
        private static void ExtractMaterialInfo(Renderer renderer, VFXLayer layer)
        {
            if (renderer.sharedMaterial != null)
            {
                var mat = renderer.sharedMaterial;
                layer.materialGuid = GetAssetGUID(mat);
                layer.shaderName = mat.shader != null ? mat.shader.name : "";

                // 尝试获取SortingOrder
                // Try to get SortingOrder
                if (renderer is SpriteRenderer sr)
                    layer.sortingOrder = sr.sortingOrder;
            }
        }

        // ==================== 角色推断 ====================
        // ==================== Role Inference ====================

        /// <summary>
        /// 基于名称和参数模式推断层级角色
        /// Infer layer role based on name and parameter patterns
        /// </summary>
        private static VFXLayerRole InferLayerRole(string name, VFXLayer layer)
        {
            if (string.IsNullOrEmpty(name)) return VFXLayerRole.Unknown;

            string lowerName = name.ToLowerInvariant();

            // 第一优先级：名称匹配
            // Priority 1: Name matching
            foreach (var (keyword, role) in k_NameRoleRules)
            {
                if (lowerName.Contains(keyword))
                    return role;
            }

            // 第二优先级：参数模式推断
            // Priority 2: Parameter pattern inference
            if (layer.mainParams != null)
            {
                var mp = layer.mainParams;

                // Trail特征：有Trails模块
                // Trail signature: has Trails module
                if (layer.modules.trails)
                    return VFXLayerRole.Trail;

                // Ring特征：Cone形状，大角度，低速率
                // Ring signature: Cone shape, large angle, low rate
                if (mp.shapeType == ParticleSystemShapeType.Cone && mp.shapeAngle > 60f && mp.emissionRate < 50f)
                    return VFXLayerRole.Ring;

                // Shockwave特征：大半径，短生命周期，爆发式
                // Shockwave signature: large radius, short lifetime, burst-like
                if (mp.startLifetime < 1f && mp.startSpeed > 10f && !mp.looping)
                    return VFXLayerRole.Shockwave;

                // Smoke特征：大粒子，低速度，高生命周期
                // Smoke signature: large particles, low speed, high lifetime
                if (mp.startSize > 2f && mp.startSpeed < 2f && mp.startLifetime > 2f)
                    return VFXLayerRole.Smoke;

                // Sparkle特征：小粒子，短生命，高速
                // Sparkle signature: small particles, short life, high speed
                if (mp.startSize < 0.3f && mp.startLifetime < 1f && mp.startSpeed > 3f)
                    return VFXLayerRole.Sparkle;

                // Glow特征：有颜色生命周期渐变
                // Glow signature: has color over lifetime
                if (layer.modules.colorOverLifetime && mp.emissionRate < 100f)
                    return VFXLayerRole.Glow;

                // 默认为粒子层
                // Default to Particle layer
                if (layer.type == VFXLayerType.ParticleSystem)
                    return VFXLayerRole.Particle;
            }

            // MeshRenderer无PS的通常为背景/装饰
            // MeshRenderer without PS is usually background/decoration
            if (layer.type == VFXLayerType.MeshRenderer)
                return VFXLayerRole.Background;

            return VFXLayerRole.Unknown;
        }

        // ==================== 分类推断 ====================
        // ==================== Category Inference ====================

        private static VFXCategory InferCategory(string prefabName, List<VFXLayer> layers)
        {
            string lowerName = prefabName.ToLowerInvariant();

            // 基于名称关键词分类
            // Classify by name keyword
            foreach (var (keyword, category) in k_CategoryRules)
            {
                if (lowerName.Contains(keyword))
                    return category;
            }

            // 基于层级组合特征分类
            // Classify by layer combination features
            if (layers.Count == 0) return VFXCategory.Unknown;

            var roleCounts = new Dictionary<VFXLayerRole, int>();
            foreach (var layer in layers)
            {
                if (!roleCounts.ContainsKey(layer.role)) roleCounts[layer.role] = 0;
                roleCounts[layer.role]++;
            }

            // 如果有Text层，大概率是UI类
            // If has Text layer, likely UI category
            if (roleCounts.ContainsKey(VFXLayerRole.Text))
                return VFXCategory.UI_Result;

            // 如果大部分是Sparkle+Glow，且总层>5，可能是恭贺类
            // If mostly Sparkle+Glow and total layers >5, might be Congratulation
            int sparkleGlowCount = 0;
            roleCounts.TryGetValue(VFXLayerRole.Sparkle, out int sc);
            roleCounts.TryGetValue(VFXLayerRole.Glow, out int gc);
            sparkleGlowCount = sc + gc;
            if (sparkleGlowCount > layers.Count * 0.4 && layers.Count > 5)
                return VFXCategory.UI_Congratulate;

            return VFXCategory.Unknown;
        }

        // ==================== 标签推断 ====================
        // ==================== Tag Inference ====================

        private static List<string> InferTags(string prefabName, VFXRecipe recipe)
        {
            var tags = new List<string>();
            string lowerName = prefabName.ToLowerInvariant();

            // 基于特征的标签
            // Feature-based tags
            if (recipe.statistics.loopingCount > 0)
                tags.Add("循环");
            if (recipe.statistics.loopingCount == 0)
                tags.Add("单次播放");
            if (recipe.statistics.totalMaxParticles > 500)
                tags.Add("高粒子量");
            if (recipe.statistics.totalMaxParticles < 200)
                tags.Add("轻量级");
            if (recipe.statistics.hierarchyDepth > 3)
                tags.Add("深层级");
            if (recipe.layers.Count > 10)
                tags.Add("复杂特效");
            else if (recipe.layers.Count <= 3)
                tags.Add("简单特效");
            if (recipe.statistics.uniqueMaterialCount == 1)
                tags.Add("单材质");

            // 基于角色的标签
            // Role-based tags
            var roleCounts = recipe.GetRoleCounts();
            foreach (var kvp in roleCounts)
            {
                if (kvp.Value >= 2)
                    tags.Add($"多{kvp.Key}");
            }

            return tags;
        }

        // ==================== 统计计算 ====================
        // ==================== Statistics Calculation ====================

        private static VFXStatistics CalculateStatistics(VFXRecipe recipe)
        {
            var stats = new VFXStatistics();

            if (recipe.layers == null || recipe.layers.Count == 0)
                return stats;

            stats.totalLayers = recipe.layers.Count;
            float totalMaxP = 0f;
            float totalDuration = 0f;
            float maxDuration = 0f;
            float minDuration = float.MaxValue;
            int durationCount = 0;
            int loopingCount = 0;
            int maxDepth = 0;
            int totalModules = 0;
            int moduleLayerCount = 0;

            var materials = new HashSet<string>();
            var shaders = new HashSet<string>();

            foreach (var layer in recipe.layers)
            {
                // 统计类型数量 / Count type numbers
                switch (layer.type)
                {
                    case VFXLayerType.ParticleSystem: stats.particleSystemCount++; break;
                    case VFXLayerType.MeshRenderer: stats.meshRendererCount++; break;
                    case VFXLayerType.TrailRenderer: stats.trailRendererCount++; break;
                    case VFXLayerType.Animator: stats.animatorCount++; break;
                }

                // 统计材质和Shader / Count materials and shaders
                if (!string.IsNullOrEmpty(layer.materialGuid)) materials.Add(layer.materialGuid);
                if (!string.IsNullOrEmpty(layer.shaderName)) shaders.Add(layer.shaderName);

                // 统计持续时间和最大粒子数
                // Count duration and max particles
                if (layer.mainParams != null)
                {
                    totalMaxP += layer.mainParams.maxParticles;

                    if (!layer.mainParams.looping)
                    {
                        float dur = layer.mainParams.duration + layer.mainParams.startDelay;
                        totalDuration += dur;
                        maxDuration = Mathf.Max(maxDuration, dur);
                        minDuration = Mathf.Min(minDuration, dur);
                        durationCount++;
                    }
                    else
                    {
                        loopingCount++;
                    }
                }

                // 计算层级深度 / Calculate hierarchy depth
                int depth = layer.path.Split('/').Length;
                maxDepth = Mathf.Max(maxDepth, depth);

                // 统计模块数 / Count module numbers
                if (layer.modules != null)
                {
                    totalModules += layer.modules.GetEnabledCount();
                    moduleLayerCount++;
                }
            }

            stats.totalMaxParticles = totalMaxP;
            stats.maxDuration = maxDuration;
            stats.minDuration = minDuration > float.MaxValue ? 0 : minDuration;
            stats.avgDuration = durationCount > 0 ? totalDuration / durationCount : 0;
            stats.hierarchyDepth = maxDepth;
            stats.uniqueMaterialCount = materials.Count;
            stats.uniqueShaderCount = shaders.Count;
            stats.enabledModuleAvg = moduleLayerCount > 0 ? totalModules / moduleLayerCount : 0;
            stats.loopingCount = loopingCount;

            return stats;
        }

        // ==================== 描述生成 ====================
        // ==================== Description Generation ====================

        private static string GenerateDescription(VFXRecipe recipe)
        {
            if (recipe.layers == null || recipe.layers.Count == 0)
                return "空特效Prefab / Empty VFX Prefab";

            var sb = new System.Text.StringBuilder();
            sb.Append($"特效 '{recipe.prefabName}' 包含 {recipe.layers.Count} 个层，");

            // 分类信息
            if (recipe.category != VFXCategory.Unknown)
                sb.Append($"分类为 {recipe.category}，");

            // 层角色分布
            var roleCounts = recipe.GetRoleCounts();
            var roleDescs = new List<string>();
            foreach (var kvp in roleCounts)
            {
                roleDescs.Add($"{kvp.Key}x{kvp.Value}");
            }
            sb.Append($"角色分布: {string.Join(", ", roleDescs)}。");

            // 性能特征
            sb.Append($" 最大粒子数总计 {recipe.statistics.totalMaxParticles:F0}，");
            sb.Append($"平均持续 {recipe.statistics.avgDuration:F2}s，");
            sb.Append($"使用 {recipe.statistics.uniqueMaterialCount} 种材质，");
            sb.Append($"{recipe.statistics.uniqueShaderCount} 种Shader。");

            if (recipe.tags.Count > 0)
                sb.Append($" 标签: [{string.Join(", ", recipe.tags)}]");

            return sb.ToString();
        }

        // ==================== 辅助方法 ====================
        // ==================== Utility Methods ====================

        private static string GetHierarchyPath(GameObject go, GameObject root)
        {
            var path = new List<string>();
            Transform current = go.transform;
            while (current != null && current != root.transform)
            {
                path.Add(current.name);
                current = current.parent;
            }
            path.Reverse();
            return string.Join("/", path);
        }

        private static string GetAssetGUID(UnityEngine.Object obj)
        {
            if (obj == null) return "";
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) return "";
            return AssetDatabase.AssetPathToGUID(path);
        }

        // ==================== 性能评估 ====================
        // ==================== Performance Evaluation ====================

        /// <summary>
        /// 评估特效性能，生成评级报告和优化建议
        /// Evaluate VFX performance, generate rating report and optimization suggestions
        /// </summary>
        public static VFXPerformanceReport EvaluatePerformance(VFXRecipe recipe)
        {
            var report = new VFXPerformanceReport();
            if (recipe == null || recipe.statistics == null || recipe.layers == null)
            {
                report.rating = VFXPerformanceRating.B;
                report.score = 50f;
                return report;
            }

            var stats = recipe.statistics;

            // 1. 粒子得分 (0~100) / Particle score (0~100)
            // S: <300, A: <600, B: <1000, C: <2000, D: >=2000
            report.particleScore = EvaluateParticleScore(stats.totalMaxParticles, stats.particleSystemCount);

            // 2. 层级得分 (0~100) / Layer score (0~100)
            // S: <=3层, A: <=6层, B: <=10层, C: <=15层, D: >15层
            report.layerScore = EvaluateLayerScore(stats.totalLayers, stats.hierarchyDepth);

            // 3. 材质得分 (0~100) / Material score (0~100)
            // S: 1种材质, A: <=2, B: <=4, C: <=6, D: >6
            report.materialScore = EvaluateMaterialScore(stats.uniqueMaterialCount, stats.uniqueShaderCount);

            // 4. 复杂度得分 (0~100) / Complexity score (0~100)
            report.complexityScore = EvaluateComplexityScore(stats);

            // 加权平均 / Weighted average
            report.score = report.particleScore * 0.35f
                         + report.layerScore * 0.20f
                         + report.materialScore * 0.20f
                         + report.complexityScore * 0.25f;

            // 确定评级 / Determine rating
            report.rating = ScoreToRating(report.score);

            // 生成优化建议 / Generate optimization suggestions
            report.suggestions = GenerateOptimizationSuggestions(recipe, report);

            return report;
        }

        private static float EvaluateParticleScore(float totalMaxParticles, int psCount)
        {
            if (psCount == 0) return 100f; // 无粒子系统 = 零消耗

            float avgPerPS = totalMaxParticles / Mathf.Max(psCount, 1);

            // 按总粒子数评分 / Score by total particle count
            if (totalMaxParticles < 300) return 95f;
            if (totalMaxParticles < 600) return 80f;
            if (totalMaxParticles < 1000) return 65f;
            if (totalMaxParticles < 2000) return 45f;
            if (totalMaxParticles < 4000) return 25f;
            return 10f;
        }

        private static float EvaluateLayerScore(int totalLayers, int hierarchyDepth)
        {
            float layerScore = 100f;
            if (totalLayers > 3) layerScore -= (totalLayers - 3) * 8f;
            if (hierarchyDepth > 3) layerScore -= (hierarchyDepth - 3) * 10f;
            return Mathf.Clamp(layerScore, 5f, 100f);
        }

        private static float EvaluateMaterialScore(int materialCount, int shaderCount)
        {
            float score = 100f;
            if (materialCount > 1) score -= (materialCount - 1) * 15f;
            if (shaderCount > 1) score -= (shaderCount - 1) * 10f;
            return Mathf.Clamp(score, 5f, 100f);
        }

        private static float EvaluateComplexityScore(VFXStatistics stats)
        {
            float score = 100f;

            // 循环粒子持续消耗 / Looping particles consume continuously
            if (stats.loopingCount > 2) score -= (stats.loopingCount - 2) * 10f;

            // 模块过多 / Too many modules
            if (stats.enabledModuleAvg > 5) score -= (stats.enabledModuleAvg - 5) * 5f;

            // TrailRenderer性能敏感 / TrailRenderer is performance-sensitive
            if (stats.trailRendererCount > 2) score -= (stats.trailRendererCount - 2) * 12f;

            return Mathf.Clamp(score, 5f, 100f);
        }

        private static VFXPerformanceRating ScoreToRating(float score)
        {
            if (score >= 85f) return VFXPerformanceRating.S;
            if (score >= 70f) return VFXPerformanceRating.A;
            if (score >= 50f) return VFXPerformanceRating.B;
            if (score >= 30f) return VFXPerformanceRating.C;
            return VFXPerformanceRating.D;
        }

        // ==================== 优化建议生成 ====================
        // ==================== Optimization Suggestion Generation ====================

        private static List<VFXOptimizationSuggestion> GenerateOptimizationSuggestions(VFXRecipe recipe, VFXPerformanceReport report)
        {
            var suggestions = new List<VFXOptimizationSuggestion>();
            var stats = recipe.statistics;

            // === 粒子量相关建议 ===
            // === Particle count related suggestions ===
            if (stats.totalMaxParticles > 2000)
            {
                suggestions.Add(new VFXOptimizationSuggestion
                {
                    title = "粒子总量过高",
                    description = $"当前粒子总量 {stats.totalMaxParticles:F0}，建议控制在1000以内。考虑降低MaxParticles或减少粒子系统数量。",
                    severity = VFXSeverity.Critical,
                    category = "粒子",
                    estimatedSaving = 40f
                });
            }
            else if (stats.totalMaxParticles > 1000)
            {
                suggestions.Add(new VFXOptimizationSuggestion
                {
                    title = "粒子总量偏高",
                    description = $"当前粒子总量 {stats.totalMaxParticles:F0}，建议控制在600以内以获得更好的性能表现。",
                    severity = VFXSeverity.Warning,
                    category = "粒子",
                    estimatedSaving = 20f
                });
            }

            // 检查单个粒子系统的MaxParticles
            // Check individual PS MaxParticles
            foreach (var layer in recipe.layers)
            {
                if (layer.mainParams != null && layer.mainParams.maxParticles > 500)
                {
                    suggestions.Add(new VFXOptimizationSuggestion
                    {
                        title = $"单层粒子量过大: {layer.name}",
                        description = $"层 [{layer.name}] 的MaxParticles为 {layer.mainParams.maxParticles}，建议降低到200以下。可通过缩短生命周期或降低发射率来补偿视觉效果。",
                        severity = layer.mainParams.maxParticles > 1000 ? VFXSeverity.Critical : VFXSeverity.Warning,
                        targetLayerName = layer.name,
                        category = "粒子",
                        estimatedSaving = 30f
                    });
                }
            }

            // === 层级结构建议 ===
            // === Layer structure suggestions ===
            if (stats.totalLayers > 10)
            {
                suggestions.Add(new VFXOptimizationSuggestion
                {
                    title = "层级数过多",
                    description = $"当前共 {stats.totalLayers} 层，建议控制在6层以内。考虑合并功能相似的层，或拆分为多个独立特效。",
                    severity = stats.totalLayers > 15 ? VFXSeverity.Critical : VFXSeverity.Warning,
                    category = "结构",
                    estimatedSaving = 25f
                });
            }

            if (stats.hierarchyDepth > 4)
            {
                suggestions.Add(new VFXOptimizationSuggestion
                {
                    title = "层级深度过深",
                    description = $"当前层级深度 {stats.hierarchyDepth}，建议控制在3以内。过深的嵌套会增加变换计算开销。",
                    severity = VFXSeverity.Warning,
                    category = "结构",
                    estimatedSaving = 10f
                });
            }

            // === 材质/Shader建议 ===
            // === Material/Shader suggestions ===
            if (stats.uniqueMaterialCount > 4)
            {
                suggestions.Add(new VFXOptimizationSuggestion
                {
                    title = "材质种类过多",
                    description = $"使用了 {stats.uniqueMaterialCount} 种不同材质，建议控制在2种以内。可考虑合并使用相同Shader的材质，使用纹理图集替代多材质。",
                    severity = VFXSeverity.Warning,
                    category = "材质",
                    estimatedSaving = 20f
                });
            }

            if (stats.uniqueShaderCount > 2)
            {
                suggestions.Add(new VFXOptimizationSuggestion
                {
                    title = "Shader种类过多",
                    description = $"使用了 {stats.uniqueShaderCount} 种不同Shader，每个Shader都会增加DrawCall和SRP Batcher中断。建议统一使用1~2种Shader。",
                    severity = VFXSeverity.Warning,
                    category = "渲染",
                    estimatedSaving = 15f
                });
            }

            // === 循环相关建议 ===
            // === Looping related suggestions ===
            if (stats.loopingCount > 0)
            {
                // 检查循环粒子系统是否可以改为单次播放
                // Check if looping PS can be changed to one-shot
                var loopingLayers = recipe.layers.Where(l => l.mainParams != null && l.mainParams.looping).ToList();
                if (loopingLayers.Count > 2)
                {
                    suggestions.Add(new VFXOptimizationSuggestion
                    {
                        title = "循环粒子系统过多",
                        description = $"有 {loopingLayers.Count} 个循环播放的粒子系统，持续消耗性能。建议将部分循环系统改为单次播放+间隔重启的方式。",
                        severity = VFXSeverity.Info,
                        category = "粒子",
                        estimatedSaving = 15f
                    });
                }
            }

            // === 模块优化建议 ===
            // === Module optimization suggestions ===
            foreach (var layer in recipe.layers)
            {
                if (layer.modules != null)
                {
                    int enabledCount = layer.modules.GetEnabledCount();

                    // Collision模块性能消耗大
                    // Collision module is performance-heavy
                    if (layer.modules.collision)
                    {
                        suggestions.Add(new VFXOptimizationSuggestion
                        {
                            title = $"Collision模块: {layer.name}",
                            description = $"层 [{layer.name}] 启用了Collision模块，此模块性能消耗较大。如非必要，建议关闭或改用平面碰撞替代世界碰撞。",
                            severity = VFXSeverity.Info,
                            targetLayerName = layer.name,
                            category = "粒子",
                            estimatedSaving = 10f
                        });
                    }

                    // 模块总数过多
                    // Too many modules total
                    if (enabledCount > 6)
                    {
                        suggestions.Add(new VFXOptimizationSuggestion
                        {
                            title = $"启用模块过多: {layer.name}",
                            description = $"层 [{layer.name}] 启用了 {enabledCount} 个模块，建议精简到5个以内。每个模块都会增加CPU计算量。",
                            severity = VFXSeverity.Info,
                            targetLayerName = layer.name,
                            category = "粒子",
                            estimatedSaving = 8f
                        });
                    }
                }
            }

            // 按严重级别排序 / Sort by severity
            suggestions.Sort((a, b) =>
            {
                int order(VFXSeverity s) => s == VFXSeverity.Critical ? 0 : s == VFXSeverity.Warning ? 1 : 2;
                return order(a.severity).CompareTo(order(b.severity));
            });

            return suggestions;
        }
    }
}
#endif
