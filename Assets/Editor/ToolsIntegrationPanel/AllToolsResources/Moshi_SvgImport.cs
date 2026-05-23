using UnityEngine;
using UnityEditor;
using UnityEditor.Presets;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

/// <summary>
/// SVG位置导入工具
/// 从SVG文件解析元素位置信息，批量生成Quad/Sprite/粒子系统
/// </summary>
public class Moshi_SvgImport : EditorWindow
{
    #region 数据结构
    
    [System.Serializable]
    public class ImageItem
    {
        public bool isSelected = true;
        public string name;
        public Texture2D texture;
        public int width;
        public int height;
        public bool hasSvgPosition = false;
        public Vector2 svgPosition;
        public Vector2 svgPixelPosition;
        public int svgCanvasWidth;
        public int svgCanvasHeight;
        
        public ImageItem(Texture2D tex)
        {
            texture = tex;
            name = tex.name;
            width = tex.width;
            height = tex.height;
        }
        
        public ImageItem(string elementName, Vector2 position, Vector2 pixelPosition, 
                         int elementWidth, int elementHeight, int canvasWidth, int canvasHeight)
        {
            texture = null;
            name = elementName;
            width = elementWidth;
            height = elementHeight;
            hasSvgPosition = true;
            svgPosition = position;
            svgPixelPosition = pixelPosition;
            svgCanvasWidth = canvasWidth;
            svgCanvasHeight = canvasHeight;
        }
    }
    
    [System.Serializable]
    public class SvgElementInfo
    {
        public string id;
        public string tagName;
        public float x;
        public float y;
        public float width;
        public float height;
        public bool isSelected = true;
        public bool isGroup;
        public string fill;
        public string stroke;
        public Texture2D texture;
        public string texturePath;
        public string pathData;  // 存储 path 的 d 属性，用于矢量绘制
        
        public float CenterX => x + width / 2f;
        public float CenterY => y + height / 2f;
    }
    
    [System.Serializable]
    public class SvgDocumentInfo
    {
        public string filePath;
        public string fileName;
        public int canvasWidth;
        public int canvasHeight;
        public List<SvgElementInfo> elements = new List<SvgElementInfo>();
    }
    
    public enum OriginMode { Center, TopLeft, BottomLeft }
    public enum SizeMode { PixelRatio, WidthOne, HeightOne }
    public enum GenerateMode { Quad, SpriteRenderer, ParticleSystem }
    public enum TransformType { Transform3D, RectTransform2D }
    private enum SvgTextureMatchMode { ById, ByFolder, Manual }
    
    #endregion
    
    #region 字段
    
    private GameObject targetObject;
    private string parentName = "fx_svg_group";
    private Shader selectedShader;
    private string texturePropertyName = "_MainTex";
    private string materialFolder = "";
    private SizeMode sizeMode = SizeMode.PixelRatio;
    private float pixelUnit = 0.01f;
    private GenerateMode generateMode = GenerateMode.Quad;
    private Preset particlePreset;
    private string sortingLayerName = "Default";
    private int sortingOrder = 0;
    private TransformType transformType = TransformType.Transform3D;
    
    private List<ImageItem> imageItems = new List<ImageItem>();
    private Vector2 listScrollPos;
    private Vector2 previewScrollPos;
    
    private bool svgSectionFoldout = true;
    private List<SvgDocumentInfo> svgDocuments = new List<SvgDocumentInfo>();
    private int selectedSvgIndex = -1;
    private Vector2 svgDocumentScrollPos;
    private Vector2 svgElementScrollPos;
    private bool svgSkipGroups = true;
    private Rect svgDropRect;
    private Rect dragDropRect;
    
    private OriginMode originMode = OriginMode.Center;
    private SvgTextureMatchMode svgTextureMatchMode = SvgTextureMatchMode.ById;
    private DefaultAsset svgTextureSearchFolder;
    private string svgTextureSearchPath = "";
    private string svgImageOutputFolder = "Assets/Textures/SVG_Images";  // SVG图片输出目录
    
    private const string TOOL_NAME = "SVG位置导入";
    
    #endregion
    
    [MenuItem("工具/Moshi/" + TOOL_NAME)]
    public static void ShowWindow()
    {
        var window = GetWindow<Moshi_SvgImport>(TOOL_NAME);
        window.minSize = new Vector2(450, 600);
    }
    
    private void OnEnable()
    {
        RefreshTargetObject();
        if (selectedShader == null)
        {
            selectedShader = Shader.Find("Pandavfx/Pandavfx_v2.3");
            if (selectedShader == null) selectedShader = Shader.Find("Unlit/Transparent");
        }
    }
    
    private void OnGUI()
    {
        EditorGUILayout.Space(5);
        DrawBasicSettings();
        EditorGUILayout.Space(10);
        DrawSvgImportSection();
        EditorGUILayout.Space(10);
        DrawGenerateList();
        EditorGUILayout.Space(10);
        DrawOutputPreview();
        EditorGUILayout.Space(10);
        DrawGenerateButton();
        HandleDragAndDrop();
        HandleSvgDragAndDrop();
    }
    
    #region SVG导入区域
    
    private void DrawSvgImportSection()
    {
        int totalSelected = svgDocuments.Sum(svg => svg.elements.Count(e => e.isSelected && !e.isGroup));
        string title = svgDocuments.Count > 0 
            ? $"═══ SVG导入 ═══  📐 {svgDocuments.Count}个文件 ({totalSelected}个元素已选)"
            : "═══ SVG导入 ═══";
        
        svgSectionFoldout = EditorGUILayout.Foldout(svgSectionFoldout, title, true, EditorStyles.foldoutHeader);
        if (!svgSectionFoldout) return;
        
        // 先显示图片目录设置（必须先设置才能导入）
        DrawImageOutputFolder();
        
        DrawSvgDropZone();
        if (svgDocuments.Count > 0)
        {
            DrawSvgDocumentList();
            DrawSvgSettings();
            DrawSvgElementList();
            DrawSvgImportButton();
        }
    }
    
    /// <summary>
    /// 绘制图片输出目录设置（在SVG导入之前）
    /// </summary>
    private void DrawImageOutputFolder()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("图片目录", GUILayout.Width(60));
        
        EditorGUI.BeginChangeCheck();
        svgImageOutputFolder = EditorGUILayout.TextField(svgImageOutputFolder);
        if (EditorGUI.EndChangeCheck())
        {
            // 用户修改了路径，进行格式化
            if (!string.IsNullOrEmpty(svgImageOutputFolder))
            {
                svgImageOutputFolder = svgImageOutputFolder.Replace("\\", "/").TrimEnd('/');
                if (!svgImageOutputFolder.StartsWith("Assets"))
                    svgImageOutputFolder = "Assets/" + svgImageOutputFolder.TrimStart('/');
            }
        }
        
        if (GUILayout.Button("选择", GUILayout.Width(50)))
        {
            string selected = EditorUtility.OpenFolderPanel("选择图片输出目录", "Assets", "");
            if (!string.IsNullOrEmpty(selected))
            {
                // 转换为相对路径
                if (selected.StartsWith(Application.dataPath))
                    svgImageOutputFolder = "Assets" + selected.Substring(Application.dataPath.Length);
                else
                    EditorUtility.DisplayDialog("错误", "请选择 Assets 目录下的文件夹", "确定");
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // 显示完整路径预览
        if (!string.IsNullOrEmpty(svgImageOutputFolder))
        {
            string fullPath = Application.dataPath + svgImageOutputFolder.Substring(6);
            EditorGUILayout.LabelField($"完整路径: {fullPath}", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();
    }
    
    private void DrawSvgDropZone()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(60));
        GUILayout.FlexibleSpace();
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField(svgDocuments.Count > 0 ? "📐 拖拽更多SVG文件" : "📐 拖拽SVG文件到此处", EditorStyles.centeredGreyMiniLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("选择文件", GUILayout.Width(80)))
        {
            string path = EditorUtility.OpenFilePanel("选择SVG文件", "", "svg");
            if (!string.IsNullOrEmpty(path)) AddSvgFile(path);
        }
        if (GUILayout.Button("粘贴代码", GUILayout.Width(80)))
            SvgPositionCodeInputWindow.ShowWindow(this);
        if (svgDocuments.Count > 0 && GUILayout.Button("清空全部", GUILayout.Width(80)))
        {
            svgDocuments.Clear();
            selectedSvgIndex = -1;
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();
        
        if (Event.current.type == EventType.Repaint)
            svgDropRect = GUILayoutUtility.GetLastRect();
    }
    
    private void DrawSvgDocumentList()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("SVG文件列表", EditorStyles.boldLabel);
        svgDocumentScrollPos = EditorGUILayout.BeginScrollView(svgDocumentScrollPos, GUILayout.MaxHeight(80));
        
        int removeIndex = -1;
        for (int i = 0; i < svgDocuments.Count; i++)
        {
            var svg = svgDocuments[i];
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(selectedSvgIndex == i, "", GUILayout.Width(20))) selectedSvgIndex = i;
            int ec = svg.elements.Count(e => !e.isGroup);
            int sc = svg.elements.Count(e => e.isSelected && !e.isGroup);
            EditorGUILayout.LabelField($"📐 {svg.fileName} ({svg.canvasWidth}×{svg.canvasHeight}) [{sc}/{ec}]", GUILayout.MinWidth(200));
            if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20))) removeIndex = i;
            EditorGUILayout.EndHorizontal();
        }
        
        if (removeIndex >= 0)
        {
            svgDocuments.RemoveAt(removeIndex);
            if (selectedSvgIndex >= svgDocuments.Count) selectedSvgIndex = svgDocuments.Count - 1;
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }
    
    private void DrawSvgSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("导入设置", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("坐标原点", GUILayout.Width(60));
        if (GUILayout.Toggle(originMode == OriginMode.Center, "中心点", EditorStyles.miniButtonLeft)) originMode = OriginMode.Center;
        if (GUILayout.Toggle(originMode == OriginMode.TopLeft, "左上角", EditorStyles.miniButtonMid)) originMode = OriginMode.TopLeft;
        if (GUILayout.Toggle(originMode == OriginMode.BottomLeft, "左下角", EditorStyles.miniButtonRight)) originMode = OriginMode.BottomLeft;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("元素过滤", GUILayout.Width(60));
        svgSkipGroups = GUILayout.Toggle(svgSkipGroups, "跳过组", EditorStyles.miniButton);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("贴图匹配", GUILayout.Width(60));
        if (GUILayout.Toggle(svgTextureMatchMode == SvgTextureMatchMode.ById, "按ID", EditorStyles.miniButtonLeft)) svgTextureMatchMode = SvgTextureMatchMode.ById;
        if (GUILayout.Toggle(svgTextureMatchMode == SvgTextureMatchMode.ByFolder, "按目录", EditorStyles.miniButtonMid)) svgTextureMatchMode = SvgTextureMatchMode.ByFolder;
        if (GUILayout.Toggle(svgTextureMatchMode == SvgTextureMatchMode.Manual, "手动", EditorStyles.miniButtonRight)) svgTextureMatchMode = SvgTextureMatchMode.Manual;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        if (svgTextureMatchMode == SvgTextureMatchMode.ByFolder)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("搜索目录", GUILayout.Width(60));
            EditorGUI.BeginChangeCheck();
            svgTextureSearchFolder = (DefaultAsset)EditorGUILayout.ObjectField(svgTextureSearchFolder, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck())
                svgTextureSearchPath = svgTextureSearchFolder != null ? AssetDatabase.GetAssetPath(svgTextureSearchFolder) : "";
            EditorGUILayout.EndHorizontal();
        }
        
        if (svgTextureMatchMode != SvgTextureMatchMode.Manual)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(65);
            if (GUILayout.Button("自动匹配", GUILayout.Width(80))) AutoMatchSvgTextures();
            if (svgDocuments.Count > 1 && GUILayout.Button("匹配全部", GUILayout.Width(80))) AutoMatchAllSvgTextures();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        
        // 矢量绘制按钮
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(65);
        if (GUILayout.Button("绘制Path", GUILayout.Width(80))) RenderSelectedPathsToTextures();
        if (GUILayout.Button("绘制全部", GUILayout.Width(80))) RenderAllPathsToTextures();
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawSvgElementList()
    {
        if (selectedSvgIndex < 0 || selectedSvgIndex >= svgDocuments.Count)
        {
            if (svgDocuments.Count > 0) selectedSvgIndex = 0;
            else return;
        }
        
        var currentSvg = svgDocuments[selectedSvgIndex];
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinHeight(120));
        EditorGUILayout.LabelField($"元素列表 - {currentSvg.fileName}", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("☑", GUILayout.Width(25));
        EditorGUILayout.LabelField("ID", GUILayout.MinWidth(60));
        EditorGUILayout.LabelField("类型", GUILayout.Width(45));
        EditorGUILayout.LabelField("位置", GUILayout.Width(80));
        EditorGUILayout.LabelField("尺寸", GUILayout.Width(60));
        EditorGUILayout.LabelField("贴图", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();
        
        svgElementScrollPos = EditorGUILayout.BeginScrollView(svgElementScrollPos, GUILayout.MaxHeight(150));
        var filtered = currentSvg.elements.Where(e => !svgSkipGroups || !e.isGroup).ToList();
        
        foreach (var el in filtered)
        {
            EditorGUILayout.BeginHorizontal();
            el.isSelected = EditorGUILayout.Toggle(el.isSelected, GUILayout.Width(25));
            string did = string.IsNullOrEmpty(el.id) ? "(无ID)" : (el.id.Length > 12 ? el.id.Substring(0, 9) + "..." : el.id);
            EditorGUILayout.LabelField(did, GUILayout.MinWidth(60));
            string icon = el.isGroup ? "📁" : (el.tagName == "rect" ? "⬜" : "📍");
            EditorGUILayout.LabelField($"{icon}{el.tagName}", GUILayout.Width(45));
            if (!el.isGroup)
            {
                EditorGUILayout.LabelField($"({el.CenterX:F0},{el.CenterY:F0})", GUILayout.Width(80));
                EditorGUILayout.LabelField($"{el.width:F0}×{el.height:F0}", GUILayout.Width(60));
                EditorGUI.BeginChangeCheck();
                el.texture = (Texture2D)EditorGUILayout.ObjectField(el.texture, typeof(Texture2D), false, GUILayout.Width(80));
                if (EditorGUI.EndChangeCheck() && el.texture != null)
                    el.texturePath = AssetDatabase.GetAssetPath(el.texture);
            }
            else
            {
                EditorGUILayout.LabelField("-", GUILayout.Width(80));
                EditorGUILayout.LabelField("(组)", GUILayout.Width(60));
                EditorGUILayout.LabelField("", GUILayout.Width(80));
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全选", EditorStyles.miniButtonLeft, GUILayout.Width(50)))
            foreach (var e in filtered) e.isSelected = true;
        if (GUILayout.Button("反选", EditorStyles.miniButtonMid, GUILayout.Width(50)))
            foreach (var e in filtered) e.isSelected = !e.isSelected;
        if (GUILayout.Button("有贴图", EditorStyles.miniButtonRight, GUILayout.Width(50)))
            foreach (var e in filtered) e.isSelected = (e.texture != null);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"已选中: {filtered.Count(e => e.isSelected)}/{filtered.Count}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawSvgImportButton()
    {
        EditorGUILayout.Space(5);
        int total = svgDocuments.Sum(svg => svg.elements.Count(e => (!svgSkipGroups || !e.isGroup) && e.isSelected && !e.isGroup));
        EditorGUI.BeginDisabledGroup(total == 0);
        if (GUILayout.Button($"🔽 导入选中元素到生成列表 ({total}个)", GUILayout.Height(28)))
            ImportAllSvgElementsToList();
        EditorGUI.EndDisabledGroup();
    }
    
    #endregion
    
    #region SVG解析
    
    private void AddSvgFile(string filePath)
    {
        try
        {
            string content = File.ReadAllText(filePath);
            var svg = ParseSvgContent(content, Path.GetFileName(filePath));
            svg.filePath = filePath;
            if (svg == null || svg.elements.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "SVG解析失败或无元素", "确定");
                return;
            }
            if (svgDocuments.Any(s => s.fileName == svg.fileName))
            {
                if (!EditorUtility.DisplayDialog("确认", $"已存在 {svg.fileName}，是否替换？", "替换", "取消")) return;
                svgDocuments.RemoveAll(s => s.fileName == svg.fileName);
            }
            svgDocuments.Add(svg);
            selectedSvgIndex = svgDocuments.Count - 1;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"加载SVG失败: {e.Message}");
            EditorUtility.DisplayDialog("错误", $"加载失败: {e.Message}", "确定");
        }
    }
    
    public void LoadSvgFromCode(string svgCode)
    {
        try
        {
            var svg = ParseSvgContent(svgCode, $"粘贴的SVG_{svgDocuments.Count + 1}");
            if (svg == null || svg.elements.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "解析失败或无元素", "确定");
                return;
            }
            svgDocuments.Add(svg);
            selectedSvgIndex = svgDocuments.Count - 1;
            Repaint();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析失败: {e.Message}");
            EditorUtility.DisplayDialog("错误", $"解析失败: {e.Message}", "确定");
        }
    }
    
    private void AutoMatchSvgTextures()
    {
        if (selectedSvgIndex < 0 || selectedSvgIndex >= svgDocuments.Count) return;
        int count = MatchTexturesForSvg(svgDocuments[selectedSvgIndex]);
        EditorUtility.DisplayDialog("完成", $"匹配了 {count} 个贴图", "确定");
        Repaint();
    }
    
    private void AutoMatchAllSvgTextures()
    {
        int total = svgDocuments.Sum(svg => MatchTexturesForSvg(svg));
        EditorUtility.DisplayDialog("完成", $"匹配了 {total} 个贴图", "确定");
        Repaint();
    }
    
    private int MatchTexturesForSvg(SvgDocumentInfo svg)
    {
        int count = 0;
        foreach (var el in svg.elements.Where(e => !e.isGroup && !string.IsNullOrEmpty(e.id)))
        {
            Texture2D tex = null;
            string[] guids = svgTextureMatchMode == SvgTextureMatchMode.ByFolder && !string.IsNullOrEmpty(svgTextureSearchPath)
                ? AssetDatabase.FindAssets("t:Texture2D", new[] { svgTextureSearchPath })
                : AssetDatabase.FindAssets($"{el.id} t:Texture2D");
            
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fn = Path.GetFileNameWithoutExtension(path);
                if (fn.Equals(el.id, System.StringComparison.OrdinalIgnoreCase))
                {
                    tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    break;
                }
            }
            if (tex == null && guids.Length > 0)
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0]));
            
            if (tex != null)
            {
                el.texture = tex;
                el.texturePath = AssetDatabase.GetAssetPath(tex);
                count++;
            }
        }
        return count;
    }
    
    private SvgDocumentInfo ParseSvgContent(string content, string fileName)
    {
        var svg = new SvgDocumentInfo { fileName = fileName };
        
        var wm = Regex.Match(content, @"<svg[^>]*\swidth\s*=\s*[""']?(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
        var hm = Regex.Match(content, @"<svg[^>]*\sheight\s*=\s*[""']?(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
        if (wm.Success) svg.canvasWidth = (int)float.Parse(wm.Groups[1].Value);
        if (hm.Success) svg.canvasHeight = (int)float.Parse(hm.Groups[1].Value);
        
        if (svg.canvasWidth == 0 || svg.canvasHeight == 0)
        {
            var vb = Regex.Match(content, @"viewBox\s*=\s*[""']?\s*[\d.]+\s+[\d.]+\s+([\d.]+)\s+([\d.]+)", RegexOptions.IgnoreCase);
            if (vb.Success)
            {
                if (svg.canvasWidth == 0) svg.canvasWidth = (int)float.Parse(vb.Groups[1].Value);
                if (svg.canvasHeight == 0) svg.canvasHeight = (int)float.Parse(vb.Groups[2].Value);
            }
        }
        if (svg.canvasWidth == 0) svg.canvasWidth = 1920;
        if (svg.canvasHeight == 0) svg.canvasHeight = 1080;
        
        // 1. 先解析所有 <pattern> 定义，建立 patternId -> base64 映射
        var patternBase64 = ParsePatternDefinitions(content);
        
        // 2. 移除 <defs>、<mask>、<clipPath> 等定义区域，避免把内部元素当作独立元素解析
        string contentForElements = content;
        contentForElements = Regex.Replace(contentForElements, @"<defs[^>]*>.*?</defs>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        contentForElements = Regex.Replace(contentForElements, @"<mask[^>]*>.*?</mask>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        contentForElements = Regex.Replace(contentForElements, @"<clipPath[^>]*>.*?</clipPath>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        contentForElements = Regex.Replace(contentForElements, @"<symbol[^>]*>.*?</symbol>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        int idx = 0;
        foreach (Match m in Regex.Matches(contentForElements, @"<rect\s+([^>]*)/?>")){
            var el = ParseRectElement(m.Groups[1].Value, idx++, patternBase64, fileName);
            if (el != null && el.width > 0 && el.height > 0) svg.elements.Add(el);
        }
        foreach (Match m in Regex.Matches(contentForElements, @"<g\s+([^>]*)>")){
            var el = ParseElement(m.Groups[1].Value, "g", idx++);
            if (el != null) { el.isGroup = true; svg.elements.Add(el); }
        }
        foreach (Match m in Regex.Matches(contentForElements, @"<path\s+([^>]*)/?>")){
            var el = ParsePathElement(m.Groups[1].Value, idx++, patternBase64, fileName);
            if (el != null && el.width > 0 && el.height > 0) svg.elements.Add(el);
        }
        foreach (Match m in Regex.Matches(contentForElements, @"<circle\s+([^>]*)/?>")){
            var el = ParseCircleElement(m.Groups[1].Value, idx++);
            if (el != null && el.width > 0) svg.elements.Add(el);
        }
        foreach (Match m in Regex.Matches(contentForElements, @"<ellipse\s+([^>]*)/?>")){
            var el = ParseEllipseElement(m.Groups[1].Value, idx++);
            if (el != null && el.width > 0) svg.elements.Add(el);
        }
        foreach (Match m in Regex.Matches(contentForElements, @"<image\s+([^>]*)/?>")){
            var el = ParseImageElement(m.Groups[1].Value, idx++, svg.fileName);
            if (el != null && el.width > 0 && el.height > 0) svg.elements.Add(el);
        }
        return svg;
    }
    
    /// <summary>
    /// 解析 SVG 中的 <pattern> 定义，提取内嵌的 base64 图片数据
    /// </summary>
    private Dictionary<string, string> ParsePatternDefinitions(string content)
    {
        var patternBase64 = new Dictionary<string, string>();
        
        // 匹配 <pattern id="xxx">...<image href="data:image/...;base64,..."/>...</pattern>
        var patternRegex = new Regex(
            @"<pattern\s+[^>]*id\s*=\s*[""']([^""']+)[""'][^>]*>.*?<image[^>]+(?:xlink:)?href\s*=\s*[""']data:image/(?:png|jpeg|jpg|gif);base64,([^""']+)[""'].*?</pattern>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase
        );
        
        foreach (Match m in patternRegex.Matches(content))
        {
            string patternId = m.Groups[1].Value;
            string base64Data = m.Groups[2].Value;
            patternBase64[patternId] = base64Data;
            Debug.Log($"解析 pattern base64: {patternId}");
        }
        
        // 也尝试匹配 use 引用的情况
        var patternRegex2 = new Regex(
            @"<pattern\s+[^>]*id\s*=\s*[""']([^""']+)[""'][^>]*>[^<]*<use[^>]+(?:xlink:)?href\s*=\s*[""']#([^""']+)[""']",
            RegexOptions.Singleline | RegexOptions.IgnoreCase
        );
        
        foreach (Match m in patternRegex2.Matches(content))
        {
            string patternId = m.Groups[1].Value;
            string imageRef = m.Groups[2].Value;
            
            var imageMatch = Regex.Match(content, 
                $@"<image[^>]+id\s*=\s*[""']{Regex.Escape(imageRef)}[""'][^>]+(?:xlink:)?href\s*=\s*[""']data:image/(?:png|jpeg|jpg|gif);base64,([^""']+)[""']",
                RegexOptions.IgnoreCase);
            
            if (imageMatch.Success && !patternBase64.ContainsKey(patternId))
            {
                patternBase64[patternId] = imageMatch.Groups[1].Value;
                Debug.Log($"解析 pattern base64 (use): {patternId}");
            }
        }
        
        return patternBase64;
    }
    
    /// <summary>
    /// 解析 rect 标签，支持 fill="url(#patternId)" 关联贴图
    /// </summary>
    private SvgElementInfo ParseRectElement(string attr, int idx, Dictionary<string, string> patternBase64, string svgFileName)
    {
        var el = new SvgElementInfo { tagName = "rect" };
        var idm = Regex.Match(attr, @"id\s*=\s*[""']([^""']+)[""']");
        el.id = idm.Success ? idm.Groups[1].Value : $"rect_{idx}";
        var xm = Regex.Match(attr, @"\bx\s*=\s*[""']?([\d.]+)");
        var ym = Regex.Match(attr, @"\by\s*=\s*[""']?([\d.]+)");
        var wm = Regex.Match(attr, @"width\s*=\s*[""']?([\d.]+)");
        var hm = Regex.Match(attr, @"height\s*=\s*[""']?([\d.]+)");
        if (xm.Success) el.x = float.Parse(xm.Groups[1].Value);
        if (ym.Success) el.y = float.Parse(ym.Groups[1].Value);
        if (wm.Success) el.width = float.Parse(wm.Groups[1].Value);
        if (hm.Success) el.height = float.Parse(hm.Groups[1].Value);
        
        // 检查 fill 是否引用 pattern
        var fillMatch = Regex.Match(attr, @"fill\s*=\s*[""']url\(#([^)]+)\)[""']");
        if (fillMatch.Success)
        {
            string patternId = fillMatch.Groups[1].Value;
            if (patternBase64 != null && patternBase64.TryGetValue(patternId, out var base64Data))
            {
                // 按 rect 的尺寸保存贴图
                el.texture = SaveBase64AsPng(base64Data, el.id, svgFileName, (int)el.width, (int)el.height);
                if (el.texture != null)
                    el.texturePath = AssetDatabase.GetAssetPath(el.texture);
                el.fill = $"pattern:{patternId}";
            }
        }
        
        return el;
    }
    
    /// <summary>
    /// 解析 image 标签，提取 base64 数据并保存为 PNG
    /// </summary>
    private SvgElementInfo ParseImageElement(string attr, int idx, string svgFileName)
    {
        var el = new SvgElementInfo { tagName = "image" };
        var idm = Regex.Match(attr, @"id\s*=\s*[""']([^""']+)[""']");
        el.id = idm.Success ? idm.Groups[1].Value : $"image_{idx}";
        var xm = Regex.Match(attr, @"\bx\s*=\s*[""']?([\d.]+)");
        var ym = Regex.Match(attr, @"\by\s*=\s*[""']?([\d.]+)");
        var wm = Regex.Match(attr, @"width\s*=\s*[""']?([\d.]+)");
        var hm = Regex.Match(attr, @"height\s*=\s*[""']?([\d.]+)");
        if (xm.Success) el.x = float.Parse(xm.Groups[1].Value);
        if (ym.Success) el.y = float.Parse(ym.Groups[1].Value);
        if (wm.Success) el.width = float.Parse(wm.Groups[1].Value);
        if (hm.Success) el.height = float.Parse(hm.Groups[1].Value);
        
        // 提取 xlink:href 或 href 中的 base64 数据
        var hrefMatch = Regex.Match(attr, @"(?:xlink:)?href\s*=\s*[""']data:image/(?:png|jpeg|jpg|gif);base64,([^""']+)[""']", RegexOptions.IgnoreCase);
        if (hrefMatch.Success)
        {
            string base64Data = hrefMatch.Groups[1].Value;
            // 按SVG定义的尺寸保存贴图
            el.texture = SaveBase64AsPng(base64Data, el.id, svgFileName, (int)el.width, (int)el.height);
            if (el.texture != null)
                el.texturePath = AssetDatabase.GetAssetPath(el.texture);
        }
        
        return el;
    }
    
    /// <summary>
    /// 将 base64 数据保存为 PNG 文件，按目标尺寸缩放
    /// </summary>
    private Texture2D SaveBase64AsPng(string base64Data, string elementId, string svgFileName, int targetWidth = 0, int targetHeight = 0)
    {
        try
        {
            // 先确保输出目录存在
            EnsureOutputFolderExists();
            
            // 生成文件名（SVG文件名_元素ID）
            string svgBaseName = Path.GetFileNameWithoutExtension(svgFileName);
            string fileName = $"{svgBaseName}_{elementId}.png";
            string assetPath = svgImageOutputFolder + "/" + fileName;
            
            // 构建完整的文件系统路径
            string fullPath = Application.dataPath + assetPath.Substring(6); // 去掉 "Assets" 前缀
            
            Debug.Log($"[SaveBase64AsPng] 目标路径: {fullPath}, 目标尺寸: {targetWidth}x{targetHeight}");
            
            // 检查是否已存在
            if (File.Exists(fullPath))
            {
                // 已存在则直接加载
                Debug.Log($"[SaveBase64AsPng] 文件已存在，直接加载: {assetPath}");
                return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }
            
            // 解码 base64
            byte[] imageData = System.Convert.FromBase64String(base64Data);
            
            // 创建临时 Texture2D 来加载图片数据
            Texture2D tempTex = new Texture2D(2, 2);
            if (!tempTex.LoadImage(imageData))
            {
                Debug.LogWarning($"无法解码图片数据: {elementId}");
                return null;
            }
            
            // 如果指定了目标尺寸，进行缩放
            Texture2D finalTex = tempTex;
            if (targetWidth > 0 && targetHeight > 0 && (tempTex.width != targetWidth || tempTex.height != targetHeight))
            {
                int originalWidth = tempTex.width;
                int originalHeight = tempTex.height;
                finalTex = ResizeTexture(tempTex, targetWidth, targetHeight);
                DestroyImmediate(tempTex);
                Debug.Log($"[SaveBase64AsPng] 缩放贴图: {originalWidth}x{originalHeight} -> {targetWidth}x{targetHeight}");
            }
            
            // 转换为 PNG
            byte[] pngData = finalTex.EncodeToPNG();
            DestroyImmediate(finalTex);
            
            // 保存文件
            File.WriteAllBytes(fullPath, pngData);
            
            // 刷新资源数据库
            AssetDatabase.Refresh();
            
            // 设置贴图导入设置
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;  // 保持原始尺寸，不缩放非2的幂次
                importer.SaveAndReimport();
            }
            
            Debug.Log($"已保存 SVG 图片: {assetPath} ({targetWidth}x{targetHeight})");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存 SVG 图片失败 [{elementId}]: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 缩放贴图到指定尺寸
    /// </summary>
    private Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
    {
        RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        rt.filterMode = FilterMode.Bilinear;
        
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);
        
        Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        result.Apply();
        
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        
        return result;
    }
    
    private SvgElementInfo ParseElement(string attr, string tag, int idx)
    {
        var el = new SvgElementInfo { tagName = tag };
        var idm = Regex.Match(attr, @"id\s*=\s*[""']([^""']+)[""']");
        el.id = idm.Success ? idm.Groups[1].Value : $"{tag}_{idx}";
        var xm = Regex.Match(attr, @"\bx\s*=\s*[""']?([\d.]+)");
        var ym = Regex.Match(attr, @"\by\s*=\s*[""']?([\d.]+)");
        var wm = Regex.Match(attr, @"width\s*=\s*[""']?([\d.]+)");
        var hm = Regex.Match(attr, @"height\s*=\s*[""']?([\d.]+)");
        if (xm.Success) el.x = float.Parse(xm.Groups[1].Value);
        if (ym.Success) el.y = float.Parse(ym.Groups[1].Value);
        if (wm.Success) el.width = float.Parse(wm.Groups[1].Value);
        if (hm.Success) el.height = float.Parse(hm.Groups[1].Value);
        return el;
    }
    
    /// <summary>
    /// 解析 path 标签，支持 fill="url(#patternId)" 关联贴图
    /// </summary>
    private SvgElementInfo ParsePathElement(string attr, int idx, Dictionary<string, string> patternBase64, string svgFileName)
    {
        var el = new SvgElementInfo { tagName = "path" };
        var idm = Regex.Match(attr, @"id\s*=\s*[""']([^""']+)[""']");
        el.id = idm.Success ? idm.Groups[1].Value : $"path_{idx}";
        var dm = Regex.Match(attr, @"d\s*=\s*[""']([^""']+)[""']");
        if (dm.Success)
        {
            el.pathData = dm.Groups[1].Value;  // 保存 path 数据用于矢量绘制
            var nums = new List<float>();
            foreach (Match m in Regex.Matches(dm.Groups[1].Value, @"-?[\d.]+"))
                if (float.TryParse(m.Value, out float n)) nums.Add(n);
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < nums.Count - 1; i += 2)
            {
                if (System.Math.Abs(nums[i]) < 10000 && System.Math.Abs(nums[i + 1]) < 10000)
                {
                    minX = Mathf.Min(minX, nums[i]); maxX = Mathf.Max(maxX, nums[i]);
                    minY = Mathf.Min(minY, nums[i + 1]); maxY = Mathf.Max(maxY, nums[i + 1]);
                }
            }
            if (minX != float.MaxValue) { el.x = minX; el.y = minY; el.width = maxX - minX; el.height = maxY - minY; }
        }
        
        // 检查 fill 是否引用 pattern
        var fillMatch = Regex.Match(attr, @"fill\s*=\s*[""']url\(#([^)]+)\)[""']");
        if (fillMatch.Success)
        {
            string patternId = fillMatch.Groups[1].Value;
            if (patternBase64 != null && patternBase64.TryGetValue(patternId, out var base64Data))
            {
                // 按 path 的尺寸保存贴图
                el.texture = SaveBase64AsPng(base64Data, el.id, svgFileName, (int)el.width, (int)el.height);
                if (el.texture != null)
                    el.texturePath = AssetDatabase.GetAssetPath(el.texture);
                el.fill = $"pattern:{patternId}";
            }
        }
        else
        {
            // 提取普通颜色填充
            var colorFillMatch = Regex.Match(attr, @"fill\s*=\s*[""']([^""']+)[""']");
            if (colorFillMatch.Success)
                el.fill = colorFillMatch.Groups[1].Value;
        }
        
        return el;
    }
    
    private SvgElementInfo ParseCircleElement(string attr, int idx)
    {
        var el = new SvgElementInfo { tagName = "circle" };
        var idm = Regex.Match(attr, @"id\s*=\s*[""']([^""']+)[""']");
        el.id = idm.Success ? idm.Groups[1].Value : $"circle_{idx}";
        var cxm = Regex.Match(attr, @"cx\s*=\s*[""']?([\d.]+)");
        var cym = Regex.Match(attr, @"cy\s*=\s*[""']?([\d.]+)");
        var rm = Regex.Match(attr, @"\br\s*=\s*[""']?([\d.]+)");
        float cx = cxm.Success ? float.Parse(cxm.Groups[1].Value) : 0;
        float cy = cym.Success ? float.Parse(cym.Groups[1].Value) : 0;
        float r = rm.Success ? float.Parse(rm.Groups[1].Value) : 0;
        el.x = cx - r; el.y = cy - r; el.width = r * 2; el.height = r * 2;
        return el;
    }
    
    private SvgElementInfo ParseEllipseElement(string attr, int idx)
    {
        var el = new SvgElementInfo { tagName = "ellipse" };
        var idm = Regex.Match(attr, @"id\s*=\s*[""']([^""']+)[""']");
        el.id = idm.Success ? idm.Groups[1].Value : $"ellipse_{idx}";
        var cxm = Regex.Match(attr, @"cx\s*=\s*[""']?([\d.]+)");
        var cym = Regex.Match(attr, @"cy\s*=\s*[""']?([\d.]+)");
        var rxm = Regex.Match(attr, @"rx\s*=\s*[""']?([\d.]+)");
        var rym = Regex.Match(attr, @"ry\s*=\s*[""']?([\d.]+)");
        float cx = cxm.Success ? float.Parse(cxm.Groups[1].Value) : 0;
        float cy = cym.Success ? float.Parse(cym.Groups[1].Value) : 0;
        float rx = rxm.Success ? float.Parse(rxm.Groups[1].Value) : 0;
        float ry = rym.Success ? float.Parse(rym.Groups[1].Value) : 0;
        el.x = cx - rx; el.y = cy - ry; el.width = rx * 2; el.height = ry * 2;
        return el;
    }
    
    private Vector2 ConvertSvgToUnityPosition(SvgElementInfo el, SvgDocumentInfo svg)
    {
        float cx = el.CenterX, cy = el.CenterY;
        switch (originMode)
        {
            case OriginMode.Center: return new Vector2((cx - svg.canvasWidth / 2f) * pixelUnit, (svg.canvasHeight / 2f - cy) * pixelUnit);
            case OriginMode.TopLeft: return new Vector2(cx * pixelUnit, -cy * pixelUnit);
            case OriginMode.BottomLeft: return new Vector2(cx * pixelUnit, (svg.canvasHeight - cy) * pixelUnit);
            default: return Vector2.zero;
        }
    }
    
    private void ImportAllSvgElementsToList()
    {
        int count = 0;
        bool multi = svgDocuments.Count > 1;
        foreach (var svg in svgDocuments)
        {
            string prefix = Path.GetFileNameWithoutExtension(svg.fileName);
            foreach (var el in svg.elements.Where(e => (!svgSkipGroups || !e.isGroup) && e.isSelected && !e.isGroup))
            {
                // 3D 模式位置（带缩放）
                Vector2 pos = ConvertSvgToUnityPosition(el, svg);
                
                // 2D 模式像素坐标（1:1 还原，不缩放）
                // SVG 左上角原点 → Unity 中心原点，Y 轴翻转
                Vector2 pix = new Vector2(
                    el.CenterX - svg.canvasWidth / 2f,   // X: 相对于画布中心
                    svg.canvasHeight / 2f - el.CenterY   // Y: 翻转后相对于画布中心
                );
                
                string name = !string.IsNullOrEmpty(el.id) ? el.id : $"{el.tagName}_{count}";
                string itemName = multi ? $"{prefix}_{name}" : name;
                if (imageItems.Any(x => x.name == itemName && x.hasSvgPosition)) continue;
                
                // 如果元素没有贴图，尝试自动匹配
                if (el.texture == null && !string.IsNullOrEmpty(el.id))
                {
                    el.texture = FindTextureById(el.id);
                    if (el.texture != null)
                        el.texturePath = AssetDatabase.GetAssetPath(el.texture);
                }
                
                // 尺寸使用 SVG 元素尺寸（保持与SVG定义一致）
                int w = (int)el.width;
                int h = (int)el.height;
                
                var item = new ImageItem(itemName, pos, pix, w, h, svg.canvasWidth, svg.canvasHeight);
                if (el.texture != null) item.texture = el.texture;
                imageItems.Add(item);
                count++;
            }
        }
        EditorUtility.DisplayDialog("完成", $"导入了 {count} 个元素", "确定");
    }
    
    /// <summary>
    /// 根据 ID 查找贴图
    /// </summary>
    private Texture2D FindTextureById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        
        string[] guids = svgTextureMatchMode == SvgTextureMatchMode.ByFolder && !string.IsNullOrEmpty(svgTextureSearchPath)
            ? AssetDatabase.FindAssets("t:Texture2D", new[] { svgTextureSearchPath })
            : AssetDatabase.FindAssets($"{id} t:Texture2D");
        
        // 优先精确匹配
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fn = Path.GetFileNameWithoutExtension(path);
            if (fn.Equals(id, System.StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        
        // 模糊匹配：文件名包含 ID
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fn = Path.GetFileNameWithoutExtension(path);
            if (fn.IndexOf(id, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        
        return null;
    }
    
    private void HandleSvgDragAndDrop()
    {
        Event evt = Event.current;
        if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && svgDropRect.Contains(evt.mousePosition))
        {
            var paths = DragAndDrop.paths.Where(p => p.EndsWith(".svg", System.StringComparison.OrdinalIgnoreCase)).ToList();
            if (paths.Count > 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var p in paths)
                    {
                        string fp = Path.IsPathRooted(p) ? p : Path.Combine(Application.dataPath.Replace("/Assets", ""), p);
                        AddSvgFile(fp);
                    }
                }
                evt.Use();
            }
        }
    }
    
    #endregion
    
    #region 基础设置和生成
    
    private void DrawBasicSettings()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("═══ 基础设置 ═══", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("目标对象", GUILayout.Width(60));
        EditorGUILayout.LabelField($"当前选中: {(targetObject != null ? targetObject.name : "未选中")}", EditorStyles.helpBox);
        if (GUILayout.Button("刷新", GUILayout.Width(50))) RefreshTargetObject();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("父对象名", GUILayout.Width(60));
        parentName = EditorGUILayout.TextField(parentName);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("生成模式", GUILayout.Width(60));
        if (GUILayout.Toggle(generateMode == GenerateMode.Quad, "Quad", EditorStyles.miniButtonLeft)) generateMode = GenerateMode.Quad;
        if (GUILayout.Toggle(generateMode == GenerateMode.SpriteRenderer, "Sprite", EditorStyles.miniButtonMid)) generateMode = GenerateMode.SpriteRenderer;
        if (GUILayout.Toggle(generateMode == GenerateMode.ParticleSystem, "粒子系统", EditorStyles.miniButtonRight)) generateMode = GenerateMode.ParticleSystem;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        if (generateMode != GenerateMode.SpriteRenderer)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Shader", GUILayout.Width(60));
            Rect sr = EditorGUILayout.GetControlRect();
            if (EditorGUI.DropdownButton(sr, new GUIContent(selectedShader != null ? selectedShader.name : "None"), FocusType.Keyboard))
                ShowShaderMenu(sr);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("贴图属性", GUILayout.Width(60));
            texturePropertyName = EditorGUILayout.TextField(texturePropertyName);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("材质目录", GUILayout.Width(60));
            materialFolder = EditorGUILayout.TextField(materialFolder);
            EditorGUILayout.EndHorizontal();
            
            if (transformType == TransformType.Transform3D)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("尺寸基准", GUILayout.Width(60));
                if (GUILayout.Toggle(sizeMode == SizeMode.PixelRatio, "像素比例", EditorStyles.miniButtonLeft)) sizeMode = SizeMode.PixelRatio;
                if (GUILayout.Toggle(sizeMode == SizeMode.WidthOne, "宽度为1", EditorStyles.miniButtonMid)) sizeMode = SizeMode.WidthOne;
                if (GUILayout.Toggle(sizeMode == SizeMode.HeightOne, "高度为1", EditorStyles.miniButtonRight)) sizeMode = SizeMode.HeightOne;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                
                if (sizeMode == SizeMode.PixelRatio)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("像素单位", GUILayout.Width(60));
                    pixelUnit = EditorGUILayout.FloatField(pixelUnit, GUILayout.Width(80));
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
            }
            
            if (generateMode == GenerateMode.ParticleSystem)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("粒子预设", GUILayout.Width(60));
                particlePreset = (Preset)EditorGUILayout.ObjectField(particlePreset, typeof(Preset), false);
                EditorGUILayout.EndHorizontal();
            }
        }
        
        if (generateMode == GenerateMode.SpriteRenderer)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("排序图层", GUILayout.Width(60));
            string[] layers = GetSortingLayerNames();
            int ci = System.Array.IndexOf(layers, sortingLayerName);
            if (ci < 0) ci = 0;
            int ni = EditorGUILayout.Popup(ci, layers);
            if (ni >= 0 && ni < layers.Length) sortingLayerName = layers[ni];
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("层级顺序", GUILayout.Width(60));
            sortingOrder = EditorGUILayout.IntField(sortingOrder);
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("变换类型", GUILayout.Width(60));
        if (GUILayout.Toggle(transformType == TransformType.Transform3D, "3D标准", EditorStyles.miniButtonLeft)) transformType = TransformType.Transform3D;
        if (GUILayout.Toggle(transformType == TransformType.RectTransform2D, "2D矩形", EditorStyles.miniButtonRight)) transformType = TransformType.RectTransform2D;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }
    
    private void ShowShaderMenu(Rect r)
    {
        var menu = new GenericMenu();
        foreach (var info in ShaderUtil.GetAllShaderInfo())
        {
            if (info.name.StartsWith("Hidden/")) continue;
            string n = info.name;
            menu.AddItem(new GUIContent(n), selectedShader != null && selectedShader.name == n, () => { selectedShader = Shader.Find(n); Repaint(); });
        }
        menu.DropDown(r);
    }
    
    private void DrawGenerateList()
    {
        EditorGUILayout.LabelField("═══ 生成列表 ═══", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("从选中获取", GUILayout.Width(80))) AddFromSelection();
        if (GUILayout.Button("清空列表", GUILayout.Width(80))) imageItems.Clear();
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        Rect lr = EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinHeight(100));
        if (imageItems.Count == 0)
            EditorGUILayout.LabelField("暂无图片", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(80));
        else
        {
            listScrollPos = EditorGUILayout.BeginScrollView(listScrollPos, GUILayout.MaxHeight(150));
            int rm = -1;
            for (int i = 0; i < imageItems.Count; i++)
            {
                var it = imageItems[i];
                EditorGUILayout.BeginHorizontal();
                it.isSelected = EditorGUILayout.Toggle(it.isSelected, GUILayout.Width(20));
                EditorGUILayout.LabelField(it.hasSvgPosition ? "📐" : "", GUILayout.Width(15));
                it.name = EditorGUILayout.TextField(it.name, GUILayout.MinWidth(60));
                EditorGUILayout.LabelField($"{it.width}x{it.height}", GUILayout.Width(60));
                if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20))) rm = i;
                EditorGUILayout.EndHorizontal();
            }
            if (rm >= 0) imageItems.RemoveAt(rm);
            EditorGUILayout.EndScrollView();
        }
        EditorGUILayout.EndVertical();
        if (Event.current.type == EventType.Repaint) dragDropRect = lr;
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全选", EditorStyles.miniButtonLeft, GUILayout.Width(50))) foreach (var it in imageItems) it.isSelected = true;
        if (GUILayout.Button("反选", EditorStyles.miniButtonRight, GUILayout.Width(50))) foreach (var it in imageItems) it.isSelected = !it.isSelected;
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"将生成: {imageItems.Count(x => x.isSelected)}/{imageItems.Count}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawOutputPreview()
    {
        EditorGUILayout.LabelField("═══ 输出预览 ═══", EditorStyles.boldLabel);
        previewScrollPos = EditorGUILayout.BeginScrollView(previewScrollPos, EditorStyles.helpBox, GUILayout.Height(80));
        EditorGUILayout.LabelField($"🎮 {(targetObject != null ? targetObject.name : "(未选中)")}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"    └── 📦 {parentName}");
        var sel = imageItems.Where(x => x.isSelected).Take(3).ToList();
        foreach (var it in sel)
            EditorGUILayout.LabelField($"        ├── {it.name}");
        if (imageItems.Count(x => x.isSelected) > 3)
            EditorGUILayout.LabelField($"        ... 还有 {imageItems.Count(x => x.isSelected) - 3} 项");
        EditorGUILayout.EndScrollView();
    }
    
    private void DrawGenerateButton()
    {
        EditorGUI.BeginDisabledGroup(!CanGenerate());
        string txt = generateMode == GenerateMode.Quad ? "生成Quad" : generateMode == GenerateMode.SpriteRenderer ? "生成Sprite" : "生成粒子";
        if (GUILayout.Button(txt, GUILayout.Height(35)))
        {
            if (generateMode == GenerateMode.Quad) GenerateQuads();
            else if (generateMode == GenerateMode.SpriteRenderer) GenerateSpriteRenderers();
            else GenerateParticleSystems();
        }
        EditorGUI.EndDisabledGroup();
        if (!CanGenerate())
        {
            string hint = targetObject == null ? "请选中目标对象" : imageItems.Count(x => x.isSelected) == 0 ? "请添加图片" : 
                          generateMode != GenerateMode.SpriteRenderer && (selectedShader == null || string.IsNullOrEmpty(materialFolder)) ? "请设置Shader和材质目录" : "";
            if (!string.IsNullOrEmpty(hint)) EditorGUILayout.HelpBox(hint, MessageType.Warning);
        }
    }
    
    private bool CanGenerate()
    {
        if (targetObject == null || imageItems.Count(x => x.isSelected) == 0) return false;
        if (generateMode != GenerateMode.SpriteRenderer && (selectedShader == null || string.IsNullOrEmpty(materialFolder))) return false;
        return true;
    }
    
    private void RefreshTargetObject() { targetObject = Selection.activeGameObject; }
    
    private void AddFromSelection()
    {
        var texs = Selection.objects.OfType<Texture2D>().ToList();
        foreach (var t in texs) if (!imageItems.Any(x => x.texture == t)) imageItems.Add(new ImageItem(t));
        if (texs.Count == 0) EditorUtility.DisplayDialog("提示", "请选中图片", "确定");
    }
    
    private void HandleDragAndDrop()
    {
        Event evt = Event.current;
        if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && dragDropRect.Contains(evt.mousePosition))
        {
            if (DragAndDrop.objectReferences.Any(x => x is Texture2D))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var o in DragAndDrop.objectReferences)
                        if (o is Texture2D t && !imageItems.Any(x => x.texture == t)) imageItems.Add(new ImageItem(t));
                }
                evt.Use();
            }
        }
    }
    
    private void GenerateQuads()
    {
        if (!AssetDatabase.IsValidFolder(materialFolder)) { EditorUtility.DisplayDialog("错误", "材质目录不存在", "确定"); return; }
        var parent = CreateParent();
        int count = 0;
        foreach (var it in imageItems.Where(x => x.isSelected))
        {
            try
            {
                var mat = CreateMaterial(it);
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = it.name;
                quad.transform.SetParent(parent.transform);
                SetTransform(quad, it);
                var col = quad.GetComponent<Collider>(); if (col) DestroyImmediate(col);
                quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
                count++;
            }
            catch (System.Exception e) { Debug.LogError($"生成 {it.name} 失败: {e.Message}"); }
        }
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = parent;
        EditorUtility.DisplayDialog("完成", $"生成了 {count} 个Quad", "确定");
    }
    
    private void GenerateParticleSystems()
    {
        if (!AssetDatabase.IsValidFolder(materialFolder)) { EditorUtility.DisplayDialog("错误", "材质目录不存在", "确定"); return; }
        var parent = CreateParent();
        int count = 0;
        foreach (var it in imageItems.Where(x => x.isSelected))
        {
            try
            {
                var mat = CreateMaterial(it);
                var obj = new GameObject(it.name);
                obj.transform.SetParent(parent.transform);
                SetTransform(obj, it, false);
                var ps = obj.AddComponent<ParticleSystem>();
                if (particlePreset != null && particlePreset.CanBeAppliedTo(ps)) particlePreset.ApplyTo(ps);
                var main = ps.main;
                main.startSize3D = true;
                main.startSizeX = it.width * (transformType == TransformType.Transform3D ? 0.01f : 1);
                main.startSizeY = it.height * (transformType == TransformType.Transform3D ? 0.01f : 1);
                main.startSizeZ = 1f;
                obj.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
                count++;
            }
            catch (System.Exception e) { Debug.LogError($"生成 {it.name} 失败: {e.Message}"); }
        }
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = parent;
        EditorUtility.DisplayDialog("完成", $"生成了 {count} 个粒子系统", "确定");
    }
    
    private void GenerateSpriteRenderers()
    {
        var parent = CreateParent();
        int count = 0;
        foreach (var it in imageItems.Where(x => x.isSelected))
        {
            try
            {
                string path = AssetDatabase.GetAssetPath(it.texture);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp != null && imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; imp.SaveAndReimport(); }
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;
                var obj = new GameObject(it.name);
                obj.transform.SetParent(parent.transform);
                SetTransformForSprite(obj, it, sprite);
                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = sortingOrder;
                count++;
            }
            catch (System.Exception e) { Debug.LogError($"生成 {it.name} 失败: {e.Message}"); }
        }
        Selection.activeGameObject = parent;
        EditorUtility.DisplayDialog("完成", $"生成了 {count} 个Sprite", "确定");
    }
    
    private GameObject CreateParent()
    {
        var parent = new GameObject(parentName);
        parent.transform.SetParent(targetObject.transform);
        parent.transform.localPosition = Vector3.zero;
        parent.transform.localRotation = Quaternion.identity;
        parent.transform.localScale = transformType == TransformType.Transform3D ? new Vector3(100, 100, 1) : Vector3.one;
        if (transformType == TransformType.RectTransform2D)
        {
            var rt = parent.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            
            // 设置父对象尺寸为 SVG 画布尺寸（用于参考）
            if (svgDocuments.Count > 0)
            {
                var svg = svgDocuments[0];
                rt.sizeDelta = new Vector2(svg.canvasWidth, svg.canvasHeight);
            }
            else
            {
                rt.sizeDelta = Vector2.zero;
            }
        }
        Undo.RegisterCreatedObjectUndo(parent, "Create SVG Group");
        return parent;
    }
    
    private Material CreateMaterial(ImageItem it)
    {
        var mat = new Material(selectedShader);
        mat.SetTexture(texturePropertyName, it.texture);
        string path = Path.Combine(materialFolder, it.name + ".mat").Replace("\\", "/");
        var exist = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (exist != null) { exist.shader = selectedShader; exist.SetTexture(texturePropertyName, it.texture); EditorUtility.SetDirty(exist); return exist; }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
    
    private void SetTransform(GameObject obj, ImageItem it, bool isQuad = true)
    {
        if (transformType == TransformType.Transform3D)
        {
            obj.transform.localPosition = it.hasSvgPosition ? new Vector3(it.svgPosition.x, it.svgPosition.y, 0) : Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = isQuad ? new Vector3(it.width * 0.01f, it.height * 0.01f, 1) : Vector3.one;
        }
        else // RectTransform2D - 直接还原 UI
        {
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            
            // 直接使用像素坐标
            rt.anchoredPosition = it.hasSvgPosition ? it.svgPixelPosition : Vector2.zero;
            rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, 0);
            
            // 直接使用像素尺寸
            rt.sizeDelta = new Vector2(it.width, it.height);
            rt.localScale = Vector3.one;
        }
    }
    
    /// <summary>
    /// 为 SpriteRenderer 设置变换，根据SVG尺寸缩放贴图
    /// </summary>
    private void SetTransformForSprite(GameObject obj, ImageItem it, Sprite sprite)
    {
        obj.transform.localPosition = it.hasSvgPosition ? new Vector3(it.svgPosition.x, it.svgPosition.y, 0) : Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        
        if (transformType == TransformType.Transform3D)
        {
            // 计算缩放比例：SVG尺寸 / 贴图尺寸 * 像素单位
            float spriteWidth = sprite.rect.width / sprite.pixelsPerUnit;
            float spriteHeight = sprite.rect.height / sprite.pixelsPerUnit;
            float scaleX = (it.width * 0.01f) / spriteWidth;
            float scaleY = (it.height * 0.01f) / spriteHeight;
            obj.transform.localScale = new Vector3(scaleX, scaleY, 1);
        }
        else // RectTransform2D
        {
            // 计算缩放比例：SVG尺寸 / 贴图像素尺寸
            float scaleX = it.width / sprite.rect.width;
            float scaleY = it.height / sprite.rect.height;
            obj.transform.localScale = new Vector3(scaleX, scaleY, 1);
            obj.transform.localPosition = it.hasSvgPosition ? new Vector3(it.svgPixelPosition.x, it.svgPixelPosition.y, 0) : Vector3.zero;
        }
    }
    
    private string[] GetSortingLayerNames()
    {
        var prop = typeof(UnityEditorInternal.InternalEditorUtility).GetProperty("sortingLayerNames", BindingFlags.Static | BindingFlags.NonPublic);
        return prop != null ? (string[])prop.GetValue(null, null) : new[] { "Default" };
    }
    
    private void OnSelectionChange() { Repaint(); }
    
    #endregion
    
    #region 矢量绘制
    
    /// <summary>
    /// 渲染选中的 path 元素为贴图
    /// </summary>
    private void RenderSelectedPathsToTextures()
    {
        if (selectedSvgIndex < 0 || selectedSvgIndex >= svgDocuments.Count) return;
        var svg = svgDocuments[selectedSvgIndex];
        var paths = svg.elements.Where(e => e.tagName == "path" && e.isSelected && !string.IsNullOrEmpty(e.pathData)).ToList();
        
        if (paths.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有选中的 path 元素", "确定");
            return;
        }
        
        int count = RenderPathsToTextures(paths, svg.fileName);
        EditorUtility.DisplayDialog("完成", $"成功绘制 {count} 个 path 为贴图", "确定");
        Repaint();
    }
    
    /// <summary>
    /// 渲染所有 path 元素为贴图
    /// </summary>
    private void RenderAllPathsToTextures()
    {
        int total = 0;
        foreach (var svg in svgDocuments)
        {
            var paths = svg.elements.Where(e => e.tagName == "path" && !string.IsNullOrEmpty(e.pathData)).ToList();
            total += RenderPathsToTextures(paths, svg.fileName);
        }
        EditorUtility.DisplayDialog("完成", $"成功绘制 {total} 个 path 为贴图", "确定");
        Repaint();
    }
    
    /// <summary>
    /// 将 path 元素列表渲染为贴图
    /// </summary>
    private int RenderPathsToTextures(List<SvgElementInfo> paths, string svgFileName)
    {
        int count = 0;
        string svgBaseName = Path.GetFileNameWithoutExtension(svgFileName);
        
        foreach (var el in paths)
        {
            try
            {
                var tex = RenderPathToTexture(el, svgBaseName);
                if (tex != null)
                {
                    el.texture = tex;
                    el.texturePath = AssetDatabase.GetAssetPath(tex);
                    count++;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"绘制 path [{el.id}] 失败: {e.Message}\n{e.StackTrace}");
            }
        }
        
        return count;
    }
    
    /// <summary>
    /// 将单个 path 渲染为 Texture2D（纯手动实现）
    /// </summary>
    private Texture2D RenderPathToTexture(SvgElementInfo el, string svgBaseName)
    {
        if (string.IsNullOrEmpty(el.pathData)) return null;
        
        // 先确保输出目录存在
        EnsureOutputFolderExists();
        
        // 构建文件路径
        string fileName = $"{svgBaseName}_{el.id}_path.png";
        string assetPath = svgImageOutputFolder + "/" + fileName;
        
        // 构建完整的文件系统路径
        string fullPath = Application.dataPath + assetPath.Substring(6); // 去掉 "Assets" 前缀
        
        Debug.Log($"[RenderPathToTexture] 目标路径: {fullPath}");
        
        // 解析填充颜色
        Color fillColor = ParseFillColor(el.fill);
        
        // 计算贴图尺寸（严格按照SVG定义的尺寸，不添加边距）
        int padding = 0;
        int texWidth = Mathf.Max(1, Mathf.CeilToInt(el.width));
        int texHeight = Mathf.Max(1, Mathf.CeilToInt(el.height));
        
        // 限制最大尺寸
        texWidth = Mathf.Min(texWidth, 2048);
        texHeight = Mathf.Min(texHeight, 2048);
        
        // 使用手动渲染
        var tex = RenderPathManually(el, texWidth, texHeight, fillColor, padding);
        
        if (tex == null) return null;
        
        byte[] pngData = tex.EncodeToPNG();
        File.WriteAllBytes(fullPath, pngData);
        
        DestroyImmediate(tex);
        AssetDatabase.Refresh();
        
        // 设置贴图导入设置
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;  // 保持原始尺寸，不缩放非2的幂次
            importer.SaveAndReimport();
        }
        
        Debug.Log($"已绘制 path: {assetPath}");
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }
    
    /// <summary>
    /// 从字符串中解析数字列表
    /// </summary>
    private List<float> ParseNumbers(string str)
    {
        var nums = new List<float>();
        foreach (Match m in Regex.Matches(str, @"-?[\d.]+(?:[eE][+-]?\d+)?"))
        {
            if (float.TryParse(m.Value, System.Globalization.NumberStyles.Float, 
                System.Globalization.CultureInfo.InvariantCulture, out float n))
                nums.Add(n);
        }
        return nums;
    }
    
    /// <summary>
    /// 解析填充颜色
    /// </summary>
    private Color ParseFillColor(string fill)
    {
        if (string.IsNullOrEmpty(fill) || fill.StartsWith("pattern:") || fill == "none")
            return Color.white;
        
        // 解析十六进制颜色
        if (fill.StartsWith("#"))
        {
            if (ColorUtility.TryParseHtmlString(fill, out Color c))
                return c;
        }
        
        // 常见颜色名称
        switch (fill.ToLower())
        {
            case "red": return Color.red;
            case "green": return Color.green;
            case "blue": return Color.blue;
            case "white": return Color.white;
            case "black": return Color.black;
            case "yellow": return Color.yellow;
            case "cyan": return Color.cyan;
            case "magenta": return Color.magenta;
            default: return Color.white;
        }
    }
    
    /// <summary>
    /// 手动渲染 path（使用扫描线填充 + 抗锯齿）
    /// </summary>
    private Texture2D RenderPathManually(SvgElementInfo el, int width, int height, Color fillColor, int padding)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color[width * height];
        
        // 填充透明背景
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.clear;
        
        // 解析路径为多个轮廓
        var contours = ParsePathToContours(el.pathData, el.x - padding, el.y - padding);
        if (contours == null || contours.Count == 0)
        {
            DestroyImmediate(tex);
            return null;
        }
        
        // 对每个轮廓进行填充
        foreach (var contour in contours)
        {
            if (contour.Count < 3) continue;
            
            // 使用扫描线填充算法
            for (int y = 0; y < height; y++)
            {
                var intersections = new List<float>();
                
                for (int i = 0; i < contour.Count; i++)
                {
                    var p1 = contour[i];
                    var p2 = contour[(i + 1) % contour.Count];
                    
                    if ((p1.y <= y && p2.y > y) || (p2.y <= y && p1.y > y))
                    {
                        float x = p1.x + (y - p1.y) / (p2.y - p1.y) * (p2.x - p1.x);
                        intersections.Add(x);
                    }
                }
                
                intersections.Sort();
                
                for (int i = 0; i < intersections.Count - 1; i += 2)
                {
                    int xStart = Mathf.Max(0, Mathf.CeilToInt(intersections[i]));
                    int xEnd = Mathf.Min(width - 1, Mathf.FloorToInt(intersections[i + 1]));
                    
                    for (int x = xStart; x <= xEnd; x++)
                    {
                        pixels[y * width + x] = fillColor;
                    }
                }
            }
        }
        
        // 绘制轮廓边缘（抗锯齿）
        foreach (var contour in contours)
        {
            for (int i = 0; i < contour.Count; i++)
            {
                var p1 = contour[i];
                var p2 = contour[(i + 1) % contour.Count];
                DrawLineAntialiased(pixels, width, height, p1, p2, fillColor);
            }
        }
        
        tex.SetPixels(pixels);
        tex.Apply();
        
        // 垂直翻转贴图
        var flippedTex = FlipTextureVertically(tex);
        DestroyImmediate(tex);
        
        return flippedTex;
    }
    
    /// <summary>
    /// 垂直翻转贴图
    /// </summary>
    private Texture2D FlipTextureVertically(Texture2D original)
    {
        int width = original.width;
        int height = original.height;
        var flipped = new Texture2D(width, height, original.format, false);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                flipped.SetPixel(x, height - 1 - y, original.GetPixel(x, y));
            }
        }
        
        flipped.Apply();
        return flipped;
    }
    
    /// <summary>
    /// 绘制抗锯齿线段（Xiaolin Wu's line algorithm）
    /// </summary>
    private void DrawLineAntialiased(Color[] pixels, int width, int height, Vector2 p0, Vector2 p1, Color color)
    {
        bool steep = Mathf.Abs(p1.y - p0.y) > Mathf.Abs(p1.x - p0.x);
        
        if (steep)
        {
            p0 = new Vector2(p0.y, p0.x);
            p1 = new Vector2(p1.y, p1.x);
        }
        
        if (p0.x > p1.x)
        {
            var temp = p0;
            p0 = p1;
            p1 = temp;
        }
        
        float dx = p1.x - p0.x;
        float dy = p1.y - p0.y;
        float gradient = dx == 0 ? 1 : dy / dx;
        
        // 第一个端点
        float xEnd = Mathf.Round(p0.x);
        float yEnd = p0.y + gradient * (xEnd - p0.x);
        float xGap = 1 - Frac(p0.x + 0.5f);
        int xPx1 = (int)xEnd;
        int yPx1 = (int)yEnd;
        
        if (steep)
        {
            PlotPixel(pixels, width, height, yPx1, xPx1, color, (1 - Frac(yEnd)) * xGap);
            PlotPixel(pixels, width, height, yPx1 + 1, xPx1, color, Frac(yEnd) * xGap);
        }
        else
        {
            PlotPixel(pixels, width, height, xPx1, yPx1, color, (1 - Frac(yEnd)) * xGap);
            PlotPixel(pixels, width, height, xPx1, yPx1 + 1, color, Frac(yEnd) * xGap);
        }
        
        float intery = yEnd + gradient;
        
        // 第二个端点
        xEnd = Mathf.Round(p1.x);
        yEnd = p1.y + gradient * (xEnd - p1.x);
        xGap = Frac(p1.x + 0.5f);
        int xPx2 = (int)xEnd;
        int yPx2 = (int)yEnd;
        
        if (steep)
        {
            PlotPixel(pixels, width, height, yPx2, xPx2, color, (1 - Frac(yEnd)) * xGap);
            PlotPixel(pixels, width, height, yPx2 + 1, xPx2, color, Frac(yEnd) * xGap);
        }
        else
        {
            PlotPixel(pixels, width, height, xPx2, yPx2, color, (1 - Frac(yEnd)) * xGap);
            PlotPixel(pixels, width, height, xPx2, yPx2 + 1, color, Frac(yEnd) * xGap);
        }
        
        // 主循环
        for (int x = xPx1 + 1; x < xPx2; x++)
        {
            if (steep)
            {
                PlotPixel(pixels, width, height, (int)intery, x, color, 1 - Frac(intery));
                PlotPixel(pixels, width, height, (int)intery + 1, x, color, Frac(intery));
            }
            else
            {
                PlotPixel(pixels, width, height, x, (int)intery, color, 1 - Frac(intery));
                PlotPixel(pixels, width, height, x, (int)intery + 1, color, Frac(intery));
            }
            intery += gradient;
        }
    }
    
    private float Frac(float x) => x - Mathf.Floor(x);
    
    private void PlotPixel(Color[] pixels, int width, int height, int x, int y, Color color, float brightness)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        int idx = y * width + x;
        var existing = pixels[idx];
        pixels[idx] = Color.Lerp(existing, color, brightness);
    }
    
    /// <summary>
    /// 将 path 数据解析为多个轮廓点列表
    /// </summary>
    private List<List<Vector2>> ParsePathToContours(string pathData, float offsetX, float offsetY)
    {
        var contours = new List<List<Vector2>>();
        var currentContour = new List<Vector2>();
        var commands = Regex.Matches(pathData, @"([MmLlHhVvCcSsQqTtAaZz])([^MmLlHhVvCcSsQqTtAaZz]*)");
        
        Vector2 currentPos = Vector2.zero;
        Vector2 startPos = Vector2.zero;
        Vector2 lastControlPoint = Vector2.zero;
        
        foreach (Match cmd in commands)
        {
            char command = cmd.Groups[1].Value[0];
            string args = cmd.Groups[2].Value;
            var nums = ParseNumbers(args);
            
            bool isRelative = char.IsLower(command);
            command = char.ToUpper(command);
            
            int i = 0;
            while (i < nums.Count || command == 'Z')
            {
                switch (command)
                {
                    case 'M':
                        if (currentContour.Count > 0)
                        {
                            contours.Add(new List<Vector2>(currentContour));
                            currentContour.Clear();
                        }
                        float mx = isRelative ? currentPos.x + nums[i] : nums[i] - offsetX;
                        float my = isRelative ? currentPos.y + nums[i + 1] : nums[i + 1] - offsetY;
                        currentPos = new Vector2(mx, my);
                        startPos = currentPos;
                        currentContour.Add(currentPos);
                        i += 2;
                        if (i < nums.Count) command = 'L';
                        break;
                        
                    case 'L':
                        float lx = isRelative ? currentPos.x + nums[i] : nums[i] - offsetX;
                        float ly = isRelative ? currentPos.y + nums[i + 1] : nums[i + 1] - offsetY;
                        currentPos = new Vector2(lx, ly);
                        currentContour.Add(currentPos);
                        i += 2;
                        break;
                        
                    case 'H':
                        float hx = isRelative ? currentPos.x + nums[i] : nums[i] - offsetX;
                        currentPos = new Vector2(hx, currentPos.y);
                        currentContour.Add(currentPos);
                        i += 1;
                        break;
                        
                    case 'V':
                        float vy = isRelative ? currentPos.y + nums[i] : nums[i] - offsetY;
                        currentPos = new Vector2(currentPos.x, vy);
                        currentContour.Add(currentPos);
                        i += 1;
                        break;
                        
                    case 'C': // 三次贝塞尔曲线
                        {
                            float c1x = isRelative ? currentPos.x + nums[i] : nums[i] - offsetX;
                            float c1y = isRelative ? currentPos.y + nums[i + 1] : nums[i + 1] - offsetY;
                            float c2x = isRelative ? currentPos.x + nums[i + 2] : nums[i + 2] - offsetX;
                            float c2y = isRelative ? currentPos.y + nums[i + 3] : nums[i + 3] - offsetY;
                            float cex = isRelative ? currentPos.x + nums[i + 4] : nums[i + 4] - offsetX;
                            float cey = isRelative ? currentPos.y + nums[i + 5] : nums[i + 5] - offsetY;
                            
                            var cp1 = new Vector2(c1x, c1y);
                            var cp2 = new Vector2(c2x, c2y);
                            var endPt = new Vector2(cex, cey);
                            
                            // 采样贝塞尔曲线
                            for (float t = 0.05f; t <= 1f; t += 0.05f)
                            {
                                var pt = CubicBezier(currentPos, cp1, cp2, endPt, t);
                                currentContour.Add(pt);
                            }
                            
                            lastControlPoint = cp2;
                            currentPos = endPt;
                            i += 6;
                        }
                        break;
                        
                    case 'S': // 平滑三次贝塞尔
                        {
                            var scp1 = currentPos * 2 - lastControlPoint;
                            float s2x = isRelative ? currentPos.x + nums[i] : nums[i] - offsetX;
                            float s2y = isRelative ? currentPos.y + nums[i + 1] : nums[i + 1] - offsetY;
                            float sex = isRelative ? currentPos.x + nums[i + 2] : nums[i + 2] - offsetX;
                            float sey = isRelative ? currentPos.y + nums[i + 3] : nums[i + 3] - offsetY;
                            var scp2 = new Vector2(s2x, s2y);
                            var sEnd = new Vector2(sex, sey);
                            
                            for (float t = 0.05f; t <= 1f; t += 0.05f)
                            {
                                var pt = CubicBezier(currentPos, scp1, scp2, sEnd, t);
                                currentContour.Add(pt);
                            }
                            
                            lastControlPoint = scp2;
                            currentPos = sEnd;
                            i += 4;
                        }
                        break;
                        
                    case 'Q': // 二次贝塞尔曲线
                        {
                            float qcx = isRelative ? currentPos.x + nums[i] : nums[i] - offsetX;
                            float qcy = isRelative ? currentPos.y + nums[i + 1] : nums[i + 1] - offsetY;
                            float qex = isRelative ? currentPos.x + nums[i + 2] : nums[i + 2] - offsetX;
                            float qey = isRelative ? currentPos.y + nums[i + 3] : nums[i + 3] - offsetY;
                            var qcp = new Vector2(qcx, qcy);
                            var qEnd = new Vector2(qex, qey);
                            
                            for (float t = 0.05f; t <= 1f; t += 0.05f)
                            {
                                var pt = QuadraticBezier(currentPos, qcp, qEnd, t);
                                currentContour.Add(pt);
                            }
                            
                            lastControlPoint = qcp;
                            currentPos = qEnd;
                            i += 4;
                        }
                        break;
                        
                    case 'T': // 平滑二次贝塞尔
                        {
                            var tcp = currentPos * 2 - lastControlPoint;
                            float tex = isRelative ? currentPos.x + nums[i] : nums[i] - offsetX;
                            float tey = isRelative ? currentPos.y + nums[i + 1] : nums[i + 1] - offsetY;
                            var tEnd = new Vector2(tex, tey);
                            
                            for (float t = 0.05f; t <= 1f; t += 0.05f)
                            {
                                var pt = QuadraticBezier(currentPos, tcp, tEnd, t);
                                currentContour.Add(pt);
                            }
                            
                            lastControlPoint = tcp;
                            currentPos = tEnd;
                            i += 2;
                        }
                        break;
                        
                    case 'A': // 椭圆弧（简化处理：用线段近似）
                        {
                            if (nums.Count >= 7)
                            {
                                float aex = isRelative ? currentPos.x + nums[i + 5] : nums[i + 5] - offsetX;
                                float aey = isRelative ? currentPos.y + nums[i + 6] : nums[i + 6] - offsetY;
                                var aEnd = new Vector2(aex, aey);
                                
                                // 简化：用直线连接
                                currentContour.Add(aEnd);
                                currentPos = aEnd;
                            }
                            i += 7;
                        }
                        break;
                        
                    case 'Z':
                        if (currentContour.Count > 0 && Vector2.Distance(currentPos, startPos) > 0.1f)
                        {
                            currentContour.Add(startPos);
                        }
                        currentPos = startPos;
                        goto nextCommand;
                        
                    default:
                        i = nums.Count;
                        break;
                }
            }
            nextCommand:;
        }
        
        if (currentContour.Count > 0)
        {
            contours.Add(currentContour);
        }
        
        return contours;
    }
    
    /// <summary>
    /// 三次贝塞尔曲线插值
    /// </summary>
    private Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;
        
        return uuu * p0 + 3 * uu * t * p1 + 3 * u * tt * p2 + ttt * p3;
    }
    
    /// <summary>
    /// 二次贝塞尔曲线插值
    /// </summary>
    private Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        float u = 1 - t;
        return u * u * p0 + 2 * u * t * p1 + t * t * p2;
    }
    
    /// <summary>
    /// 确保输出目录存在，返回验证后的完整路径
    /// </summary>
    private bool EnsureOutputFolderExists()
    {
        // 验证路径格式
        if (string.IsNullOrEmpty(svgImageOutputFolder))
        {
            svgImageOutputFolder = "Assets/Textures/SVG_Images";
            Debug.LogWarning("图片目录为空，已重置为默认值: " + svgImageOutputFolder);
        }
        
        // 确保以 Assets 开头
        if (!svgImageOutputFolder.StartsWith("Assets"))
        {
            svgImageOutputFolder = "Assets/" + svgImageOutputFolder.TrimStart('/');
            Debug.LogWarning("图片目录必须在 Assets 下，已修正为: " + svgImageOutputFolder);
        }
        
        // 移除末尾斜杠
        svgImageOutputFolder = svgImageOutputFolder.TrimEnd('/');
        
        // 验证完整路径是否可用
        string fullFolderPath = Application.dataPath + svgImageOutputFolder.Substring(6);
        Debug.Log($"[EnsureOutputFolderExists] 图片目录: {svgImageOutputFolder}");
        Debug.Log($"[EnsureOutputFolderExists] 完整路径: {fullFolderPath}");
        
        // 创建目录
        if (!AssetDatabase.IsValidFolder(svgImageOutputFolder))
        {
            string[] folders = svgImageOutputFolder.Split('/');
            string currentPath = folders[0];
            for (int i = 1; i < folders.Length; i++)
            {
                string nextPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    Debug.Log($"创建目录: {nextPath}");
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath = nextPath;
            }
        }
        
        return true;
    }
    
    #endregion
}

public class SvgPositionCodeInputWindow : EditorWindow
{
    private string svgCode = "";
    private Vector2 scrollPos;
    private Moshi_SvgImport parentWindow;
    
    public static void ShowWindow(Moshi_SvgImport parent)
    {
        var w = GetWindow<SvgPositionCodeInputWindow>("粘贴SVG代码");
        w.minSize = new Vector2(500, 400);
        w.parentWindow = parent;
    }
    
    private void OnGUI()
    {
        EditorGUILayout.LabelField("请粘贴SVG代码:", EditorStyles.boldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
        svgCode = EditorGUILayout.TextArea(svgCode, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("清空", GUILayout.Height(30))) svgCode = "";
        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(svgCode) || !svgCode.Contains("<svg"));
        if (GUILayout.Button("解析并导入", GUILayout.Height(30))) { parentWindow?.LoadSvgFromCode(svgCode); Close(); }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(svgCode) && !svgCode.Contains("<svg"))
            EditorGUILayout.HelpBox("请输入有效的SVG代码", MessageType.Warning);
    }
}
