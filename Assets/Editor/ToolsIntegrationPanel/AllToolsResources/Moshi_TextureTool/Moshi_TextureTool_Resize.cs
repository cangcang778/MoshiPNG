using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace MoshiTools
{
    /// <summary>
    /// 图片处理工具 - 图片尺寸调整模块（缩放/画布尺寸）
    /// Texture Tool - Image resize module (Scale/Canvas)
    /// </summary>
    public partial class Moshi_TextureTool
    {
        #region 图片尺寸 数据

        // 图片尺寸标签页数据 - 批量/单个调整图片尺寸
        // Resize Tab Data - Batch/Single texture resize
        private class ResizeTabData
        {
            public List<Texture2D> inputTextures = new List<Texture2D>();
            
            // 图片缩放模式 / Image scaling mode
            public int targetWidth = 512;
            public int targetHeight = 512;
            public float scaleFactor = 2.0f;
            public bool useScaleFactor = true;
            public bool useCustomSize = false;
            public bool keepAspectRatio = true;
            
            // 画布尺寸模式 / Canvas size mode
            public ResizeMode resizeMode = ResizeMode.Scale;
            public int canvasWidth = 512;
            public int canvasHeight = 512;
            public float canvasScaleFactor = 2.0f;           // 画布缩放倍数
            public bool canvasUseScaleFactor = true;          // 画布使用缩放倍数
            public bool canvasUseCustomSize = false;          // 画布使用自定义尺寸
            public CanvasAnchor canvasAnchor = CanvasAnchor.Center;
            [ColorUsage(true, true)]
            public Color canvasFillColor = new Color(0, 0, 0, 0); // 透明黑色 / Transparent black
            
            public string outputSuffix = "_resized";
            public List<Texture2D> originalPreviews = new List<Texture2D>();
            public List<Texture2D> outputPreviews = new List<Texture2D>();
            public Vector2 scroll;
            public Vector2 listScroll;
        }

        private ResizeTabData resizeData = new ResizeTabData();

        #endregion

        #region 图片尺寸 初始化/清理

        partial void InitResizeModule() { }

        partial void ClearResizePreviews()
        {
            ClearPreviews(resizeData.originalPreviews, resizeData.outputPreviews);
        }

        #endregion

        #region 图片尺寸 拖放

        private void HandleResizeDragDrop(UnityEngine.Object[] draggedObjects)
        {
            int added = 0;
            foreach (UnityEngine.Object draggedObject in draggedObjects)
            {
                if (draggedObject is Texture2D texture && !resizeData.inputTextures.Contains(texture))
                {
                    resizeData.inputTextures.Add(texture);
                    added++;
                }
            }
            if (added > 0)
            {
                GenerateResizePreviews();
            }
        }

        #endregion

        #region 图片尺寸 UI

        private void DrawResizeTab()
        {
            EditorGUILayout.LabelField("输入纹理（支持批量）", EditorStyles.boldLabel);
            
            Rect dropArea = EditorGUILayout.GetControlRect(GUILayout.Height(60));
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
                    if (tex != null && !resizeData.inputTextures.Contains(tex))
                    {
                        resizeData.inputTextures.Add(tex);
                        added++;
                    }
                }
                if (added > 0)
                {
                    GenerateResizePreviews();
                }
            }
            if (GUILayout.Button("清空列表", GUILayout.Width(80)))
            {
                resizeData.inputTextures.Clear();
                GenerateResizePreviews();
            }
            EditorGUILayout.EndHorizontal();
            
            // 显示已添加的纹理列表
            EditorGUILayout.LabelField($"已选择纹理 ({resizeData.inputTextures.Count}):");
            
            float scrollHeight = Mathf.Min(150, 25 * resizeData.inputTextures.Count + 5);
            resizeData.listScroll = EditorGUILayout.BeginScrollView(resizeData.listScroll, GUILayout.Height(scrollHeight));
            
            for (int i = 0; i < resizeData.inputTextures.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                resizeData.inputTextures[i] = (Texture2D)EditorGUILayout.ObjectField(
                    resizeData.inputTextures[i], typeof(Texture2D), false, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    resizeData.inputTextures.RemoveAt(i);
                    i--;
                    GenerateResizePreviews();
                }
                EditorGUILayout.EndHorizontal();
            }
            
            if (resizeData.inputTextures.Count == 0)
            {
                EditorGUILayout.HelpBox("请添加纹理（支持多选批量处理）", MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("尺寸设置", EditorStyles.boldLabel);
            
            // 模式选择：图片缩放 / 画布尺寸
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("调整模式:", GUILayout.Width(70));
            ResizeMode newMode = (ResizeMode)GUILayout.SelectionGrid((int)resizeData.resizeMode, 
                new string[] { "图片缩放", "画布尺寸" }, 2);
            if (newMode != resizeData.resizeMode)
            {
                resizeData.resizeMode = newMode;
            }
            EditorGUILayout.EndHorizontal();
            
            // 图片缩放模式
            if (resizeData.resizeMode == ResizeMode.Scale)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("缩放方式:", GUILayout.Width(70));
                bool newUseScaleFactor = GUILayout.Toggle(resizeData.useScaleFactor, "缩放倍数", GUILayout.Width(100));
                bool newUseCustomSize = GUILayout.Toggle(resizeData.useCustomSize, "自定义尺寸", GUILayout.Width(100));
                
                if (newUseScaleFactor != resizeData.useScaleFactor)
                {
                    resizeData.useScaleFactor = true;
                    resizeData.useCustomSize = false;
                }
                else if (newUseCustomSize != resizeData.useCustomSize)
                {
                    resizeData.useScaleFactor = false;
                    resizeData.useCustomSize = true;
                }
                EditorGUILayout.EndHorizontal();
                
                if (resizeData.useScaleFactor)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("缩放倍数:", GUILayout.Width(70));
                    resizeData.scaleFactor = EditorGUILayout.FloatField(resizeData.scaleFactor, GUILayout.Width(60));
                    GUILayout.Label("（>1放大，<1缩小）");
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("目标宽度:", GUILayout.Width(70));
                    resizeData.targetWidth = EditorGUILayout.IntField(Mathf.Max(1, resizeData.targetWidth));
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("目标高度:", GUILayout.Width(70));
                    resizeData.targetHeight = EditorGUILayout.IntField(Mathf.Max(1, resizeData.targetHeight));
                    EditorGUILayout.EndHorizontal();
                    
                    resizeData.keepAspectRatio = EditorGUILayout.Toggle("保持宽高比", resizeData.keepAspectRatio);
                }
            }
            // 画布尺寸模式
            else
            {
                EditorGUILayout.Space(5);
                
                // 缩放方式选择（与图片缩放模式一致）
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("尺寸方式:", GUILayout.Width(70));
                bool newCanvasUseScaleFactor = GUILayout.Toggle(resizeData.canvasUseScaleFactor, "缩放倍数", GUILayout.Width(100));
                bool newCanvasUseCustomSize = GUILayout.Toggle(resizeData.canvasUseCustomSize, "自定义尺寸", GUILayout.Width(100));
                
                if (newCanvasUseScaleFactor != resizeData.canvasUseScaleFactor)
                {
                    resizeData.canvasUseScaleFactor = true;
                    resizeData.canvasUseCustomSize = false;
                }
                else if (newCanvasUseCustomSize != resizeData.canvasUseCustomSize)
                {
                    resizeData.canvasUseScaleFactor = false;
                    resizeData.canvasUseCustomSize = true;
                }
                EditorGUILayout.EndHorizontal();
                
                if (resizeData.canvasUseScaleFactor)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("缩放倍数:", GUILayout.Width(70));
                    resizeData.canvasScaleFactor = EditorGUILayout.FloatField(resizeData.canvasScaleFactor, GUILayout.Width(60));
                    GUILayout.Label("（>1放大，<1缩小）");
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    // 画布尺寸输入
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("画布宽度:", GUILayout.Width(70));
                    resizeData.canvasWidth = EditorGUILayout.IntField(Mathf.Max(1, resizeData.canvasWidth), GUILayout.Width(80));
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("画布高度:", GUILayout.Width(70));
                    resizeData.canvasHeight = EditorGUILayout.IntField(Mathf.Max(1, resizeData.canvasHeight), GUILayout.Width(80));
                    EditorGUILayout.EndHorizontal();
                }
                
                // 锚点选择 - 9宫格
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("锚点位置:", EditorStyles.miniLabel);
                DrawAnchorSelector();
                
                // 填充颜色
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("填充颜色:", GUILayout.Width(70));
                resizeData.canvasFillColor = EditorGUILayout.ColorField(resizeData.canvasFillColor);
                GUILayout.Label($"(A:{resizeData.canvasFillColor.a:F2})");
                EditorGUILayout.EndHorizontal();
            }
            
            resizeData.outputSuffix = EditorGUILayout.TextField("输出后缀", resizeData.outputSuffix);
            
            if (EditorGUI.EndChangeCheck())
            {
                GenerateResizePreviews();
            }
            
            DrawPreviewSection(resizeData.originalPreviews, resizeData.outputPreviews, ref resizeData.scroll);
            
            EditorGUILayout.Space(15);
            string buttonText = resizeData.resizeMode == ResizeMode.Scale ? "执行尺寸调整" : "执行画布调整";
            if (GUILayout.Button(buttonText, GUILayout.Height(30)))
            {
                ProcessResize();
            }
        }
        
        // 绘制锚点选择器 - 9宫格布局
        // Draw anchor selector - 9-grid layout
        private void DrawAnchorSelector()
        {
            // 计算按钮尺寸
            float btnWidth = 40;
            float btnHeight = 25;
            
            // 9宫格按钮布局
            string[] anchorNames = new string[] { "↖", "↑", "↗", "←", "●", "→", "↙", "↓", "↘" };
            CanvasAnchor[] anchors = new CanvasAnchor[] 
            { 
                CanvasAnchor.TopLeft, CanvasAnchor.Top, CanvasAnchor.TopRight,
                CanvasAnchor.Left, CanvasAnchor.Center, CanvasAnchor.Right,
                CanvasAnchor.BottomLeft, CanvasAnchor.Bottom, CanvasAnchor.BottomRight
            };
            
            for (int row = 0; row < 3; row++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                for (int col = 0; col < 3; col++)
                {
                    int index = row * 3 + col;
                    bool isSelected = resizeData.canvasAnchor == anchors[index];
                    
                    // 高亮选中按钮
                    GUIContent content = new GUIContent(anchorNames[index]);
                    if (isSelected)
                    {
                        GUI.color = Color.cyan;
                    }
                    if (GUILayout.Button(content, GUILayout.Width(btnWidth), GUILayout.Height(btnHeight)))
                    {
                        resizeData.canvasAnchor = anchors[index];
                        GenerateResizePreviews();
                    }
                    GUI.color = Color.white;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        #endregion

        #region 图片尺寸 逻辑

        // 图片尺寸预览生成方法
        // Resize Preview Generation
        private void GenerateResizePreviews()
        {
            ClearPreviews(resizeData.originalPreviews, resizeData.outputPreviews);
            
            foreach (var tex in resizeData.inputTextures)
            {
                if (tex != null)
                {
                    GenerateOriginalPreview(tex, resizeData.originalPreviews);
                    
                    Texture2D outputPreview = null;
                    
                    if (resizeData.resizeMode == ResizeMode.Scale)
                    {
                        // 图片缩放模式
                        int targetWidth, targetHeight;
                        CalculateTargetSize(tex.width, tex.height, out targetWidth, out targetHeight);
                        outputPreview = ResizeTexturePoint(tex, targetWidth, targetHeight);
                    }
                    else
                    {
                        // 画布尺寸模式
                        int canvasW, canvasH;
                        CalculateCanvasSize(tex.width, tex.height, out canvasW, out canvasH);
                        outputPreview = ApplyCanvasResize(tex, canvasW, canvasH,
                            resizeData.canvasAnchor, resizeData.canvasFillColor);
                    }
                    
                    if (outputPreview != null)
                    {
                        resizeData.outputPreviews.Add(outputPreview);
                    }
                }
            }
        }
        
        // 计算画布尺寸
        // Calculate canvas size
        private void CalculateCanvasSize(int originalWidth, int originalHeight, out int canvasWidth, out int canvasHeight)
        {
            if (resizeData.canvasUseScaleFactor)
            {
                // 使用缩放倍数计算画布尺寸
                canvasWidth = Mathf.Max(1, Mathf.RoundToInt(originalWidth * resizeData.canvasScaleFactor));
                canvasHeight = Mathf.Max(1, Mathf.RoundToInt(originalHeight * resizeData.canvasScaleFactor));
            }
            else
            {
                // 使用自定义尺寸
                canvasWidth = resizeData.canvasWidth;
                canvasHeight = resizeData.canvasHeight;
            }
        }
        
        // 计算目标尺寸
        // Calculate target size
        private void CalculateTargetSize(int originalWidth, int originalHeight, out int targetWidth, out int targetHeight)
        {
            if (resizeData.useScaleFactor)
            {
                targetWidth = Mathf.Max(1, Mathf.RoundToInt(originalWidth * resizeData.scaleFactor));
                targetHeight = Mathf.Max(1, Mathf.RoundToInt(originalHeight * resizeData.scaleFactor));
            }
            else
            {
                targetWidth = resizeData.targetWidth;
                targetHeight = resizeData.targetHeight;
                
                if (resizeData.keepAspectRatio)
                {
                    float aspectRatio = (float)originalWidth / originalHeight;
                    if (aspectRatio > 1f)
                    {
                        targetHeight = Mathf.RoundToInt(targetWidth / aspectRatio);
                    }
                    else
                    {
                        targetWidth = Mathf.RoundToInt(targetHeight * aspectRatio);
                    }
                }
            }
            
            targetWidth = Mathf.Max(1, targetWidth);
            targetHeight = Mathf.Max(1, targetHeight);
        }
        
        // 计算图片在画布中的位置
        // Calculate image position within canvas
        private void CalculateCanvasPosition(int imgWidth, int imgHeight, 
            int canvasWidth, int canvasHeight, CanvasAnchor anchor,
            out int x, out int y)
        {
            x = 0;
            y = 0;
            
            switch (anchor)
            {
                case CanvasAnchor.TopLeft:
                    x = 0;
                    y = canvasHeight - imgHeight;
                    break;
                case CanvasAnchor.Top:
                    x = (canvasWidth - imgWidth) / 2;
                    y = canvasHeight - imgHeight;
                    break;
                case CanvasAnchor.TopRight:
                    x = canvasWidth - imgWidth;
                    y = canvasHeight - imgHeight;
                    break;
                case CanvasAnchor.Left:
                    x = 0;
                    y = (canvasHeight - imgHeight) / 2;
                    break;
                case CanvasAnchor.Center:
                    x = (canvasWidth - imgWidth) / 2;
                    y = (canvasHeight - imgHeight) / 2;
                    break;
                case CanvasAnchor.Right:
                    x = canvasWidth - imgWidth;
                    y = (canvasHeight - imgHeight) / 2;
                    break;
                case CanvasAnchor.BottomLeft:
                    x = 0;
                    y = 0;
                    break;
                case CanvasAnchor.Bottom:
                    x = (canvasWidth - imgWidth) / 2;
                    y = 0;
                    break;
                case CanvasAnchor.BottomRight:
                    x = canvasWidth - imgWidth;
                    y = 0;
                    break;
            }
            
            // 确保坐标非负
            x = Mathf.Max(0, x);
            y = Mathf.Max(0, y);
        }
        
        // 应用画布尺寸变换
        // Apply canvas resize transformation
        private Texture2D ApplyCanvasResize(Texture2D source, int canvasWidth, 
            int canvasHeight, CanvasAnchor anchor, Color fillColor)
        {
            if (source == null) return null;
            
            // 创建新画布
            Texture2D canvas = new Texture2D(canvasWidth, canvasHeight, TextureFormat.ARGB32, false);
            
            // 填充背景颜色
            Color[] bgPixels = new Color[canvasWidth * canvasHeight];
            for (int i = 0; i < bgPixels.Length; i++)
            {
                bgPixels[i] = fillColor;
            }
            canvas.SetPixels(bgPixels);
            
            // 获取源图像像素
            Color[] sourcePixels = GetTexturePixels(source);
            if (sourcePixels == null)
            {
                DestroyImmediate(canvas);
                return null;
            }
            
            // 计算绘制区域（处理裁剪情况）
            int drawWidth = Mathf.Min(source.width, canvasWidth);
            int drawHeight = Mathf.Min(source.height, canvasHeight);
            
            // 计算源图像的起始读取位置
            int srcStartX = 0;
            int srcStartY = 0;
            
            // 计算画布中的起始位置
            int canvasX, canvasY;
            CalculateCanvasPosition(source.width, source.height, canvasWidth, canvasHeight, anchor, out canvasX, out canvasY);
            
            // 处理图片超出画布的情况（调整读取位置）
            if (source.width > canvasWidth)
            {
                if (anchor == CanvasAnchor.TopRight || anchor == CanvasAnchor.Right || anchor == CanvasAnchor.BottomRight)
                {
                    srcStartX = source.width - canvasWidth;
                }
                else if (anchor == CanvasAnchor.Top || anchor == CanvasAnchor.Center || anchor == CanvasAnchor.Bottom)
                {
                    srcStartX = (source.width - canvasWidth) / 2;
                }
                canvasX = 0;
                drawWidth = canvasWidth;
            }
            
            if (source.height > canvasHeight)
            {
                if (anchor == CanvasAnchor.TopLeft || anchor == CanvasAnchor.Top || anchor == CanvasAnchor.TopRight)
                {
                    srcStartY = source.height - canvasHeight;
                }
                else if (anchor == CanvasAnchor.Left || anchor == CanvasAnchor.Center || anchor == CanvasAnchor.Right)
                {
                    srcStartY = (source.height - canvasHeight) / 2;
                }
                canvasY = 0;
                drawHeight = canvasHeight;
            }
            
            // 将源图像像素复制到画布
            for (int y = 0; y < drawHeight; y++)
            {
                for (int x = 0; x < drawWidth; x++)
                {
                    int srcX = srcStartX + x;
                    int srcY = srcStartY + y;
                    
                    // 确保源坐标在有效范围内
                    if (srcX >= 0 && srcX < source.width && srcY >= 0 && srcY < source.height)
                    {
                        int canvasIdx = (canvasY + y) * canvasWidth + (canvasX + x);
                        int srcIdx = srcY * source.width + srcX;
                        
                        if (canvasIdx >= 0 && canvasIdx < bgPixels.Length && srcIdx >= 0 && srcIdx < sourcePixels.Length)
                        {
                            canvas.SetPixel(canvasX + x, canvasY + y, sourcePixels[srcIdx]);
                        }
                    }
                }
            }
            
            canvas.Apply();
            return canvas;
        }

        // 图片尺寸处理方法 - 批量/单个调整图片尺寸
        // Resize Process Method - Batch/Single texture resize
        private void ProcessResize()
        {
            if (resizeData.inputTextures.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "请选择至少一个纹理", "确定");
                return;
            }
            
            int successCount = 0;
            int failCount = 0;
            
            for (int i = 0; i < resizeData.inputTextures.Count; i++)
            {
                Texture2D tex = resizeData.inputTextures[i];
                if (tex == null) continue;
                
                string path = AssetDatabase.GetAssetPath(tex);
                if (string.IsNullOrEmpty(path))
                {
                    failCount++;
                    continue;
                }
                
                string directory = Path.GetDirectoryName(path);
                string fileName = Path.GetFileNameWithoutExtension(path);
                
                Texture2D outputTexture = null;
                
                if (resizeData.resizeMode == ResizeMode.Scale)
                {
                    // 图片缩放模式
                    int targetWidth, targetHeight;
                    CalculateTargetSize(tex.width, tex.height, out targetWidth, out targetHeight);
                    outputTexture = ResizeTexture(tex, targetWidth, targetHeight);
                }
                else
                {
                    // 画布尺寸模式
                    int canvasW, canvasH;
                    CalculateCanvasSize(tex.width, tex.height, out canvasW, out canvasH);
                    outputTexture = ApplyCanvasResize(tex, canvasW, canvasH,
                        resizeData.canvasAnchor, resizeData.canvasFillColor);
                }
                
                if (outputTexture == null)
                {
                    failCount++;
                    continue;
                }
                
                try
                {
                    byte[] bytes = outputTexture.EncodeToPNG();
                    string outputPath = Path.Combine(directory, $"{fileName}{resizeData.outputSuffix}.png");
                    
                    // 如果文件已存在，添加序号
                    int counter = 1;
                    while (File.Exists(outputPath))
                    {
                        outputPath = Path.Combine(directory, $"{fileName}{resizeData.outputSuffix}_{counter}.png");
                        counter++;
                    }
                    
                    File.WriteAllBytes(outputPath, bytes);
                    successCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"保存纹理失败 {fileName}: {e.Message}");
                    failCount++;
                }
                finally
                {
                    DestroyImmediate(outputTexture);
                }
            }
            
            AssetDatabase.Refresh();
            
            string modeName = resizeData.resizeMode == ResizeMode.Scale ? "尺寸调整" : "画布调整";
            string message = $"{modeName}完成\n成功: {successCount}";
            if (failCount > 0)
            {
                message += $"\n失败: {failCount}";
            }
            EditorUtility.DisplayDialog("完成", message, "确定");
        }

        #endregion
    }
}
