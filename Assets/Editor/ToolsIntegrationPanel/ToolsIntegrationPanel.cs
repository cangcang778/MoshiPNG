#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ToolAttribute : Attribute
{
    public ToolAttribute(string bindingKey, string toolName, string toolDescription, string category)
    {
        BindingKey = bindingKey;
        ToolName = toolName;
        ToolDescription = toolDescription;
        Category = category;
    }

    public string BindingKey { get; }
    public string ToolName { get; }
    public string ToolDescription { get; }
    public string Category { get; }
    public bool ShowInQuickAccess { get; set; } = true;
    public int Order { get; set; }
}

public sealed class ToolDefinition
{
    public ToolDefinition(
        string bindingKey,
        string toolName,
        string toolDescription,
        string category,
        bool showInQuickAccess,
        int order,
        Action executeAction)
    {
        BindingKey = bindingKey;
        ToolName = toolName;
        ToolDescription = toolDescription;
        Category = category;
        ShowInQuickAccess = showInQuickAccess;
        Order = order;
        ExecuteAction = executeAction;
    }

    public string BindingKey { get; }
    public string ToolName { get; }
    public string ToolDescription { get; }
    public string Category { get; }
    public bool ShowInQuickAccess { get; }
    public int Order { get; }
    public Action ExecuteAction { get; }

    public ToolItem CreateToolItem()
    {
        return new ToolItem
        {
            toolName = ToolName,
            toolDescription = ToolDescription,
            category = Category,
            showInQuickAccess = ShowInQuickAccess,
            bindingKey = BindingKey
        };
    }
}

public static class ToolBindingCatalog
{
    public const string ToolScriptsRoot = "Assets/Editor/ToolsIntegrationPanel/AllToolsResources";

    private static readonly string ToolScriptsRootNormalized = NormalizeUnityPath(ToolScriptsRoot);
    private static readonly Dictionary<Type, string> TypePathLookup = new Dictionary<Type, string>();
    private static readonly Dictionary<string, string> TypeNameFallbackLookup = new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly Regex ClassNameRegex = new Regex(@"\bclass\s+([A-Za-z_]\w*)", RegexOptions.Compiled);
    private static bool typePathLookupBuilt;

    private static List<ToolDefinition> cachedDefinitions;
    private static IReadOnlyDictionary<string, Action> cachedActionLookup;

    public static IReadOnlyList<ToolDefinition> Definitions
    {
        get
        {
            if (cachedDefinitions == null)
            {
                cachedDefinitions = BuildDefinitions();
            }

            return cachedDefinitions;
        }
    }

    public static IReadOnlyDictionary<string, Action> ActionLookup
    {
        get
        {
            if (cachedActionLookup == null)
            {
                cachedActionLookup = BuildActionLookup();
            }

            return cachedActionLookup;
        }
    }

    public static List<ToolItem> CreateToolItemsSnapshot()
    {
        List<ToolItem> items = new List<ToolItem>(Definitions.Count);
        foreach (ToolDefinition definition in Definitions)
        {
            items.Add(definition.CreateToolItem());
        }

        return items;
    }

    public static void InvalidateCache()
    {
        cachedDefinitions = null;
        cachedActionLookup = null;
        TypePathLookup.Clear();
        TypeNameFallbackLookup.Clear();
        typePathLookupBuilt = false;
    }

    private static List<ToolDefinition> BuildDefinitions()
    {
        HashSet<string> bindingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<ToolDefinition> definitions = new List<ToolDefinition>();
        foreach (ToolDefinition definition in BuildAttributeDefinitions(bindingKeys))
        {
            definitions.Add(definition);
        }

        foreach (ToolDefinition definition in BuildMenuItemDefinitions(bindingKeys))
        {
            definitions.Add(definition);
        }

        definitions.Sort((left, right) =>
        {
            int orderCompare = left.Order.CompareTo(right.Order);
            if (orderCompare != 0)
            {
                return orderCompare;
            }

            return string.CompareOrdinal(left.ToolName, right.ToolName);
        });

        return definitions;
    }

    private static IEnumerable<ToolDefinition> BuildAttributeDefinitions(ISet<string> bindingKeys)
    {
        List<ToolDefinition> results = new List<ToolDefinition>();
        foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<ToolAttribute>())
        {
            ToolAttribute attribute = method.GetCustomAttribute<ToolAttribute>();
            if (attribute == null)
            {
                continue;
            }

            if (!IsMethodUnderAllowedFolder(method))
            {
                continue;
            }

            if (!IsValidEntryPoint(method))
            {
                Debug.LogWarning($"[Tools Integration] 已忽略 {method.DeclaringType?.FullName}.{method.Name} —— Tool 入口必须是无参静态方法。");
                continue;
            }

            Action action = null;
            try
            {
                action = (Action)Delegate.CreateDelegate(typeof(Action), method);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Tools Integration] 无法为 {attribute.BindingKey} 创建委托：{e.Message}");
            }

            if (string.IsNullOrEmpty(attribute.BindingKey))
            {
                continue;
            }

            if (bindingKeys.Contains(attribute.BindingKey))
            {
                continue;
            }

            results.Add(new ToolDefinition(
                attribute.BindingKey,
                attribute.ToolName,
                attribute.ToolDescription,
                attribute.Category,
                attribute.ShowInQuickAccess,
                attribute.Order,
                action));
            bindingKeys.Add(attribute.BindingKey);
        }

        return results;
    }

    private static IEnumerable<ToolDefinition> BuildMenuItemDefinitions(ISet<string> bindingKeys)
    {
        List<ToolDefinition> results = new List<ToolDefinition>();
        foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<MenuItem>())
        {
            if (!IsMethodUnderAllowedFolder(method))
            {
                continue;
            }

            if (!IsValidEntryPoint(method))
            {
                continue;
            }

            if (method.GetCustomAttribute<ToolAttribute>() != null)
            {
                continue;
            }

            Type declaringType = method.DeclaringType;
            if (declaringType != null && declaringType.IsAbstract && declaringType.IsSealed)
            {
                continue;
            }

            MenuItem[] menuAttributes = method.GetCustomAttributes<MenuItem>().ToArray();
            if (menuAttributes == null || menuAttributes.Length == 0)
            {
                continue;
            }

            foreach (MenuItem menuAttribute in menuAttributes)
            {
                if (menuAttribute == null || menuAttribute.validate)
                {
                    continue;
                }

                string menuPath = menuAttribute.menuItem;
                string bindingKey = GenerateBindingKeyFromMenuPath(menuPath, method, bindingKeys);
                if (string.IsNullOrEmpty(bindingKey))
                {
                    continue;
                }

                Action action = null;
                try
                {
                    action = (Action)Delegate.CreateDelegate(typeof(Action), method);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[Tools Integration] 无法为自动菜单 {menuPath} 创建委托：{exception.Message}");
                }

                if (action == null)
                {
                    continue;
                }

                string toolName = ExtractToolNameFromMenuPath(menuPath);
                string category = ExtractCategoryFromMenuPath(menuPath);
                int order = menuAttribute.priority >= 0 ? menuAttribute.priority : 1000;

                results.Add(new ToolDefinition(
                    bindingKey,
                    toolName,
                    $"来自菜单项 \"{menuPath}\" 的自动注册工具。",
                    category,
                    true,
                    order,
                    action));

                bindingKeys.Add(bindingKey);
                break;
            }
        }

        return results;
    }

    private static string GenerateBindingKeyFromMenuPath(string menuPath, MethodInfo method, ISet<string> bindingKeys)
    {
        string candidate = SanitizeBindingKey(menuPath);
        if (string.IsNullOrEmpty(candidate))
        {
            string fallbackSource = string.Concat(method.DeclaringType?.Name ?? string.Empty, method.Name ?? string.Empty);
            candidate = SanitizeBindingKey(fallbackSource);
        }

        return EnsureUniqueBindingKey(candidate, bindingKeys);
    }

    private static string SanitizeBindingKey(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return null;
        }

        StringBuilder builder = new StringBuilder(source.Length);
        foreach (char character in source)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.Length > 0 ? builder.ToString() : null;
    }

    private static string EnsureUniqueBindingKey(string candidate, ISet<string> bindingKeys)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            return null;
        }

        string resolved = candidate;
        int suffix = 1;
        while (bindingKeys.Contains(resolved))
        {
            resolved = candidate + suffix;
            suffix++;
        }

        return resolved;
    }

    private static string ExtractToolNameFromMenuPath(string menuPath)
    {
        if (string.IsNullOrWhiteSpace(menuPath))
        {
            return "未命名工具";
        }

        string[] segments = menuPath.Split('/');
        return segments.Length == 0 ? menuPath : segments[segments.Length - 1].Trim();
    }

    private static string ExtractCategoryFromMenuPath(string menuPath)
    {
        if (string.IsNullOrWhiteSpace(menuPath))
        {
            return "默认分类";
        }

        string[] segments = menuPath.Split('/');
        if (segments.Length <= 1)
        {
            return "默认分类";
        }

        IEnumerable<string> categorySegments = segments
            .Take(segments.Length - 1)
            .Where(segment => !string.Equals(segment, "Tools", StringComparison.OrdinalIgnoreCase))
            .Select(segment => segment.Trim())
            .Where(segment => !string.IsNullOrEmpty(segment));

        string category = string.Join(" / ", categorySegments);
        return string.IsNullOrEmpty(category) ? "默认分类" : category;
    }

    private static bool IsMethodUnderAllowedFolder(MethodInfo method)
    {
        Type declaringType = method.DeclaringType;
        string path = GetScriptAssetPath(declaringType);
        return !string.IsNullOrEmpty(path) &&
               path.StartsWith(ToolScriptsRootNormalized, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetScriptAssetPath(Type type)
    {
        if (type == null)
        {
            return null;
        }

        if (!typePathLookupBuilt)
        {
            BuildTypePathLookup();
        }

        if (TypePathLookup.TryGetValue(type, out string path))
        {
            return path;
        }

        if (type != null && TypeNameFallbackLookup.TryGetValue(type.Name, out string fallbackPath))
        {
            return fallbackPath;
        }

        return null;
    }

    private static void BuildTypePathLookup()
    {
        typePathLookupBuilt = true;
        TypePathLookup.Clear();
        TypeNameFallbackLookup.Clear();

        if (!AssetDatabase.IsValidFolder(ToolScriptsRoot))
        {
            return;
        }

        string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { ToolScriptsRoot });
        foreach (string guid in scriptGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                continue;
            }

            MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            if (monoScript == null)
            {
                continue;
            }

            string normalizedPath = NormalizeUnityPath(assetPath);
            Type scriptClass = monoScript.GetClass();
            if (scriptClass != null)
            {
                TypePathLookup[scriptClass] = normalizedPath;
            }

            CacheClassNamesForFallback(normalizedPath, monoScript.text);
        }
    }

    private static void CacheClassNamesForFallback(string normalizedPath, string scriptText)
    {
        if (string.IsNullOrEmpty(normalizedPath) || string.IsNullOrEmpty(scriptText))
        {
            return;
        }

        foreach (string className in ExtractClassNames(scriptText))
        {
            if (!TypeNameFallbackLookup.ContainsKey(className))
            {
                TypeNameFallbackLookup[className] = normalizedPath;
            }
        }
    }

    private static IEnumerable<string> ExtractClassNames(string scriptText)
    {
        if (string.IsNullOrEmpty(scriptText))
        {
            yield break;
        }

        MatchCollection matches = ClassNameRegex.Matches(scriptText);
        foreach (Match match in matches)
        {
            if (match.Success && match.Groups.Count > 1)
            {
                string name = match.Groups[1].Value;
                if (!string.IsNullOrEmpty(name))
                {
                    yield return name;
                }
            }
        }
    }

    private static string NormalizeUnityPath(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.Replace("\\", "/");
    }

    private static bool IsValidEntryPoint(MethodInfo method)
    {
        return method.IsStatic && method.ReturnType == typeof(void) && method.GetParameters().Length == 0;
    }

    private static IReadOnlyDictionary<string, Action> BuildActionLookup()
    {
        Dictionary<string, Action> lookup = new Dictionary<string, Action>();
        foreach (ToolDefinition definition in Definitions)
        {
            if (string.IsNullOrEmpty(definition.BindingKey) || definition.ExecuteAction == null)
            {
                continue;
            }

            lookup[definition.BindingKey] = definition.ExecuteAction;
        }

        return lookup;
    }
}

public class ToolsIntegrationPanel : EditorWindow
{
    private ToolsConfig activeConfig;
    private readonly List<ToolItem> quickTools = new List<ToolItem>();
    private readonly Dictionary<string, List<ToolItem>> groupedTools = new Dictionary<string, List<ToolItem>>();
    private readonly List<string> orderedCategories = new List<string>();

    private Vector2 scrollPosition;

    private GUIStyle headerStyle;
    private GUIStyle categoryStyle;
    private GUIStyle toolButtonStyle;
    private GUIStyle quickButtonStyle;
    private GUIStyle warningStyle;
    private GUIStyle quickSectionStyle;
    private GUIStyle toolCardStyle;
    private GUIStyle footerStyle;
    private GUIStyle toolbarPathStyle;
    private GUIStyle toolbarButtonStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle descriptionStyle;
    private GUIStyle foldoutStyle;

    private bool showToolList;

    private static readonly Color32 PanelBackgroundColor = new Color32(56, 56, 56, 255);
    private static readonly Color32 CardBackgroundColor = new Color32(50, 50, 50, 255);
    private static readonly Color32 QuickButtonColor = new Color32(64, 64, 64, 255);
    private static readonly Color32 ToolButtonColor = new Color32(64, 64, 64, 255);
    private static readonly Color32 PrimaryAccentColor = new Color32(255, 255, 255, 255);
    private static readonly Color32 SecondaryAccentColor = new Color32(255, 255, 255, 255);
    private static readonly Color32 SoftTextColor = new Color32(255, 255, 255, 255);
    private static readonly Color32 WarningTextColor = new Color32(255, 140, 140, 255);

    private const float QuickAccessButtonPreferredWidth = 150f;
    private const float QuickAccessButtonMinWidth = 110f;
    private const float QuickAccessDefaultMaxWidth = 150f;
    private const float QuickAccessDefaultButtonSpacing = 6f;
    private const float QuickAccessDefaultRowSpacing = 4f;
    private const float QuickAccessDefaultHorizontalPadding = 32f;
    private const float QuickAccessDefaultButtonHeight = 80f;
    private const int QuickAccessDefaultMaxColumns = 10;

    private static IReadOnlyDictionary<string, Action> BuiltinActionLookup => ToolBindingCatalog.ActionLookup;

    private static readonly Dictionary<Color32, Texture2D> SolidColorTextureCache = new Dictionary<Color32, Texture2D>();

    [MenuItem("Tools/MoshiResourceTools", priority = 0)]
    public static void ShowWindow()
    {
        ToolsIntegrationPanel window = GetWindow<ToolsIntegrationPanel>("Moshi 工具箱");
        window.RefreshFromConfig(false);
        window.Show();
    }

    public static void RequestConfigRefresh()
    {
        ToolsIntegrationPanel[] windows = Resources.FindObjectsOfTypeAll<ToolsIntegrationPanel>();
        foreach (ToolsIntegrationPanel window in windows)
        {
            window.RefreshFromConfig(true);
        }
    }

    private void OnEnable()
    {
        RefreshFromConfig(false);
    }

    private void OnFocus()
    {
        RefreshFromConfig(false);
    }

    private void RefreshFromConfig(bool forceReloadAsset)
    {
        if (forceReloadAsset || activeConfig == null)
        {
            activeConfig = AssetDatabase.LoadAssetAtPath<ToolsConfig>(ToolsConfig.DefaultAssetPath);
        }

        UpdateWindowBounds();
        BindToolActions();
        RebuildCaches();
        Repaint();
    }
    
    private void UpdateWindowBounds()
    {
        if (activeConfig == null)
        {
            minSize = new Vector2(150, 150);
            maxSize = new Vector2(4000, 4000);
            return;
        }
        
        float minW = activeConfig.WindowMinWidth;
        float minH = activeConfig.WindowMinHeight;
        float maxW = activeConfig.WindowMaxWidth;
        float maxH = activeConfig.WindowMaxHeight;
        
        minSize = new Vector2(minW, minH);
        maxSize = new Vector2(maxW, maxH);
    }

    private void BindToolActions()
    {
        if (activeConfig == null)
        {
            return;
        }

        foreach (ToolItem tool in activeConfig.AllTools)
        {
            if (tool == null)
            {
                continue;
            }

            tool.onExecute = null;
            if (!string.IsNullOrEmpty(tool.bindingKey) && BuiltinActionLookup.TryGetValue(tool.bindingKey, out Action action))
            {
                tool.onExecute = action;
            }
        }
    }

    private void RebuildCaches()
    {
        quickTools.Clear();
        groupedTools.Clear();
        orderedCategories.Clear();

        if (activeConfig == null)
        {
            return;
        }

        foreach (ToolItem tool in activeConfig.AllTools)
        {
            if (tool == null)
            {
                continue;
            }

            if (tool.showInQuickAccess)
            {
                quickTools.Add(tool);
            }

            string category = string.IsNullOrWhiteSpace(tool.category) ? "未分类" : tool.category.Trim();
            if (!groupedTools.TryGetValue(category, out List<ToolItem> list))
            {
                list = new List<ToolItem>();
                groupedTools.Add(category, list);
                orderedCategories.Add(category);
            }

            list.Add(tool);
        }
    }

    private void OnGUI()
    {
        EnsureStyles();
        DrawToolbar();

        if (activeConfig == null)
        {
            DrawMissingConfigAlert();
            return;
        }

        DrawQuickAccessRow();
        DrawToolListSection();
        DrawFooter();
    }

    private void DrawToolListSection()
    {
        if (activeConfig == null)
        {
            return;
        }

        if (!ShouldShowFullToolsSection())
        {
            return;
        }

        int totalTools = activeConfig.AllTools?.Count ?? 0;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showToolList = EditorGUILayout.Foldout(showToolList, $"全部工具 ({totalTools})", true, foldoutStyle);
        if (showToolList)
        {
            EditorGUILayout.Space(4);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            foreach (string category in orderedCategories)
            {
                if (!groupedTools.TryGetValue(category, out List<ToolItem> tools) || tools.Count == 0)
                {
                    continue;
                }

                DrawCategoryHeader(category, tools.Count);
                foreach (ToolItem tool in tools)
                {
                    DrawToolButton(tool);
                }
            }
            EditorGUILayout.EndScrollView();
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label(activeConfig != null ? ToolsConfig.DefaultAssetPath : "配置未找到", toolbarPathStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("配置刷新", toolbarButtonStyle))
        {
            InitToolsConfig.SyncAndPruneToolsConfig();
        }

        if (GUILayout.Button("配置初始化", toolbarButtonStyle))
        {
            InitToolsConfig.CreateOrResetToolsConfig();
        }

        if (GUILayout.Button("重新加载", toolbarButtonStyle))
        {
            RefreshFromConfig(true);
        }

        if (activeConfig != null && GUILayout.Button("定位配置", toolbarButtonStyle))
        {
            EditorGUIUtility.PingObject(activeConfig);
        }
        
        if (GUILayout.Button("帮助文档", toolbarButtonStyle))
        {
            MoshiHelpButton.OpenToolHelp("Moshi工具箱");
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawMissingConfigAlert()
    {
        EditorGUILayout.HelpBox(
            $"未能在 {ToolsConfig.DefaultAssetPath} 找到 ToolsConfig 资源。\n\n请通过菜单 Tools/初始化工具配置 创建默认配置，或手动在该路径下放置 ScriptableObject。",
            MessageType.Warning);

        if (GUILayout.Button("立刻创建/刷新配置"))
        {
            InitToolsConfig.CreateOrResetToolsConfig();
        }
    }

    private void DrawQuickAccessRow()
    {
        if (quickTools.Count == 0)
        {
            return;
        }

        EditorGUILayout.BeginVertical(quickSectionStyle);
        bool showInfo = ShouldShowQuickAccessInfo();
        if (showInfo)
        {
            GUILayout.Label("🚀 快速入口", headerStyle);
            GUILayout.Label("常用工具集中管理，一键直达高频操作", subtitleStyle);
            EditorGUILayout.Space(6);
        }
        else
        {
            EditorGUILayout.Space(4);
        }

        int columns = CalculateQuickAccessColumns();
        float buttonWidth = CalculateQuickAccessButtonWidth(columns);
        float buttonHeightValue = GetConfiguredQuickAccessButtonHeight();
        float buttonSpacing = GetConfiguredQuickAccessButtonSpacing();
        float rowSpacing = GetConfiguredQuickAccessRowSpacing();
        int index = 0;
        GUILayoutOption buttonHeightOption = GUILayout.Height(buttonHeightValue);
        GUILayoutOption buttonWidthOption = GUILayout.Width(buttonWidth);
        Color previousContentColor = GUI.contentColor;
        GUI.contentColor = PrimaryAccentColor;
        while (index < quickTools.Count)
        {
            EditorGUILayout.BeginHorizontal();
            for (int col = 0; col < columns && index < quickTools.Count; col++)
            {
                if (col > 0)
                {
                    GUILayout.Space(buttonSpacing);
                }

                ToolItem tool = quickTools[index];
                string label = tool.toolName;
                if (GUILayout.Button(label, quickButtonStyle, buttonHeightOption, buttonWidthOption))
                {
                    ExecuteTool(tool);
                }
                index++;
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(rowSpacing);
        }

        GUI.contentColor = previousContentColor;
        if (showInfo)
        {
            GUILayout.Label("提示：在 ToolsConfig 中勾选“快速入口”即可出现于此。", descriptionStyle);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(showInfo ? 6f : 2f);
    }

    private int CalculateQuickAccessColumns()
    {
        float horizontalPadding = GetConfiguredQuickAccessHorizontalPadding();
        float availableWidth = Mathf.Max(1f, position.width - horizontalPadding);
        float configuredMinWidth = GetConfiguredQuickAccessMinWidth();
        float buttonSpacing = GetConfiguredQuickAccessButtonSpacing();
        
        // 使用最小宽度计算最大可容纳列数
        float normalizedMinWidth = configuredMinWidth + buttonSpacing;
        int maxPossibleColumns = Mathf.FloorToInt((availableWidth + buttonSpacing) / normalizedMinWidth);
        
        int configuredMaxColumns = GetConfiguredQuickAccessMaxColumns();
        int maxColumns = quickTools.Count > 0
            ? Mathf.Min(configuredMaxColumns, quickTools.Count)
            : configuredMaxColumns;
        
        int columns = Mathf.Clamp(maxPossibleColumns, 1, Mathf.Max(1, maxColumns));
        return columns;
    }

    private float CalculateQuickAccessButtonWidth(int columns)
    {
        if (columns <= 0)
        {
            return GetConfiguredQuickAccessMinWidth();
        }

        float availableWidth = Mathf.Max(1f, position.width - GetConfiguredQuickAccessHorizontalPadding());
        float buttonSpacing = GetConfiguredQuickAccessButtonSpacing();
        float totalSpacing = buttonSpacing * (columns - 1);
        float width = (availableWidth - totalSpacing) / columns;
        
        // 按钮宽度只受最小宽度限制，允许自动扩展填满可用空间
        float minWidth = GetConfiguredQuickAccessMinWidth();
        float maxWidth = GetConfiguredQuickAccessMaxWidth();
        
        // 如果 maxWidth >= minWidth，则限制在范围内；否则忽略 maxWidth 限制
        if (maxWidth >= minWidth)
        {
            return Mathf.Clamp(width, minWidth, maxWidth);
        }
        return Mathf.Max(width, minWidth);
    }

    private float GetConfiguredQuickAccessButtonSpacing()
    {
        if (activeConfig == null)
        {
            return QuickAccessDefaultButtonSpacing;
        }

        return Mathf.Max(0f, activeConfig.QuickAccessButtonSpacing);
    }

    private float GetConfiguredQuickAccessRowSpacing()
    {
        if (activeConfig == null)
        {
            return QuickAccessDefaultRowSpacing;
        }

        return Mathf.Max(0f, activeConfig.QuickAccessRowSpacing);
    }

    private bool ShouldShowQuickAccessInfo()
    {
        return activeConfig == null || activeConfig.ShowQuickAccessInfo;
    }

    private bool ShouldShowFullToolsSection()
    {
        return activeConfig == null || activeConfig.ShowFullToolsSection;
    }

    private float GetConfiguredQuickAccessHorizontalPadding()
    {
        if (activeConfig == null)
        {
            return QuickAccessDefaultHorizontalPadding;
        }

        return Mathf.Max(0f, activeConfig.QuickAccessHorizontalPadding);
    }

    private int GetConfiguredQuickAccessMaxColumns()
    {
        if (activeConfig == null)
        {
            return QuickAccessDefaultMaxColumns;
        }

        return Mathf.Max(1, activeConfig.QuickAccessMaxColumns);
    }

    private float GetConfiguredQuickAccessMaxWidth()
    {
        if (activeConfig == null)
        {
            return QuickAccessDefaultMaxWidth;
        }

        float minWidth = GetConfiguredQuickAccessMinWidth();
        return Mathf.Max(minWidth, activeConfig.QuickAccessButtonMaxWidth);
    }
    
    private float GetConfiguredQuickAccessMinWidth()
    {
        if (activeConfig == null)
        {
            return QuickAccessButtonMinWidth;
        }

        return Mathf.Max(20f, activeConfig.QuickAccessButtonMinWidth);
    }

    private float GetConfiguredQuickAccessButtonHeight()
    {
        if (activeConfig == null)
        {
            return QuickAccessDefaultButtonHeight;
        }

        return Mathf.Max(24f, activeConfig.QuickAccessButtonMaxHeight);
    }

    private void DrawCategoryHeader(string category, int toolCount)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"• {category}", categoryStyle);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"{toolCount} 项", subtitleStyle);
        EditorGUILayout.EndHorizontal();
        Rect lineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));
        EditorGUI.DrawRect(lineRect, new Color(1f, 1f, 1f, 0.05f));
        EditorGUILayout.Space(2);
    }

    private void DrawToolButton(ToolItem tool)
    {
        EditorGUILayout.BeginVertical(toolCardStyle);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button($"▶  {tool.toolName}", toolButtonStyle))
        {
            ExecuteTool(tool);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrWhiteSpace(tool.toolDescription))
        {
            EditorGUILayout.LabelField(tool.toolDescription, descriptionStyle);
        }

        if (string.IsNullOrEmpty(tool.bindingKey))
        {
            EditorGUILayout.LabelField("未设置 Binding Key", warningStyle);
        }
        else if (!BuiltinActionLookup.ContainsKey(tool.bindingKey))
        {
            EditorGUILayout.LabelField($"未匹配动作：{tool.bindingKey}", warningStyle);
        }
        else
        {
            EditorGUILayout.LabelField($"绑定：{tool.bindingKey}", subtitleStyle);
        }

        EditorGUILayout.EndVertical();
        Rect lastRect = GUILayoutUtility.GetLastRect();
        EditorGUI.DrawRect(new Rect(lastRect.x, lastRect.y, lastRect.width, 1f), new Color(1f, 1f, 1f, 0.03f));
        EditorGUILayout.Space(4);
    }

    private void DrawFooter()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("Tools Integration Panel · 配置驱动示例", footerStyle);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);
    }

    private void ExecuteTool(ToolItem tool)
    {
        if (tool == null)
        {
            return;
        }

        if (tool.onExecute == null)
        {
            EditorUtility.DisplayDialog("未绑定执行方法", $"工具 \"{tool.toolName}\" 尚未绑定到具体逻辑。\n\n请确认 Binding Key 是否填写正确。", "确定");
            return;
        }

        try
        {
            tool.onExecute.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"执行工具 {tool.toolName} 时发生异常: {e}");
        }
    }

    private void EnsureStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft
            };
            headerStyle.normal.textColor = PrimaryAccentColor;
        }

        if (categoryStyle == null)
        {
            categoryStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13
            };
            categoryStyle.normal.textColor = PrimaryAccentColor;
        }

        if (toolButtonStyle == null)
        {
            toolButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 32,
                padding = new RectOffset(10, 10, 4, 4)
            };
            Color toolButtonColor = ToolButtonColor;
            toolButtonStyle.normal.textColor = PrimaryAccentColor;
            toolButtonStyle.hover.textColor = PrimaryAccentColor;
            toolButtonStyle.active.textColor = PrimaryAccentColor;
            toolButtonStyle.normal.background = GetSolidTexture(toolButtonColor);
            toolButtonStyle.hover.background = GetSolidTexture(toolButtonColor + new Color(0.08f, 0.08f, 0.08f, 0));
            toolButtonStyle.active.background = GetSolidTexture(toolButtonColor - new Color(0.05f, 0.05f, 0.05f, 0));
        }

        if (quickButtonStyle == null)
        {
            quickButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                wordWrap = true,
                padding = new RectOffset(8, 8, 6, 6)
            };
            Color quickColor = QuickButtonColor;
            quickButtonStyle.normal.textColor = PrimaryAccentColor;
            quickButtonStyle.hover.textColor = PrimaryAccentColor;
            quickButtonStyle.active.textColor = PrimaryAccentColor;
            quickButtonStyle.normal.background = GetSolidTexture(quickColor);
            quickButtonStyle.hover.background = GetSolidTexture(quickColor + new Color(0.08f, 0.06f, 0.04f, 0));
            quickButtonStyle.active.background = GetSolidTexture(quickColor - new Color(0.05f, 0.04f, 0.03f, 0));
        }

        if (warningStyle == null)
        {
            warningStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = WarningTextColor }
            };
        }

        if (quickSectionStyle == null)
        {
            quickSectionStyle = new GUIStyle("HelpBox");
            quickSectionStyle.normal.background = GetSolidTexture(PanelBackgroundColor);
        }
        
        // 从配置动态更新 padding 和 margin
        int sectionPadding = Mathf.RoundToInt(activeConfig?.QuickAccessSectionPadding ?? 4f);
        int sectionMargin = Mathf.RoundToInt(activeConfig?.QuickAccessSectionMargin ?? 2f);
        quickSectionStyle.padding = new RectOffset(sectionPadding, sectionPadding, sectionPadding, sectionPadding);
        quickSectionStyle.margin = new RectOffset(sectionMargin, sectionMargin, sectionMargin, sectionMargin + 2);

        if (toolCardStyle == null)
        {
            toolCardStyle = new GUIStyle("HelpBox")
            {
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(0, 0, 4, 6)
            };
            toolCardStyle.normal.background = GetSolidTexture(CardBackgroundColor);
        }

        if (footerStyle == null)
        {
            footerStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = SoftTextColor }
            };
        }

        if (toolbarPathStyle == null)
        {
            toolbarPathStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = PrimaryAccentColor }
            };
        }

        if (toolbarButtonStyle == null)
        {
            toolbarButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
            toolbarButtonStyle.normal.textColor = PrimaryAccentColor;
            toolbarButtonStyle.hover.textColor = PrimaryAccentColor;
            toolbarButtonStyle.active.textColor = PrimaryAccentColor;
            toolbarButtonStyle.focused.textColor = PrimaryAccentColor;
        }

        if (subtitleStyle == null)
        {
            subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = PrimaryAccentColor }
            };
        }

        if (descriptionStyle == null)
        {
            descriptionStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                normal = { textColor = PrimaryAccentColor }
            };
        }

        if (foldoutStyle == null)
        {
            foldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };
            foldoutStyle.normal.textColor = PrimaryAccentColor;
            foldoutStyle.onNormal.textColor = PrimaryAccentColor;
            foldoutStyle.active.textColor = PrimaryAccentColor;
            foldoutStyle.onActive.textColor = PrimaryAccentColor;
        }

    }

    private static Texture2D GetSolidTexture(Color color)
    {
        Color32 key = (Color32)color;
        if (!SolidColorTextureCache.TryGetValue(key, out Texture2D texture) || texture == null)
        {
            texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            SolidColorTextureCache[key] = texture;
        }

        return texture;
    }

    private static void OpenDocumentationFolder()
    {
        const string docPath = "Assets/Editor/ToolsIntegrationPanel/Documentation";
        var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(docPath);
        if (folder != null)
        {
            EditorGUIUtility.PingObject(folder);
            Selection.activeObject = folder;
            EditorUtility.FocusProjectWindow();
        }
        else
        {
            EditorUtility.DisplayDialog("帮助文档", $"未找到文档目录：\n{docPath}", "确定");
        }
    }

}
#endif
