using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MoshiTools
{
    /// <summary>
    /// 原点对齐工具 — 将物体坐标原点移动到包围盒指定位置，视觉位置不变
    /// Pivot alignment tool — move object pivot to bounding box position without changing visual position
    /// 
    /// 使用方法：
    /// 在 Hierarchy 中右键物体 → Moshi/原点对齐/移到底部（或其他方向）
    /// </summary>
    public static class Moshi_PivotAlign
    {
        // 对齐方向枚举
        // Alignment direction enum
        public enum AlignDirection
        {
            Bottom,  // 底部
            Top,     // 顶部
            Center,  // 中心
            Left,    // 左侧
            Right,   // 右侧
            Front,   // 前方
            Back     // 后方
        }

        #region 菜单项 MenuItem

        [MenuItem("GameObject/Moshi/原点对齐/移到底部", false, 10)]
        private static void PivotToBottom()
        {
            AlignSelectedObjects(AlignDirection.Bottom);
        }

        [MenuItem("GameObject/Moshi/原点对齐/移到顶部", false, 11)]
        private static void PivotToTop()
        {
            AlignSelectedObjects(AlignDirection.Top);
        }

        [MenuItem("GameObject/Moshi/原点对齐/移到中心", false, 12)]
        private static void PivotToCenter()
        {
            AlignSelectedObjects(AlignDirection.Center);
        }

        [MenuItem("GameObject/Moshi/原点对齐/移到左侧", false, 20)]
        private static void PivotToLeft()
        {
            AlignSelectedObjects(AlignDirection.Left);
        }

        [MenuItem("GameObject/Moshi/原点对齐/移到右侧", false, 21)]
        private static void PivotToRight()
        {
            AlignSelectedObjects(AlignDirection.Right);
        }

        [MenuItem("GameObject/Moshi/原点对齐/移到前方", false, 22)]
        private static void PivotToFront()
        {
            AlignSelectedObjects(AlignDirection.Front);
        }

        [MenuItem("GameObject/Moshi/原点对齐/移到后方", false, 23)]
        private static void PivotToBack()
        {
            AlignSelectedObjects(AlignDirection.Back);
        }

        // 验证：必须有选中物体
        // Validate: must have selected objects
        [MenuItem("GameObject/Moshi/原点对齐/移到底部", true)]
        [MenuItem("GameObject/Moshi/原点对齐/移到顶部", true)]
        [MenuItem("GameObject/Moshi/原点对齐/移到中心", true)]
        [MenuItem("GameObject/Moshi/原点对齐/移到左侧", true)]
        [MenuItem("GameObject/Moshi/原点对齐/移到右侧", true)]
        [MenuItem("GameObject/Moshi/原点对齐/移到前方", true)]
        [MenuItem("GameObject/Moshi/原点对齐/移到后方", true)]
        private static bool ValidatePivotAlign()
        {
            return Selection.activeGameObject != null;
        }

        #endregion

        #region 核心逻辑 Core Logic

        /// <summary>
        /// 对所有选中物体执行原点对齐
        /// Align pivot for all selected objects
        /// </summary>
        private static void AlignSelectedObjects(AlignDirection direction)
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0) return;

            string dirName = GetDirectionName(direction);
            int successCount = 0;

            Undo.SetCurrentGroupName($"原点{dirName}");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (var go in selected)
            {
                if (go == null) continue;
                if (AlignPivot(go, direction))
                {
                    successCount++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (successCount > 0)
            {
                Debug.Log($"[Moshi_PivotAlign] 已将 {successCount} 个物体的原点{dirName}");
            }
        }

        /// <summary>
        /// 对单个物体执行原点对齐
        /// Align pivot for a single object
        /// </summary>
        /// <returns>是否成功</returns>
        private static bool AlignPivot(GameObject go, AlignDirection direction)
        {
            if (go == null) return false;

            // 1. 计算总包围盒（世界空间）
            // Calculate total bounding box (world space)
            Bounds totalBounds;
            if (!CalculateTotalBounds(go, out totalBounds))
            {
                Debug.LogWarning($"[Moshi_PivotAlign] [{go.name}] 没有可渲染组件，无法计算包围盒");
                return false;
            }

            // 2. 根据方向计算目标点（世界空间）
            // Calculate target position based on direction (world space)
            Vector3 currentPos = go.transform.position;
            Vector3 targetPos = CalculateTargetPosition(currentPos, totalBounds, direction);

            // 3. 如果目标点和当前位置相同，无需操作
            // Skip if target is same as current
            if (Vector3.Distance(currentPos, targetPos) < 0.0001f)
            {
                Debug.Log($"[Moshi_PivotAlign] [{go.name}] 原点已在目标位置，无需调整");
                return true;
            }

            // 4. 检查是否有子物体
            // Check if object has children
            bool hasChildren = go.transform.childCount > 0;

            if (!hasChildren)
            {
                // 无子物体：先用 Moshi_AddParent 包裹一层父节点，然后对父节点操作
                // No children: wrap with parent first, then align the parent
                GameObject parent = Moshi_AddParent.AddParentToGameObject(go);
                if (parent == null)
                {
                    Debug.LogWarning($"[Moshi_PivotAlign] [{go.name}] 创建父物体失败");
                    return false;
                }

                // 重新计算包围盒（因为层级变了）
                // Recalculate bounds (hierarchy changed)
                if (!CalculateTotalBounds(parent, out totalBounds))
                {
                    return false;
                }

                // 现在 parent 有子物体了，对 parent 执行对齐
                // Now parent has children, align parent
                currentPos = parent.transform.position;
                targetPos = CalculateTargetPosition(currentPos, totalBounds, direction);

                return DoAlignWithChildren(parent, targetPos);
            }
            else
            {
                // 有子物体：直接移动父物体 + 反向偏移子物体
                // Has children: move parent + offset children inversely
                return DoAlignWithChildren(go, targetPos);
            }
        }

        /// <summary>
        /// 执行有子物体情况的对齐：移动父物体到目标位置，子物体保持世界坐标不变
        /// Perform alignment with children: move parent to target, keep children's world position
        /// </summary>
        private static bool DoAlignWithChildren(GameObject parent, Vector3 targetPos)
        {
            Transform parentTransform = parent.transform;

            // 记录所有直接子物体的世界坐标
            // Record world positions of all direct children
            int childCount = parentTransform.childCount;
            Vector3[] childWorldPositions = new Vector3[childCount];
            Quaternion[] childWorldRotations = new Quaternion[childCount];
            Vector3[] childLossyScales = new Vector3[childCount];

            for (int i = 0; i < childCount; i++)
            {
                Transform child = parentTransform.GetChild(i);
                childWorldPositions[i] = child.position;
                childWorldRotations[i] = child.rotation;
                childLossyScales[i] = child.lossyScale;
            }

            // 记录 Undo
            // Record Undo for parent
            Undo.RecordObject(parentTransform, "移动原点");

            // 移动父物体到目标位置
            // Move parent to target position
            parentTransform.position = targetPos;

            // 恢复所有子物体的世界坐标（视觉位置不变）
            // Restore children's world positions (visual position unchanged)
            for (int i = 0; i < childCount; i++)
            {
                Transform child = parentTransform.GetChild(i);
                Undo.RecordObject(child, "恢复子物体位置");

                child.position = childWorldPositions[i];
                child.rotation = childWorldRotations[i];

                // 尽量还原缩放
                // Try to restore scale
                Vector3 parentLossy = parentTransform.lossyScale;
                if (Mathf.Abs(parentLossy.x) > 1e-6f &&
                    Mathf.Abs(parentLossy.y) > 1e-6f &&
                    Mathf.Abs(parentLossy.z) > 1e-6f)
                {
                    child.localScale = new Vector3(
                        childLossyScales[i].x / parentLossy.x,
                        childLossyScales[i].y / parentLossy.y,
                        childLossyScales[i].z / parentLossy.z
                    );
                }
            }

            return true;
        }

        #endregion

        #region 包围盒计算 Bounds Calculation

        /// <summary>
        /// 计算物体及所有子物体的世界空间总包围盒
        /// Calculate world-space total bounding box of object and all descendants
        /// </summary>
        /// <returns>是否找到有效的 Renderer</returns>
        private static bool CalculateTotalBounds(GameObject go, out Bounds totalBounds)
        {
            totalBounds = new Bounds();
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);

            if (renderers == null || renderers.Length == 0)
            {
                return false;
            }

            bool first = true;
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                // 跳过禁用的 Renderer
                // Skip disabled renderers
                if (!renderer.enabled) continue;

                if (first)
                {
                    totalBounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    totalBounds.Encapsulate(renderer.bounds);
                }
            }

            // 如果所有 Renderer 都被禁用，尝试包含禁用的
            // If all renderers are disabled, try including disabled ones
            if (first)
            {
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    if (first)
                    {
                        totalBounds = renderer.bounds;
                        first = false;
                    }
                    else
                    {
                        totalBounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            return !first;
        }

        #endregion

        #region 工具方法 Utility

        /// <summary>
        /// 根据对齐方向计算目标位置（世界空间）
        /// Calculate target position based on alignment direction (world space)
        /// 
        /// 规则：
        /// - 底部/顶部：只改 Y，X/Z 不变
        /// - 左侧/右侧：只改 X，Y/Z 不变
        /// - 前方/后方：只改 Z，X/Y 不变
        /// - 中心：X/Y/Z 都移到包围盒中心
        /// </summary>
        private static Vector3 CalculateTargetPosition(Vector3 currentPos, Bounds bounds, AlignDirection direction)
        {
            switch (direction)
            {
                case AlignDirection.Bottom:
                    return new Vector3(currentPos.x, bounds.min.y, currentPos.z);

                case AlignDirection.Top:
                    return new Vector3(currentPos.x, bounds.max.y, currentPos.z);

                case AlignDirection.Center:
                    return bounds.center;

                case AlignDirection.Left:
                    return new Vector3(bounds.min.x, currentPos.y, currentPos.z);

                case AlignDirection.Right:
                    return new Vector3(bounds.max.x, currentPos.y, currentPos.z);

                case AlignDirection.Front:
                    return new Vector3(currentPos.x, currentPos.y, bounds.max.z);

                case AlignDirection.Back:
                    return new Vector3(currentPos.x, currentPos.y, bounds.min.z);

                default:
                    return currentPos;
            }
        }

        /// <summary>
        /// 获取方向的中文名称
        /// Get direction display name in Chinese
        /// </summary>
        private static string GetDirectionName(AlignDirection direction)
        {
            switch (direction)
            {
                case AlignDirection.Bottom: return "移到底部";
                case AlignDirection.Top:    return "移到顶部";
                case AlignDirection.Center: return "移到中心";
                case AlignDirection.Left:   return "移到左侧";
                case AlignDirection.Right:  return "移到右侧";
                case AlignDirection.Front:  return "移到前方";
                case AlignDirection.Back:   return "移到后方";
                default:                    return "对齐";
            }
        }

        #endregion
    }
}
