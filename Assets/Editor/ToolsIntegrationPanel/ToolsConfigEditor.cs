#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ToolsConfig))]
public class ToolsConfigEditor : UnityEditor.Editor
{
    private static bool quickAccessSettingsExpanded = true;
    private static bool buttonPreviewExpanded = true;
    private static bool bindingKeyExpanded = false;

    private SerializedProperty allToolsProperty;
    private SerializedProperty quickAccessButtonMinWidthProperty;
    private SerializedProperty quickAccessButtonMinHeightProperty;
    private SerializedProperty quickAccessButtonMaxWidthProperty;
    private SerializedProperty quickAccessButtonMaxHeightProperty;
    private SerializedProperty quickAccessButtonSpacingProperty;
    private SerializedProperty quickAccessRowSpacingProperty;
    private SerializedProperty quickAccessHorizontalPaddingProperty;
    private SerializedProperty quickAccessSectionPaddingProperty;
    private SerializedProperty quickAccessSectionMarginProperty;
    private SerializedProperty quickAccessMaxColumnsProperty;
    private SerializedProperty windowMinWidthProperty;
    private SerializedProperty windowMinHeightProperty;
    private SerializedProperty windowMaxWidthProperty;
    private SerializedProperty windowMaxHeightProperty;
    private SerializedProperty showQuickAccessInfoProperty;
    private SerializedProperty showFullToolsSectionProperty;

    // 双击重命名相关
    private int editingIndex = -1;
    private string editingName = "";

    private void OnEnable()
    {
        if (target == null)
        {
            return;
        }

        CacheSerializedProperties();
    }

    public override void OnInspectorGUI()
    {
        if (target == null)
        {
            DrawMissingTargetFallback();
            return;
        }

        if (allToolsProperty == null)
        {
            CacheSerializedProperties();
        }

        serializedObject.Update();

        DrawInfoBox();
        DrawBindingKeyGuide();
        DrawQuickAccessSettings();

        EditorGUILayout.Space();
        DrawToolListWithRename();

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("保存并刷新面板"))
        {
            AssetDatabase.SaveAssets();
            ToolsIntegrationPanel.RequestConfigRefresh();
        }

        if (GUILayout.Button("打开 Tools Integration Panel"))
        {
            ToolsIntegrationPanel.ShowWindow();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void CacheSerializedProperties()
    {
        allToolsProperty = serializedObject.FindProperty("allTools");
        quickAccessButtonMinWidthProperty = serializedObject.FindProperty("quickAccessButtonMinWidth");
        quickAccessButtonMinHeightProperty = serializedObject.FindProperty("quickAccessButtonMinHeight");
        quickAccessButtonMaxWidthProperty = serializedObject.FindProperty("quickAccessButtonMaxWidth");
        quickAccessButtonMaxHeightProperty = serializedObject.FindProperty("quickAccessButtonMaxHeight");
        quickAccessButtonSpacingProperty = serializedObject.FindProperty("quickAccessButtonSpacing");
        quickAccessRowSpacingProperty = serializedObject.FindProperty("quickAccessRowSpacing");
        quickAccessHorizontalPaddingProperty = serializedObject.FindProperty("quickAccessHorizontalPadding");
        quickAccessSectionPaddingProperty = serializedObject.FindProperty("quickAccessSectionPadding");
        quickAccessSectionMarginProperty = serializedObject.FindProperty("quickAccessSectionMargin");
        quickAccessMaxColumnsProperty = serializedObject.FindProperty("quickAccessMaxColumns");
        windowMinWidthProperty = serializedObject.FindProperty("windowMinWidth");
        windowMinHeightProperty = serializedObject.FindProperty("windowMinHeight");
        windowMaxWidthProperty = serializedObject.FindProperty("windowMaxWidth");
        windowMaxHeightProperty = serializedObject.FindProperty("windowMaxHeight");
        showQuickAccessInfoProperty = serializedObject.FindProperty("showQuickAccessInfo");
        showFullToolsSectionProperty = serializedObject.FindProperty("showFullToolsSection");
    }

    private void DrawMissingTargetFallback()
    {
        EditorGUILayout.HelpBox(
            "未找到可编辑的 ToolsConfig 资源。请确保在默认路径下创建或刷新配置。",
            MessageType.Warning);

        if (GUILayout.Button("创建/刷新配置"))
        {
            InitToolsConfig.CreateOrResetToolsConfig();
        }
    }

    private void DrawInfoBox()
    {
        EditorGUILayout.HelpBox(
            $"配置资源固定存放在 {ToolsConfig.DefaultAssetPath}\n工具绑定将扫描 AllToolsResources 目录中带 Tool 特性或带 MenuItem 的静态方法，点击下方按钮即可执行刷新。",
            MessageType.Info);
    }

    private void DrawQuickAccessSettings()
    {
        EditorGUILayout.BeginVertical("box");
        quickAccessSettingsExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(quickAccessSettingsExpanded, "快速入口 UI");
        if (quickAccessSettingsExpanded)
        {
            EditorGUILayout.Space(4);
            
            // 按钮预览区域
            DrawButtonPreview();
            
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Quick Access Decorations", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(showQuickAccessInfoProperty, new GUIContent("显示标题与提示"));
            
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Quick Access UI", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(quickAccessButtonMinWidthProperty, new GUIContent("按钮最小宽度 (px)"));
            EditorGUILayout.PropertyField(quickAccessButtonMaxWidthProperty, new GUIContent("按钮最大宽度 (px)"));
            EditorGUILayout.PropertyField(quickAccessButtonMinHeightProperty, new GUIContent("按钮最小高度 (px)"));
            EditorGUILayout.PropertyField(quickAccessButtonMaxHeightProperty, new GUIContent("按钮最大高度 (px)"));
            EditorGUILayout.PropertyField(quickAccessButtonSpacingProperty, new GUIContent("按钮水平间距 (px)"));
            EditorGUILayout.PropertyField(quickAccessRowSpacingProperty, new GUIContent("行间距 (px)"));
            EditorGUILayout.PropertyField(quickAccessHorizontalPaddingProperty, new GUIContent("区域水平内边距 (px)"));
            EditorGUILayout.PropertyField(quickAccessSectionPaddingProperty, new GUIContent("区域内边距 (px)"));
            EditorGUILayout.PropertyField(quickAccessSectionMarginProperty, new GUIContent("区域外边距 (px)"));
            EditorGUILayout.PropertyField(quickAccessMaxColumnsProperty, new GUIContent("最大列数"));

            EditorGUILayout.Space(8);
            GUILayout.Label("窗口尺寸限制", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(windowMinWidthProperty, new GUIContent("窗口最小宽度 (px)"));
            EditorGUILayout.PropertyField(windowMinHeightProperty, new GUIContent("窗口最小高度 (px)"));
            EditorGUILayout.PropertyField(windowMaxWidthProperty, new GUIContent("窗口最大宽度 (px)"));
            EditorGUILayout.PropertyField(windowMaxHeightProperty, new GUIContent("窗口最大高度 (px)"));

            EditorGUILayout.HelpBox("这些参数控制快速入口卡片的最小/最大尺寸以及工具窗口的尺寸上限。", MessageType.None);

            EditorGUILayout.Space(8);
            GUILayout.Label("全部工具折叠栏", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(showFullToolsSectionProperty, new GUIContent("显示 \"全部工具\" 折叠栏"));
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }
    
    private void DrawButtonPreview()
    {
        EditorGUILayout.BeginVertical("box");
        buttonPreviewExpanded = EditorGUILayout.Foldout(buttonPreviewExpanded, "按钮尺寸预览", true, EditorStyles.foldoutHeader);
        
        if (buttonPreviewExpanded)
        {
            EditorGUILayout.Space(4);
            
            float minWidth = quickAccessButtonMinWidthProperty.floatValue;
            float maxWidth = quickAccessButtonMaxWidthProperty.floatValue;
            float minHeight = quickAccessButtonMinHeightProperty.floatValue;
            float maxHeight = quickAccessButtonMaxHeightProperty.floatValue;
            float spacing = quickAccessButtonSpacingProperty.floatValue;
            
            // 显示尺寸信息
            EditorGUILayout.LabelField($"最小尺寸: {minWidth} x {minHeight} px", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"最大尺寸: {maxWidth} x {maxHeight} px", EditorStyles.miniLabel);
            
            EditorGUILayout.Space(8);
            
            // 预览按钮 - 最小尺寸
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUIStyle previewStyle = new GUIStyle(GUI.skin.button);
            previewStyle.fontSize = 11;
            previewStyle.wordWrap = true;
            
            // 最小尺寸按钮
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("最小尺寸", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(minWidth));
            if (GUILayout.Button("示例按钮", previewStyle, GUILayout.Width(minWidth), GUILayout.Height(minHeight)))
            {
                // 预览按钮，无操作
            }
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(spacing);
            
            // 最大尺寸按钮
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("最大尺寸", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(maxWidth));
            if (GUILayout.Button("示例按钮", previewStyle, GUILayout.Width(maxWidth), GUILayout.Height(maxHeight)))
            {
                // 预览按钮，无操作
            }
            EditorGUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(8);
            
            // 多按钮排列预览
            EditorGUILayout.LabelField("排列预览 (3个按钮)", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            float avgWidth = (minWidth + maxWidth) / 2f;
            float avgHeight = (minHeight + maxHeight) / 2f;
            
            for (int i = 0; i < 3; i++)
            {
                if (GUILayout.Button($"工具{i + 1}", previewStyle, GUILayout.Width(avgWidth), GUILayout.Height(avgHeight)))
                {
                    // 预览按钮，无操作
                }
                if (i < 2) GUILayout.Space(spacing);
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(4);
        }
        
        EditorGUILayout.EndVertical();
    }

    private void DrawBindingKeyGuide()
    {
        EditorGUILayout.BeginVertical("box");
        IReadOnlyList<ToolDefinition> definitions = ToolBindingCatalog.Definitions;
        int count = definitions.Count;
        bindingKeyExpanded = EditorGUILayout.Foldout(bindingKeyExpanded, $"可用 Binding Key ({count})", true, EditorStyles.foldoutHeader);
        
        if (bindingKeyExpanded)
        {
            if (count == 0)
            {
                EditorGUILayout.LabelField(
                    "暂无绑定。",
                    "请在 AllToolsResources 目录添加带 Tool 特性或 MenuItem 的静态方法，然后点击刷新。");
            }
            else
            {
                foreach (ToolDefinition definition in definitions)
                {
                    string display = $"{definition.ToolName} · {definition.Category}";
                    EditorGUILayout.LabelField(definition.BindingKey, display);
                }
            }
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawToolListWithRename()
    {
        // 使用原生 PropertyField 绘制列表（保留拖拽手柄）
        EditorGUILayout.PropertyField(allToolsProperty, new GUIContent("工具列表"), true);
        
        // 检测双击重命名
        if (allToolsProperty.isExpanded)
        {
            HandleDoubleClickRename();
        }
    }

    private void HandleDoubleClickRename()
    {
        Event e = Event.current;
        
        // 处理编辑中的输入
        if (editingIndex >= 0 && editingIndex < allToolsProperty.arraySize)
        {
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    SerializedProperty itemProperty = allToolsProperty.GetArrayElementAtIndex(editingIndex);
                    SerializedProperty toolNameProperty = itemProperty.FindPropertyRelative("toolName");
                    ApplyRename(toolNameProperty);
                    e.Use();
                    return;
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    CancelRename();
                    e.Use();
                    return;
                }
            }
        }
        
        // 检测双击
        if (e.type == EventType.MouseDown && e.clickCount == 2)
        {
            // 计算点击位置对应的工具项索引
            int clickedIndex = GetToolIndexAtMousePosition(e.mousePosition);
            if (clickedIndex >= 0 && clickedIndex < allToolsProperty.arraySize)
            {
                SerializedProperty itemProperty = allToolsProperty.GetArrayElementAtIndex(clickedIndex);
                // 只在未展开时允许双击重命名
                if (!itemProperty.isExpanded)
                {
                    SerializedProperty toolNameProperty = itemProperty.FindPropertyRelative("toolName");
                    StartRename(clickedIndex, toolNameProperty.stringValue);
                    e.Use();
                }
            }
        }
    }

    private int GetToolIndexAtMousePosition(Vector2 mousePos)
    {
        // 这是一个简化的实现，基于行高估算
        // Unity 默认的 PropertyField 每个折叠项大约 18-20 像素高
        float headerHeight = 20f; // "工具列表" 标题行
        float itemHeight = 20f;   // 每个折叠项的高度
        
        // 需要找到列表开始的位置，这里用一个近似值
        // 实际位置取决于之前绘制的内容
        Rect lastRect = GUILayoutUtility.GetLastRect();
        float listStartY = lastRect.y - (allToolsProperty.arraySize * itemHeight) - headerHeight;
        
        float relativeY = mousePos.y - listStartY - headerHeight;
        if (relativeY < 0) return -1;
        
        int index = Mathf.FloorToInt(relativeY / itemHeight);
        return index;
    }

    private void StartRename(int index, string currentName)
    {
        editingIndex = index;
        editingName = currentName ?? "";
        
        // 弹出重命名窗口
        RenamePopup.Show(editingName, (newName) =>
        {
            if (!string.IsNullOrWhiteSpace(newName) && editingIndex >= 0 && editingIndex < allToolsProperty.arraySize)
            {
                serializedObject.Update();
                SerializedProperty itemProperty = allToolsProperty.GetArrayElementAtIndex(editingIndex);
                SerializedProperty toolNameProperty = itemProperty.FindPropertyRelative("toolName");
                toolNameProperty.stringValue = newName.Trim();
                serializedObject.ApplyModifiedProperties();
            }
            editingIndex = -1;
            editingName = "";
        }, () =>
        {
            editingIndex = -1;
            editingName = "";
        });
    }

    private void ApplyRename(SerializedProperty toolNameProperty)
    {
        if (!string.IsNullOrWhiteSpace(editingName))
        {
            toolNameProperty.stringValue = editingName.Trim();
        }
        editingIndex = -1;
        editingName = "";
    }

    private void CancelRename()
    {
        editingIndex = -1;
        editingName = "";
    }
}

// 重命名弹窗
public class RenamePopup : EditorWindow
{
    private string newName;
    private System.Action<string> onConfirm;
    private System.Action onCancel;
    private bool focusSet;

    public static void Show(string currentName, System.Action<string> onConfirm, System.Action onCancel)
    {
        RenamePopup window = CreateInstance<RenamePopup>();
        window.newName = currentName;
        window.onConfirm = onConfirm;
        window.onCancel = onCancel;
        window.titleContent = new GUIContent("重命名");
        
        Vector2 size = new Vector2(250, 60);
        window.minSize = size;
        window.maxSize = size;
        
        // 在鼠标位置显示
        Vector2 mousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
        window.position = new Rect(mousePos.x - 125, mousePos.y - 30, size.x, size.y);
        
        window.ShowPopup();
        window.Focus();
    }

    private void OnGUI()
    {
        Event e = Event.current;
        
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                Confirm();
                e.Use();
                return;
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                Cancel();
                e.Use();
                return;
            }
        }

        EditorGUILayout.Space(8);
        
        GUI.SetNextControlName("RenameField");
        newName = EditorGUILayout.TextField("名称", newName);
        
        if (!focusSet)
        {
            EditorGUI.FocusTextInControl("RenameField");
            focusSet = true;
        }

        EditorGUILayout.Space(4);
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("确定", GUILayout.Width(60)))
        {
            Confirm();
        }
        if (GUILayout.Button("取消", GUILayout.Width(60)))
        {
            Cancel();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void Confirm()
    {
        onConfirm?.Invoke(newName);
        Close();
    }

    private void Cancel()
    {
        onCancel?.Invoke();
        Close();
    }

    private void OnLostFocus()
    {
        Cancel();
    }
}
#endif
