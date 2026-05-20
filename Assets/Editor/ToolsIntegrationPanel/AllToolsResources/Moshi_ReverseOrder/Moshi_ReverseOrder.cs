using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MoshiTools
{
    // 反转排序工具
    // Reverse sibling order tool
    //
    // 菜单列出所有判断模式：
    // 1. 反转子物体       — 反转当前选中物体的所有子物体顺序
    // 2. 反转选中同级     — 仅反转多选物体的相对顺序（不影响未选中兄弟）
    // 3. 智能反转         — 自动判断：单选→反转子物体，多选→反转同级
    //
    // 重要说明（Unity 特性踩坑）：
    // [MenuItem("GameObject/...")] 会被 Unity 对每个选中物体逐一调用，
    // 也就是选中 N 个物体时，函数会被调 N 次。为避免重复反转，
    // 使用 EditorApplication.delayCall + 帧内去重标记，只执行一次。
    public static class Moshi_ReverseOrder
    {
        private const string MENU_ROOT = "GameObject/Moshi/反转排序/";

        // 去重标记：同一帧内只执行一次真正的反转操作
        // Deduplication flag: ensure the action runs only once per frame
        private static int _lastExecFrame = -1;

        // ================= 模式 1：反转子物体 =================
        // Mode 1: Reverse children of the selected object(s)

        [MenuItem(MENU_ROOT + "反转子物体", false, 10)]
        private static void ReverseChildren()
        {
            if (!AcquireExecSlot()) return;
            EditorApplication.delayCall += DoReverseChildren;
        }

        [MenuItem(MENU_ROOT + "反转子物体", true)]
        private static bool ValidateReverseChildren()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        private static void DoReverseChildren()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog("反转排序", "请先在 Hierarchy 中选中物体", "确定");
                return;
            }

            // 开启新组，并记录"进入之前"的最新组号
            // Open new group; remember the group number BEFORE our work starts
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Moshi 反转子物体");

            int total = 0;
            int processedParents = 0;

            foreach (var go in selected)
            {
                if (go == null) continue;
                Transform parent = go.transform;
                if (parent.childCount < 2) continue;

                List<Transform> children = new List<Transform>();
                for (int i = 0; i < parent.childCount; i++)
                {
                    children.Add(parent.GetChild(i));
                }
                ReverseSiblings(children);
                total += children.Count;
                processedParents++;
            }

            // Collapse：将所有 >= undoGroup 的组合并成一个撤销步骤
            // 传入参数是"目标组号"，Unity 会把更高编号的组并入该组
            Undo.CollapseUndoOperations(undoGroup);

            if (processedParents == 0)
            {
                EditorUtility.DisplayDialog("反转排序",
                    "所选物体均没有足够的子物体（少于 2 个），无法反转", "确定");
                return;
            }

            Debug.Log($"[Moshi_ReverseOrder] 反转子物体：共处理 {processedParents} 个父物体，{total} 个子物体");
        }

        // ================= 模式 2：反转选中同级 =================
        // Mode 2: Reverse order of selected siblings only
        //
        // 注意：为避免 Unity 把此菜单项隐藏（当选中数 < 2 时会隐藏）
        // Validate 始终返回 true，在执行函数中再检查数量并弹窗提示
        // Always show this menu item; check count in the action itself

        [MenuItem(MENU_ROOT + "反转选中同级", false, 11)]
        private static void ReverseSelectedSiblings()
        {
            if (!AcquireExecSlot()) return;
            EditorApplication.delayCall += DoReverseSelectedSiblings;
        }

        [MenuItem(MENU_ROOT + "反转选中同级", true)]
        private static bool ValidateReverseSelectedSiblings()
        {
            // 始终显示该菜单项（数量不足时在执行时弹窗提示）
            // Always visible; numeric check happens inside the action
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        private static void DoReverseSelectedSiblings()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected == null || selected.Length < 2)
            {
                EditorUtility.DisplayDialog("反转排序", "请至少选中 2 个同级物体", "确定");
                return;
            }

            // 关键：必须先 Increment 开启新组，GetCurrentGroup 才会返回"这次操作"的新组号
            // Critical: IncrementCurrentGroup FIRST to open a fresh Undo group
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Moshi 反转选中同级");

            // 按父物体分组（跨父级自动分组处理）
            // Group by parent (auto-handle cross-parent selection)
            var groups = selected
                .Where(go => go != null)
                .Select(go => go.transform)
                .GroupBy(t => t.parent);

            int total = 0;
            int groupCount = 0;

            foreach (var group in groups)
            {
                List<Transform> siblings = group
                    .OrderBy(t => t.GetSiblingIndex())
                    .ToList();

                if (siblings.Count < 2) continue;

                ReverseSiblings(siblings);
                total += siblings.Count;
                groupCount++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (total == 0)
            {
                EditorUtility.DisplayDialog("反转排序",
                    "选中的物体没有 2 个或以上处于同一父级下，无法反转", "确定");
                return;
            }

            Debug.Log($"[Moshi_ReverseOrder] 反转选中同级：{groupCount} 组，共 {total} 个物体");
        }

        // ================= 模式 3：智能反转 =================
        // Mode 3: Smart reverse (auto-detect by selection count)

        [MenuItem(MENU_ROOT + "智能反转", false, 12)]
        private static void SmartReverse()
        {
            if (!AcquireExecSlot()) return;
            EditorApplication.delayCall += DoSmartReverse;
        }

        [MenuItem(MENU_ROOT + "智能反转", true)]
        private static bool ValidateSmartReverse()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        private static void DoSmartReverse()
        {
            // 注意：直接调用 DoReverseChildren/DoReverseSelectedSiblings 会造成
            //      Undo.IncrementCurrentGroup 嵌套 + 两次 Collapse，导致合并失败
            //      这里只做选择分发，不重复开 Undo 组
            // NOTE: do NOT delegate to DoXxx (would nest Undo groups)
            GameObject[] selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog("反转排序", "请先在 Hierarchy 中选中物体", "确定");
                return;
            }

            if (selected.Length == 1)
            {
                DoReverseChildren();
            }
            else
            {
                DoReverseSelectedSiblings();
            }
        }

        // ================= 去重锁 =================

        // 判断是否可以执行本次操作（同一帧内多次触发只执行一次）
        // Returns true only for the first call in the current frame
        private static bool AcquireExecSlot()
        {
            int currentFrame = Time.frameCount;
            if (currentFrame == _lastExecFrame)
            {
                return false;
            }
            _lastExecFrame = currentFrame;
            return true;
        }

        // ================= 核心算法 =================

        // 反转一组兄弟物体的排序（占位重排算法）
        // Reverse a group of sibling transforms by slot-reassignment
        //
        // 算法说明（核心）：
        // 1. 记录每个选中兄弟当前占据的"槽位"（siblingIndex），例如 [1, 3, 5]
        // 2. 将这些槽位按升序排列，作为"目标槽位池"
        // 3. 反转 siblings 顺序后，逐个把每个物体放入"目标槽位池"中对应位置
        //    即：最后一个物体 → 最小槽位，倒数第二 → 次小槽位 ...
        //
        // 关键点：从"小槽位"开始依次 SetSiblingIndex，能精准落位且不干扰未选中兄弟
        // 对"不连续多选"稳定工作（例如选中 idx[1,3,5] 会变成 [原5,原3,原1]）
        private static void ReverseSiblings(List<Transform> siblings)
        {
            int n = siblings.Count;
            if (n < 2) return;

            // 注册 Undo（记录完整父级层级结构，包含 siblingIndex 变化）
            // Register Undo using RegisterFullObjectHierarchyUndo — this is the ONLY
            // Undo API that captures siblingIndex changes reliably.
            //
            // 为什么不用 RegisterCompleteObjectUndo？
            //   它只记录单个 GameObject 自身组件数据，不包含它在父级中的排序位置。
            //   SetSiblingIndex 的变化 → 必须通过父级的"完整层级"快照才能回滚。
            Transform parent = siblings[0].parent;
            if (parent != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(parent.gameObject, "Reverse Sibling Order");
            }
            else
            {
                // 如果 siblings 是场景根物体，需要逐个注册每个根的 hierarchy
                // Root objects in scene: register each one's hierarchy
                foreach (var t in siblings)
                {
                    Undo.RegisterFullObjectHierarchyUndo(t.gameObject, "Reverse Sibling Order");
                }
            }

            // 1. 采集原始槽位（升序）
            // Collect original slot indices (ascending)
            List<int> targetSlots = siblings
                .Select(t => t.GetSiblingIndex())
                .OrderBy(i => i)
                .ToList();

            // 2. 反转 siblings 顺序
            // Reverse the sibling list (this is the desired new order)
            List<Transform> reversed = new List<Transform>(siblings);
            reversed.Reverse();

            // 3. 依次放置：reversed[0] → targetSlots[0], reversed[1] → targetSlots[1] ...
            //    从小到大设置，避免后设的物体影响先设物体的索引
            // Apply slot by slot, from smallest to largest index
            for (int i = 0; i < n; i++)
            {
                reversed[i].SetSiblingIndex(targetSlots[i]);
            }

            // 只对父级 SetDirty 一次，避免反复污染 Undo 栈
            // Mark dirty only once on parent (not per-child) to keep undo clean
            if (parent != null)
            {
                EditorUtility.SetDirty(parent.gameObject);
            }
        }
    }
}
