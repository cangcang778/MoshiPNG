using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 子对象自动合位脚本
/// 当子对象被关闭时，剩余激活的子对象会自动向中心聚拢排列
/// 新激活的牌直接就位，已有的牌平滑移动让位
/// </summary>
[ExecuteAlways]
public class Moshi_AutoAlignChildren : MonoBehaviour
{
    [Header("排列设置")]
    [Tooltip("间隔距离")]
    public float 间隔 = 1.0f;
    
    [Tooltip("排列轴向")]
    public AlignAxis 轴向 = AlignAxis.X轴;
    
    [Tooltip("对齐中心点")]
    public AlignCenter 中心点 = AlignCenter.父对象位置;
    
    [Tooltip("自定义中心偏移")]
    public Vector3 自定义偏移 = Vector3.zero;
    
    [Header("动画设置")]
    [Tooltip("启用平滑动画")]
    public bool 启用动画 = true;
    
    [Tooltip("移动速度")]
    public float 移动速度 = 10f;
    
    [Tooltip("新牌插入方式")]
    public InsertMode 插入方式 = InsertMode.直接就位;
    
    [Header("抬牌设置")]
    [Tooltip("启用抬牌功能")]
    public bool 启用抬牌 = true;
    
    [Tooltip("检测的子对象名称")]
    public string 检测名称 = "in";
    
    [Tooltip("抬起高度")]
    public float 抬起高度 = 0.5f;
    
    [Tooltip("抬牌动画速度")]
    public float 抬牌速度 = 10f;
    
    [Header("出牌设置")]
    [Tooltip("启用出牌功能")]
    public bool 启用出牌 = true;
    
    [Tooltip("出牌检测名称")]
    public string 出牌检测名称 = "out";
    
    [Tooltip("出牌移动距离")]
    public float 出牌距离 = 2f;
    
    [Tooltip("出牌动画时长")]
    public float 出牌时长 = 0.3f;
    
    [Header("发牌设置")]
    [Tooltip("启用发牌功能")]
    public bool 启用发牌 = true;
    
    [Tooltip("发牌检测名称")]
    public string 发牌检测名称 = "deal";
    
    [Tooltip("发牌起点Transform（牌堆位置）")]
    public Transform 发牌起点;
    
    [Tooltip("发牌飞行时长")]
    public float 发牌时长 = 0.4f;
    
    [Tooltip("发牌起始缩放")]
    public Vector3 起始缩放 = new Vector3(0.3f, 0.3f, 0.3f);
    
    [Tooltip("回正时长")]
    public float 回正时长 = 0.15f;
    
    [Tooltip("启用翻牌动画")]
    public bool 启用翻牌 = true;
    
    [Tooltip("翻牌时长")]
    public float 翻牌时长 = 0.2f;
    
    [Tooltip("牌背贴图（发牌时显示，翻到90度时切换为正面）")]
    public Sprite 牌背贴图;
    
    [Header("渲染排序")]
    [Tooltip("启用自动排序")]
    public bool 自动排序 = true;
    
    [Tooltip("基础Order值")]
    public int 基础排序值 = -50;
    
    [Tooltip("Order递增值")]
    public int 递增值 = 1;
    
    [Tooltip("包含嵌套子物体")]
    public bool 包含嵌套 = true;
    
    public enum AlignAxis
    {
        X轴,
        Y轴,
        Z轴
    }
    
    public enum AlignCenter
    {
        父对象位置,
        首个子对象,
        自定义偏移
    }
    
    /// <summary>
    /// 新牌插入方式
    /// </summary>
    public enum InsertMode
    {
        直接就位,    // 新牌直接出现在目标位置
        从边缘滑入   // 新牌从排列边缘滑入
    }
    
    // 缓存子对象层级结构的哈希值，用于检测顺序和嵌套变化
    private int lastHierarchyHash = -1;
    
    // 目标位置列表
    private Dictionary<Transform, Vector3> targetPositions = new Dictionary<Transform, Vector3>();
    
    // 抬牌目标Y偏移
    private Dictionary<Transform, float> liftTargets = new Dictionary<Transform, float>();
    
    // 出牌动画数据
    private class CardOutData
    {
        public float startY;
        public float targetY;
        public float startTime;
        public float duration;
        public List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
        public List<Color> originalColors = new List<Color>();
    }
    private Dictionary<Transform, CardOutData> cardOutAnimations = new Dictionary<Transform, CardOutData>();
    private HashSet<Transform> previousOutActiveCards = new HashSet<Transform>();
    
    // 已完成出牌的卡牌（保持隐藏状态，不参与排列）
    private HashSet<Transform> completedOutCards = new HashSet<Transform>();
    
    // 记录上一帧激活的子对象（用于区分新激活和已存在的牌）
    private HashSet<Transform> previousActiveChildren = new HashSet<Transform>();
    
    // 发牌动画数据
    private enum DealPhase
    {
        Flying,     // 飞行阶段
        Rotating,   // 回正阶段
        Flipping    // 翻牌阶段
    }
    
    private class CardDealData
    {
        public DealPhase phase;
        public float startTime;
        public Vector3 startPos;
        public Vector3 targetPos;
        public Quaternion startRot;
        public float startRotZ;     // 飞行时的Z轴旋转
        public Vector3 startScale;
        public Vector3 targetScale;
        public float currentYRotation; // 当前Y轴旋转（用于翻牌）
        
        // 牌背贴图切换相关
        public SpriteRenderer mainSpriteRenderer;  // 主SpriteRenderer
        public Sprite originalSprite;               // 原始正面贴图
        public bool hasSwappedToFront;              // 是否已切换到正面
    }
    private Dictionary<Transform, CardDealData> cardDealAnimations = new Dictionary<Transform, CardDealData>();
    private HashSet<Transform> previousDealActiveCards = new HashSet<Transform>();
    
    // 正在发牌中的卡牌（不参与排列计算）
    private HashSet<Transform> dealingCards = new HashSet<Transform>();
    
    // 编辑器动画相关
#if UNITY_EDITOR
    private double lastEditorTime;
    private bool isAnimatingInEditor = false;
    private int lastEditorHierarchyHash = -1;
    private HashSet<Transform> previousActiveChildrenEditor = new HashSet<Transform>();
#endif
    
    private void Start()
    {
        // 初始化时记录当前激活的子对象
        previousActiveChildren.Clear();
        foreach (var child in GetActiveChildren())
        {
            previousActiveChildren.Add(child);
        }
        
        // 初始化发牌检测记录
        previousDealActiveCards.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            Transform dealChild = child.Find(发牌检测名称);
            if (dealChild != null && dealChild.gameObject.activeSelf)
            {
                previousDealActiveCards.Add(child);
            }
        }
        
        UpdateAlignment();
    }
    
    private void Update()
    {
        // 编辑器模式下不使用 Update，使用 EditorApplication.update
        if (!Application.isPlaying)
        {
            return;
        }
        
        // 检测层级结构是否变化（包括数量、顺序、嵌套子物体、in状态）
        int currentHash = CalculateHierarchyHash();
        if (currentHash != lastHierarchyHash)
        {
            lastHierarchyHash = currentHash;
            UpdateAlignment();
            UpdateLiftState();
            CheckCardOut();
            CheckCardDeal();
        }
        
        // 平滑移动到目标位置（只控制排列轴）
        if (启用动画 && targetPositions.Count > 0)
        {
            foreach (var kvp in targetPositions)
            {
                // 跳过已出牌的卡牌
                if (kvp.Key == null || completedOutCards.Contains(kvp.Key))
                {
                    continue;
                }
                
                if (kvp.Key.gameObject.activeSelf)
                {
                    Vector3 current = kvp.Key.localPosition;
                    Vector3 target = kvp.Value;
                    
                    // 只对排列轴进行插值
                    kvp.Key.localPosition = LerpOnlyAxis(current, target, Time.deltaTime * 移动速度);
                }
            }
        }
        
        // 抬牌动画
        if (启用抬牌 && liftTargets.Count > 0)
        {
            foreach (var kvp in liftTargets)
            {
                // 跳过已出牌的卡牌
                if (kvp.Key == null || completedOutCards.Contains(kvp.Key))
                {
                    continue;
                }
                
                if (kvp.Key.gameObject.activeSelf)
                {
                    Vector3 current = kvp.Key.localPosition;
                    float targetY = kvp.Value;
                    float newY = Mathf.Lerp(current.y, targetY, Time.deltaTime * 抬牌速度);
                    kvp.Key.localPosition = new Vector3(current.x, newY, current.z);
                }
            }
        }
        
        // 出牌动画
        UpdateCardOutAnimations();
        
        // 发牌动画
        UpdateCardDealAnimations();
        
        // 发牌期间每帧更新排序（确保牌背显示在最上层）
        if (dealingCards.Count > 0 && 自动排序)
        {
            var allActive = GetActiveChildren();
            foreach (var dealingCard in dealingCards)
            {
                if (!allActive.Contains(dealingCard))
                {
                    allActive.Add(dealingCard);
                }
            }
            UpdateSortingOrder(allActive);
        }
    }
    
    /// <summary>
    /// 获取所有激活的子对象（排除已出牌的和透明的）
    /// </summary>
    private List<Transform> GetActiveChildren()
    {
        List<Transform> activeChildren = new List<Transform>();
        bool needRealign = false;
        
        // 先检测completedOutCards中是否有需要恢复的卡牌
        var cardsToRestore = new List<Transform>();
        foreach (var card in completedOutCards)
        {
            if (card == null) continue;
            
            // 检测out是否已关闭（不管卡牌是否激活）
            Transform outChild = card.Find(出牌检测名称);
            bool isOutActive = outChild != null && outChild.gameObject.activeSelf;
            
            // out未激活，需要恢复
            if (!isOutActive)
            {
                cardsToRestore.Add(card);
            }
        }
        
        // 恢复卡牌
        foreach (var card in cardsToRestore)
        {
            completedOutCards.Remove(card);
            previousOutActiveCards.Remove(card);
            
            // 恢复透明度
            var renderers = card.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in renderers)
            {
                Color c = sr.color;
                c.a = 1f;
                sr.color = c;
            }
            
            // 重置Y位置（出牌动画会改变Y位置）
            Vector3 pos = card.localPosition;
            float baseY = 0f;
            if (启用抬牌)
            {
                Transform inChild = card.Find(检测名称);
                bool shouldLift = inChild != null && inChild.gameObject.activeSelf;
                baseY = shouldLift ? 抬起高度 : 0f;
            }
            card.localPosition = new Vector3(pos.x, baseY, pos.z);
            
            // 加入previousActiveChildren，避免被视为新激活的牌
            if (!previousActiveChildren.Contains(card))
            {
                previousActiveChildren.Add(card);
            }
#if UNITY_EDITOR
            if (!previousActiveChildrenEditor.Contains(card))
            {
                previousActiveChildrenEditor.Add(card);
            }
#endif
            needRealign = true;
        }
        
        // 检测发牌状态重置（deal关闭时重置）
        if (启用发牌 && !string.IsNullOrEmpty(发牌检测名称))
        {
            // 检测previousDealActiveCards中是否有deal已关闭的卡牌
            var dealCardsToReset = new List<Transform>();
            foreach (var card in previousDealActiveCards)
            {
                if (card == null) continue;
                
                Transform dealChild = card.Find(发牌检测名称);
                bool isDealActive = dealChild != null && dealChild.gameObject.activeSelf;
                
                // deal已关闭，需要重置
                if (!isDealActive)
                {
                    dealCardsToReset.Add(card);
                }
            }
            
            // 重置发牌状态
            foreach (var card in dealCardsToReset)
            {
                // 如果有正在进行的发牌动画，恢复原始贴图
                if (cardDealAnimations.TryGetValue(card, out CardDealData dealData))
                {
                    if (dealData.mainSpriteRenderer != null && dealData.originalSprite != null)
                    {
                        dealData.mainSpriteRenderer.sprite = dealData.originalSprite;
                    }
                }
                
                previousDealActiveCards.Remove(card);
                dealingCards.Remove(card);
                cardDealAnimations.Remove(card);
                
                // 重置卡牌状态（位置、旋转、缩放）
                card.localRotation = Quaternion.identity;
                card.localScale = Vector3.one;
                
                // 从previousActiveChildren中移除，让它被视为"未激活过"的牌
                previousActiveChildren.Remove(card);
#if UNITY_EDITOR
                previousActiveChildrenEditor.Remove(card);
#endif
                needRealign = true;
            }
        }
        
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            // 排除已完成出牌的卡牌
            if (completedOutCards.Contains(child))
            {
                continue;
            }
            
            // 排除正在发牌中的卡牌（不参与排列计算）
            if (dealingCards.Contains(child))
            {
                continue;
            }
            
            if (!child.gameObject.activeSelf)
            {
                continue;
            }
            
            // 检测out状态下透明度为0的卡牌，直接排除
            if (启用出牌 && !string.IsNullOrEmpty(出牌检测名称))
            {
                Transform outChild = child.Find(出牌检测名称);
                if (outChild != null && outChild.gameObject.activeSelf)
                {
                    // out激活状态，检测透明度
                    SpriteRenderer sr = child.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null && sr.color.a <= 0.01f)
                    {
                        // 透明度为0，排除并加入completedOutCards
                        completedOutCards.Add(child);
                        previousActiveChildren.Remove(child);
#if UNITY_EDITOR
                        previousActiveChildrenEditor.Remove(child);
#endif
                        needRealign = true;
                        continue;
                    }
                }
            }
            
            activeChildren.Add(child);
        }
        
        // 如果有卡牌被新排除，强制触发重新排列
        if (needRealign)
        {
            lastHierarchyHash = -1;
#if UNITY_EDITOR
            lastEditorHierarchyHash = -1;
#endif
        }
        
        return activeChildren;
    }
    
    /// <summary>
    /// 计算层级结构的哈希值，用于检测子对象变化
    /// 包含：子对象数量、顺序、每个子对象的子物体数量、in子对象激活状态
    /// </summary>
    private int CalculateHierarchyHash()
    {
        int hash = 17;
        
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            
            // 已完成出牌的卡牌：只检测out状态变化（用于恢复检测）
            if (completedOutCards.Contains(child))
            {
                if (启用出牌 && !string.IsNullOrEmpty(出牌检测名称))
                {
                    Transform outChild = child.Find(出牌检测名称);
                    hash = hash * 31 + (outChild != null && outChild.gameObject.activeSelf ? 1 : 0);
                }
                continue;
            }
            
            // 加入子对象的 InstanceID（检测顺序和新增/删除）
            hash = hash * 31 + child.GetInstanceID();
            // 加入激活状态
            hash = hash * 31 + (child.gameObject.activeSelf ? 1 : 0);
            // 加入子对象的子物体数量（检测嵌套层级变化）
            hash = hash * 31 + GetTotalChildCount(child);
            
            // 加入检测名称子对象的激活状态（用于抬牌检测）
            if (启用抬牌 && !string.IsNullOrEmpty(检测名称))
            {
                Transform inChild = child.Find(检测名称);
                hash = hash * 31 + (inChild != null && inChild.gameObject.activeSelf ? 1 : 0);
            }
            
            // 加入出牌检测名称子对象的激活状态
            if (启用出牌 && !string.IsNullOrEmpty(出牌检测名称))
            {
                Transform outChild = child.Find(出牌检测名称);
                hash = hash * 31 + (outChild != null && outChild.gameObject.activeSelf ? 1 : 0);
            }
            
            // 加入发牌检测名称子对象的激活状态
            if (启用发牌 && !string.IsNullOrEmpty(发牌检测名称))
            {
                Transform dealChild = child.Find(发牌检测名称);
                hash = hash * 31 + (dealChild != null && dealChild.gameObject.activeSelf ? 1 : 0);
            }
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
    /// 更新排列
    /// </summary>
    public void UpdateAlignment()
    {
        List<Transform> activeChildren = GetActiveChildren();
        
        // 获取所有子对象（包括未激活的，但排除已出牌的）
        List<Transform> allChildren = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!completedOutCards.Contains(child))
            {
                allChildren.Add(child);
            }
        }
        
        if (activeChildren.Count == 0)
        {
            // 没有激活的卡牌时，所有未激活的卡牌位置设为(0,0,0)
            foreach (var child in allChildren)
            {
                if (!child.gameObject.activeSelf)
                {
                    child.localPosition = Vector3.zero;
                }
            }
            previousActiveChildren.Clear();
            return;
        }
        
        targetPositions.Clear();
        
        // 找出新激活的子对象
        HashSet<Transform> newlyActivated = new HashSet<Transform>();
        foreach (var child in activeChildren)
        {
            if (!previousActiveChildren.Contains(child))
            {
                newlyActivated.Add(child);
            }
        }
        
        // 计算中心位置
        Vector3 centerPos = GetCenterPosition();
        
        // 为每个未激活的卡牌计算"假设它被激活后"的位置
        foreach (var child in allChildren)
        {
            if (!child.gameObject.activeSelf)
            {
                // 找到该卡牌在激活列表中的插入位置（按层级顺序）
                int insertIndex = 0;
                int childSiblingIndex = child.GetSiblingIndex();
                
                for (int j = 0; j < activeChildren.Count; j++)
                {
                    if (activeChildren[j].GetSiblingIndex() < childSiblingIndex)
                    {
                        insertIndex = j + 1;
                    }
                }
                
                // 计算假设该卡牌激活后的排列（激活数+1）
                int newCount = activeChildren.Count + 1;
                float newTotalWidth = (newCount - 1) * 间隔;
                float newStartOffset = -newTotalWidth / 2f;
                float offset = newStartOffset + insertIndex * 间隔;
                
                Vector3 presetPos = GetOffsetPosition(Vector3.zero, centerPos, offset);
                child.localPosition = presetPos;
            }
        }
        
        // 计算激活卡牌的总宽度
        float totalWidth = (activeChildren.Count - 1) * 间隔;
        float startOffset = -totalWidth / 2f;
        
        // 计算每个激活子对象的目标位置
        for (int i = 0; i < activeChildren.Count; i++)
        {
            Transform child = activeChildren[i];
            float offset = startOffset + i * 间隔;
            // 传入子对象的原始位置，保留非排列轴的值
            Vector3 targetLocalPos = GetOffsetPosition(child.localPosition, centerPos, offset);
            
            bool isNewlyActivated = newlyActivated.Contains(child);
            
            if (isNewlyActivated)
            {
                // 新激活的牌：根据插入方式处理
                if (插入方式 == InsertMode.直接就位 || !启用动画)
                {
                    // 直接设置到目标位置
                    child.localPosition = targetLocalPos;
                }
                else
                {
                    // 从边缘滑入：设置起始位置，然后动画到目标位置
                    Vector3 edgeStart = GetEdgeStartPosition(centerPos, totalWidth, i, activeChildren.Count);
                    child.localPosition = GetOffsetPosition(child.localPosition, centerPos, GetAxisValue(edgeStart));
                    targetPositions[child] = targetLocalPos;
                }
            }
            else
            {
                // 已存在的牌：平滑移动让位
                targetPositions[child] = targetLocalPos;
                
                // 如果不启用动画，直接设置位置
                if (!启用动画)
                {
                    child.localPosition = targetLocalPos;
                }
            }
        }
        
        // 更新记录
        previousActiveChildren.Clear();
        foreach (var child in activeChildren)
        {
            previousActiveChildren.Add(child);
        }
        
        // 更新渲染排序
        if (自动排序)
        {
            UpdateSortingOrder(activeChildren);
        }
    }
    
    /// <summary>
    /// 更新抬牌状态
    /// 检测每张牌下是否有指定名称的子对象处于激活状态
    /// </summary>
    public void UpdateLiftState()
    {
        if (!启用抬牌 || string.IsNullOrEmpty(检测名称))
        {
            liftTargets.Clear();
            return;
        }
        
        var activeChildren = GetActiveChildren();
        liftTargets.Clear();
        
        foreach (var child in activeChildren)
        {
            // 查找指定名称的子对象
            Transform inChild = child.Find(检测名称);
            bool shouldLift = inChild != null && inChild.gameObject.activeSelf;
            
            // 计算目标Y位置（基于原始Y位置 + 抬起偏移）
            float baseY = 0f; // 假设基础Y为0，可根据需要调整
            float targetY = shouldLift ? baseY + 抬起高度 : baseY;
            
            liftTargets[child] = targetY;
            
            // 如果不启用动画，直接设置Y位置
            if (!启用动画)
            {
                Vector3 pos = child.localPosition;
                child.localPosition = new Vector3(pos.x, targetY, pos.z);
            }
        }
    }
    
    /// <summary>
    /// 检测出牌状态
    /// 当检测到out子对象激活时，开始出牌动画
    /// 当out子对象关闭时，卡牌重新加入排列
    /// </summary>
    private void CheckCardOut()
    {
        if (!启用出牌 || string.IsNullOrEmpty(出牌检测名称))
        {
            return;
        }
        
        // 检测已出牌的卡牌是否需要恢复（out被关闭）
        var cardsToRestore = new List<Transform>();
        foreach (var card in completedOutCards)
        {
            if (card == null) continue;
            
            Transform outChild = card.Find(出牌检测名称);
            bool isOutActive = outChild != null && outChild.gameObject.activeSelf;
            
            // out被关闭，恢复卡牌
            if (!isOutActive)
            {
                cardsToRestore.Add(card);
            }
        }
        
        // 恢复卡牌
        foreach (var card in cardsToRestore)
        {
            RestoreCard(card);
        }
        
        var activeChildren = GetActiveChildren();
        
        foreach (var child in activeChildren)
        {
            Transform outChild = child.Find(出牌检测名称);
            bool isOutActive = outChild != null && outChild.gameObject.activeSelf;
            bool wasOutActive = previousOutActiveCards.Contains(child);
            
            // 检测到out刚被激活
            if (isOutActive && !wasOutActive && !cardOutAnimations.ContainsKey(child))
            {
                StartCardOutAnimation(child);
            }
        }
        
        // 更新记录
        previousOutActiveCards.Clear();
        foreach (var child in activeChildren)
        {
            Transform outChild = child.Find(出牌检测名称);
            if (outChild != null && outChild.gameObject.activeSelf)
            {
                previousOutActiveCards.Add(child);
            }
        }
    }
    
    /// <summary>
    /// 恢复已出牌的卡牌，重新加入排列
    /// </summary>
    private void RestoreCard(Transform card)
    {
        completedOutCards.Remove(card);
        previousOutActiveCards.Remove(card);
        
        // 先激活卡牌，确保能获取到所有子对象
        card.gameObject.SetActive(true);
        
        // 恢复所有SpriteRenderer的透明度
        var renderers = card.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in renderers)
        {
            Color c = sr.color;
            c.a = 1f;
            sr.color = c;
        }
        
        // 重置Y位置到基准位置
        Vector3 pos = card.localPosition;
        float baseY = 0f;
        if (启用抬牌)
        {
            Transform inChild = card.Find(检测名称);
            bool shouldLift = inChild != null && inChild.gameObject.activeSelf;
            baseY = shouldLift ? 抬起高度 : 0f;
        }
        card.localPosition = new Vector3(pos.x, baseY, pos.z);
        
        // 触发重新排列
        lastHierarchyHash = -1;
    }
    
    /// <summary>
    /// 开始出牌动画
    /// </summary>
    private void StartCardOutAnimation(Transform card)
    {
        var data = new CardOutData();
        data.startY = card.localPosition.y;
        data.targetY = data.startY + 出牌距离;
        data.startTime = Time.time;
        data.duration = 出牌时长;
        
        // 收集所有SpriteRenderer
        data.spriteRenderers.Clear();
        data.originalColors.Clear();
        var renderers = card.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in renderers)
        {
            data.spriteRenderers.Add(sr);
            data.originalColors.Add(sr.color);
        }
        
        cardOutAnimations[card] = data;
    }
    
    /// <summary>
    /// 更新出牌动画
    /// </summary>
    private void UpdateCardOutAnimations()
    {
        if (!启用出牌 || cardOutAnimations.Count == 0)
        {
            return;
        }
        
        var completedCards = new List<Transform>();
        
        foreach (var kvp in cardOutAnimations)
        {
            Transform card = kvp.Key;
            CardOutData data = kvp.Value;
            
            if (card == null)
            {
                completedCards.Add(card);
                continue;
            }
            
            float elapsed = Time.time - data.startTime;
            float t = Mathf.Clamp01(elapsed / data.duration);
            
            // 使用缓动函数（EaseOut）
            float easedT = 1f - Mathf.Pow(1f - t, 2f);
            
            // 更新Y位置
            Vector3 pos = card.localPosition;
            pos.y = Mathf.Lerp(data.startY, data.targetY, easedT);
            card.localPosition = pos;
            
            // 更新透明度（渐隐）
            float alpha = 1f - t;
            for (int i = 0; i < data.spriteRenderers.Count; i++)
            {
                if (data.spriteRenderers[i] != null)
                {
                    Color c = data.originalColors[i];
                    c.a = data.originalColors[i].a * alpha;
                    data.spriteRenderers[i].color = c;
                }
            }
            
            // 动画完成
            if (t >= 1f)
            {
                completedCards.Add(card);
                // 标记为已完成出牌
                completedOutCards.Add(card);
                
                // 不隐藏卡牌，保持透明状态即可
                // 这样Timeline重新激活时不需要额外处理
                
                // 从previousActiveChildren中移除，避免重新排列时出问题
                previousActiveChildren.Remove(card);
                
                // 从目标位置和抬牌目标中移除
                targetPositions.Remove(card);
                liftTargets.Remove(card);
            }
        }
        
        // 移除已完成的动画
        foreach (var card in completedCards)
        {
            cardOutAnimations.Remove(card);
        }
    }
    
    /// <summary>
    /// 检测发牌状态
    /// 当检测到deal子对象激活时，开始发牌动画
    /// </summary>
    private void CheckCardDeal()
    {
        if (!启用发牌 || string.IsNullOrEmpty(发牌检测名称))
        {
            return;
        }
        
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            
            // 跳过已完成出牌的卡牌
            if (completedOutCards.Contains(child))
            {
                continue;
            }
            
            Transform dealChild = child.Find(发牌检测名称);
            bool isDealActive = dealChild != null && dealChild.gameObject.activeSelf;
            bool wasDealActive = previousDealActiveCards.Contains(child);
            
            // 检测到deal刚被激活
            if (isDealActive && !wasDealActive && !cardDealAnimations.ContainsKey(child))
            {
                StartCardDealAnimation(child);
            }
        }
        
        // 更新记录
        previousDealActiveCards.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            Transform dealChild = child.Find(发牌检测名称);
            if (dealChild != null && dealChild.gameObject.activeSelf)
            {
                previousDealActiveCards.Add(child);
            }
        }
    }
    
    /// <summary>
    /// 开始发牌动画
    /// </summary>
    private void StartCardDealAnimation(Transform card)
    {
        // 激活卡牌
        card.gameObject.SetActive(true);
        
        // 加入正在发牌的集合
        dealingCards.Add(card);
        
        // 计算目标位置（该卡牌在手牌中的位置）
        Vector3 targetPos = CalculateDealTargetPosition(card);
        
        // 设置起始位置（牌堆位置）
        Vector3 startPos;
        Quaternion startRot;
        if (发牌起点 != null)
        {
            // 转换为本地坐标
            startPos = transform.InverseTransformPoint(发牌起点.position);
            startRot = Quaternion.Inverse(transform.rotation) * 发牌起点.rotation;
        }
        else
        {
            // 默认起点
            startPos = new Vector3(3f, 0f, 0f);
            startRot = Quaternion.identity;
        }
        
        // 设置卡牌初始状态
        card.localPosition = startPos;
        card.localRotation = startRot;
        card.localScale = 起始缩放;
        
        // 计算飞行时的朝向
        // 自家发牌（向下飞）保持正向，其他方向需要旋转
        Vector3 direction = (targetPos - startPos).normalized;
        float flyAngle = 0f;
        bool isSelfDeal = direction.y < -0.5f; // 向下飞行认为是自家发牌
        if (!isSelfDeal && direction.sqrMagnitude > 0.001f)
        {
            flyAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
        }
        
        var data = new CardDealData();
        data.phase = DealPhase.Flying;
        data.startTime = Time.time;
        data.startPos = startPos;
        data.targetPos = targetPos;
        data.startRot = startRot;
        data.startRotZ = flyAngle;
        data.startScale = 起始缩放;
        data.targetScale = Vector3.one;
        data.currentYRotation = 启用翻牌 ? 180f : 0f; // 背面朝上时Y轴旋转180度
        
        // 处理牌背贴图
        data.hasSwappedToFront = false;
        if (牌背贴图 != null && 启用翻牌)
        {
            // 获取主SpriteRenderer（卡牌本身的，不是子对象的）
            data.mainSpriteRenderer = card.GetComponent<SpriteRenderer>();
            if (data.mainSpriteRenderer != null)
            {
                data.originalSprite = data.mainSpriteRenderer.sprite;  // 缓存原始贴图
                data.mainSpriteRenderer.sprite = 牌背贴图;              // 替换为牌背
            }
        }
        
        cardDealAnimations[card] = data;
    }
    
    /// <summary>
    /// 计算发牌目标位置
    /// </summary>
    private Vector3 CalculateDealTargetPosition(Transform card)
    {
        // 获取当前激活的卡牌（不包括正在发牌的）
        List<Transform> activeChildren = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (completedOutCards.Contains(child)) continue;
            if (dealingCards.Contains(child) && child != card) continue;
            if (!child.gameObject.activeSelf && child != card) continue;
            activeChildren.Add(child);
        }
        
        // 确保当前卡牌在列表中
        if (!activeChildren.Contains(card))
        {
            // 按层级顺序插入
            int insertIndex = 0;
            int cardSiblingIndex = card.GetSiblingIndex();
            for (int j = 0; j < activeChildren.Count; j++)
            {
                if (activeChildren[j].GetSiblingIndex() < cardSiblingIndex)
                {
                    insertIndex = j + 1;
                }
            }
            activeChildren.Insert(insertIndex, card);
        }
        
        // 计算该卡牌的目标位置
        int cardIndex = activeChildren.IndexOf(card);
        Vector3 centerPos = GetCenterPosition();
        float totalWidth = (activeChildren.Count - 1) * 间隔;
        float startOffset = -totalWidth / 2f;
        float offset = startOffset + cardIndex * 间隔;
        
        return GetOffsetPosition(Vector3.zero, centerPos, offset);
    }
    
    /// <summary>
    /// 更新发牌动画
    /// </summary>
    private void UpdateCardDealAnimations()
    {
        if (!启用发牌 || cardDealAnimations.Count == 0)
        {
            return;
        }
        
        var completedCards = new List<Transform>();
        
        foreach (var kvp in cardDealAnimations)
        {
            Transform card = kvp.Key;
            CardDealData data = kvp.Value;
            
            if (card == null)
            {
                completedCards.Add(card);
                continue;
            }
            
            float elapsed = Time.time - data.startTime;
            
            switch (data.phase)
            {
                case DealPhase.Flying:
                    UpdateFlyingPhase(card, data, elapsed);
                    break;
                case DealPhase.Rotating:
                    UpdateRotatingPhase(card, data, elapsed);
                    break;
                case DealPhase.Flipping:
                    UpdateFlippingPhase(card, data, elapsed, completedCards);
                    break;
            }
        }
        
        // 移除已完成的动画
        foreach (var card in completedCards)
        {
            cardDealAnimations.Remove(card);
            dealingCards.Remove(card);
            
            // 加入正常排列
            if (!previousActiveChildren.Contains(card))
            {
                previousActiveChildren.Add(card);
            }
            
            // 触发重新排列
            lastHierarchyHash = -1;
        }
        
        // 发牌期间持续更新排序（让发牌的卡牌保持在最上层）
        if (cardDealAnimations.Count > 0 && 自动排序)
        {
            var allActive = GetActiveChildren();
            // 把正在发牌的卡牌也加入排序
            foreach (var dealingCard in dealingCards)
            {
                if (!allActive.Contains(dealingCard))
                {
                    allActive.Add(dealingCard);
                }
            }
            UpdateSortingOrder(allActive);
        }
    }
    
    /// <summary>
    /// 更新飞行阶段
    /// </summary>
    private void UpdateFlyingPhase(Transform card, CardDealData data, float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / 发牌时长);
        float easedT = 1f - Mathf.Pow(1f - t, 2f); // EaseOut
        
        // 实时更新目标位置
        data.targetPos = CalculateDealTargetPosition(card);
        
        // 位置插值
        card.localPosition = Vector3.Lerp(data.startPos, data.targetPos, easedT);
        
        // 缩放插值
        card.localScale = Vector3.Lerp(data.startScale, data.targetScale, easedT);
        
        // 飞行时保持正向，Z轴旋转为0
        card.localRotation = Quaternion.Euler(0, data.currentYRotation, 0);
        
        // 飞行阶段完成，直接进入翻牌阶段（跳过回正）
        if (t >= 1f)
        {
            if (启用翻牌)
            {
                data.phase = DealPhase.Flipping;
                data.startTime = Time.time;
            }
            else
            {
                // 直接完成
                card.localRotation = Quaternion.identity;
                data.phase = DealPhase.Flipping;
                data.startTime = Time.time - 翻牌时长;
            }
        }
    }
    
    /// <summary>
    /// 更新回正阶段
    /// </summary>
    private void UpdateRotatingPhase(Transform card, CardDealData data, float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / 回正时长);
        float easedT = 1f - Mathf.Pow(1f - t, 2f); // EaseOut
        
        // 实时更新位置跟随手牌排列
        data.targetPos = CalculateDealTargetPosition(card);
        card.localPosition = data.targetPos;
        
        // Z轴旋转回正到0
        float currentZ = Mathf.Lerp(data.startRotZ, 0f, easedT);
        card.localRotation = Quaternion.Euler(0, data.currentYRotation, currentZ);
        
        // 回正阶段完成
        if (t >= 1f)
        {
            if (启用翻牌)
            {
                // 进入翻牌阶段
                data.phase = DealPhase.Flipping;
                data.startTime = Time.time;
            }
            else
            {
                // 直接完成
                card.localRotation = Quaternion.identity;
                data.phase = DealPhase.Flipping;
                data.startTime = Time.time - 翻牌时长; // 立即完成
            }
        }
    }
    
    /// <summary>
    /// 更新翻牌阶段
    /// </summary>
    private void UpdateFlippingPhase(Transform card, CardDealData data, float elapsed, List<Transform> completedCards)
    {
        float t = Mathf.Clamp01(elapsed / 翻牌时长);
        float easedT = 1f - Mathf.Pow(1f - t, 2f); // EaseOut
        
        // 实时更新位置跟随手牌排列
        data.targetPos = CalculateDealTargetPosition(card);
        card.localPosition = data.targetPos;
        
        // Y轴从180度翻转到0度（左右翻）
        float currentY = Mathf.Lerp(180f, 0f, easedT);
        card.localRotation = Quaternion.Euler(0, currentY, 0);
        
        // 在翻转到90度时切换贴图（此时卡牌侧面朝向玩家，切换不会被察觉）
        if (!data.hasSwappedToFront && currentY <= 90f)
        {
            if (data.mainSpriteRenderer != null && data.originalSprite != null)
            {
                data.mainSpriteRenderer.sprite = data.originalSprite;
            }
            data.hasSwappedToFront = true;
        }
        
        // 翻牌完成
        if (t >= 1f)
        {
            card.localRotation = Quaternion.identity;
            completedCards.Add(card);
        }
    }
    
    /// <summary>
    /// 获取边缘起始位置（用于从边缘滑入效果）
    /// </summary>
    private Vector3 GetEdgeStartPosition(Vector3 centerPos, float totalWidth, int index, int totalCount)
    {
        // 判断是在左半边还是右半边
        float halfCount = totalCount / 2f;
        float edgeOffset;
        
        if (index < halfCount)
        {
            // 左半边，从左边缘外滑入
            edgeOffset = -totalWidth / 2f - 间隔;
        }
        else
        {
            // 右半边，从右边缘外滑入
            edgeOffset = totalWidth / 2f + 间隔;
        }
        
        return centerPos + GetAxisVector(edgeOffset);
    }
    
    /// <summary>
    /// 获取轴向的向量
    /// </summary>
    private Vector3 GetAxisVector(float value)
    {
        switch (轴向)
        {
            case AlignAxis.X轴: return new Vector3(value, 0, 0);
            case AlignAxis.Y轴: return new Vector3(0, value, 0);
            case AlignAxis.Z轴: return new Vector3(0, 0, value);
            default: return Vector3.zero;
        }
    }
    
    /// <summary>
    /// 获取向量在排列轴上的值
    /// </summary>
    private float GetAxisValue(Vector3 pos)
    {
        switch (轴向)
        {
            case AlignAxis.X轴: return pos.x;
            case AlignAxis.Y轴: return pos.y;
            case AlignAxis.Z轴: return pos.z;
            default: return 0f;
        }
    }
    
    /// <summary>
    /// 更新子对象的渲染排序
    /// 深度优先遍历，紧密排列：子物体比父物体高，后兄弟比前兄弟高
    /// 例如: pai1: K1=-50, suo_01=-49, K2=-48, suo_02=-47
    ///       pai2: K1=-46, suo_03=-45, K2=-44, suo_04=-43
    /// 正在发牌的卡牌会被提升到最高层级
    /// </summary>
    private void UpdateSortingOrder(List<Transform> activeChildren)
    {
        // 使用数组包装计数器，以便在递归中传递引用
        int[] counter = new int[] { 基础排序值 };
        
        // 先处理非发牌中的卡牌
        for (int i = 0; i < activeChildren.Count; i++)
        {
            if (!dealingCards.Contains(activeChildren[i]))
            {
                SetSortingOrderRecursive(activeChildren[i], counter);
            }
        }
        
        // 再处理正在发牌的卡牌（排序值更高，显示在最上层）
        for (int i = 0; i < activeChildren.Count; i++)
        {
            if (dealingCards.Contains(activeChildren[i]))
            {
                SetSortingOrderRecursive(activeChildren[i], counter);
            }
        }
    }
    
    /// <summary>
    /// 递归设置排序层级（深度优先，紧密排列）
    /// </summary>
    private void SetSortingOrderRecursive(Transform target, int[] counter)
    {
        // 当前节点使用当前计数器值
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
        
        // 设置 MeshRenderer (如果使用 sortingOrder)
        MeshRenderer meshRenderer = target.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = currentOrder;
        }
        
        // 计数器递增，为下一个节点准备
        counter[0] += 递增值;
        
        // 递归处理子物体（深度优先）
        if (包含嵌套)
        {
            for (int i = 0; i < target.childCount; i++)
            {
                SetSortingOrderRecursive(target.GetChild(i), counter);
            }
        }
    }
    
    /// <summary>
    /// 获取中心位置（本地坐标）
    /// </summary>
    private Vector3 GetCenterPosition()
    {
        switch (中心点)
        {
            case AlignCenter.父对象位置:
                return Vector3.zero;
                
            case AlignCenter.首个子对象:
                var activeChildren = GetActiveChildren();
                if (activeChildren.Count > 0)
                {
                    // 保持非排列轴的位置
                    Vector3 firstPos = activeChildren[0].localPosition;
                    return GetNonAxisPosition(firstPos);
                }
                return Vector3.zero;
                
            case AlignCenter.自定义偏移:
                return 自定义偏移;
                
            default:
                return Vector3.zero;
        }
    }
    
    /// <summary>
    /// 获取非排列轴的位置分量
    /// </summary>
    private Vector3 GetNonAxisPosition(Vector3 pos)
    {
        switch (轴向)
        {
            case AlignAxis.X轴:
                return new Vector3(0, pos.y, pos.z);
            case AlignAxis.Y轴:
                return new Vector3(pos.x, 0, pos.z);
            case AlignAxis.Z轴:
                return new Vector3(pos.x, pos.y, 0);
            default:
                return Vector3.zero;
        }
    }
    
    /// <summary>
    /// 根据轴向计算偏移位置，保留其他轴的原始值
    /// </summary>
    private Vector3 GetOffsetPosition(Vector3 originalPos, Vector3 center, float offset)
    {
        switch (轴向)
        {
            case AlignAxis.X轴:
                // 只修改X轴，保留原始Y和Z
                return new Vector3(center.x + offset, originalPos.y, originalPos.z);
            case AlignAxis.Y轴:
                // 只修改Y轴，保留原始X和Z
                return new Vector3(originalPos.x, center.y + offset, originalPos.z);
            case AlignAxis.Z轴:
                // 只修改Z轴，保留原始X和Y
                return new Vector3(originalPos.x, originalPos.y, center.z + offset);
            default:
                return originalPos;
        }
    }
    
    /// <summary>
    /// 手动触发重新排列
    /// </summary>
    [ContextMenu("立即重新排列")]
    public void ForceUpdateAlignment()
    {
        lastHierarchyHash = -1;
        UpdateAlignment();
    }
    
    /// <summary>
    /// 手动触发重新排序
    /// </summary>
    [ContextMenu("立即重新排序")]
    public void ForceUpdateSortingOrder()
    {
        if (自动排序)
        {
            UpdateSortingOrder(GetActiveChildren());
        }
    }
    
    /// <summary>
    /// 重置所有已出牌状态（让出过的牌可以重新参与排列）
    /// </summary>
    [ContextMenu("重置出牌状态")]
    public void ResetCardOutState()
    {
        completedOutCards.Clear();
        previousOutActiveCards.Clear();
        cardOutAnimations.Clear();
        lastHierarchyHash = -1;
        UpdateAlignment();
    }
    
    /// <summary>
    /// 重置所有发牌状态
    /// </summary>
    [ContextMenu("重置发牌状态")]
    public void ResetCardDealState()
    {
        cardDealAnimations.Clear();
        previousDealActiveCards.Clear();
        dealingCards.Clear();
        lastHierarchyHash = -1;
        UpdateAlignment();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 编辑器中修改参数后立即生效
        if (!Application.isPlaying)
        {
            // 使用更安全的延迟调用方式
            UnityEditor.EditorApplication.delayCall -= DelayedUpdateAlignment;
            UnityEditor.EditorApplication.delayCall += DelayedUpdateAlignment;
        }
    }
    
    private void DelayedUpdateAlignment()
    {
        // 移除自身，避免重复调用
        UnityEditor.EditorApplication.delayCall -= DelayedUpdateAlignment;
        
        // 安全检查
        if (this == null) return;
        if (!gameObject) return;
        
        try
        {
            UpdateAlignmentEditor();
        }
        catch (System.Exception)
        {
            // 忽略在不安全时机的异常
        }
    }
    
    /// <summary>
    /// 编辑器中更新排列（支持动画）
    /// </summary>
    private void UpdateAlignmentEditor()
    {
        List<Transform> activeChildren = GetActiveChildren();
        
        // 获取所有子对象（包括未激活的，但排除已出牌的）
        List<Transform> allChildren = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!completedOutCards.Contains(child))
            {
                allChildren.Add(child);
            }
        }
        
        if (activeChildren.Count == 0)
        {
            // 没有激活的卡牌时，所有未激活的卡牌位置设为(0,0,0)
            foreach (var child in allChildren)
            {
                if (!child.gameObject.activeSelf)
                {
                    child.localPosition = Vector3.zero;
                }
            }
            previousActiveChildrenEditor.Clear();
            return;
        }
        
        targetPositions.Clear();
        
        // 找出新激活的子对象
        HashSet<Transform> newlyActivated = new HashSet<Transform>();
        foreach (var child in activeChildren)
        {
            if (!previousActiveChildrenEditor.Contains(child))
            {
                newlyActivated.Add(child);
            }
        }
        
        Vector3 centerPos = GetCenterPosition();
        
        // 为每个未激活的卡牌计算"假设它被激活后"的位置
        foreach (var child in allChildren)
        {
            if (!child.gameObject.activeSelf)
            {
                // 找到该卡牌在激活列表中的插入位置（按层级顺序）
                int insertIndex = 0;
                int childSiblingIndex = child.GetSiblingIndex();
                
                for (int j = 0; j < activeChildren.Count; j++)
                {
                    if (activeChildren[j].GetSiblingIndex() < childSiblingIndex)
                    {
                        insertIndex = j + 1;
                    }
                }
                
                // 计算假设该卡牌激活后的排列（激活数+1）
                int newCount = activeChildren.Count + 1;
                float newTotalWidth = (newCount - 1) * 间隔;
                float newStartOffset = -newTotalWidth / 2f;
                float offset = newStartOffset + insertIndex * 间隔;
                
                Vector3 presetPos = GetOffsetPosition(Vector3.zero, centerPos, offset);
                child.localPosition = presetPos;
            }
        }
        
        // 计算激活卡牌的排列
        float totalWidth = (activeChildren.Count - 1) * 间隔;
        float startOffset = -totalWidth / 2f;
        
        for (int i = 0; i < activeChildren.Count; i++)
        {
            Transform child = activeChildren[i];
            float offset = startOffset + i * 间隔;
            Vector3 targetLocalPos = GetOffsetPosition(child.localPosition, centerPos, offset);
            
            bool isNewlyActivated = newlyActivated.Contains(child);
            
            if (isNewlyActivated)
            {
                // 新激活的牌：根据插入方式处理
                if (插入方式 == InsertMode.直接就位 || !启用动画)
                {
                    child.localPosition = targetLocalPos;
                }
                else
                {
                    // 从边缘滑入
                    Vector3 edgeStart = GetEdgeStartPosition(centerPos, totalWidth, i, activeChildren.Count);
                    child.localPosition = GetOffsetPosition(child.localPosition, centerPos, GetAxisValue(edgeStart));
                    targetPositions[child] = targetLocalPos;
                }
            }
            else
            {
                // 已存在的牌：平滑移动让位
                targetPositions[child] = targetLocalPos;
                
                if (!启用动画)
                {
                    child.localPosition = targetLocalPos;
                }
            }
        }
        
        // 更新记录
        previousActiveChildrenEditor.Clear();
        foreach (var child in activeChildren)
        {
            previousActiveChildrenEditor.Add(child);
        }
        
        // 更新抬牌状态
        UpdateLiftStateEditor();
        
        // 检测出牌
        CheckCardOutEditor();
        
        // 检测发牌
        CheckCardDealEditor();
        
        // 启用编辑器动画（排列或抬牌或出牌或发牌）
        bool needAnimation = (targetPositions.Count > 0 || liftTargets.Count > 0 || cardOutAnimations.Count > 0 || cardDealAnimations.Count > 0);
        if (启用动画 && needAnimation && !isAnimatingInEditor)
        {
            isAnimatingInEditor = true;
            lastEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.EditorApplication.update += EditorAnimationUpdate;
        }
    }
    
    /// <summary>
    /// 编辑器模式下更新抬牌状态
    /// </summary>
    private void UpdateLiftStateEditor()
    {
        if (!启用抬牌 || string.IsNullOrEmpty(检测名称))
        {
            liftTargets.Clear();
            return;
        }
        
        var activeChildren = GetActiveChildren();
        liftTargets.Clear();
        
        foreach (var child in activeChildren)
        {
            Transform inChild = child.Find(检测名称);
            bool shouldLift = inChild != null && inChild.gameObject.activeSelf;
            
            float baseY = 0f;
            float targetY = shouldLift ? baseY + 抬起高度 : baseY;
            
            liftTargets[child] = targetY;
            
            if (!启用动画)
            {
                Vector3 pos = child.localPosition;
                child.localPosition = new Vector3(pos.x, targetY, pos.z);
            }
        }
    }
    
    /// <summary>
    /// 编辑器模式下检测出牌
    /// 当out子对象关闭时，卡牌重新加入排列
    /// </summary>
    private void CheckCardOutEditor()
    {
        if (!启用出牌 || string.IsNullOrEmpty(出牌检测名称))
        {
            return;
        }
        
        // 检测已出牌的卡牌是否需要恢复（out被关闭）
        var cardsToRestore = new List<Transform>();
        foreach (var card in completedOutCards)
        {
            if (card == null) continue;
            
            Transform outChild = card.Find(出牌检测名称);
            bool isOutActive = outChild != null && outChild.gameObject.activeSelf;
            
            // out被关闭，恢复卡牌
            if (!isOutActive)
            {
                cardsToRestore.Add(card);
            }
        }
        
        // 恢复卡牌
        foreach (var card in cardsToRestore)
        {
            RestoreCardEditor(card);
        }
        
        var activeChildren = GetActiveChildren();
        
        foreach (var child in activeChildren)
        {
            Transform outChild = child.Find(出牌检测名称);
            bool isOutActive = outChild != null && outChild.gameObject.activeSelf;
            bool wasOutActive = previousOutActiveCards.Contains(child);
            
            if (isOutActive && !wasOutActive && !cardOutAnimations.ContainsKey(child))
            {
                StartCardOutAnimationEditor(child);
            }
        }
        
        previousOutActiveCards.Clear();
        foreach (var child in activeChildren)
        {
            Transform outChild = child.Find(出牌检测名称);
            if (outChild != null && outChild.gameObject.activeSelf)
            {
                previousOutActiveCards.Add(child);
            }
        }
    }
    
    /// <summary>
    /// 编辑器模式下恢复已出牌的卡牌
    /// </summary>
    private void RestoreCardEditor(Transform card)
    {
        completedOutCards.Remove(card);
        previousOutActiveCards.Remove(card);
        previousActiveChildrenEditor.Remove(card);
        
        // 先激活卡牌，确保能获取到所有子对象
        card.gameObject.SetActive(true);
        
        // 恢复所有SpriteRenderer的透明度
        var renderers = card.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in renderers)
        {
            Color c = sr.color;
            c.a = 1f;
            sr.color = c;
        }
        
        // 重置Y位置到基准位置
        Vector3 pos = card.localPosition;
        float baseY = 0f;
        if (启用抬牌)
        {
            Transform inChild = card.Find(检测名称);
            bool shouldLift = inChild != null && inChild.gameObject.activeSelf;
            baseY = shouldLift ? 抬起高度 : 0f;
        }
        card.localPosition = new Vector3(pos.x, baseY, pos.z);
        
        // 触发重新排列
        lastEditorHierarchyHash = -1;
    }
    
    /// <summary>
    /// 编辑器模式下开始出牌动画
    /// </summary>
    private void StartCardOutAnimationEditor(Transform card)
    {
        var data = new CardOutData();
        data.startY = card.localPosition.y;
        data.targetY = data.startY + 出牌距离;
        data.startTime = (float)UnityEditor.EditorApplication.timeSinceStartup;
        data.duration = 出牌时长;
        
        data.spriteRenderers.Clear();
        data.originalColors.Clear();
        var renderers = card.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in renderers)
        {
            data.spriteRenderers.Add(sr);
            data.originalColors.Add(sr.color);
        }
        
        cardOutAnimations[card] = data;
    }
    
    /// <summary>
    /// 编辑器模式下检测发牌
    /// </summary>
    private void CheckCardDealEditor()
    {
        if (!启用发牌 || string.IsNullOrEmpty(发牌检测名称))
        {
            return;
        }
        
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            
            // 跳过已完成出牌的卡牌
            if (completedOutCards.Contains(child))
            {
                continue;
            }
            
            Transform dealChild = child.Find(发牌检测名称);
            bool isDealActive = dealChild != null && dealChild.gameObject.activeSelf;
            bool wasDealActive = previousDealActiveCards.Contains(child);
            
            // 检测到deal刚被激活
            if (isDealActive && !wasDealActive && !cardDealAnimations.ContainsKey(child))
            {
                StartCardDealAnimationEditor(child);
            }
        }
        
        // 更新记录
        previousDealActiveCards.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            Transform dealChild = child.Find(发牌检测名称);
            if (dealChild != null && dealChild.gameObject.activeSelf)
            {
                previousDealActiveCards.Add(child);
            }
        }
    }
    
    /// <summary>
    /// 编辑器模式下开始发牌动画
    /// </summary>
    private void StartCardDealAnimationEditor(Transform card)
    {
        // 激活卡牌
        card.gameObject.SetActive(true);
        
        // 加入正在发牌的集合
        dealingCards.Add(card);
        
        // 计算目标位置
        Vector3 targetPos = CalculateDealTargetPosition(card);
        
        // 设置起始位置（牌堆位置）
        Vector3 startPos;
        Quaternion startRot;
        if (发牌起点 != null)
        {
            startPos = transform.InverseTransformPoint(发牌起点.position);
            startRot = Quaternion.Inverse(transform.rotation) * 发牌起点.rotation;
        }
        else
        {
            startPos = new Vector3(3f, 0f, 0f);
            startRot = Quaternion.identity;
        }
        
        // 设置卡牌初始状态
        card.localPosition = startPos;
        card.localRotation = startRot;
        card.localScale = 起始缩放;
        
        // 计算飞行时的朝向
        // 自家发牌（向下飞）保持正向，其他方向需要旋转
        Vector3 direction = (targetPos - startPos).normalized;
        float flyAngle = 0f;
        bool isSelfDeal = direction.y < -0.5f; // 向下飞行认为是自家发牌
        if (!isSelfDeal && direction.sqrMagnitude > 0.001f)
        {
            flyAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
        }
        
        var data = new CardDealData();
        data.phase = DealPhase.Flying;
        data.startTime = (float)UnityEditor.EditorApplication.timeSinceStartup;
        data.startPos = startPos;
        data.targetPos = targetPos;
        data.startRot = startRot;
        data.startRotZ = flyAngle;
        data.startScale = 起始缩放;
        data.targetScale = Vector3.one;
        data.currentYRotation = 启用翻牌 ? 180f : 0f;
        
        // 处理牌背贴图
        data.hasSwappedToFront = false;
        if (牌背贴图 != null && 启用翻牌)
        {
            // 获取主SpriteRenderer（卡牌本身的，不是子对象的）
            data.mainSpriteRenderer = card.GetComponent<SpriteRenderer>();
            if (data.mainSpriteRenderer != null)
            {
                data.originalSprite = data.mainSpriteRenderer.sprite;  // 缓存原始贴图
                data.mainSpriteRenderer.sprite = 牌背贴图;              // 替换为牌背
            }
        }
        
        cardDealAnimations[card] = data;
    }
    
    /// <summary>
    /// 编辑器动画更新
    /// </summary>
    private void EditorAnimationUpdate()
    {
        if (this == null || Application.isPlaying)
        {
            StopEditorAnimation();
            return;
        }
        
        double currentTime = UnityEditor.EditorApplication.timeSinceStartup;
        float deltaTime = (float)(currentTime - lastEditorTime);
        lastEditorTime = currentTime;
        
        bool allReached = true;
        
        // 排列轴动画
        foreach (var kvp in targetPositions)
        {
            // 跳过已出牌的卡牌
            if (kvp.Key == null || completedOutCards.Contains(kvp.Key))
            {
                continue;
            }
            
            if (kvp.Key.gameObject.activeSelf)
            {
                Vector3 current = kvp.Key.localPosition;
                Vector3 target = kvp.Value;
                
                // 只比较排列轴的距离
                float axisDistance = GetAxisDistance(current, target);
                
                if (axisDistance > 0.001f)
                {
                    // 只对排列轴进行插值，保留其他轴的当前值
                    kvp.Key.localPosition = LerpOnlyAxis(current, target, deltaTime * 移动速度);
                    allReached = false;
                }
                else
                {
                    // 只设置排列轴的值，保留其他轴当前值
                    kvp.Key.localPosition = SetOnlyAxis(current, target);
                }
            }
        }
        
        // 抬牌动画（Y轴）
        if (启用抬牌)
        {
            foreach (var kvp in liftTargets)
            {
                // 跳过已出牌的卡牌
                if (kvp.Key == null || completedOutCards.Contains(kvp.Key))
                {
                    continue;
                }
                
                if (kvp.Key.gameObject.activeSelf)
                {
                    Vector3 current = kvp.Key.localPosition;
                    float targetY = kvp.Value;
                    
                    float yDistance = Mathf.Abs(current.y - targetY);
                    
                    if (yDistance > 0.001f)
                    {
                        float newY = Mathf.Lerp(current.y, targetY, deltaTime * 抬牌速度);
                        kvp.Key.localPosition = new Vector3(current.x, newY, current.z);
                        allReached = false;
                    }
                    else
                    {
                        kvp.Key.localPosition = new Vector3(current.x, targetY, current.z);
                    }
                }
            }
        }
        
        // 出牌动画
        if (启用出牌 && cardOutAnimations.Count > 0)
        {
            allReached = false;
            UpdateCardOutAnimationsEditor((float)currentTime);
        }
        
        // 发牌动画
        if (启用发牌 && cardDealAnimations.Count > 0)
        {
            allReached = false;
            UpdateCardDealAnimationsEditor((float)currentTime);
        }
        
        // 强制刷新Scene视图
        UnityEditor.SceneView.RepaintAll();
        
        if (allReached && cardOutAnimations.Count == 0 && cardDealAnimations.Count == 0)
        {
            StopEditorAnimation();
        }
    }
    
    /// <summary>
    /// 编辑器模式下更新出牌动画
    /// </summary>
    private void UpdateCardOutAnimationsEditor(float currentTime)
    {
        var completedCards = new List<Transform>();
        
        foreach (var kvp in cardOutAnimations)
        {
            Transform card = kvp.Key;
            CardOutData data = kvp.Value;
            
            if (card == null)
            {
                completedCards.Add(card);
                continue;
            }
            
            float elapsed = currentTime - data.startTime;
            float t = Mathf.Clamp01(elapsed / data.duration);
            
            float easedT = 1f - Mathf.Pow(1f - t, 2f);
            
            Vector3 pos = card.localPosition;
            pos.y = Mathf.Lerp(data.startY, data.targetY, easedT);
            card.localPosition = pos;
            
            float alpha = 1f - t;
            for (int i = 0; i < data.spriteRenderers.Count; i++)
            {
                if (data.spriteRenderers[i] != null)
                {
                    Color c = data.originalColors[i];
                    c.a = data.originalColors[i].a * alpha;
                    data.spriteRenderers[i].color = c;
                }
            }
            
            if (t >= 1f)
            {
                completedCards.Add(card);
                // 标记为已完成出牌
                completedOutCards.Add(card);
                
                // 不隐藏卡牌，保持透明状态即可
                
                // 从编辑器的previousActiveChildrenEditor中移除
                previousActiveChildrenEditor.Remove(card);
                
                // 从目标位置和抬牌目标中移除
                targetPositions.Remove(card);
                liftTargets.Remove(card);
            }
        }
        
        foreach (var card in completedCards)
        {
            cardOutAnimations.Remove(card);
        }
    }
    
    /// <summary>
    /// 编辑器模式下更新发牌动画
    /// </summary>
    private void UpdateCardDealAnimationsEditor(float currentTime)
    {
        var completedCards = new List<Transform>();
        
        foreach (var kvp in cardDealAnimations)
        {
            Transform card = kvp.Key;
            CardDealData data = kvp.Value;
            
            if (card == null)
            {
                completedCards.Add(card);
                continue;
            }
            
            float elapsed = currentTime - data.startTime;
            
            switch (data.phase)
            {
                case DealPhase.Flying:
                    UpdateFlyingPhaseEditor(card, data, elapsed, currentTime);
                    break;
                case DealPhase.Rotating:
                    UpdateRotatingPhaseEditor(card, data, elapsed, currentTime);
                    break;
                case DealPhase.Flipping:
                    UpdateFlippingPhaseEditor(card, data, elapsed, completedCards);
                    break;
            }
        }
        
        // 移除已完成的动画
        foreach (var card in completedCards)
        {
            cardDealAnimations.Remove(card);
            dealingCards.Remove(card);
            
            // 加入正常排列
            if (!previousActiveChildrenEditor.Contains(card))
            {
                previousActiveChildrenEditor.Add(card);
            }
            
            // 触发重新排列
            lastEditorHierarchyHash = -1;
        }
        
        // 发牌期间持续更新排序（让发牌的卡牌保持在最上层）
        if (cardDealAnimations.Count > 0 && 自动排序)
        {
            var allActive = GetActiveChildren();
            // 把正在发牌的卡牌也加入排序
            foreach (var dealingCard in dealingCards)
            {
                if (!allActive.Contains(dealingCard))
                {
                    allActive.Add(dealingCard);
                }
            }
            UpdateSortingOrder(allActive);
        }
    }
    
    /// <summary>
    /// 编辑器模式下更新飞行阶段
    /// </summary>
    private void UpdateFlyingPhaseEditor(Transform card, CardDealData data, float elapsed, float currentTime)
    {
        float t = Mathf.Clamp01(elapsed / 发牌时长);
        float easedT = 1f - Mathf.Pow(1f - t, 2f);
        
        // 实时更新目标位置
        data.targetPos = CalculateDealTargetPosition(card);
        
        card.localPosition = Vector3.Lerp(data.startPos, data.targetPos, easedT);
        card.localScale = Vector3.Lerp(data.startScale, data.targetScale, easedT);
        
        // 飞行时保持正向，Z轴旋转为0
        card.localRotation = Quaternion.Euler(0, data.currentYRotation, 0);
        
        // 飞行阶段完成，直接进入翻牌阶段（跳过回正）
        if (t >= 1f)
        {
            if (启用翻牌)
            {
                data.phase = DealPhase.Flipping;
                data.startTime = currentTime;
            }
            else
            {
                card.localRotation = Quaternion.identity;
                data.phase = DealPhase.Flipping;
                data.startTime = currentTime - 翻牌时长;
            }
        }
    }
    
    /// <summary>
    /// 编辑器模式下更新回正阶段
    /// </summary>
    private void UpdateRotatingPhaseEditor(Transform card, CardDealData data, float elapsed, float currentTime)
    {
        float t = Mathf.Clamp01(elapsed / 回正时长);
        float easedT = 1f - Mathf.Pow(1f - t, 2f);
        
        // 实时更新位置跟随手牌排列
        data.targetPos = CalculateDealTargetPosition(card);
        card.localPosition = data.targetPos;
        
        float currentZ = Mathf.Lerp(data.startRotZ, 0f, easedT);
        card.localRotation = Quaternion.Euler(0, data.currentYRotation, currentZ);
        
        if (t >= 1f)
        {
            if (启用翻牌)
            {
                data.phase = DealPhase.Flipping;
                data.startTime = currentTime;
            }
            else
            {
                card.localRotation = Quaternion.identity;
                data.phase = DealPhase.Flipping;
                data.startTime = currentTime - 翻牌时长;
            }
        }
    }
    
    /// <summary>
    /// 编辑器模式下更新翻牌阶段
    /// </summary>
    private void UpdateFlippingPhaseEditor(Transform card, CardDealData data, float elapsed, List<Transform> completedCards)
    {
        float t = Mathf.Clamp01(elapsed / 翻牌时长);
        float easedT = 1f - Mathf.Pow(1f - t, 2f);
        
        // 实时更新位置跟随手牌排列
        data.targetPos = CalculateDealTargetPosition(card);
        card.localPosition = data.targetPos;
        
        float currentY = Mathf.Lerp(180f, 0f, easedT);
        card.localRotation = Quaternion.Euler(0, currentY, 0);
        
        // 在翻转到90度时切换贴图（此时卡牌侧面朝向玩家，切换不会被察觉）
        if (!data.hasSwappedToFront && currentY <= 90f)
        {
            if (data.mainSpriteRenderer != null && data.originalSprite != null)
            {
                data.mainSpriteRenderer.sprite = data.originalSprite;
            }
            data.hasSwappedToFront = true;
        }
        
        if (t >= 1f)
        {
            card.localRotation = Quaternion.identity;
            completedCards.Add(card);
        }
    }
    
    /// <summary>
    /// 获取排列轴上的距离
    /// </summary>
    private float GetAxisDistance(Vector3 a, Vector3 b)
    {
        switch (轴向)
        {
            case AlignAxis.X轴: return Mathf.Abs(a.x - b.x);
            case AlignAxis.Y轴: return Mathf.Abs(a.y - b.y);
            case AlignAxis.Z轴: return Mathf.Abs(a.z - b.z);
            default: return 0f;
        }
    }
    
    /// <summary>
    /// 只对排列轴进行插值
    /// </summary>
    private Vector3 LerpOnlyAxis(Vector3 current, Vector3 target, float t)
    {
        switch (轴向)
        {
            case AlignAxis.X轴:
                return new Vector3(Mathf.Lerp(current.x, target.x, t), current.y, current.z);
            case AlignAxis.Y轴:
                return new Vector3(current.x, Mathf.Lerp(current.y, target.y, t), current.z);
            case AlignAxis.Z轴:
                return new Vector3(current.x, current.y, Mathf.Lerp(current.z, target.z, t));
            default:
                return current;
        }
    }
    
    /// <summary>
    /// 只设置排列轴的值
    /// </summary>
    private Vector3 SetOnlyAxis(Vector3 current, Vector3 target)
    {
        switch (轴向)
        {
            case AlignAxis.X轴:
                return new Vector3(target.x, current.y, current.z);
            case AlignAxis.Y轴:
                return new Vector3(current.x, target.y, current.z);
            case AlignAxis.Z轴:
                return new Vector3(current.x, current.y, target.z);
            default:
                return current;
        }
    }
    
    /// <summary>
    /// 停止编辑器动画
    /// </summary>
    private void StopEditorAnimation()
    {
        isAnimatingInEditor = false;
        UnityEditor.EditorApplication.update -= EditorAnimationUpdate;
    }
    
    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            StopEditorAnimation();
            UnityEditor.EditorApplication.update -= CheckChildrenChangeInEditor;
            lastEditorHierarchyHash = -1;
            previousActiveChildrenEditor.Clear();
            previousDealActiveCards.Clear();
            dealingCards.Clear();
            cardDealAnimations.Clear();
        }
    }
    
    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            lastEditorHierarchyHash = CalculateHierarchyHash();
            // 初始化编辑器的激活记录
            previousActiveChildrenEditor.Clear();
            foreach (var child in GetActiveChildren())
            {
                previousActiveChildrenEditor.Add(child);
            }
            // 初始化发牌检测记录
            previousDealActiveCards.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                Transform dealChild = child.Find(发牌检测名称);
                if (dealChild != null && dealChild.gameObject.activeSelf)
                {
                    previousDealActiveCards.Add(child);
                }
            }
            UnityEditor.EditorApplication.update += CheckChildrenChangeInEditor;
        }
    }
    
    /// <summary>
    /// 编辑器模式下检测子对象层级变化
    /// </summary>
    private void CheckChildrenChangeInEditor()
    {
        if (this == null || Application.isPlaying)
        {
            UnityEditor.EditorApplication.update -= CheckChildrenChangeInEditor;
            return;
        }
        
        int currentHash = CalculateHierarchyHash();
        if (currentHash != lastEditorHierarchyHash)
        {
            lastEditorHierarchyHash = currentHash;
            UpdateAlignmentEditor();
            
            // 同时更新排序
            if (自动排序)
            {
                UpdateSortingOrder(GetActiveChildren());
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // 绘制排列预览
        var activeChildren = GetActiveChildren();
        if (activeChildren.Count == 0) return;
        
        Gizmos.color = Color.cyan;
        Vector3 center = transform.TransformPoint(GetCenterPosition());
        Gizmos.DrawWireSphere(center, 0.1f);
        
        // 绘制排列方向
        Gizmos.color = Color.yellow;
        Vector3 direction = Vector3.zero;
        switch (轴向)
        {
            case AlignAxis.X轴: direction = transform.right; break;
            case AlignAxis.Y轴: direction = transform.up; break;
            case AlignAxis.Z轴: direction = transform.forward; break;
        }
        Gizmos.DrawLine(center - direction * 2f, center + direction * 2f);
    }
#endif
}
