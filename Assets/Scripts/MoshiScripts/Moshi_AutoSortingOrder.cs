using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 子对象自动渲染排序脚本
/// 自动设置子对象的 Order in Layer，支持嵌套子物体
/// </summary>
[ExecuteAlways]
public class Moshi_AutoSortingOrder : MonoBehaviour
{
    [Header("渲染排序")]
    [Tooltip("启用自动排序")]
    public bool 自动排序 = true;
    
    [Tooltip("基础Order值")]
    public int 基础排序值 = -50;
    
    [Tooltip("Order递增值")]
    public int 递增值 = 1;
    
    [Tooltip("包含嵌套子物体")]
    public bool 包含嵌套 = true;
    
    // 缓存子对象层级结构的哈希值，用于检测变化
    private int lastHierarchyHash = -1;
    
#if UNITY_EDITOR
    private int lastEditorHierarchyHash = -1;
#endif
    
    private void Start()
    {
        if (自动排序)
        {
            UpdateSortingOrder();
        }
    }
    
    private void Update()
    {
        if (!Application.isPlaying) return;
        
        if (自动排序)
        {
            int currentHash = CalculateHierarchyHash();
            if (currentHash != lastHierarchyHash)
            {
                lastHierarchyHash = currentHash;
                UpdateSortingOrder();
            }
        }
    }
    
    /// <summary>
    /// 获取所有激活的子对象
    /// </summary>
    private List<Transform> GetActiveChildren()
    {
        List<Transform> activeChildren = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.gameObject.activeSelf)
            {
                activeChildren.Add(child);
            }
        }
        return activeChildren;
    }
    
    /// <summary>
    /// 计算层级结构的哈希值
    /// </summary>
    private int CalculateHierarchyHash()
    {
        int hash = 17;
        
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            hash = hash * 31 + child.GetInstanceID();
            hash = hash * 31 + (child.gameObject.activeSelf ? 1 : 0);
            hash = hash * 31 + GetTotalChildCount(child);
        }
        
        return hash;
    }
    
    /// <summary>
    /// 递归获取总子物体数量
    /// </summary>
    private int GetTotalChildCount(Transform target)
    {
        int count = target.childCount;
        for (int i = 0; i < target.childCount; i++)
        {
            count += GetTotalChildCount(target.GetChild(i));
        }
        return count;
    }
    
    /// <summary>
    /// 更新渲染排序
    /// </summary>
    public void UpdateSortingOrder()
    {
        List<Transform> activeChildren = GetActiveChildren();
        if (activeChildren.Count == 0) return;
        
        int[] counter = new int[] { 基础排序值 };
        
        for (int i = 0; i < activeChildren.Count; i++)
        {
            SetSortingOrderRecursive(activeChildren[i], counter);
        }
    }
    
    /// <summary>
    /// 递归设置排序层级（深度优先，紧密排列）
    /// </summary>
    private void SetSortingOrderRecursive(Transform target, int[] counter)
    {
        int currentOrder = counter[0];
        
        // 设置 SpriteRenderer
        SpriteRenderer spriteRenderer = target.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = currentOrder;
        }
        
        // 设置 ParticleSystemRenderer
        ParticleSystemRenderer particleRenderer = target.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            particleRenderer.sortingOrder = currentOrder;
        }
        
        // 设置 MeshRenderer
        MeshRenderer meshRenderer = target.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = currentOrder;
        }
        
        counter[0] += 递增值;
        
        // 递归处理子物体
        if (包含嵌套)
        {
            for (int i = 0; i < target.childCount; i++)
            {
                SetSortingOrderRecursive(target.GetChild(i), counter);
            }
        }
    }
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && 自动排序)
                {
                    UpdateSortingOrder();
                }
            };
        }
    }
    
    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            lastEditorHierarchyHash = CalculateHierarchyHash();
            UnityEditor.EditorApplication.update += CheckChildrenChangeInEditor;
        }
    }
    
    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.update -= CheckChildrenChangeInEditor;
            lastEditorHierarchyHash = -1;
        }
    }
    
    private void CheckChildrenChangeInEditor()
    {
        if (this == null || Application.isPlaying)
        {
            UnityEditor.EditorApplication.update -= CheckChildrenChangeInEditor;
            return;
        }
        
        if (自动排序)
        {
            int currentHash = CalculateHierarchyHash();
            if (currentHash != lastEditorHierarchyHash)
            {
                lastEditorHierarchyHash = currentHash;
                UpdateSortingOrder();
            }
        }
    }
#endif
}
