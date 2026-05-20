using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;

namespace MoshiTools
{
    /// <summary>
    /// 图片处理工具 - 色条生成模块（渐变色条/混合输出/AE混合模式）
    /// Texture Tool - Gradient bar module (Gradient bar/Blended output/AE blend modes)
    /// </summary>
    public partial class Moshi_TextureTool
    {
        #region 色条生成 数据

        // 色条生成标签页数据 - 渐变色条生成
        // Gradient Bar Tab Data - Generate gradient bar texture
        private class GradientBarTabData
        {
            public Gradient gradient = new Gradient();      // 渐变编辑器
            public List<Texture2D> inputTextures = new List<Texture2D>(); // 输入纹理列表（支持批量）
            public BlendMode blendMode = BlendMode.Multiply; // 混合模式
            public float blendOpacity = 1f;                 // 混合不透明度
            public int outputWidth = 512;                   // 输出宽度
            public int outputHeight = 64;                   // 输出高度
            public bool horizontal = true;                  // 水平方向（false为垂直）
            public float rotation = 0f;                     // 旋转角度 (0-360度)
            public bool usePolar = false;                   // 极坐标模式 / Polar coordinates mode
            public string outputPath = "Assets";            // 输出相对路径 / Output relative path
            public string outputName = "gradient_bar";      // 输出文件名
            public Texture2D previewTexture;                // 渐变色条预览纹理（3x3）
            public List<Texture2D> originalPreviews = new List<Texture2D>(); // 原始纹理预览
            public List<Texture2D> blendedPreviews = new List<Texture2D>();  // 混合后预览
            public Vector2 scroll;
            public Vector2 listScroll;                      // 纹理列表滚动
            public Vector2 previewScroll;                   // 预览区域滚动
            
            // 输出模式切换
            // Output mode toggle
            public GradientBarOutputMode outputMode = GradientBarOutputMode.GradientOnly; // 输出模式
            public bool useBlackBackground = false;         // 黑底背景开关 / Black background toggle
            public bool showPreview = true;                 // 预览折叠开关 / Preview foldout toggle
            public bool showBlendPreview = true;            // 混合预览折叠开关 / Blend preview foldout toggle
            
            public GradientBarTabData()
            {
                // 初始化默认渐变（黑到白）
                gradient.SetKeys(
                    new GradientColorKey[] { 
                        new GradientColorKey(Color.black, 0f), 
                        new GradientColorKey(Color.white, 1f) 
                    },
                    new GradientAlphaKey[] { 
                        new GradientAlphaKey(1f, 0f), 
                        new GradientAlphaKey(1f, 1f) 
                    }
                );
            }
        }

        private GradientBarTabData gradientBarData = new GradientBarTabData();

        // 用于渐变复制粘贴的静态变量
        // Static variable for gradient copy/paste
        private static Gradient copiedGradient = null;
        
        // 用于序列化渐变的临时对象（用于JSON序列化到系统剪贴板）
        // Temporary object for serializing gradient (for JSON serialization to system clipboard)
        [Serializable]
        private class GradientContainer
        {
            [SerializeField] public Gradient gradient = new Gradient();
        }
        
        // 反射获取Unity内部渐变剪贴板 - 动态搜索多个类中的Gradient字段
        // Reflection to get Unity internal gradient clipboard
        private static System.Reflection.FieldInfo gradientClipboardField = null;
        private static bool hasSearchedFields = false;

        #endregion

        #region 色条生成 初始化/清理

        partial void InitGradientBarModule() { }

        partial void ClearGradientBarPreviews()
        {
            if (gradientBarData.previewTexture != null)
            {
                DestroyImmediate(gradientBarData.previewTexture);
                gradientBarData.previewTexture = null;
            }
            foreach (var tex in gradientBarData.originalPreviews)
            {
                if (tex != null) DestroyImmediate(tex);
            }
            gradientBarData.originalPreviews.Clear();
            foreach (var tex in gradientBarData.blendedPreviews)
            {
                if (tex != null) DestroyImmediate(tex);
            }
            gradientBarData.blendedPreviews.Clear();
        }

        #endregion

        #region 色条生成 拖放

        private void HandleGradientBarDragDrop(UnityEngine.Object[] draggedObjects)
        {
            int added = 0;
            foreach (UnityEngine.Object draggedObject in draggedObjects)
            {
                if (draggedObject is Texture2D texture && !gradientBarData.inputTextures.Contains(texture))
                {
                    gradientBarData.inputTextures.Add(texture);
                    added++;
                }
            }
            if (added > 0)
            {
                UpdateGradientPreview();
            }
        }

        #endregion

        #region 色条生成 反射/剪贴板

        private static void FindGradientClipboardField()
        {
            if (hasSearchedFields) return;
            hasSearchedFields = true;
            
            // 搜索多个可能的类
            var typesToSearch = new Type[] 
            {
                typeof(EditorGUI),
                typeof(EditorGUIUtility),
                typeof(SerializedProperty)
            };
            
            foreach (var searchType in typesToSearch)
            {
                if (searchType == null) continue;
                
                try
                {
                    var fields = searchType.GetFields(
                        System.Reflection.BindingFlags.Static | 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Public);
                    
                    foreach (var field in fields)
                    {
                        if (field.FieldType == typeof(Gradient))
                        {
                            gradientClipboardField = field;
                            return;
                        }
                    }
                }
                catch { }
            }
            
            // 尝试搜索 GradientEditor 类（如果存在）
            try
            {
                var gradientEditorType = Type.GetType("UnityEditor.GradientEditor, UnityEditor");
                if (gradientEditorType != null)
                {
                    var fields = gradientEditorType.GetFields(
                        System.Reflection.BindingFlags.Static | 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Public);
                    
                    foreach (var field in fields)
                    {
                        if (field.FieldType == typeof(Gradient))
                        {
                            gradientClipboardField = field;
                            return;
                        }
                    }
                }
            }
            catch { }
        }
        
        private static Gradient GetUnityGradientClipboard()
        {
            try
            {
                FindGradientClipboardField();
                if (gradientClipboardField != null)
                {
                    return gradientClipboardField.GetValue(null) as Gradient;
                }
            }
            catch { }
            return null;
        }
        
        private static void SetUnityGradientClipboard(Gradient gradient)
        {
            try
            {
                FindGradientClipboardField();
                if (gradientClipboardField != null)
                {
                    gradientClipboardField.SetValue(null, gradient);
                }
            }
            catch { }
        }
        
        // 从选中的粒子系统导入渐变
        // Import gradient from selected particle system
        private void ImportGradientFromParticleSystem()
        {
            if (Selection.activeGameObject != null)
            {
                var ps = Selection.activeGameObject.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var colorOverLifetime = ps.colorOverLifetime;
                    if (colorOverLifetime.enabled && colorOverLifetime.color.mode == ParticleSystemGradientMode.Gradient)
                    {
                        gradientBarData.gradient.SetKeys(
                            colorOverLifetime.color.gradient.colorKeys,
                            colorOverLifetime.color.gradient.alphaKeys
                        );
                        UpdateGradientPreview();
                        Repaint();
                        return;
                    }
                }
            }
            EditorUtility.DisplayDialog("提示", "请选中一个启用了 Color over Lifetime (Gradient 模式) 的粒子系统", "确定");
        }
        
        // 导出到选中的粒子系统
        // Export gradient to selected particle system
        private void ExportGradientToParticleSystem()
        {
            if (Selection.activeGameObject != null)
            {
                var ps = Selection.activeGameObject.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    // 使用 Undo 支持撤销
                    Undo.RecordObject(ps, "Export Gradient to Particle System");
                    
                    var colorOverLifetime = ps.colorOverLifetime;
                    colorOverLifetime.enabled = true;
                    
                    // 创建 MinMaxGradient 并设置为 Gradient 模式
                    var minMaxGradient = new ParticleSystem.MinMaxGradient(gradientBarData.gradient);
                    colorOverLifetime.color = minMaxGradient;
                    
                    EditorUtility.SetDirty(ps);
                    return;
                }
            }
            EditorUtility.DisplayDialog("提示", "请选中一个粒子系统", "确定");
        }
        
        // 复制渐变到剪贴板
        // Copy gradient to clipboard
        private void CopyGradient()
        {
            if (gradientBarData.gradient != null)
            {
                // 保存到本地变量
                copiedGradient = new Gradient();
                copiedGradient.SetKeys(
                    gradientBarData.gradient.colorKeys,
                    gradientBarData.gradient.alphaKeys
                );
                
                // 同时保存到Unity内部剪贴板（支持粒子系统粘贴）
                SetUnityGradientClipboard(copiedGradient);
                
                // 同时保存到系统剪贴板（JSON格式，作为备用）
                try
                {
                    GradientContainer container = new GradientContainer();
                    container.gradient = new Gradient();
                    container.gradient.SetKeys(
                        gradientBarData.gradient.colorKeys,
                        gradientBarData.gradient.alphaKeys
                    );
                    string json = JsonUtility.ToJson(container);
                    EditorGUIUtility.systemCopyBuffer = "GRADIENT_JSON:" + json;
                }
                catch { }
            }
        }
        
        // 从剪贴板粘贴渐变
        // Paste gradient from clipboard
        private void PasteGradient()
        {
            Gradient sourceGradient = null;
            
            // 1. 优先从Unity内部剪贴板获取
            sourceGradient = GetUnityGradientClipboard();
            
            // 2. 尝试从系统剪贴板解析JSON
            if (sourceGradient == null)
            {
                try
                {
                    string buffer = EditorGUIUtility.systemCopyBuffer;
                    if (buffer.StartsWith("GRADIENT_JSON:"))
                    {
                        string json = buffer.Substring("GRADIENT_JSON:".Length);
                        GradientContainer container = JsonUtility.FromJson<GradientContainer>(json);
                        if (container?.gradient != null)
                        {
                            sourceGradient = container.gradient;
                        }
                    }
                }
                catch { }
            }
            
            // 3. 使用本地变量
            if (sourceGradient == null && copiedGradient != null)
            {
                sourceGradient = copiedGradient;
            }
            
            if (sourceGradient != null)
            {
                gradientBarData.gradient.SetKeys(
                    sourceGradient.colorKeys,
                    sourceGradient.alphaKeys
                );
                UpdateGradientPreview();
                Repaint();
            }
        }

        #endregion

        #region 色条生成 UI

        private void DrawGradientBarTab()
        {
            EditorGUILayout.LabelField("渐变编辑器", EditorStyles.boldLabel);
            
            // 使用 GradientField 显示渐变 - 固定宽度200
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.GradientField(GUIContent.none, gradientBarData.gradient, GUILayout.Width(200), GUILayout.Height(EditorGUIUtility.singleLineHeight * 1.5f));
            if (EditorGUI.EndChangeCheck())
            {
                UpdateGradientPreview();
            }
            
            // 右键菜单支持
            if (Event.current.type == EventType.ContextClick)
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Copy"), false, CopyGradient);
                bool canPaste = copiedGradient != null || GetUnityGradientClipboard() != null;
                if (canPaste)
                {
                    menu.AddItem(new GUIContent("Paste"), false, PasteGradient);
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Paste"));
                }
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Import from Selected Particle"), false, ImportGradientFromParticleSystem);
                menu.AddItem(new GUIContent("Export to Selected Particle"), false, ExportGradientToParticleSystem);
                menu.ShowAsContext();
                Event.current.Use();
            }
            
            // 导入/导出按钮
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Import", GUILayout.Width(80)))
            {
                ImportGradientFromParticleSystem();
            }
            if (GUILayout.Button("Export", GUILayout.Width(80)))
            {
                ExportGradientToParticleSystem();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // 输入纹理区域（支持批量）
            EditorGUILayout.LabelField("输入纹理（可选，支持批量）", EditorStyles.boldLabel);
            
            Rect dropArea = EditorGUILayout.GetControlRect(GUILayout.Height(50));
            if (dropAreaStyle != null)
            {
                GUI.Box(dropArea, "拖放纹理到这里或点击下方按钮添加", dropAreaStyle);
            }
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("添加选中的纹理"))
            {
                string[] guids = Selection.assetGUIDs;
                int added = 0;
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex != null && !gradientBarData.inputTextures.Contains(tex))
                    {
                        gradientBarData.inputTextures.Add(tex);
                        added++;
                    }
                }
                if (added > 0)
                {
                    UpdateGradientPreview();
                }
            }
            if (GUILayout.Button("清空列表", GUILayout.Width(80)))
            {
                gradientBarData.inputTextures.Clear();
                UpdateGradientPreview();
            }
            EditorGUILayout.EndHorizontal();
            
            // 显示已添加的纹理列表
            if (gradientBarData.inputTextures.Count > 0)
            {
                EditorGUILayout.LabelField($"已选择纹理 ({gradientBarData.inputTextures.Count}):");
                
                float scrollHeight = Mathf.Min(100, 22 * gradientBarData.inputTextures.Count + 5);
                gradientBarData.listScroll = EditorGUILayout.BeginScrollView(gradientBarData.listScroll, GUILayout.Height(scrollHeight));
                
                for (int i = 0; i < gradientBarData.inputTextures.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    gradientBarData.inputTextures[i] = (Texture2D)EditorGUILayout.ObjectField(
                        gradientBarData.inputTextures[i], typeof(Texture2D), false, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("×", GUILayout.Width(20)))
                    {
                        gradientBarData.inputTextures.RemoveAt(i);
                        i--;
                        UpdateGradientPreview();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
            
            EditorGUILayout.Space(10);
            
            // 输出路径设置
            EditorGUILayout.LabelField("输出路径", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("保存目录:", GUILayout.Width(80));
            gradientBarData.outputPath = EditorGUILayout.TextField(gradientBarData.outputPath);
            if (GUILayout.Button("浏览", GUILayout.Width(50)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("选择保存目录", "Assets", "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    // 转换为相对路径
                    if (selectedPath.StartsWith(Application.dataPath))
                    {
                        gradientBarData.outputPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                    }
                    else if (selectedPath.StartsWith(Application.dataPath.Replace("/", "\\")))
                    {
                        gradientBarData.outputPath = "Assets" + selectedPath.Substring(Application.dataPath.Replace("/", "\\").Length);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("输出设置", EditorStyles.boldLabel);
            
            // 输出模式切换
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("输出模式:", GUILayout.Width(80));
            GradientBarOutputMode newOutputMode = (GradientBarOutputMode)GUILayout.SelectionGrid(
                (int)gradientBarData.outputMode, 
                new string[] { "仅色条图", "混合输出" }, 
                2, 
                GUILayout.Width(160));
            if (newOutputMode != gradientBarData.outputMode)
            {
                gradientBarData.outputMode = newOutputMode;
                UpdateGradientPreview();
            }
            EditorGUILayout.EndHorizontal();
            
            // 混合模式选择（仅当有输入纹理且为混合输出模式时显示）
            if (gradientBarData.inputTextures.Count > 0 && gradientBarData.outputMode == GradientBarOutputMode.Blended)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("混合模式:", GUILayout.Width(80));
                gradientBarData.blendMode = (BlendMode)EditorGUILayout.Popup((int)gradientBarData.blendMode, blendModeNames, GUILayout.Width(150));
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("不透明度:", GUILayout.Width(80));
                gradientBarData.blendOpacity = EditorGUILayout.Slider(gradientBarData.blendOpacity, 0f, 1f, GUILayout.Width(150));
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
            }
            
            // 尺寸设置
            if (gradientBarData.outputMode == GradientBarOutputMode.GradientOnly)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("输出宽度:", GUILayout.Width(80));
                gradientBarData.outputWidth = EditorGUILayout.IntField(Mathf.Max(1, gradientBarData.outputWidth), GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("输出高度:", GUILayout.Width(80));
                gradientBarData.outputHeight = EditorGUILayout.IntField(Mathf.Max(1, gradientBarData.outputHeight), GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // 混合输出模式：尺寸由输入纹理决定
                EditorGUILayout.HelpBox("混合输出模式：输出尺寸由输入纹理决定", MessageType.Info);
            }
            
            // 黑底背景开关
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("黑底背景:", GUILayout.Width(80));
            gradientBarData.useBlackBackground = EditorGUILayout.Toggle(gradientBarData.useBlackBackground, GUILayout.Width(20));
            GUILayout.Label(gradientBarData.useBlackBackground ? "启用" : "禁用");
            EditorGUILayout.EndHorizontal();
            
            // 方向选择
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("方向:", GUILayout.Width(80));
            bool newHorizontal = GUILayout.Toggle(gradientBarData.horizontal, "水平", GUILayout.Width(60));
            bool newVertical = GUILayout.Toggle(!gradientBarData.horizontal, "垂直", GUILayout.Width(60));
            if (newHorizontal != gradientBarData.horizontal)
            {
                gradientBarData.horizontal = true;
                UpdateGradientPreview();
            }
            else if (newVertical == gradientBarData.horizontal && newVertical)
            {
                gradientBarData.horizontal = false;
                UpdateGradientPreview();
            }
            EditorGUILayout.EndHorizontal();
            
            // 旋转角度滑块
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("旋转角度:", GUILayout.Width(80));
            gradientBarData.rotation = EditorGUILayout.Slider(gradientBarData.rotation, 0f, 360f, GUILayout.Width(200));
            GUILayout.Label("°", GUILayout.Width(15));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                UpdateGradientPreview();
            }
            
            // 极坐标开关
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("极坐标:", GUILayout.Width(80));
            bool newUsePolar = GUILayout.Toggle(gradientBarData.usePolar, "启用", GUILayout.Width(60));
            if (newUsePolar != gradientBarData.usePolar)
            {
                gradientBarData.usePolar = newUsePolar;
                UpdateGradientPreview();
            }
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                UpdateGradientPreview();
            }
            
            // 输出文件名
            gradientBarData.outputName = EditorGUILayout.TextField("文件名", gradientBarData.outputName);
            
            EditorGUILayout.Space(10);
            
            // 预览区域 - 可折叠
            gradientBarData.showPreview = EditorGUILayout.Foldout(gradientBarData.showPreview, "预览 (3x3方格 384x384)", true, EditorStyles.boldLabel);
            
            if (gradientBarData.showPreview)
            {
                // 预览区域 - 固定384x384，不随UI缩放
                if (gradientBarData.previewTexture == null)
                {
                    UpdateGradientPreview();
                }
                
                if (gradientBarData.previewTexture != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    
                    Rect previewRect = GUILayoutUtility.GetRect(384, 384, GUILayout.Width(384), GUILayout.Height(384), GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
                    
                    // 绘制棋盘格背景
                    if (checkerboardTexture != null)
                    {
                        GUI.DrawTextureWithTexCoords(previewRect, checkerboardTexture, new Rect(0, 0, previewRect.width / 24, previewRect.height / 24));
                    }
                    
                    // 绘制渐变预览
                    GUI.DrawTexture(previewRect, gradientBarData.previewTexture, ScaleMode.StretchToFill);
                    
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
            }
            
            // 混合预览区域
            if (gradientBarData.inputTextures.Count > 0 && gradientBarData.blendedPreviews.Count > 0 
                && gradientBarData.outputMode == GradientBarOutputMode.Blended)
            {
                EditorGUILayout.Space(10);
                
                // 标题行：折叠 + 刷新按钮
                EditorGUILayout.BeginHorizontal();
                gradientBarData.showBlendPreview = EditorGUILayout.Foldout(gradientBarData.showBlendPreview, $"混合预览 ({gradientBarData.blendedPreviews.Count}张)", true, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("刷新预览", GUILayout.Width(70)))
                {
                    UpdateBlendPreviews();
                }
                EditorGUILayout.EndHorizontal();
                
                if (gradientBarData.showBlendPreview)
                {
                    DrawPreviewSection(gradientBarData.originalPreviews, gradientBarData.blendedPreviews, ref gradientBarData.previewScroll);
                }
            }
            
            EditorGUILayout.Space(15);
            
            // 执行按钮
            string buttonText = gradientBarData.outputMode == GradientBarOutputMode.GradientOnly 
                ? "执行生成色条" 
                : "执行混合输出";
            if (GUILayout.Button(buttonText, GUILayout.Height(30)))
            {
                ProcessGradientBar();
            }
        }

        #endregion

        #region 色条生成 逻辑

        // 更新渐变预览（纯渐变色条3x3预览）
        // Update gradient preview (pure gradient bar 3x3 preview)
        private void UpdateGradientPreview()
        {
            if (gradientBarData.previewTexture != null)
            {
                DestroyImmediate(gradientBarData.previewTexture);
            }
            
            // 预览使用3x3方格布局，每个128x128，总计384x384
            Texture2D previewGrid = GenerateGradientGridTexture(
                gradientBarData.gradient, 
                gradientBarData.horizontal, 
                gradientBarData.rotation,
                gradientBarData.usePolar);
            
            // 应用黑底背景（如果启用）
            if (gradientBarData.useBlackBackground && previewGrid != null)
            {
                gradientBarData.previewTexture = ApplyBlackBackground(previewGrid);
                DestroyImmediate(previewGrid);
            }
            else
            {
                gradientBarData.previewTexture = previewGrid;
            }
            
            // 同时更新混合预览
            UpdateBlendPreviews();
        }
        
        // 更新混合预览
        // Update blend previews
        private void UpdateBlendPreviews()
        {
            // 清理旧预览
            foreach (var tex in gradientBarData.originalPreviews)
            {
                if (tex != null) DestroyImmediate(tex);
            }
            foreach (var tex in gradientBarData.blendedPreviews)
            {
                if (tex != null) DestroyImmediate(tex);
            }
            gradientBarData.originalPreviews.Clear();
            gradientBarData.blendedPreviews.Clear();
            
            // 仅在混合输出模式下生成预览
            if (gradientBarData.outputMode != GradientBarOutputMode.Blended) return;
            
            // 为每个输入纹理生成预览
            foreach (var inputTexture in gradientBarData.inputTextures)
            {
                if (inputTexture == null) continue;
                
                // 获取可读纹理
                Texture2D readableInput = GetReadableTexture(inputTexture);
                
                // 使用输入图原始尺寸作为预览尺寸（限制最大512px避免UI过大）
                int maxPreviewSize = 512;
                int previewWidth, previewHeight;
                
                if (readableInput.width <= maxPreviewSize && readableInput.height <= maxPreviewSize)
                {
                    previewWidth = readableInput.width;
                    previewHeight = readableInput.height;
                }
                else
                {
                    float aspectRatio = (float)readableInput.width / readableInput.height;
                    if (aspectRatio >= 1f)
                    {
                        previewWidth = maxPreviewSize;
                        previewHeight = Mathf.RoundToInt(maxPreviewSize / aspectRatio);
                    }
                    else
                    {
                        previewHeight = maxPreviewSize;
                        previewWidth = Mathf.RoundToInt(maxPreviewSize * aspectRatio);
                    }
                }
                
                Texture2D originalPreview = ResizeTexture(readableInput, previewWidth, previewHeight);
                
                // 原始预览也应用黑底背景（如果启用）
                if (gradientBarData.useBlackBackground)
                {
                    Texture2D originalWithBg = ApplyBlackBackground(originalPreview);
                    DestroyImmediate(originalPreview);
                    originalPreview = originalWithBg;
                }
                gradientBarData.originalPreviews.Add(originalPreview);
                
                // 生成对应尺寸的渐变纹理
                Texture2D gradientTexture = GenerateGradientTexture(
                    previewWidth, previewHeight,
                    gradientBarData.gradient,
                    gradientBarData.horizontal,
                    gradientBarData.rotation,
                    gradientBarData.usePolar);
                
                // 应用混合模式
                Texture2D blendedPreview = ApplyGradientToTexture(originalPreview, gradientTexture, gradientBarData.blendMode, gradientBarData.blendOpacity);
                
                // 混合预览也应用黑底背景（如果启用）
                if (gradientBarData.useBlackBackground && blendedPreview != null)
                {
                    Texture2D blendedWithBg = ApplyBlackBackground(blendedPreview);
                    DestroyImmediate(blendedPreview);
                    blendedPreview = blendedWithBg;
                }
                gradientBarData.blendedPreviews.Add(blendedPreview);
                
                // 清理临时纹理
                if (readableInput != inputTexture) DestroyImmediate(readableInput);
                DestroyImmediate(gradientTexture);
            }
        }
        
        // 生成渐变方格纹理 - 3x3布局
        // Generate gradient grid texture - 3x3 layout
        private Texture2D GenerateGradientGridTexture(Gradient gradient, bool horizontal, float rotation, bool usePolar)
        {
            int cellSize = 128;
            int gridSize = 3;
            int totalSize = cellSize * gridSize; // 384x384
            
            Texture2D texture = new Texture2D(totalSize, totalSize, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[totalSize * totalSize];
            
            // 生成单个渐变单元格
            Color[] cellPixels = GenerateGradientCellPixels(cellSize, gradient, horizontal, rotation, usePolar);
            
            // 将渐变单元格填充到3x3网格中
            for (int gridY = 0; gridY < gridSize; gridY++)
            {
                for (int gridX = 0; gridX < gridSize; gridX++)
                {
                    int startX = gridX * cellSize;
                    int startY = gridY * cellSize;
                    
                    for (int y = 0; y < cellSize; y++)
                    {
                        for (int x = 0; x < cellSize; x++)
                        {
                            int pixelX = startX + x;
                            int pixelY = startY + y;
                            pixels[pixelY * totalSize + pixelX] = cellPixels[y * cellSize + x];
                        }
                    }
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
        
        // 生成单个渐变单元格像素
        // Generate single gradient cell pixels
        private Color[] GenerateGradientCellPixels(int size, Gradient gradient, bool horizontal, float rotation, bool usePolar)
        {
            Color[] pixels = new Color[size * size];
            
            // 单元格中心点
            float centerX = size * 0.5f;
            float centerY = size * 0.5f;
            float maxRadius = size * 0.5f;
            
            if (usePolar)
            {
                // 极坐标模式 - 从中心向外辐射的径向渐变
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - centerX;
                        float dy = y - centerY;
                        
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float t = Mathf.Clamp01(dist / maxRadius);
                        
                        if (rotation != 0f)
                        {
                            float angle = Mathf.Atan2(dy, dx) + rotation * Mathf.Deg2Rad;
                            float angleT = (angle / (2 * Mathf.PI) + 0.5f) % 1f;
                            t = Mathf.Lerp(t, angleT, 0.5f);
                        }
                        
                        pixels[y * size + x] = gradient.Evaluate(t);
                    }
                }
            }
            else
            {
                // 线性渐变模式
                float baseAngle = horizontal ? 0f : 90f;
                float totalAngle = baseAngle + rotation;
                float radians = totalAngle * Mathf.Deg2Rad;
                
                float dirX = Mathf.Cos(radians);
                float dirY = Mathf.Sin(radians);
                
                float maxDist = size * 0.5f * Mathf.Max(Mathf.Abs(Mathf.Cos(radians)), Mathf.Abs(Mathf.Sin(radians))) * 2f;
                
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - centerX;
                        float dy = y - centerY;
                        
                        float proj = dx * dirX + dy * dirY;
                        
                        float t = (proj / (maxDist * 0.5f) + 1f) * 0.5f;
                        t = Mathf.Clamp01(t);
                        
                        pixels[y * size + x] = gradient.Evaluate(t);
                    }
                }
            }
            
            return pixels;
        }
        
        // 生成渐变纹理（用于导出）
        // Generate gradient texture (for export)
        private Texture2D GenerateGradientTexture(int width, int height, Gradient gradient, bool horizontal, float rotation, bool usePolar)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            Color[] pixels = new Color[width * height];
            
            // 图像中心点
            float centerX = width * 0.5f;
            float centerY = height * 0.5f;
            float maxRadius = Mathf.Max(width, height) * 0.5f;
            
            if (usePolar)
            {
                // 极坐标模式
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float dx = x - centerX;
                        float dy = y - centerY;
                        
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float t = Mathf.Clamp01(dist / maxRadius);
                        
                        if (rotation != 0f)
                        {
                            float angle = Mathf.Atan2(dy, dx) + rotation * Mathf.Deg2Rad;
                            float angleT = (angle / (2 * Mathf.PI) + 0.5f) % 1f;
                            t = Mathf.Lerp(t, angleT, 0.5f);
                        }
                        
                        pixels[y * width + x] = gradient.Evaluate(t);
                    }
                }
            }
            else
            {
                // 线性渐变模式
                float baseAngle = horizontal ? 0f : 90f;
                float totalAngle = baseAngle + rotation;
                float radians = totalAngle * Mathf.Deg2Rad;
                
                float dirX = Mathf.Cos(radians);
                float dirY = Mathf.Sin(radians);
                
                float maxDist = Mathf.Max(width, height) * 0.5f * Mathf.Max(Mathf.Abs(Mathf.Cos(radians)), Mathf.Abs(Mathf.Sin(radians))) * 2f;
                
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float dx = x - centerX;
                        float dy = y - centerY;
                        
                        float proj = dx * dirX + dy * dirY;
                        
                        float t = (proj / (maxDist * 0.5f) + 1f) * 0.5f;
                        t = Mathf.Clamp01(t);
                        
                        pixels[y * width + x] = gradient.Evaluate(t);
                    }
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
        
        // 将渐变应用到纹理（AE风格混合模式）
        // Apply gradient to texture (AE-style blend modes)
        private Texture2D ApplyGradientToTexture(Texture2D inputTexture, Texture2D gradientTexture, BlendMode blendMode, float opacity)
        {
            int width = inputTexture.width;
            int height = inputTexture.height;
            
            // 安全检查：确保渐变纹理尺寸与输入纹理匹配
            if (gradientTexture.width != width || gradientTexture.height != height)
            {
                Debug.LogWarning($"渐变纹理尺寸 ({gradientTexture.width}x{gradientTexture.height}) 与输入纹理尺寸 ({width}x{height}) 不匹配");
                return new Texture2D(width, height);
            }
            
            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            
            Color[] inputPixels = inputTexture.GetPixels();
            Color[] gradientPixels = gradientTexture.GetPixels();
            Color[] resultPixels = new Color[width * height];
            
            for (int i = 0; i < resultPixels.Length; i++)
            {
                Color baseColor = inputPixels[i];
                Color blendColor = gradientPixels[i];
                
                // 应用混合模式
                Color blended = ApplyBlendMode(baseColor, blendColor, blendMode);
                
                // 应用不透明度混合
                resultPixels[i] = Color.Lerp(baseColor, blended, opacity * blendColor.a);
            }
            
            result.SetPixels(resultPixels);
            result.Apply();
            return result;
        }
        
        // AE风格混合模式计算
        // AE-style blend mode calculation
        private Color ApplyBlendMode(Color baseColor, Color blendColor, BlendMode mode)
        {
            float r, g, b;
            
            switch (mode)
            {
                // 正常
                case BlendMode.Normal:
                    return new Color(blendColor.r, blendColor.g, blendColor.b, blendColor.a);
                
                // 变暗组 / Darken group
                case BlendMode.Darken:
                    r = Mathf.Min(baseColor.r, blendColor.r);
                    g = Mathf.Min(baseColor.g, blendColor.g);
                    b = Mathf.Min(baseColor.b, blendColor.b);
                    return new Color(r, g, b, baseColor.a);
                
                case BlendMode.Multiply:
                    r = baseColor.r * blendColor.r;
                    g = baseColor.g * blendColor.g;
                    b = baseColor.b * blendColor.b;
                    return new Color(r, g, b, baseColor.a);
                
                case BlendMode.ColorBurn:
                    r = blendColor.r > 0 ? 1f - Mathf.Min(1f, (1f - baseColor.r) / blendColor.r) : 0f;
                    g = blendColor.g > 0 ? 1f - Mathf.Min(1f, (1f - baseColor.g) / blendColor.g) : 0f;
                    b = blendColor.b > 0 ? 1f - Mathf.Min(1f, (1f - baseColor.b) / blendColor.b) : 0f;
                    return new Color(r, g, b, baseColor.a);
                
                case BlendMode.LinearBurn:
                    r = Mathf.Max(0, baseColor.r + blendColor.r - 1f);
                    g = Mathf.Max(0, baseColor.g + blendColor.g - 1f);
                    b = Mathf.Max(0, baseColor.b + blendColor.b - 1f);
                    return new Color(r, g, b, baseColor.a);
                
                case BlendMode.DarkerColor:
                    float baseLum = GetLuminosity(baseColor);
                    float blendLum = GetLuminosity(blendColor);
                    return blendLum < baseLum ? blendColor : baseColor;
                
                // 变亮组 / Lighten group
                case BlendMode.Lighten:
                    r = Mathf.Max(baseColor.r, blendColor.r);
                    g = Mathf.Max(baseColor.g, blendColor.g);
                    b = Mathf.Max(baseColor.b, blendColor.b);
                    return new Color(r, g, b, baseColor.a);
                
                case BlendMode.Screen:
                    r = 1f - (1f - baseColor.r) * (1f - blendColor.r);
                    g = 1f - (1f - baseColor.g) * (1f - blendColor.g);
                    b = 1f - (1f - baseColor.b) * (1f - blendColor.b);
                    return new Color(r, g, b, baseColor.a);
                
                case BlendMode.ColorDodge:
                    r = blendColor.r < 1f ? Mathf.Min(1f, baseColor.r / (1f - blendColor.r)) : 1f;
                    g = blendColor.g < 1f ? Mathf.Min(1f, baseColor.g / (1f - blendColor.g)) : 1f;
                    b = blendColor.b < 1f ? Mathf.Min(1f, baseColor.b / (1f - blendColor.b)) : 1f;
                    return new Color(r, g, b, baseColor.a);
                
                case BlendMode.LinearDodge:
                    r = Mathf.Min(1f, baseColor.r + blendColor.r);
                    g = Mathf.Min(1f, baseColor.g + blendColor.g);
                    b = Mathf.Min(1f, baseColor.b + blendColor.b);
                    return new Color(r, g, b, baseColor.a);
                
                case BlendMode.LighterColor:
                    float baseLum2 = GetLuminosity(baseColor);
                    float blendLum2 = GetLuminosity(blendColor);
                    return blendLum2 > baseLum2 ? blendColor : baseColor;
                
                // 对比组 / Contrast group
                case BlendMode.Overlay:
                    r = baseColor.r < 0.5f ? 2f * baseColor.r * blendColor.r : 1f - 2f * (1f - baseColor.r) * (1f - blendColor.r);
                    g = baseColor.g < 0.5f ? 2f * baseColor.g * blendColor.g : 1f - 2f * (1f - baseColor.g) * (1f - blendColor.g);
                    b = baseColor.b < 0.5f ? 2f * baseColor.b * blendColor.b : 1f - 2f * (1f - baseColor.b) * (1f - blendColor.b);
                    return new Color(r, g, b, baseColor.a);
                
                case BlendMode.SoftLight:
                    r = blendColor.r < 0.5f ? baseColor.r - (1f - 2f * blendColor.r) * baseColor.r * (1f - baseColor.r) : 
                        baseColor.r + (2f * blendColor.r - 1f) * (Mathf.Pow(baseColor.r, 0.5f) - baseColor.r);
                    g = blendColor.g < 0.5f ? baseColor.g - (1f - 2f * blendColor.g) * baseColor.g * (1f - baseColor.g) : 
                        baseColor.g + (2f * blendColor.g - 1f) * (Mathf.Pow(baseColor.g, 0.5f) - baseColor.g);
                    b = blendColor.b < 0.5f ? baseColor.b - (1f - 2f * blendColor.b) * baseColor.b * (1f - baseColor.b) : 
                        baseColor.b + (2f * blendColor.b - 1f) * (Mathf.Pow(baseColor.b, 0.5f) - baseColor.b);
                    return new Color(r, g, b, baseColor.a);
                
                case BlendMode.HardLight:
                    r = blendColor.r < 0.5f ? 2f * baseColor.r * blendColor.r : 1f - 2f * (1f - baseColor.r) * (1f - blendColor.r);
                    g = blendColor.g < 0.5f ? 2f * baseColor.g * blendColor.g : 1f - 2f * (1f - baseColor.g) * (1f - blendColor.g);
                    b = blendColor.b < 0.5f ? 2f * baseColor.b * blendColor.b : 1f - 2f * (1f - baseColor.b) * (1f - blendColor.b);
                    return new Color(r, g, b, baseColor.a);
                
                case BlendMode.VividLight:
                    r = blendColor.r < 0.5f ? 
                        (blendColor.r > 0 ? 1f - Mathf.Min(1f, (1f - baseColor.r) / (2f * blendColor.r)) : 1f) :
                        (blendColor.r < 1f ? Mathf.Min(1f, baseColor.r / (2f * (1f - blendColor.r))) : 0f);
                    g = blendColor.g < 0.5f ? 
                        (blendColor.g > 0 ? 1f - Mathf.Min(1f, (1f - baseColor.g) / (2f * blendColor.g)) : 1f) :
                        (blendColor.g < 1f ? Mathf.Min(1f, baseColor.g / (2f * (1f - blendColor.g))) : 0f);
                    b = blendColor.b < 0.5f ? 
                        (blendColor.b > 0 ? 1f - Mathf.Min(1f, (1f - baseColor.b) / (2f * blendColor.b)) : 1f) :
                        (blendColor.b < 1f ? Mathf.Min(1f, baseColor.b / (2f * (1f - blendColor.b))) : 0f);
                    return new Color(r, g, b, baseColor.a);
                
                case BlendMode.LinearLight:
                    r = Mathf.Clamp01(baseColor.r + 2f * blendColor.r - 1f);
                    g = Mathf.Clamp01(baseColor.g + 2f * blendColor.g - 1f);
                    b = Mathf.Clamp01(baseColor.b + 2f * blendColor.b - 1f);
                    return new Color(r, g, b, baseColor.a);
                
                case BlendMode.PinLight:
                    r = blendColor.r < 0.5f ? Mathf.Min(baseColor.r, 2f * blendColor.r) : Mathf.Max(baseColor.r, 2f * (blendColor.r - 0.5f));
                    g = blendColor.g < 0.5f ? Mathf.Min(baseColor.g, 2f * blendColor.g) : Mathf.Max(baseColor.g, 2f * (blendColor.g - 0.5f));
                    b = blendColor.b < 0.5f ? Mathf.Min(baseColor.b, 2f * blendColor.b) : Mathf.Max(baseColor.b, 2f * (blendColor.b - 0.5f));
                    return new Color(r, g, b, baseColor.a);
                
                case BlendMode.HardMix:
                    r = baseColor.r + blendColor.r > 1f ? 1f : 0f;
                    g = baseColor.g + blendColor.g > 1f ? 1f : 0f;
                    b = baseColor.b + blendColor.b > 1f ? 1f : 0f;
                    return new Color(r, g, b, baseColor.a);
                
                // 差值组 / Difference group
                case BlendMode.Difference:
                    r = Mathf.Abs(baseColor.r - blendColor.r);
                    g = Mathf.Abs(baseColor.g - blendColor.g);
                    b = Mathf.Abs(baseColor.b - blendColor.b);
                    return new Color(r, g, b, baseColor.a);
                
                case BlendMode.Exclusion:
                    r = baseColor.r + blendColor.r - 2f * baseColor.r * blendColor.r;
                    g = baseColor.g + blendColor.g - 2f * baseColor.g * blendColor.g;
                    b = baseColor.b + blendColor.b - 2f * baseColor.b * blendColor.b;
                    return new Color(r, g, b, baseColor.a);
                
                case BlendMode.Subtract:
                    r = Mathf.Max(0, baseColor.r - blendColor.r);
                    g = Mathf.Max(0, baseColor.g - blendColor.g);
                    b = Mathf.Max(0, baseColor.b - blendColor.b);
                    return new Color(r, g, b, baseColor.a);
                
                case BlendMode.Divide:
                    r = blendColor.r > 0 ? Mathf.Min(1f, baseColor.r / blendColor.r) : 1f;
                    g = blendColor.g > 0 ? Mathf.Min(1f, baseColor.g / blendColor.g) : 1f;
                    b = blendColor.b > 0 ? Mathf.Min(1f, baseColor.b / blendColor.b) : 1f;
                    return new Color(r, g, b, baseColor.a);
                
                // 颜色组 / Color group
                case BlendMode.Hue:
                    Color hsvBase = RGBToHSV(baseColor);
                    Color hsvBlend = RGBToHSV(blendColor);
                    return HSVToRGB(new Color(hsvBlend.r, hsvBase.g, hsvBase.b, baseColor.a));
                
                case BlendMode.Saturation:
                    Color hsvBaseSat = RGBToHSV(baseColor);
                    Color hsvBlendSat = RGBToHSV(blendColor);
                    return HSVToRGB(new Color(hsvBaseSat.r, hsvBlendSat.g, hsvBaseSat.b, baseColor.a));
                
                case BlendMode.Color:
                    Color hsvBaseCol = RGBToHSV(baseColor);
                    Color hsvBlendCol = RGBToHSV(blendColor);
                    return HSVToRGB(new Color(hsvBlendCol.r, hsvBlendCol.g, hsvBaseCol.b, baseColor.a));
                
                case BlendMode.Luminosity:
                    Color hsvBaseLum = RGBToHSV(baseColor);
                    Color hsvBlendLum = RGBToHSV(blendColor);
                    return HSVToRGB(new Color(hsvBaseLum.r, hsvBaseLum.g, hsvBlendLum.b, baseColor.a));
                
                default:
                    return baseColor;
            }
        }
        
        // 获取明度
        // Get luminosity
        private float GetLuminosity(Color color)
        {
            return 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
        }
        
        // RGB转HSV
        // RGB to HSV
        private Color RGBToHSV(Color rgb)
        {
            float max = Mathf.Max(rgb.r, Mathf.Max(rgb.g, rgb.b));
            float min = Mathf.Min(rgb.r, Mathf.Min(rgb.g, rgb.b));
            float delta = max - min;
            
            float h = 0f;
            float s = max == 0f ? 0f : delta / max;
            float v = max;
            
            if (delta > 0f)
            {
                if (max == rgb.r)
                    h = 60f * (((rgb.g - rgb.b) / delta) % 6f);
                else if (max == rgb.g)
                    h = 60f * (((rgb.b - rgb.r) / delta) + 2f);
                else
                    h = 60f * (((rgb.r - rgb.g) / delta) + 4f);
            }
            
            if (h < 0f) h += 360f;
            h /= 360f; // 归一化到 0-1
            
            return new Color(h, s, v, rgb.a);
        }
        
        // HSV转RGB
        // HSV to RGB
        private Color HSVToRGB(Color hsv)
        {
            float h = hsv.r * 360f;
            float s = hsv.g;
            float v = hsv.b;
            
            float c = v * s;
            float x = c * (1f - Mathf.Abs((h / 60f) % 2f - 1f));
            float m = v - c;
            
            float r = 0f, g = 0f, b = 0f;
            
            if (h < 60f) { r = c; g = x; b = 0f; }
            else if (h < 120f) { r = x; g = c; b = 0f; }
            else if (h < 180f) { r = 0f; g = c; b = x; }
            else if (h < 240f) { r = 0f; g = x; b = c; }
            else if (h < 300f) { r = x; g = 0f; b = c; }
            else { r = c; g = 0f; b = x; }
            
            return new Color(r + m, g + m, b + m, hsv.a);
        }
        
        // 处理渐变条生成
        // Process gradient bar generation
        private void ProcessGradientBar()
        {
            string outputDir = gradientBarData.outputPath;
            
            // 确保目录存在
            if (!AssetDatabase.IsValidFolder(outputDir))
            {
                if (!CreateAssetFolder(outputDir))
                {
                    EditorUtility.DisplayDialog("错误", $"输出目录不存在且无法创建:\n{outputDir}", "确定");
                    return;
                }
            }
            
            int successCount = 0;
            List<string> outputPaths = new List<string>();
            
            // 根据输出模式处理
            if (gradientBarData.outputMode == GradientBarOutputMode.GradientOnly)
            {
                // ========== 仅色条图模式 ==========
                int width = gradientBarData.outputWidth;
                int height = gradientBarData.outputHeight;
                
                // 如果有输入纹理，为每个纹理生成对应名称的色条图
                if (gradientBarData.inputTextures.Count > 0)
                {
                    foreach (var inputTexture in gradientBarData.inputTextures)
                    {
                        if (inputTexture == null) continue;
                        
                        string inputPath = AssetDatabase.GetAssetPath(inputTexture);
                        string inputFileName = Path.GetFileNameWithoutExtension(inputPath);
                        
                        Texture2D gradientTex = GenerateGradientTexture(
                            width, height,
                            gradientBarData.gradient,
                            gradientBarData.horizontal,
                            gradientBarData.rotation,
                            gradientBarData.usePolar);
                        
                        if (gradientTex == null) continue;
                        
                        // 应用黑底背景（如果启用）
                        Texture2D outputTexture = gradientTex;
                        if (gradientBarData.useBlackBackground)
                        {
                            outputTexture = ApplyBlackBackground(gradientTex);
                        }
                        
                        try
                        {
                            string fileName = $"{inputFileName}_{gradientBarData.outputName}";
                            string outputPath = Path.Combine(outputDir, $"{fileName}.png");
                            
                            int counter = 1;
                            while (File.Exists(outputPath))
                            {
                                outputPath = Path.Combine(outputDir, $"{fileName}_{counter}.png");
                                counter++;
                            }
                            
                            byte[] bytes = outputTexture.EncodeToPNG();
                            File.WriteAllBytes(outputPath, bytes);
                            outputPaths.Add(outputPath);
                            successCount++;
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"保存渐变条失败: {e.Message}");
                        }
                        finally
                        {
                            DestroyImmediate(gradientTex);
                            if (outputTexture != gradientTex) DestroyImmediate(outputTexture);
                        }
                    }
                }
                else
                {
                    // 没有输入纹理，生成纯渐变色条
                    Texture2D gradientTex = GenerateGradientTexture(
                        width, height,
                        gradientBarData.gradient,
                        gradientBarData.horizontal,
                        gradientBarData.rotation,
                        gradientBarData.usePolar);
                    
                    if (gradientTex == null)
                    {
                        EditorUtility.DisplayDialog("错误", "生成渐变条失败", "确定");
                        return;
                    }
                    
                    // 应用黑底背景（如果启用）
                    Texture2D outputTexture = gradientTex;
                    if (gradientBarData.useBlackBackground)
                    {
                        outputTexture = ApplyBlackBackground(gradientTex);
                    }
                    
                    try
                    {
                        string fileName = gradientBarData.outputName;
                        string outputPath = Path.Combine(outputDir, $"{fileName}.png");
                        
                        int counter = 1;
                        while (File.Exists(outputPath))
                        {
                            outputPath = Path.Combine(outputDir, $"{fileName}_{counter}.png");
                            counter++;
                        }
                        
                        byte[] bytes = outputTexture.EncodeToPNG();
                        File.WriteAllBytes(outputPath, bytes);
                        AssetDatabase.Refresh();
                        
                        string modeName = gradientBarData.usePolar ? "极坐标" : "线性";
                        EditorUtility.DisplayDialog("完成", $"色条已生成:\n{outputPath}\n尺寸: {width}x{height}\n模式: {modeName}", "确定");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"保存渐变条失败: {e.Message}");
                        EditorUtility.DisplayDialog("错误", $"保存失败: {e.Message}", "确定");
                    }
                    finally
                    {
                        DestroyImmediate(gradientTex);
                        if (outputTexture != gradientTex) DestroyImmediate(outputTexture);
                    }
                    return;
                }
                
                AssetDatabase.Refresh();
                
                if (successCount > 0)
                {
                    string modeName = gradientBarData.usePolar ? "极坐标" : "线性";
                    EditorUtility.DisplayDialog("完成", 
                        $"已处理 {successCount} 个色条\n" +
                        $"模式: 仅色条图 ({modeName})\n" +
                        $"尺寸: {width}x{height}\n" +
                        $"输出目录: {outputDir}", "确定");
                }
                else
                {
                    EditorUtility.DisplayDialog("错误", "处理失败，请检查输入纹理", "确定");
                }
            }
            else
            {
                // ========== 混合输出模式 ==========
                if (gradientBarData.inputTextures.Count > 0)
                {
                    foreach (var inputTexture in gradientBarData.inputTextures)
                    {
                        if (inputTexture == null) continue;
                        
                        string inputPath = AssetDatabase.GetAssetPath(inputTexture);
                        string inputFileName = Path.GetFileNameWithoutExtension(inputPath);
                        
                        // 使用输入纹理的原始尺寸
                        int width = inputTexture.width;
                        int height = inputTexture.height;
                        
                        // 生成渐变纹理
                        Texture2D gradientTex = GenerateGradientTexture(
                            width, height,
                            gradientBarData.gradient,
                            gradientBarData.horizontal,
                            gradientBarData.rotation,
                            gradientBarData.usePolar);
                        
                        if (gradientTex == null) continue;
                        
                        // 读取输入纹理
                        Texture2D readableInput = GetReadableTexture(inputTexture);
                        if (readableInput == null)
                        {
                            DestroyImmediate(gradientTex);
                            continue;
                        }
                        
                        // 将输入纹理缩放到目标尺寸
                        Texture2D resizedInput = readableInput;
                        bool needCleanupResized = false;
                        if (readableInput.width != width || readableInput.height != height)
                        {
                            resizedInput = ResizeTexture(readableInput, width, height);
                            needCleanupResized = true;
                        }
                        
                        // 合并渐变和输入纹理
                        Texture2D outputTexture = ApplyGradientToTexture(resizedInput, gradientTex, gradientBarData.blendMode, gradientBarData.blendOpacity);
                        
                        // 应用黑底背景（如果启用）
                        Texture2D finalOutput = outputTexture;
                        if (gradientBarData.useBlackBackground)
                        {
                            finalOutput = ApplyBlackBackground(outputTexture);
                        }
                        
                        try
                        {
                            string fileName = $"{inputFileName}_{gradientBarData.outputName}";
                            string outputPath = Path.Combine(outputDir, $"{fileName}.png");
                            
                            int counter = 1;
                            while (File.Exists(outputPath))
                            {
                                outputPath = Path.Combine(outputDir, $"{fileName}_{counter}.png");
                                counter++;
                            }
                            
                            byte[] bytes = finalOutput.EncodeToPNG();
                            File.WriteAllBytes(outputPath, bytes);
                            outputPaths.Add(outputPath);
                            successCount++;
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"保存渐变条失败: {e.Message}");
                        }
                        finally
                        {
                            DestroyImmediate(gradientTex);
                            if (needCleanupResized) DestroyImmediate(resizedInput);
                            if (readableInput != inputTexture) DestroyImmediate(readableInput);
                            DestroyImmediate(outputTexture);
                            if (finalOutput != outputTexture) DestroyImmediate(finalOutput);
                        }
                    }
                    
                    AssetDatabase.Refresh();
                    
                    if (successCount > 0)
                    {
                        string modeName = gradientBarData.usePolar ? "极坐标" : "线性";
                        EditorUtility.DisplayDialog("完成", 
                            $"已处理 {successCount} 个纹理\n" +
                            $"模式: 混合输出 ({modeName})\n" +
                            $"输出目录: {outputDir}", "确定");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", "处理失败，请检查输入纹理", "确定");
                    }
                }
                else
                {
                    // 混合模式下没有输入纹理，提示用户
                    EditorUtility.DisplayDialog("提示", "混合输出模式需要至少一个输入纹理", "确定");
                }
            }
        }

        #endregion
    }
}
