#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MoshiVFXGenerator.Analyzer
{
    /// <summary>
    /// VFX Prefab对比器 - 比较两个特效Prefab的差异
    /// VFX Prefab Comparator - Compare differences between two VFX Prefabs
    /// </summary>
    public static class VFXComparator
    {
        // ==================== 公共接口 ====================
        // ==================== Public API ====================

        /// <summary>
        /// 对比两个VFX配方，返回对比结果
        /// Compare two VFX recipes and return comparison result
        /// </summary>
        public static VFXComparison Compare(VFXRecipe recipeA, VFXRecipe recipeB)
        {
            if (recipeA == null || recipeB == null) return null;

            var result = new VFXComparison
            {
                prefabPathA = recipeA.prefabPath,
                prefabPathB = recipeB.prefabPath,
                matchedLayers = new List<VFXLayerMatch>(),
                onlyInA = new List<VFXLayer>(),
                onlyInB = new List<VFXLayer>(),
                paramDiffs = new List<VFXParamDiff>(),
                discoveredRules = new List<VFXRule>()
            };

            // 1. 按角色分组并匹配层级
            // 1. Group by role and match layers
            var matchResult = MatchLayersByRole(recipeA.layers, recipeB.layers);
            result.matchedLayers = matchResult.matches;
            result.onlyInA = matchResult.unmatchedA;
            result.onlyInB = matchResult.unmatchedB;

            // 2. 计算匹配层对的参数差异
            // 2. Calculate parameter differences for matched pairs
            foreach (var match in result.matchedLayers)
            {
                var diffs = CalculateParamDiffs(match.layerA, match.layerB);
                result.paramDiffs.AddRange(diffs);
                match.paramDiffs = diffs.Select(d => d.diffDescription).ToList();
            }

            // 3. 计算整体相似度
            // 3. Calculate overall similarity
            result.overallSimilarity = CalculateOverallSimilarity(recipeA, recipeB, result);

            // 4. 发现规则
            // 4. Discover rules
            result.discoveredRules = DiscoverRulesFromComparison(recipeA, recipeB, result);

            return result;
        }

        // ==================== 层级匹配 ====================
        // ==================== Layer Matching ====================

        private class LayerMatchResult
        {
            public List<VFXLayerMatch> matches;
            public List<VFXLayer> unmatchedA;
            public List<VFXLayer> unmatchedB;
        }

        /// <summary>
        /// 按角色分组匹配两个特效的层级
        /// Match layers of two VFX by role grouping
        /// </summary>
        private static LayerMatchResult MatchLayersByRole(List<VFXLayer> layersA, List<VFXLayer> layersB)
        {
            var result = new LayerMatchResult
            {
                matches = new List<VFXLayerMatch>(),
                unmatchedA = new List<VFXLayer>(),
                unmatchedB = new List<VFXLayer>()
            };

            // 按角色分组
            // Group by role
            var groupA = layersA.GroupBy(l => l.role).ToDictionary(g => g.Key, g => g.ToList());
            var groupB = layersB.GroupBy(l => l.role).ToDictionary(g => g.Key, g => g.ToList());

            // 获取所有角色
            // Get all roles
            var allRoles = new HashSet<VFXLayerRole>(groupA.Keys);
            foreach (var role in groupB.Keys) allRoles.Add(role);

            var usedB = new HashSet<VFXLayer>();

            foreach (var role in allRoles)
            {
                groupA.TryGetValue(role, out var listA);
                groupB.TryGetValue(role, out var listB);

                if (listA == null || listA.Count == 0)
                {
                    if (listB != null) result.unmatchedB.AddRange(listB);
                    continue;
                }
                if (listB == null || listB.Count == 0)
                {
                    result.unmatchedA.AddRange(listA);
                    continue;
                }

                // 在同角色组内按名称相似度匹配
                // Match by name similarity within same role group
                var usedIndices = new HashSet<int>();
                foreach (var layerA in listA)
                {
                    int bestIdx = -1;
                    float bestSim = 0.3f; // 最低匹配阈值 / Minimum match threshold

                    for (int i = 0; i < listB.Count; i++)
                    {
                        if (usedIndices.Contains(i) || usedB.Contains(listB[i])) continue;

                        float sim = CalculateLayerSimilarity(layerA, listB[i]);
                        if (sim > bestSim)
                        {
                            bestSim = sim;
                            bestIdx = i;
                        }
                    }

                    if (bestIdx >= 0)
                    {
                        usedIndices.Add(bestIdx);
                        usedB.Add(listB[bestIdx]);
                        result.matches.Add(new VFXLayerMatch
                        {
                            layerA = layerA,
                            layerB = listB[bestIdx],
                            similarity = bestSim
                        });
                    }
                    else
                    {
                        result.unmatchedA.Add(layerA);
                    }
                }

                // B中未匹配的
                // Unmatched in B
                for (int i = 0; i < listB.Count; i++)
                {
                    if (!usedIndices.Contains(i) && !usedB.Contains(listB[i]))
                        result.unmatchedB.Add(listB[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// 计算两个层的相似度（0~1）
        /// Calculate similarity between two layers (0~1)
        /// </summary>
        private static float CalculateLayerSimilarity(VFXLayer a, VFXLayer b)
        {
            float score = 0f;
            float totalWeight = 0f;

            // 名称相似度（权重0.2）
            // Name similarity (weight 0.2)
            float nameSim = CalculateStringSimilarity(a.name, b.name);
            score += nameSim * 0.2f;
            totalWeight += 0.2f;

            // 参数相似度（权重0.5）
            // Parameter similarity (weight 0.5)
            if (a.mainParams != null && b.mainParams != null)
            {
                score += CalculateParamSimilarity(a.mainParams, b.mainParams) * 0.5f;
            }
            totalWeight += 0.5f;

            // 模块相似度（权重0.15）
            // Module similarity (weight 0.15)
            if (a.modules != null && b.modules != null)
            {
                score += CalculateModuleSimilarity(a.modules, b.modules) * 0.15f;
            }
            totalWeight += 0.15f;

            // 类型相同加分（权重0.15）
            // Same type bonus (weight 0.15)
            if (a.type == b.type)
                score += 1f * 0.15f;
            totalWeight += 0.15f;

            return totalWeight > 0 ? score / totalWeight : 0f;
        }

        /// <summary>
        /// 计算两个主参数的相似度
        /// Calculate similarity between two main parameter sets
        /// </summary>
        private static float CalculateParamSimilarity(VFXMainParams a, VFXMainParams b)
        {
            float score = 0f;
            int count = 0;

            // 比较关键参数，使用相对误差
            // Compare key parameters using relative error
            score += CompareParam(a.duration, b.duration, 1f); count++;
            score += CompareParam(a.startLifetime, b.startLifetime, 0.5f); count++;
            score += CompareParam(a.startSpeed, b.startSpeed, 2f); count++;
            score += CompareParam(a.startSize, b.startSize, 0.5f); count++;
            score += CompareParam(a.emissionRate, b.emissionRate, 10f); count++;
            score += CompareParam(a.maxParticles, b.maxParticles, 100f); count++;
            score += CompareParam(a.gravityModifier, b.gravityModifier, 0.5f); count++;
            score += CompareParam(a.simulationSpeed, b.simulationSpeed, 0.1f); count++;

            // 布尔值
            // Boolean values
            if (a.looping == b.looping) score += 1f;
            count++;

            // 形状类型
            // Shape type
            if (a.shapeType == b.shapeType) score += 1f;
            count++;

            // 颜色相似度
            // Color similarity
            score += CompareColor(a.startColor, b.startColor);
            count++;

            return count > 0 ? score / count : 0f;
        }

        private static float CompareParam(float a, float b, float tolerance)
        {
            float diff = Mathf.Abs(a - b);
            float maxVal = Mathf.Max(Mathf.Abs(a), Mathf.Abs(b), 0.001f);
            float relativeDiff = diff / maxVal;

            if (diff <= tolerance) return 1f;
            if (relativeDiff < 0.1f) return 0.9f;
            if (relativeDiff < 0.3f) return 0.7f;
            if (relativeDiff < 0.5f) return 0.5f;
            if (relativeDiff < 1.0f) return 0.3f;
            return 0.1f;
        }

        private static float CompareColor(Color a, Color b)
        {
            float diff = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a);
            return Mathf.Clamp01(1f - diff / 4f);
        }

        private static float CalculateModuleSimilarity(VFXEnabledModules a, VFXEnabledModules b)
        {
            var namesA = new HashSet<string>(a.GetEnabledNames());
            var namesB = new HashSet<string>(b.GetEnabledNames());

            int intersection = namesA.Intersect(namesB).Count();
            int union = namesA.Union(namesB).Count();

            return union > 0 ? (float)intersection / union : 1f;
        }

        private static float CalculateStringSimilarity(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b) ? 1f : 0f;

            a = a.ToLowerInvariant();
            b = b.ToLowerInvariant();

            if (a == b) return 1f;

            // 包含关系
            // Containment relationship
            if (a.Contains(b) || b.Contains(a)) return 0.8f;

            // 共同子串比例（简单实现）
            // Common substring ratio (simple implementation)
            int common = 0;
            string shorter = a.Length <= b.Length ? a : b;
            string longer = a.Length > b.Length ? a : b;

            for (int i = 0; i < shorter.Length; i++)
            {
                if (longer.Contains(shorter[i]))
                    common++;
            }

            return (float)common / longer.Length;
        }

        // ==================== 参数差异 ====================
        // ==================== Parameter Differences ====================

        private static List<VFXParamDiff> CalculateParamDiffs(VFXLayer a, VFXLayer b)
        {
            var diffs = new List<VFXParamDiff>();

            if (a.mainParams != null && b.mainParams != null)
            {
                var mpA = a.mainParams;
                var mpB = b.mainParams;

                AddDiffIfSignificant(diffs, a.name, "Duration", mpA.duration, mpB.duration, "s");
                AddDiffIfSignificant(diffs, a.name, "StartLifetime", mpA.startLifetime, mpB.startLifetime, "s");
                AddDiffIfSignificant(diffs, a.name, "StartSpeed", mpA.startSpeed, mpB.startSpeed, "");
                AddDiffIfSignificant(diffs, a.name, "StartSize", mpA.startSize, mpB.startSize, "");
                AddDiffIfSignificant(diffs, a.name, "EmissionRate", mpA.emissionRate, mpB.emissionRate, "/s");
                AddDiffIfSignificant(diffs, a.name, "MaxParticles", mpA.maxParticles, mpB.maxParticles, "");
                AddDiffIfSignificant(diffs, a.name, "GravityModifier", mpA.gravityModifier, mpB.gravityModifier, "");
                AddDiffIfSignificant(diffs, a.name, "SimulationSpeed", mpA.simulationSpeed, mpB.simulationSpeed, "x");

                if (mpA.looping != mpB.looping)
                {
                    diffs.Add(new VFXParamDiff
                    {
                        layerName = a.name,
                        paramName = "Looping",
                        valueA = mpA.looping.ToString(),
                        valueB = mpB.looping.ToString(),
                        diffDescription = $"Looping: {mpA.looping} → {mpB.looping}"
                    });
                }

                if (mpA.shapeType != mpB.shapeType)
                {
                    diffs.Add(new VFXParamDiff
                    {
                        layerName = a.name,
                        paramName = "ShapeType",
                        valueA = mpA.shapeType.ToString(),
                        valueB = mpB.shapeType.ToString(),
                        diffDescription = $"Shape: {mpA.shapeType} → {mpB.shapeType}"
                    });
                }
            }

            // 材质差异
            // Material differences
            if (a.materialGuid != b.materialGuid)
            {
                diffs.Add(new VFXParamDiff
                {
                    layerName = a.name,
                    paramName = "Material",
                    valueA = a.materialGuid?.Substring(0, Math.Min(8, a.materialGuid.Length)) ?? "None",
                    valueB = b.materialGuid?.Substring(0, Math.Min(8, b.materialGuid.Length)) ?? "None",
                    diffDescription = $"Material changed"
                });
            }

            // Shader差异
            // Shader differences
            if (a.shaderName != b.shaderName)
            {
                diffs.Add(new VFXParamDiff
                {
                    layerName = a.name,
                    paramName = "Shader",
                    valueA = a.shaderName ?? "None",
                    valueB = b.shaderName ?? "None",
                    diffDescription = $"Shader: {a.shaderName} → {b.shaderName}"
                });
            }

            return diffs;
        }

        private static void AddDiffIfSignificant(List<VFXParamDiff> diffs, string layerName, string paramName, float valA, float valB, string unit)
        {
            float diff = Mathf.Abs(valA - valB);
            float maxVal = Mathf.Max(Mathf.Abs(valA), Mathf.Abs(valB), 0.001f);
            float relativeDiff = diff / maxVal;

            // 相对差异超过10%或绝对差异超过阈值才记录
            // Only record if relative diff > 10% or absolute diff > threshold
            if (relativeDiff > 0.1f && diff > 0.01f)
            {
                diffs.Add(new VFXParamDiff
                {
                    layerName = layerName,
                    paramName = paramName,
                    valueA = $"{valA:F2}{unit}",
                    valueB = $"{valB:F2}{unit}",
                    diffDescription = $"{paramName}: {valA:F2} → {valB:F2} ({(valB > valA ? "+" : "")}{((valB - valA) / Mathf.Max(Mathf.Abs(valA), 0.001f) * 100):F1}%)"
                });
            }
        }

        // ==================== 整体相似度 ====================
        // ==================== Overall Similarity ====================

        private static float CalculateOverallSimilarity(VFXRecipe a, VFXRecipe b, VFXComparison comp)
        {
            if (a.layers.Count == 0 && b.layers.Count == 0) return 1f;
            if (a.layers.Count == 0 || b.layers.Count == 0) return 0f;

            float score = 0f;
            int count = 0;

            // 1. 分类一致性（权重0.15）
            // 1. Category consistency (weight 0.15)
            score += (a.category == b.category ? 1f : 0f) * 0.15f;
            count++;

            // 2. 层数相似度（权重0.15）
            // 2. Layer count similarity (weight 0.15)
            int maxLayers = Mathf.Max(a.layers.Count, b.layers.Count);
            float layerSim = 1f - (float)Math.Abs(a.layers.Count - b.layers.Count) / maxLayers;
            score += layerSim * 0.15f;
            count++;

            // 3. 匹配层对的平均相似度（权重0.35）
            // 3. Average similarity of matched pairs (weight 0.35)
            float avgMatchSim = 0f;
            if (comp.matchedLayers.Count > 0)
            {
                avgMatchSim = comp.matchedLayers.Average(m => m.similarity);
                // 乘以覆盖率（匹配层数 / 总层数）
                // Multiply by coverage (matched / total)
                float coverage = (float)(comp.matchedLayers.Count * 2) / (a.layers.Count + b.layers.Count);
                avgMatchSim *= coverage;
            }
            score += avgMatchSim * 0.35f;
            count++;

            // 4. 独有层惩罚（权重0.15）
            // 4. Unique layer penalty (weight 0.15)
            float uniquePenalty = (float)(comp.onlyInA.Count + comp.onlyInB.Count) / maxLayers;
            score += (1f - uniquePenalty) * 0.15f;
            count++;

            // 5. 材质/Shader重合度（权重0.1）
            // 5. Material/Shader overlap (weight 0.1)
            float matOverlap = CalculateSetOverlap(
                new HashSet<string>(a.layers.Where(l => !string.IsNullOrEmpty(l.materialGuid)).Select(l => l.materialGuid)),
                new HashSet<string>(b.layers.Where(l => !string.IsNullOrEmpty(l.materialGuid)).Select(l => l.materialGuid))
            );
            score += matOverlap * 0.1f;
            count++;

            // 6. 最大粒子数相似度（权重0.1）
            // 6. Max particles similarity (weight 0.1)
            float particleSim = 1f - Mathf.Abs(a.statistics.totalMaxParticles - b.statistics.totalMaxParticles)
                / Mathf.Max(a.statistics.totalMaxParticles, b.statistics.totalMaxParticles, 1f);
            score += particleSim * 0.1f;
            count++;

            return Mathf.Clamp01(count > 0 ? score : 0f);
        }

        private static float CalculateSetOverlap(HashSet<string> setA, HashSet<string> setB)
        {
            if (setA.Count == 0 && setB.Count == 0) return 1f;
            if (setA.Count == 0 || setB.Count == 0) return 0f;
            int intersection = setA.Intersect(setB).Count();
            return (float)intersection / setA.Union(setB).Count();
        }

        // ==================== 规则发现 ====================
        // ==================== Rule Discovery ====================

        private static List<VFXRule> DiscoverRulesFromComparison(VFXRecipe a, VFXRecipe b, VFXComparison comp)
        {
            var rules = new List<VFXRule>();
            string nameA = a.prefabName;
            string nameB = b.prefabName;

            // 组成规则：层级数量差异
            // Composition rule: layer count difference
            if (comp.onlyInA.Count > 0)
            {
                string layerNames = string.Join(", ", comp.onlyInA.Select(l => l.name));
                rules.Add(new VFXRule
                {
                    type = VFXRuleType.Composition,
                    sourceA = nameA,
                    sourceB = nameB,
                    description = $"{nameA} 比 {nameB} 多出 {comp.onlyInA.Count} 个层: {layerNames}",
                    confidence = 0.8f
                });
            }
            if (comp.onlyInB.Count > 0)
            {
                string layerNames = string.Join(", ", comp.onlyInB.Select(l => l.name));
                rules.Add(new VFXRule
                {
                    type = VFXRuleType.Composition,
                    sourceA = nameA,
                    sourceB = nameB,
                    description = $"{nameB} 比 {nameA} 多出 {comp.onlyInB.Count} 个层: {layerNames}",
                    confidence = 0.8f
                });
            }

            // 参数规则：提取显著参数差异
            // Parameter rule: extract significant parameter differences
            foreach (var diff in comp.paramDiffs)
            {
                rules.Add(new VFXRule
                {
                    type = VFXRuleType.Parameter,
                    sourceA = nameA,
                    sourceB = nameB,
                    description = $"层 [{diff.layerName}] 参数差异: {diff.diffDescription}",
                    confidence = 0.7f,
                    exampleLayerName = diff.layerName
                });
            }

            // 性能规则：粒子量差异
            // Performance rule: particle count difference
            float particleDiff = Mathf.Abs(a.statistics.totalMaxParticles - b.statistics.totalMaxParticles);
            float particleRatio = Mathf.Max(a.statistics.totalMaxParticles, b.statistics.totalMaxParticles) / 
                                  Mathf.Max(Mathf.Min(a.statistics.totalMaxParticles, b.statistics.totalMaxParticles), 1f);
            if (particleRatio > 1.5f)
            {
                rules.Add(new VFXRule
                {
                    type = VFXRuleType.Performance,
                    sourceA = nameA,
                    sourceB = nameB,
                    description = $"粒子量差异显著: {nameA}({a.statistics.totalMaxParticles:F0}) vs {nameB}({b.statistics.totalMaxParticles:F0})，倍率 {particleRatio:F1}x",
                    confidence = 0.9f
                });
            }

            // 命名规则：命名模式差异
            // Naming rule: naming pattern difference
            var patternA = DetectNamingPattern(a.layers.Select(l => l.name).ToList());
            var patternB = DetectNamingPattern(b.layers.Select(l => l.name).ToList());
            if (patternA != patternB)
            {
                rules.Add(new VFXRule
                {
                    type = VFXRuleType.Naming,
                    sourceA = nameA,
                    sourceB = nameB,
                    description = $"命名模式不同: {nameA} 使用 '{patternA}'，{nameB} 使用 '{patternB}'",
                    confidence = 0.85f
                });
            }

            return rules;
        }

        private static string DetectNamingPattern(List<string> names)
        {
            if (names == null || names.Count == 0) return "empty";

            // 检测常见命名模式
            // Detect common naming patterns
            bool hasChinesePinyin = names.Any(n => RegexContainsChinesePinyin(n));
            bool hasUnderscore = names.Any(n => n.Contains("_"));
            bool hasNumberSuffix = names.Count(n => System.Text.RegularExpressions.Regex.IsMatch(n, @"\d+$")) > names.Count / 2;

            if (hasChinesePinyin) return "拼音缩写" + (hasNumberSuffix ? "+数字后缀" : "");
            if (hasUnderscore) return "下划线分隔";
            if (hasNumberSuffix) return "数字后缀";
            return "语义化命名";
        }

        private static bool RegexContainsChinesePinyin(string name)
        {
            string[] pinyinKeywords = { "lizi", "tuowei", "guang", "caidai", "tiaodai", "yan", "sz" };
            string lower = name.ToLowerInvariant();
            return pinyinKeywords.Any(k => lower.Contains(k));
        }

        // ==================== 对比报告生成 ====================
        // ==================== Comparison Report Generation ====================

        /// <summary>
        /// 生成可读的对比报告
        /// Generate a readable comparison report
        /// </summary>
        public static string GenerateComparisonReport(VFXComparison comp)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"===== VFX 对比报告 =====");
            sb.AppendLine($"A: {Path.GetFileName(comp.prefabPathA)}");
            sb.AppendLine($"B: {Path.GetFileName(comp.prefabPathB)}");
            sb.AppendLine($"整体相似度: {comp.overallSimilarity:P1}");
            sb.AppendLine();

            // 匹配的层
            // Matched layers
            sb.AppendLine($"--- 匹配的层 ({comp.matchedLayers.Count} 对) ---");
            foreach (var match in comp.matchedLayers)
            {
                sb.AppendLine($"  [{match.layerA.role}] {match.layerA.name} ↔ {match.layerB.name} (相似度: {match.similarity:P0})");
                foreach (var diff in match.paramDiffs)
                    sb.AppendLine($"    · {diff}");
            }
            sb.AppendLine();

            // 仅在A中
            // Only in A
            if (comp.onlyInA.Count > 0)
            {
                sb.AppendLine($"--- 仅在 A 中 ({comp.onlyInA.Count} 个) ---");
                foreach (var layer in comp.onlyInA)
                    sb.AppendLine($"  {layer.ToBrief()}");
                sb.AppendLine();
            }

            // 仅在B中
            // Only in B
            if (comp.onlyInB.Count > 0)
            {
                sb.AppendLine($"--- 仅在 B 中 ({comp.onlyInB.Count} 个) ---");
                foreach (var layer in comp.onlyInB)
                    sb.AppendLine($"  {layer.ToBrief()}");
                sb.AppendLine();
            }

            // 发现的规则
            // Discovered rules
            if (comp.discoveredRules.Count > 0)
            {
                sb.AppendLine($"--- 发现的规则 ({comp.discoveredRules.Count} 条) ---");
                foreach (var rule in comp.discoveredRules)
                    sb.AppendLine($"  [{rule.type}] (置信度: {rule.confidence:P0}) {rule.description}");
            }

            return sb.ToString();
        }
    }
}
#endif
