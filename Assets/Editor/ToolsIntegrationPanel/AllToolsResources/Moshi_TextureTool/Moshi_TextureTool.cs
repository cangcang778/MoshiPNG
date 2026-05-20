using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MoshiTools
{
    /// <summary>
    /// 图片处理工具 - 拆分/合并/通道/去黑/加黑/UnMult/尺寸/色条/材质渲染/极坐标 (partial class 主文件)
    /// Texture Tool - Split/Merge/Channel/RemoveBlack/AddBlack/UnMult/Resize/GradientBar/MatRender/PolarDisplace (main partial)
    /// </summary>
    public partial class Moshi_TextureTool : EditorWindow
    {
        private const string TOOL_NAME = "图片处理工具";

        #region 共享枚举

        // 标签页模式
        // Tab mode
        private enum TabMode
        {
            Split,              // 拆分序列图
            Merge,              // 合并序列图
            SplitChannel,       // 拆分通道
            CombineChannel,     // 合并通道
            RemoveBlack,        // 去黑背景
            AddBlack,           // 加黑背景
            UnMult,             // UnMult
            Resize,             // 图片尺寸
            GradientBar,        // 色条生成
            MatRender,          // 材质渲染
            PolarDisplace       // 极坐标置换
        }

        // 尺寸模式枚举
        // Size Mode Enumeration
        private enum SizeMode
        {
            Auto,
            Manual
        }

        // 图片尺寸调整模式
        // Resize mode enumeration
        private enum ResizeMode
        {
            Scale,      // 图片缩放 / Image scaling
            Canvas      // 画布尺寸 / Canvas size
        }

        // 画布锚点位置
        // Canvas anchor position
        private enum CanvasAnchor
        {
            TopLeft,    // 左上
            Top,        // 上
            TopRight,   // 右上
            Left,       // 左
            Center,     // 中
            Right,      // 右
            BottomLeft, // 左下
            Bottom,     // 下
            BottomRight // 右下
        }

        // AE风格混合模式枚举
        // AE-style blend mode enumeration
        public enum BlendMode
        {
            // 基础组 / Basic group
            Normal = 0,           // 正常 / Normal
            
            // 变暗组 / Darken group
            Darken = 1,           // 变暗 / Darken
            Multiply = 2,         // 正片叠底 / Multiply
            ColorBurn = 3,        // 颜色加深 / Color Burn
            LinearBurn = 4,       // 线性加深 / Linear Burn
            DarkerColor = 5,      // 深色 / Darker Color
            
            // 变亮组 / Lighten group
            Lighten = 6,          // 变亮 / Lighten
            Screen = 7,           // 滤色 / Screen
            ColorDodge = 8,       // 颜色减淡 / Color Dodge
            LinearDodge = 9,      // 线性减淡(添加) / Linear Dodge (Add)
            LighterColor = 10,    // 浅色 / Lighter Color
            
            // 对比组 / Contrast group
            Overlay = 11,         // 叠加 / Overlay
            SoftLight = 12,       // 柔光 / Soft Light
            HardLight = 13,       // 强光 / Hard Light
            VividLight = 14,      // 亮光 / Vivid Light
            LinearLight = 15,     // 线性光 / Linear Light
            PinLight = 16,        // 点光 / Pin Light
            HardMix = 17,         // 实色混合 / Hard Mix
            
            // 差值组 / Difference group
            Difference = 18,      // 差值 / Difference
            Exclusion = 19,       // 排除 / Exclusion
            Subtract = 20,        // 减去 / Subtract
            Divide = 21,          // 划分 / Divide
            
            // 颜色组 / Color group
            Hue = 22,             // 色相 / Hue
            Saturation = 23,      // 饱和度 / Saturation
            Color = 24,           // 颜色 / Color
            Luminosity = 25       // 明度 / Luminosity
        }

        // 色条输出模式枚举
        // Gradient bar output mode enumeration
        private enum GradientBarOutputMode
        {
            GradientOnly,   // 仅输出色条图 / Output gradient bar only
            Blended         // 混合输出 / Blended output
        }

        #endregion

        #region 共享字段

        // 标签页
        // Tab
        private TabMode currentTab = TabMode.Split;

        // 拖放状态
        // Drag state
        private bool isDragAreaHovered = false;

        // 共享资源
        // Shared resources
        private static Texture2D checkerboardTexture;
        private static GUIStyle dropAreaStyle;
        private static GUIStyle previewLabelStyle;
        private static GUIStyle centeredStyle;

        private static bool stylesInitialized = false;
        private static bool checkerboardInitialized = false;

        // AE混合模式中文名称
        // AE blend mode Chinese names
        private static readonly string[] blendModeNames = new string[]
        {
            // 基础组 / Basic group
            "正常",
            // 变暗组 / Darken group
            "变暗", "正片叠底", "颜色加深", "线性加深", "深色",
            // 变亮组 / Lighten group
            "变亮", "滤色", "颜色减淡", "线性减淡", "浅色",
            // 对比组 / Contrast group
            "叠加", "柔光", "强光", "亮光", "线性光", "点光", "实色混合",
            // 差值组 / Difference group
            "差值", "排除", "减去", "划分",
            // 颜色组 / Color group
            "色相", "饱和度", "颜色", "明度"
        };

        // 进度条
        // Progress bar
        private float progress = 0f;
        private string progressTitle = "";
        private bool isProcessing = false;

        // 纹理池减少GC
        // Texture pool reduces GC
        private List<Texture2D> texturePool = new List<Texture2D>();

        // 缓存原始纹理像素数据
        // Cache original texture pixel data
        private Dictionary<Texture2D, Color[]> texturePixelCache = new Dictionary<Texture2D, Color[]>();

        // 异步处理相关
        // Asynchronous processing related
        private IEnumerator currentCoroutine;
        private bool coroutineRunning;

        #endregion

        #region 窗口生命周期

        [MenuItem("工具/Moshi/" + TOOL_NAME)]
        public static void ShowWindow()
        {
            var window = GetWindow<Moshi_TextureTool>(TOOL_NAME);
            window.minSize = new Vector2(500, 700);
            window.InitializeResources();
        }

        private void InitializeResources()
        {
            if (!checkerboardInitialized)
            {
                CreateCheckerboardTexture();
                checkerboardInitialized = true;
            }

            if (!stylesInitialized)
            {
                InitializeStyles();
                stylesInitialized = true;
            }
        }

        private void OnEnable()
        {
            ClearAllPreviews();
            titleContent = new GUIContent(TOOL_NAME);
            EditorApplication.update += OnEditorUpdate;
            // 各模块初始化
            // Module initialization
            InitSplitModule();
            InitMergeModule();
            InitSplitChannelModule();
            InitCombineChannelModule();
            InitRemoveBlackModule();
            InitAddBlackModule();
            InitUnMultModule();
            InitResizeModule();
            InitGradientBarModule();
            InitMatRenderModule();
            InitPolarDisplaceModule();
        }

        private void OnDisable()
        {
            ClearAllPreviews();
            ClearTexturePool();
            texturePixelCache.Clear();
            EditorApplication.update -= OnEditorUpdate;
            coroutineRunning = false;
            currentCoroutine = null;
        }

        #endregion

        #region OnGUI 总控

        // 左侧标签栏宽度
        // Left tab bar width
        private const float TAB_BAR_WIDTH = 80f;

        public void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            try
            {
                if (isProcessing)
                {
                    // 显示进度条
                    Rect rect = EditorGUILayout.GetControlRect(false, 30);
                    EditorGUI.ProgressBar(rect, progress, progressTitle);
                    Repaint();
                    return;
                }

                if (checkerboardTexture == null)
                    CreateCheckerboardTexture();

                if (dropAreaStyle == null || previewLabelStyle == null || centeredStyle == null)
                    InitializeStyles();

                // 左侧竖排标签页
                // Left vertical tab bar
                DrawTabButtons();

                // 右侧内容区
                // Right content area
                EditorGUILayout.BeginVertical();

                // 标题 + 帮助按钮
                // Title + Help button
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME);
                EditorGUILayout.EndHorizontal();

                // 按标签页分发绘制
                // Dispatch drawing by tab
                switch (currentTab)
                {
                    case TabMode.Split:           DrawSplitTab(); break;
                    case TabMode.Merge:           DrawMergeTab(); break;
                    case TabMode.SplitChannel:    DrawSplitChannelsTab(); break;
                    case TabMode.CombineChannel:  DrawCombineChannelsTab(); break;
                    case TabMode.RemoveBlack:     DrawRemoveBlackTab(); break;
                    case TabMode.AddBlack:        DrawAddBlackBackgroundTab(); break;
                    case TabMode.UnMult:          DrawUnMultTab(); break;
                    case TabMode.Resize:          DrawResizeTab(); break;
                    case TabMode.GradientBar:     DrawGradientBarTab(); break;
                    case TabMode.MatRender:       DrawMatRenderTab(); break;
                    case TabMode.PolarDisplace:   DrawPolarDisplaceTab(); break;
                }

                EditorGUILayout.EndVertical();
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }

            HandleDragAndDrop();
        }

        // 竖排标签页按钮样式（延迟初始化）
        // Vertical tab button style (lazy init)
        private GUIStyle verticalTabStyle;
        private GUIStyle verticalTabActiveStyle;

        private void InitTabStyles()
        {
            if (verticalTabStyle != null) return;

            verticalTabStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 4, 3, 3),
                margin = new RectOffset(0, 0, 0, 0),
                fixedHeight = 22,
                fontSize = 11
            };

            verticalTabActiveStyle = new GUIStyle(verticalTabStyle);
            verticalTabActiveStyle.normal.textColor = Color.white;
            verticalTabActiveStyle.normal.background = EditorStyles.miniButton.active.background;
        }

        /// <summary>
        /// 标签页按钮组（竖向排列，左侧栏）
        /// Tab buttons (vertical layout, left sidebar)
        /// </summary>
        private void DrawTabButtons()
        {
            InitTabStyles();

            EditorGUILayout.BeginVertical(GUILayout.Width(TAB_BAR_WIDTH));

            // 所有标签页竖向排列，默认选中项(Split)在最上方
            // All tabs in a vertical column, default selection (Split) on top
            TabMode[] tabs = {
                TabMode.Split, TabMode.Merge, TabMode.SplitChannel, TabMode.CombineChannel,
                TabMode.RemoveBlack, TabMode.AddBlack, TabMode.UnMult, TabMode.Resize,
                TabMode.GradientBar, TabMode.MatRender, TabMode.PolarDisplace
            };
            string[] labels = {
                "拆分序列", "合并序列", "拆分通道", "合并通道",
                "去黑背景", "加黑背景", "UnMult", "图片尺寸",
                "色条生成", "材质渲染", "极坐标"
            };

            for (int i = 0; i < tabs.Length; i++)
            {
                bool isActive = currentTab == tabs[i];
                GUIStyle style = isActive ? verticalTabActiveStyle : verticalTabStyle;

                if (GUILayout.Toggle(isActive, labels[i], style, GUILayout.Width(TAB_BAR_WIDTH - 2)))
                {
                    if (!isActive) currentTab = tabs[i];
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region 拖放处理

        private void HandleDragAndDrop()
        {
            Event evt = Event.current;
            Rect dropArea = GUILayoutUtility.GetLastRect();

            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    isDragAreaHovered = true;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        // 根据标签页调度拖放处理
                        // Dispatch drag-drop handling by tab
                        switch (currentTab)
                        {
                            case TabMode.Split:          HandleSplitDragDrop(DragAndDrop.objectReferences); break;
                            case TabMode.Merge:          HandleMergeDragDrop(DragAndDrop.objectReferences); break;
                            case TabMode.SplitChannel:   HandleSplitChannelDragDrop(DragAndDrop.objectReferences); break;
                            case TabMode.CombineChannel: HandleCombineChannelDragDrop(DragAndDrop.objectReferences); break;
                            case TabMode.RemoveBlack:    HandleRemoveBlackDragDrop(DragAndDrop.objectReferences); break;
                            case TabMode.AddBlack:       HandleAddBlackDragDrop(DragAndDrop.objectReferences); break;
                            case TabMode.UnMult:         HandleUnMultDragDrop(DragAndDrop.objectReferences); break;
                            case TabMode.Resize:         HandleResizeDragDrop(DragAndDrop.objectReferences); break;
                            case TabMode.GradientBar:    HandleGradientBarDragDrop(DragAndDrop.objectReferences); break;
                            case TabMode.MatRender:      break; // 材质渲染不支持拖放
                            case TabMode.PolarDisplace:  HandlePolarDisplaceDragDrop(DragAndDrop.objectReferences); break;
                        }
                    }
                    break;

                case EventType.Repaint:
                    if (isDragAreaHovered)
                    {
                        isDragAreaHovered = false;
                    }
                    break;
            }
        }

        #endregion

        #region 共享工具方法

        private static void CreateCheckerboardTexture()
        {
            if (checkerboardTexture != null) return;

            checkerboardTexture = new Texture2D(16, 16, TextureFormat.ARGB32, false);
            var c0 = new Color(0.8f, 0.8f, 0.8f, 1f);
            var c1 = new Color(0.6f, 0.6f, 0.6f, 1f);

            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    checkerboardTexture.SetPixel(x, y, ((x + y) % 2 == 0) ? c0 : c1);
                }
            }
            checkerboardTexture.Apply();
            checkerboardTexture.hideFlags = HideFlags.HideAndDontSave;
        }

        private static void InitializeStyles()
        {
            if (dropAreaStyle != null) return;

            GUIStyle baseStyle = EditorStyles.helpBox;
            if (baseStyle == null)
            {
                baseStyle = new GUIStyle(GUI.skin.box);
            }

            dropAreaStyle = new GUIStyle(baseStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                padding = new RectOffset(10, 10, 10, 10)
            };

            previewLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };

            centeredStyle = new GUIStyle()
            {
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void OnEditorUpdate()
        {
            if (coroutineRunning && currentCoroutine != null)
            {
                if (!currentCoroutine.MoveNext())
                {
                    coroutineRunning = false;
                    currentCoroutine = null;
                    isProcessing = false;
                }
                Repaint();
            }
        }

        private void StartEditorCoroutine(IEnumerator coroutine)
        {
            if (coroutineRunning)
            {
                Debug.LogWarning("A coroutine is already running");
                return;
            }

            currentCoroutine = coroutine;
            coroutineRunning = true;
            isProcessing = true;
        }

        private void UpdateProgress(string title, float progressValue)
        {
            progressTitle = title;
            progress = Mathf.Clamp01(progressValue);
            Repaint();
        }

        #endregion

        #region 纹理池管理

        private Texture2D GetFromTexturePool(int width, int height)
        {
            for (int i = texturePool.Count - 1; i >= 0; i--)
            {
                Texture2D tex = texturePool[i];

                if (tex == null)
                {
                    texturePool.RemoveAt(i);
                    continue;
                }

                if (tex.width == width && tex.height == height)
                {
                    texturePool.RemoveAt(i);
                    return tex;
                }
            }
            return null;
        }

        private void ReturnToTexturePool(Texture2D texture)
        {
            if (texture != null && !texturePool.Contains(texture))
            {
                texturePool.Add(texture);
            }
        }

        private void ClearTexturePool()
        {
            foreach (var tex in texturePool)
            {
                if (tex != null) DestroyImmediate(tex);
            }
            texturePool.Clear();
        }

        #endregion

        #region 纹理格式/缩放/像素工具方法

        // 检查纹理格式是否支持 SetPixels 操作
        // Check if texture format supports SetPixels operation
        private bool IsFormatSupportedForSetPixels(TextureFormat format)
        {
            return format == TextureFormat.ARGB32 ||
                   format == TextureFormat.RGBA32 ||
                   format == TextureFormat.RGB24 ||
                   format == TextureFormat.Alpha8 ||
                   format == TextureFormat.RGBAFloat ||
                   format == TextureFormat.RGBAHalf ||
                   format == TextureFormat.R8;
        }

        // 确保纹理使用支持的格式
        // Ensure texture uses supported format
        private Texture2D EnsureSupportedFormat(Texture2D source)
        {
            if (source == null) return null;

            if (IsFormatSupportedForSetPixels(source.format))
            {
                return source;
            }

            Texture2D newTexture = new Texture2D(source.width, source.height, TextureFormat.ARGB32, false);
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height);
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);
            newTexture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            newTexture.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return newTexture;
        }

        // 高质量纹理缩放
        // High quality texture resize
        private Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
        {
            if (source == null) return null;

            Texture2D safeSource = EnsureSupportedFormat(source);

            Texture2D newTexture = GetFromTexturePool(newWidth, newHeight);
            if (newTexture == null)
            {
                newTexture = new Texture2D(newWidth, newHeight, TextureFormat.ARGB32, false);
            }

            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            safeSource.filterMode = FilterMode.Bilinear;
            RenderTexture.active = rt;
            Graphics.Blit(safeSource, rt);

            newTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            newTexture.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            if (safeSource != source)
            {
                DestroyImmediate(safeSource);
            }

            return newTexture;
        }

        // 使用点过滤模式的纹理缩放
        // Texture resize with point filtering
        private Texture2D ResizeTexturePoint(Texture2D source, int newWidth, int newHeight)
        {
            if (source == null) return null;

            source.filterMode = FilterMode.Point;
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            rt.filterMode = FilterMode.Point;
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);
            Texture2D newTexture = new Texture2D(newWidth, newHeight);
            newTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            newTexture.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            return newTexture;
        }


        // 获取纹理像素数据（带缓存）
        // Get texture pixel data (with cache)
        private Color[] GetTexturePixels(Texture2D texture)
        {
            if (texture == null) return null;

            if (texturePixelCache.TryGetValue(texture, out Color[] cachedPixels))
            {
                return cachedPixels;
            }

            string path = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = null;
            bool wasReadable = true;

            if (!string.IsNullOrEmpty(path))
            {
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    wasReadable = importer.isReadable;
                    if (!wasReadable)
                    {
                        importer.isReadable = true;
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    }
                }
            }

            Color[] pixels = texture.GetPixels();
            texturePixelCache[texture] = pixels;

            if (importer != null && !wasReadable)
            {
                importer.isReadable = false;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            return pixels;
        }

        // 获取可读纹理
        // Get readable texture
        private Texture2D GetReadableTexture(Texture2D source)
        {
            if (source == null) return null;

            string path = AssetDatabase.GetAssetPath(source);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer != null && !importer.isReadable)
            {
                // 创建临时可读副本
                Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);

                RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, rt);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readable.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);

                return readable;
            }

            return source;
        }

        // 创建Asset文件夹
        // Create Asset folder
        private bool CreateAssetFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "Assets")
                return true;

            if (!path.StartsWith("Assets"))
                return false;

            string[] folders = path.Split('/');
            string currentPath = "Assets";

            for (int i = 1; i < folders.Length; i++)
            {
                string nextPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    string guid = AssetDatabase.CreateFolder(currentPath, folders[i]);
                    if (string.IsNullOrEmpty(guid))
                        return false;
                }
                currentPath = nextPath;
            }

            return true;
        }

        // 处理透明纹理
        // Process transparent texture
        private Texture2D ProcessTransparentTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            Texture2D resized = ResizeTexturePoint(source, targetWidth, targetHeight);

            Texture2D processed = new Texture2D(targetWidth, targetHeight, TextureFormat.ARGB32, false);

            float blackThreshold = 0f;
            float blendIntensity = 1f;

            Color[] pixels = GetTexturePixels(resized);
            if (pixels != null)
            {
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color pixel = pixels[i];
                    float brightness = 0.299f * pixel.r + 0.587f * pixel.g + 0.114f * pixel.b;
                    float newGray = Mathf.Clamp01((brightness - blackThreshold) / (1 - blackThreshold));
                    newGray = Mathf.Lerp(brightness, newGray, blendIntensity);
                    newGray *= pixel.a;
                    pixels[i] = new Color(newGray, newGray, newGray, 1f);
                }

                processed.SetPixels(pixels);
                processed.Apply();
            }

            DestroyImmediate(resized);
            return processed;
        }

        // 应用黑底背景
        // Apply black background
        private Texture2D ApplyBlackBackground(Texture2D source)
        {
            if (source == null) return null;

            int width = source.width;
            int height = source.height;

            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] sourcePixels = source.GetPixels();
            Color[] resultPixels = new Color[width * height];

            for (int i = 0; i < resultPixels.Length; i++)
            {
                Color c = sourcePixels[i];
                float alpha = c.a;
                resultPixels[i] = new Color(c.r * alpha, c.g * alpha, c.b * alpha, 1f);
            }

            result.SetPixels(resultPixels);
            result.Apply();
            return result;
        }

        // 旋转像素数组
        // Rotate pixel array
        private Color[] RotatePixels(Color[] pixels, int width, int height, float angle)
        {
            Color[] rotated = new Color[pixels.Length];
            float radAngle = -angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radAngle);
            float sin = Mathf.Sin(radAngle);

            float centerX = width * 0.5f;
            float centerY = height * 0.5f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;

                    float srcX = cos * dx - sin * dy + centerX;
                    float srcY = sin * dx + cos * dy + centerY;

                    int x0 = Mathf.Clamp(Mathf.FloorToInt(srcX), 0, width - 1);
                    int y0 = Mathf.Clamp(Mathf.FloorToInt(srcY), 0, height - 1);
                    int x1 = Mathf.Clamp(x0 + 1, 0, width - 1);
                    int y1 = Mathf.Clamp(y0 + 1, 0, height - 1);

                    float fx = srcX - x0;
                    float fy = srcY - y0;

                    Color c00 = pixels[y0 * width + x0];
                    Color c10 = pixels[y0 * width + x1];
                    Color c01 = pixels[y1 * width + x0];
                    Color c11 = pixels[y1 * width + x1];

                    Color c0 = Color.Lerp(c00, c10, fx);
                    Color c1 = Color.Lerp(c01, c11, fx);
                    Color c = Color.Lerp(c0, c1, fy);

                    rotated[y * width + x] = c;
                }
            }

            return rotated;
        }

        #endregion

        #region 预览通用方法

        private void ClearAllPreviews()
        {
            ClearSplitPreviews();
            ClearMergePreviews();
            ClearRemoveBlackPreviews();
            ClearSplitChannelPreviews();
            ClearCombineChannelPreviews();
            ClearAddBlackPreviews();
            ClearUnMultPreviews();
            ClearResizePreviews();
            ClearGradientBarPreviews();
            ClearMatRenderPreviews();
            ClearPolarDisplacePreviews();
        }

        private void ClearPreviews(List<Texture2D> originalPreviews, List<Texture2D> outputPreviews)
        {
            foreach (var tex in originalPreviews)
            {
                if (tex != null) ReturnToTexturePool(tex);
            }
            originalPreviews.Clear();

            foreach (var tex in outputPreviews)
            {
                if (tex != null) ReturnToTexturePool(tex);
            }
            outputPreviews.Clear();
        }

        private void GenerateOriginalPreview(Texture2D texture, List<Texture2D> previewList)
        {
            if (texture == null) return;

            try
            {
                Texture2D preview = GetFromTexturePool(texture.width, texture.height);
                if (preview == null)
                {
                    preview = new Texture2D(texture.width, texture.height, TextureFormat.ARGB32, false);
                }

                Color[] pixels = GetTexturePixels(texture);
                if (pixels != null)
                {
                    preview.SetPixels(pixels);
                    preview.Apply();
                }

                previewList.Add(preview);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"生成纹理预览错误: {e.Message}");
            }
        }

        private void DrawPreviewSection(List<Texture2D> originalPreviews, List<Texture2D> outputPreviews, ref Vector2 scroll)
        {
            EditorGUILayout.Space(10);
            scroll = EditorGUILayout.BeginScrollView(scroll);

            try
            {
                if (originalPreviews != null && originalPreviews.Count > 0)
                {
                    EditorGUILayout.LabelField("原始纹理:", previewLabelStyle);

                    if (originalPreviews.Count == 1)
                    {
                        EditorGUILayout.BeginHorizontal(centeredStyle);
                        DrawTexturePreview(originalPreviews[0]);
                        EditorGUILayout.EndHorizontal();
                    }
                    else
                    {
                        DrawTexturesGrid(originalPreviews);
                    }
                }

                if (outputPreviews != null && outputPreviews.Count > 0)
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("输出预览:", previewLabelStyle);

                    if (outputPreviews.Count == 1)
                    {
                        EditorGUILayout.BeginHorizontal(centeredStyle);
                        DrawTexturePreview(outputPreviews[0]);
                        EditorGUILayout.EndHorizontal();
                    }
                    else
                    {
                        DrawTexturesGrid(outputPreviews);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("无可用输出预览。请添加纹理并设置参数。", MessageType.Info);
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawTexturePreview(Texture2D texture)
        {
            if (texture == null) return;

            if (checkerboardTexture == null)
                CreateCheckerboardTexture();

            if (previewLabelStyle == null || centeredStyle == null)
                InitializeStyles();

            float displayWidth = texture.width;
            float displayHeight = texture.height;
            float maxDimension = Mathf.Max(displayWidth, displayHeight);

            if (maxDimension > 256)
            {
                float scale = 256 / maxDimension;
                displayWidth *= scale;
                displayHeight *= scale;
            }

            Rect r = EditorGUILayout.GetControlRect(
                GUILayout.Width(displayWidth),
                GUILayout.Height(displayHeight));

            if (Event.current.type == EventType.Repaint && checkerboardTexture != null)
            {
                GUI.DrawTextureWithTexCoords(r, checkerboardTexture,
                    new Rect(0, 0, r.width / checkerboardTexture.width, r.height / checkerboardTexture.height));
            }

            EditorGUI.DrawTextureTransparent(r, texture);

            string resolutionText = $"{texture.width}×{texture.height}";
            GUIStyle resolutionStyle = new GUIStyle(EditorStyles.miniLabel);
            resolutionStyle.normal.textColor = Color.white;
            resolutionStyle.alignment = TextAnchor.LowerCenter;

            Vector2 textSize = resolutionStyle.CalcSize(new GUIContent(resolutionText));
            float padding = 4f;
            float bgHeight = textSize.y + padding * 2;

            Rect bgRect = new Rect(r.x, r.y + r.height - bgHeight, r.width, bgHeight);
            EditorGUI.DrawRect(bgRect, new Color(0f, 0f, 0f, 0.7f));

            Rect textRect = new Rect(r.x, r.y + r.height - textSize.y - padding, r.width, textSize.y);
            GUI.Label(textRect, resolutionText, resolutionStyle);
        }

        private void DrawTexturesGrid(List<Texture2D> textures)
        {
            if (textures == null || textures.Count == 0) return;

            float availableWidth = EditorGUIUtility.currentViewWidth - 30f;
            float spacing = 5f;
            float maxWidth = 256f;

            List<Vector2> displaySizes = new List<Vector2>();
            foreach (var texture in textures)
            {
                if (texture == null) continue;

                float width = texture.width;
                float height = texture.height;
                float maxDimension = Mathf.Max(width, height);

                if (maxDimension > maxWidth)
                {
                    float scale = maxWidth / maxDimension;
                    width *= scale;
                    height *= scale;
                }
                displaySizes.Add(new Vector2(width, height));
            }

            int index = 0;
            List<List<Texture2D>> rows = new List<List<Texture2D>>();
            List<List<Vector2>> rowSizes = new List<List<Vector2>>();

            while (index < textures.Count)
            {
                List<Texture2D> currentRow = new List<Texture2D>();
                List<Vector2> currentRowSizes = new List<Vector2>();
                float rowWidth = 0f;

                while (index < textures.Count)
                {
                    Texture2D tex = textures[index];
                    if (tex == null)
                    {
                        index++;
                        continue;
                    }

                    Vector2 size = displaySizes[index];
                    float requiredWidth = rowWidth + (rowWidth > 0 ? spacing : 0) + size.x;

                    if (rowWidth > 0 && requiredWidth > availableWidth)
                    {
                        break;
                    }

                    currentRow.Add(tex);
                    currentRowSizes.Add(size);
                    rowWidth = requiredWidth;
                    index++;
                }

                if (currentRow.Count > 0)
                {
                    rows.Add(currentRow);
                    rowSizes.Add(currentRowSizes);
                }
            }

            for (int i = 0; i < rows.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                try
                {
                    for (int j = 0; j < rows[i].Count; j++)
                    {
                        Texture2D tex = rows[i][j];
                        Vector2 size = rowSizes[i][j];

                        Rect r = EditorGUILayout.GetControlRect(
                            GUILayout.Width(size.x),
                            GUILayout.Height(size.y));

                        if (Event.current.type == EventType.Repaint && checkerboardTexture != null)
                        {
                            GUI.DrawTextureWithTexCoords(r, checkerboardTexture,
                                new Rect(0, 0, r.width / checkerboardTexture.width, r.height / checkerboardTexture.height));
                        }

                        EditorGUI.DrawTextureTransparent(r, tex);

                        string resolutionText = $"{tex.width}×{tex.height}";
                        GUIStyle resolutionStyle = new GUIStyle(EditorStyles.miniLabel);
                        resolutionStyle.normal.textColor = Color.white;
                        resolutionStyle.alignment = TextAnchor.LowerCenter;

                        Vector2 textSize = resolutionStyle.CalcSize(new GUIContent(resolutionText));
                        float padding = 4f;
                        float bgHeight = textSize.y + padding * 2;

                        Rect bgRect = new Rect(r.x, r.y + r.height - bgHeight, r.width, bgHeight);
                        EditorGUI.DrawRect(bgRect, new Color(0f, 0f, 0f, 0.7f));

                        Rect textRect = new Rect(r.x, r.y + r.height - textSize.y - padding, r.width, textSize.y);
                        GUI.Label(textRect, resolutionText, resolutionStyle);

                        if (j < rows[i].Count - 1)
                        {
                            GUILayout.Space(spacing);
                        }
                    }
                }
                finally
                {
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        #endregion

        #region Partial 初始化桩方法（各模块各自实现）

        partial void InitSplitModule();
        partial void InitMergeModule();
        partial void InitSplitChannelModule();
        partial void InitCombineChannelModule();
        partial void InitRemoveBlackModule();
        partial void InitAddBlackModule();
        partial void InitUnMultModule();
        partial void InitResizeModule();
        partial void InitGradientBarModule();
        partial void InitMatRenderModule();
        partial void InitPolarDisplaceModule();

        #endregion

        #region Partial 清理桩方法

        partial void ClearSplitPreviews();
        partial void ClearMergePreviews();
        partial void ClearRemoveBlackPreviews();
        partial void ClearSplitChannelPreviews();
        partial void ClearCombineChannelPreviews();
        partial void ClearAddBlackPreviews();
        partial void ClearUnMultPreviews();
        partial void ClearResizePreviews();
        partial void ClearGradientBarPreviews();
        partial void ClearMatRenderPreviews();
        partial void ClearPolarDisplacePreviews();

        #endregion
    }
}
