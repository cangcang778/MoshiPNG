using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Presets;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

/// <summary>
/// PSD位置导入工具
/// 将多张图片批量生成材质球和Quad子对象
/// </summary>
public class Moshi_ImgToQuad : EditorWindow
{
    #region 数据结构
    
    /// <summary>
    /// 图片项数据
    /// </summary>
    [System.Serializable]
    public class ImageItem
    {
        public bool isSelected = true;
        public string name;
        public Texture2D texture;
        public int width;
        public int height;
        
        // PSD图层位置信息
        public bool hasPsdPosition = false;
        public Vector2 psdPosition;        // 3D模式坐标（已乘以pixelUnit）
        public Vector2 psdPixelPosition;   // 原始像素坐标（用于2D模式）
        public int psdLayerIndex;
        public int psdCanvasWidth;         // PSD画布宽度
        public int psdCanvasHeight;        // PSD画布高度
        
        public ImageItem(Texture2D tex)
        {
            texture = tex;
            name = tex.name;
            width = tex.width;
            height = tex.height;
            hasPsdPosition = false;
        }
        
        // 从PSD图层创建
        public ImageItem(string layerName, Texture2D tex, Vector2 position, int layerIndex)
        {
            texture = tex;
            name = layerName;
            width = tex.width;
            height = tex.height;
            hasPsdPosition = true;
            psdPosition = position;
            psdLayerIndex = layerIndex;
        }
        
        // 从PSD图层创建（完整版本，包含像素坐标）
        public ImageItem(string layerName, Texture2D tex, Vector2 position, Vector2 pixelPosition, 
                         int layerIndex, int canvasWidth, int canvasHeight)
        {
            texture = tex;
            name = layerName;
            width = tex != null ? tex.width : 0;
            height = tex != null ? tex.height : 0;
            hasPsdPosition = true;
            psdPosition = position;
            psdPixelPosition = pixelPosition;
            psdLayerIndex = layerIndex;
            psdCanvasWidth = canvasWidth;
            psdCanvasHeight = canvasHeight;
        }
    }
    
    /// <summary>
    /// PSD图层数据
    /// </summary>
    [System.Serializable]
    public class PsdLayerInfo
    {
        public string name;
        public int index;
        public bool isVisible;
        public bool isGroup;
        public int left;
        public int top;
        public int right;
        public int bottom;
        public bool isSelected = true;
        
        public int Width => right - left;
        public int Height => bottom - top;
        public float CenterX => left + Width / 2f;
        public float CenterY => top + Height / 2f;
        
        // 通道信息
        public List<PsdChannelInfo> channels = new List<PsdChannelInfo>();
        public long channelDataStart; // 通道数据在文件中的起始位置
        
        // 导出的贴图
        public Texture2D texture;
        public string texturePath;
    }
    
    /// <summary>
    /// PSD通道信息
    /// </summary>
    [System.Serializable]
    public class PsdChannelInfo
    {
        public short channelId; // -1=透明蒙版, 0=红, 1=绿, 2=蓝
        public uint dataLength;
    }
    
    /// <summary>
    /// PSD文档数据
    /// </summary>
    [System.Serializable]
    public class PsdDocumentInfo
    {
        public string filePath;
        public string fileName;
        public int canvasWidth;
        public int canvasHeight;
        public List<PsdLayerInfo> layers = new List<PsdLayerInfo>();
    }
    
    /// <summary>
    /// PSD坐标原点模式
    /// </summary>
    public enum PsdOriginMode
    {
        Center,     // 画布中心
        TopLeft,    // 左上角
        BottomLeft  // 左下角
    }
    
    /// <summary>
    /// 尺寸基准模式
    /// </summary>
    public enum SizeMode
    {
        PixelRatio,  // 像素比例
        WidthOne,    // 宽度为1
        HeightOne    // 高度为1
    }
    
    /// <summary>
    /// 生成模式
    /// </summary>
    public enum GenerateMode
    {
        Quad,           // Quad模式
        SpriteRenderer, // Sprite模式
        Image,          // Image模式（UGUI）
        ParticleSystem  // 粒子系统模式
    }
    
    /// <summary>
    /// Sprite Transform类型
    /// </summary>
    public enum SpriteTransformType
    {
        Transform3D,      // 3D标准
        RectTransform2D   // 2D矩形
    }
    
    #endregion
    
    #region 字段
    
    // 基础设置
    private GameObject targetObject;
    private string parentName = "fx_quad_group";
    private Shader selectedShader;
    private string texturePropertyName = "_MainTex";
    private string materialFolder = "";
    private SizeMode sizeMode = SizeMode.PixelRatio;
    private float pixelUnit = 0.01f;
    
    // 生成模式
    private GenerateMode generateMode = GenerateMode.Quad;
    private Preset particlePreset;
    
    // Sprite模式设置
    private string sortingLayerName = "Default";
    private int sortingOrder = 0;
    
    // Image模式设置
    private bool imageRaycastTarget = false;
    
    // Transform类型设置（所有模式共用）
    private SpriteTransformType transformType = SpriteTransformType.Transform3D;
    private float rectScale = 100f;
    private float zSpacing = 0.01f; // Z轴间距，用于图层前后排序
    
    // 生成列表
    private List<ImageItem> imageItems = new List<ImageItem>();
    private Vector2 listScrollPos;
    private Vector2 previewScrollPos;
    
    // PSD导入相关
    private bool psdSectionFoldout = true;
    private PsdDocumentInfo currentPsd;
    private Vector2 psdLayerScrollPos;
    private PsdOriginMode psdOriginMode = PsdOriginMode.Center;
    private bool psdOnlyVisible = true;
    private bool psdSkipGroups = true;
    private string psdTextureOutputPath = "Assets/Textures/PSD_Export";
    private Rect psdDropRect;
    
    private const string TOOL_NAME = "图片转面片";
    
    #endregion
    
    #region 窗口初始化
    
    [MenuItem("工具/Moshi/" + TOOL_NAME)]
    public static void ShowWindow()
    {
        var window = GetWindow<Moshi_ImgToQuad>(TOOL_NAME);
        window.minSize = new Vector2(450, 600);
    }
    
    private void OnEnable()
    {
        RefreshTargetObject();
        
        // 默认Shader
        if (selectedShader == null)
        {
            selectedShader = Shader.Find("Pandavfx/Pandavfx_v2.3");
            if (selectedShader == null)
                selectedShader = Shader.Find("Unlit/Transparent");
        }
    }
    
    #endregion
    
    #region GUI绑定
    
    private void OnGUI()
    {
        EditorGUILayout.Space(5);
        
        DrawBasicSettings();
        EditorGUILayout.Space(10);
        
        DrawPsdImportSection();
        EditorGUILayout.Space(10);
        
        DrawGenerateList();
        EditorGUILayout.Space(10);
        
        DrawOutputPreview();
        EditorGUILayout.Space(10);
        
        DrawGenerateButton();
        
        HandleDragAndDrop();
        HandlePsdDragAndDrop();
    }
    
    #endregion
    
    #region PSD导入区域
    
    private void DrawPsdImportSection()
    {
        EditorGUILayout.BeginHorizontal();
        
        // 折叠标题
        string foldoutTitle = currentPsd != null 
            ? $"═══ PSD导入 ═══  📄 {currentPsd.fileName} ({currentPsd.layers.Count(l => l.isSelected)}个图层已选)"
            : "═══ PSD导入 ═══";
        
        psdSectionFoldout = EditorGUILayout.Foldout(psdSectionFoldout, foldoutTitle, true, EditorStyles.foldoutHeader);
        
        EditorGUILayout.EndHorizontal();
        
        if (!psdSectionFoldout) return;
        
        // PSD拖拽区域
        if (currentPsd == null)
        {
            DrawPsdDropZone();
        }
        else
        {
            DrawPsdInfo();
            
            // 检查是否被清除
            if (currentPsd == null) return;
            
            DrawPsdSettings();
            DrawPsdLayerList();
            DrawPsdImportButton();
        }
    }
    
    private void DrawPsdDropZone()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(80));
        GUILayout.FlexibleSpace();
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("🎨 拖拽PSD文件到此处", EditorStyles.centeredGreyMiniLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("选择文件", GUILayout.Width(80)))
        {
            string path = EditorUtility.OpenFilePanel("选择PSD文件", "", "psd");
            if (!string.IsNullOrEmpty(path))
            {
                LoadPsdFile(path);
            }
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();
        
        // 记录拖拽区域
        if (Event.current.type == EventType.Repaint)
        {
            psdDropRect = GUILayoutUtility.GetLastRect();
        }
    }
    
    private void DrawPsdInfo()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"📄 {currentPsd.fileName}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("清除", EditorStyles.miniButton, GUILayout.Width(50)))
        {
            currentPsd = null;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();
        
        int visibleCount = currentPsd.layers.Count(l => l.isVisible && !l.isGroup);
        int groupCount = currentPsd.layers.Count(l => l.isGroup);
        EditorGUILayout.LabelField($"   画布尺寸: {currentPsd.canvasWidth} × {currentPsd.canvasHeight}");
        EditorGUILayout.LabelField($"   图层总数: {currentPsd.layers.Count}个 | 可见图层: {visibleCount}个 | 图层组: {groupCount}个");
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawPsdSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("导入设置", EditorStyles.boldLabel);
        
        // 坐标原点
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("坐标原点", GUILayout.Width(60));
        
        if (GUILayout.Toggle(psdOriginMode == PsdOriginMode.Center, "中心点", EditorStyles.miniButtonLeft))
            psdOriginMode = PsdOriginMode.Center;
        if (GUILayout.Toggle(psdOriginMode == PsdOriginMode.TopLeft, "左上角", EditorStyles.miniButtonMid))
            psdOriginMode = PsdOriginMode.TopLeft;
        if (GUILayout.Toggle(psdOriginMode == PsdOriginMode.BottomLeft, "左下角", EditorStyles.miniButtonRight))
            psdOriginMode = PsdOriginMode.BottomLeft;
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        // 图层过滤
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("图层过滤", GUILayout.Width(60));
        psdOnlyVisible = GUILayout.Toggle(psdOnlyVisible, "仅可见", EditorStyles.miniButtonLeft);
        psdSkipGroups = GUILayout.Toggle(psdSkipGroups, "跳过组", EditorStyles.miniButtonRight);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        // 贴图输出目录
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("贴图目录", GUILayout.Width(60));
        psdTextureOutputPath = EditorGUILayout.TextField(psdTextureOutputPath);
        if (GUILayout.Button("选择", GUILayout.Width(50)))
        {
            string path = EditorUtility.OpenFolderPanel("选择贴图输出目录", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                {
                    psdTextureOutputPath = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawPsdLayerList()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinHeight(120));
        EditorGUILayout.LabelField("图层列表", EditorStyles.boldLabel);
        
        // 表头
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("☑", GUILayout.Width(25));
        EditorGUILayout.LabelField("#", GUILayout.Width(25));
        EditorGUILayout.LabelField("图层名称", GUILayout.MinWidth(100));
        EditorGUILayout.LabelField("位置(X,Y)", GUILayout.Width(100));
        EditorGUILayout.LabelField("尺寸", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();
        
        // 列表
        psdLayerScrollPos = EditorGUILayout.BeginScrollView(psdLayerScrollPos, GUILayout.MaxHeight(150));
        
        var filteredLayers = currentPsd.layers
            .Where(l => (!psdOnlyVisible || l.isVisible) && (!psdSkipGroups || !l.isGroup))
            .ToList();
        
        foreach (var layer in filteredLayers)
        {
            EditorGUILayout.BeginHorizontal();
            
            // 勾选
            layer.isSelected = EditorGUILayout.Toggle(layer.isSelected, GUILayout.Width(25));
            
            // 序号
            EditorGUILayout.LabelField(layer.index.ToString(), GUILayout.Width(25));
            
            // 名称（图层组用不同样式）
            GUIStyle nameStyle = layer.isGroup ? EditorStyles.boldLabel : EditorStyles.label;
            string prefix = layer.isGroup ? "📁 " : (layer.isVisible ? "" : "(隐藏) ");
            EditorGUILayout.LabelField(prefix + layer.name, nameStyle, GUILayout.MinWidth(100));
            
            // 位置
            if (!layer.isGroup)
            {
                EditorGUILayout.LabelField($"({layer.CenterX:F0}, {layer.CenterY:F0})", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{layer.Width}×{layer.Height}", GUILayout.Width(80));
            }
            else
            {
                EditorGUILayout.LabelField("-", GUILayout.Width(100));
                EditorGUILayout.LabelField("(图层组)", GUILayout.Width(80));
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        
        // 全选/反选
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全选", EditorStyles.miniButtonLeft, GUILayout.Width(50)))
        {
            foreach (var layer in filteredLayers)
                layer.isSelected = true;
        }
        if (GUILayout.Button("反选", EditorStyles.miniButtonRight, GUILayout.Width(50)))
        {
            foreach (var layer in filteredLayers)
                layer.isSelected = !layer.isSelected;
        }
        
        GUILayout.FlexibleSpace();
        
        int selectedCount = filteredLayers.Count(l => l.isSelected);
        EditorGUILayout.LabelField($"已选中: {selectedCount}/{filteredLayers.Count} 个图层", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawPsdImportButton()
    {
        EditorGUILayout.Space(5);
        
        var filteredLayers = currentPsd.layers
            .Where(l => (!psdOnlyVisible || l.isVisible) && (!psdSkipGroups || !l.isGroup) && l.isSelected)
            .ToList();
        
        EditorGUI.BeginDisabledGroup(filteredLayers.Count == 0);
        if (GUILayout.Button("🔽 导入选中图层到生成列表", GUILayout.Height(28)))
        {
            ImportPsdLayersToList();
        }
        EditorGUI.EndDisabledGroup();
    }
    
    #endregion
    
    #region PSD解析和导入
    
    private void LoadPsdFile(string filePath)
    {
        try
        {
            currentPsd = ParsePsdFile(filePath);
            
            if (currentPsd == null || currentPsd.layers.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "PSD文件解析失败或没有图层", "确定");
                currentPsd = null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"加载PSD失败: {e.Message}");
            EditorUtility.DisplayDialog("错误", $"加载PSD失败: {e.Message}", "确定");
            currentPsd = null;
        }
    }
    
    /// <summary>
    /// 解析PSD文件（完整版本，支持图层像素提取）
    /// </summary>
    private PsdDocumentInfo ParsePsdFile(string filePath)
    {
        var psd = new PsdDocumentInfo();
        psd.filePath = filePath;
        psd.fileName = Path.GetFileName(filePath);
        
        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        using (var reader = new BinaryReader(stream))
        {
            // 读取PSD签名 "8BPS"
            string signature = new string(reader.ReadChars(4));
            if (signature != "8BPS")
            {
                throw new System.Exception("不是有效的PSD文件");
            }
            
            // 版本号 (1 = PSD, 2 = PSB)
            ushort version = ReadUInt16BE(reader);
            if (version != 1 && version != 2)
            {
                throw new System.Exception($"不支持的PSD版本: {version}");
            }
            
            // 保留字节
            reader.ReadBytes(6);
            
            // 通道数
            ushort channels = ReadUInt16BE(reader);
            
            // 画布尺寸
            psd.canvasHeight = (int)ReadUInt32BE(reader);
            psd.canvasWidth = (int)ReadUInt32BE(reader);
            
            // 位深度
            ushort depth = ReadUInt16BE(reader);
            
            // 颜色模式
            ushort colorMode = ReadUInt16BE(reader);
            
            // 跳过颜色模式数据
            uint colorModeDataLength = ReadUInt32BE(reader);
            reader.ReadBytes((int)colorModeDataLength);
            
            // 跳过图像资源
            uint imageResourcesLength = ReadUInt32BE(reader);
            reader.ReadBytes((int)imageResourcesLength);
            
            // 图层和蒙版信息
            uint layerMaskInfoLength = ReadUInt32BE(reader);
            long layerMaskInfoEnd = stream.Position + layerMaskInfoLength;
            
            if (layerMaskInfoLength > 0)
            {
                // 图层信息
                uint layerInfoLength = ReadUInt32BE(reader);
                long layerInfoEnd = stream.Position + layerInfoLength;
                
                if (layerInfoLength > 0)
                {
                    // 图层数量
                    short layerCount = ReadInt16BE(reader);
                    int absLayerCount = System.Math.Abs(layerCount);
                    
                    var tempLayers = new List<PsdLayerInfo>();
                    
                    // 第一遍：读取图层记录
                    for (int i = 0; i < absLayerCount; i++)
                    {
                        var layer = new PsdLayerInfo();
                        layer.index = i;
                        
                        // 图层边界
                        layer.top = ReadInt32BE(reader);
                        layer.left = ReadInt32BE(reader);
                        layer.bottom = ReadInt32BE(reader);
                        layer.right = ReadInt32BE(reader);
                        
                        // 通道数
                        ushort layerChannels = ReadUInt16BE(reader);
                        
                        // 读取通道信息
                        layer.channels = new List<PsdChannelInfo>();
                        for (int c = 0; c < layerChannels; c++)
                        {
                            var channelInfo = new PsdChannelInfo();
                            channelInfo.channelId = ReadInt16BE(reader);
                            channelInfo.dataLength = ReadUInt32BE(reader);
                            layer.channels.Add(channelInfo);
                        }
                        
                        // 混合模式签名
                        string blendSig = new string(reader.ReadChars(4));
                        
                        // 混合模式
                        string blendMode = new string(reader.ReadChars(4));
                        
                        // 不透明度
                        byte opacity = reader.ReadByte();
                        
                        // 裁剪
                        byte clipping = reader.ReadByte();
                        
                        // 标志
                        byte flags = reader.ReadByte();
                        layer.isVisible = (flags & 0x02) == 0;
                        
                        // 填充
                        reader.ReadByte();
                        
                        // 额外数据长度
                        uint extraDataLength = ReadUInt32BE(reader);
                        long extraDataEnd = stream.Position + extraDataLength;
                        
                        // 图层蒙版数据
                        uint maskDataLength = ReadUInt32BE(reader);
                        reader.ReadBytes((int)maskDataLength);
                        
                        // 图层混合范围
                        uint blendingRangesLength = ReadUInt32BE(reader);
                        reader.ReadBytes((int)blendingRangesLength);
                        
                        // 图层名称（Pascal字符串）
                        byte nameLength = reader.ReadByte();
                        byte[] nameBytes = reader.ReadBytes(nameLength);
                        layer.name = System.Text.Encoding.UTF8.GetString(nameBytes);
                        
                        // 对齐到4字节边界
                        int padding = (4 - ((nameLength + 1) % 4)) % 4;
                        reader.ReadBytes(padding);
                        
                        // 检查图层组
                        while (stream.Position < extraDataEnd)
                        {
                            if (stream.Position + 8 > extraDataEnd) break;
                            
                            string sig = new string(reader.ReadChars(4));
                            if (sig != "8BIM" && sig != "8B64")
                            {
                                stream.Position = extraDataEnd;
                                break;
                            }
                            
                            string key = new string(reader.ReadChars(4));
                            uint dataLen = ReadUInt32BE(reader);
                            
                            if (key == "lsct" || key == "lsdk")
                            {
                                if (dataLen >= 4)
                                {
                                    uint sectionType = ReadUInt32BE(reader);
                                    layer.isGroup = sectionType == 1 || sectionType == 2;
                                    reader.ReadBytes((int)dataLen - 4);
                                }
                                else
                                {
                                    reader.ReadBytes((int)dataLen);
                                }
                            }
                            // Unicode图层名
                            else if (key == "luni")
                            {
                                uint strLen = ReadUInt32BE(reader);
                                byte[] unicodeBytes = reader.ReadBytes((int)strLen * 2);
                                layer.name = System.Text.Encoding.BigEndianUnicode.GetString(unicodeBytes).TrimEnd('\0');
                                if (dataLen > strLen * 2 + 4)
                                    reader.ReadBytes((int)(dataLen - strLen * 2 - 4));
                            }
                            else
                            {
                                reader.ReadBytes((int)dataLen);
                            }
                            
                            if (dataLen % 2 != 0 && stream.Position < extraDataEnd)
                            {
                                reader.ReadByte();
                            }
                        }
                        
                        stream.Position = extraDataEnd;
                        tempLayers.Add(layer);
                    }
                    
                    // 第二遍：读取图层像素数据位置
                    foreach (var layer in tempLayers)
                    {
                        layer.channelDataStart = stream.Position;
                        
                        // 跳过该图层的所有通道数据
                        foreach (var channel in layer.channels)
                        {
                            stream.Position += channel.dataLength;
                        }
                        
                        // 只添加有效图层
                        if (layer.Width > 0 && layer.Height > 0)
                        {
                            psd.layers.Add(layer);
                        }
                        else if (layer.isGroup)
                        {
                            psd.layers.Add(layer);
                        }
                    }
                }
            }
        }
        
        // 反转图层顺序
        psd.layers.Reverse();
        
        // 重新编号
        for (int i = 0; i < psd.layers.Count; i++)
        {
            psd.layers[i].index = i + 1;
        }
        
        return psd;
    }
    
    /// <summary>
    /// 从PSD文件提取图层像素数据并创建Texture2D
    /// </summary>
    private Texture2D ExtractLayerTexture(PsdLayerInfo layer)
    {
        if (layer.Width <= 0 || layer.Height <= 0 || layer.isGroup)
            return null;
        
        int width = layer.Width;
        int height = layer.Height;
        
        Debug.Log($"[PSD] 提取图层 '{layer.name}': 尺寸={width}x{height}, 通道数={layer.channels.Count}, 数据起始={layer.channelDataStart}");
        
        // 创建通道数据数组
        byte[] redChannel = new byte[width * height];
        byte[] greenChannel = new byte[width * height];
        byte[] blueChannel = new byte[width * height];
        byte[] alphaChannel = new byte[width * height];
        
        // 默认不透明
        for (int i = 0; i < alphaChannel.Length; i++)
            alphaChannel[i] = 255;
        
        bool hasRed = false, hasGreen = false, hasBlue = false, hasAlpha = false;
        
        using (var stream = new FileStream(currentPsd.filePath, FileMode.Open, FileAccess.Read))
        using (var reader = new BinaryReader(stream))
        {
            // 使用独立变量跟踪每个通道的起始位置
            long currentPosition = layer.channelDataStart;
            
            foreach (var channel in layer.channels)
            {
                // 定位到当前通道的起始位置
                stream.Position = currentPosition;
                
                byte[] targetChannel = null;
                string channelName = "";
                
                switch (channel.channelId)
                {
                    case 0: 
                        targetChannel = redChannel; 
                        channelName = "Red";
                        hasRed = true;
                        break;
                    case 1: 
                        targetChannel = greenChannel; 
                        channelName = "Green";
                        hasGreen = true;
                        break;
                    case 2: 
                        targetChannel = blueChannel; 
                        channelName = "Blue";
                        hasBlue = true;
                        break;
                    case -1: 
                        targetChannel = alphaChannel; 
                        channelName = "Alpha";
                        hasAlpha = true;
                        break;
                    default:
                        Debug.Log($"[PSD]   跳过通道 ID={channel.channelId}, 长度={channel.dataLength}");
                        currentPosition += channel.dataLength;
                        continue;
                }
                
                Debug.Log($"[PSD]   读取{channelName}通道: ID={channel.channelId}, 位置={currentPosition}, 长度={channel.dataLength}");
                
                if (channel.dataLength < 2)
                {
                    Debug.LogWarning($"[PSD]   通道数据太短: {channel.dataLength}");
                    currentPosition += channel.dataLength;
                    continue;
                }
                
                // 读取压缩类型
                ushort compression = ReadUInt16BE(reader);
                Debug.Log($"[PSD]   压缩类型: {compression} ({(compression == 0 ? "RAW" : compression == 1 ? "RLE" : "未知")})");
                
                if (compression == 0)
                {
                    // RAW - 未压缩
                    int bytesToRead = System.Math.Min(width * height, (int)channel.dataLength - 2);
                    byte[] rawData = reader.ReadBytes(bytesToRead);
                    System.Array.Copy(rawData, targetChannel, System.Math.Min(rawData.Length, targetChannel.Length));
                    
                    // 检查数据是否全为0
                    int nonZeroCount = 0;
                    for (int i = 0; i < System.Math.Min(100, rawData.Length); i++)
                    {
                        if (rawData[i] != 0) nonZeroCount++;
                    }
                    Debug.Log($"[PSD]   RAW数据: 读取{bytesToRead}字节, 前100字节非零数={nonZeroCount}");
                }
                else if (compression == 1)
                {
                    // RLE压缩 (PackBits)
                    // 先读取每行的压缩字节数
                    int[] rowByteCounts = new int[height];
                    long totalRowBytes = 0;
                    for (int row = 0; row < height; row++)
                    {
                        rowByteCounts[row] = ReadUInt16BE(reader);
                        totalRowBytes += rowByteCounts[row];
                    }
                    Debug.Log($"[PSD]   RLE: 行数={height}, 总压缩字节={totalRowBytes}");
                    
                    // 解压每行
                    int totalPixelsDecoded = 0;
                    for (int row = 0; row < height; row++)
                    {
                        int rowStart = row * width;
                        int col = 0;
                        
                        // 记录当前行数据的结束位置
                        long rowDataEnd = stream.Position + rowByteCounts[row];
                        
                        while (stream.Position < rowDataEnd && col < width)
                        {
                            sbyte len = (sbyte)reader.ReadByte();
                            
                            if (len >= 0)
                            {
                                // 字面量运行：复制接下来的 len+1 个字节
                                int count = len + 1;
                                for (int j = 0; j < count && col < width && stream.Position < rowDataEnd; j++)
                                {
                                    targetChannel[rowStart + col] = reader.ReadByte();
                                    col++;
                                    totalPixelsDecoded++;
                                }
                            }
                            else if (len > -128)
                            {
                                // 重复运行：重复下一个字节 1-len 次
                                int count = 1 - len;
                                byte value = reader.ReadByte();
                                for (int j = 0; j < count && col < width; j++)
                                {
                                    targetChannel[rowStart + col] = value;
                                    col++;
                                    totalPixelsDecoded++;
                                }
                            }
                            // len == -128 表示无操作 (NOP)
                        }
                        
                        // 强制对齐到行数据结束位置，确保下一行从正确位置开始
                        stream.Position = rowDataEnd;
                    }
                    
                    // 检查解压后的数据
                    int nonZeroCount = 0;
                    for (int i = 0; i < System.Math.Min(100, targetChannel.Length); i++)
                    {
                        if (targetChannel[i] != 0) nonZeroCount++;
                    }
                    Debug.Log($"[PSD]   RLE解压: 像素数={totalPixelsDecoded}, 期望={width * height}, 前100像素非零数={nonZeroCount}");
                }
                else
                {
                    Debug.LogWarning($"[PSD]   不支持的压缩格式: {compression}");
                }
                
                // 移动到下一个通道的位置
                currentPosition += channel.dataLength;
            }
        }
        
        Debug.Log($"[PSD] 通道状态: R={hasRed}, G={hasGreen}, B={hasBlue}, A={hasAlpha}");
        
        // 创建Texture2D
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[width * height];
        
        // PSD像素是从上到下存储的，Unity是从下到上，需要Y轴翻转
        for (int y = 0; y < height; y++)
        {
            int srcRow = y;
            int dstRow = height - 1 - y;
            
            for (int x = 0; x < width; x++)
            {
                int srcIdx = srcRow * width + x;
                int dstIdx = dstRow * width + x;
                
                pixels[dstIdx] = new Color32(
                    redChannel[srcIdx],
                    greenChannel[srcIdx],
                    blueChannel[srcIdx],
                    alphaChannel[srcIdx]
                );
            }
        }
        
        texture.SetPixels32(pixels);
        texture.Apply();
        
        // 检查最终像素
        int finalNonZero = 0;
        for (int i = 0; i < System.Math.Min(100, pixels.Length); i++)
        {
            if (pixels[i].r != 0 || pixels[i].g != 0 || pixels[i].b != 0)
                finalNonZero++;
        }
        Debug.Log($"[PSD] 最终像素: 前100像素非零数={finalNonZero}");
        
        return texture;
    }
    
    // 大端读取辅助方法
    private ushort ReadUInt16BE(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(2);
        System.Array.Reverse(bytes);
        return System.BitConverter.ToUInt16(bytes, 0);
    }
    
    private short ReadInt16BE(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(2);
        System.Array.Reverse(bytes);
        return System.BitConverter.ToInt16(bytes, 0);
    }
    
    private uint ReadUInt32BE(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        System.Array.Reverse(bytes);
        return System.BitConverter.ToUInt32(bytes, 0);
    }
    
    private int ReadInt32BE(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        System.Array.Reverse(bytes);
        return System.BitConverter.ToInt32(bytes, 0);
    }
    
    /// <summary>
    /// 将PSD坐标转换为Unity坐标
    /// </summary>
    private Vector2 ConvertPsdToUnityPosition(PsdLayerInfo layer)
    {
        float layerCenterX = layer.CenterX;
        float layerCenterY = layer.CenterY;
        
        float unityX, unityY;
        
        switch (psdOriginMode)
        {
            case PsdOriginMode.Center:
                unityX = (layerCenterX - currentPsd.canvasWidth / 2f) * pixelUnit;
                unityY = (currentPsd.canvasHeight / 2f - layerCenterY) * pixelUnit;
                break;
                
            case PsdOriginMode.TopLeft:
                unityX = layerCenterX * pixelUnit;
                unityY = -layerCenterY * pixelUnit;
                break;
                
            case PsdOriginMode.BottomLeft:
                unityX = layerCenterX * pixelUnit;
                unityY = (currentPsd.canvasHeight - layerCenterY) * pixelUnit;
                break;
                
            default:
                unityX = 0;
                unityY = 0;
                break;
        }
        
        return new Vector2(unityX, unityY);
    }
    
    /// <summary>
    /// 导入PSD图层到生成列表
    /// </summary>
    private void ImportPsdLayersToList()
    {
        // 确保输出目录存在
        if (!AssetDatabase.IsValidFolder(psdTextureOutputPath))
        {
            // 创建目录
            string[] folders = psdTextureOutputPath.Split('/');
            string currentPath = folders[0];
            for (int i = 1; i < folders.Length; i++)
            {
                string newPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(newPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath = newPath;
            }
        }
        
        var selectedLayers = currentPsd.layers
            .Where(l => (!psdOnlyVisible || l.isVisible) && (!psdSkipGroups || !l.isGroup) && l.isSelected && !l.isGroup)
            .ToList();
        
        int successCount = 0;
        
        EditorUtility.DisplayProgressBar("导入PSD图层", "正在处理...", 0);
        
        try
        {
            for (int i = 0; i < selectedLayers.Count; i++)
            {
                var layer = selectedLayers[i];
                EditorUtility.DisplayProgressBar("导入PSD图层", $"处理图层: {layer.name}", (float)i / selectedLayers.Count);
                
                // 尝试导出图层贴图
                Texture2D layerTexture = ExportPsdLayerTexture(layer);
                
                if (layerTexture != null)
                {
                    // 计算Unity坐标（用于3D模式，已乘以pixelUnit）
                    Vector2 unityPos = ConvertPsdToUnityPosition(layer);
                    
                    // 计算像素坐标（用于2D模式，直接使用像素值）
                    // 以画布中心为原点的像素坐标
                    Vector2 pixelPos = new Vector2(
                        layer.CenterX - currentPsd.canvasWidth / 2f,
                        currentPsd.canvasHeight / 2f - layer.CenterY
                    );
                    
                    // 创建ImageItem（带完整信息）
                    var item = new ImageItem(layer.name, layerTexture, unityPos, pixelPos, 
                                             layer.index, currentPsd.canvasWidth, currentPsd.canvasHeight);
                    
                    // 检查是否已存在
                    if (!imageItems.Any(x => x.name == item.name && x.hasPsdPosition))
                    {
                        imageItems.Add(item);
                        successCount++;
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("完成", $"成功导入 {successCount}/{selectedLayers.Count} 个图层", "确定");
    }
    
    /// <summary>
    /// 导出PSD图层贴图
    /// </summary>
    private Texture2D ExportPsdLayerTexture(PsdLayerInfo layer)
    {
        // 检查是否已有导出的贴图
        string textureName = $"{Path.GetFileNameWithoutExtension(currentPsd.fileName)}_{layer.name}";
        textureName = SanitizeFileName(textureName);
        string texturePath = $"{psdTextureOutputPath}/{textureName}.png";
        
        // 检查是否已存在
        Texture2D existingTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (existingTex != null)
        {
            layer.texture = existingTex;
            layer.texturePath = texturePath;
            return existingTex;
        }
        
        // 从PSD提取图层像素
        try
        {
            Texture2D extractedTex = ExtractLayerTexture(layer);
            
            if (extractedTex != null)
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(texturePath);
                if (!AssetDatabase.IsValidFolder(directory))
                {
                    string[] folders = directory.Split('/');
                    string currentPath = folders[0];
                    for (int i = 1; i < folders.Length; i++)
                    {
                        string newPath = currentPath + "/" + folders[i];
                        if (!AssetDatabase.IsValidFolder(newPath))
                        {
                            AssetDatabase.CreateFolder(currentPath, folders[i]);
                        }
                        currentPath = newPath;
                    }
                }
                
                // 保存为PNG
                byte[] pngData = extractedTex.EncodeToPNG();
                string absolutePath = Path.Combine(Application.dataPath.Replace("/Assets", ""), texturePath);
                File.WriteAllBytes(absolutePath, pngData);
                
                // 清理临时Texture
                UnityEngine.Object.DestroyImmediate(extractedTex);
                
                // 导入资源
                AssetDatabase.ImportAsset(texturePath);
                
                // 设置贴图导入设置
                TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.SaveAndReimport();
                }
                
                // 加载导入的贴图
                Texture2D importedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                layer.texture = importedTex;
                layer.texturePath = texturePath;
                
                Debug.Log($"成功导出图层: {layer.name} -> {texturePath}");
                return importedTex;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"提取图层 '{layer.name}' 失败: {e.Message}");
        }
        
        // 如果提取失败，尝试查找同名贴图
        string[] guids = AssetDatabase.FindAssets($"{layer.name} t:Texture2D");
        foreach (var guid in guids)
        {
            string foundPath = AssetDatabase.GUIDToAssetPath(guid);
            if (foundPath.Contains(layer.name))
            {
                Texture2D foundTex = AssetDatabase.LoadAssetAtPath<Texture2D>(foundPath);
                if (foundTex != null)
                {
                    layer.texture = foundTex;
                    layer.texturePath = foundPath;
                    return foundTex;
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 清理文件名中的非法字符
    /// </summary>
    private string SanitizeFileName(string fileName)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        foreach (char c in invalidChars)
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName;
    }
    
    private void HandlePsdDragAndDrop()
    {
        Event evt = Event.current;
        
        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            if (psdDropRect.Contains(evt.mousePosition))
            {
                // 检查是否有PSD文件
                bool hasPsd = false;
                string psdPath = null;
                
                foreach (var path in DragAndDrop.paths)
                {
                    if (path.EndsWith(".psd", System.StringComparison.OrdinalIgnoreCase))
                    {
                        hasPsd = true;
                        psdPath = path;
                        break;
                    }
                }
                
                if (hasPsd)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        
                        // 如果是相对路径，转换为绝对路径
                        if (!Path.IsPathRooted(psdPath))
                        {
                            psdPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), psdPath);
                        }
                        
                        LoadPsdFile(psdPath);
                    }
                    
                    evt.Use();
                }
            }
        }
    }
    
    #endregion
    
    #region 基础设置
    
    private void DrawBasicSettings()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("═══ 基础设置 ═══", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME);
        EditorGUILayout.EndHorizontal();
        
        // 目标对象
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("目标对象", GUILayout.Width(60));
        string targetName = targetObject != null ? targetObject.name : "未选中";
        EditorGUILayout.LabelField($"当前选中: {targetName}", EditorStyles.helpBox);
        if (GUILayout.Button("刷新", GUILayout.Width(50)))
        {
            RefreshTargetObject();
        }
        EditorGUILayout.EndHorizontal();
        
        // 父对象名称
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("父对象名", GUILayout.Width(60));
        parentName = EditorGUILayout.TextField(parentName);
        EditorGUILayout.EndHorizontal();
        
        // 生成模式
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("生成模式", GUILayout.Width(60));
        
        if (GUILayout.Toggle(generateMode == GenerateMode.Quad, "Quad", EditorStyles.miniButtonLeft))
            generateMode = GenerateMode.Quad;
        if (GUILayout.Toggle(generateMode == GenerateMode.SpriteRenderer, "Sprite", EditorStyles.miniButtonMid))
            generateMode = GenerateMode.SpriteRenderer;
        if (GUILayout.Toggle(generateMode == GenerateMode.Image, "Image", EditorStyles.miniButtonMid))
            generateMode = GenerateMode.Image;
        if (GUILayout.Toggle(generateMode == GenerateMode.ParticleSystem, "粒子系统", EditorStyles.miniButtonRight))
            generateMode = GenerateMode.ParticleSystem;
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        // Quad和粒子系统模式显示Shader设置
        if (generateMode != GenerateMode.SpriteRenderer && generateMode != GenerateMode.Image)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Shader", GUILayout.Width(60));
            
            string shaderName = selectedShader != null ? selectedShader.name : "None";
            Rect shaderRect = EditorGUILayout.GetControlRect();
            
            if (EditorGUI.DropdownButton(shaderRect, new GUIContent(shaderName), FocusType.Keyboard))
            {
                ShowShaderSelectionMenu(shaderRect);
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 贴图属性名
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("贴图属性", GUILayout.Width(60));
            texturePropertyName = EditorGUILayout.TextField(texturePropertyName);
            EditorGUILayout.EndHorizontal();
            
            // 材质目录（直接输入路径）
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("材质目录", GUILayout.Width(60));
            materialFolder = EditorGUILayout.TextField(materialFolder);
            EditorGUILayout.EndHorizontal();
            
            // 尺寸基准（仅3D模式显示）
            if (transformType == SpriteTransformType.Transform3D)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("尺寸基准", GUILayout.Width(60));
                
                if (GUILayout.Toggle(sizeMode == SizeMode.PixelRatio, "像素比例", EditorStyles.miniButtonLeft))
                    sizeMode = SizeMode.PixelRatio;
                if (GUILayout.Toggle(sizeMode == SizeMode.WidthOne, "宽度为1", EditorStyles.miniButtonMid))
                    sizeMode = SizeMode.WidthOne;
                if (GUILayout.Toggle(sizeMode == SizeMode.HeightOne, "高度为1", EditorStyles.miniButtonRight))
                    sizeMode = SizeMode.HeightOne;
                
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                
                // 像素单位（仅像素比例模式显示）
                if (sizeMode == SizeMode.PixelRatio)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("像素单位", GUILayout.Width(60));
                    pixelUnit = EditorGUILayout.FloatField(pixelUnit, GUILayout.Width(80));
                    EditorGUILayout.LabelField("(像素×单位系数)", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
            }
            
            // 粒子系统模式特有设置
            if (generateMode == GenerateMode.ParticleSystem)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("粒子预设", GUILayout.Width(60));
                particlePreset = (Preset)EditorGUILayout.ObjectField(particlePreset, typeof(Preset), false);
                EditorGUILayout.EndHorizontal();
                
                // 预设验证提示
                if (particlePreset != null && !particlePreset.CanBeAppliedTo(new ParticleSystem()))
                {
                    EditorGUILayout.HelpBox("所选预设不是ParticleSystem预设", MessageType.Warning);
                }
            }
        }
        
        // Sprite模式特有设置
        if (generateMode == GenerateMode.SpriteRenderer)
        {
            // 排序图层
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("排序图层", GUILayout.Width(60));
            
            // 获取所有排序图层
            string[] sortingLayers = GetSortingLayerNames();
            int currentIndex = System.Array.IndexOf(sortingLayers, sortingLayerName);
            if (currentIndex < 0) currentIndex = 0;
            
            int newIndex = EditorGUILayout.Popup(currentIndex, sortingLayers);
            if (newIndex >= 0 && newIndex < sortingLayers.Length)
                sortingLayerName = sortingLayers[newIndex];
            
            EditorGUILayout.EndHorizontal();
            
            // 层级顺序
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("层级顺序", GUILayout.Width(60));
            sortingOrder = EditorGUILayout.IntField(sortingOrder);
            EditorGUILayout.EndHorizontal();
        }
        
        // Image模式特有设置
        if (generateMode == GenerateMode.Image)
        {
            // Canvas检查提示
            if (targetObject != null && targetObject.GetComponentInParent<Canvas>() == null)
            {
                EditorGUILayout.HelpBox("目标对象不在Canvas层级下，Image组件无法正常渲染", MessageType.Warning);
            }
            
            // 射线检测
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("射线检测", GUILayout.Width(60));
            if (GUILayout.Toggle(!imageRaycastTarget, "关闭", EditorStyles.miniButtonLeft))
                imageRaycastTarget = false;
            if (GUILayout.Toggle(imageRaycastTarget, "开启", EditorStyles.miniButtonRight))
                imageRaycastTarget = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        
        // ========== 变换类型（固定在最下面） ==========
        // Image模式强制使用2D矩形
        if (generateMode == GenerateMode.Image)
        {
            transformType = SpriteTransformType.RectTransform2D;
        }
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("变换类型", GUILayout.Width(60));
        
        EditorGUI.BeginDisabledGroup(generateMode == GenerateMode.Image);
        if (GUILayout.Toggle(transformType == SpriteTransformType.Transform3D, "3D标准", EditorStyles.miniButtonLeft))
            transformType = SpriteTransformType.Transform3D;
        if (GUILayout.Toggle(transformType == SpriteTransformType.RectTransform2D, "2D矩形", EditorStyles.miniButtonRight))
            transformType = SpriteTransformType.RectTransform2D;
        EditorGUI.EndDisabledGroup();
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        // RectTransform模式显示缩放系数
        if (transformType == SpriteTransformType.RectTransform2D)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("缩放系数", GUILayout.Width(60));
            rectScale = EditorGUILayout.FloatField(rectScale, GUILayout.Width(80));
            EditorGUILayout.LabelField("(XYZ统一缩放)", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        
        // Z轴间距：所有模式都显示
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Z轴间距", GUILayout.Width(60));
            zSpacing = EditorGUILayout.FloatField(zSpacing, GUILayout.Width(80));
            EditorGUILayout.LabelField("(图层间隔)", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }
    
    /// <summary>
    /// 显示Shader选择下拉菜单（与Unity材质Inspector相同样式）
    /// </summary>
    private void ShowShaderSelectionMenu(Rect buttonRect)
    {
        // 使用GenericMenu构建分层菜单
        GenericMenu menu = new GenericMenu();
        
        // 获取所有Shader
        var shaderInfos = ShaderUtil.GetAllShaderInfo();
        
        foreach (var info in shaderInfos)
        {
            // 跳过隐藏的Shader
            if (info.name.StartsWith("Hidden/")) continue;
            
            string menuPath = info.name;
            string shaderNameCopy = info.name; // 闭包需要拷贝
            
            bool isSelected = selectedShader != null && selectedShader.name == info.name;
            
            menu.AddItem(new GUIContent(menuPath), isSelected, () =>
            {
                selectedShader = Shader.Find(shaderNameCopy);
                Repaint();
            });
        }
        
        menu.DropDown(buttonRect);
    }
    
    #endregion
    
    #region 生成列表
    
    private void DrawGenerateList()
    {
        EditorGUILayout.LabelField("═══ 生成列表 ═══", EditorStyles.boldLabel);
        
        // 操作按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("从选中获取", GUILayout.Width(80)))
        {
            AddFromSelection();
        }
        if (GUILayout.Button("清空列表", GUILayout.Width(80)))
        {
            imageItems.Clear();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("拖拽图片到此区域添加", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        // 列表区域
        Rect listRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinHeight(150));
        
        if (imageItems.Count == 0)
        {
            EditorGUILayout.LabelField("暂无图片，请拖拽图片或点击\"从选中获取\"", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(130));
        }
        else
        {
            // 表头
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("☑", GUILayout.Width(20));
            EditorGUILayout.LabelField("名称", GUILayout.MinWidth(80));
            EditorGUILayout.LabelField("源图片", GUILayout.Width(100));
            EditorGUILayout.LabelField("尺寸", GUILayout.Width(65));
            EditorGUILayout.LabelField("位置", GUILayout.Width(80));
            EditorGUILayout.LabelField("操作", GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            
            // 列表内容
            listScrollPos = EditorGUILayout.BeginScrollView(listScrollPos, GUILayout.MaxHeight(200));
            
            int removeIndex = -1;
            int moveUpIndex = -1;
            int moveDownIndex = -1;
            
            for (int i = 0; i < imageItems.Count; i++)
            {
                var item = imageItems[i];
                EditorGUILayout.BeginHorizontal();
                
                // 勾选
                item.isSelected = EditorGUILayout.Toggle(item.isSelected, GUILayout.Width(20));
                
                // 名称（可编辑）+ PSD标记
                string nameDisplay = item.hasPsdPosition ? "📍" : "";
                EditorGUILayout.LabelField(nameDisplay, GUILayout.Width(15));
                item.name = EditorGUILayout.TextField(item.name, GUILayout.MinWidth(65));
                
                // 源图片
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(item.texture, typeof(Texture2D), false, GUILayout.Width(100));
                EditorGUI.EndDisabledGroup();
                
                // 尺寸
                EditorGUILayout.LabelField($"{item.width}x{item.height}", GUILayout.Width(65));
                
                // 位置
                if (item.hasPsdPosition)
                {
                    EditorGUILayout.LabelField($"({item.psdPosition.x:F1},{item.psdPosition.y:F1})", GUILayout.Width(80));
                }
                else
                {
                    EditorGUILayout.LabelField("-", GUILayout.Width(80));
                }
                
                // 操作按钮
                if (GUILayout.Button("↑", EditorStyles.miniButtonLeft, GUILayout.Width(20)))
                    moveUpIndex = i;
                if (GUILayout.Button("↓", EditorStyles.miniButtonMid, GUILayout.Width(20)))
                    moveDownIndex = i;
                if (GUILayout.Button("×", EditorStyles.miniButtonRight, GUILayout.Width(20)))
                    removeIndex = i;
                
                EditorGUILayout.EndHorizontal();
            }
            
            // 处理移动和删除
            if (moveUpIndex > 0)
            {
                var temp = imageItems[moveUpIndex];
                imageItems[moveUpIndex] = imageItems[moveUpIndex - 1];
                imageItems[moveUpIndex - 1] = temp;
            }
            if (moveDownIndex >= 0 && moveDownIndex < imageItems.Count - 1)
            {
                var temp = imageItems[moveDownIndex];
                imageItems[moveDownIndex] = imageItems[moveDownIndex + 1];
                imageItems[moveDownIndex + 1] = temp;
            }
            if (removeIndex >= 0)
            {
                imageItems.RemoveAt(removeIndex);
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        EditorGUILayout.EndVertical();
        
        // 记录拖拽区域
        if (Event.current.type == EventType.Repaint)
        {
            dragDropRect = listRect;
        }
        
        // 全选/反选
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全选", EditorStyles.miniButtonLeft, GUILayout.Width(50)))
        {
            foreach (var item in imageItems)
                item.isSelected = true;
        }
        if (GUILayout.Button("反选", EditorStyles.miniButtonRight, GUILayout.Width(50)))
        {
            foreach (var item in imageItems)
                item.isSelected = !item.isSelected;
        }
        
        GUILayout.FlexibleSpace();
        
        int selectedCount = imageItems.Count(x => x.isSelected);
        EditorGUILayout.LabelField($"将生成: {selectedCount}/{imageItems.Count}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }
    
    private Rect dragDropRect;
    
    #endregion
    
    #region 输出预览
    
    private void DrawOutputPreview()
    {
        EditorGUILayout.LabelField("═══ 输出预览 ═══", EditorStyles.boldLabel);
        
        previewScrollPos = EditorGUILayout.BeginScrollView(previewScrollPos, EditorStyles.helpBox, GUILayout.Height(120));
        
        // 目标对象
        string targetName = targetObject != null ? targetObject.name : "(未选中)";
        EditorGUILayout.LabelField($"🎮 {targetName}", EditorStyles.boldLabel);
        
        // 父对象
        EditorGUILayout.LabelField($"    └── 📦 {parentName}");
        
        // 子对象预览
        var selectedItems = imageItems.Where(x => x.isSelected).ToList();
        
        if (generateMode == GenerateMode.Quad)
        {
            // Quad模式预览
            for (int i = 0; i < selectedItems.Count && i < 5; i++)
            {
                var item = selectedItems[i];
                string prefix = i == selectedItems.Count - 1 || i == 4 ? "        └──" : "        ├──";
                string posInfo = item.hasPsdPosition ? $"Pos:({item.psdPosition.x:F1},{item.psdPosition.y:F1})" : "Pos:(0,0)";
                
                if (transformType == SpriteTransformType.Transform3D)
                {
                    Vector3 scale = CalculateScale(item.width, item.height);
                    EditorGUILayout.LabelField($"{prefix} 🔲 {item.name} {posInfo} Scale:({scale.x:F2},{scale.y:F2},1)");
                }
                else
                {
                    EditorGUILayout.LabelField($"{prefix} 🔲 {item.name} {posInfo} Size:({item.width},{item.height})");
                }
            }
        }
        else if (generateMode == GenerateMode.SpriteRenderer)
        {
            // Sprite模式预览
            for (int i = 0; i < selectedItems.Count && i < 5; i++)
            {
                var item = selectedItems[i];
                string prefix = i == selectedItems.Count - 1 || i == 4 ? "        └──" : "        ├──";
                string posInfo = item.hasPsdPosition ? $"Pos:({item.psdPosition.x:F1},{item.psdPosition.y:F1})" : "Pos:(0,0)";
                
                if (transformType == SpriteTransformType.Transform3D)
                {
                    Vector3 scale = CalculateScale(item.width, item.height);
                    EditorGUILayout.LabelField($"{prefix} 🖼 {item.name} {posInfo} Scale:({scale.x:F2},{scale.y:F2},1)");
                }
                else
                {
                    EditorGUILayout.LabelField($"{prefix} 🖼 {item.name} {posInfo} Size:({item.width},{item.height})");
                }
            }
        }
        else
        {
            // 粒子系统模式预览
            string presetName = particlePreset != null ? particlePreset.name : "(无预设)";
            for (int i = 0; i < selectedItems.Count && i < 5; i++)
            {
                var item = selectedItems[i];
                string prefix = i == selectedItems.Count - 1 || i == 4 ? "        └──" : "        ├──";
                string posInfo = item.hasPsdPosition ? $"Pos:({item.psdPosition.x:F1},{item.psdPosition.y:F1})" : "Pos:(0,0)";
                
                if (transformType == SpriteTransformType.Transform3D)
                {
                    Vector3 scale = CalculateScale(item.width, item.height);
                    EditorGUILayout.LabelField($"{prefix} ✨ {item.name} {posInfo} Scale:({scale.x:F2},{scale.y:F2},1)");
                }
                else
                {
                    EditorGUILayout.LabelField($"{prefix} ✨ {item.name} {posInfo} Size:({item.width},{item.height}) [预设:{presetName}]");
                }
            }
        }
        
        if (selectedItems.Count > 5)
        {
            EditorGUILayout.LabelField($"        ... 还有 {selectedItems.Count - 5} 项");
        }
        
        EditorGUILayout.EndScrollView();
    }
    
    #endregion
    
    #region 生成按钮
    
    private void DrawGenerateButton()
    {
        EditorGUI.BeginDisabledGroup(!CanGenerate());
        
        string buttonText;
        switch (generateMode)
        {
            case GenerateMode.Quad:
                buttonText = "生成Quad";
                break;
            case GenerateMode.SpriteRenderer:
                buttonText = "生成Sprite";
                break;
            case GenerateMode.Image:
                buttonText = "生成Image";
                break;
            default:
                buttonText = "生成粒子";
                break;
        }
        
        if (GUILayout.Button(buttonText, GUILayout.Height(35)))
        {
            switch (generateMode)
            {
                case GenerateMode.Quad:
                    GenerateQuads();
                    break;
                case GenerateMode.SpriteRenderer:
                    GenerateSpriteRenderers();
                    break;
                case GenerateMode.Image:
                    GenerateImages();
                    break;
                case GenerateMode.ParticleSystem:
                    GenerateParticleSystems();
                    break;
            }
        }
        
        EditorGUI.EndDisabledGroup();
        
        // 提示信息
        if (!CanGenerate())
        {
            string hint = GetGenerateHint();
            EditorGUILayout.HelpBox(hint, MessageType.Warning);
        }
    }
    
    private bool CanGenerate()
    {
        if (targetObject == null) return false;
        if (imageItems.Count(x => x.isSelected) == 0) return false;
        
        // Sprite和Image模式不需要Shader和材质目录
        if (generateMode == GenerateMode.SpriteRenderer || generateMode == GenerateMode.Image)
            return true;
        
        if (selectedShader == null) return false;
        if (string.IsNullOrEmpty(materialFolder)) return false;
        return true;
    }
    
    private string GetGenerateHint()
    {
        if (targetObject == null) return "请在Hierarchy中选中目标对象";
        if (imageItems.Count(x => x.isSelected) == 0) return "请添加并勾选要生成的图片";
        
        // Sprite和Image模式不需要这些检查
        if (generateMode != GenerateMode.SpriteRenderer && generateMode != GenerateMode.Image)
        {
            if (selectedShader == null) return "请选择Shader";
            if (string.IsNullOrEmpty(materialFolder)) return "请输入材质保存目录";
        }
        return "";
    }
    
    #endregion
    
    #region 核心功能
    
    private void RefreshTargetObject()
    {
        targetObject = Selection.activeGameObject;
    }
    
    private void AddFromSelection()
    {
        var textures = Selection.objects.OfType<Texture2D>().ToList();
        
        foreach (var tex in textures)
        {
            // 检查是否已存在
            if (imageItems.Any(x => x.texture == tex))
                continue;
            
            imageItems.Add(new ImageItem(tex));
        }
        
        if (textures.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请在Project面板中选中图片文件", "确定");
        }
    }
    
    private void HandleDragAndDrop()
    {
        Event evt = Event.current;
        
        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            if (dragDropRect.Contains(evt.mousePosition) || 
                (evt.type == EventType.DragUpdated && position.Contains(GUIUtility.GUIToScreenPoint(evt.mousePosition)) == false))
            {
                // 检查是否有图片
                bool hasTexture = DragAndDrop.objectReferences.Any(x => x is Texture2D);
                
                if (hasTexture)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        
                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            if (obj is Texture2D tex)
                            {
                                if (!imageItems.Any(x => x.texture == tex))
                                {
                                    imageItems.Add(new ImageItem(tex));
                                }
                            }
                        }
                    }
                    
                    evt.Use();
                }
            }
        }
    }
    
    private Vector3 CalculateScale(int width, int height)
    {
        float scaleX = 1f;
        float scaleY = 1f;
        
        switch (sizeMode)
        {
            case SizeMode.PixelRatio:
                scaleX = width * pixelUnit;
                scaleY = height * pixelUnit;
                break;
                
            case SizeMode.WidthOne:
                scaleX = 1f;
                scaleY = (float)height / width;
                break;
                
            case SizeMode.HeightOne:
                scaleX = (float)width / height;
                scaleY = 1f;
                break;
        }
        
        return new Vector3(scaleX, scaleY, 1f);
    }
    
    private void GenerateQuads()
    {
        if (!CanGenerate()) return;
        
        // 确保材质目录存在
        if (!AssetDatabase.IsValidFolder(materialFolder))
        {
            EditorUtility.DisplayDialog("错误", $"材质目录不存在: {materialFolder}", "确定");
            return;
        }
        
        // 创建父对象
        GameObject parent = new GameObject(parentName);
        parent.transform.SetParent(targetObject.transform);
        parent.transform.localPosition = Vector3.zero;
        parent.transform.localRotation = Quaternion.identity;
        
        // 根据Transform类型设置根对象Scale
        if (transformType == SpriteTransformType.Transform3D)
        {
            // 3D标准：根的Scale.xy=100
            parent.transform.localScale = new Vector3(100, 100, 1);
        }
        else
        {
            // 2D矩形：根的Scale.xyz=1
            parent.transform.localScale = Vector3.one;
        }
        
        // 2D矩形模式下，父对象也添加RectTransform
        if (transformType == SpriteTransformType.RectTransform2D)
        {
            RectTransform parentRect = parent.AddComponent<RectTransform>();
            parentRect.anchorMin = new Vector2(0.5f, 0.5f);
            parentRect.anchorMax = new Vector2(0.5f, 0.5f);
            parentRect.pivot = new Vector2(0.5f, 0.5f);
            parentRect.anchoredPosition = Vector2.zero;
            parentRect.sizeDelta = Vector2.zero;
        }
        
        // 记录Undo
        Undo.RegisterCreatedObjectUndo(parent, "Create Quad Group");
        
        var selectedItems = imageItems.Where(x => x.isSelected).ToList();
        int successCount = 0;
        
        for (int i = 0; i < selectedItems.Count; i++)
        {
            var item = selectedItems[i];
            try
            {
                // 创建材质
                Material mat = new Material(selectedShader);
                mat.SetTexture(texturePropertyName, item.texture);
                
                string matPath = Path.Combine(materialFolder, item.name + ".mat").Replace("\\", "/");
                
                // 检查材质是否已存在
                Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (existingMat != null)
                {
                    // 更新现有材质
                    existingMat.shader = selectedShader;
                    existingMat.SetTexture(texturePropertyName, item.texture);
                    mat = existingMat;
                    EditorUtility.SetDirty(mat);
                }
                else
                {
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                
                // 创建Quad
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = item.name;
                quad.transform.SetParent(parent.transform);
                
                // 根据Transform类型设置
                if (transformType == SpriteTransformType.Transform3D)
                {
                    // 3D标准模式：使用已乘以pixelUnit的坐标
                    float zPos = i * zSpacing;
                    if (item.hasPsdPosition)
                    {
                        quad.transform.localPosition = new Vector3(item.psdPosition.x, item.psdPosition.y, zPos);
                    }
                    else
                    {
                        quad.transform.localPosition = new Vector3(0, 0, zPos);
                    }
                    quad.transform.localRotation = Quaternion.identity;
                    // Quad 3D标准: Scale.x=图片x*0.01, Scale.y=图片y*0.01, Scale.z=1
                    quad.transform.localScale = new Vector3(item.width * 0.01f, item.height * 0.01f, 1);
                }
                else
                {
                    // 2D矩形模式：添加RectTransform，使用像素坐标
                    RectTransform rectTransform = quad.AddComponent<RectTransform>();
                    rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    
                    // 使用像素坐标设置位置，Z轴默认为0
                    if (item.hasPsdPosition)
                    {
                        // 根据坐标原点模式计算位置
                        float posX, posY;
                        switch (psdOriginMode)
                        {
                            case PsdOriginMode.Center:
                                // 画布中心为原点，直接使用像素坐标
                                posX = item.psdPixelPosition.x;
                                posY = item.psdPixelPosition.y;
                                break;
                            case PsdOriginMode.TopLeft:
                                // 左上角为原点，需要转换
                                posX = item.psdPixelPosition.x + item.psdCanvasWidth / 2f;
                                posY = item.psdPixelPosition.y + item.psdCanvasHeight / 2f;
                                break;
                            case PsdOriginMode.BottomLeft:
                                // 左下角为原点
                                posX = item.psdPixelPosition.x + item.psdCanvasWidth / 2f;
                                posY = item.psdPixelPosition.y - item.psdCanvasHeight / 2f;
                                break;
                            default:
                                posX = 0;
                                posY = 0;
                                break;
                        }
                        rectTransform.anchoredPosition = new Vector2(posX, posY);
                    }
                    else
                    {
                        rectTransform.anchoredPosition = Vector2.zero;
                    }
                    
                    // 2D矩形模式：Z轴按图层间距计算
                    float zPos = i * zSpacing;
                    rectTransform.localPosition = new Vector3(
                        rectTransform.localPosition.x, 
                        rectTransform.localPosition.y, 
                        zPos
                    );
                    
                    // Quad 2D矩形: Width=1, Height=1
                    rectTransform.sizeDelta = new Vector2(1, 1);
                    
                    // Scale.x=图片x, Scale.y=图片y, Scale.z=1
                    rectTransform.localScale = new Vector3(item.width, item.height, 1);
                }
                
                quad.transform.localRotation = Quaternion.identity;
                
                // 移除Collider
                var collider = quad.GetComponent<Collider>();
                if (collider != null)
                    DestroyImmediate(collider);
                
                // 设置材质
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = mat;
                
                successCount++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"生成 {item.name} 失败: {e.Message}");
            }
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // 选中父对象
        Selection.activeGameObject = parent;
        
        EditorUtility.DisplayDialog("完成", $"成功生成 {successCount}/{selectedItems.Count} 个Quad", "确定");
    }
    
    private void GenerateParticleSystems()
    {
        if (!CanGenerate()) return;
        
        // 确保材质目录存在
        if (!AssetDatabase.IsValidFolder(materialFolder))
        {
            EditorUtility.DisplayDialog("错误", $"材质目录不存在: {materialFolder}", "确定");
            return;
        }
        
        // 创建父对象
        GameObject parent = new GameObject(parentName);
        parent.transform.SetParent(targetObject.transform);
        parent.transform.localPosition = Vector3.zero;
        parent.transform.localRotation = Quaternion.identity;
        
        // 根据Transform类型设置根对象Scale
        if (transformType == SpriteTransformType.Transform3D)
        {
            // 3D标准：根的Scale.xy=100
            parent.transform.localScale = new Vector3(100, 100, 1);
        }
        else
        {
            // 2D矩形：根的Scale.xyz=1
            parent.transform.localScale = Vector3.one;
        }
        
        // 2D矩形模式下，父对象也添加RectTransform
        if (transformType == SpriteTransformType.RectTransform2D)
        {
            RectTransform parentRect = parent.AddComponent<RectTransform>();
            parentRect.anchorMin = new Vector2(0.5f, 0.5f);
            parentRect.anchorMax = new Vector2(0.5f, 0.5f);
            parentRect.pivot = new Vector2(0.5f, 0.5f);
            parentRect.anchoredPosition = Vector2.zero;
            parentRect.sizeDelta = Vector2.zero;
        }
        
        // 记录Undo
        Undo.RegisterCreatedObjectUndo(parent, "Create ParticleSystem Group");
        
        var selectedItems = imageItems.Where(x => x.isSelected).ToList();
        int successCount = 0;
        
        for (int i = 0; i < selectedItems.Count; i++)
        {
            var item = selectedItems[i];
            try
            {
                // 创建材质
                Material mat = new Material(selectedShader);
                mat.SetTexture(texturePropertyName, item.texture);
                
                string matPath = Path.Combine(materialFolder, item.name + ".mat").Replace("\\", "/");
                
                // 检查材质是否已存在
                Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (existingMat != null)
                {
                    existingMat.shader = selectedShader;
                    existingMat.SetTexture(texturePropertyName, item.texture);
                    mat = existingMat;
                    EditorUtility.SetDirty(mat);
                }
                else
                {
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                
                // 创建粒子系统对象
                GameObject psObj = new GameObject(item.name);
                psObj.transform.SetParent(parent.transform);
                
                // 根据Transform类型设置
                if (transformType == SpriteTransformType.Transform3D)
                {
                    // 3D标准模式：使用已乘以pixelUnit的坐标
                    float zPos = i * zSpacing;
                    if (item.hasPsdPosition)
                    {
                        psObj.transform.localPosition = new Vector3(item.psdPosition.x, item.psdPosition.y, zPos);
                    }
                    else
                    {
                        psObj.transform.localPosition = new Vector3(0, 0, zPos);
                    }
                    psObj.transform.localRotation = Quaternion.identity;
                    // 粒子系统 3D标准: Scale=(1,1,1)
                    psObj.transform.localScale = Vector3.one;
                }
                else
                {
                    // 2D矩形模式：添加RectTransform，使用像素坐标
                    RectTransform rectTransform = psObj.AddComponent<RectTransform>();
                    rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    
                    // 使用像素坐标设置位置，Z轴默认为0
                    if (item.hasPsdPosition)
                    {
                        // 根据坐标原点模式计算位置
                        float posX, posY;
                        switch (psdOriginMode)
                        {
                            case PsdOriginMode.Center:
                                // 画布中心为原点，直接使用像素坐标
                                posX = item.psdPixelPosition.x;
                                posY = item.psdPixelPosition.y;
                                break;
                            case PsdOriginMode.TopLeft:
                                // 左上角为原点，需要转换
                                posX = item.psdPixelPosition.x + item.psdCanvasWidth / 2f;
                                posY = item.psdPixelPosition.y + item.psdCanvasHeight / 2f;
                                break;
                            case PsdOriginMode.BottomLeft:
                                // 左下角为原点
                                posX = item.psdPixelPosition.x + item.psdCanvasWidth / 2f;
                                posY = item.psdPixelPosition.y - item.psdCanvasHeight / 2f;
                                break;
                            default:
                                posX = 0;
                                posY = 0;
                                break;
                        }
                        rectTransform.anchoredPosition = new Vector2(posX, posY);
                    }
                    else
                    {
                        rectTransform.anchoredPosition = Vector2.zero;
                    }
                    
                    // 2D矩形模式：Z轴按图层间距计算
                    float zPos = i * zSpacing;
                    rectTransform.localPosition = new Vector3(
                        rectTransform.localPosition.x, 
                        rectTransform.localPosition.y, 
                        zPos
                    );
                    
                    // 粒子系统 2D矩形: Width=图片x, Height=图片y
                    rectTransform.sizeDelta = new Vector2(item.width, item.height);
                    
                    // Scale=(1,1,1)
                    rectTransform.localScale = Vector3.one;
                }
                
                psObj.transform.localRotation = Quaternion.identity;
                
                // 添加粒子系统组件
                ParticleSystem ps = psObj.AddComponent<ParticleSystem>();
                
                // 应用预设（如果有）
                if (particlePreset != null && particlePreset.CanBeAppliedTo(ps))
                {
                    particlePreset.ApplyTo(ps);
                }
                
                // 设置粒子系统的3D Start Size
                var mainModule = ps.main;
                mainModule.startSize3D = true;
                if (transformType == SpriteTransformType.Transform3D)
                {
                    // 3D标准: x=图片x*0.01, y=图片y*0.01, z=1
                    mainModule.startSizeX = item.width * 0.01f;
                    mainModule.startSizeY = item.height * 0.01f;
                    mainModule.startSizeZ = 1f;
                }
                else
                {
                    // 2D矩形: x=图片x, y=图片y, z=1
                    mainModule.startSizeX = item.width;
                    mainModule.startSizeY = item.height;
                    mainModule.startSizeZ = 1f;
                }
                
                // 设置材质到ParticleSystemRenderer
                var psRenderer = psObj.GetComponent<ParticleSystemRenderer>();
                if (psRenderer != null)
                {
                    psRenderer.sharedMaterial = mat;
                }
                
                successCount++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"生成 {item.name} 失败: {e.Message}");
            }
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // 选中父对象
        Selection.activeGameObject = parent;
        
        EditorUtility.DisplayDialog("完成", $"成功生成 {successCount}/{selectedItems.Count} 个粒子系统", "确定");
    }
    
    private void GenerateSpriteRenderers()
    {
        if (!CanGenerate()) return;
        
        // 创建父对象
        GameObject parent = new GameObject(parentName);
        parent.transform.SetParent(targetObject.transform);
        parent.transform.localPosition = Vector3.zero;
        parent.transform.localRotation = Quaternion.identity;
        
        // 根据Transform类型设置根对象Scale
        if (transformType == SpriteTransformType.Transform3D)
        {
            // 3D标准：根的Scale.xy=100
            parent.transform.localScale = new Vector3(100, 100, 1);
        }
        else
        {
            // 2D矩形：根的Scale.xyz=1
            parent.transform.localScale = Vector3.one;
        }
        
        // 2D矩形模式下，父对象也添加RectTransform
        if (transformType == SpriteTransformType.RectTransform2D)
        {
            RectTransform parentRect = parent.AddComponent<RectTransform>();
            parentRect.anchorMin = new Vector2(0.5f, 0.5f);
            parentRect.anchorMax = new Vector2(0.5f, 0.5f);
            parentRect.pivot = new Vector2(0.5f, 0.5f);
            parentRect.anchoredPosition = Vector2.zero;
            parentRect.sizeDelta = Vector2.zero;
        }
        
        // 记录Undo
        Undo.RegisterCreatedObjectUndo(parent, "Create SpriteRenderer Group");
        
        var selectedItems = imageItems.Where(x => x.isSelected).ToList();
        int successCount = 0;
        
        for (int i = 0; i < selectedItems.Count; i++)
        {
            var item = selectedItems[i];
            try
            {
                // 获取贴图路径
                string texturePath = AssetDatabase.GetAssetPath(item.texture);
                
                // 检查并转换贴图类型为Sprite
                TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.SaveAndReimport();
                }
                
                // 加载Sprite
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
                
                if (sprite == null)
                {
                    Debug.LogWarning($"无法加载 {item.name} 的Sprite");
                    continue;
                }
                
                // 创建SpriteRenderer对象
                GameObject spriteObj = new GameObject(item.name);
                spriteObj.transform.SetParent(parent.transform);
                
                // 根据Transform类型设置
                if (transformType == SpriteTransformType.Transform3D)
                {
                    // 3D标准模式：使用已乘以pixelUnit的坐标
                    float zPos = i * zSpacing;
                    if (item.hasPsdPosition)
                    {
                        spriteObj.transform.localPosition = new Vector3(item.psdPosition.x, item.psdPosition.y, zPos);
                    }
                    else
                    {
                        spriteObj.transform.localPosition = new Vector3(0, 0, zPos);
                    }
                    spriteObj.transform.localRotation = Quaternion.identity;
                    // Sprite 3D标准: Scale=(1,1,1)
                    spriteObj.transform.localScale = Vector3.one;
                }
                else
                {
                    // 2D矩形模式：添加RectTransform，使用像素坐标
                    RectTransform rectTransform = spriteObj.AddComponent<RectTransform>();
                    rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    
                    // 使用像素坐标设置位置，Z轴默认为0
                    if (item.hasPsdPosition)
                    {
                        // 根据坐标原点模式计算位置
                        float posX, posY;
                        switch (psdOriginMode)
                        {
                            case PsdOriginMode.Center:
                                // 画布中心为原点，直接使用像素坐标
                                posX = item.psdPixelPosition.x;
                                posY = item.psdPixelPosition.y;
                                break;
                            case PsdOriginMode.TopLeft:
                                // 左上角为原点，需要转换
                                posX = item.psdPixelPosition.x + item.psdCanvasWidth / 2f;
                                posY = item.psdPixelPosition.y + item.psdCanvasHeight / 2f;
                                break;
                            case PsdOriginMode.BottomLeft:
                                // 左下角为原点
                                posX = item.psdPixelPosition.x + item.psdCanvasWidth / 2f;
                                posY = item.psdPixelPosition.y - item.psdCanvasHeight / 2f;
                                break;
                            default:
                                posX = 0;
                                posY = 0;
                                break;
                        }
                        rectTransform.anchoredPosition = new Vector2(posX, posY);
                    }
                    else
                    {
                        rectTransform.anchoredPosition = Vector2.zero;
                    }
                    
                    // 2D矩形模式：Z轴按图层间距计算
                    float zPos = i * zSpacing;
                    rectTransform.localPosition = new Vector3(
                        rectTransform.localPosition.x, 
                        rectTransform.localPosition.y, 
                        zPos
                    );
                    
                    // Sprite 2D矩形: Width=图片x*0.01, Height=图片y*0.01
                    rectTransform.sizeDelta = new Vector2(item.width * 0.01f, item.height * 0.01f);
                    
                    // Scale=(100,100,100)
                    rectTransform.localScale = new Vector3(100, 100, 100);
                }
                
                spriteObj.transform.localRotation = Quaternion.identity;
                
                // 添加SpriteRenderer组件
                SpriteRenderer sr = spriteObj.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = sortingOrder;
                
                successCount++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"生成 {item.name} 失败: {e.Message}");
            }
        }
        
        // 选中父对象
        Selection.activeGameObject = parent;
        
        EditorUtility.DisplayDialog("完成", $"成功生成 {successCount}/{selectedItems.Count} 个SpriteRenderer", "确定");
    }
    
    /// <summary>
    /// 生成UGUI Image组件
    /// Image模式强制使用2D矩形(RectTransform)
    /// </summary>
    private void GenerateImages()
    {
        if (!CanGenerate()) return;
        
        // 创建父对象（强制使用RectTransform）
        GameObject parent = new GameObject(parentName);
        parent.transform.SetParent(targetObject.transform);
        
        RectTransform parentRect = parent.AddComponent<RectTransform>();
        parentRect.anchorMin = new Vector2(0.5f, 0.5f);
        parentRect.anchorMax = new Vector2(0.5f, 0.5f);
        parentRect.pivot = new Vector2(0.5f, 0.5f);
        parentRect.anchoredPosition = Vector2.zero;
        parentRect.sizeDelta = Vector2.zero;
        parentRect.localScale = Vector3.one;
        parentRect.localRotation = Quaternion.identity;
        
        // 记录Undo
        Undo.RegisterCreatedObjectUndo(parent, "Create Image Group");
        
        var selectedItems = imageItems.Where(x => x.isSelected).ToList();
        int successCount = 0;
        
        for (int i = 0; i < selectedItems.Count; i++)
        {
            var item = selectedItems[i];
            try
            {
                // 获取贴图路径
                string texturePath = AssetDatabase.GetAssetPath(item.texture);
                
                // 检查并转换贴图类型为Sprite
                TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.SaveAndReimport();
                }
                
                // 加载Sprite
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
                
                if (sprite == null)
                {
                    Debug.LogWarning($"无法加载 {item.name} 的Sprite");
                    continue;
                }
                
                // 创建Image对象（自带RectTransform）
                GameObject imgObj = new GameObject(item.name);
                imgObj.transform.SetParent(parent.transform);
                
                RectTransform rectTransform = imgObj.AddComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                
                // 使用像素坐标设置位置
                if (item.hasPsdPosition)
                {
                    float posX, posY;
                    switch (psdOriginMode)
                    {
                        case PsdOriginMode.Center:
                            posX = item.psdPixelPosition.x;
                            posY = item.psdPixelPosition.y;
                            break;
                        case PsdOriginMode.TopLeft:
                            posX = item.psdPixelPosition.x + item.psdCanvasWidth / 2f;
                            posY = item.psdPixelPosition.y + item.psdCanvasHeight / 2f;
                            break;
                        case PsdOriginMode.BottomLeft:
                            posX = item.psdPixelPosition.x + item.psdCanvasWidth / 2f;
                            posY = item.psdPixelPosition.y - item.psdCanvasHeight / 2f;
                            break;
                        default:
                            posX = 0;
                            posY = 0;
                            break;
                    }
                    rectTransform.anchoredPosition = new Vector2(posX, posY);
                }
                else
                {
                    rectTransform.anchoredPosition = Vector2.zero;
                }
                
                // Z轴按图层间距计算
                float zPos = i * zSpacing;
                rectTransform.localPosition = new Vector3(
                    rectTransform.localPosition.x, 
                    rectTransform.localPosition.y, 
                    zPos
                );
                
                // Image 2D矩形: sizeDelta=图片原始像素尺寸
                rectTransform.sizeDelta = new Vector2(item.width, item.height);
                rectTransform.localScale = Vector3.one;
                rectTransform.localRotation = Quaternion.identity;
                
                // 添加Image组件
                Image image = imgObj.AddComponent<Image>();
                image.sprite = sprite;
                image.raycastTarget = imageRaycastTarget;
                // 保持原始比例，不拉伸
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                
                successCount++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"生成 {item.name} 失败: {e.Message}");
            }
        }
        
        // 选中父对象
        Selection.activeGameObject = parent;
        
        EditorUtility.DisplayDialog("完成", $"成功生成 {successCount}/{selectedItems.Count} 个Image", "确定");
    }
    
    /// <summary>
    /// 获取所有排序图层名称
    /// </summary>
    private string[] GetSortingLayerNames()
    {
        System.Type internalEditorUtilityType = typeof(UnityEditorInternal.InternalEditorUtility);
        PropertyInfo sortingLayersProperty = internalEditorUtilityType.GetProperty("sortingLayerNames", BindingFlags.Static | BindingFlags.NonPublic);
        if (sortingLayersProperty != null)
        {
            return (string[])sortingLayersProperty.GetValue(null, new object[0]);
        }
        return new string[] { "Default" };
    }
    
    #endregion
    
    #region Selection监听
    
    private void OnSelectionChange()
    {
        Repaint();
    }
    
    #endregion
}

