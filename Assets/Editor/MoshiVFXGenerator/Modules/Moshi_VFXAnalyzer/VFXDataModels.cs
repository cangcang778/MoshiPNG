#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoshiVFXGenerator.Analyzer
{
    // ==================== 枚举定义 ====================
    // ==================== Enumeration Definitions ====================

    /// <summary>
    /// 特效分类枚举
    /// VFX category enumeration
    /// </summary>
    public enum VFXCategory
    {
        Unknown,           // 未知 / Unknown
        UI_Result,         // 结算类特效 / Settlement VFX
        UI_Congratulate,   // 恭贺类特效 / Congratulation VFX
        Combat_Hit,        // 打击特效 / Combat hit VFX
        Combat_Buff,       // Buff特效 / Buff VFX
        Combat_Skill,      // 技能特效 / Skill VFX
        Scene_Ambient,     // 场景氛围特效 / Scene ambient VFX
        Scene_Weather,     // 天气特效 / Weather VFX
        Character,         // 角色特效 / Character VFX
        Prop,              // 道具特效 / Prop VFX
        UI_Common,         // 通用UI特效 / Common UI VFX
        Custom             // 自定义 / Custom
    }

    /// <summary>
    /// 特效层角色枚举
    /// VFX layer role enumeration
    /// </summary>
    public enum VFXLayerRole
    {
        Unknown,       // 未知 / Unknown
        Glow,          // 光晕层 / Glow layer
        Particle,      // 粒子层 / Particle layer
        Ribbon,        // 飘带层 / Ribbon layer
        Trail,         // 拖尾层 / Trail layer
        Core,          // 核心层 / Core layer
        Background,    // 背景层 / Background layer
        Text,          // 文字层 / Text layer
        Sparkle,       // 闪光层 / Sparkle layer
        Smoke,         // 烟雾层 / Smoke layer
        Ring,          // 环形层 / Ring layer
        Shockwave,     // 冲击波层 / Shockwave layer
        Debris         // 碎片层 / Debris layer
    }

    /// <summary>
    /// 特效层类型枚举
    /// VFX layer type enumeration
    /// </summary>
    public enum VFXLayerType
    {
        ParticleSystem,  // 粒子系统 / Particle System
        MeshRenderer,    // 网格渲染器 / Mesh Renderer
        TrailRenderer,   // 拖尾渲染器 / Trail Renderer
        LineRenderer,    // 线条渲染器 / Line Renderer
        Animator,        // 动画控制器 / Animator
        Light,           // 光源 / Light
        Audio,           // 音频 / Audio
        Custom           // 自定义 / Custom
    }

    /// <summary>
    /// 规则类型枚举
    /// Rule type enumeration
    /// </summary>
    public enum VFXRuleType
    {
        Composition,     // 组成规则 / Composition rule
        Parameter,       // 参数规则 / Parameter rule
        Performance,     // 性能规则 / Performance rule
        Naming,          // 命名规则 / Naming rule
        Structure        // 结构规则 / Structure rule
    }

    /// <summary>
    /// 性能评级枚举
    /// Performance rating enumeration
    /// </summary>
    public enum VFXPerformanceRating
    {
        S,   // 优秀 - 极低消耗 / Excellent - Very low cost
        A,   // 良好 - 低消耗 / Good - Low cost
        B,   // 一般 - 中等消耗 / Average - Medium cost
        C,   // 较差 - 较高消耗 / Below Average - High cost
        D    // 差 - 高消耗 / Poor - Very high cost
    }

    /// <summary>
    /// 优化建议严重级别
    /// Optimization suggestion severity level
    /// </summary>
    public enum VFXSeverity
    {
        Info,       // 信息提示 / Informational
        Warning,    // 警告 / Warning
        Critical    // 严重 / Critical
    }

    // ==================== 数据结构定义 ====================
    // ==================== Data Structure Definitions ====================

    /// <summary>
    /// 特效层主模块参数
    /// VFX layer main module parameters
    /// </summary>
    [Serializable]
    public class VFXMainParams
    {
        public float duration = 5f;
        public float simulationSpeed = 1f;
        public bool looping = false;
        public float startLifetime = 1f;
        public float startSpeed = 5f;
        public float startSize = 1f;
        public float gravityModifier = 0f;
        public int maxParticles = 1000;
        public float emissionRate = 10f;
        public float startDelay = 0f;

        // 形状参数 / Shape parameters
        public ParticleSystemShapeType shapeType = ParticleSystemShapeType.Cone;
        public float shapeAngle = 25f;
        public float shapeRadius = 1f;
        public Vector3 shapeScale = Vector3.one;

        // 颜色参数 / Color parameters
        public Color startColor = Color.white;
        public bool colorOverLifetime = false;

        // 大小参数 / Size parameters
        public bool sizeOverLifetime = false;

        // 旋转参数 / Rotation parameters
        public float startRotation = 0f;
        public float rotationSpeed = 0f;

        /// <summary>
        /// 序列化为可读字符串 / Serialize to readable string
        /// </summary>
        public string ToSummary()
        {
            return $"Duration:{duration:F2}s Speed:{simulationSpeed:F1}x Loop:{looping} " +
                   $"Life:{startLifetime:F2}s Speed:{startSpeed:F1} Size:{startSize:F2} " +
                   $"Gravity:{gravityModifier:F2} Max:{maxParticles} Rate:{emissionRate:F1}/s";
        }
    }

    /// <summary>
    /// 特效层启用模块记录
    /// VFX layer enabled modules record
    /// </summary>
    [Serializable]
    public class VFXEnabledModules
    {
        public bool trails = false;
        public bool subEmitters = false;
        public bool collision = false;
        public bool noise = false;
        public bool colorOverLifetime = false;
        public bool sizeOverLifetime = false;
        public bool rotationOverLifetime = false;
        public bool velocityOverLifetime = false;
        public bool forceOverLifetime = false;
        public bool limitVelocityOverLifetime = false;
        public bool textureSheetAnimation = false;
        public bool lights = false;
        public bool customData = false;
        public bool externalForces = false;

        /// <summary>
        /// 获取启用的模块数量 / Get count of enabled modules
        /// </summary>
        public int GetEnabledCount()
        {
            int count = 0;
            var fields = typeof(VFXEnabledModules).GetFields();
            foreach (var f in fields)
                if (f.FieldType == typeof(bool) && (bool)f.GetValue(this))
                    count++;
            return count;
        }

        /// <summary>
        /// 获取所有启用的模块名 / Get all enabled module names
        /// </summary>
        public List<string> GetEnabledNames()
        {
            var names = new List<string>();
            var fields = typeof(VFXEnabledModules).GetFields();
            foreach (var f in fields)
                if (f.FieldType == typeof(bool) && (bool)f.GetValue(this))
                    names.Add(f.Name);
            return names;
        }
    }

    /// <summary>
    /// 特效层定义
    /// VFX layer definition
    /// </summary>
    [Serializable]
    public class VFXLayer
    {
        public string name;                   // 层名称 / Layer name
        public string path;                   // 层路径 / Layer path (e.g. "root/child/sub")
        public VFXLayerRole role;             // 推断的角色 / Inferred role
        public VFXLayerType type;             // 组件类型 / Component type
        public VFXMainParams mainParams;      // 主参数 / Main parameters
        public VFXEnabledModules modules;     // 启用的模块 / Enabled modules
        public string materialGuid;           // 材质GUID / Material GUID
        public string shaderName;             // Shader名称 / Shader name
        public bool isPlaying = true;         // 是否播放中 / Is playing
        public Vector3 localPosition;         // 本地坐标 / Local position
        public Vector3 localScale;            // 本地缩放 / Local scale
        public int sortingOrder;              // 排序顺序 / Sorting order

        /// <summary>
        /// 获取简短描述 / Get brief description
        /// </summary>
        public string ToBrief()
        {
            string matInfo = string.IsNullOrEmpty(materialGuid) ? "NoMat" : $"Mat:{materialGuid.Substring(0, 8)}";
            return $"[{role}] {name} ({type}) {matInfo}";
        }
    }

    /// <summary>
    /// 特效统计信息
    /// VFX statistics
    /// </summary>
    [Serializable]
    public class VFXStatistics
    {
        public int totalLayers;
        public int particleSystemCount;
        public int meshRendererCount;
        public int trailRendererCount;
        public int animatorCount;
        public int uniqueMaterialCount;
        public int uniqueShaderCount;
        public float totalMaxParticles;
        public float avgDuration;
        public float maxDuration;
        public float minDuration;
        public int hierarchyDepth;
        public int enabledModuleAvg;
        public int loopingCount;            // 循环层数量 / Number of looping layers
    }

    /// <summary>
    /// 性能评估报告
    /// Performance evaluation report
    /// </summary>
    [Serializable]
    public class VFXPerformanceReport
    {
        public VFXPerformanceRating rating;       // 综合评级 / Overall rating
        public float score;                        // 综合得分 0~100 / Overall score 0~100
        public float particleScore;                // 粒子得分 / Particle score
        public float layerScore;                   // 层级得分 / Layer score
        public float materialScore;                // 材质得分 / Material score
        public float complexityScore;              // 复杂度得分 / Complexity score
        public List<VFXOptimizationSuggestion> suggestions = new List<VFXOptimizationSuggestion>();

        /// <summary>
        /// 获取评级对应的颜色 / Get color for rating
        /// </summary>
        public Color GetRatingColor()
        {
            switch (rating)
            {
                case VFXPerformanceRating.S: return new Color(0.2f, 0.9f, 0.3f);
                case VFXPerformanceRating.A: return new Color(0.4f, 0.8f, 0.4f);
                case VFXPerformanceRating.B: return new Color(0.9f, 0.8f, 0.2f);
                case VFXPerformanceRating.C: return new Color(0.9f, 0.5f, 0.2f);
                case VFXPerformanceRating.D: return new Color(0.9f, 0.2f, 0.2f);
                default: return Color.gray;
            }
        }

        /// <summary>
        /// 获取评级描述 / Get rating description
        /// </summary>
        public string GetRatingDescription()
        {
            switch (rating)
            {
                case VFXPerformanceRating.S: return "优秀 - 极低消耗，可放心使用";
                case VFXPerformanceRating.A: return "良好 - 低消耗，适合常规使用";
                case VFXPerformanceRating.B: return "一般 - 中等消耗，需注意用量";
                case VFXPerformanceRating.C: return "较差 - 较高消耗，建议优化";
                case VFXPerformanceRating.D: return "严重 - 高消耗，必须优化";
                default: return "未评估";
            }
        }
    }

    /// <summary>
    /// 优化建议
    /// Optimization suggestion
    /// </summary>
    [Serializable]
    public class VFXOptimizationSuggestion
    {
        public string title;                       // 标题 / Title
        public string description;                 // 详细描述 / Detailed description
        public VFXSeverity severity;               // 严重级别 / Severity level
        public string targetLayerName;             // 目标层名 / Target layer name (empty = global)
        public string category;                    // 建议分类(粒子/材质/结构/渲染) / Suggestion category
        public float estimatedSaving;              // 预估节省百分比 / Estimated saving percentage

        /// <summary>
        /// 获取严重级别颜色 / Get severity color
        /// </summary>
        public Color GetSeverityColor()
        {
            switch (severity)
            {
                case VFXSeverity.Info: return new Color(0.3f, 0.6f, 0.9f);
                case VFXSeverity.Warning: return new Color(0.9f, 0.7f, 0.2f);
                case VFXSeverity.Critical: return new Color(0.9f, 0.2f, 0.2f);
                default: return Color.gray;
            }
        }

        /// <summary>
        /// 获取严重级别图标 / Get severity icon
        /// </summary>
        public string GetSeverityIcon()
        {
            switch (severity)
            {
                case VFXSeverity.Info: return "ℹ";
                case VFXSeverity.Warning: return "⚠";
                case VFXSeverity.Critical: return "✖";
                default: return "?";
            }
        }
    }

    /// <summary>
    /// 特效结构模板 - 基于规则库提炼的推荐结构
    /// VFX structure template - recommended structure distilled from rule library
    /// </summary>
    [Serializable]
    public class VFXTemplate
    {
        public string templateName;                // 模板名称 / Template name
        public VFXCategory category;               // 适用分类 / Applicable category
        public List<VFXTemplateLayer> layers = new List<VFXTemplateLayer>();
        public string description;                 // 模板描述 / Template description
        public float confidence;                   // 置信度 / Confidence
        public int sampleCount;                    // 样本数量 / Sample count
        public List<string> sourcePrefabNames = new List<string>(); // 来源特效名 / Source VFX names
    }

    /// <summary>
    /// 模板层定义
    /// Template layer definition
    /// </summary>
    [Serializable]
    public class VFXTemplateLayer
    {
        public VFXLayerRole role;                  // 推荐角色 / Recommended role
        public VFXLayerType type;                  // 推荐类型 / Recommended type
        public string suggestedName;               // 建议命名 / Suggested naming
        public VFXMainParams recommendedParams;    // 推荐参数范围 / Recommended parameter range
        public bool isOptional;                    // 是否可选 / Is optional
        public float occurrenceRate;               // 出现率 / Occurrence rate (0~1)
    }

    /// <summary>
    /// 特效配方（完整分析结果）
    /// VFX recipe (complete analysis result)
    /// </summary>
    [Serializable]
    public class VFXRecipe
    {
        public string prefabPath;             // Prefab路径 / Prefab path
        public string prefabName;             // Prefab名称 / Prefab name
        public VFXCategory category;          // 推断的分类 / Inferred category
        public List<string> tags;             // 标签列表 / Tag list
        public List<VFXLayer> layers;         // 所有特效层 / All VFX layers
        public VFXStatistics statistics;      // 统计信息 / Statistics
        public string aiDescription;          // AI生成的描述 / AI-generated description
        public long fileSizeBytes;            // 文件大小 / File size in bytes
        public DateTime analyzeTime;          // 分析时间 / Analysis timestamp
        public VFXPerformanceReport performanceReport; // 性能评估报告 / Performance evaluation report

        /// <summary>
        /// 获取各角色的层列表 / Get layers by role
        /// </summary>
        public List<VFXLayer> GetLayersByRole(VFXLayerRole role)
        {
            return layers?.FindAll(l => l.role == role) ?? new List<VFXLayer>();
        }

        /// <summary>
        /// 获取角色的数量统计 / Get role count summary
        /// </summary>
        public Dictionary<VFXLayerRole, int> GetRoleCounts()
        {
            var counts = new Dictionary<VFXLayerRole, int>();
            if (layers == null) return counts;
            foreach (var layer in layers)
            {
                if (!counts.ContainsKey(layer.role))
                    counts[layer.role] = 0;
                counts[layer.role]++;
            }
            return counts;
        }
    }

    /// <summary>
    /// 层间匹配对
    /// Layer match pair
    /// </summary>
    [Serializable]
    public class VFXLayerMatch
    {
        public VFXLayer layerA;          // Prefab A的层 / Layer from Prefab A
        public VFXLayer layerB;          // Prefab B的层 / Layer from Prefab B
        public float similarity;         // 相似度 0~1 / Similarity score 0~1
        public List<string> paramDiffs;  // 参数差异列表 / Parameter differences
    }

    /// <summary>
    /// 参数差异
    /// Parameter difference
    /// </summary>
    [Serializable]
    public class VFXParamDiff
    {
        public string layerName;         // 层名称 / Layer name
        public string paramName;         // 参数名 / Parameter name
        public string valueA;            // Prefab A的值 / Value in Prefab A
        public string valueB;            // Prefab B的值 / Value in Prefab B
        public string diffDescription;   // 差异描述 / Difference description
    }

    /// <summary>
    /// 特效对比结果
    /// VFX comparison result
    /// </summary>
    [Serializable]
    public class VFXComparison
    {
        public string prefabPathA;
        public string prefabPathB;
        public List<VFXLayerMatch> matchedLayers;   // 匹配的层对 / Matched layer pairs
        public List<VFXLayer> onlyInA;              // 仅在A中 / Only in A
        public List<VFXLayer> onlyInB;              // 仅在B中 / Only in B
        public List<VFXParamDiff> paramDiffs;       // 参数差异 / Parameter differences
        public float overallSimilarity;            // 整体相似度 / Overall similarity
        public List<VFXRule> discoveredRules;      // 发现的规则 / Discovered rules
    }

    /// <summary>
    /// 特效规则
    /// VFX rule
    /// </summary>
    [Serializable]
    public class VFXRule
    {
        public VFXRuleType type;         // 规则类型 / Rule type
        public string sourceA;           // 来源A / Source A (prefab name)
        public string sourceB;           // 来源B / Source B (prefab name)
        public string description;       // 规则描述 / Rule description
        public float confidence;         // 置信度 0~1 / Confidence score 0~1
        public VFXCategory? category;    // 关联分类 / Related category (optional)
        public string exampleLayerName;  // 示例层名 / Example layer name
    }
}
#endif
