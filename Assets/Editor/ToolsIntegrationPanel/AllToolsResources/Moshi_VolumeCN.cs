using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Volume 后处理中文编辑窗口
/// 选中场景中的 Volume 或 VolumeProfile 资产后，显示中文界面
/// </summary>
public class Moshi_VolumeCN : EditorWindow
{
    #region 中英对照

    private static readonly Dictionary<string, string> ComponentNameCN = new Dictionary<string, string>
    {
        { "Bloom", "辉光" },
        { "Tonemapping", "色调映射" },
        { "Color Adjustments", "颜色调整" },
        { "White Balance", "白平衡" },
        { "Split Toning", "分离色调" },
        { "Channel Mixer", "通道混合器" },
        { "Shadows Midtones Highlights", "阴影/中间调/高光" },
        { "Lift Gamma Gain", "提升/伽马/增益" },
        { "Depth Of Field", "景深" },
        { "Motion Blur", "运动模糊" },
        { "Vignette", "暗角" },
        { "Film Grain", "胶片颗粒" },
        { "Chromatic Aberration", "色差" },
        { "Lens Distortion", "镜头畸变" },
        { "Panini Projection", "帕尼尼投影" },
        { "Bloom (URP)", "辉光" },
        { "Tonemapping (URP)", "色调映射" },
        { "Color Adjustments (URP)", "颜色调整" },
        { "White Balance (URP)", "白平衡" },
        { "Split Toning (URP)", "分离色调" },
        { "Channel Mixer (URP)", "通道混合器" },
        { "Shadows Midtones Highlights (URP)", "阴影/中间调/高光" },
        { "Depth Of Field (URP)", "景深" },
        { "Motion Blur (URP)", "运动模糊" },
        { "Vignette (URP)", "暗角" },
        { "Film Grain (URP)", "胶片颗粒" },
        { "Chromatic Aberration (URP)", "色差" },
        { "Lens Distortion (URP)", "镜头畸变" },
        { "Panini Projection (URP)", "帕尼尼投影" },
        { "Color Curves", "颜色曲线" },
        { "Lift Gamma Gain (URP)", "提升/伽马/增益" },
        { "Screen Space Reflection", "屏幕空间反射" },
        { "Screen Space Ambient Occlusion", "屏幕空间环境光遮蔽" },
        { "Exposure", "曝光" },
        { "Global Illumination", "全局光照" },
        { "Screen Space Lens Flare", "屏幕空间镜头光晕" },
    };

    /// <summary>
    /// 常用参数名 → 中文
    /// </summary>
    private static readonly Dictionary<string, string> ParamNameCN = new Dictionary<string, string>
    {
        // Bloom
        { "intensity", "强度" },
        { "threshold", "阈值" },
        { "scatter", "散射" },
        { "tint", "色调" },
        { "clamp", "钳制" },
        { "highQualityFiltering", "高质量滤波" },
        { "skipIterations", "跳过迭代" },
        { "dirtTexture", "污渍贴图" },
        { "dirtIntensity", "污渍强度" },
        // Tonemapping
        { "mode", "模式" },
        { "mixMode", "混合模式" },
        // Color Adjustments (HDRP)
        { "postExposure", "后期曝光" },
        { "contrast", "对比度" },
        { "colorFilter", "颜色滤镜" },
        { "hueShift", "色相偏移" },
        { "saturation", "饱和度" },
        // White Balance
        { "temperature", "色温" },
        { "tintWB", "色调" },
        // Split Toning
        { "shadows", "阴影" },
        { "highlights", "高光" },
        { "balance", "平衡" },
        // Channel Mixer
        { "redOutRedIn", "红出/红入" },
        { "redOutGreenIn", "红出/绿入" },
        { "redOutBlueIn", "红出/蓝入" },
        { "greenOutRedIn", "绿出/红入" },
        { "greenOutGreenIn", "绿出/绿入" },
        { "greenOutBlueIn", "绿出/蓝入" },
        { "blueOutRedIn", "蓝出/红入" },
        { "blueOutGreenIn", "蓝出/绿入" },
        { "blueOutBlueIn", "蓝出/蓝入" },
        // Shadows Midtones Highlights
        { "shadowsMidtonesHighlightsShadows", "阴影调整" },
        { "shadowsMidtonesHighlightsMidtones", "中间调调整" },
        { "shadowsMidtonesHighlightsHighlights", "高光调整" },
        // Depth of Field
        { "focusDistance", "焦点距离" },
        { "aperture", "光圈" },
        { "focalLength", "焦距" },
        { "bladeCount", "叶片数量" },
        { "bladeCurvature", "叶片曲率" },
        { "bladeRotation", "叶片旋转" },
        { "maxBlurSize", "最大模糊大小" },
        // Motion Blur
        { "intensityMB", "强度" },
        { "clampMB", "钳制" },
        { "sampleCount", "采样数" },
        // Vignette
        { "color", "颜色" },
        { "center", "中心点" },
        { "intensityVig", "强度" },
        { "smoothness", "平滑度" },
        { "rounded", "圆角" },
        // Film Grain
        { "type", "类型" },
        { "intensityFG", "强度" },
        { "response", "响应" },
        // Chromatic Aberration
        { "intensityCA", "强度" },
        // Lens Distortion
        { "intensityLD", "强度" },
        { "xMultiplier", "X倍率" },
        { "yMultiplier", "Y倍率" },
        { "centerX", "中心X" },
        { "centerY", "中心Y" },
        { "scale", "缩放" },
        // Color Curves
        { "master", "主控" },
        { "red", "红色" },
        { "green", "绿色" },
        { "blue", "蓝色" },
        { "hueVsHue", "色相vs色相" },
        { "hueVsSat", "色相vs饱和度" },
        { "satVsSat", "饱和度vs饱和度" },
        { "lumVsSat", "亮度vs饱和度" },
        // Lift Gamma Gain
        { "lift", "提升" },
        { "gamma", "伽马" },
        { "gain", "增益" },
        // Exposure
        { "fixedExposure", "固定曝光" },
        { "compensation", "补偿" },
        { "limitMin", "最小值" },
        { "limitMax", "最大值" },
        { "modeExp", "模式" },
        { "meteringMask", "测光遮罩" },
    };

    #endregion

    private VolumeProfile targetProfile;
    private Volume targetVolume;
    private Vector2 scrollPos;
    private const float LabelWidth = 200f;

    [MenuItem("工具/Moshi/Volume 中文本地化窗口", false, 100)]
    public static void ShowWindow()
    {
        var window = GetWindow<Moshi_VolumeCN>("Volume 中文显示");
        window.minSize = new Vector2(400, 300);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);

        // 选择目标
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("选择 Volume Profile:", GUILayout.Width(120));
        targetProfile = (VolumeProfile)EditorGUILayout.ObjectField(targetProfile, typeof(VolumeProfile), false);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("或选择场景 Volume:", GUILayout.Width(120));
        targetVolume = (Volume)EditorGUILayout.ObjectField(targetVolume, typeof(Volume), false);

        if (GUILayout.Button("刷新", GUILayout.Width(50)))
        {
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        // 如果没有手动选 Profile，从 Volume 获取
        VolumeProfile activeProfile = targetProfile;
        if (activeProfile == null && targetVolume != null && targetVolume.sharedProfile != null)
        {
            activeProfile = targetVolume.sharedProfile;
        }

        // 如果没有指定，自动检测场景中的 Volume
        if (activeProfile == null && targetVolume == null)
        {
            Volume sceneVolume = FindObjectOfType<Volume>();
            if (sceneVolume != null && sceneVolume.sharedProfile != null)
            {
                activeProfile = sceneVolume.sharedProfile;
                EditorGUILayout.HelpBox($"已自动检测场景 Volume: {sceneVolume.name}", MessageType.Info);
            }
        }

        if (activeProfile == null)
        {
            EditorGUILayout.HelpBox("请选择一个 Volume Profile 资产，或场景中包含 VolumeProfile 的 Volume 组件。", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"当前: {activeProfile.name}", EditorStyles.boldLabel);

        // 获取所有 VolumeComponent
        var components = activeProfile.components;
        if (components == null || components.Count == 0)
        {
            EditorGUILayout.HelpBox("该 Volume Profile 中没有添加任何后处理组件。", MessageType.Info);
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        foreach (var comp in components)
        {
            if (comp == null) continue;

            string typeName = comp.GetType().Name;
            string cnName = GetCNName(ComponentNameCN, typeName, typeName);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 标题行: toggle + 中文名
            EditorGUILayout.BeginHorizontal();
            comp.active = EditorGUILayout.Toggle(comp.active, GUILayout.Width(16));
            EditorGUILayout.LabelField($"<b>{cnName}</b> <size=10>({typeName})</size>", 
                new GUIStyle(EditorStyles.label) { richText = true, fontStyle = FontStyle.Bold });
            EditorGUILayout.EndHorizontal();

            if (comp.active)
            {
                DrawComponentParameters(comp);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawComponentParameters(VolumeComponent comp)
    {
        EditorGUI.indentLevel++;

        var fields = comp.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

        foreach (var field in fields)
        {
            // 跳过非 public 字段 (但 SerializeField 的不跳过)
            if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null)
                continue;

            // 跳过 active 字段
            if (field.Name == "active") continue;

            // 尝试获取 VolumeParameter 基类值
            object fieldValue = field.GetValue(comp);
            if (fieldValue == null) continue;

            Type fieldType = fieldValue.GetType();

            // 检查是否是 VolumeParameter<> 类型
            bool hasOverride = GetOverrideState(fieldValue);
            bool hadOverride = hasOverride;
            Type valueType = GetVolumeParameterValueType(fieldType);

            if (valueType == null) continue;

            string cnParam = GetCNName(ParamNameCN, field.Name, field.Name);

            // 只显示有 override 或常用参数（减少视觉噪音）
            EditorGUILayout.BeginHorizontal();

            // Override 开关
            hasOverride = EditorGUILayout.Toggle(hasOverride, GUILayout.Width(16));

            if (hasOverride != hadOverride)
            {
                SetOverrideState(fieldValue, hasOverride);
                EditorUtility.SetDirty(targetProfile ?? targetVolume?.sharedProfile);
            }

            if (!hasOverride)
            {
                GUI.enabled = false;
            }

            // 参数名（中文）
            EditorGUILayout.LabelField(cnParam, GUILayout.Width(LabelWidth - 80));

            // 参数值
            object currentVal = GetOverrideValue(fieldValue, valueType);
            object newVal = DrawField(valueType, currentVal);
            if (newVal != null && !newVal.Equals(currentVal))
            {
                SetOverrideValue(fieldValue, valueType, newVal);
                EditorUtility.SetDirty(targetProfile ?? targetVolume?.sharedProfile);
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        EditorGUI.indentLevel--;
    }

    #region Reflection Helpers

    private string GetCNName(Dictionary<string, string> dict, string key, string fallback)
    {
        // 精确匹配
        if (dict.TryGetValue(key, out string value))
            return value;

        // 大小写不敏感匹配
        foreach (var kv in dict)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        return fallback;
    }

    private Type GetVolumeParameterValueType(Type volumeParameterType)
    {
        Type t = volumeParameterType;
        while (t != null && t != typeof(object))
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(VolumeParameter<>))
            {
                return t.GetGenericArguments()[0];
            }
            t = t.BaseType;
        }
        return null;
    }

    private bool GetOverrideState(object volumeParameter)
    {
        if (volumeParameter == null) return false;
        var prop = volumeParameter.GetType().GetProperty("overrideState", BindingFlags.Public | BindingFlags.Instance);
        return prop != null && (bool)prop.GetValue(volumeParameter);
    }

    private void SetOverrideState(object volumeParameter, bool state)
    {
        var prop = volumeParameter.GetType().GetProperty("overrideState", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(volumeParameter, state);
    }

    private object GetOverrideValue(object volumeParameter, Type valueType)
    {
        var prop = volumeParameter.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(volumeParameter);
    }

    private void SetOverrideValue(object volumeParameter, Type valueType, object newValue)
    {
        var prop = volumeParameter.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(volumeParameter, newValue);
    }

    private object DrawField(Type valueType, object currentValue)
    {
        if (valueType == typeof(float))
            return EditorGUILayout.FloatField((float)(currentValue ?? 0f));
        if (valueType == typeof(int))
            return EditorGUILayout.IntField((int)(currentValue ?? 0));
        if (valueType == typeof(bool))
            return EditorGUILayout.Toggle((bool)(currentValue ?? false));
        if (valueType == typeof(Color))
            return EditorGUILayout.ColorField((Color)(currentValue ?? Color.white));
        if (valueType == typeof(Vector2))
            return EditorGUILayout.Vector2Field("", (Vector2)(currentValue ?? Vector2.zero));
        if (valueType == typeof(Vector3))
            return EditorGUILayout.Vector3Field("", (Vector3)(currentValue ?? Vector3.zero));
        if (valueType == typeof(Vector4))
            return EditorGUILayout.Vector4Field("", (Vector4)(currentValue ?? Vector4.zero));
        if (valueType == typeof(Texture) || valueType.IsSubclassOf(typeof(Texture)))
            return EditorGUILayout.ObjectField((Texture)currentValue, typeof(Texture), false);
        if (valueType == typeof(LayerMask))
            return (LayerMask)EditorGUILayout.LayerField((LayerMask)(currentValue ?? 0));
        if (valueType.IsEnum)
            return EditorGUILayout.EnumPopup((Enum)(currentValue ?? Activator.CreateInstance(valueType)));

        // 未支持的类型：只读显示
        EditorGUILayout.LabelField(currentValue?.ToString() ?? "null", GUILayout.MaxWidth(300));
        return currentValue;
    }

    #endregion
}
