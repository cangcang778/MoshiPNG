using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 工具名称优化器 - 自动将工具名称缩短到4字以内
/// </summary>
public static class ToolNameOptimizer
{
    // 手动优化映射（仅用于自动处理效果不理想的情况）
    private static readonly Dictionary<string, string> ManualOverrides = new Dictionary<string, string>
    {
        { "残留文件清理", "残留清理" },
        { "图片转面片", "图转面片" },
        { "SVG位置导入", "SVG导入" },
        { "路径动画烘焙", "路径烘焙" },
        { "Timeline快捷键", "时线快捷" },
        { "PSD位置导入", "PSD导入" },
    };

    // 可移除的后缀词（按优先级排序）
    private static readonly string[] RemovableSuffixes = { "工具", "助手", "器", "导入", "导出", "修改", "清理", "统计" };

    // 可移除的前缀词（按优先级排序）
    private static readonly string[] RemovablePrefixes = { "资源", "文件", "批量", "物体", "粒子" };

    /// <summary>
    /// 获取工具的短名称（4字以内）
    /// </summary>
    public static string GetShortName(string fullName)
    {
        if (string.IsNullOrEmpty(fullName))
            return fullName;

        // 1. 优先使用手动映射
        if (ManualOverrides.TryGetValue(fullName, out string manual))
            return manual;

        // 2. 已经4字以内，直接返回
        if (fullName.Length <= 4)
            return fullName;

        // 3. 自动智能缩短
        return AutoShorten(fullName);
    }

    private static string AutoShorten(string name)
    {
        string result = name;

        // 尝试移除后缀
        foreach (var suffix in RemovableSuffixes)
        {
            if (result.EndsWith(suffix) && result.Length > 4)
            {
                string trimmed = result.Substring(0, result.Length - suffix.Length);
                if (trimmed.Length >= 2)
                {
                    result = trimmed;
                    break;
                }
            }
        }

        // 如果还是超过4字，尝试移除前缀
        if (result.Length > 4)
        {
            foreach (var prefix in RemovablePrefixes)
            {
                if (result.StartsWith(prefix))
                {
                    string trimmed = result.Substring(prefix.Length);
                    if (trimmed.Length >= 2)
                    {
                        result = trimmed;
                        break;
                    }
                }
            }
        }

        // 如果还是超过4字，截断
        if (result.Length > 4)
            result = result.Substring(0, 4);

        return result;
    }
}

[Serializable]
public class ToolItem
{
    [Tooltip("在面板中显示的名称（双击可编辑）")]
    public string toolName;

    [TextArea(2, 4)]
    [Tooltip("用于解释工具作用的描述")]
    public string toolDescription;

    [Tooltip("工具所属分类，用于列表分组")]
    public string category;

    [Tooltip("是否在快速入口区域展示")]
    public bool showInQuickAccess;

    [Tooltip("用于绑定到具体执行逻辑的唯一 Key")]
    public string bindingKey;

    [NonSerialized]
    public Action onExecute;

    public void Invoke()
    {
        if (onExecute != null)
        {
            onExecute.Invoke();
        }
        else
        {
            Debug.LogWarning($"工具 \"{toolName}\" 尚未绑定执行方法。");
        }
    }
}
