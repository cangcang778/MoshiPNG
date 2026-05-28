#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MoshiVFXGenerator.Analyzer
{
    /// <summary>
    /// VFX规则发现引擎 - 从多个特效中批量发现规律
    /// VFX Rule Discovery Engine - Batch discover patterns from multiple VFX
    /// </summary>
    public static class VFXRuleDiscovery
    {
        // ==================== 公共接口 ====================
        // ==================== Public API ====================

        /// <summary>
        /// 从多个配方中发现所有规则
        /// Discover all rules from multiple recipes
        /// </summary>
        public static List<VFXRule> DiscoverAllRules(List<VFXRecipe> recipes)
        {
            var allRules = new List<VFXRule>();

            if (recipes == null || recipes.Count < 2)
            {
                Debug.LogWarning("[VFX RuleDiscovery] Need at least 2 recipes to discover rules.");
                return allRules;
            }

            // 1. 组成规则
            // 1. Composition rules
            allRules.AddRange(DiscoverCompositionRules(recipes));

            // 2. 参数规则
            // 2. Parameter rules
            allRules.AddRange(DiscoverParameterRules(recipes));

            // 3. 性能规则
            // 3. Performance rules
            allRules.AddRange(DiscoverPerformanceRules(recipes));

            // 4. 命名规则
            // 4. Naming rules
            allRules.AddRange(DiscoverNamingRules(recipes));

            // 5. 结构规则
            // 5. Structure rules
            allRules.AddRange(DiscoverStructureRules(recipes));

            // 按置信度排序
            // Sort by confidence
            allRules.Sort((a, b) => b.confidence.CompareTo(a.confidence));

            return allRules;
        }

        /// <summary>
        /// 按分类发现规则
        /// Discover rules grouped by category
        /// </summary>
        public static Dictionary<VFXCategory, List<VFXRule>> DiscoverCategoryRules(List<VFXRecipe> recipes)
        {
            var result = new Dictionary<VFXCategory, List<VFXRule>>();

            // 按分类分组
            // Group by category
            var groups = recipes
                .Where(r => r.category != VFXCategory.Unknown)
                .GroupBy(r => r.category)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var kvp in groups)
            {
                if (kvp.Value.Count < 2) continue;
                var categoryRules = DiscoverAllRules(kvp.Value);
                foreach (var rule in categoryRules)
                    rule.category = kvp.Key;
                result[kvp.Key] = categoryRules;
            }

            return result;
        }

        /// <summary>
        /// 从规则库提炼特效结构模板
        /// Distill VFX structure templates from rule library
        /// </summary>
        public static List<VFXTemplate> DiscoverTemplates(List<VFXRecipe> recipes)
        {
            var templates = new List<VFXTemplate>();
            if (recipes == null || recipes.Count < 2) return templates;

            // 按分类分组生成模板
            // Group by category to generate templates
            var categoryGroups = recipes
                .GroupBy(r => r.category)
                .Where(g => g.Count() >= 2)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var kvp in categoryGroups)
            {
                var template = BuildTemplateFromGroup(kvp.Key, kvp.Value);
                if (template != null && template.layers.Count > 0)
                    templates.Add(template);
            }

            // 通用模板（不限分类，从所有特效中提炼）
            // Universal template (all categories, distilled from all VFX)
            if (recipes.Count >= 3)
            {
                var universalTemplate = BuildTemplateFromGroup(VFXCategory.Unknown, recipes);
                if (universalTemplate != null && universalTemplate.layers.Count > 0)
                {
                    universalTemplate.templateName = "通用特效模板";
                    templates.Add(universalTemplate);
                }
            }

            return templates;
        }

        /// <summary>
        /// 检测偏离规则的异常特效
        /// Detect anomalous VFX that deviate from rules
        /// </summary>
        public static List<VFXRule> DetectAnomalies(List<VFXRecipe> recipes, List<VFXRule> existingRules)
        {
            var anomalies = new List<VFXRule>();
            if (recipes == null || recipes.Count < 2) return anomalies;

            // 计算各参数的统计基线
            // Calculate statistical baselines for each parameter
            float avgParticles = (float)recipes.Average(r => r.statistics.totalMaxParticles);
            float avgLayers = (float)recipes.Average(r => r.statistics.totalLayers);
            float avgMaterials = (float)recipes.Average(r => r.statistics.uniqueMaterialCount);
            float avgShaders = (float)recipes.Average(r => r.statistics.uniqueShaderCount);

            // 标准差
            // Standard deviation
            float stdParticles = StdDev(recipes.Select(r => r.statistics.totalMaxParticles).ToList(), avgParticles);
            float stdLayers = StdDev(recipes.Select(r => (float)r.statistics.totalLayers).ToList(), avgLayers);

            foreach (var recipe in recipes)
            {
                // 检测偏离2个标准差的特效
                // Detect VFX deviating by 2 standard deviations
                if (stdParticles > 0 && recipe.statistics.totalMaxParticles > avgParticles + 2 * stdParticles)
                {
                    anomalies.Add(new VFXRule
                    {
                        type = VFXRuleType.Performance,
                        sourceA = recipe.prefabName,
                        sourceB = "baseline",
                        description = $"异常: [{recipe.prefabName}] 粒子量 {recipe.statistics.totalMaxParticles:F0} 显著偏高 (均值 {avgParticles:F0}±{stdParticles:F0})",
                        confidence = 0.9f,
                        category = recipe.category
                    });
                }

                if (stdLayers > 0 && recipe.statistics.totalLayers > avgLayers + 2 * stdLayers)
                {
                    anomalies.Add(new VFXRule
                    {
                        type = VFXRuleType.Structure,
                        sourceA = recipe.prefabName,
                        sourceB = "baseline",
                        description = $"异常: [{recipe.prefabName}] 层级数 {recipe.statistics.totalLayers} 显著偏高 (均值 {avgLayers:F1}±{stdLayers:F1})",
                        confidence = 0.85f,
                        category = recipe.category
                    });
                }

                // 材质异常
                // Material anomaly
                if (recipe.statistics.uniqueMaterialCount > avgMaterials * 2 && recipe.statistics.uniqueMaterialCount > 4)
                {
                    anomalies.Add(new VFXRule
                    {
                        type = VFXRuleType.Performance,
                        sourceA = recipe.prefabName,
                        sourceB = "baseline",
                        description = $"异常: [{recipe.prefabName}] 使用 {recipe.statistics.uniqueMaterialCount} 种材质 (均值 {avgMaterials:F1})",
                        confidence = 0.8f,
                        category = recipe.category
                    });
                }

                // 性能评级异常（D级特效标记）
                // Performance rating anomaly (mark D-rated VFX)
                if (recipe.performanceReport != null && recipe.performanceReport.rating == VFXPerformanceRating.D)
                {
                    anomalies.Add(new VFXRule
                    {
                        type = VFXRuleType.Performance,
                        sourceA = recipe.prefabName,
                        sourceB = "baseline",
                        description = $"高消耗特效: [{recipe.prefabName}] 性能评级 D ({recipe.performanceReport.score:F0}分)，需要优化",
                        confidence = 0.95f,
                        category = recipe.category
                    });
                }
            }

            return anomalies;
        }

        // ==================== 组成规则 ====================
        // ==================== Composition Rules ====================

        private static List<VFXRule> DiscoverCompositionRules(List<VFXRecipe> recipes)
        {
            var rules = new List<VFXRule>();

            // 1. 分析每个分类的常见角色组合
            // 1. Analyze common role combinations per category
            var categoryRoleSets = new Dictionary<VFXCategory, Dictionary<VFXLayerRole, int>>();

            foreach (var recipe in recipes)
            {
                var cat = recipe.category;
                if (!categoryRoleSets.ContainsKey(cat))
                    categoryRoleSets[cat] = new Dictionary<VFXLayerRole, int>();

                foreach (var layer in recipe.layers)
                {
                    var dict = categoryRoleSets[cat];
                    if (!dict.ContainsKey(layer.role)) dict[layer.role] = 0;
                    dict[layer.role]++;
                }
            }

            // 对每个分类，找出出现频率高的角色
            // For each category, find high-frequency roles
            foreach (var kvp in categoryRoleSets)
            {
                int recipeCount = recipes.Count(r => r.category == kvp.Key);
                if (recipeCount < 2) continue;

                foreach (var roleKvp in kvp.Value)
                {
                    float frequency = (float)roleKvp.Value / recipeCount;
                    if (frequency >= 0.7f) // 70%以上的特效都含有此角色
                    {
                        rules.Add(new VFXRule
                        {
                            type = VFXRuleType.Composition,
                            sourceA = "batch",
                            sourceB = "batch",
                            category = kvp.Key,
                            description = $"分类 [{kvp.Key}] 的特效通常包含 [{roleKvp.Key}] 角色 (出现率 {frequency:P0})",
                            confidence = frequency
                        });
                    }
                }
            }

            // 2. 分析常见的层角色组合模式
            // 2. Analyze common layer role combination patterns
            var combinationCounts = new Dictionary<string, int>();
            var combinationExamples = new Dictionary<string, string>();

            foreach (var recipe in recipes)
            {
                var roles = recipe.layers.Select(l => l.role).Distinct().OrderBy(r => r).ToList();
                string combo = string.Join("+", roles);

                if (!combinationCounts.ContainsKey(combo)) combinationCounts[combo] = 0;
                combinationCounts[combo]++;

                if (!combinationExamples.ContainsKey(combo))
                    combinationExamples[combo] = recipe.prefabName;
            }

            foreach (var kvp in combinationCounts.Where(k => k.Value >= 2))
            {
                float frequency = (float)kvp.Value / recipes.Count;
                rules.Add(new VFXRule
                {
                    type = VFXRuleType.Composition,
                    sourceA = "batch",
                    sourceB = "batch",
                    description = $"常见角色组合 [{kvp.Key}] 出现 {kvp.Value} 次 (频率 {frequency:P0})，示例: {combinationExamples[kvp.Key]}",
                    confidence = frequency,
                    exampleLayerName = combinationExamples[kvp.Key]
                });
            }

            return rules;
        }

        // ==================== 参数规则 ====================
        // ==================== Parameter Rules ====================

        private static List<VFXRule> DiscoverParameterRules(List<VFXRecipe> recipes)
        {
            var rules = new List<VFXRule>();

            // 1. 分析同角色层的参数范围
            // 1. Analyze parameter ranges for same-role layers
            var roleParamStats = new Dictionary<VFXLayerRole, List<VFXMainParams>>();

            foreach (var recipe in recipes)
            {
                foreach (var layer in recipe.layers)
                {
                    if (layer.mainParams == null) continue;
                    if (!roleParamStats.ContainsKey(layer.role))
                        roleParamStats[layer.role] = new List<VFXMainParams>();
                    roleParamStats[layer.role].Add(layer.mainParams);
                }
            }

            foreach (var kvp in roleParamStats)
            {
                if (kvp.Value.Count < 3) continue; // 至少3个样本

                var paramsList = kvp.Value;

                // 分析Duration范围
                var durations = paramsList.Select(p => p.duration).ToList();
                rules.AddRange(AnalyzeParamRange(kvp.Key, "Duration", durations, "s"));

                // 分析StartLifetime范围
                var lifetimes = paramsList.Select(p => p.startLifetime).ToList();
                rules.AddRange(AnalyzeParamRange(kvp.Key, "StartLifetime", lifetimes, "s"));

                // 分析StartSpeed范围
                var speeds = paramsList.Select(p => p.startSpeed).ToList();
                rules.AddRange(AnalyzeParamRange(kvp.Key, "StartSpeed", speeds, ""));

                // 分析StartSize范围
                var sizes = paramsList.Select(p => p.startSize).ToList();
                rules.AddRange(AnalyzeParamRange(kvp.Key, "StartSize", sizes, ""));

                // 分析EmissionRate范围
                var rates = paramsList.Select(p => p.emissionRate).ToList();
                rules.AddRange(AnalyzeParamRange(kvp.Key, "EmissionRate", rates, "/s"));
            }

            // 2. 分析跨角色的参数关系
            // 2. Analyze cross-role parameter relationships
            foreach (var recipe in recipes)
            {
                var glowLayers = recipe.GetLayersByRole(VFXLayerRole.Glow);
                var particleLayers = recipe.GetLayersByRole(VFXLayerRole.Particle);

                if (glowLayers.Count > 0 && particleLayers.Count > 0)
                {
                    // Glow通常比Particle寿命长
                    // Glow usually has longer lifetime than Particle
                    var avgGlowLife = glowLayers.Where(l => l.mainParams != null).Average(l => l.mainParams.startLifetime);
                    var avgPartLife = particleLayers.Where(l => l.mainParams != null).Average(l => l.mainParams.startLifetime);

                    if (avgGlowLife > avgPartLife * 1.5f)
                    {
                        rules.Add(new VFXRule
                        {
                            type = VFXRuleType.Parameter,
                            sourceA = recipe.prefabName,
                            sourceB = "batch",
                            description = $"[{recipe.prefabName}] Glow层平均寿命 ({avgGlowLife:F2}s) 显著长于 Particle层 ({avgPartLife:F2}s)",
                            confidence = 0.6f,
                            exampleLayerName = recipe.prefabName
                        });
                    }
                }
            }

            return rules;
        }

        private static List<VFXRule> AnalyzeParamRange(VFXLayerRole role, string paramName, List<float> values, string unit)
        {
            var rules = new List<VFXRule>();
            if (values.Count < 3) return rules;

            values.Sort();
            float min = values[0];
            float max = values[values.Count - 1];
            float avg = (float)values.Average();
            float median = values[values.Count / 2];

            // 检测集中范围（中间50%数据的范围）
            // Detect concentration range (middle 50% data range)
            int q1Idx = values.Count / 4;
            int q3Idx = values.Count * 3 / 4;
            float q1 = values[q1Idx];
            float q3 = values[q3Idx];
            float iqr = q3 - q1; // 四分位距

            // 标准差
            // Standard deviation
            float variance = values.Sum(v => (v - avg) * (v - avg)) / values.Count;
            float stdDev = Mathf.Sqrt(variance);
            float cv = avg > 0.001f ? stdDev / avg : 0f; // 变异系数

            string rangeDesc = $"[{min:F2}, {max:F2}] (中位数: {median:F2}{unit}, 标准差: {stdDev:F2})";

            // 低变异：参数值非常一致 → 强规则
            // Low variance: parameter values very consistent → strong rule
            if (cv < 0.2f)
            {
                rules.Add(new VFXRule
                {
                    type = VFXRuleType.Parameter,
                    sourceA = "batch",
                    sourceB = "batch",
                    description = $"[{role}] 的 {paramName} 高度一致: {rangeDesc} (变异系数 {cv:P0})",
                    confidence = 1f - cv
                });
            }
            // 中变异：有趋势 → 中等规则
            // Medium variance: has trend → medium rule
            else if (cv < 0.5f)
            {
                rules.Add(new VFXRule
                {
                    type = VFXRuleType.Parameter,
                    sourceA = "batch",
                    sourceB = "batch",
                    description = $"[{role}] 的 {paramName} 较为集中: {rangeDesc} (IQR: [{q1:F2}, {q3:F2}])",
                    confidence = 0.5f
                });
            }
            // 高变异：参数差异大 → 有意义的发现
            // High variance: large parameter differences → meaningful finding
            else
            {
                rules.Add(new VFXRule
                {
                    type = VFXRuleType.Parameter,
                    sourceA = "batch",
                    sourceB = "batch",
                    description = $"[{role}] 的 {paramName} 差异大: {rangeDesc}，最大值是最小值的 {max / Mathf.Max(min, 0.001f):F1} 倍",
                    confidence = 0.3f
                });
            }

            return rules;
        }

        // ==================== 性能规则 ====================
        // ==================== Performance Rules ====================

        private static List<VFXRule> DiscoverPerformanceRules(List<VFXRecipe> recipes)
        {
            var rules = new List<VFXRule>();

            // 1. 粒子总量分布
            // 1. Total particle count distribution
            var particleCounts = recipes.Select(r => r.statistics.totalMaxParticles).ToList();
            if (particleCounts.Count > 0)
            {
                particleCounts.Sort();
                float avg = (float)particleCounts.Average();
                float max = particleCounts.Max();
                float min = particleCounts.Min();

                // 识别高消耗特效
                // Identify high-cost VFX
                var highCost = recipes.Where(r => r.statistics.totalMaxParticles > avg * 1.5f).ToList();
                if (highCost.Count > 0)
                {
                    string names = string.Join(", ", highCost.Select(r => r.prefabName));
                    rules.Add(new VFXRule
                    {
                        type = VFXRuleType.Performance,
                        sourceA = "batch",
                        sourceB = "batch",
                        description = $"高粒子量特效 (>{avg * 1.5f:F0}): {names}，平均 {highCost.Average(r => r.statistics.totalMaxParticles):F0}",
                        confidence = 0.8f
                    });
                }
            }

            // 2. 层数与性能的关系
            // 2. Layer count vs performance relationship
            var layerParticlePairs = recipes.Select(r => new { r.layers.Count, r.statistics.totalMaxParticles, r.prefabName }).ToList();
            float avgLayers = (float)layerParticlePairs.Average(p => p.Count);
            var complexVFX = layerParticlePairs.Where(p => p.Count > avgLayers * 1.5f).ToList();

            if (complexVFX.Count > 0)
            {
                foreach (var vfx in complexVFX)
                {
                    rules.Add(new VFXRule
                    {
                        type = VFXRuleType.Performance,
                        sourceA = vfx.prefabName,
                        sourceB = "batch",
                        description = $"[{vfx.prefabName}] 层级数 {vfx.Count} (平均 {avgLayers:F1})，最大粒子 {vfx.totalMaxParticles:F0}",
                        confidence = 0.7f
                    });
                }
            }

            // 3. 材质多样性规则
            // 3. Material diversity rules
            var matCounts = recipes.Select(r => r.statistics.uniqueMaterialCount).ToList();
            float avgMats = (float)matCounts.Average();
            var highMatVFX = recipes.Where(r => r.statistics.uniqueMaterialCount > avgMats * 1.5f).ToList();

            if (highMatVFX.Count > 0)
            {
                string names = string.Join(", ", highMatVFX.Select(r => $"{r.prefabName}({r.statistics.uniqueMaterialCount})"));
                rules.Add(new VFXRule
                {
                    type = VFXRuleType.Performance,
                    sourceA = "batch",
                    sourceB = "batch",
                    description = $"高材质多样性: {names}，平均使用 {avgMats:F1} 种材质",
                    confidence = 0.6f
                });
            }

            return rules;
        }

        // ==================== 命名规则 ====================
        // ==================== Naming Rules ====================

        private static List<VFXRule> DiscoverNamingRules(List<VFXRecipe> recipes)
        {
            var rules = new List<VFXRule>();

            // 1. 统计命名模式
            // 1. Count naming patterns
            var allLayerNames = recipes.SelectMany(r => r.layers.Select(l => l.name)).ToList();
            var patternCounts = new Dictionary<string, int>();

            foreach (var name in allLayerNames)
            {
                string pattern = DetectNamingPatternSingle(name);
                if (!patternCounts.ContainsKey(pattern)) patternCounts[pattern] = 0;
                patternCounts[pattern]++;
            }

            // 找出主要命名模式
            // Find dominant naming patterns
            var dominantPatterns = patternCounts
                .Where(k => k.Value >= allLayerNames.Count * 0.1f)
                .OrderByDescending(k => k.Value)
                .ToList();

            if (dominantPatterns.Count > 0)
            {
                string patternList = string.Join(", ", dominantPatterns.Select(k => $"{k.Key}({k.Value})"));
                rules.Add(new VFXRule
                {
                    type = VFXRuleType.Naming,
                    sourceA = "batch",
                    sourceB = "batch",
                    description = $"主要命名模式: {patternList}（共 {allLayerNames.Count} 个层）",
                    confidence = (float)dominantPatterns[0].Value / allLayerNames.Count
                });
            }

            // 2. 前缀/后缀分析
            // 2. Prefix/suffix analysis
            var prefixCounts = new Dictionary<string, int>();
            foreach (var name in allLayerNames)
            {
                if (name.Contains("_"))
                {
                    string prefix = name.Split('_')[0].ToLowerInvariant();
                    if (prefix.Length <= 10 && prefix.Length >= 2)
                    {
                        if (!prefixCounts.ContainsKey(prefix)) prefixCounts[prefix] = 0;
                        prefixCounts[prefix]++;
                    }
                }
            }

            var commonPrefixes = prefixCounts
                .Where(k => k.Value >= 3)
                .OrderByDescending(k => k.Value)
                .Take(10)
                .ToList();

            if (commonPrefixes.Count > 0)
            {
                string prefixList = string.Join(", ", commonPrefixes.Select(k => $"{k.Key}({k.Value})"));
                rules.Add(new VFXRule
                {
                    type = VFXRuleType.Naming,
                    sourceA = "batch",
                    sourceB = "batch",
                    description = $"常见前缀: {prefixList}",
                    confidence = (float)commonPrefixes[0].Value / allLayerNames.Count
                });
            }

            return rules;
        }

        private static string DetectNamingPatternSingle(string name)
        {
            string lower = name.ToLowerInvariant();

            // 拼音缩写
            // Chinese pinyin abbreviation
            string[] pinyinParts = { "lizi", "tuowei", "guang", "caidai", "tiaodai", "glow", "obj" };
            if (pinyinParts.Any(p => lower.Contains(p))) return "拼音缩写";

            // 纯数字
            // Pure number
            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^\d+$")) return "纯数字";

            // 数字后缀
            // Number suffix
            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\d+$")) return "语义+数字";

            // 下划线分隔
            // Underscore separated
            if (name.Contains("_")) return "下划线分隔";

            return "语义化";
        }

        // ==================== 结构规则 ====================
        // ==================== Structure Rules ====================

        private static List<VFXRule> DiscoverStructureRules(List<VFXRecipe> recipes)
        {
            var rules = new List<VFXRule>();

            // 1. 层级深度分析
            // 1. Hierarchy depth analysis
            var depths = recipes.Select(r => r.statistics.hierarchyDepth).ToList();
            float avgDepth = (float)depths.Average();
            int maxDepth = depths.Max();
            int minDepth = depths.Min();

            if (maxDepth - minDepth > 1)
            {
                var deepVFX = recipes.Where(r => r.statistics.hierarchyDepth >= maxDepth).ToList();
                string names = string.Join(", ", deepVFX.Select(r => r.prefabName));
                rules.Add(new VFXRule
                {
                    type = VFXRuleType.Structure,
                    sourceA = "batch",
                    sourceB = "batch",
                    description = $"层级深度范围 [{minDepth}, {maxDepth}] (平均 {avgDepth:F1})，最深: {names}",
                    confidence = 0.6f
                });
            }

            // 2. 根节点组件模式
            // 2. Root node component pattern
            var rootTypes = recipes.Select(r =>
            {
                var root = r.layers.FirstOrDefault(l => !l.path.Contains("/"));
                return root?.type ?? VFXLayerType.Custom;
            }).ToList();

            var rootTypeCounts = rootTypes.GroupBy(t => t).OrderByDescending(g => g.Count()).ToList();
            if (rootTypeCounts.Count > 0)
            {
                string typeList = string.Join(", ", rootTypeCounts.Select(g => $"{g.Key}({g.Count()})"));
                rules.Add(new VFXRule
                {
                    type = VFXRuleType.Structure,
                    sourceA = "batch",
                    sourceB = "batch",
                    description = $"根节点组件类型分布: {typeList}",
                    confidence = (float)rootTypeCounts[0].Count() / recipes.Count
                });
            }

            // 3. Shader使用规律
            // 3. Shader usage patterns
            var allShaders = recipes
                .SelectMany(r => r.layers.Where(l => !string.IsNullOrEmpty(l.shaderName)).Select(l => l.shaderName))
                .GroupBy(s => s)
                .OrderByDescending(g => g.Count())
                .ToList();

            if (allShaders.Count > 0)
            {
                var topShaders = allShaders.Take(5).ToList();
                string shaderList = string.Join(", ", topShaders.Select(g => $"{g.Key.Split('/').Last()}({g.Count()})"));
                rules.Add(new VFXRule
                {
                    type = VFXRuleType.Structure,
                    sourceA = "batch",
                    sourceB = "batch",
                    description = $"常用Shader: {shaderList}",
                    confidence = (float)topShaders[0].Count() / allShaders.Sum(g => g.Count())
                });
            }

            return rules;
        }

        // ==================== 规则报告 ====================
        // ==================== Rule Report ====================

        /// <summary>
        /// 生成规则发现报告
        /// Generate rule discovery report
        /// </summary>
        public static string GenerateRuleReport(List<VFXRule> rules)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"===== VFX 规则发现报告 =====");
            sb.AppendLine($"共发现 {rules.Count} 条规则");
            sb.AppendLine();

            // 按类型分组
            // Group by type
            var grouped = rules.GroupBy(r => r.type).OrderBy(g => g.Key);
            foreach (var group in grouped)
            {
                sb.AppendLine($"--- {group.Key} ({group.Count()} 条) ---");
                foreach (var rule in group.OrderByDescending(r => r.confidence))
                {
                    sb.AppendLine($"  [置信度: {rule.confidence:P0}] {rule.description}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // ==================== 模板构建辅助 ====================
        // ==================== Template Building Helpers ====================

        private static VFXTemplate BuildTemplateFromGroup(VFXCategory category, List<VFXRecipe> groupRecipes)
        {
            if (groupRecipes.Count < 2) return null;

            var template = new VFXTemplate
            {
                category = category,
                templateName = $"{category} 推荐模板",
                sampleCount = groupRecipes.Count,
                sourcePrefabNames = groupRecipes.Select(r => r.prefabName).Take(5).ToList()
            };

            // 统计每个角色的出现率和参数
            // Count occurrence rate and parameters for each role
            var roleStats = new Dictionary<VFXLayerRole, List<VFXLayer>>();
            foreach (var recipe in groupRecipes)
            {
                var seenRoles = new HashSet<VFXLayerRole>();
                foreach (var layer in recipe.layers)
                {
                    if (!roleStats.ContainsKey(layer.role))
                        roleStats[layer.role] = new List<VFXLayer>();
                    roleStats[layer.role].Add(layer);
                    seenRoles.Add(layer.role);
                }
            }

            // 生成模板层
            // Generate template layers
            var descParts = new List<string>();
            foreach (var kvp in roleStats.OrderByDescending(k => k.Value.Count))
            {
                float occurrenceRate = (float)kvp.Value.Count / groupRecipes.Count;
                if (occurrenceRate < 0.3f) continue; // 出现率<30%的跳过

                var exampleLayer = kvp.Value[0];
                var templateLayer = new VFXTemplateLayer
                {
                    role = kvp.Key,
                    type = exampleLayer.type,
                    suggestedName = GetSuggestedName(kvp.Key),
                    isOptional = occurrenceRate < 0.7f,
                    occurrenceRate = occurrenceRate
                };

                // 提取推荐参数（取中位数）
                // Extract recommended parameters (median)
                if (kvp.Value.Any(l => l.mainParams != null))
                {
                    var paramsList = kvp.Value.Where(l => l.mainParams != null).Select(l => l.mainParams).ToList();
                    templateLayer.recommendedParams = new VFXMainParams
                    {
                        duration = Median(paramsList.Select(p => p.duration).ToList()),
                        startLifetime = Median(paramsList.Select(p => p.startLifetime).ToList()),
                        startSpeed = Median(paramsList.Select(p => p.startSpeed).ToList()),
                        startSize = Median(paramsList.Select(p => p.startSize).ToList()),
                        maxParticles = Mathf.RoundToInt(Median(paramsList.Select(p => (float)p.maxParticles).ToList())),
                        emissionRate = Median(paramsList.Select(p => p.emissionRate).ToList()),
                        simulationSpeed = Median(paramsList.Select(p => p.simulationSpeed).ToList()),
                        looping = paramsList.Count(p => p.looping) > paramsList.Count / 2
                    };
                }

                template.layers.Add(templateLayer);
                descParts.Add($"{kvp.Key}{(templateLayer.isOptional ? "?" : "")}");
            }

            template.description = $"推荐层级: {string.Join(" → ", descParts)}";
            template.confidence = template.layers.Count > 0 ? (float)template.layers.Count(l => !l.isOptional) / Mathf.Max(template.layers.Count, 1) : 0f;

            return template;
        }

        private static string GetSuggestedName(VFXLayerRole role)
        {
            switch (role)
            {
                case VFXLayerRole.Core: return "Core";
                case VFXLayerRole.Glow: return "Glow";
                case VFXLayerRole.Particle: return "Part_Main";
                case VFXLayerRole.Trail: return "Trail";
                case VFXLayerRole.Ribbon: return "Ribbon";
                case VFXLayerRole.Background: return "BG";
                case VFXLayerRole.Text: return "Text";
                case VFXLayerRole.Sparkle: return "Sparkle";
                case VFXLayerRole.Smoke: return "Smoke";
                case VFXLayerRole.Ring: return "Ring";
                case VFXLayerRole.Shockwave: return "Shockwave";
                case VFXLayerRole.Debris: return "Debris";
                default: return "Layer";
            }
        }

        private static float Median(List<float> values)
        {
            if (values == null || values.Count == 0) return 0f;
            var sorted = values.OrderBy(v => v).ToList();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 != 0 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2f;
        }

        private static float StdDev(List<float> values, float mean)
        {
            if (values == null || values.Count < 2) return 0f;
            float sumSquares = values.Sum(v => (v - mean) * (v - mean));
            return Mathf.Sqrt(sumSquares / values.Count);
        }
    }
}
#endif
