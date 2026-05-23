using System;
using System.Collections.Generic;
using System.Linq;
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
    private VolumeProfile activeWorkingProfile; // 当前实际使用的 profile（含自动检测），用于 SetDirty
    private Vector2 scrollPos;
    private const float LabelWidth = 200f;

    /// <summary>
    /// 缓存每个 VolumeComponent 类型的反射元数据，避免每帧重复反射
    /// </summary>
    private class FieldMeta
    {
        public FieldInfo Field;
        public string CnName;
        public Type ValueType;
        /// <summary>VolumeParameter 包装类型（如 FloatParameter、MinFloatParameter 等）</summary>
        public Type ParameterType;
    }

    private static readonly Dictionary<Type, List<FieldMeta>> FieldCache = new Dictionary<Type, List<FieldMeta>>();

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

        // 如果没有指定，自动检测场景中的 Volume（仅首次，后续用缓存避免每帧 FindObjectOfType）
        if (activeProfile == null && targetVolume == null)
        {
            if (activeWorkingProfile != null)
            {
                activeProfile = activeWorkingProfile;
            }
            else
            {
                Volume sceneVolume = FindObjectOfType<Volume>();
                if (sceneVolume != null && sceneVolume.sharedProfile != null)
                {
                    activeProfile = sceneVolume.sharedProfile;
                    EditorGUILayout.HelpBox($"已自动检测场景 Volume: {sceneVolume.name}", MessageType.Info);
                }
            }
        }

        if (activeProfile == null)
        {
            EditorGUILayout.HelpBox("请选择一个 Volume Profile 资产，或场景中包含 VolumeProfile 的 Volume 组件。", MessageType.Info);
            return;
        }

        activeWorkingProfile = activeProfile;
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

            // 组件级 active 开关（大开关）
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            bool newActive = EditorGUILayout.ToggleLeft(
                $"<b>{cnName}</b> <size=10>({typeName})</size>",
                comp.active, new GUIStyle(EditorStyles.label) { richText = true, fontStyle = FontStyle.Bold });
            if (EditorGUI.EndChangeCheck())
            {
                comp.active = newActive;
                EditorUtility.SetDirty(activeWorkingProfile);
                EditorUtility.SetDirty(comp);
                RepaintUnityInspector();
            }
            EditorGUILayout.EndHorizontal();

            if (comp.active)
            {
                bool changed = DrawComponentParameters(comp);
                if (changed)
                {
                    EditorUtility.SetDirty(activeWorkingProfile);
                    RepaintUnityInspector();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndScrollView();
    }

    private bool DrawComponentParameters(VolumeComponent comp)
    {
        EditorGUI.indentLevel++;

        var so = new SerializedObject(comp);
        so.Update();

        var metas = GetOrCacheFieldMetas(comp);

        foreach (var meta in metas)
        {
            var sp = so.FindProperty(meta.Field.Name);
            if (sp == null) continue;

            var overrideProp = sp.FindPropertyRelative("m_OverrideState") ?? sp.FindPropertyRelative("overrideState");
            var valueProp = sp.FindPropertyRelative("m_Value") ?? sp.FindPropertyRelative("value");

            if (overrideProp == null || valueProp == null) continue;

            bool hasOverride = overrideProp.boolValue;

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            bool newOverride = EditorGUILayout.ToggleLeft(meta.CnName, hasOverride, GUILayout.Width(LabelWidth));
            if (EditorGUI.EndChangeCheck())
            {
                overrideProp.boolValue = newOverride;
            }

            using (new EditorGUI.DisabledGroupScope(!newOverride))
            {
                EditorGUILayout.PropertyField(valueProp, GUIContent.none, true);
            }

            EditorGUILayout.EndHorizontal();
        }

        bool changed = so.hasModifiedProperties;
        if (changed)
        {
            so.ApplyModifiedProperties();
            // 强制立即刷新：标记 VolumeComponent 脏，触发原生 Inspector 更新
            EditorUtility.SetDirty(comp);
            // 通知 Unity Inspector 窗口重绘
            EditorApplication.RepaintHierarchyWindow();
            EditorApplication.RepaintProjectWindow();
        }

        EditorGUI.indentLevel--;
        return changed;
    }

    /// <summary>
    /// 强制刷新 Unity 原生 Inspector + SceneView，使参数变更即时可见
    /// </summary>
    private static void RepaintUnityInspector()
    {
        // 刷新所有已打开的 Inspector 窗口
        var inspectorType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        if (inspectorType != null)
        {
            foreach (var win in Resources.FindObjectsOfTypeAll(inspectorType))
                ((EditorWindow)win).Repaint();
        }
        // 刷新所有 SceneView（Volume 效果即时可见）
        foreach (var sceneView in Resources.FindObjectsOfTypeAll<SceneView>())
            sceneView.Repaint();
    }

    /// <summary>
    /// 获取或构建类型的反射元数据缓存
    /// </summary>
    private List<FieldMeta> GetOrCacheFieldMetas(VolumeComponent comp)
    {
        Type compType = comp.GetType();
        if (FieldCache.TryGetValue(compType, out var cached))
            return cached;

        var metas = new List<FieldMeta>();
        var fields = compType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

        foreach (var field in fields)
        {
            if (field.Name == "active") continue;
            if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null)
                continue;

            try
            {
                object fieldValue = field.GetValue(comp);
                if (fieldValue == null) continue;

                Type valueType = GetVolumeParameterValueType(fieldValue.GetType());
                if (valueType == null) continue;

                metas.Add(new FieldMeta
                {
                    Field = field,
                    CnName = GetCNName(ParamNameCN, field.Name, field.Name),
                    ValueType = valueType,
                    ParameterType = fieldValue.GetType()
                });
            }
            catch
            {
                // 忽略无法反射的字段
            }
        }

        FieldCache[compType] = metas;
        return metas;
    }

    #region Reflection Helpers

    /// <summary>缓存每个 VolumeParameter 类型的 overrideState PropertyInfo</summary>
    private static readonly Dictionary<Type, PropertyInfo> OverrideStatePropCache = new Dictionary<Type, PropertyInfo>();
    /// <summary>缓存每个 VolumeParameter 类型的 value PropertyInfo</summary>
    private static readonly Dictionary<Type, PropertyInfo> ValuePropCache = new Dictionary<Type, PropertyInfo>();

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

    private PropertyInfo GetOverrideStateProp(Type paramType)
    {
        if (!OverrideStatePropCache.TryGetValue(paramType, out var prop))
        {
            prop = paramType.GetProperty("overrideState", BindingFlags.Public | BindingFlags.Instance);
            OverrideStatePropCache[paramType] = prop;
        }
        return prop;
    }

    private PropertyInfo GetValueProp(Type paramType)
    {
        if (!ValuePropCache.TryGetValue(paramType, out var prop))
        {
            prop = paramType.GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
            ValuePropCache[paramType] = prop;
        }
        return prop;
    }

    private bool GetOverrideState(object volumeParameter)
    {
        if (volumeParameter == null) return false;
        var prop = GetOverrideStateProp(volumeParameter.GetType());
        return prop != null && (bool)prop.GetValue(volumeParameter);
    }

    private void SetOverrideState(object volumeParameter, bool state)
    {
        GetOverrideStateProp(volumeParameter?.GetType())?.SetValue(volumeParameter, state);
    }

    private object GetOverrideValue(object volumeParameter, Type valueType)
    {
        return GetValueProp(volumeParameter?.GetType())?.GetValue(volumeParameter);
    }

    private void SetOverrideValue(object volumeParameter, Type valueType, object newValue)
    {
        GetValueProp(volumeParameter?.GetType())?.SetValue(volumeParameter, newValue);
    }

    private object DrawField(Type valueType, Type paramType, object currentValue)
    {
        if (valueType == typeof(float))
        {
            float min, max;
            if (TryGetParamRange(paramType, out min, out max))
                return EditorGUILayout.Slider((float)(currentValue ?? 0f), min, max);
            return EditorGUILayout.FloatField((float)(currentValue ?? 0f));
        }
        if (valueType == typeof(int))
        {
            float min, max;
            if (TryGetParamRange(paramType, out min, out max))
                return EditorGUILayout.IntSlider((int)(currentValue ?? 0), (int)min, (int)max);
            return EditorGUILayout.IntField((int)(currentValue ?? 0));
        }
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
        if (valueType == typeof(AnimationCurve))
            return EditorGUILayout.CurveField((AnimationCurve)(currentValue ?? AnimationCurve.Linear(0, 0, 1, 1)));

        // 未支持的类型：尝试用 ObjectField 兜底（适用于 TextureCurve 等 ScriptableObject 派生类型）
        if (valueType.IsClass || valueType.IsValueType)
        {
            if (typeof(UnityEngine.Object).IsAssignableFrom(valueType))
                return EditorGUILayout.ObjectField((UnityEngine.Object)currentValue, valueType, false);
            // 其他值类型 → 尝试字符串转换
            var str = EditorGUILayout.TextField(currentValue?.ToString() ?? "");
            try { return Convert.ChangeType(str, valueType); } catch { return currentValue; }
        }

        EditorGUILayout.LabelField(currentValue?.ToString() ?? "null", GUILayout.MaxWidth(300));
        return currentValue;
    }

    /// <summary>
    /// 尝试从 VolumeParameter 包装类型读取 min/max 约束
    /// </summary>
    private bool TryGetParamRange(Type paramType, out float min, out float max)
    {
        min = float.MinValue;
        max = float.MaxValue;
        if (paramType == null) return false;

        // 检查类型名是否包含 Clamped / Min / Range 等标识
        // 优先尝试读取 "min" 和 "max" 属性
        var minProp = paramType.GetProperty("min", BindingFlags.Public | BindingFlags.Instance);
        var maxProp = paramType.GetProperty("max", BindingFlags.Public | BindingFlags.Instance);

        if (minProp != null && maxProp != null)
        {
            try
            {
                min = Convert.ToSingle(minProp.GetValue(null)); // static prop like ClampedFloatParameter.min
                max = Convert.ToSingle(maxProp.GetValue(null));
                return true;
            }
            catch { }
        }

        // 尝试实例属性
        return false;
    }

    #endregion
}
