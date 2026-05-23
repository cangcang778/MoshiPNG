using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class Moshi_GOArray : EditorWindow
{
    // 布局模式枚举
    private enum LayoutMode { 线性阵列, 网格阵列, 矩阵阵列, 环形阵列, 螺旋阵列, 路径阵列 }
    // 操作模式枚举
    private enum OperationMode { 重排模式, 复制模式 }
    
    private LayoutMode layoutMode = LayoutMode.线性阵列;
    private OperationMode operationMode = OperationMode.复制模式;
    
    // 源对象（复制模式）
    private GameObject sourceObject;
    
    // 通用参数
    private int copyCount = 5;
    private Transform parentTransform;
    
    // 线性阵列参数
    private Vector3 linearSpacing = new Vector3(1f, 0f, 0f);
    
    // 网格阵列参数 (2D)
    private int gridColumns = 3;
    private int gridRows = 3;
    private Vector2 gridSpacing = new Vector2(1f, 1f);
    private enum GridPlane { XY, XZ, YZ }
    private GridPlane gridPlane = GridPlane.XZ;
    
    // 3D矩阵阵列参数
    private Vector3Int matrixSize = new Vector3Int(2, 2, 2);
    private Vector3 matrixSpacing = new Vector3(1f, 1f, 1f);
    
    // 环形阵列参数
    private float circleRadius = 5f;
    private int circleCount = 8;
    private float circleStartAngle = 0f;
    private float circleEndAngle = 360f;
    private bool circleFaceCenter = true;
    private enum CirclePlane { XY, XZ, YZ }
    private CirclePlane circlePlane = CirclePlane.XZ;
    
    // 螺旋阵列参数
    private float spiralRadius = 5f;
    private float spiralHeight = 10f;
    private int spiralCount = 20;
    private float spiralTurns = 3f;
    private bool spiralFaceCenter = true;
    
    // 路径阵列参数
    private List<Transform> pathPoints = new List<Transform>();
    private int pathCount = 10;
    private bool pathAlignToDirection = true;
    private bool pathClosedLoop = false;
    
    // 随机偏移参数
    private bool enableRandomOffset = false;
    private Vector3 randomPositionRange = Vector3.zero;
    private Vector3 randomRotationRange = Vector3.zero;
    private Vector3 randomScaleRange = Vector3.zero;
    private bool uniformRandomScale = true;
    
    // 增量变换参数
    private bool enableIncrementalTransform = false;
    private Vector3 incrementalRotation = Vector3.zero;
    private Vector3 incrementalScale = Vector3.zero;
    
    // 预览
    private bool showPreview = true;
    private List<Vector3> previewPositions = new List<Vector3>();
    private List<Quaternion> previewRotations = new List<Quaternion>();
    
    // 滚动位置
    private Vector2 scrollPosition;
    
    // 折叠状态
    private bool foldoutRandom = false;
    private bool foldoutIncremental = false;
    private bool foldoutPath = false;
    
    private const string TOOL_NAME = "物体阵列工具";

    [MenuItem("工具/Moshi/" + TOOL_NAME)]
    public static void ShowWindow()
    {
        var window = GetWindow<Moshi_GOArray>(TOOL_NAME);
        window.minSize = new Vector2(350, 500);
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(TOOL_NAME, EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME);
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
        
        // 操作模式选择
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("操作模式", EditorStyles.boldLabel);
        operationMode = DrawOperationModeButtons(operationMode);
        
        if (operationMode == OperationMode.复制模式)
        {
            sourceObject = (GameObject)EditorGUILayout.ObjectField("源对象", sourceObject, typeof(GameObject), true);
            if (sourceObject == null)
            {
                EditorGUILayout.HelpBox("请指定要复制的源对象", MessageType.Warning);
            }
        }
        
        // 复制模式才显示父对象字段
        if (operationMode == OperationMode.复制模式)
        {
            parentTransform = (Transform)EditorGUILayout.ObjectField("父对象", parentTransform, typeof(Transform), true);
        }
        
        if (operationMode == OperationMode.重排模式)
        {
            // 从 Hierarchy 选中获取父对象
            GameObject selectedParent = Selection.activeGameObject;
            if (selectedParent == null)
            {
                EditorGUILayout.HelpBox("请在 Hierarchy 中选择一个父对象", MessageType.Warning);
            }
            else if (selectedParent.transform.childCount == 0)
            {
                EditorGUILayout.HelpBox($"选中对象 [{selectedParent.name}] 没有子对象", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox($"将重排 [{selectedParent.name}] 下的 {selectedParent.transform.childCount} 个子对象", MessageType.Info);
            }
        }
        EditorGUILayout.EndVertical();
        
        GUILayout.Space(5);
        
        // 布局模式选择
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("布局模式", EditorStyles.boldLabel);
        layoutMode = DrawLayoutModeButtons(layoutMode);
        GUILayout.Space(5);
        
        // 根据布局模式显示不同参数
        switch (layoutMode)
        {
            case LayoutMode.线性阵列:
                DrawLinearArrayParams();
                break;
            case LayoutMode.网格阵列:
                DrawGridArrayParams();
                break;
            case LayoutMode.矩阵阵列:
                Draw3DMatrixArrayParams();
                break;
            case LayoutMode.环形阵列:
                DrawCircleArrayParams();
                break;
            case LayoutMode.螺旋阵列:
                DrawSpiralArrayParams();
                break;
            case LayoutMode.路径阵列:
                DrawPathArrayParams();
                break;
        }
        EditorGUILayout.EndVertical();
        
        GUILayout.Space(5);
        
        // 随机偏移设置
        foldoutRandom = EditorGUILayout.Foldout(foldoutRandom, "随机偏移设置", true);
        if (foldoutRandom)
        {
            EditorGUILayout.BeginVertical("box");
            enableRandomOffset = EditorGUILayout.Toggle("启用随机偏移", enableRandomOffset);
            if (enableRandomOffset)
            {
                EditorGUI.indentLevel++;
                randomPositionRange = EditorGUILayout.Vector3Field("位置随机范围 (±)", randomPositionRange);
                randomRotationRange = EditorGUILayout.Vector3Field("旋转随机范围 (±)", randomRotationRange);
                uniformRandomScale = EditorGUILayout.Toggle("统一缩放随机", uniformRandomScale);
                if (uniformRandomScale)
                {
                    float scaleRange = EditorGUILayout.FloatField("缩放随机范围 (±)", randomScaleRange.x);
                    randomScaleRange = new Vector3(scaleRange, scaleRange, scaleRange);
                }
                else
                {
                    randomScaleRange = EditorGUILayout.Vector3Field("缩放随机范围 (±)", randomScaleRange);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }
        
        // 增量变换设置
        foldoutIncremental = EditorGUILayout.Foldout(foldoutIncremental, "增量变换设置", true);
        if (foldoutIncremental)
        {
            EditorGUILayout.BeginVertical("box");
            enableIncrementalTransform = EditorGUILayout.Toggle("启用增量变换", enableIncrementalTransform);
            if (enableIncrementalTransform)
            {
                EditorGUI.indentLevel++;
                incrementalRotation = EditorGUILayout.Vector3Field("每个增量旋转", incrementalRotation);
                incrementalScale = EditorGUILayout.Vector3Field("每个增量缩放", incrementalScale);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }
        
        GUILayout.Space(10);
        
        // 预览开关
        showPreview = EditorGUILayout.Toggle("场景预览", showPreview);
        
        GUILayout.Space(10);
        
        // 执行按钮
        EditorGUI.BeginDisabledGroup(!CanExecute());
        if (GUILayout.Button("执行阵列", GUILayout.Height(30)))
        {
            ExecuteArray();
        }
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.EndScrollView();
        
        // 更新预览
        if (showPreview)
        {
            UpdatePreview();
            SceneView.RepaintAll();
        }
    }

    private OperationMode DrawOperationModeButtons(OperationMode currentMode)
    {
        EditorGUILayout.BeginHorizontal();
        
        string[] modeNames = { "重排模式", "复制模式" };
        OperationMode[] modeValues = { OperationMode.重排模式, OperationMode.复制模式 };
        
        for (int i = 0; i < modeNames.Length; i++)
        {
            GUIStyle style = i == 0 ? EditorStyles.miniButtonLeft : EditorStyles.miniButtonRight;
            bool isSelected = (currentMode == modeValues[i]);
            
            Color originalBg = GUI.backgroundColor;
            if (isSelected) GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            
            if (GUILayout.Toggle(isSelected, modeNames[i], style, GUILayout.Height(22)))
            {
                if (!isSelected) currentMode = modeValues[i];
            }
            
            GUI.backgroundColor = originalBg;
        }
        
        EditorGUILayout.EndHorizontal();
        return currentMode;
    }

    /// <summary>
    /// 获取重排模式下的目标对象（从 Hierarchy 选中的父对象的所有子对象）
    /// </summary>
    private GameObject[] GetRearrangeTargetObjects()
    {
        GameObject selectedParent = Selection.activeGameObject;
        if (selectedParent == null)
            return new GameObject[0];
        
        List<GameObject> children = new List<GameObject>();
        for (int i = 0; i < selectedParent.transform.childCount; i++)
        {
            children.Add(selectedParent.transform.GetChild(i).gameObject);
        }
        return children.ToArray();
    }

    private LayoutMode DrawLayoutModeButtons(LayoutMode currentMode)
    {
        string[] modeNames = { "线性", "网格", "矩阵", "环形", "螺旋", "路径" };
        LayoutMode[] modeValues = { LayoutMode.线性阵列, LayoutMode.网格阵列, LayoutMode.矩阵阵列, 
                                     LayoutMode.环形阵列, LayoutMode.螺旋阵列, LayoutMode.路径阵列 };
        
        // 第一行：前3个
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < 3; i++)
        {
            GUIStyle style = EditorStyles.miniButtonMid;
            if (i == 0) style = EditorStyles.miniButtonLeft;
            if (i == 2) style = EditorStyles.miniButtonRight;
            
            bool isSelected = (currentMode == modeValues[i]);
            Color originalBg = GUI.backgroundColor;
            if (isSelected) GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            
            if (GUILayout.Toggle(isSelected, modeNames[i], style, GUILayout.Height(22)))
            {
                if (!isSelected) currentMode = modeValues[i];
            }
            
            GUI.backgroundColor = originalBg;
        }
        EditorGUILayout.EndHorizontal();
        
        // 第二行：后3个
        EditorGUILayout.BeginHorizontal();
        for (int i = 3; i < 6; i++)
        {
            GUIStyle style = EditorStyles.miniButtonMid;
            if (i == 3) style = EditorStyles.miniButtonLeft;
            if (i == 5) style = EditorStyles.miniButtonRight;
            
            bool isSelected = (currentMode == modeValues[i]);
            Color originalBg = GUI.backgroundColor;
            if (isSelected) GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            
            if (GUILayout.Toggle(isSelected, modeNames[i], style, GUILayout.Height(22)))
            {
                if (!isSelected) currentMode = modeValues[i];
            }
            
            GUI.backgroundColor = originalBg;
        }
        EditorGUILayout.EndHorizontal();
        
        return currentMode;
    }

    private void DrawLinearArrayParams()
    {
        GUILayout.Label("线性阵列参数", EditorStyles.miniBoldLabel);
        if (operationMode == OperationMode.复制模式)
        {
            copyCount = EditorGUILayout.IntField("复制数量", copyCount);
            copyCount = Mathf.Max(1, copyCount);
        }
        linearSpacing = EditorGUILayout.Vector3Field("间距", linearSpacing);
    }

    private void DrawGridArrayParams()
    {
        GUILayout.Label("网格阵列参数 (2D)", EditorStyles.miniBoldLabel);
        gridColumns = EditorGUILayout.IntField("列数", gridColumns);
        gridRows = EditorGUILayout.IntField("行数", gridRows);
        gridColumns = Mathf.Max(1, gridColumns);
        gridRows = Mathf.Max(1, gridRows);
        gridSpacing = EditorGUILayout.Vector2Field("间距", gridSpacing);
        gridPlane = (GridPlane)EditorGUILayout.EnumPopup("平面", gridPlane);
        
        int totalCount = gridColumns * gridRows;
        EditorGUILayout.HelpBox($"总数量: {totalCount}", MessageType.None);
    }

    private void Draw3DMatrixArrayParams()
    {
        GUILayout.Label("3D矩阵阵列参数", EditorStyles.miniBoldLabel);
        matrixSize = EditorGUILayout.Vector3IntField("矩阵尺寸 (X,Y,Z)", matrixSize);
        matrixSize = new Vector3Int(Mathf.Max(1, matrixSize.x), Mathf.Max(1, matrixSize.y), Mathf.Max(1, matrixSize.z));
        matrixSpacing = EditorGUILayout.Vector3Field("间距", matrixSpacing);
        
        int totalCount = matrixSize.x * matrixSize.y * matrixSize.z;
        EditorGUILayout.HelpBox($"总数量: {totalCount}", MessageType.None);
    }

    private void DrawCircleArrayParams()
    {
        GUILayout.Label("环形阵列参数", EditorStyles.miniBoldLabel);
        circleCount = EditorGUILayout.IntField("数量", circleCount);
        circleCount = Mathf.Max(1, circleCount);
        circleRadius = EditorGUILayout.FloatField("半径", circleRadius);
        circleStartAngle = EditorGUILayout.FloatField("起始角度", circleStartAngle);
        circleEndAngle = EditorGUILayout.FloatField("结束角度", circleEndAngle);
        circlePlane = (CirclePlane)EditorGUILayout.EnumPopup("平面", circlePlane);
        circleFaceCenter = EditorGUILayout.Toggle("朝向中心", circleFaceCenter);
    }

    private void DrawSpiralArrayParams()
    {
        GUILayout.Label("螺旋阵列参数", EditorStyles.miniBoldLabel);
        spiralCount = EditorGUILayout.IntField("数量", spiralCount);
        spiralCount = Mathf.Max(1, spiralCount);
        spiralRadius = EditorGUILayout.FloatField("半径", spiralRadius);
        spiralHeight = EditorGUILayout.FloatField("高度", spiralHeight);
        spiralTurns = EditorGUILayout.FloatField("圈数", spiralTurns);
        spiralFaceCenter = EditorGUILayout.Toggle("朝向中心", spiralFaceCenter);
    }

    private void DrawPathArrayParams()
    {
        GUILayout.Label("路径阵列参数", EditorStyles.miniBoldLabel);
        
        foldoutPath = EditorGUILayout.Foldout(foldoutPath, $"路径点 ({pathPoints.Count})", true);
        if (foldoutPath)
        {
            EditorGUI.indentLevel++;
            
            int removeIndex = -1;
            for (int i = 0; i < pathPoints.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                pathPoints[i] = (Transform)EditorGUILayout.ObjectField($"点 {i}", pathPoints[i], typeof(Transform), true);
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    removeIndex = i;
                }
                EditorGUILayout.EndHorizontal();
            }
            
            if (removeIndex >= 0)
            {
                pathPoints.RemoveAt(removeIndex);
            }
            
            if (GUILayout.Button("添加路径点"))
            {
                pathPoints.Add(null);
            }
            
            if (GUILayout.Button("从选中对象添加"))
            {
                foreach (var go in Selection.gameObjects)
                {
                    if (go != null)
                    {
                        pathPoints.Add(go.transform);
                    }
                }
            }
            
            EditorGUI.indentLevel--;
        }
        
        pathCount = EditorGUILayout.IntField("数量", pathCount);
        pathCount = Mathf.Max(1, pathCount);
        pathClosedLoop = EditorGUILayout.Toggle("闭合路径", pathClosedLoop);
        pathAlignToDirection = EditorGUILayout.Toggle("沿路径方向", pathAlignToDirection);
    }

    private bool CanExecute()
    {
        if (operationMode == OperationMode.复制模式 && sourceObject == null)
            return false;
        
        if (operationMode == OperationMode.重排模式)
        {
            GameObject selectedParent = Selection.activeGameObject;
            if (selectedParent == null || selectedParent.transform.childCount == 0)
                return false;
        }
        
        if (layoutMode == LayoutMode.路径阵列 && GetValidPathPoints().Count < 2)
            return false;
        
        return true;
    }

    private List<Transform> GetValidPathPoints()
    {
        List<Transform> valid = new List<Transform>();
        foreach (var p in pathPoints)
        {
            if (p != null) valid.Add(p);
        }
        return valid;
    }

    private void UpdatePreview()
    {
        previewPositions.Clear();
        previewRotations.Clear();
        
        Vector3 basePosition = Vector3.zero;
        if (operationMode == OperationMode.复制模式 && sourceObject != null)
        {
            basePosition = sourceObject.transform.position;
        }
        else if (operationMode == OperationMode.重排模式 && Selection.activeGameObject != null)
        {
            basePosition = Selection.activeGameObject.transform.position;
        }
        
        CalculateArrayPositions(basePosition, previewPositions, previewRotations);
    }

    private void CalculateArrayPositions(Vector3 basePosition, List<Vector3> positions, List<Quaternion> rotations)
    {
        switch (layoutMode)
        {
            case LayoutMode.线性阵列:
                CalculateLinearPositions(basePosition, positions, rotations);
                break;
            case LayoutMode.网格阵列:
                CalculateGridPositions(basePosition, positions, rotations);
                break;
            case LayoutMode.矩阵阵列:
                Calculate3DMatrixPositions(basePosition, positions, rotations);
                break;
            case LayoutMode.环形阵列:
                CalculateCirclePositions(basePosition, positions, rotations);
                break;
            case LayoutMode.螺旋阵列:
                CalculateSpiralPositions(basePosition, positions, rotations);
                break;
            case LayoutMode.路径阵列:
                CalculatePathPositions(positions, rotations);
                break;
        }
    }

    private void CalculateLinearPositions(Vector3 basePosition, List<Vector3> positions, List<Quaternion> rotations)
    {
        int count = operationMode == OperationMode.复制模式 ? copyCount : GetRearrangeTargetCount();
        for (int i = 0; i < count; i++)
        {
            positions.Add(basePosition + linearSpacing * i);
            rotations.Add(Quaternion.identity);
        }
    }
    
    /// <summary>
    /// 获取重排模式下的目标对象数量
    /// </summary>
    private int GetRearrangeTargetCount()
    {
        GameObject selectedParent = Selection.activeGameObject;
        if (selectedParent == null)
            return 0;
        return selectedParent.transform.childCount;
    }

    private void CalculateGridPositions(Vector3 basePosition, List<Vector3> positions, List<Quaternion> rotations)
    {
        for (int row = 0; row < gridRows; row++)
        {
            for (int col = 0; col < gridColumns; col++)
            {
                Vector3 offset = Vector3.zero;
                switch (gridPlane)
                {
                    case GridPlane.XY:
                        offset = new Vector3(col * gridSpacing.x, row * gridSpacing.y, 0);
                        break;
                    case GridPlane.XZ:
                        offset = new Vector3(col * gridSpacing.x, 0, row * gridSpacing.y);
                        break;
                    case GridPlane.YZ:
                        offset = new Vector3(0, col * gridSpacing.x, row * gridSpacing.y);
                        break;
                }
                positions.Add(basePosition + offset);
                rotations.Add(Quaternion.identity);
            }
        }
    }

    private void Calculate3DMatrixPositions(Vector3 basePosition, List<Vector3> positions, List<Quaternion> rotations)
    {
        for (int z = 0; z < matrixSize.z; z++)
        {
            for (int y = 0; y < matrixSize.y; y++)
            {
                for (int x = 0; x < matrixSize.x; x++)
                {
                    Vector3 offset = new Vector3(x * matrixSpacing.x, y * matrixSpacing.y, z * matrixSpacing.z);
                    positions.Add(basePosition + offset);
                    rotations.Add(Quaternion.identity);
                }
            }
        }
    }

    private void CalculateCirclePositions(Vector3 basePosition, List<Vector3> positions, List<Quaternion> rotations)
    {
        float angleRange = circleEndAngle - circleStartAngle;
        bool fullCircle = Mathf.Abs(angleRange) >= 360f;
        int divider = fullCircle ? circleCount : Mathf.Max(1, circleCount - 1);
        
        for (int i = 0; i < circleCount; i++)
        {
            float angle = circleStartAngle + (angleRange / divider) * i;
            float rad = angle * Mathf.Deg2Rad;
            
            Vector3 offset = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            
            switch (circlePlane)
            {
                case CirclePlane.XZ:
                    offset = new Vector3(Mathf.Cos(rad) * circleRadius, 0, Mathf.Sin(rad) * circleRadius);
                    if (circleFaceCenter)
                        rot = Quaternion.LookRotation(-offset.normalized, Vector3.up);
                    break;
                case CirclePlane.XY:
                    offset = new Vector3(Mathf.Cos(rad) * circleRadius, Mathf.Sin(rad) * circleRadius, 0);
                    if (circleFaceCenter)
                        rot = Quaternion.LookRotation(Vector3.forward, -offset.normalized);
                    break;
                case CirclePlane.YZ:
                    offset = new Vector3(0, Mathf.Cos(rad) * circleRadius, Mathf.Sin(rad) * circleRadius);
                    if (circleFaceCenter)
                        rot = Quaternion.LookRotation(-offset.normalized, Vector3.up);
                    break;
            }
            
            positions.Add(basePosition + offset);
            rotations.Add(rot);
        }
    }

    private void CalculateSpiralPositions(Vector3 basePosition, List<Vector3> positions, List<Quaternion> rotations)
    {
        for (int i = 0; i < spiralCount; i++)
        {
            float t = (float)i / Mathf.Max(1, spiralCount - 1);
            float angle = t * spiralTurns * 360f * Mathf.Deg2Rad;
            float height = t * spiralHeight;
            
            Vector3 offset = new Vector3(Mathf.Cos(angle) * spiralRadius, height, Mathf.Sin(angle) * spiralRadius);
            
            Quaternion rot = Quaternion.identity;
            if (spiralFaceCenter)
            {
                Vector3 dirToCenter = new Vector3(-offset.x, 0, -offset.z).normalized;
                if (dirToCenter != Vector3.zero)
                    rot = Quaternion.LookRotation(dirToCenter, Vector3.up);
            }
            
            positions.Add(basePosition + offset);
            rotations.Add(rot);
        }
    }

    private void CalculatePathPositions(List<Vector3> positions, List<Quaternion> rotations)
    {
        List<Transform> validPoints = GetValidPathPoints();
        if (validPoints.Count < 2) return;
        
        // 构建路径点列表
        List<Vector3> pathPositions = new List<Vector3>();
        foreach (var p in validPoints)
        {
            pathPositions.Add(p.position);
        }
        
        if (pathClosedLoop && pathPositions.Count > 0)
        {
            pathPositions.Add(pathPositions[0]);
        }
        
        // 计算路径总长度
        float totalLength = 0f;
        List<float> segmentLengths = new List<float>();
        for (int i = 0; i < pathPositions.Count - 1; i++)
        {
            float len = Vector3.Distance(pathPositions[i], pathPositions[i + 1]);
            segmentLengths.Add(len);
            totalLength += len;
        }
        
        if (totalLength <= 0) return;
        
        // 沿路径均匀分布
        for (int i = 0; i < pathCount; i++)
        {
            float t = (float)i / Mathf.Max(1, pathCount - 1);
            float targetDist = t * totalLength;
            
            // 找到对应的路径段
            float accumulatedDist = 0f;
            Vector3 pos = pathPositions[0];
            Vector3 dir = Vector3.forward;
            
            for (int j = 0; j < segmentLengths.Count; j++)
            {
                if (accumulatedDist + segmentLengths[j] >= targetDist)
                {
                    float segmentT = (targetDist - accumulatedDist) / segmentLengths[j];
                    pos = Vector3.Lerp(pathPositions[j], pathPositions[j + 1], segmentT);
                    dir = (pathPositions[j + 1] - pathPositions[j]).normalized;
                    break;
                }
                accumulatedDist += segmentLengths[j];
            }
            
            positions.Add(pos);
            
            Quaternion rot = Quaternion.identity;
            if (pathAlignToDirection && dir != Vector3.zero)
            {
                rot = Quaternion.LookRotation(dir, Vector3.up);
            }
            rotations.Add(rot);
        }
    }

    private void ExecuteArray()
    {
        List<Vector3> positions = new List<Vector3>();
        List<Quaternion> rotations = new List<Quaternion>();
        
        Vector3 basePosition = Vector3.zero;
        Quaternion baseRotation = Quaternion.identity;
        Vector3 baseScale = Vector3.one;
        
        if (operationMode == OperationMode.复制模式 && sourceObject != null)
        {
            basePosition = sourceObject.transform.position;
            baseRotation = sourceObject.transform.rotation;
            baseScale = sourceObject.transform.localScale;
        }
        else if (operationMode == OperationMode.重排模式 && Selection.activeGameObject != null)
        {
            basePosition = Selection.activeGameObject.transform.position;
        }
        
        CalculateArrayPositions(basePosition, positions, rotations);
        
        if (operationMode == OperationMode.复制模式)
        {
            ExecuteCopyMode(positions, rotations, baseRotation, baseScale);
        }
        else
        {
            ExecuteRearrangeMode(positions, rotations);
        }
    }

    private void ExecuteCopyMode(List<Vector3> positions, List<Quaternion> rotations, Quaternion baseRotation, Vector3 baseScale)
    {
        Undo.SetCurrentGroupName("GameObject Array - Copy");
        int undoGroup = Undo.GetCurrentGroup();
        
        List<GameObject> createdObjects = new List<GameObject>();
        
        for (int i = 0; i < positions.Count; i++)
        {
            GameObject newObj;
            
            // 检查是否是Prefab
            bool isPrefab = PrefabUtility.IsPartOfPrefabAsset(sourceObject) || 
                           PrefabUtility.GetPrefabAssetType(sourceObject) != PrefabAssetType.NotAPrefab;
            
            if (isPrefab)
            {
                GameObject prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(sourceObject);
                if (prefabSource == null) prefabSource = sourceObject;
                newObj = (GameObject)PrefabUtility.InstantiatePrefab(prefabSource);
            }
            else
            {
                newObj = Instantiate(sourceObject);
            }
            
            newObj.name = sourceObject.name + "_" + i;
            
            // 设置位置
            newObj.transform.position = positions[i];
            
            // 设置旋转（布局旋转 + 基础旋转）
            Quaternion finalRotation = rotations[i] * baseRotation;
            
            // 应用增量旋转
            if (enableIncrementalTransform)
            {
                finalRotation *= Quaternion.Euler(incrementalRotation * i);
            }
            
            // 应用随机旋转
            if (enableRandomOffset)
            {
                Vector3 randomRot = new Vector3(
                    Random.Range(-randomRotationRange.x, randomRotationRange.x),
                    Random.Range(-randomRotationRange.y, randomRotationRange.y),
                    Random.Range(-randomRotationRange.z, randomRotationRange.z)
                );
                finalRotation *= Quaternion.Euler(randomRot);
            }
            
            newObj.transform.rotation = finalRotation;
            
            // 设置缩放
            Vector3 finalScale = baseScale;
            
            // 应用增量缩放
            if (enableIncrementalTransform)
            {
                finalScale += incrementalScale * i;
            }
            
            // 应用随机缩放
            if (enableRandomOffset)
            {
                if (uniformRandomScale)
                {
                    float randomS = Random.Range(-randomScaleRange.x, randomScaleRange.x);
                    finalScale += new Vector3(randomS, randomS, randomS);
                }
                else
                {
                    finalScale += new Vector3(
                        Random.Range(-randomScaleRange.x, randomScaleRange.x),
                        Random.Range(-randomScaleRange.y, randomScaleRange.y),
                        Random.Range(-randomScaleRange.z, randomScaleRange.z)
                    );
                }
            }
            
            newObj.transform.localScale = finalScale;
            
            // 应用随机位置偏移
            if (enableRandomOffset)
            {
                Vector3 randomPos = new Vector3(
                    Random.Range(-randomPositionRange.x, randomPositionRange.x),
                    Random.Range(-randomPositionRange.y, randomPositionRange.y),
                    Random.Range(-randomPositionRange.z, randomPositionRange.z)
                );
                newObj.transform.position += randomPos;
            }
            
            // 设置父对象
            if (parentTransform != null)
            {
                newObj.transform.SetParent(parentTransform);
            }
            
            Undo.RegisterCreatedObjectUndo(newObj, "Create Array Object");
            createdObjects.Add(newObj);
        }
        
        Undo.CollapseUndoOperations(undoGroup);
        
        // 选中创建的对象
        Selection.objects = createdObjects.ToArray();
        
        Debug.Log($"阵列完成：创建了 {createdObjects.Count} 个对象");
    }

    private void ExecuteRearrangeMode(List<Vector3> positions, List<Quaternion> rotations)
    {
        GameObject[] targetObjects = GetRearrangeTargetObjects();
        
        if (targetObjects.Length == 0)
        {
            Debug.LogWarning("没有找到要重排的对象。请选择对象或选择包含子对象的父对象。");
            return;
        }
        
        Undo.SetCurrentGroupName("GameObject Array - Rearrange");
        int undoGroup = Undo.GetCurrentGroup();
        
        int count = Mathf.Min(targetObjects.Length, positions.Count);
        
        for (int i = 0; i < count; i++)
        {
            Undo.RecordObject(targetObjects[i].transform, "Rearrange Object");
            
            targetObjects[i].transform.position = positions[i];
            
            // 应用布局旋转
            Quaternion finalRotation = rotations[i] * targetObjects[i].transform.rotation;
            
            // 应用增量旋转
            if (enableIncrementalTransform)
            {
                finalRotation *= Quaternion.Euler(incrementalRotation * i);
            }
            
            // 应用随机旋转
            if (enableRandomOffset)
            {
                Vector3 randomRot = new Vector3(
                    Random.Range(-randomRotationRange.x, randomRotationRange.x),
                    Random.Range(-randomRotationRange.y, randomRotationRange.y),
                    Random.Range(-randomRotationRange.z, randomRotationRange.z)
                );
                finalRotation *= Quaternion.Euler(randomRot);
            }
            
            targetObjects[i].transform.rotation = finalRotation;
            
            // 应用增量缩放
            if (enableIncrementalTransform)
            {
                targetObjects[i].transform.localScale += incrementalScale * i;
            }
            
            // 应用随机缩放
            if (enableRandomOffset)
            {
                Vector3 randomScale;
                if (uniformRandomScale)
                {
                    float randomS = Random.Range(-randomScaleRange.x, randomScaleRange.x);
                    randomScale = new Vector3(randomS, randomS, randomS);
                }
                else
                {
                    randomScale = new Vector3(
                        Random.Range(-randomScaleRange.x, randomScaleRange.x),
                        Random.Range(-randomScaleRange.y, randomScaleRange.y),
                        Random.Range(-randomScaleRange.z, randomScaleRange.z)
                    );
                }
                targetObjects[i].transform.localScale += randomScale;
            }
            
            // 应用随机位置偏移
            if (enableRandomOffset)
            {
                Vector3 randomPos = new Vector3(
                    Random.Range(-randomPositionRange.x, randomPositionRange.x),
                    Random.Range(-randomPositionRange.y, randomPositionRange.y),
                    Random.Range(-randomPositionRange.z, randomPositionRange.z)
                );
                targetObjects[i].transform.position += randomPos;
            }
            
            // 重排模式下子对象已经在父对象下，不需要再设置父对象
        }
        
        Undo.CollapseUndoOperations(undoGroup);
        
        Debug.Log($"阵列完成：重排了 {count} 个对象");
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!showPreview || previewPositions.Count == 0) return;
        
        Handles.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        
        // 绘制预览点
        for (int i = 0; i < previewPositions.Count; i++)
        {
            float size = HandleUtility.GetHandleSize(previewPositions[i]) * 0.1f;
            Handles.SphereHandleCap(0, previewPositions[i], Quaternion.identity, size, EventType.Repaint);
            
            // 绘制方向指示
            if (i < previewRotations.Count)
            {
                Handles.color = Color.blue;
                Handles.ArrowHandleCap(0, previewPositions[i], previewRotations[i], size * 3f, EventType.Repaint);
                Handles.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            }
        }
        
        // 绘制连线
        if (previewPositions.Count > 1)
        {
            Handles.color = new Color(1f, 1f, 0f, 0.5f);
            for (int i = 0; i < previewPositions.Count - 1; i++)
            {
                Handles.DrawLine(previewPositions[i], previewPositions[i + 1]);
            }
        }
        
        // 路径模式下绘制路径线
        if (layoutMode == LayoutMode.路径阵列)
        {
            List<Transform> validPoints = GetValidPathPoints();
            if (validPoints.Count >= 2)
            {
                Handles.color = new Color(0f, 1f, 0f, 0.8f);
                for (int i = 0; i < validPoints.Count - 1; i++)
                {
                    Handles.DrawLine(validPoints[i].position, validPoints[i + 1].position);
                }
                if (pathClosedLoop)
                {
                    Handles.DrawLine(validPoints[validPoints.Count - 1].position, validPoints[0].position);
                }
            }
        }
    }
}
