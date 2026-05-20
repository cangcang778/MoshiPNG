using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class Moshi_ParticleOrder : EditorWindow
{
    private const string TOOL_NAME = "粒子排序修改";
    
    // 排序模式枚举
    private enum SortMode { 统一偏移, 递增排序, 递减排序 }
    
    // Order in Layer 参数
    private bool modifyOrderInLayer = true;
    private int orderInLayerOffset = 0;
    private SortMode orderSortMode = SortMode.统一偏移;
    private int orderStepValue = 1;
    
    // Sorting Fudge 参数
    private bool modifySortingFudge = true;
    private float sortingFudgeOffset = 0f;
    private SortMode fudgeSortMode = SortMode.统一偏移;
    private float fudgeStepValue = 0.1f;
    
    // Start Delay 参数
    private bool modifyStartDelay = false;
    private float startDelayOffset = 0f;
    private SortMode delaySortMode = SortMode.统一偏移;
    private float delayStepValue = 0.1f;
    
    // Start Lifetime 参数
    private bool modifyStartLifetime = false;
    private float startLifetimeOffset = 0f;
    private SortMode lifetimeSortMode = SortMode.统一偏移;
    private float lifetimeStepValue = 0.1f;
    
    // 显示选项
    private bool showPreview = true;
    private Vector2 scrollPosition;
    private List<PreviewInfo> previewList = new List<PreviewInfo>();
    
    private class PreviewInfo
    {
        public string objectName;
        public int currentOrder;
        public int newOrder;
        public float currentFudge;
        public float newFudge;
        public float currentStartDelay;
        public float newStartDelay;
        public float currentStartLifetime;
        public float newStartLifetime;
    }

    [MenuItem("工具/Moshi/" + TOOL_NAME)]
    public static void ShowWindow()
    {
        GetWindow<Moshi_ParticleOrder>(TOOL_NAME);
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("粒子系统 Order in Layer & Sorting Fudge 增量修改工具", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME);
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10);

        EditorGUILayout.HelpBox("此工具会在原有数值基础上进行加减法修改，而非设置为统一数值。", MessageType.Info);
        GUILayout.Space(10);

        // ========== Order in Layer 设置区域 ==========
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Order in Layer 增量修改", EditorStyles.boldLabel);
        
        modifyOrderInLayer = EditorGUILayout.Toggle("启用 Order in Layer 修改", modifyOrderInLayer);
        
        if (modifyOrderInLayer)
        {
            EditorGUI.indentLevel++;
            
            orderSortMode = DrawSortModeButtons("排序模式:", orderSortMode);
            
            if (orderSortMode == SortMode.统一偏移)
            {
                orderInLayerOffset = EditorGUILayout.IntField("偏移量（增量值）:", orderInLayerOffset);
                
                if (orderInLayerOffset != 0)
                {
                    string operationText = orderInLayerOffset > 0 ? 
                        $"原有值 + {orderInLayerOffset}" : 
                        $"原有值 - {Mathf.Abs(orderInLayerOffset)}";
                    EditorGUILayout.HelpBox($"操作: {operationText}", MessageType.None);
                }
            }
            else
            {
                orderStepValue = EditorGUILayout.IntField("步长:", orderStepValue);
                
                string modeText = orderSortMode == SortMode.递增排序 ? "递增" : "递减";
                int sign = orderSortMode == SortMode.递增排序 ? 1 : -1;
                int step = sign * orderStepValue;
                EditorGUILayout.HelpBox($"累加模式: 原值{(step >= 0 ? "+" : "")}{step}, 上一个{(step >= 0 ? "+" : "")}{step}, ...", MessageType.None);
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);

        // ========== Sorting Fudge 设置区域 ==========
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Sorting Fudge 增量修改", EditorStyles.boldLabel);
        
        modifySortingFudge = EditorGUILayout.Toggle("启用 Sorting Fudge 修改", modifySortingFudge);
        
        if (modifySortingFudge)
        {
            EditorGUI.indentLevel++;
            
            fudgeSortMode = DrawSortModeButtons("排序模式:", fudgeSortMode);
            
            if (fudgeSortMode == SortMode.统一偏移)
            {
                sortingFudgeOffset = EditorGUILayout.FloatField("偏移量（增量值）:", sortingFudgeOffset);
                
                if (sortingFudgeOffset != 0)
                {
                    string operationText = sortingFudgeOffset > 0 ? 
                        $"原有值 + {sortingFudgeOffset:F3}" : 
                        $"原有值 - {Mathf.Abs(sortingFudgeOffset):F3}";
                    EditorGUILayout.HelpBox($"操作: {operationText}", MessageType.None);
                }
            }
            else
            {
                fudgeStepValue = EditorGUILayout.FloatField("步长:", fudgeStepValue);
                
                int sign = fudgeSortMode == SortMode.递增排序 ? 1 : -1;
                float step = sign * fudgeStepValue;
                EditorGUILayout.HelpBox($"累加模式: 原值{(step >= 0 ? "+" : "")}{step:F2}, 上一个{(step >= 0 ? "+" : "")}{step:F2}, ...", MessageType.None);
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);

        // ========== Start Delay 设置区域 ==========
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Start Delay 增量修改", EditorStyles.boldLabel);
        
        modifyStartDelay = EditorGUILayout.Toggle("启用 Start Delay 修改", modifyStartDelay);
        
        if (modifyStartDelay)
        {
            EditorGUI.indentLevel++;
            
            delaySortMode = DrawSortModeButtons("排序模式:", delaySortMode);
            
            if (delaySortMode == SortMode.统一偏移)
            {
                startDelayOffset = EditorGUILayout.FloatField("偏移量（增量值）:", startDelayOffset);
                
                if (startDelayOffset != 0)
                {
                    string operationText = startDelayOffset > 0 ? 
                        $"原有值 + {startDelayOffset:F3}" : 
                        $"原有值 - {Mathf.Abs(startDelayOffset):F3}";
                    EditorGUILayout.HelpBox($"操作: {operationText}", MessageType.None);
                }
            }
            else
            {
                delayStepValue = EditorGUILayout.FloatField("步长:", delayStepValue);
                
                int sign = delaySortMode == SortMode.递增排序 ? 1 : -1;
                float step = sign * delayStepValue;
                EditorGUILayout.HelpBox($"累加模式: 原值{(step >= 0 ? "+" : "")}{step:F2}, 上一个{(step >= 0 ? "+" : "")}{step:F2}, ...", MessageType.None);
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);

        // ========== Start Lifetime 设置区域 ==========
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Start Lifetime 增量修改", EditorStyles.boldLabel);
        
        modifyStartLifetime = EditorGUILayout.Toggle("启用 Start Lifetime 修改", modifyStartLifetime);
        
        if (modifyStartLifetime)
        {
            EditorGUI.indentLevel++;
            
            lifetimeSortMode = DrawSortModeButtons("排序模式:", lifetimeSortMode);
            
            if (lifetimeSortMode == SortMode.统一偏移)
            {
                startLifetimeOffset = EditorGUILayout.FloatField("偏移量（增量值）:", startLifetimeOffset);
                
                if (startLifetimeOffset != 0)
                {
                    string operationText = startLifetimeOffset > 0 ? 
                        $"原有值 + {startLifetimeOffset:F3}" : 
                        $"原有值 - {Mathf.Abs(startLifetimeOffset):F3}";
                    EditorGUILayout.HelpBox($"操作: {operationText}", MessageType.None);
                }
            }
            else
            {
                lifetimeStepValue = EditorGUILayout.FloatField("步长:", lifetimeStepValue);
                
                int sign = lifetimeSortMode == SortMode.递增排序 ? 1 : -1;
                float step = sign * lifetimeStepValue;
                EditorGUILayout.HelpBox($"累加模式: 原值{(step >= 0 ? "+" : "")}{step:F2}, 上一个{(step >= 0 ? "+" : "")}{step:F2}, ...", MessageType.None);
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);

        // ========== 预览开关 ==========
        showPreview = EditorGUILayout.Toggle("显示修改预览", showPreview);
        GUILayout.Space(10);

        // ========== 预览区域 ==========
        if (showPreview && Selection.gameObjects.Length > 0)
        {
            if (GUILayout.Button("刷新预览", GUILayout.Height(30)))
            {
                RefreshPreview();
            }
            
            GUILayout.Space(5);
            
            if (previewList.Count > 0)
            {
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label($"预览结果 (共 {previewList.Count} 个粒子系统)", EditorStyles.boldLabel);
                
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
                
                foreach (var info in previewList)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField($"对象: {info.objectName}", EditorStyles.boldLabel);
                    
                    if (modifyOrderInLayer)
                    {
                        Color originalColor = GUI.color;
                        GUI.color = info.currentOrder != info.newOrder ? Color.yellow : Color.white;
                        EditorGUILayout.LabelField($"  Order in Layer: {info.currentOrder} → {info.newOrder}");
                        GUI.color = originalColor;
                    }
                    
                    if (modifySortingFudge)
                    {
                        Color originalColor = GUI.color;
                        GUI.color = !Mathf.Approximately(info.currentFudge, info.newFudge) ? Color.yellow : Color.white;
                        EditorGUILayout.LabelField($"  Sorting Fudge: {info.currentFudge:F3} → {info.newFudge:F3}");
                        GUI.color = originalColor;
                    }
                    
                    if (modifyStartDelay)
                    {
                        Color originalColor = GUI.color;
                        GUI.color = !Mathf.Approximately(info.currentStartDelay, info.newStartDelay) ? Color.yellow : Color.white;
                        EditorGUILayout.LabelField($"  Start Delay: {info.currentStartDelay:F3} → {info.newStartDelay:F3}");
                        GUI.color = originalColor;
                    }
                    
                    if (modifyStartLifetime)
                    {
                        Color originalColor = GUI.color;
                        GUI.color = !Mathf.Approximately(info.currentStartLifetime, info.newStartLifetime) ? Color.yellow : Color.white;
                        EditorGUILayout.LabelField($"  Start Lifetime: {info.currentStartLifetime:F3} → {info.newStartLifetime:F3}");
                        GUI.color = originalColor;
                    }
                    
                    EditorGUILayout.EndVertical();
                    GUILayout.Space(2);
                }
                
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
            }
        }

        GUILayout.Space(10);

        // ========== 操作按钮 ==========
        EditorGUILayout.HelpBox("在Hierarchy中选择包含粒子系统的GameObject，然后点击下方按钮应用修改。", MessageType.Info);

        bool hasOrderChange = modifyOrderInLayer && (orderSortMode != SortMode.统一偏移 || orderInLayerOffset != 0);
        bool hasFudgeChange = modifySortingFudge && (fudgeSortMode != SortMode.统一偏移 || sortingFudgeOffset != 0);
        bool hasDelayChange = modifyStartDelay && (delaySortMode != SortMode.统一偏移 || startDelayOffset != 0);
        bool hasLifetimeChange = modifyStartLifetime && (lifetimeSortMode != SortMode.统一偏移 || startLifetimeOffset != 0);
        GUI.enabled = hasOrderChange || hasFudgeChange || hasDelayChange || hasLifetimeChange;
        
        if (GUILayout.Button("应用修改到选中对象", GUILayout.Height(40)))
        {
            ApplyModifications();
        }
        
        GUI.enabled = true;

        GUILayout.Space(10);

        // ========== 快捷按钮区域 ==========
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("快捷操作", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("重置所有参数", GUILayout.Height(30)))
        {
            orderInLayerOffset = 0;
            sortingFudgeOffset = 0f;
            startDelayOffset = 0f;
            startLifetimeOffset = 0f;
            orderSortMode = SortMode.统一偏移;
            fudgeSortMode = SortMode.统一偏移;
            delaySortMode = SortMode.统一偏移;
            lifetimeSortMode = SortMode.统一偏移;
            orderStepValue = 1;
            fudgeStepValue = 0.1f;
            delayStepValue = 0.1f;
            lifetimeStepValue = 0.1f;
            previewList.Clear();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
    }

    private void OnSelectionChange()
    {
        if (showPreview)
        {
            RefreshPreview();
            Repaint();
        }
    }

    private void RefreshPreview()
    {
        previewList.Clear();
        
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
            return;

        // 收集所有粒子系统并按Hierarchy顺序排序
        List<ParticleSystem> sortedParticleSystems = new List<ParticleSystem>();
        foreach (GameObject obj in selectedObjects)
        {
            ParticleSystem[] particleSystems = obj.GetComponentsInChildren<ParticleSystem>(true);
            sortedParticleSystems.AddRange(particleSystems);
        }
        
        // 按Hierarchy顺序排序（使用transform的sibling index和深度）
        sortedParticleSystems.Sort((a, b) => CompareHierarchyOrder(a.transform, b.transform));

        // 累加计算的中间值
        int lastOrderValue = 0;
        float lastFudgeValue = 0f;
        float lastDelayValue = 0f;
        float lastLifetimeValue = 0f;

        for (int i = 0; i < sortedParticleSystems.Count; i++)
        {
            ParticleSystem ps = sortedParticleSystems[i];
            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                var main = ps.main;
                PreviewInfo info = new PreviewInfo
                {
                    objectName = GetGameObjectPath(ps.gameObject),
                    currentOrder = renderer.sortingOrder,
                    newOrder = CalculateNewOrderValue(renderer.sortingOrder, i, ref lastOrderValue),
                    currentFudge = renderer.sortingFudge,
                    newFudge = CalculateNewFudgeValue(renderer.sortingFudge, i, ref lastFudgeValue),
                    currentStartDelay = main.startDelay.constant,
                    newStartDelay = CalculateNewDelayValue(main.startDelay.constant, i, ref lastDelayValue),
                    currentStartLifetime = main.startLifetime.constant,
                    newStartLifetime = CalculateNewLifetimeValue(main.startLifetime.constant, i, ref lastLifetimeValue)
                };
                previewList.Add(info);
            }
        }
    }

    private int CompareHierarchyOrder(Transform a, Transform b)
    {
        // 获取完整的层级路径索引
        List<int> pathA = GetHierarchyPath(a);
        List<int> pathB = GetHierarchyPath(b);
        
        int minLength = Mathf.Min(pathA.Count, pathB.Count);
        for (int i = 0; i < minLength; i++)
        {
            if (pathA[i] != pathB[i])
                return pathA[i].CompareTo(pathB[i]);
        }
        return pathA.Count.CompareTo(pathB.Count);
    }

    private List<int> GetHierarchyPath(Transform t)
    {
        List<int> path = new List<int>();
        while (t != null)
        {
            path.Insert(0, t.GetSiblingIndex());
            t = t.parent;
        }
        return path;
    }

    // 累加计算方法 - 基于上一个计算结果累加
    private int CalculateNewOrderValue(int currentValue, int index, ref int lastCalculatedValue)
    {
        if (orderSortMode == SortMode.统一偏移)
            return currentValue + orderInLayerOffset;
        
        int sign = orderSortMode == SortMode.递增排序 ? 1 : -1;
        if (index == 0)
        {
            // 第一个：原值 + 偏移值
            lastCalculatedValue = currentValue + sign * orderStepValue;
        }
        else
        {
            // 后续：上一个计算结果 + 偏移值
            lastCalculatedValue = lastCalculatedValue + sign * orderStepValue;
        }
        return lastCalculatedValue;
    }

    private float CalculateNewFudgeValue(float currentValue, int index, ref float lastCalculatedValue)
    {
        if (fudgeSortMode == SortMode.统一偏移)
            return currentValue + sortingFudgeOffset;
        
        int sign = fudgeSortMode == SortMode.递增排序 ? 1 : -1;
        if (index == 0)
        {
            lastCalculatedValue = currentValue + sign * fudgeStepValue;
        }
        else
        {
            lastCalculatedValue = lastCalculatedValue + sign * fudgeStepValue;
        }
        return lastCalculatedValue;
    }

    private float CalculateNewDelayValue(float currentValue, int index, ref float lastCalculatedValue)
    {
        if (delaySortMode == SortMode.统一偏移)
            return Mathf.Max(0, currentValue + startDelayOffset);
        
        int sign = delaySortMode == SortMode.递增排序 ? 1 : -1;
        if (index == 0)
        {
            lastCalculatedValue = currentValue + sign * delayStepValue;
        }
        else
        {
            lastCalculatedValue = lastCalculatedValue + sign * delayStepValue;
        }
        return Mathf.Max(0, lastCalculatedValue);
    }

    private float CalculateNewLifetimeValue(float currentValue, int index, ref float lastCalculatedValue)
    {
        if (lifetimeSortMode == SortMode.统一偏移)
            return Mathf.Max(0.001f, currentValue + startLifetimeOffset);
        
        int sign = lifetimeSortMode == SortMode.递增排序 ? 1 : -1;
        if (index == 0)
        {
            lastCalculatedValue = currentValue + sign * lifetimeStepValue;
        }
        else
        {
            lastCalculatedValue = lastCalculatedValue + sign * lifetimeStepValue;
        }
        return Mathf.Max(0.001f, lastCalculatedValue);
    }

    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        
        while (parent != null && !Selection.Contains(parent.gameObject))
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }

    private void ApplyModifications()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("错误", "请先在Hierarchy中选择至少一个GameObject", "确定");
            return;
        }

        if (!modifyOrderInLayer && !modifySortingFudge && !modifyStartDelay && !modifyStartLifetime)
        {
            EditorUtility.DisplayDialog("提示", "请至少启用一个修改选项", "确定");
            return;
        }

        // 检查是否有有效的修改
        bool hasOrderChange = modifyOrderInLayer && (orderSortMode != SortMode.统一偏移 || orderInLayerOffset != 0);
        bool hasFudgeChange = modifySortingFudge && (fudgeSortMode != SortMode.统一偏移 || sortingFudgeOffset != 0);
        bool hasDelayChange = modifyStartDelay && (delaySortMode != SortMode.统一偏移 || startDelayOffset != 0);
        bool hasLifetimeChange = modifyStartLifetime && (lifetimeSortMode != SortMode.统一偏移 || startLifetimeOffset != 0);
        
        if (!hasOrderChange && !hasFudgeChange && !hasDelayChange && !hasLifetimeChange)
        {
            EditorUtility.DisplayDialog("提示", "请设置有效的修改参数", "确定");
            return;
        }

        // 收集所有粒子系统并按Hierarchy顺序排序
        List<ParticleSystem> sortedParticleSystems = new List<ParticleSystem>();
        foreach (GameObject obj in selectedObjects)
        {
            ParticleSystem[] particleSystems = obj.GetComponentsInChildren<ParticleSystem>(true);
            sortedParticleSystems.AddRange(particleSystems);
        }
        sortedParticleSystems.Sort((a, b) => CompareHierarchyOrder(a.transform, b.transform));

        int modifiedCount = 0;
        int totalSystems = sortedParticleSystems.Count;
        List<string> modificationLog = new List<string>();

        Undo.RecordObjects(selectedObjects, "批量修改粒子系统 Order & Fudge");

        // 累加计算的中间值
        int lastOrderValue = 0;
        float lastFudgeValue = 0f;
        float lastDelayValue = 0f;
        float lastLifetimeValue = 0f;

        for (int i = 0; i < sortedParticleSystems.Count; i++)
        {
            ParticleSystem ps = sortedParticleSystems[i];
            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            
            if (renderer != null)
            {
                Undo.RecordObject(renderer, "修改粒子系统渲染器");
                
                bool modified = false;
                string logMessage = $"'{GetGameObjectPath(ps.gameObject)}': ";
                List<string> changes = new List<string>();

                // 修改 Order in Layer
                if (hasOrderChange)
                {
                    int oldOrder = renderer.sortingOrder;
                    int newOrder = CalculateNewOrderValue(oldOrder, i, ref lastOrderValue);
                    renderer.sortingOrder = newOrder;
                    
                    changes.Add($"Order {oldOrder} → {newOrder}");
                    modified = true;
                }

                // 修改 Sorting Fudge
                if (hasFudgeChange)
                {
                    float oldFudge = renderer.sortingFudge;
                    float newFudge = CalculateNewFudgeValue(oldFudge, i, ref lastFudgeValue);
                    renderer.sortingFudge = newFudge;
                    
                    changes.Add($"Fudge {oldFudge:F3} → {newFudge:F3}");
                    modified = true;
                }

                // 修改 Start Delay
                if (hasDelayChange)
                {
                    Undo.RecordObject(ps, "修改粒子系统 Start Delay");
                    var main = ps.main;
                    float oldDelay = main.startDelay.constant;
                    float newDelay = CalculateNewDelayValue(oldDelay, i, ref lastDelayValue);
                    
                    var newStartDelay = main.startDelay;
                    newStartDelay.constant = newDelay;
                    main.startDelay = newStartDelay;
                    
                    changes.Add($"StartDelay {oldDelay:F3} → {newDelay:F3}");
                    modified = true;
                }

                // 修改 Start Lifetime
                if (hasLifetimeChange)
                {
                    Undo.RecordObject(ps, "修改粒子系统 Start Lifetime");
                    var main = ps.main;
                    float oldLifetime = main.startLifetime.constant;
                    float newLifetime = CalculateNewLifetimeValue(oldLifetime, i, ref lastLifetimeValue);
                    
                    var newStartLifetime = main.startLifetime;
                    newStartLifetime.constant = newLifetime;
                    main.startLifetime = newStartLifetime;
                    
                    changes.Add($"StartLifetime {oldLifetime:F3} → {newLifetime:F3}");
                    modified = true;
                }

                if (modified)
                {
                    modifiedCount++;
                    logMessage += string.Join(", ", changes.ToArray());
                    modificationLog.Add(logMessage);
                    Debug.Log("粒子系统修改: " + logMessage);
                    EditorUtility.SetDirty(ps.gameObject);
                }
            }
        }
        
        // 标记场景为脏
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        // 生成结果消息
        string resultMessage = $"修改完成!\n\n总粒子系统: {totalSystems}\n成功修改: {modifiedCount}\n未修改: {totalSystems - modifiedCount}\n";
        
        if (hasOrderChange)
        {
            string modeText = orderSortMode == SortMode.统一偏移 ? $"偏移 {(orderInLayerOffset >= 0 ? "+" : "")}{orderInLayerOffset}" : 
                              $"{orderSortMode} (步长:{orderStepValue})";
            resultMessage += $"\nOrder in Layer: {modeText}";
        }
        
        if (hasFudgeChange)
        {
            string modeText = fudgeSortMode == SortMode.统一偏移 ? $"偏移 {(sortingFudgeOffset >= 0 ? "+" : "")}{sortingFudgeOffset:F3}" : 
                              $"{fudgeSortMode} (步长:{fudgeStepValue:F2})";
            resultMessage += $"\nSorting Fudge: {modeText}";
        }
        
        if (hasDelayChange)
        {
            string modeText = delaySortMode == SortMode.统一偏移 ? $"偏移 {(startDelayOffset >= 0 ? "+" : "")}{startDelayOffset:F3}" : 
                              $"{delaySortMode} (步长:{delayStepValue:F2})";
            resultMessage += $"\nStart Delay: {modeText}";
        }
        
        if (hasLifetimeChange)
        {
            string modeText = lifetimeSortMode == SortMode.统一偏移 ? $"偏移 {(startLifetimeOffset >= 0 ? "+" : "")}{startLifetimeOffset:F3}" : 
                              $"{lifetimeSortMode} (步长:{lifetimeStepValue:F2})";
            resultMessage += $"\nStart Lifetime: {modeText}";
        }

        if (modificationLog.Count > 0 && modificationLog.Count <= 15)
        {
            resultMessage += "\n\n修改详情:\n" + string.Join("\n", modificationLog.ToArray());
        }
        else if (modificationLog.Count > 15)
        {
            resultMessage += "\n\n(详细修改信息请查看Console)";
        }

        EditorUtility.DisplayDialog("完成", resultMessage, "确定");
        
        // 刷新预览
        if (showPreview)
        {
            RefreshPreview();
        }
    }

    /// <summary>
    /// 绘制排序模式的并排按钮
    /// </summary>
    private SortMode DrawSortModeButtons(string label, SortMode currentMode)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(60));
        
        string[] modeNames = { "偏移", "递增", "递减" };
        SortMode[] modeValues = { SortMode.统一偏移, SortMode.递增排序, SortMode.递减排序 };
        
        for (int i = 0; i < modeNames.Length; i++)
        {
            GUIStyle style = new GUIStyle(EditorStyles.miniButtonMid);
            if (i == 0) style = new GUIStyle(EditorStyles.miniButtonLeft);
            if (i == modeNames.Length - 1) style = new GUIStyle(EditorStyles.miniButtonRight);
            
            bool isSelected = (currentMode == modeValues[i]);
            
            Color originalBg = GUI.backgroundColor;
            if (isSelected)
            {
                GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
            }
            
            if (GUILayout.Toggle(isSelected, modeNames[i], style, GUILayout.Width(50), GUILayout.Height(18)) && !isSelected)
            {
                currentMode = modeValues[i];
            }
            
            GUI.backgroundColor = originalBg;
        }
        
        EditorGUILayout.EndHorizontal();
        
        return currentMode;
    }
}
