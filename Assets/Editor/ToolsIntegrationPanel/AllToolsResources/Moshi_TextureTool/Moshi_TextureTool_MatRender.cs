using UnityEngine;
using UnityEditor;
using System.IO;

namespace MoshiTools
{
    /// <summary>
    /// 图片处理工具 - 材质渲染模块（将材质球渲染成贴图）
    /// Texture Tool - Material render module (Render material to texture)
    /// </summary>
    public partial class Moshi_TextureTool
    {
        #region 材质渲染 数据

        // 材质渲染标签页数据 - 将材质渲染成贴图
        // Material Render Tab Data - Render material to texture
        private class MatRenderTabData
        {
            public Material material;                       // 要渲染的材质
            public int outputWidth = 512;                   // 输出宽度
            public int outputHeight = 512;                  // 输出高度
            public string outputPath = "Assets";            // 输出路径
            public string outputName = "mat_render";        // 输出文件名
            public Texture2D previewTexture;                // 预览纹理
            public bool showPreview = true;                 // 预览折叠开关
            
            // 渲染设置 / Render settings
            public bool useBlackBackground = false;         // 黑底背景 / Black background
            public bool flipHorizontal = false;             // 水平翻转 / Horizontal flip
            public bool flipVertical = false;               // 垂直翻转 / Vertical flip
            public float rotation = 0f;                     // 旋转角度 (0-360度) / Rotation angle
            
            // 预设尺寸
            public int selectedSizeIndex = 1;               // 默认512x512
            public readonly string[] sizeOptions = { "256", "512", "1024", "2048" };
            public readonly int[] sizeValues = { 256, 512, 1024, 2048 };
        }

        private MatRenderTabData matRenderData = new MatRenderTabData();

        #endregion

        #region 材质渲染 初始化/清理

        partial void InitMatRenderModule() { }

        partial void ClearMatRenderPreviews()
        {
            if (matRenderData.previewTexture != null)
            {
                DestroyImmediate(matRenderData.previewTexture);
                matRenderData.previewTexture = null;
            }
        }

        #endregion

        #region 材质渲染 UI

        // 材质渲染标签页绘制
        // Material render tab drawing
        private void DrawMatRenderTab()
        {
            EditorGUILayout.LabelField("材质渲染", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("将材质球渲染成贴图，支持任意材质渲染", MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            // 材质选择
            // Material selection
            EditorGUILayout.LabelField("渲染设置", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            matRenderData.material = (Material)EditorGUILayout.ObjectField("材质球", matRenderData.material, typeof(Material), false);
            bool materialChanged = EditorGUI.EndChangeCheck();
            
            if (matRenderData.material != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Shader: {matRenderData.material.shader.name}", EditorStyles.miniLabel);
                
                EditorGUILayout.Space(10);
                
                // 渲染设置
                // Render settings
                EditorGUILayout.LabelField("渲染选项", EditorStyles.boldLabel);
                
                // 背景设置 - 按钮组
                // Background settings - button group
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("背景", GUILayout.Width(60));
                
                if (GUILayout.Toggle(!matRenderData.useBlackBackground, "透明", EditorStyles.miniButtonLeft))
                {
                    matRenderData.useBlackBackground = false;
                }
                if (GUILayout.Toggle(matRenderData.useBlackBackground, "黑底", EditorStyles.miniButtonRight))
                {
                    matRenderData.useBlackBackground = true;
                }
                
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                
                // 翻转设置 - 按钮组（可同时启用）
                // Flip settings - button group (can be enabled simultaneously)
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("翻转", GUILayout.Width(60));
                
                matRenderData.flipHorizontal = GUILayout.Toggle(matRenderData.flipHorizontal, "水平", EditorStyles.miniButtonLeft);
                matRenderData.flipVertical = GUILayout.Toggle(matRenderData.flipVertical, "垂直", EditorStyles.miniButtonRight);
                
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                
                // 旋转角度
                // Rotation angle
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("旋转", GUILayout.Width(60));
                matRenderData.rotation = EditorGUILayout.Slider(matRenderData.rotation, 0f, 360f);
                GUILayout.Label("°", GUILayout.Width(15));
                EditorGUILayout.EndHorizontal();
                
                // 材质实时预览区域
                // Material real-time preview area
                EditorGUILayout.Space(10);
                
                // 预览区域 - 可折叠
                // Preview area - foldable
                matRenderData.showPreview = EditorGUILayout.Foldout(matRenderData.showPreview, "材质预览 (3x3方格 384x384)", true, EditorStyles.boldLabel);
                
                if (matRenderData.showPreview && matRenderData.material != null)
                {
                    // 预览区域 - 固定384x384，不随UI缩放
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    
                    // 固定尺寸预览区域 - 384x384 (3x3个128x128方格)
                    Rect previewRect = GUILayoutUtility.GetRect(384, 384, GUILayout.Width(384), GUILayout.Height(384), GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
                    
                    // 绘制背景
                    if (matRenderData.useBlackBackground)
                    {
                        EditorGUI.DrawRect(previewRect, Color.black);
                    }
                    else if (checkerboardTexture != null)
                    {
                        GUI.DrawTexture(previewRect, checkerboardTexture, ScaleMode.ScaleToFit, false);
                    }
                    else
                    {
                        EditorGUI.DrawRect(previewRect, new Color(0.3f, 0.3f, 0.3f, 1f));
                    }
                    
                    // 使用九宫格方式预览材质效果
                    DrawMaterialPreviewInGrid(previewRect, matRenderData.material);
                    
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
            }
            
            EditorGUILayout.Space(10);
            
            // 尺寸设置
            // Size settings
            EditorGUILayout.LabelField("输出尺寸", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("预设:", GUILayout.Width(50));
            
            // 使用按钮组选择预设尺寸
            if (GUILayout.Toggle(matRenderData.selectedSizeIndex == 0, "256", EditorStyles.miniButtonLeft))
            {
                matRenderData.selectedSizeIndex = 0;
                matRenderData.outputWidth = matRenderData.outputHeight = matRenderData.sizeValues[0];
            }
            if (GUILayout.Toggle(matRenderData.selectedSizeIndex == 1, "512", EditorStyles.miniButtonMid))
            {
                matRenderData.selectedSizeIndex = 1;
                matRenderData.outputWidth = matRenderData.outputHeight = matRenderData.sizeValues[1];
            }
            if (GUILayout.Toggle(matRenderData.selectedSizeIndex == 2, "1024", EditorStyles.miniButtonMid))
            {
                matRenderData.selectedSizeIndex = 2;
                matRenderData.outputWidth = matRenderData.outputHeight = matRenderData.sizeValues[2];
            }
            if (GUILayout.Toggle(matRenderData.selectedSizeIndex == 3, "2048", EditorStyles.miniButtonRight))
            {
                matRenderData.selectedSizeIndex = 3;
                matRenderData.outputWidth = matRenderData.outputHeight = matRenderData.sizeValues[3];
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // 自定义尺寸
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("自定义:", GUILayout.Width(50));
            matRenderData.outputWidth = EditorGUILayout.IntField(matRenderData.outputWidth, GUILayout.Width(60));
            GUILayout.Label("x", GUILayout.Width(15));
            matRenderData.outputHeight = EditorGUILayout.IntField(matRenderData.outputHeight, GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            // 限制最小尺寸
            matRenderData.outputWidth = Mathf.Max(1, matRenderData.outputWidth);
            matRenderData.outputHeight = Mathf.Max(1, matRenderData.outputHeight);
            
            EditorGUILayout.Space(10);
            
            // 输出设置
            EditorGUILayout.LabelField("输出设置", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("保存目录:", GUILayout.Width(60));
            matRenderData.outputPath = EditorGUILayout.TextField(matRenderData.outputPath);
            if (GUILayout.Button("浏览", GUILayout.Width(50)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("选择保存目录", "Assets", "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    // 转换为相对路径
                    if (selectedPath.StartsWith(Application.dataPath))
                    {
                        matRenderData.outputPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                    }
                    else if (selectedPath.StartsWith(Application.dataPath.Replace("/", "\\")))
                    {
                        matRenderData.outputPath = "Assets" + selectedPath.Substring(Application.dataPath.Replace("/", "\\").Length);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            matRenderData.outputName = EditorGUILayout.TextField("文件名", matRenderData.outputName);
            
            EditorGUILayout.Space(15);
            
            // 操作按钮
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = matRenderData.material != null;
            if (GUILayout.Button("渲染预览", GUILayout.Height(30)))
            {
                RenderMaterialPreview();
            }
            
            if (GUILayout.Button("保存贴图", GUILayout.Height(30)))
            {
                SaveMaterialRenderTexture();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // 预览区域
            if (matRenderData.previewTexture != null)
            {
                matRenderData.showPreview = EditorGUILayout.Foldout(matRenderData.showPreview, "渲染预览", true, EditorStyles.boldLabel);
                
                if (matRenderData.showPreview)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    
                    // 计算预览尺寸
                    float maxPreviewSize = 256f;
                    float aspectRatio = (float)matRenderData.previewTexture.width / matRenderData.previewTexture.height;
                    float previewWidth, previewHeight;
                    
                    if (aspectRatio > 1f)
                    {
                        previewWidth = maxPreviewSize;
                        previewHeight = maxPreviewSize / aspectRatio;
                    }
                    else
                    {
                        previewHeight = maxPreviewSize;
                        previewWidth = maxPreviewSize * aspectRatio;
                    }
                    
                    GUILayout.Box(matRenderData.previewTexture, GUILayout.Width(previewWidth), GUILayout.Height(previewHeight));
                    
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.LabelField($"尺寸: {matRenderData.previewTexture.width} x {matRenderData.previewTexture.height}", EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        #endregion

        #region 材质渲染 逻辑

        // 九宫格方式绘制材质预览
        // Draw material preview in 3x3 grid (same content repeated)
        private void DrawMaterialPreviewInGrid(Rect rect, Material material)
        {
            if (material == null) return;
            
            int cellSize = 128;
            int gridSize = 3;
            int totalSize = cellSize * gridSize; // 384x384
            
            // 创建临时RenderTexture用于渲染单个单元格
            RenderTexture cellRT = RenderTexture.GetTemporary(cellSize, cellSize, 24, RenderTextureFormat.ARGB32);
            RenderTexture gridRT = RenderTexture.GetTemporary(totalSize, totalSize, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevRT = RenderTexture.active;
            
            // 渲染单个材质单元格
            RenderTexture.active = cellRT;
            GL.Clear(true, true, new Color(0, 0, 0, 0));
            
            GL.PushMatrix();
            GL.LoadOrtho();
            
            material.SetPass(0);
            
            // 根据翻转设置调整UV坐标
            float u0 = matRenderData.flipHorizontal ? 1f : 0f;
            float u1 = matRenderData.flipHorizontal ? 0f : 1f;
            float v0 = matRenderData.flipVertical ? 1f : 0f;
            float v1 = matRenderData.flipVertical ? 0f : 1f;
            
            GL.Begin(GL.QUADS);
            GL.TexCoord2(u0, v0); GL.Vertex3(0, 0, 0);
            GL.TexCoord2(u1, v0); GL.Vertex3(1, 0, 0);
            GL.TexCoord2(u1, v1); GL.Vertex3(1, 1, 0);
            GL.TexCoord2(u0, v1); GL.Vertex3(0, 1, 0);
            GL.End();
            
            GL.PopMatrix();
            
            // 读取单元格像素
            Texture2D cellTexture = new Texture2D(cellSize, cellSize, TextureFormat.ARGB32, false);
            cellTexture.ReadPixels(new Rect(0, 0, cellSize, cellSize), 0, 0);
            cellTexture.Apply();
            
            // 处理旋转（如果需要）
            Color[] cellPixels = cellTexture.GetPixels();
            if (matRenderData.rotation != 0f)
            {
                cellPixels = RotatePixels(cellPixels, cellSize, cellSize, matRenderData.rotation);
            }
            
            // 将单元格像素复制到3x3网格纹理
            RenderTexture.active = gridRT;
            GL.Clear(true, true, new Color(0, 0, 0, 0));
            
            Texture2D gridTexture = new Texture2D(totalSize, totalSize, TextureFormat.ARGB32, false);
            Color[] gridPixels = new Color[totalSize * totalSize];
            
            // 将单元格重复填充到3x3网格
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
                            gridPixels[pixelY * totalSize + pixelX] = cellPixels[y * cellSize + x];
                        }
                    }
                }
            }
            
            gridTexture.SetPixels(gridPixels);
            gridTexture.Apply();
            
            // 恢复之前的RenderTexture
            RenderTexture.active = prevRT;
            
            // 绘制九宫格预览
            GUI.DrawTexture(rect, gridTexture, ScaleMode.ScaleToFit, false);
            
            // 绘制网格分隔线
            Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            float cellWidth = rect.width / 3f;
            float cellHeight = rect.height / 3f;
            
            // 垂直分隔线
            for (int i = 1; i < 3; i++)
            {
                float xPos = rect.x + i * cellWidth;
                Handles.DrawLine(new Vector3(xPos, rect.y), new Vector3(xPos, rect.yMax));
            }
            
            // 水平分隔线
            for (int i = 1; i < 3; i++)
            {
                float yPos = rect.y + i * cellHeight;
                Handles.DrawLine(new Vector3(rect.x, yPos), new Vector3(rect.xMax, yPos));
            }
            
            // 绘制整体边框
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            Handles.DrawSolidRectangleWithOutline(rect, Color.clear, new Color(0.5f, 0.5f, 0.5f));
            
            // 清理临时资源
            DestroyImmediate(cellTexture);
            DestroyImmediate(gridTexture);
            RenderTexture.ReleaseTemporary(cellRT);
            RenderTexture.ReleaseTemporary(gridRT);
            
            // 请求重绘
            Repaint();
        }
        
        // 渲染材质预览
        // Render material preview
        private void RenderMaterialPreview()
        {
            if (matRenderData.material == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选择材质球", "确定");
                return;
            }
            
            // 清理旧预览
            if (matRenderData.previewTexture != null)
            {
                DestroyImmediate(matRenderData.previewTexture);
                matRenderData.previewTexture = null;
            }
            
            // 渲染材质到贴图
            matRenderData.previewTexture = RenderMaterialToTexture(matRenderData.material, matRenderData.outputWidth, matRenderData.outputHeight);
            
            if (matRenderData.previewTexture == null)
            {
                EditorUtility.DisplayDialog("错误", "渲染失败", "确定");
            }
        }
        
        // 将材质渲染成贴图
        // Render material to texture
        private Texture2D RenderMaterialToTexture(Material material, int width, int height)
        {
            // 创建渲染贴图
            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture.active = renderTexture;
            
            // 根据黑底背景设置清除颜色
            Color clearColor = matRenderData.useBlackBackground ? Color.black : new Color(0, 0, 0, 0);
            GL.Clear(true, true, clearColor);
            
            // 创建全屏四边形并渲染材质
            GL.PushMatrix();
            GL.LoadOrtho();
            
            material.SetPass(0);
            
            // 根据翻转设置调整UV坐标
            float u0 = matRenderData.flipHorizontal ? 1f : 0f;
            float u1 = matRenderData.flipHorizontal ? 0f : 1f;
            float v0 = matRenderData.flipVertical ? 1f : 0f;
            float v1 = matRenderData.flipVertical ? 0f : 1f;
            
            GL.Begin(GL.QUADS);
            GL.TexCoord2(u0, v0); GL.Vertex3(0, 0, 0);
            GL.TexCoord2(u1, v0); GL.Vertex3(1, 0, 0);
            GL.TexCoord2(u1, v1); GL.Vertex3(1, 1, 0);
            GL.TexCoord2(u0, v1); GL.Vertex3(0, 1, 0);
            GL.End();
            
            GL.PopMatrix();
            
            // 读取像素到Texture2D
            Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            
            // 处理旋转（如果需要）
            if (matRenderData.rotation != 0f)
            {
                Color[] pixels = texture.GetPixels();
                pixels = RotatePixels(pixels, width, height, matRenderData.rotation);
                texture.SetPixels(pixels);
                texture.Apply();
            }
            
            // 清理
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(renderTexture);
            
            return texture;
        }
        
        // 保存材质渲染贴图
        // Save material render texture
        private void SaveMaterialRenderTexture()
        {
            if (matRenderData.previewTexture == null)
            {
                // 如果没有预览，先渲染
                if (matRenderData.material != null)
                {
                    RenderMaterialPreview();
                }
                
                if (matRenderData.previewTexture == null)
                {
                    EditorUtility.DisplayDialog("错误", "请先渲染预览", "确定");
                    return;
                }
            }
            
            // 确保输出目录存在
            if (!AssetDatabase.IsValidFolder(matRenderData.outputPath))
            {
                Directory.CreateDirectory(matRenderData.outputPath);
            }
            
            // 生成文件路径
            string assetPath = $"{matRenderData.outputPath}/{matRenderData.outputName}.png";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            
            // 编码并保存
            byte[] bytes = matRenderData.previewTexture.EncodeToPNG();
            File.WriteAllBytes(assetPath, bytes);
            
            AssetDatabase.Refresh();
            
            // 设置贴图导入设置
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            
            EditorUtility.DisplayDialog("成功", $"贴图已保存到: {assetPath}", "确定");
            
            // 选中保存的贴图
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        #endregion
    }
}
