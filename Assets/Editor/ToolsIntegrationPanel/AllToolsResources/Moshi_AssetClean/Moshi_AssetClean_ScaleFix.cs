using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MoshiTools
{
    /// <summary>
    /// 资源清理工具 - 缩放修复模块
    /// Asset Cleaner - Particle scaling fix module
    /// </summary>
    public partial class Moshi_AssetClean
    {
        #region 缩放修复数据结构

        /// <summary>
        /// 修复结果状态
        /// Fix result status
        /// </summary>
        private enum ScaleFixStatus
        {
            Fixed,              // 已修复
            FixedNoScale,       // 已修复（未调整Scale，Renderer未启用）
            SkippedHierarchy,   // 跳过（已是Hierarchy）
            SkippedOtherMode,   // 跳过（非Local模式）
            Failed              // 失败
        }

        /// <summary>
        /// 缩放修复条目
        /// Scale fix entry
        /// </summary>
        [Serializable]
        private class ScaleFixEntry
        {
            public GameObject gameObject;
            public ParticleSystemScalingMode originalMode;
            public bool rendererEnabled;
            public bool scaleAdjusted;
            public Vector3 originalScale;
            public Vector3 newScale;
            public ScaleFixStatus status;
            public string statusReason;
        }

        #endregion

        #region 缩放修复字段

        // 修复结果
        // Fix results
        private List<ScaleFixEntry> scaleFixResults = new List<ScaleFixEntry>();
        private bool scaleFixHasExecuted = false;

        // 统计
        // Statistics
        private int sfPhase1Count = 0;      // 阶段1处理数
        private int sfPhase2Count = 0;      // 阶段2处理数
        private int sfSkipHierarchy = 0;    // 跳过-已是Hierarchy
        private int sfSkipOther = 0;        // 跳过-非Local模式
        private int sfFailCount = 0;        // 失败数

        // UI状态
        // UI state
        private Vector2 scaleFixScrollPos;

        #endregion

        #region 初始化

        partial void InitScaleFixModule()
        {
            // 初始化完成
            // Initialization complete
        }

        #endregion

        #region 缩放修复标签页绘制

        /// <summary>
        /// 绘制缩放修复标签页
        /// Draw scale fix tab
        /// </summary>
        private void DrawScaleFixTab()
        {
            // 说明区域
            // Description area
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("功能说明", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("将粒子系统的 ScalingMode 从 Local 改为 Hierarchy，", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("并自动补偿 Scale 保持粒子大小不变。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("• 阶段1: Renderer未启用 → 仅改模式，不调整Scale", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• 阶段2: Renderer启用 → 改模式 + 补偿Scale", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• 修复后粒子跟随父物体缩放，避免特效大小不一致", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("💡 提示：修复前请确保特效已在正确缩放下调整好效果", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 一键修复按钮
            // One-click fix button
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("一键修复", GUILayout.Height(30)))
            {
                ExecuteScaleFix();
            }
            GUI.backgroundColor = originalColor;

            EditorGUILayout.Space(5);

            // 结果显示
            // Results display
            DrawScaleFixResults();
        }

        /// <summary>
        /// 绘制缩放修复结果
        /// Draw scale fix results
        /// </summary>
        private void DrawScaleFixResults()
        {
            if (!scaleFixHasExecuted)
            {
                EditorGUILayout.HelpBox("点击\"一键修复\"按钮开始执行", MessageType.Info);
                return;
            }

            if (scaleFixResults.Count == 0)
            {
                EditorGUILayout.HelpBox("未找到需要修复的粒子系统", MessageType.Info);
                return;
            }

            // 统计信息
            // Statistics
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("执行结果", EditorStyles.boldLabel);

            if (sfPhase1Count > 0)
                EditorGUILayout.LabelField($"  ✅ 阶段1（Renderer未启用，仅改模式）: {sfPhase1Count}", EditorStyles.miniLabel);
            if (sfPhase2Count > 0)
                EditorGUILayout.LabelField($"  ✅ 阶段2（Renderer启用，改模式+补偿）: {sfPhase2Count}", EditorStyles.miniLabel);
            if (sfSkipHierarchy > 0)
                EditorGUILayout.LabelField($"  ⏭ 跳过（已是Hierarchy）: {sfSkipHierarchy}", EditorStyles.miniLabel);
            if (sfSkipOther > 0)
                EditorGUILayout.LabelField($"  ⏭ 跳过（非Local模式）: {sfSkipOther}", EditorStyles.miniLabel);
            if (sfFailCount > 0)
                EditorGUILayout.LabelField($"  ❌ 失败: {sfFailCount}", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 结果列表
            // Result list
            scaleFixScrollPos = EditorGUILayout.BeginScrollView(scaleFixScrollPos);

            foreach (var entry in scaleFixResults)
            {
                DrawScaleFixResultItem(entry);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制单条缩放修复结果
        /// Draw single scale fix result row
        /// </summary>
        private void DrawScaleFixResultItem(ScaleFixEntry entry)
        {
            if (entry.gameObject == null) return;

            var originalColor = GUI.backgroundColor;

            EditorGUILayout.BeginHorizontal();

            // 1. GameObject引用
            // GameObject reference
            EditorGUILayout.ObjectField("", entry.gameObject, typeof(GameObject), true, GUILayout.Width(150));

            // 2. 状态标签
            // Status label
            string statusText;
            Color statusColor;
            switch (entry.status)
            {
                case ScaleFixStatus.Fixed:
                    statusText = "✅ 已修复+补偿";
                    statusColor = new Color(0.7f, 1f, 0.7f);
                    break;
                case ScaleFixStatus.FixedNoScale:
                    statusText = "✅ 仅改模式";
                    statusColor = new Color(0.8f, 1f, 0.8f);
                    break;
                case ScaleFixStatus.SkippedHierarchy:
                    statusText = "⏭ 已是Hierarchy";
                    statusColor = new Color(0.9f, 0.9f, 0.9f);
                    break;
                case ScaleFixStatus.SkippedOtherMode:
                    statusText = $"⏭ {entry.originalMode}";
                    statusColor = new Color(1f, 1f, 0.7f);
                    break;
                case ScaleFixStatus.Failed:
                    statusText = "❌ 失败";
                    statusColor = new Color(1f, 0.7f, 0.7f);
                    break;
                default:
                    statusText = "未知";
                    statusColor = Color.white;
                    break;
            }

            GUI.backgroundColor = statusColor;
            EditorGUILayout.LabelField(statusText, EditorStyles.miniButton, GUILayout.Width(130));
            GUI.backgroundColor = originalColor;

            // 3. Scale变化信息
            // Scale change info
            if (entry.scaleAdjusted)
            {
                string scaleInfo = $"Scale: {FormatVector3(entry.originalScale)} → {FormatVector3(entry.newScale)}";
                EditorGUILayout.LabelField(scaleInfo, EditorStyles.miniLabel, GUILayout.MinWidth(200));
            }
            else if (!string.IsNullOrEmpty(entry.statusReason))
            {
                EditorGUILayout.LabelField(entry.statusReason, EditorStyles.miniLabel, GUILayout.MinWidth(200));
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 格式化Vector3为简短字符串
        /// Format Vector3 to short string
        /// </summary>
        private string FormatVector3(Vector3 v)
        {
            return $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
        }

        #endregion

        #region 缩放修复执行逻辑

        /// <summary>
        /// 执行缩放修复
        /// Execute scale fix
        /// </summary>
        private void ExecuteScaleFix()
        {
            // 清空上次结果
            // Clear previous results
            scaleFixResults.Clear();
            scaleFixHasExecuted = true;
            sfPhase1Count = 0;
            sfPhase2Count = 0;
            sfSkipHierarchy = 0;
            sfSkipOther = 0;
            sfFailCount = 0;

            var targets = GetTargetGameObjects();
            if (targets == null)
            {
                scaleFixHasExecuted = false;
                return;
            }

            Undo.SetCurrentGroupName("批量缩放修复");

            foreach (var rootObj in targets)
            {
                if (rootObj == null) continue;

                // 分类收集粒子系统
                // Categorize particle systems
                ScaleFix_CategorizeParticleSystems(rootObj,
                    out List<ScaleFixParticleData> disabledRenderers,
                    out List<ScaleFixParticleData> enabledRenderers);

                // 阶段1: 处理Renderer未启用的粒子
                // Phase 1: Process particles with disabled Renderer
                foreach (var data in disabledRenderers)
                {
                    ScaleFix_ProcessPhase1(data);
                }

                // 阶段2: 处理Renderer启用的粒子
                // Phase 2: Process particles with enabled Renderer
                foreach (var data in enabledRenderers)
                {
                    ScaleFix_ProcessPhase2(data);
                }
            }

            Debug.Log($"[{TOOL_NAME}] 缩放修复完成 - 阶段1:{sfPhase1Count} 阶段2:{sfPhase2Count} 跳过Hierarchy:{sfSkipHierarchy} 跳过其他:{sfSkipOther} 失败:{sfFailCount}");
        }

        /// <summary>
        /// 粒子系统数据（内部用）
        /// Particle system data (internal)
        /// </summary>
        private class ScaleFixParticleData
        {
            public GameObject gameObject;
            public ParticleSystem particleSystem;
            public bool rendererEnabled;
        }

        /// <summary>
        /// 分类收集粒子系统
        /// Categorize particle systems
        /// </summary>
        private void ScaleFix_CategorizeParticleSystems(GameObject root,
            out List<ScaleFixParticleData> disabled, out List<ScaleFixParticleData> enabled)
        {
            disabled = new List<ScaleFixParticleData>();
            enabled = new List<ScaleFixParticleData>();

            var allPS = root.GetComponentsInChildren<ParticleSystem>(true);

            foreach (var ps in allPS)
            {
                var renderer = ps.GetComponent<Renderer>();
                bool isEnabled = renderer == null || renderer.enabled;

                var data = new ScaleFixParticleData
                {
                    gameObject = ps.gameObject,
                    particleSystem = ps,
                    rendererEnabled = isEnabled
                };

                if (isEnabled)
                    enabled.Add(data);
                else
                    disabled.Add(data);
            }
        }

        /// <summary>
        /// 阶段1处理: Renderer未启用，仅改ScalingMode，不调整Scale
        /// Phase 1: Renderer disabled, only change ScalingMode, no Scale adjustment
        /// </summary>
        private void ScaleFix_ProcessPhase1(ScaleFixParticleData data)
        {
            var ps = data.particleSystem;
            if (ps == null)
            {
                sfFailCount++;
                scaleFixResults.Add(new ScaleFixEntry
                {
                    gameObject = data.gameObject,
                    status = ScaleFixStatus.Failed,
                    statusReason = "ParticleSystem为空"
                });
                return;
            }

            var main = ps.main;

            if (main.scalingMode == ParticleSystemScalingMode.Hierarchy)
            {
                sfSkipHierarchy++;
                scaleFixResults.Add(new ScaleFixEntry
                {
                    gameObject = data.gameObject,
                    originalMode = ParticleSystemScalingMode.Hierarchy,
                    rendererEnabled = false,
                    status = ScaleFixStatus.SkippedHierarchy,
                    statusReason = "已是Hierarchy模式"
                });
                return;
            }

            var originalMode = main.scalingMode;

            Undo.RecordObject(ps, "缩放修复-阶段1");
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            EditorUtility.SetDirty(ps);

            sfPhase1Count++;
            scaleFixResults.Add(new ScaleFixEntry
            {
                gameObject = data.gameObject,
                originalMode = originalMode,
                rendererEnabled = false,
                scaleAdjusted = false,
                status = ScaleFixStatus.FixedNoScale,
                statusReason = "Renderer未启用，仅改模式"
            });
        }

        /// <summary>
        /// 阶段2处理: Renderer启用，改ScalingMode + 补偿Scale
        /// Phase 2: Renderer enabled, change ScalingMode + compensate Scale
        /// </summary>
        private void ScaleFix_ProcessPhase2(ScaleFixParticleData data)
        {
            var ps = data.particleSystem;
            if (ps == null)
            {
                sfFailCount++;
                scaleFixResults.Add(new ScaleFixEntry
                {
                    gameObject = data.gameObject,
                    status = ScaleFixStatus.Failed,
                    statusReason = "ParticleSystem为空"
                });
                return;
            }

            var main = ps.main;
            var currentMode = main.scalingMode;

            if (currentMode == ParticleSystemScalingMode.Hierarchy)
            {
                sfSkipHierarchy++;
                scaleFixResults.Add(new ScaleFixEntry
                {
                    gameObject = data.gameObject,
                    originalMode = ParticleSystemScalingMode.Hierarchy,
                    rendererEnabled = true,
                    status = ScaleFixStatus.SkippedHierarchy,
                    statusReason = "已是Hierarchy模式"
                });
                return;
            }

            if (currentMode != ParticleSystemScalingMode.Local)
            {
                sfSkipOther++;
                scaleFixResults.Add(new ScaleFixEntry
                {
                    gameObject = data.gameObject,
                    originalMode = currentMode,
                    rendererEnabled = true,
                    status = ScaleFixStatus.SkippedOtherMode,
                    statusReason = $"当前模式为 {currentMode}，非Local"
                });
                return;
            }

            // Local → Hierarchy + 补偿Scale
            // Local → Hierarchy + compensate Scale
            Vector3 originalScale = data.gameObject.transform.localScale;
            Vector3 parentTotalScale = ScaleFix_CalculateParentTotalScale(data.gameObject.transform);
            Vector3 newScale = originalScale;

            try
            {
                if (!ScaleFix_IsApproximatelyZero(parentTotalScale))
                {
                    newScale = new Vector3(
                        Mathf.Approximately(parentTotalScale.x, 0f) ? originalScale.x : originalScale.x / parentTotalScale.x,
                        Mathf.Approximately(parentTotalScale.y, 0f) ? originalScale.y : originalScale.y / parentTotalScale.y,
                        Mathf.Approximately(parentTotalScale.z, 0f) ? originalScale.z : originalScale.z / parentTotalScale.z
                    );
                }
            }
            catch
            {
                newScale = originalScale;
            }

            Undo.RecordObject(ps, "缩放修复-阶段2");
            Undo.RecordObject(data.gameObject.transform, "缩放修复-Scale补偿");

            data.gameObject.transform.localScale = newScale;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            EditorUtility.SetDirty(ps);
            EditorUtility.SetDirty(data.gameObject);

            sfPhase2Count++;
            scaleFixResults.Add(new ScaleFixEntry
            {
                gameObject = data.gameObject,
                originalMode = ParticleSystemScalingMode.Local,
                rendererEnabled = true,
                scaleAdjusted = true,
                originalScale = originalScale,
                newScale = newScale,
                status = ScaleFixStatus.Fixed,
                statusReason = "Local→Hierarchy + Scale补偿"
            });
        }

        /// <summary>
        /// 计算父级累积缩放
        /// Calculate parent total scale
        /// </summary>
        private Vector3 ScaleFix_CalculateParentTotalScale(Transform trans)
        {
            Vector3 total = Vector3.one;
            Transform parent = trans.parent;

            while (parent != null)
            {
                total = new Vector3(
                    total.x * parent.localScale.x,
                    total.y * parent.localScale.y,
                    total.z * parent.localScale.z
                );
                parent = parent.parent;
            }

            return total;
        }

        /// <summary>
        /// 判断向量是否近似零
        /// Check if vector is approximately zero
        /// </summary>
        private bool ScaleFix_IsApproximatelyZero(Vector3 v)
        {
            return Mathf.Approximately(v.x, 0f) && Mathf.Approximately(v.y, 0f) && Mathf.Approximately(v.z, 0f);
        }

        #endregion
    }
}
