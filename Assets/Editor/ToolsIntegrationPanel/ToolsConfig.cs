using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "MyToolsConfig", menuName = "Tools/Create Tools Config", order = 0)]
public class ToolsConfig : ScriptableObject
{
    public const string DefaultAssetPath = "Assets/Editor/ToolsIntegrationPanel/MyToolsConfig.asset";

    [SerializeField]
    private List<ToolItem> allTools = new List<ToolItem>();

    [Header("Quick Access UI")]
    [SerializeField, Min(1f)]
    private float quickAccessButtonMinWidth = 110f;

    [SerializeField, Min(1f)]
    private float quickAccessButtonMinHeight = 40f;

    [SerializeField, Min(1f)]
    private float quickAccessButtonMaxWidth = 150f;

    [SerializeField, Min(1f)]
    private float quickAccessButtonMaxHeight = 80f;

    [SerializeField, Min(0f)]
    private float quickAccessButtonSpacing = 6f;

    [SerializeField, Min(0f)]
    private float quickAccessRowSpacing = 4f;

    [SerializeField, Min(0f)]
    private float quickAccessHorizontalPadding = 12f;

    [SerializeField, Range(1, 20)]
    private int quickAccessMaxColumns = 10;

    [Header("Window Bounds")]
    [SerializeField, Min(32f)]
    private float windowMinWidth = 420f;

    [SerializeField, Min(32f)]
    private float windowMinHeight = 320f;

    [SerializeField, Min(32f)]
    private float windowMaxWidth = 1600f;

    [SerializeField, Min(32f)]
    private float windowMaxHeight = 960f;

    [Header("Quick Access Section Style")]
    [SerializeField, Min(0f)]
    private float quickAccessSectionPadding = 4f;

    [SerializeField, Min(0f)]
    private float quickAccessSectionMargin = 2f;

    [Header("Quick Access Decorations")]
    [SerializeField]
    private bool showQuickAccessInfo = true;

    [Header("All Tools Section")]
    [SerializeField]
    private bool showFullToolsSection = true;

    public IReadOnlyList<ToolItem> AllTools => allTools;

    public float QuickAccessButtonMinWidth
    {
        get
        {
            float sanitizedMin = Mathf.Max(1f, quickAccessButtonMinWidth);
            float sanitizedMax = Mathf.Max(1f, quickAccessButtonMaxWidth);
            return Mathf.Min(sanitizedMin, sanitizedMax);
        }
    }

    public float QuickAccessButtonMinHeight
    {
        get
        {
            float sanitizedMin = Mathf.Max(1f, quickAccessButtonMinHeight);
            float sanitizedMax = Mathf.Max(1f, quickAccessButtonMaxHeight);
            return Mathf.Min(sanitizedMin, sanitizedMax);
        }
    }

    public float QuickAccessButtonMaxWidth
    {
        get
        {
            float sanitizedMax = Mathf.Max(1f, quickAccessButtonMaxWidth);
            float sanitizedMin = Mathf.Max(1f, quickAccessButtonMinWidth);
            return Mathf.Max(sanitizedMax, sanitizedMin);
        }
    }

    public float QuickAccessButtonMaxHeight
    {
        get
        {
            float sanitizedMax = Mathf.Max(1f, quickAccessButtonMaxHeight);
            float sanitizedMin = Mathf.Max(1f, quickAccessButtonMinHeight);
            return Mathf.Max(sanitizedMax, sanitizedMin);
        }
    }

    public float QuickAccessButtonSpacing => Mathf.Max(0f, quickAccessButtonSpacing);

    public float QuickAccessRowSpacing => Mathf.Max(0f, quickAccessRowSpacing);

    public float QuickAccessHorizontalPadding => Mathf.Max(0f, quickAccessHorizontalPadding);

    public int QuickAccessMaxColumns => Mathf.Clamp(quickAccessMaxColumns, 1, 20);

    public float WindowMinWidth => Mathf.Clamp(windowMinWidth, 32f, Mathf.Max(32f, windowMaxWidth));

    public float WindowMinHeight => Mathf.Clamp(windowMinHeight, 32f, Mathf.Max(32f, windowMaxHeight));

    public float WindowMaxWidth => Mathf.Max(32f, Mathf.Max(windowMinWidth, windowMaxWidth));

    public float WindowMaxHeight => Mathf.Max(32f, Mathf.Max(windowMinHeight, windowMaxHeight));

    public float QuickAccessSectionPadding => Mathf.Max(0f, quickAccessSectionPadding);

    public float QuickAccessSectionMargin => Mathf.Max(0f, quickAccessSectionMargin);

    public bool ShowQuickAccessInfo => showQuickAccessInfo;

    public bool ShowFullToolsSection => showFullToolsSection;

    public IEnumerable<ToolItem> GetQuickAccessTools()
    {
        return allTools.Where(tool => tool != null && tool.showInQuickAccess);
    }

    public IEnumerable<string> GetCategoriesInOrder()
    {
        HashSet<string> encountered = new HashSet<string>();
        foreach (ToolItem tool in allTools)
        {
            string category = GetNormalizedCategory(tool);
            if (string.IsNullOrEmpty(category))
            {
                continue;
            }

            if (encountered.Add(category))
            {
                yield return category;
            }
        }
    }

    public IEnumerable<ToolItem> GetToolsByCategory(string category)
    {
        string normalized = string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim();
        return allTools.Where(tool => tool != null && GetNormalizedCategory(tool) == normalized);
    }

    public void ReplaceAllTools(IEnumerable<ToolItem> tools)
    {
        allTools.Clear();
        if (tools == null)
        {
            return;
        }

        foreach (ToolItem tool in tools)
        {
            if (tool != null)
            {
                allTools.Add(tool);
            }
        }
    }

    public void AddTool(ToolItem tool)
    {
        if (tool == null)
        {
            return;
        }

        allTools.Add(tool);
    }

    private static string GetNormalizedCategory(ToolItem tool)
    {
        if (tool == null || string.IsNullOrWhiteSpace(tool.category))
        {
            return string.Empty;
        }

        return tool.category.Trim();
    }
}
