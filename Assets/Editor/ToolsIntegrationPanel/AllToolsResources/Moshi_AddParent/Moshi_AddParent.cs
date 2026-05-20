using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 层级工具集 - 添加父物体、替换为Prefab
/// 
/// 使用方法：
/// 在 Hierarchy 中右键物体 → Moshi/添加父物体
/// 在 Hierarchy 中右键物体 → Moshi/替换为Prefab
/// </summary>
public static class Moshi_AddParent
{
    #region 替换为Prefab功能
    
    /// <summary>
    /// 右键菜单：替换为Prefab
    /// </summary>
    [MenuItem("GameObject/Moshi/替换为Prefab", false, 1)]
    public static void ReplaceWithPrefab()
    {
        if (Selection.activeGameObject == null) return;
        
        // 打开选择Prefab的弹窗
        ReplaceWithPrefabWindow.ShowWindow(Selection.gameObjects);
    }
    
    [MenuItem("GameObject/Moshi/替换为Prefab", true)]
    public static bool ValidateReplaceWithPrefab()
    {
        return Selection.activeGameObject != null;
    }
    
    // 需要排除的基础组件类型（不迁移）
    // Base component types to skip when copying extra components
    private static readonly HashSet<System.Type> SkipComponentTypes = new HashSet<System.Type>
    {
        typeof(Transform),
        typeof(RectTransform),
        typeof(CanvasRenderer),
    };
    
    /// <summary>
    /// 执行替换操作
    /// Replace target objects with Prefab
    /// </summary>
    /// <param name="targets">被替换的原物体列表</param>
    /// <param name="prefab">目标Prefab资源</param>
    /// <param name="keepChildren">是否保留原物体的子物体</param>
    /// <param name="useOriginalName">是否沿用原物体名称（false则使用Prefab名称）</param>
    /// <param name="keepComponents">是否保留原物体上的额外组件（如JSBehaviour等）</param>
    public static void DoReplaceWithPrefab(GameObject[] targets, GameObject prefab,
                                            bool keepChildren = true, bool useOriginalName = true,
                                            bool keepComponents = true)
    {
        if (prefab == null || targets == null || targets.Length == 0) return;
        
        Undo.SetCurrentGroupName("替换为Prefab");
        int undoGroup = Undo.GetCurrentGroup();
        
        List<GameObject> newObjects = new List<GameObject>();
        int totalMigratedChildren = 0;
        int totalCopiedComponents = 0;
        
        foreach (var target in targets)
        {
            if (target == null) continue;
            
            // 记录原始信息
            // Record original info
            Transform originalParent = target.transform.parent;
            Vector3 localPos = target.transform.localPosition;
            Quaternion localRot = target.transform.localRotation;
            Vector3 localScale = target.transform.localScale;
            int siblingIndex = target.transform.GetSiblingIndex();
            string originalName = target.name;
            
            // 收集原物体的直接子物体
            // Collect direct children of original object
            List<Transform> originalChildren = new List<Transform>();
            if (keepChildren)
            {
                for (int i = 0; i < target.transform.childCount; i++)
                {
                    originalChildren.Add(target.transform.GetChild(i));
                }
            }
            
            // 实例化Prefab
            // Instantiate Prefab
            GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(newObj, "实例化Prefab");
            
            // 设置层级关系
            // Set hierarchy parent
            if (originalParent != null)
            {
                Undo.SetTransformParent(newObj.transform, originalParent, "设置层级");
            }
            
            // 应用原Transform
            // Apply original transform
            newObj.transform.localPosition = localPos;
            newObj.transform.localRotation = localRot;
            newObj.transform.localScale = localScale;
            newObj.transform.SetSiblingIndex(siblingIndex);
            
            // 命名：根据选项决定用原物体名还是Prefab名
            // Naming: use original name or prefab name based on option
            newObj.name = useOriginalName ? originalName : prefab.name;
            
            // 将原物体的子物体迁移到新Prefab下
            // Transfer original children to new Prefab
            if (keepChildren && originalChildren.Count > 0)
            {
                foreach (var child in originalChildren)
                {
                    if (child == null) continue;
                    
                    // 记录子物体的世界坐标信息
                    // Record child world transform
                    Vector3 childWorldPos = child.position;
                    Quaternion childWorldRot = child.rotation;
                    Vector3 childLossyScale = child.lossyScale;
                    
                    Undo.SetTransformParent(child, newObj.transform, "迁移子物体");
                    
                    // 恢复世界坐标（保证视觉位置不变）
                    // Restore world transform (keep visual position unchanged)
                    child.position = childWorldPos;
                    child.rotation = childWorldRot;
                    
                    // 尽量还原缩放（lossyScale是只读的，通过计算localScale还原）
                    // Try to restore scale via lossyScale compensation
                    Vector3 parentLossy = newObj.transform.lossyScale;
                    if (Mathf.Abs(parentLossy.x) > 1e-6f &&
                        Mathf.Abs(parentLossy.y) > 1e-6f &&
                        Mathf.Abs(parentLossy.z) > 1e-6f)
                    {
                        child.localScale = new Vector3(
                            childLossyScale.x / parentLossy.x,
                            childLossyScale.y / parentLossy.y,
                            childLossyScale.z / parentLossy.z
                        );
                    }
                    
                    totalMigratedChildren++;
                }
            }
            
            // 保留原物体上的额外组件（如 JSBehaviour 等）
            // Copy extra components from original object (e.g. JSBehaviour)
            if (keepComponents)
            {
                totalCopiedComponents += CopyExtraComponents(target, newObj);
            }
            
            // 删除原物体（子物体已迁移，此时原物体下已无子物体）
            // Destroy original (children already transferred)
            Undo.DestroyObjectImmediate(target);
            
            newObjects.Add(newObj);
        }
        
        Undo.CollapseUndoOperations(undoGroup);
        
        // 选中新创建的物体
        // Select newly created objects
        Selection.objects = newObjects.ToArray();
        
        string nameMode = useOriginalName ? "原物体名" : "Prefab名";
        string childInfo = keepChildren ? $"，已迁移 {totalMigratedChildren} 个子物体" : "";
        string compInfo = keepComponents && totalCopiedComponents > 0 ? $"，已保留 {totalCopiedComponents} 个脚本组件" : "";
        Debug.Log($"[Moshi_AddParent] 已将 {targets.Length} 个物体替换为 Prefab [{prefab.name}]（{nameMode}{childInfo}{compInfo}）");
    }
    
    /// <summary>
    /// 将原物体上的额外组件拷贝到新物体（排除基础组件与已有同类型组件）
    /// Copy extra components from source to target, excluding base components and existing types
    /// </summary>
    private static int CopyExtraComponents(GameObject source, GameObject target)
    {
        if (source == null || target == null) return 0;
        
        int copiedCount = 0;
        Component[] sourceComponents = source.GetComponents<Component>();
        
        foreach (var comp in sourceComponents)
        {
            if (comp == null) continue;
            
            System.Type compType = comp.GetType();
            
            // 跳过基础组件
            // Skip base components
            if (SkipComponentTypes.Contains(compType)) continue;
            
            // 跳过新物体上已有的同类型组件（避免重复）
            // Skip if target already has the same component type
            if (target.GetComponent(compType) != null) continue;
            
            // 使用 ComponentUtility 拷贝组件（保留所有序列化属性）
            // Copy component via ComponentUtility (preserves all serialized values)
            if (UnityEditorInternal.ComponentUtility.CopyComponent(comp))
            {
                if (UnityEditorInternal.ComponentUtility.PasteComponentAsNew(target))
                {
                    copiedCount++;
                }
            }
        }
        
        return copiedCount;
    }
    
    #endregion
    #region 添加父物体功能
    
    /// <summary>
    /// 右键菜单：添加父物体（子物体坐标归零）
    /// </summary>
    [MenuItem("GameObject/Moshi/添加父物体", false, 0)]
    public static void AddParent()
    {
        if (Selection.activeGameObject == null) return;
        
        // 支持多选
        foreach (var go in Selection.gameObjects)
        {
            AddParentToGameObject(go);
        }
    }
    
    [MenuItem("GameObject/Moshi/添加父物体", true)]
    public static bool ValidateAddParent()
    {
        return Selection.activeGameObject != null;
    }
    
    /// <summary>
    /// 核心方法：为指定物体添加父物体
    /// 父物体继承子物体的localPosition/localRotation，子物体归零
    /// 同时偏移Animation组件中所有AnimationClip的位置曲线
    /// </summary>
    public static GameObject AddParentToGameObject(GameObject child)
    {
        if (child == null) return null;
        
        // 记录原始信息
        Transform originalParent = child.transform.parent;
        Vector3 childLocalPos = child.transform.localPosition;
        Quaternion childLocalRot = child.transform.localRotation;
        Vector3 childLocalScale = child.transform.localScale;
        int siblingIndex = child.transform.GetSiblingIndex();
        
        // 生成父物体名称
        string parentName = child.name + "_Root";
        
        // 创建Undo组
        Undo.SetCurrentGroupName("添加父物体");
        int undoGroup = Undo.GetCurrentGroup();
        
        // 偏移Animation组件中的动画曲线
        OffsetAnimationClips(child, -childLocalPos, Quaternion.Inverse(childLocalRot));
        
        // 创建新的父物体
        GameObject newParent = new GameObject(parentName);
        Undo.RegisterCreatedObjectUndo(newParent, "创建父物体");
        
        // 设置父物体的层级关系（放到原父物体下）
        if (originalParent != null)
        {
            Undo.SetTransformParent(newParent.transform, originalParent, "设置父物体层级");
        }
        newParent.transform.SetSiblingIndex(siblingIndex);
        
        // 父物体继承子物体的localPosition和localRotation
        newParent.transform.localPosition = childLocalPos;
        newParent.transform.localRotation = childLocalRot;
        newParent.transform.localScale = Vector3.one;
        
        // 将子物体设为新父物体的子物体
        Undo.SetTransformParent(child.transform, newParent.transform, "设置子物体");
        
        // 子物体localPosition和localRotation归零，保持原有scale
        Undo.RecordObject(child.transform, "重置子物体变换");
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = childLocalScale;
        
        Undo.CollapseUndoOperations(undoGroup);
        
        // 选中新创建的父物体
        Selection.activeGameObject = newParent;
        
        Debug.Log($"[Moshi_AddParent] 已为 [{child.name}] 添加父物体 [{parentName}]，动画曲线已偏移");
        
        return newParent;
    }
    
    /// <summary>
    /// 偏移Animation组件中所有AnimationClip的位置曲线
    /// </summary>
    private static void OffsetAnimationClips(GameObject go, Vector3 posOffset, Quaternion rotOffset)
    {
        Animation anim = go.GetComponent<Animation>();
        if (anim == null) return;
        
        // 收集所有AnimationClip（去重）
        HashSet<AnimationClip> clips = new HashSet<AnimationClip>();
        
        if (anim.clip != null)
            clips.Add(anim.clip);
        
        foreach (AnimationState state in anim)
        {
            if (state.clip != null)
                clips.Add(state.clip);
        }
        
        // 处理每个AnimationClip
        foreach (var clip in clips)
        {
            OffsetClipCurves(clip, posOffset, rotOffset);
        }
    }
    
    /// <summary>
    /// 偏移单个AnimationClip的位置和旋转曲线
    /// </summary>
    private static void OffsetClipCurves(AnimationClip clip, Vector3 posOffset, Quaternion rotOffset)
    {
        if (clip == null) return;
        
        // 记录Undo
        Undo.RecordObject(clip, "偏移动画曲线");
        
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        
        foreach (var binding in bindings)
        {
            // 只处理根物体的曲线（path为空）
            if (!string.IsNullOrEmpty(binding.path)) continue;
            
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null) continue;
            
            bool modified = false;
            
            // 处理位置曲线
            if (binding.propertyName == "m_LocalPosition.x")
            {
                OffsetCurveValues(curve, posOffset.x);
                modified = true;
            }
            else if (binding.propertyName == "m_LocalPosition.y")
            {
                OffsetCurveValues(curve, posOffset.y);
                modified = true;
            }
            else if (binding.propertyName == "m_LocalPosition.z")
            {
                OffsetCurveValues(curve, posOffset.z);
                modified = true;
            }
            
            if (modified)
            {
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
        }
        
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"[Moshi_AddParent] 已偏移动画 [{clip.name}] 的位置曲线，偏移量: {posOffset}");
    }
    
    /// <summary>
    /// 偏移曲线所有关键帧的值
    /// </summary>
    private static void OffsetCurveValues(AnimationCurve curve, float offset)
    {
        Keyframe[] keys = curve.keys;
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i].value += offset;
        }
        curve.keys = keys;
    }
    
    #endregion
}

/// <summary>
/// 替换为Prefab弹窗
/// </summary>
public class ReplaceWithPrefabWindow : EditorWindow
{
    private static GameObject[] targetObjects;
    private GameObject selectedPrefab;
    private bool keepChildren = true;         // 默认保留子物体
    private bool useOriginalName = true;      // 默认以原物体命名
    private bool keepComponents = true;       // 默认保留原物体上的脚本组件
    
    public static void ShowWindow(GameObject[] targets)
    {
        targetObjects = targets;
        var window = GetWindow<ReplaceWithPrefabWindow>(true, "替换为Prefab", true);
        window.minSize = new Vector2(320, 220);
        window.maxSize = new Vector2(420, 260);
        window.ShowUtility();
    }
    
    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        
        // Prefab选择
        // Prefab selection
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("目标Prefab", GUILayout.Width(70));
        selectedPrefab = (GameObject)EditorGUILayout.ObjectField(selectedPrefab, typeof(GameObject), false);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        // 保留子物体选项
        // Keep children option
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("保留子物体", GUILayout.Width(70));
        keepChildren = EditorGUILayout.Toggle(keepChildren);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(3);
        
        // 保留脚本选项（保留原物体上的 JSBehaviour 等非基础组件）
        // Keep components option (preserve extra components like JSBehaviour)
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("保留脚本", GUILayout.Width(70));
        keepComponents = EditorGUILayout.Toggle(keepComponents);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(3);
        
        // 命名方式按钮组（默认"原物体名"在左侧第一位）
        // Naming mode toggle (default "original name" on the left)
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("命名方式", GUILayout.Width(70));
        
        if (GUILayout.Toggle(useOriginalName, "原物体名", EditorStyles.miniButtonLeft))
            useOriginalName = true;
        if (GUILayout.Toggle(!useOriginalName, "Prefab名", EditorStyles.miniButtonRight))
            useOriginalName = false;
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        // 显示选中数量
        EditorGUILayout.LabelField($"已选中 {targetObjects?.Length ?? 0} 个物体", EditorStyles.centeredGreyMiniLabel);
        
        EditorGUILayout.Space(10);
        
        // 按钮
        // Action buttons
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        if (GUILayout.Button("取消", GUILayout.Width(80)))
        {
            Close();
        }
        
        GUI.enabled = selectedPrefab != null && PrefabUtility.IsPartOfPrefabAsset(selectedPrefab);
        if (GUILayout.Button("替换", GUILayout.Width(80)))
        {
            Moshi_AddParent.DoReplaceWithPrefab(targetObjects, selectedPrefab, keepChildren, useOriginalName, keepComponents);
            Close();
        }
        GUI.enabled = true;
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        // 提示
        // Hint
        if (selectedPrefab != null && !PrefabUtility.IsPartOfPrefabAsset(selectedPrefab))
        {
            EditorGUILayout.HelpBox("请选择Project中的Prefab资源", MessageType.Warning);
        }
    }
}
