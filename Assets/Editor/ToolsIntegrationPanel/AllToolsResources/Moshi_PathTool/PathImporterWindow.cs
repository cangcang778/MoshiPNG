using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Moshi.PathTool;

namespace Moshi.PathTool.Editor
{
    /// <summary>
    /// 路径导入器窗口 - 支持从3ds Max导出的各种格式导入路径
    /// </summary>
    public class PathImporterWindow : EditorWindow
    {
        // 目标PathCreator
        private PathCreator targetPathCreator;

        // 导入模式
        private enum ImportMode
        {
            FromMesh,       // 从FBX/模型导入
            FromCSV,        // 从CSV文件导入
            FromJSON        // 从JSON文件导入
        }
        private ImportMode importMode = ImportMode.FromMesh;

        // Mesh导入设置
        private GameObject meshSource;
        private bool sortByDistance = true;
        private bool removeDuplicates = true;
        private float duplicateThreshold = 0.001f;

        // CSV导入设置
        private string csvFilePath = "";
        private bool csvHasHeader = true;

        // JSON导入设置
        private string jsonFilePath = "";

        // 通用设置
        private bool swapYZ = true;  // 3ds Max Z-up -> Unity Y-up
        private float importScale = 1f;
        private bool centerPath = false;
        private bool simplifyPath = false;
        private float simplifyTolerance = 0.1f;
        private float importedPointSize = 0.3f;
        private bool replaceExisting = true;

        // 预览
        private List<Vector3> previewPoints = new List<Vector3>();
        private bool showPreview = false;


        // 滚动位置
        private Vector2 scrollPosition;
        
        private const string TOOL_NAME = "路径导入器";

        // MenuItem 已在 Moshi_PathTool.Moshi_PathToolMenu 中注册，此处移除避免重复
        public static void ShowWindow()
        {
            PathImporterWindow window = GetWindow<PathImporterWindow>(TOOL_NAME);
            window.minSize = new Vector2(400, 500);
        }

        /// <summary>
        /// 从PathCreator打开导入器
        /// </summary>
        public static void ShowWindow(PathCreator target)
        {
            PathImporterWindow window = GetWindow<PathImporterWindow>(TOOL_NAME);
            window.minSize = new Vector2(400, 500);
            window.targetPathCreator = target;
            if (target != null)
                window.importedPointSize = target.pointSize;
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("3ds Max 路径导入器", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            MoshiHelpButton.DrawHelpButtonMini("路径工具");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "支持从3ds Max导出的路径数据导入到PathCreator\n" +
                "• FBX: 将样条线转为网格后导出\n" +
                "• CSV: 使用MaxScript导出坐标\n" +
                "• JSON: 结构化路径数据", 
                MessageType.Info);

            EditorGUILayout.Space(10);

            // 目标PathCreator
            DrawTargetSettings();

            EditorGUILayout.Space(10);

            // 导入模式选择
            EditorGUILayout.LabelField("导入模式", EditorStyles.boldLabel);
            importMode = (ImportMode)GUILayout.Toolbar((int)importMode, 
                new string[] { "从模型(FBX)", "从CSV", "从JSON" });

            EditorGUILayout.Space(10);

            // 根据模式显示不同的设置
            switch (importMode)
            {
                case ImportMode.FromMesh:
                    DrawMeshImportSettings();
                    break;
                case ImportMode.FromCSV:
                    DrawCSVImportSettings();
                    break;
                case ImportMode.FromJSON:
                    DrawJSONImportSettings();
                    break;
            }

            EditorGUILayout.Space(10);

            // 通用设置
            DrawCommonSettings();

            EditorGUILayout.Space(10);

            // 操作按钮
            DrawActionButtons();

            EditorGUILayout.Space(10);

            // 预览信息
            if (showPreview && previewPoints.Count > 0)
            {
                DrawPreviewInfo();
            }

            EditorGUILayout.Space(10);

            // MaxScript帮助
            DrawMaxScriptHelp();

            EditorGUILayout.EndScrollView();
        }

        private void DrawTargetSettings()
        {
            EditorGUILayout.LabelField("目标设置", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            targetPathCreator = (PathCreator)EditorGUILayout.ObjectField(
                "目标 PathCreator", targetPathCreator, typeof(PathCreator), true);
            if (EditorGUI.EndChangeCheck() && targetPathCreator != null)
                importedPointSize = targetPathCreator.pointSize;
            
            if (GUILayout.Button("查找", GUILayout.Width(50)))
            {
                FindPathCreatorInScene();
            }
            EditorGUILayout.EndHorizontal();

            if (targetPathCreator == null)
            {
                EditorGUILayout.HelpBox("未指定目标，导入时将自动创建新的PathCreator", MessageType.Info);
            }
        }

        private void FindPathCreatorInScene()
        {
            PathCreator[] paths = Object.FindObjectsOfType<PathCreator>();
            if (paths.Length > 0)
            {
                // 显示选择菜单
                GenericMenu menu = new GenericMenu();
                foreach (var path in paths)
                {
                    PathCreator p = path; // 闭包捕获
                    menu.AddItem(new GUIContent($"{p.pathName} ({p.pathPoints.Count}点)"), 
                        targetPathCreator == p, 
                        () =>
                        {
                            targetPathCreator = p;
                            importedPointSize = p.pointSize;
                        });
                }
                menu.ShowAsContext();
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "场景中没有找到PathCreator", "确定");
            }
        }

        private void DrawMeshImportSettings()
        {
            EditorGUILayout.LabelField("模型导入设置", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            meshSource = (GameObject)EditorGUILayout.ObjectField(
                "模型源", meshSource, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                ClearPreview();
            }

            sortByDistance = EditorGUILayout.Toggle(
                new GUIContent("按距离排序", "将顶点按最近邻顺序排列成路径"), 
                sortByDistance);

            removeDuplicates = EditorGUILayout.Toggle(
                new GUIContent("移除重复顶点", "移除距离过近的重复顶点"), 
                removeDuplicates);

            if (removeDuplicates)
            {
                EditorGUI.indentLevel++;
                duplicateThreshold = EditorGUILayout.FloatField(
                    new GUIContent("重复阈值", "小于此距离的顶点视为重复"), 
                    duplicateThreshold);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.HelpBox(
                "3ds Max操作步骤:\n" +
                "1. 选中样条线(Spline)\n" +
                "2. 右键 → Convert To → Editable Mesh\n" +
                "3. 导出为FBX格式\n" +
                "4. 将FBX拖入Unity，然后拖到上方\"模型源\"",
                MessageType.None);
        }

        private void DrawCSVImportSettings()
        {
            EditorGUILayout.LabelField("CSV导入设置", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            csvFilePath = EditorGUILayout.TextField("CSV文件路径", csvFilePath);
            if (GUILayout.Button("浏览", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFilePanel("选择CSV文件", "", "csv");
                if (!string.IsNullOrEmpty(path))
                {
                    csvFilePath = path;
                    ClearPreview();
                }
            }
            EditorGUILayout.EndHorizontal();

            csvHasHeader = EditorGUILayout.Toggle(
                new GUIContent("包含表头", "第一行是否为列名"), 
                csvHasHeader);

            EditorGUILayout.HelpBox(
                "CSV格式示例:\n" +
                "x,y,z\n" +
                "0.0,0.0,0.0\n" +
                "1.0,0.5,2.0\n" +
                "...",
                MessageType.None);
        }

        private void DrawJSONImportSettings()
        {
            EditorGUILayout.LabelField("JSON导入设置", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            jsonFilePath = EditorGUILayout.TextField("JSON文件路径", jsonFilePath);
            if (GUILayout.Button("浏览", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFilePanel("选择JSON文件", "", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    jsonFilePath = path;
                    ClearPreview();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "JSON格式示例:\n" +
                "[\n" +
                "  {\"x\": 0.0, \"y\": 0.0, \"z\": 0.0},\n" +
                "  {\"x\": 1.0, \"y\": 0.5, \"z\": 2.0}\n" +
                "]",
                MessageType.None);
        }

        private void DrawCommonSettings()
        {
            EditorGUILayout.LabelField("通用设置", EditorStyles.boldLabel);

            swapYZ = EditorGUILayout.Toggle(
                new GUIContent("交换Y/Z轴", "3ds Max使用Z-up，Unity使用Y-up"), 
                swapYZ);

            importScale = EditorGUILayout.FloatField(
                new GUIContent("缩放系数", "导入时的缩放倍数"), 
                importScale);

            centerPath = EditorGUILayout.Toggle(
                new GUIContent("居中路径", "将路径中心移到原点"), 
                centerPath);

            simplifyPath = EditorGUILayout.Toggle(
                new GUIContent("简化路径", "减少路径点数量"), 
                simplifyPath);

            if (simplifyPath)
            {
                EditorGUI.indentLevel++;
                simplifyTolerance = EditorGUILayout.FloatField(
                    new GUIContent("简化容差", "容差越大，简化程度越高"), 
                    simplifyTolerance);
                EditorGUI.indentLevel--;
            }

            importedPointSize = EditorGUILayout.Slider(
                new GUIContent("Point Size", "导入完成后写入目标 PathCreator 的控制点显示大小"),
                importedPointSize, 0.1f, 2f);

            replaceExisting = EditorGUILayout.Toggle(
                new GUIContent("替换现有路径", "是否清空现有路径点（关闭则追加）"), 
                replaceExisting);
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            // 预览按钮
            if (GUILayout.Button("预览", GUILayout.Height(30)))
            {
                PreviewImport();
            }

            // 导入按钮
            GUI.enabled = previewPoints.Count > 0;
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("导入", GUILayout.Height(30)))
            {
                ApplyImport();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            // 清除预览
            if (showPreview)
            {
                if (GUILayout.Button("清除预览"))
                {
                    ClearPreview();
                }
            }
        }

        private void DrawPreviewInfo()
        {
            EditorGUILayout.LabelField("预览信息", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"路径点数量: {previewPoints.Count}");

            if (previewPoints.Count > 0)
            {
                // 计算边界
                Vector3 min = previewPoints[0];
                Vector3 max = previewPoints[0];
                foreach (var point in previewPoints)
                {
                    min = Vector3.Min(min, point);
                    max = Vector3.Max(max, point);
                }
                Vector3 size = max - min;

                EditorGUILayout.LabelField($"边界大小: ({size.x:F2}, {size.y:F2}, {size.z:F2})");
                EditorGUILayout.LabelField($"最小点: ({min.x:F2}, {min.y:F2}, {min.z:F2})");
                EditorGUILayout.LabelField($"最大点: ({max.x:F2}, {max.y:F2}, {max.z:F2})");

                // 计算路径长度
                float totalLength = 0;
                for (int i = 0; i < previewPoints.Count - 1; i++)
                {
                    totalLength += Vector3.Distance(previewPoints[i], previewPoints[i + 1]);
                }
                EditorGUILayout.LabelField($"路径长度: {totalLength:F2}");
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawMaxScriptHelp()
        {
            if (GUILayout.Button("显示MaxScript导出脚本"))
            {
                ShowMaxScriptWindow();
            }
        }

        private void PreviewImport()
        {
            previewPoints.Clear();

            switch (importMode)
            {
                case ImportMode.FromMesh:
                    if (meshSource != null)
                    {
                        previewPoints = PathImporter.ImportFromGameObject(
                            meshSource, sortByDistance, removeDuplicates, duplicateThreshold);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", "请先指定模型源", "确定");
                        return;
                    }
                    break;

                case ImportMode.FromCSV:
                    if (!string.IsNullOrEmpty(csvFilePath) && File.Exists(csvFilePath))
                    {
                        previewPoints = PathImporter.ImportFromCSV(csvFilePath, csvHasHeader, swapYZ, importScale);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", "请先指定有效的CSV文件", "确定");
                        return;
                    }
                    break;

                case ImportMode.FromJSON:
                    if (!string.IsNullOrEmpty(jsonFilePath) && File.Exists(jsonFilePath))
                    {
                        previewPoints = PathImporter.ImportFromJSON(jsonFilePath, swapYZ, importScale);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", "请先指定有效的JSON文件", "确定");
                        return;
                    }
                    break;
            }

            // 应用通用处理
            if (importMode == ImportMode.FromMesh && swapYZ)
            {
                // Mesh导入时也需要交换YZ
                List<Vector3> swapped = new List<Vector3>();
                foreach (var p in previewPoints)
                {
                    swapped.Add(new Vector3(p.x, p.z, p.y));
                }
                previewPoints = swapped;
            }

            if (importMode == ImportMode.FromMesh)
            {
                // Mesh导入时应用缩放
                previewPoints = PathImporter.ScalePath(previewPoints, importScale);
            }

            if (centerPath)
            {
                previewPoints = PathImporter.CenterPath(previewPoints);
            }

            if (simplifyPath && previewPoints.Count > 2)
            {
                previewPoints = PathImporter.SimplifyPath(previewPoints, simplifyTolerance);
            }

            showPreview = true;
            SceneView.RepaintAll();

            if (previewPoints.Count == 0)
            {
                EditorUtility.DisplayDialog("警告", "未能导入任何路径点，请检查源数据", "确定");
            }
        }

        private void ApplyImport()
        {
            if (previewPoints.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "没有可导入的路径点，请先预览", "确定");
                return;
            }

            // 如果没有指定目标，自动创建
            if (targetPathCreator == null)
            {
                GameObject go = new GameObject("ImportedPath");
                targetPathCreator = go.AddComponent<PathCreator>();
                targetPathCreator.pathName = "Imported Path";
                
                if (SceneView.lastActiveSceneView != null)
                {
                    go.transform.position = SceneView.lastActiveSceneView.pivot;
                }
                
                Undo.RegisterCreatedObjectUndo(go, "Create Imported Path");
                Debug.Log("已自动创建 PathCreator: ImportedPath");
            }

            Undo.RecordObject(targetPathCreator, "Import Path");

            // 使用内部方法设置路径点，并同步显示参数
            targetPathCreator.SetPathPoints(previewPoints, replaceExisting);
            targetPathCreator.pointSize = Mathf.Clamp(importedPointSize, 0.1f, 2f);

            EditorUtility.SetDirty(targetPathCreator);

            string modeText = replaceExisting ? "替换" : "追加";
            EditorUtility.DisplayDialog("成功",
                $"成功{modeText}导入 {previewPoints.Count} 个路径点到 {targetPathCreator.pathName}\n" +
                $"当前总点数: {targetPathCreator.pathPoints.Count}",
                "确定");

            // 选中目标对象
            Selection.activeGameObject = targetPathCreator.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            SceneView.RepaintAll();
        }

        private void ClearPreview()
        {
            previewPoints.Clear();
            showPreview = false;
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!showPreview || previewPoints.Count == 0)
                return;

            // 绘制预览路径
            Handles.color = Color.magenta;
            for (int i = 0; i < previewPoints.Count - 1; i++)
            {
                Handles.DrawLine(previewPoints[i], previewPoints[i + 1], 3f);
            }

            // 绘制预览点
            Handles.color = Color.yellow;
            foreach (var point in previewPoints)
            {
                float size = HandleUtility.GetHandleSize(point) * 0.1f;
                Handles.SphereHandleCap(0, point, Quaternion.identity, size, EventType.Repaint);
            }

            // 绘制起点和终点标记
            if (previewPoints.Count > 0)
            {
                Handles.color = Color.green;
                float startSize = HandleUtility.GetHandleSize(previewPoints[0]) * 0.15f;
                Handles.SphereHandleCap(0, previewPoints[0], Quaternion.identity, startSize, EventType.Repaint);
                Handles.Label(previewPoints[0] + Vector3.up * 0.3f, "起点");

                Handles.color = Color.red;
                Vector3 endPoint = previewPoints[previewPoints.Count - 1];
                float endSize = HandleUtility.GetHandleSize(endPoint) * 0.15f;
                Handles.SphereHandleCap(0, endPoint, Quaternion.identity, endSize, EventType.Repaint);
                Handles.Label(endPoint + Vector3.up * 0.3f, "终点");
            }
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        /// <summary>
        /// 显示MaxScript导出脚本窗口
        /// </summary>
        private void ShowMaxScriptWindow()
        {
            MaxScriptHelperWindow.ShowWindow();
        }
    }

    /// <summary>
    /// MaxScript帮助窗口 - 提供3ds Max导出脚本
    /// </summary>
    public class MaxScriptHelperWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private int selectedScript = 0;
        private string[] scriptNames = { "CSV导出脚本", "JSON导出脚本" };

        public static void ShowWindow()
        {
            MaxScriptHelperWindow window = GetWindow<MaxScriptHelperWindow>("MaxScript导出脚本");
            window.minSize = new Vector2(500, 400);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("3ds Max 路径导出脚本", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "将以下脚本复制到3ds Max的MaxScript编辑器中运行，\n" +
                "可以将选中的样条线导出为CSV或JSON格式。",
                MessageType.Info);

            EditorGUILayout.Space(10);

            selectedScript = GUILayout.Toolbar(selectedScript, scriptNames);

            EditorGUILayout.Space(10);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            string script = selectedScript == 0 ? GetCSVExportScript() : GetJSONExportScript();

            EditorGUILayout.TextArea(script, GUILayout.ExpandHeight(true));

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("复制到剪贴板", GUILayout.Height(30)))
            {
                EditorGUIUtility.systemCopyBuffer = script;
                EditorUtility.DisplayDialog("成功", "脚本已复制到剪贴板", "确定");
            }
        }

        private string GetCSVExportScript()
        {
            return @"-- MaxScript: 导出样条线顶点到CSV
-- 使用方法: 选中样条线后运行此脚本

(
    if selection.count == 0 then
    (
        messageBox ""请先选择一个样条线对象""
        return undefined
    )
    
    local obj = selection[1]
    
    if classOf obj != SplineShape and classOf obj != Line then
    (
        messageBox ""请选择样条线(Spline)对象""
        return undefined
    )
    
    -- 选择保存路径
    local savePath = getSaveFileName caption:""保存CSV文件"" types:""CSV Files (*.csv)|*.csv""
    
    if savePath == undefined then return undefined
    
    -- 打开文件
    local file = createFile savePath
    
    -- 写入表头
    format ""x,y,z\n"" to:file
    
    -- 遍历所有样条线
    local numSplines = numSplines obj
    for s = 1 to numSplines do
    (
        local numKnots = numKnots obj s
        for k = 1 to numKnots do
        (
            local pos = getKnotPoint obj s k
            format ""%.6f,%.6f,%.6f\n"" pos.x pos.y pos.z to:file
        )
    )
    
    close file
    messageBox (""成功导出到: "" + savePath)
)";
        }

        private string GetJSONExportScript()
        {
            return @"-- MaxScript: 导出样条线顶点到JSON
-- 使用方法: 选中样条线后运行此脚本

(
    if selection.count == 0 then
    (
        messageBox ""请先选择一个样条线对象""
        return undefined
    )
    
    local obj = selection[1]
    
    if classOf obj != SplineShape and classOf obj != Line then
    (
        messageBox ""请选择样条线(Spline)对象""
        return undefined
    )
    
    -- 选择保存路径
    local savePath = getSaveFileName caption:""保存JSON文件"" types:""JSON Files (*.json)|*.json""
    
    if savePath == undefined then return undefined
    
    -- 打开文件
    local file = createFile savePath
    
    -- 写入JSON数组开始
    format ""[\n"" to:file
    
    local isFirst = true
    
    -- 遍历所有样条线
    local numSplines = numSplines obj
    for s = 1 to numSplines do
    (
        local numKnots = numKnots obj s
        for k = 1 to numKnots do
        (
            local pos = getKnotPoint obj s k
            
            if not isFirst then
                format "",\n"" to:file
            
            format ""  {\""x\"": %.6f, \""y\"": %.6f, \""z\"": %.6f}"" pos.x pos.y pos.z to:file
            isFirst = false
        )
    )
    
    -- 写入JSON数组结束
    format ""\n]\n"" to:file
    
    close file
    messageBox (""成功导出到: "" + savePath)
)";
        }
    }
}
