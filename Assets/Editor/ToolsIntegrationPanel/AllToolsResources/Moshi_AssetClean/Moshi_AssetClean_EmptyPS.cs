using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MoshiTools
{
    /// <summary>
    /// 资源清理工具 - 空粒子清理模块
    /// Asset Cleaner - Empty particle system cleanup module
    /// 检测粒子系统的 Renderer 和 Sub Emitters 模块是否开启，
    /// 两者都未开启则视为空粒子，可一键删除。
    /// </summary>
    public partial class Moshi_AssetClean
    {
        #region 空粒子数据结构

        /// <summary>
        /// 空粒子检测状态
        /// Empty PS detection status
        /// </summary>
        private enum EmptyPSStatus
        {
            Empty,          // 空粒子（Renderer和SubEmitters都未开启）
            HasRenderer,    // 开启了Renderer
            HasSubEmitters, // 开启了SubEmitters
            HasBoth         // 两者都开启了
        }

        /// <summary>
        /// 空粒子检测条目
        /// Empty PS entry
        /// </summary>
        [Serializable]
        private class EmptyPSEntry
        {
            public GameObject gameObject;           // 粒子所在物体
            public ParticleSystem particleSystem;   // 粒子系统引用
            public string objectPath;               // Hierarchy路径
            public EmptyPSStatus status;            // 检测状态
            public bool rendererEnabled;            // Renderer模块是否启用
            public bool subEmittersEnabled;         // SubEmitters模块是否启用
            public bool selected;                   // 是否选中（用于删除）
        }



        #endregion

        #region 空粒子字段

        // 扫描结果
        // Scan results
        private List<EmptyPSEntry> emptyPSResults = new List<EmptyPSEntry>();
        private bool emptyPSHasScanned = false;

        // 统计
        // Statistics
        private int epsTotalScanned = 0;        // 总扫描粒子数
        private int epsEmptyCount = 0;          // 空粒子数
        private int epsHasRendererCount = 0;    // 有Renderer的数量
        private int epsHasSubEmitCount = 0;     // 有SubEmitters的数量
        private int epsHasBothCount = 0;        // 两者都有的数量

        // UI状态
        // UI state
        private Vector2 emptyPSScrollPos;
        private bool epsSelectAll = true;           // 全选开关
        private bool epsShowSafeItems = false;      // 是否显示安全项（有Renderer/SubEmitters的）

        // 自动处理SubEmitters统计
        // Auto-processed SubEmitters statistics
        private int autoDisabledSubEmitCount = 0;   // 扫描时自动关闭全空SubEmitters的数量
        private int autoCleanedNullSubEmitCount = 0; // 扫描时自动清理部分空引用SubEmitters的数量

        #endregion

        #region 初始化

        partial void InitEmptyPSModule()
        {
            // 初始化完成
            // Initialization complete
        }

        #endregion

        #region 空粒子标签页绘制

        /// <summary>
        /// 绘制空粒子清理标签页
        /// Draw empty particle system cleanup tab
        /// </summary>
        private void DrawEmptyPSTab()
        {
            // 说明区域
            // Description area
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("功能说明", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("检测粒子系统的 Renderer 和 Sub Emitters 模块：", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("• Renderer 和 Sub Emitters 都未开启 → 标记为空粒子，可删除", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• 任意一个或两者都开启 → 保留", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Sub Emitters 中引用全空 → 自动关闭模块", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Sub Emitters 中部分引用为空 → 自动清理空引用", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• 空粒子会增加DrawCall和内存开销，建议定期清理", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("💡 提示：删除操作支持撤销（Ctrl+Z）", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 扫描按钮
            // Scan button
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            if (GUILayout.Button("开始扫描", GUILayout.Height(30)))
            {
                ExecuteEmptyPSScan();
            }
            GUI.backgroundColor = originalColor;

            EditorGUILayout.Space(5);

            // 结果显示
            // Results display
            DrawEmptyPSResults();

        }

        /// <summary>
        /// 绘制空粒子检测结果
        /// Draw empty PS results
        /// </summary>
        private void DrawEmptyPSResults()
        {
            if (!emptyPSHasScanned)
            {
                EditorGUILayout.HelpBox("点击\"开始扫描\"按钮开始检测", MessageType.Info);
                return;
            }

            if (emptyPSResults.Count == 0)
            {
                EditorGUILayout.HelpBox("未找到任何粒子系统", MessageType.Info);
                return;
            }

            // 统计信息
            // Statistics
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("扫描结果", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  总扫描粒子系统: {epsTotalScanned}", EditorStyles.miniLabel);

            if (epsEmptyCount > 0)
                EditorGUILayout.LabelField($"  ⚠ 空粒子（可删除）: {epsEmptyCount}", EditorStyles.miniLabel);
            else
                EditorGUILayout.LabelField($"  ✅ 未发现空粒子", EditorStyles.miniLabel);

            if (epsHasRendererCount > 0)
                EditorGUILayout.LabelField($"  ✅ 有Renderer: {epsHasRendererCount}", EditorStyles.miniLabel);
            if (epsHasSubEmitCount > 0)
                EditorGUILayout.LabelField($"  ✅ 有SubEmitters: {epsHasSubEmitCount}", EditorStyles.miniLabel);
            if (epsHasBothCount > 0)
                EditorGUILayout.LabelField($"  ✅ 两者都有: {epsHasBothCount}", EditorStyles.miniLabel);

            if (autoDisabledSubEmitCount > 0)
                EditorGUILayout.LabelField($"  🔧 已自动关闭全空SubEmitters: {autoDisabledSubEmitCount}个（支持Undo撤销）", EditorStyles.miniLabel);
            if (autoCleanedNullSubEmitCount > 0)
                EditorGUILayout.LabelField($"  🔧 已自动清理部分空引用SubEmitters: {autoCleanedNullSubEmitCount}个（支持Undo撤销）", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(3);

            // 操作按钮行
            // Action buttons row
            if (epsEmptyCount > 0)
            {
                EditorGUILayout.BeginHorizontal();

                // 全选/取消全选
                // Select all / deselect all
                if (GUILayout.Button(epsSelectAll ? "取消全选" : "全选空粒子", EditorStyles.miniButtonLeft, GUILayout.Width(80)))
                {
                    epsSelectAll = !epsSelectAll;
                    foreach (var entry in emptyPSResults)
                    {
                        if (entry.status == EmptyPSStatus.Empty)
                            entry.selected = epsSelectAll;
                    }
                }

                // 一键选中（Hierarchy中高亮）
                // Select in hierarchy
                if (GUILayout.Button("定位选中", EditorStyles.miniButtonMid, GUILayout.Width(80)))
                {
                    EmptyPS_SelectInHierarchy();
                }

                // 删除按钮
                // Delete button
                var originalBgColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                int selectedCount = EmptyPS_GetSelectedCount();
                if (GUILayout.Button($"删除选中 ({selectedCount})", EditorStyles.miniButtonRight, GUILayout.Width(100)))
                {
                    if (selectedCount > 0)
                    {
                        EmptyPS_DeleteSelected();
                    }
                }
                GUI.backgroundColor = originalBgColor;

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(3);

            // 显示安全项开关
            // Toggle safe items display
            epsShowSafeItems = EditorGUILayout.ToggleLeft("显示安全项（有Renderer/SubEmitters的粒子）", epsShowSafeItems);

            EditorGUILayout.Space(3);

            // 结果列表
            // Result list
            emptyPSScrollPos = EditorGUILayout.BeginScrollView(emptyPSScrollPos);

            foreach (var entry in emptyPSResults)
            {
                // 跳过安全项（如果未勾选显示）
                // Skip safe items if not showing
                if (!epsShowSafeItems && entry.status != EmptyPSStatus.Empty)
                    continue;

                DrawEmptyPSResultItem(entry);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制单条空粒子结果行
        /// Draw single empty PS result row
        /// </summary>
        private void DrawEmptyPSResultItem(EmptyPSEntry entry)
        {
            if (entry.gameObject == null) return;

            var originalColor = GUI.backgroundColor;

            EditorGUILayout.BeginHorizontal();

            // 1. 勾选框（仅空粒子显示）
            // Checkbox (only for empty PS)
            if (entry.status == EmptyPSStatus.Empty)
            {
                entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(18));
            }
            else
            {
                GUILayout.Space(22);
            }

            // 2. GameObject引用
            // GameObject reference
            EditorGUILayout.ObjectField("", entry.gameObject, typeof(GameObject), true, GUILayout.Width(150));

            // 3. 状态标签
            // Status label
            string statusText;
            Color statusColor;
            switch (entry.status)
            {
                case EmptyPSStatus.Empty:
                    statusText = "⚠ 空粒子";
                    statusColor = new Color(1f, 0.8f, 0.5f);
                    break;
                case EmptyPSStatus.HasRenderer:
                    statusText = "✅ 有Renderer";
                    statusColor = new Color(0.7f, 1f, 0.7f);
                    break;
                case EmptyPSStatus.HasSubEmitters:
                    statusText = "✅ 有SubEmit";
                    statusColor = new Color(0.7f, 0.9f, 1f);
                    break;
                case EmptyPSStatus.HasBoth:
                    statusText = "✅ 两者都有";
                    statusColor = new Color(0.8f, 1f, 0.8f);
                    break;
                default:
                    statusText = "未知";
                    statusColor = Color.white;
                    break;
            }

            GUI.backgroundColor = statusColor;
            EditorGUILayout.LabelField(statusText, EditorStyles.miniButton, GUILayout.Width(100));
            GUI.backgroundColor = originalColor;

            // 4. 详情：Renderer/SubEmitters状态
            // Detail: Renderer/SubEmitters status
            string detail = $"Renderer:{(entry.rendererEnabled ? "✓" : "✗")}  SubEmit:{(entry.subEmittersEnabled ? "✓" : "✗")}";
            EditorGUILayout.LabelField(detail, EditorStyles.miniLabel, GUILayout.MinWidth(160));

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region 空粒子扫描逻辑

        /// <summary>
        /// 执行空粒子扫描
        /// Execute empty PS scan
        /// </summary>
        private void ExecuteEmptyPSScan()
        {
            ClearEmptyPSResults();
            emptyPSHasScanned = true;
            autoDisabledSubEmitCount = 0;
            autoCleanedNullSubEmitCount = 0;

            var targets = GetTargetGameObjects();
            if (targets == null)
            {
                emptyPSHasScanned = false;
                return;
            }

            Undo.SetCurrentGroupName("扫描并自动处理SubEmitters空引用");
            int undoGroup = Undo.GetCurrentGroup();

            int total = targets.Length;

            for (int i = 0; i < total; i++)
            {
                var target = targets[i];
                if (target == null) continue;

                // 进度条
                // Progress bar
                if (total > 1 || scanScope == ScanScope.Prefab)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(TOOL_NAME,
                        $"正在扫描空粒子: {target.name} ({i + 1}/{total})",
                        (float)i / total))
                    {
                        break;
                    }
                }

                EmptyPS_ScanGameObject(target);
            }

            if (total > 1 || scanScope == ScanScope.Prefab)
                EditorUtility.ClearProgressBar();

            Undo.CollapseUndoOperations(undoGroup);

            // 更新统计
            // Update statistics
            EmptyPS_UpdateStatistics();

            string logMsg = $"[{TOOL_NAME}] 空粒子扫描完成 - 总计:{epsTotalScanned} 空粒子:{epsEmptyCount} 有Renderer:{epsHasRendererCount} 有SubEmit:{epsHasSubEmitCount} 两者都有:{epsHasBothCount}";
            if (autoDisabledSubEmitCount > 0)
                logMsg += $" | 自动关闭全空SubEmitters:{autoDisabledSubEmitCount}个";
            if (autoCleanedNullSubEmitCount > 0)
                logMsg += $" | 自动清理部分空引用SubEmitters:{autoCleanedNullSubEmitCount}个";
            Debug.Log(logMsg);
        }

        /// <summary>
        /// 扫描一个GameObject及其所有子物体的粒子系统
        /// Scan a GameObject and all children for particle systems
        /// </summary>
        private void EmptyPS_ScanGameObject(GameObject root)
        {
            if (root == null) return;

            var allPS = root.GetComponentsInChildren<ParticleSystem>(true);

            foreach (var ps in allPS)
            {
                if (ps == null) continue;

                // 检测Renderer模块是否启用
                // Check if Renderer module is enabled
                var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
                bool rendererEnabled = psRenderer != null && psRenderer.enabled;

                // 检测Sub Emitters模块状态
                // Check Sub Emitters module status
                bool subEmittersEnabled = ps.subEmitters.enabled;
                bool subEmittersHasValidRef = false;
                int subNullCount = 0;
                int subTotalCount = 0;

                if (subEmittersEnabled)
                {
                    subTotalCount = ps.subEmitters.subEmittersCount;
                    for (int si = 0; si < subTotalCount; si++)
                    {
                        if (ps.subEmitters.GetSubEmitterSystem(si) != null)
                            subEmittersHasValidRef = true;
                        else
                            subNullCount++;
                    }

                    // 全部引用为空 → 直接自动关闭SubEmitters模块（无需用户确认）
                    // All refs are null → auto-disable SubEmitters module (no confirmation needed)
                    if (subTotalCount > 0 && !subEmittersHasValidRef)
                    {
                        // 使用 SerializedObject 安全移除，避免 RemoveSubEmitter 的越界 bug
                        // Use SerializedObject to safely remove, avoiding RemoveSubEmitter IndexOutOfRange bug
                        RemoveNullSubEmittersViaSO(ps, true);

                        subEmittersEnabled = false;
                        autoDisabledSubEmitCount++;

                        Debug.Log($"[{TOOL_NAME}] 自动关闭 SubEmitters: {GetHierarchyPath(ps.gameObject)} (全部 {subTotalCount} 个引用为空)");
                    }
                    // 部分引用为空 → 直接自动清理空引用，保留有效引用（无需用户确认）
                    // Partial null refs → auto-clean null refs, keep valid ones (no confirmation needed)
                    else if (subNullCount > 0)
                    {
                        // 使用 SerializedObject 安全移除空引用
                        // Use SerializedObject to safely remove null refs
                        RemoveNullSubEmittersViaSO(ps, false);
                        autoCleanedNullSubEmitCount++;

                        Debug.Log($"[{TOOL_NAME}] 自动清理空引用 SubEmitters: {GetHierarchyPath(ps.gameObject)} (清理 {subNullCount} 个空引用，保留 {subTotalCount - subNullCount} 个有效引用)");
                    }
                }

                // 清理操作后，重新验证 SubEmitters 状态
                // After cleanup, re-verify SubEmitters state
                subEmittersEnabled = ps.subEmitters.enabled;

                // 如果模块仍开启，检查是否全部引用为空
                // If module still enabled, check if all refs are null
                if (subEmittersEnabled)
                {
                    bool hasValid = false;
                    int currentCount = ps.subEmitters.subEmittersCount;
                    for (int si = 0; si < currentCount; si++)
                    {
                        if (ps.subEmitters.GetSubEmitterSystem(si) != null)
                        {
                            hasValid = true;
                            break;
                        }
                    }

                    if (!hasValid)
                    {
                        // 模块仍开启但没有任何有效引用 → 强制再次尝试关闭
                        // Module still enabled but no valid refs → force retry disable
                        try
                        {
                            var subMod = ps.subEmitters;
                            subMod.enabled = false;
                            EditorUtility.SetDirty(ps);
                        }
                        catch (System.Exception) { }

                        // 重新读取状态
                        // Re-read state
                        subEmittersEnabled = ps.subEmitters.enabled;

                        // 如果API方式也失败，标记为无效（不影响删除判定）
                        // If API also failed, mark as invalid (won't affect deletion judgment)
                        if (subEmittersEnabled)
                            subEmittersEnabled = false;
                    }
                }

                // 判定状态（基于清理后的真实状态）
                // Determine status (based on actual state after cleanup)
                EmptyPSStatus status;
                if (rendererEnabled && subEmittersEnabled)
                    status = EmptyPSStatus.HasBoth;
                else if (rendererEnabled)
                    status = EmptyPSStatus.HasRenderer;
                else if (subEmittersEnabled)
                    status = EmptyPSStatus.HasSubEmitters;
                else
                    status = EmptyPSStatus.Empty;

                emptyPSResults.Add(new EmptyPSEntry
                {
                    gameObject = ps.gameObject,
                    particleSystem = ps,
                    objectPath = GetHierarchyPath(ps.gameObject),
                    status = status,
                    rendererEnabled = rendererEnabled,
                    subEmittersEnabled = subEmittersEnabled,
                    selected = (status == EmptyPSStatus.Empty)  // 空粒子默认选中
                });
            }
        }

        #endregion

        #region 空粒子辅助方法

        /// <summary>
        /// 清空空粒子结果
        /// Clear empty PS results
        /// </summary>
        private void ClearEmptyPSResults()
        {
            emptyPSResults.Clear();
            emptyPSHasScanned = false;
            epsTotalScanned = 0;
            epsEmptyCount = 0;
            epsHasRendererCount = 0;
            epsHasSubEmitCount = 0;
            epsHasBothCount = 0;
            epsSelectAll = true;

            // 清空SubEmitters自动处理统计
            // Clear SubEmitters auto-processing statistics
            autoDisabledSubEmitCount = 0;
            autoCleanedNullSubEmitCount = 0;
        }

        /// <summary>
        /// 更新空粒子统计数据
        /// Update empty PS statistics
        /// </summary>
        private void EmptyPS_UpdateStatistics()
        {
            epsTotalScanned = emptyPSResults.Count;
            epsEmptyCount = 0;
            epsHasRendererCount = 0;
            epsHasSubEmitCount = 0;
            epsHasBothCount = 0;

            foreach (var entry in emptyPSResults)
            {
                switch (entry.status)
                {
                    case EmptyPSStatus.Empty:
                        epsEmptyCount++;
                        break;
                    case EmptyPSStatus.HasRenderer:
                        epsHasRendererCount++;
                        break;
                    case EmptyPSStatus.HasSubEmitters:
                        epsHasSubEmitCount++;
                        break;
                    case EmptyPSStatus.HasBoth:
                        epsHasBothCount++;
                        break;
                }
            }
        }

        /// <summary>
        /// 获取当前选中的空粒子数量
        /// Get selected empty PS count
        /// </summary>
        private int EmptyPS_GetSelectedCount()
        {
            int count = 0;
            foreach (var entry in emptyPSResults)
            {
                if (entry.status == EmptyPSStatus.Empty && entry.selected)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 在Hierarchy中选中选中的空粒子物体
        /// Select checked empty PS objects in Hierarchy
        /// </summary>
        private void EmptyPS_SelectInHierarchy()
        {
            var objects = new List<GameObject>();
            foreach (var entry in emptyPSResults)
            {
                if (entry.status == EmptyPSStatus.Empty && entry.selected && entry.gameObject != null)
                {
                    objects.Add(entry.gameObject);
                }
            }

            if (objects.Count == 0)
            {
                Debug.LogWarning($"[{TOOL_NAME}] 没有选中任何空粒子");
                return;
            }

            Selection.objects = objects.ToArray();
            Debug.Log($"[{TOOL_NAME}] 已在Hierarchy中选中 {objects.Count} 个空粒子物体");
        }

        /// <summary>
        /// 删除选中的空粒子系统
        /// Delete selected empty particle systems
        /// </summary>
        private void EmptyPS_DeleteSelected()
        {
            // 收集待删除项
            // Collect items to delete
            var toDelete = new List<EmptyPSEntry>();
            foreach (var entry in emptyPSResults)
            {
                if (entry.status == EmptyPSStatus.Empty && entry.selected && entry.particleSystem != null)
                {
                    toDelete.Add(entry);
                }
            }

            if (toDelete.Count == 0)
            {
                EditorUtility.DisplayDialog(TOOL_NAME, "没有选中任何可删除的空粒子", "确定");
                return;
            }

            // 确认对话框
            // Confirmation dialog
            if (!EditorUtility.DisplayDialog(TOOL_NAME,
                $"确定要删除 {toDelete.Count} 个空粒子系统吗？\n\n" +
                "此操作支持 Undo 撤销。\n\n" +
                "注意：如果粒子系统是物体上唯一的组件，\n" +
                "将仅移除 ParticleSystem 组件（保留 GameObject）。",
                "确定删除", "取消"))
            {
                return;
            }

            Undo.SetCurrentGroupName("删除空粒子系统");
            int undoGroup = Undo.GetCurrentGroup();

            int deletedCount = 0;

            foreach (var entry in toDelete)
            {
                if (entry.particleSystem == null) continue;

                var go = entry.gameObject;
                var ps = entry.particleSystem;

                // 同时移除 ParticleSystemRenderer（如果存在）
                // Also remove ParticleSystemRenderer if exists
                var psRenderer = ps.GetComponent<ParticleSystemRenderer>();

                Undo.DestroyObjectImmediate(ps);

                if (psRenderer != null)
                {
                    Undo.DestroyObjectImmediate(psRenderer);
                }

                deletedCount++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"[{TOOL_NAME}] 已删除 {deletedCount} 个空粒子系统（支持Undo撤销）");

            // 从结果列表中移除已删除项
            // Remove deleted items from result list
            emptyPSResults.RemoveAll(e => e.status == EmptyPSStatus.Empty && e.selected);

            // 更新统计
            // Update statistics
            EmptyPS_UpdateStatistics();
        }

        /// <summary>
        /// 通过 SerializedObject 安全移除空引用的 SubEmitters
        /// Safely remove null SubEmitter references via SerializedObject
        /// 使用迭代器动态查找属性路径，兼容所有 Unity 版本
        /// Uses iterator to dynamically find property paths, compatible with all Unity versions
        /// </summary>
        /// <param name="ps">目标粒子系统</param>
        /// <param name="disableModule">是否同时关闭 SubEmitters 模块</param>
        private void RemoveNullSubEmittersViaSO(ParticleSystem ps, bool disableModule)
        {
            Undo.RecordObject(ps, disableModule ? "自动关闭全空SubEmitters" : "自动清理部分空引用SubEmitters");

            var so = new SerializedObject(ps);
            so.Update();

            // 通过迭代器查找 SubEmitters 模块的真实序列化路径
            // Find the real serialized property path of SubEmitters module via iterator
            SerializedProperty subModuleProp = FindSubEmittersModuleProperty(so);

            if (subModuleProp != null)
            {
                // 查找子发射器数组属性（遍历模块内所有子属性找到数组类型的）
                // Find sub-emitters array property (iterate module children to find array type)
                SerializedProperty subEmittersProp = FindSubEmittersArrayProperty(subModuleProp);

                if (subEmittersProp != null)
                {
                    // 从后往前移除空引用条目
                    // Remove null reference entries from back to front
                    for (int i = subEmittersProp.arraySize - 1; i >= 0; i--)
                    {
                        var element = subEmittersProp.GetArrayElementAtIndex(i);
                        // 查找该元素中的 ObjectReference 类型属性（即 emitter 引用）
                        // Find ObjectReference property in element (the emitter reference)
                        var emitterProp = FindEmitterRefProperty(element);
                        if (emitterProp != null && emitterProp.objectReferenceValue == null)
                        {
                            subEmittersProp.DeleteArrayElementAtIndex(i);
                        }
                    }
                }

                // 关闭模块
                // Disable module
                if (disableModule)
                {
                    var enabledProp = subModuleProp.FindPropertyRelative("enabled");
                    if (enabledProp != null)
                        enabledProp.boolValue = false;
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(ps);

                // 验证是否成功关闭
                // Verify if successfully disabled
                if (disableModule && ps.subEmitters.enabled)
                {
                    Debug.LogWarning($"[{TOOL_NAME}] SerializedObject 方式未能关闭 SubEmitters，尝试 Fallback...");
                }
                else
                {
                    return;
                }
            }

            // Fallback: SerializedObject 路径未找到或未生效时，用 try-catch 包裹 API 调用
            // Fallback: if SerializedObject path not found or ineffective, use try-catch wrapped API calls
            var subMod = ps.subEmitters;
            for (int si = subMod.subEmittersCount - 1; si >= 0; si--)
            {
                if (subMod.GetSubEmitterSystem(si) == null)
                {
                    try
                    {
                        subMod.RemoveSubEmitter(si);
                    }
                    catch (System.Exception)
                    {
                        // Unity bug: RemoveSubEmitter 在某些情况下越界，跳过
                        // Unity bug: RemoveSubEmitter may throw in some cases, skip
                    }
                }
            }

            if (disableModule)
            {
                subMod.enabled = false;
            }

            EditorUtility.SetDirty(ps);
        }

        /// <summary>
        /// 通过迭代器在 SerializedObject 中查找 SubEmitters 模块属性
        /// Find SubEmitters module property in SerializedObject via iterator
        /// </summary>
        private SerializedProperty FindSubEmittersModuleProperty(SerializedObject so)
        {
            var iter = so.GetIterator();
            if (!iter.Next(true)) return null;

            // 遍历所有顶级属性，找名称中包含 "Sub" 且有 "enabled" 子属性的模块
            // Iterate all top-level properties, find one with "Sub" in name and "enabled" child
            do
            {
                string propName = iter.name;
                // 粒子系统所有模块属性都以 "Module" 结尾或包含模块功能名
                // 只要名称包含 "Sub" 就可能是 SubEmitters 模块（SubModule / SubEmittersModule 等）
                // PS module properties typically end with "Module" or contain the function name
                // Any property containing "Sub" could be SubEmitters module
                if (propName.IndexOf("Sub", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // 验证这个属性内有 "enabled" 子属性（所有粒子系统模块都有 enabled）
                    // Verify it has "enabled" child property (all PS modules have enabled)
                    var enabledProp = iter.FindPropertyRelative("enabled");
                    if (enabledProp != null)
                    {
                        return so.FindProperty(propName);
                    }
                }
            } while (iter.Next(false));

            return null;
        }

        /// <summary>
        /// 在 SubEmitters 模块中查找子发射器数组属性
        /// Find sub-emitters array property within the SubEmitters module
        /// </summary>
        private SerializedProperty FindSubEmittersArrayProperty(SerializedProperty moduleProp)
        {
            var copy = moduleProp.Copy();
            if (!copy.Next(true)) return null;

            int depth = copy.depth;
            do
            {
                if (copy.depth <= moduleProp.depth) break;
                // 只看直接子属性中的数组
                // Only look at direct child arrays
                if (copy.depth == depth && copy.isArray && copy.propertyType == SerializedPropertyType.Generic)
                {
                    // 验证数组元素中是否包含 ObjectReference（即 ParticleSystem 引用）
                    // Verify array elements contain ObjectReference (i.e. ParticleSystem ref)
                    if (copy.arraySize > 0)
                    {
                        var firstElement = copy.GetArrayElementAtIndex(0);
                        var refProp = FindEmitterRefProperty(firstElement);
                        if (refProp != null)
                        {
                            return moduleProp.FindPropertyRelative(copy.name);
                        }
                    }
                    else
                    {
                        // 数组为空但名称包含 "emitter" 或 "sub"
                        // Empty array but name contains "emitter" or "sub"
                        string name = copy.name.ToLower();
                        if (name.Contains("emitter") || name.Contains("sub"))
                        {
                            return moduleProp.FindPropertyRelative(copy.name);
                        }
                    }
                }
            } while (copy.Next(false));

            return null;
        }

        /// <summary>
        /// 在子发射器数组元素中查找发射器引用属性（ObjectReference 类型，指向 ParticleSystem）
        /// Find emitter reference property in sub-emitter array element (ObjectReference type pointing to ParticleSystem)
        /// </summary>
        private SerializedProperty FindEmitterRefProperty(SerializedProperty element)
        {
            var copy = element.Copy();
            int baseDepth = copy.depth;
            if (!copy.Next(true)) return null;

            do
            {
                if (copy.depth <= baseDepth) break;
                // 查找 ObjectReference 类型的属性
                // Find ObjectReference type property
                if (copy.propertyType == SerializedPropertyType.ObjectReference)
                {
                    // 名称中包含 "emitter" 或 "system" 优先
                    // Prefer properties with "emitter" or "system" in name
                    string name = copy.name.ToLower();
                    if (name.Contains("emitter") || name.Contains("system"))
                    {
                        return element.FindPropertyRelative(copy.name);
                    }
                }
            } while (copy.Next(false));

            // 如果没找到名称匹配的，返回第一个 ObjectReference
            // If no name match found, return first ObjectReference
            copy = element.Copy();
            if (!copy.Next(true)) return null;
            do
            {
                if (copy.depth <= baseDepth) break;
                if (copy.propertyType == SerializedPropertyType.ObjectReference)
                {
                    return element.FindPropertyRelative(copy.name);
                }
            } while (copy.Next(false));

            return null;
        }

        #endregion
    }
}



