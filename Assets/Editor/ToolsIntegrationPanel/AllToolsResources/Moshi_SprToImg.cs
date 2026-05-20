using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;

namespace MoshiTools
{
    /// <summary>
    /// 精灵/图片/网格 三方互转工具
    /// SpriteRenderer ↔ Image ↔ MeshRenderer tri-directional converter tool
    /// </summary>
    public class Moshi_SprToImg : EditorWindow
    {
        private const string TOOL_NAME = "精灵图片互转";

        // 组件类型
        // Component type
        private enum ComponentType { Sprite, Image, Mesh }
        private ComponentType sourceType = ComponentType.Sprite;
        private ComponentType targetType = ComponentType.Image;

        // 转换选项
        // Conversion options
        private bool includeChildren = true;
        private bool preserveSize = true;
        private bool removeOldComponent = true;
        private bool addCanvasIfNeeded = true;
        private bool useBuiltinQuad = true;

        // 内置 Quad 网格缓存
        // Built-in Quad mesh cache
        private static Mesh _cachedQuadMesh;

        // 转换结果
        // Conversion results
        private List<ConversionRecord> conversionRecords = new List<ConversionRecord>();
        private Vector2 scrollPos;

        [System.Serializable]
        private class ConversionRecord
        {
            public string gameObjectName;
            public string spriteName;
            public string materialName;
            public bool success;
            public string message;
        }

        [MenuItem("工具/Moshi/" + TOOL_NAME)]
        public static void ShowWindow()
        {
            var window = GetWindow<Moshi_SprToImg>(TOOL_NAME);
            window.minSize = new Vector2(420, 360);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("SpriteRenderer ↔ Image ↔ Mesh 三方互转工具", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // 源类型选择
            // Source type selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("源类型", GUILayout.Width(60));
            if (GUILayout.Toggle(sourceType == ComponentType.Sprite, "精灵", EditorStyles.miniButtonLeft))
                sourceType = ComponentType.Sprite;
            if (GUILayout.Toggle(sourceType == ComponentType.Image, "图片", EditorStyles.miniButtonMid))
                sourceType = ComponentType.Image;
            if (GUILayout.Toggle(sourceType == ComponentType.Mesh, "网格", EditorStyles.miniButtonRight))
                sourceType = ComponentType.Mesh;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 目标类型选择
            // Target type selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("目标类型", GUILayout.Width(60));
            if (GUILayout.Toggle(targetType == ComponentType.Sprite, "精灵", EditorStyles.miniButtonLeft))
                targetType = ComponentType.Sprite;
            if (GUILayout.Toggle(targetType == ComponentType.Image, "图片", EditorStyles.miniButtonMid))
                targetType = ComponentType.Image;
            if (GUILayout.Toggle(targetType == ComponentType.Mesh, "网格", EditorStyles.miniButtonRight))
                targetType = ComponentType.Mesh;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            bool sameType = sourceType == targetType;
            if (sameType)
            {
                EditorGUILayout.HelpBox("源类型与目标类型相同，请选择不同的类型。", MessageType.Warning);
            }

            EditorGUILayout.Space(3);

            // 选项区
            // Options section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("转换选项", EditorStyles.miniBoldLabel);
            EditorGUILayout.Space(2);

            includeChildren = EditorGUILayout.Toggle("包含子物体", includeChildren);
            preserveSize = EditorGUILayout.Toggle("保持尺寸", preserveSize);
            removeOldComponent = EditorGUILayout.Toggle("移除旧组件", removeOldComponent);

            // 仅当目标为 Image 时显示
            // Only show when target is Image
            if (targetType == ComponentType.Image)
            {
                addCanvasIfNeeded = EditorGUILayout.Toggle("自动添加Canvas", addCanvasIfNeeded);
            }

            // 仅当目标为 Mesh 时显示
            // Only show when target is Mesh
            if (targetType == ComponentType.Mesh)
            {
                useBuiltinQuad = EditorGUILayout.Toggle("使用内置Quad", useBuiltinQuad);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 选中物体信息
            // Selection info
            var selection = Selection.gameObjects;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"当前选中: {selection.Length} 个物体", EditorStyles.miniLabel);

            if (selection.Length > 0)
            {
                int componentCount = 0;
                string componentName = GetComponentTypeName(sourceType);

                foreach (var go in selection)
                {
                    componentCount += CountSourceComponents(go);
                }
                EditorGUILayout.LabelField($"找到 {componentName}: {componentCount} 个", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 操作按钮
            // Action buttons
            EditorGUI.BeginDisabledGroup(selection.Length == 0 || sameType);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("预览转换", GUILayout.Height(30)))
            {
                PreviewConversion(selection);
            }
            if (GUILayout.Button("执行转换", GUILayout.Height(30)))
            {
                ExecuteConversion(selection);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(10);

            // 结果列表
            // Result list
            if (conversionRecords.Count > 0)
            {
                EditorGUILayout.LabelField($"转换记录 ({conversionRecords.Count})", EditorStyles.boldLabel);
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

                foreach (var record in conversionRecords)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                    var icon = record.success ? "d_greenLight" : "d_redLight";
                    EditorGUILayout.LabelField(EditorGUIUtility.IconContent(icon), GUILayout.Width(20));

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField(record.gameObjectName, EditorStyles.miniBoldLabel);

                    string info = "";
                    if (!string.IsNullOrEmpty(record.spriteName))
                        info += $"Sprite: {record.spriteName}  ";
                    if (!string.IsNullOrEmpty(record.materialName))
                        info += $"Material: {record.materialName}";
                    if (!string.IsNullOrEmpty(info))
                        EditorGUILayout.LabelField(info, EditorStyles.miniLabel);
                    if (!string.IsNullOrEmpty(record.message))
                        EditorGUILayout.LabelField(record.message, EditorStyles.miniLabel);

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(5);
                if (GUILayout.Button("清除记录"))
                {
                    conversionRecords.Clear();
                }
            }
        }

        // ==================== 扫描与预览 ====================
        // ==================== Scan & Preview ====================

        /// <summary>
        /// 获取组件类型显示名
        /// Get display name of component type
        /// </summary>
        private string GetComponentTypeName(ComponentType type)
        {
            switch (type)
            {
                case ComponentType.Sprite: return "SpriteRenderer";
                case ComponentType.Image: return "Image";
                case ComponentType.Mesh: return "MeshFilter";
                default: return "";
            }
        }

        /// <summary>
        /// 统计单个物体下的源组件数量
        /// Count source components under a GameObject
        /// </summary>
        private int CountSourceComponents(GameObject go)
        {
            switch (sourceType)
            {
                case ComponentType.Sprite:
                    return includeChildren
                        ? go.GetComponentsInChildren<SpriteRenderer>(true).Length
                        : (go.GetComponent<SpriteRenderer>() != null ? 1 : 0);
                case ComponentType.Image:
                    return includeChildren
                        ? go.GetComponentsInChildren<Image>(true).Length
                        : (go.GetComponent<Image>() != null ? 1 : 0);
                case ComponentType.Mesh:
                    return includeChildren
                        ? go.GetComponentsInChildren<MeshFilter>(true).Length
                        : (go.GetComponent<MeshFilter>() != null ? 1 : 0);
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 预览转换（不实际执行）
        /// Preview conversion without executing
        /// </summary>
        private void PreviewConversion(GameObject[] selection)
        {
            conversionRecords.Clear();

            string modeDesc = $"[预览] {GetComponentTypeName(sourceType)} → {GetComponentTypeName(targetType)}";

            foreach (var go in selection)
            {
                switch (sourceType)
                {
                    case ComponentType.Sprite:
                    {
                        SpriteRenderer[] renderers = includeChildren
                            ? go.GetComponentsInChildren<SpriteRenderer>(true)
                            : go.GetComponents<SpriteRenderer>();
                        foreach (var sr in renderers)
                        {
                            conversionRecords.Add(new ConversionRecord
                            {
                                gameObjectName = GetFullPath(sr.gameObject),
                                spriteName = sr.sprite != null ? sr.sprite.name : "(无)",
                                materialName = sr.sharedMaterial != null ? sr.sharedMaterial.name : "(无)",
                                success = true,
                                message = modeDesc
                            });
                        }
                        break;
                    }
                    case ComponentType.Image:
                    {
                        Image[] images = includeChildren
                            ? go.GetComponentsInChildren<Image>(true)
                            : go.GetComponents<Image>();
                        foreach (var img in images)
                        {
                            conversionRecords.Add(new ConversionRecord
                            {
                                gameObjectName = GetFullPath(img.gameObject),
                                spriteName = img.sprite != null ? img.sprite.name : "(无)",
                                materialName = img.material != null ? img.material.name : "(无)",
                                success = true,
                                message = modeDesc
                            });
                        }
                        break;
                    }
                    case ComponentType.Mesh:
                    {
                        MeshFilter[] filters = includeChildren
                            ? go.GetComponentsInChildren<MeshFilter>(true)
                            : go.GetComponents<MeshFilter>();
                        foreach (var mf in filters)
                        {
                            var mr = mf.GetComponent<MeshRenderer>();
                            conversionRecords.Add(new ConversionRecord
                            {
                                gameObjectName = GetFullPath(mf.gameObject),
                                spriteName = mf.sharedMesh != null ? mf.sharedMesh.name : "(无)",
                                materialName = (mr != null && mr.sharedMaterial != null) ? mr.sharedMaterial.name : "(无)",
                                success = true,
                                message = modeDesc
                            });
                        }
                        break;
                    }
                }
            }

            if (conversionRecords.Count == 0)
            {
                EditorUtility.DisplayDialog(TOOL_NAME, $"所选物体中未找到 {GetComponentTypeName(sourceType)} 组件", "确定");
            }
        }

        // ==================== 执行转换 ====================
        // ==================== Execute Conversion ====================

        /// <summary>
        /// 执行转换
        /// Execute conversion
        /// </summary>
        private void ExecuteConversion(GameObject[] selection)
        {
            conversionRecords.Clear();

            string undoName = $"{GetComponentTypeName(sourceType)} 转 {GetComponentTypeName(targetType)}";
            Undo.SetCurrentGroupName(undoName);
            int undoGroup = Undo.GetCurrentGroup();

            int successCount = 0;
            int failCount = 0;

            foreach (var go in selection)
            {
                switch (sourceType)
                {
                    case ComponentType.Sprite:
                    {
                        SpriteRenderer[] renderers = includeChildren
                            ? go.GetComponentsInChildren<SpriteRenderer>(true)
                            : go.GetComponents<SpriteRenderer>();
                        foreach (var sr in renderers)
                        {
                            var record = ConvertFromSpriteRenderer(sr);
                            conversionRecords.Add(record);
                            if (record.success) successCount++; else failCount++;
                        }
                        break;
                    }
                    case ComponentType.Image:
                    {
                        Image[] images = includeChildren
                            ? go.GetComponentsInChildren<Image>(true)
                            : go.GetComponents<Image>();
                        foreach (var img in images)
                        {
                            var record = ConvertFromImage(img);
                            conversionRecords.Add(record);
                            if (record.success) successCount++; else failCount++;
                        }
                        break;
                    }
                    case ComponentType.Mesh:
                    {
                        MeshFilter[] filters = includeChildren
                            ? go.GetComponentsInChildren<MeshFilter>(true)
                            : go.GetComponents<MeshFilter>();
                        foreach (var mf in filters)
                        {
                            var record = ConvertFromMesh(mf);
                            conversionRecords.Add(record);
                            if (record.success) successCount++; else failCount++;
                        }
                        break;
                    }
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            string resultMsg = $"转换完成！成功: {successCount}, 失败: {failCount}";
            EditorUtility.DisplayDialog(TOOL_NAME, resultMsg, "确定");
        }

        // ==================== 分发入口 ====================
        // ==================== Dispatch ====================

        private ConversionRecord ConvertFromSpriteRenderer(SpriteRenderer sr)
        {
            switch (targetType)
            {
                case ComponentType.Image: return ConvertSpriteRendererToImage(sr);
                case ComponentType.Mesh: return ConvertSpriteRendererToMesh(sr);
            }
            return FailRecord(sr != null ? sr.gameObject : null, "目标类型无效");
        }

        private ConversionRecord ConvertFromImage(Image img)
        {
            switch (targetType)
            {
                case ComponentType.Sprite: return ConvertImageToSpriteRenderer(img);
                case ComponentType.Mesh: return ConvertImageToMesh(img);
            }
            return FailRecord(img != null ? img.gameObject : null, "目标类型无效");
        }

        private ConversionRecord ConvertFromMesh(MeshFilter mf)
        {
            switch (targetType)
            {
                case ComponentType.Sprite: return ConvertMeshToSpriteRenderer(mf);
                case ComponentType.Image: return ConvertMeshToImage(mf);
            }
            return FailRecord(mf != null ? mf.gameObject : null, "目标类型无效");
        }

        private ConversionRecord FailRecord(GameObject go, string reason)
        {
            return new ConversionRecord
            {
                gameObjectName = go != null ? GetFullPath(go) : "(null)",
                success = false,
                message = reason
            };
        }

        // ==================== SpriteRenderer → Image ====================

        private ConversionRecord ConvertSpriteRendererToImage(SpriteRenderer sr)
        {
            var record = new ConversionRecord
            {
                gameObjectName = GetFullPath(sr.gameObject),
                spriteName = sr.sprite != null ? sr.sprite.name : "(无)",
                materialName = sr.sharedMaterial != null ? sr.sharedMaterial.name : "(无)"
            };

            try
            {
                GameObject targetGO = sr.gameObject;

                Sprite originalSprite = sr.sprite;
                Material originalMaterial = sr.sharedMaterial;
                Color originalColor = sr.color;
                int originalSortingOrder = sr.sortingOrder;
                string originalSortingLayer = sr.sortingLayerName;
                bool originalFlipX = sr.flipX;
                bool originalFlipY = sr.flipY;

                // 确保有 RectTransform
                // Ensure RectTransform exists
                RectTransform rectTransform = EnsureRectTransform(targetGO);

                // 自动添加 Canvas
                // Auto add Canvas
                if (addCanvasIfNeeded)
                {
                    Canvas parentCanvas = targetGO.GetComponentInParent<Canvas>();
                    if (parentCanvas == null && targetGO.GetComponent<Canvas>() == null)
                    {
                        Canvas canvas = Undo.AddComponent<Canvas>(targetGO);
                        canvas.renderMode = RenderMode.WorldSpace;
                        canvas.sortingOrder = originalSortingOrder;
                        canvas.sortingLayerName = originalSortingLayer;
                    }
                }

                if (removeOldComponent)
                    Undo.DestroyObjectImmediate(sr);

                Image image = targetGO.GetComponent<Image>();
                if (image == null)
                    image = Undo.AddComponent<Image>(targetGO);
                else
                    Undo.RecordObject(image, "Modify Image");

                image.sprite = originalSprite;
                image.color = originalColor;

                if (originalMaterial != null && originalMaterial.name != "Sprites-Default")
                    image.material = originalMaterial;

                if (preserveSize && originalSprite != null)
                {
                    rectTransform.sizeDelta = new Vector2(originalSprite.rect.width, originalSprite.rect.height);
                }

                if (originalFlipX || originalFlipY)
                {
                    Vector3 scale = rectTransform.localScale;
                    if (originalFlipX) scale.x = -Mathf.Abs(scale.x);
                    if (originalFlipY) scale.y = -Mathf.Abs(scale.y);
                    rectTransform.localScale = scale;
                }

                if (originalSprite != null)
                    image.SetNativeSize();

                record.success = true;
                record.message = "转换成功 (SpriteRenderer → Image)";
                EditorUtility.SetDirty(targetGO);
            }
            catch (System.Exception ex)
            {
                record.success = false;
                record.message = $"转换失败: {ex.Message}";
                Debug.LogError($"[{TOOL_NAME}] {record.gameObjectName} 转换失败: {ex}");
            }

            return record;
        }

        // ==================== SpriteRenderer → Mesh ====================

        private ConversionRecord ConvertSpriteRendererToMesh(SpriteRenderer sr)
        {
            var record = new ConversionRecord
            {
                gameObjectName = GetFullPath(sr.gameObject),
                spriteName = sr.sprite != null ? sr.sprite.name : "(无)",
                materialName = sr.sharedMaterial != null ? sr.sharedMaterial.name : "(无)"
            };

            try
            {
                GameObject targetGO = sr.gameObject;

                Sprite originalSprite = sr.sprite;
                Material originalMaterial = sr.sharedMaterial;
                Color originalColor = sr.color;
                bool originalFlipX = sr.flipX;
                bool originalFlipY = sr.flipY;

                // 判断源材质是否有效（非空且非Unity默认材质）
                // Check if source material is valid (not null and not Unity default)
                bool hasValidMaterial = originalMaterial != null && originalMaterial.name != "Sprites-Default";

                // 计算目标尺寸（基于 Sprite PPU），仅当有有效材质时才换算
                // Compute target size via PPU, only when material is valid
                Vector2 targetSize = Vector2.one;
                if (originalSprite != null)
                {
                    float ppu = originalSprite.pixelsPerUnit > 0 ? originalSprite.pixelsPerUnit : 100f;
                    targetSize = new Vector2(originalSprite.rect.width / ppu, originalSprite.rect.height / ppu);
                }

                if (removeOldComponent)
                    Undo.DestroyObjectImmediate(sr);

                // 添加 MeshFilter + MeshRenderer
                // Add MeshFilter + MeshRenderer
                MeshFilter mf = targetGO.GetComponent<MeshFilter>();
                if (mf == null) mf = Undo.AddComponent<MeshFilter>(targetGO);
                else Undo.RecordObject(mf, "Modify MeshFilter");

                MeshRenderer mr = targetGO.GetComponent<MeshRenderer>();
                if (mr == null) mr = Undo.AddComponent<MeshRenderer>(targetGO);
                else Undo.RecordObject(mr, "Modify MeshRenderer");

                mf.sharedMesh = GetQuadMesh();

                // 材质：源材质有效则继承；源材质为空则保持 None（不自动生成）
                // Material: inherit if valid; otherwise leave None (do NOT auto-generate)
                if (hasValidMaterial)
                    mr.sharedMaterial = originalMaterial;
                else
                    mr.sharedMaterial = null;

                // 尺寸：仅当有有效材质时按 PPU 换算；否则保持原 scale 不变
                // Size: only rescale when material is valid; otherwise keep original scale unchanged
                if (hasValidMaterial)
                {
                    Vector3 scale = targetGO.transform.localScale;
                    if (preserveSize)
                    {
                        scale.x = Mathf.Abs(scale.x) * targetSize.x;
                        scale.y = Mathf.Abs(scale.y) * targetSize.y;
                    }
                    else
                    {
                        scale.x = Mathf.Abs(scale.x);
                        scale.y = Mathf.Abs(scale.y);
                    }
                    if (originalFlipX) scale.x = -scale.x;
                    if (originalFlipY) scale.y = -scale.y;
                    Undo.RecordObject(targetGO.transform, "Apply Scale");
                    targetGO.transform.localScale = scale;
                }
                else if (originalFlipX || originalFlipY)
                {
                    // 即便不改尺寸，也需要迁移翻转信息
                    // Still migrate flip info even when size is unchanged
                    Vector3 scale = targetGO.transform.localScale;
                    if (originalFlipX) scale.x = -Mathf.Abs(scale.x);
                    if (originalFlipY) scale.y = -Mathf.Abs(scale.y);
                    Undo.RecordObject(targetGO.transform, "Apply Flip");
                    targetGO.transform.localScale = scale;
                }

                // 颜色传递（仅当材质有效时）
                // Pass color only when material is valid
                if (hasValidMaterial)
                    ApplyColorToMaterial(mr, originalColor);

                record.success = true;
                record.message = "转换成功 (SpriteRenderer → Mesh)";
                EditorUtility.SetDirty(targetGO);
            }
            catch (System.Exception ex)
            {
                record.success = false;
                record.message = $"转换失败: {ex.Message}";
                Debug.LogError($"[{TOOL_NAME}] {record.gameObjectName} 转换失败: {ex}");
            }

            return record;
        }

        // ==================== Image → SpriteRenderer ====================

        private ConversionRecord ConvertImageToSpriteRenderer(Image img)
        {
            var record = new ConversionRecord
            {
                gameObjectName = GetFullPath(img.gameObject),
                spriteName = img.sprite != null ? img.sprite.name : "(无)",
                materialName = img.material != null ? img.material.name : "(无)"
            };

            try
            {
                GameObject targetGO = img.gameObject;

                Sprite originalSprite = img.sprite;
                Material originalMaterial = img.material;
                Color originalColor = img.color;

                int sortingOrder = 0;
                string sortingLayer = "Default";
                Canvas canvas = targetGO.GetComponent<Canvas>();
                if (canvas != null)
                {
                    sortingOrder = canvas.sortingOrder;
                    sortingLayer = canvas.sortingLayerName;
                }
                else
                {
                    Canvas parentCanvas = targetGO.GetComponentInParent<Canvas>();
                    if (parentCanvas != null)
                    {
                        sortingOrder = parentCanvas.sortingOrder;
                        sortingLayer = parentCanvas.sortingLayerName;
                    }
                }

                Vector3 localScale = targetGO.transform.localScale;
                bool flipX = localScale.x < 0;
                bool flipY = localScale.y < 0;

                if (removeOldComponent)
                {
                    Undo.DestroyObjectImmediate(img);

                    CanvasRenderer canvasRenderer = targetGO.GetComponent<CanvasRenderer>();
                    if (canvasRenderer != null)
                        Undo.DestroyObjectImmediate(canvasRenderer);

                    if (canvas != null)
                    {
                        Graphic[] remainingGraphics = targetGO.GetComponentsInChildren<Graphic>(true);
                        if (remainingGraphics == null || remainingGraphics.Length == 0)
                            Undo.DestroyObjectImmediate(canvas);
                    }
                }

                SpriteRenderer spriteRenderer = targetGO.GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                    spriteRenderer = Undo.AddComponent<SpriteRenderer>(targetGO);
                else
                    Undo.RecordObject(spriteRenderer, "Modify SpriteRenderer");

                spriteRenderer.sprite = originalSprite;
                spriteRenderer.color = originalColor;
                spriteRenderer.sortingOrder = sortingOrder;
                spriteRenderer.sortingLayerName = sortingLayer;

                if (originalMaterial != null
                    && originalMaterial.name != "Default UI Material"
                    && originalMaterial.name != "UI/Default")
                {
                    spriteRenderer.sharedMaterial = originalMaterial;
                }

                spriteRenderer.flipX = flipX;
                spriteRenderer.flipY = flipY;

                if (flipX || flipY)
                {
                    Undo.RecordObject(targetGO.transform, "Fix Scale");
                    Vector3 fixedScale = localScale;
                    if (flipX) fixedScale.x = Mathf.Abs(fixedScale.x);
                    if (flipY) fixedScale.y = Mathf.Abs(fixedScale.y);
                    targetGO.transform.localScale = fixedScale;
                }

                record.success = true;
                record.message = "转换成功 (Image → SpriteRenderer)";
                EditorUtility.SetDirty(targetGO);
            }
            catch (System.Exception ex)
            {
                record.success = false;
                record.message = $"转换失败: {ex.Message}";
                Debug.LogError($"[{TOOL_NAME}] {record.gameObjectName} 转换失败: {ex}");
            }

            return record;
        }

        // ==================== Image → Mesh ====================

        private ConversionRecord ConvertImageToMesh(Image img)
        {
            var record = new ConversionRecord
            {
                gameObjectName = GetFullPath(img.gameObject),
                spriteName = img.sprite != null ? img.sprite.name : "(无)",
                materialName = img.material != null ? img.material.name : "(无)"
            };

            try
            {
                GameObject targetGO = img.gameObject;

                Sprite originalSprite = img.sprite;
                Material originalMaterial = img.material;
                Color originalColor = img.color;

                // 判断源材质是否有效（非空且非Unity UI默认材质）
                // Check if source material is valid
                bool hasValidMaterial = originalMaterial != null
                    && originalMaterial.name != "Default UI Material"
                    && originalMaterial.name != "UI/Default";

                // 基于 RectTransform sizeDelta 计算目标尺寸（仅在材质有效时使用）
                // Compute target size from RectTransform sizeDelta (only used when material is valid)
                RectTransform rt = img.rectTransform;
                Vector2 rectSize = rt != null ? rt.sizeDelta : Vector2.one;
                float ppu = 100f;
                if (originalSprite != null && originalSprite.pixelsPerUnit > 0)
                    ppu = originalSprite.pixelsPerUnit;
                Vector2 targetSize = rectSize / ppu;
                if (targetSize.x <= 0.0001f) targetSize.x = 1f;
                if (targetSize.y <= 0.0001f) targetSize.y = 1f;

                Vector3 localScale = targetGO.transform.localScale;
                bool flipX = localScale.x < 0;
                bool flipY = localScale.y < 0;

                if (removeOldComponent)
                {
                    Undo.DestroyObjectImmediate(img);

                    CanvasRenderer canvasRenderer = targetGO.GetComponent<CanvasRenderer>();
                    if (canvasRenderer != null)
                        Undo.DestroyObjectImmediate(canvasRenderer);

                    Canvas canvas = targetGO.GetComponent<Canvas>();
                    if (canvas != null)
                    {
                        Graphic[] remainingGraphics = targetGO.GetComponentsInChildren<Graphic>(true);
                        if (remainingGraphics == null || remainingGraphics.Length == 0)
                            Undo.DestroyObjectImmediate(canvas);
                    }

                    // Mesh 不依赖 RectTransform，但由于 RectTransform 无法直接移除（Unity 会自动保留 Transform 基类）
                    // 这里保留 RectTransform 以避免组件丢失问题
                }

                MeshFilter mf = targetGO.GetComponent<MeshFilter>();
                if (mf == null) mf = Undo.AddComponent<MeshFilter>(targetGO);
                else Undo.RecordObject(mf, "Modify MeshFilter");

                MeshRenderer mr = targetGO.GetComponent<MeshRenderer>();
                if (mr == null) mr = Undo.AddComponent<MeshRenderer>(targetGO);
                else Undo.RecordObject(mr, "Modify MeshRenderer");

                mf.sharedMesh = GetQuadMesh();

                // 材质：源材质有效则继承；源材质为空则保持 None（不自动生成）
                // Material: inherit if valid; otherwise leave None (do NOT auto-generate)
                if (hasValidMaterial)
                    mr.sharedMaterial = originalMaterial;
                else
                    mr.sharedMaterial = null;

                // 尺寸：仅当有有效材质时按 PPU 换算；否则保持原 scale 不变
                // Size: only rescale when material is valid; otherwise keep original scale unchanged
                if (hasValidMaterial)
                {
                    Vector3 scale = localScale;
                    if (preserveSize)
                    {
                        scale.x = Mathf.Abs(scale.x) * targetSize.x;
                        scale.y = Mathf.Abs(scale.y) * targetSize.y;
                    }
                    else
                    {
                        scale.x = Mathf.Abs(scale.x);
                        scale.y = Mathf.Abs(scale.y);
                    }
                    if (flipX) scale.x = -scale.x;
                    if (flipY) scale.y = -scale.y;
                    Undo.RecordObject(targetGO.transform, "Apply Scale");
                    targetGO.transform.localScale = scale;

                    ApplyColorToMaterial(mr, originalColor);
                }
                // 无有效材质：完全不改 scale，保持原样
                // No valid material: leave localScale untouched

                record.success = true;
                record.message = "转换成功 (Image → Mesh)";
                EditorUtility.SetDirty(targetGO);
            }
            catch (System.Exception ex)
            {
                record.success = false;
                record.message = $"转换失败: {ex.Message}";
                Debug.LogError($"[{TOOL_NAME}] {record.gameObjectName} 转换失败: {ex}");
            }

            return record;
        }

        // ==================== Mesh → SpriteRenderer ====================

        private ConversionRecord ConvertMeshToSpriteRenderer(MeshFilter mf)
        {
            var mr = mf.GetComponent<MeshRenderer>();
            var record = new ConversionRecord
            {
                gameObjectName = GetFullPath(mf.gameObject),
                spriteName = mf.sharedMesh != null ? mf.sharedMesh.name : "(无)",
                materialName = (mr != null && mr.sharedMaterial != null) ? mr.sharedMaterial.name : "(无)"
            };

            try
            {
                GameObject targetGO = mf.gameObject;

                Material originalMaterial = mr != null ? mr.sharedMaterial : null;
                Sprite guessedSprite = TryFindSpriteFromMaterial(originalMaterial);
                Color originalColor = GetMaterialColor(originalMaterial, Color.white);

                Vector3 localScale = targetGO.transform.localScale;
                bool flipX = localScale.x < 0;
                bool flipY = localScale.y < 0;

                if (removeOldComponent)
                {
                    if (mr != null) Undo.DestroyObjectImmediate(mr);
                    Undo.DestroyObjectImmediate(mf);
                }

                SpriteRenderer spriteRenderer = targetGO.GetComponent<SpriteRenderer>();
                if (spriteRenderer == null) spriteRenderer = Undo.AddComponent<SpriteRenderer>(targetGO);
                else Undo.RecordObject(spriteRenderer, "Modify SpriteRenderer");

                spriteRenderer.sprite = guessedSprite;
                spriteRenderer.color = originalColor;

                if (originalMaterial != null)
                    spriteRenderer.sharedMaterial = originalMaterial;

                spriteRenderer.flipX = flipX;
                spriteRenderer.flipY = flipY;

                // 尺寸从 scale 恢复
                // Restore size from scale
                Vector3 fixedScale = localScale;
                fixedScale.x = Mathf.Abs(fixedScale.x);
                fixedScale.y = Mathf.Abs(fixedScale.y);
                if (!preserveSize)
                {
                    fixedScale.x = 1f;
                    fixedScale.y = 1f;
                }
                else if (guessedSprite != null)
                {
                    // Mesh Quad 的实际视觉大小 = localScale × 1(单位Quad)
                    // SpriteRenderer 基于 PPU 自动计算大小，需要把 scale 规整为 1
                    // Mesh Quad visual size = localScale × 1; SpriteRenderer auto-sizes via PPU, so normalize scale to 1
                    fixedScale.x = 1f;
                    fixedScale.y = 1f;
                    fixedScale.z = Mathf.Abs(localScale.z);
                }
                if (flipX) fixedScale.x = -fixedScale.x;
                if (flipY) fixedScale.y = -fixedScale.y;

                // 翻转 SpriteRenderer 通过 flipX/Y 表达，scale 恢复正值
                // SpriteRenderer uses flipX/Y for flipping, scale stays positive
                fixedScale.x = Mathf.Abs(fixedScale.x);
                fixedScale.y = Mathf.Abs(fixedScale.y);
                Undo.RecordObject(targetGO.transform, "Fix Scale");
                targetGO.transform.localScale = fixedScale;

                record.success = true;
                record.message = guessedSprite != null
                    ? "转换成功 (Mesh → SpriteRenderer)"
                    : "转换成功 (Mesh → SpriteRenderer)，未能反查到 Sprite";
                EditorUtility.SetDirty(targetGO);
            }
            catch (System.Exception ex)
            {
                record.success = false;
                record.message = $"转换失败: {ex.Message}";
                Debug.LogError($"[{TOOL_NAME}] {record.gameObjectName} 转换失败: {ex}");
            }

            return record;
        }

        // ==================== Mesh → Image ====================

        private ConversionRecord ConvertMeshToImage(MeshFilter mf)
        {
            var mr = mf.GetComponent<MeshRenderer>();
            var record = new ConversionRecord
            {
                gameObjectName = GetFullPath(mf.gameObject),
                spriteName = mf.sharedMesh != null ? mf.sharedMesh.name : "(无)",
                materialName = (mr != null && mr.sharedMaterial != null) ? mr.sharedMaterial.name : "(无)"
            };

            try
            {
                GameObject targetGO = mf.gameObject;

                Material originalMaterial = mr != null ? mr.sharedMaterial : null;
                Sprite guessedSprite = TryFindSpriteFromMaterial(originalMaterial);
                Color originalColor = GetMaterialColor(originalMaterial, Color.white);

                Vector3 localScale = targetGO.transform.localScale;
                bool flipX = localScale.x < 0;
                bool flipY = localScale.y < 0;
                Vector3 absScale = new Vector3(Mathf.Abs(localScale.x), Mathf.Abs(localScale.y), Mathf.Abs(localScale.z));

                RectTransform rectTransform = EnsureRectTransform(targetGO);

                if (addCanvasIfNeeded)
                {
                    Canvas parentCanvas = targetGO.GetComponentInParent<Canvas>();
                    if (parentCanvas == null && targetGO.GetComponent<Canvas>() == null)
                    {
                        Canvas canvas = Undo.AddComponent<Canvas>(targetGO);
                        canvas.renderMode = RenderMode.WorldSpace;
                    }
                }

                if (removeOldComponent)
                {
                    if (mr != null) Undo.DestroyObjectImmediate(mr);
                    Undo.DestroyObjectImmediate(mf);
                }

                Image image = targetGO.GetComponent<Image>();
                if (image == null) image = Undo.AddComponent<Image>(targetGO);
                else Undo.RecordObject(image, "Modify Image");

                image.sprite = guessedSprite;
                image.color = originalColor;

                if (originalMaterial != null)
                    image.material = originalMaterial;

                // 尺寸：Mesh Quad 基础尺寸为 1，scale 即为世界大小
                // Size: base Quad is 1 unit, scale equals world size
                if (preserveSize && guessedSprite != null)
                {
                    float ppu = guessedSprite.pixelsPerUnit > 0 ? guessedSprite.pixelsPerUnit : 100f;
                    rectTransform.sizeDelta = new Vector2(absScale.x * ppu, absScale.y * ppu);

                    Vector3 newScale = new Vector3(1f, 1f, absScale.z);
                    if (flipX) newScale.x = -1f;
                    if (flipY) newScale.y = -1f;
                    Undo.RecordObject(targetGO.transform, "Fix Scale");
                    targetGO.transform.localScale = newScale;
                }
                else if (preserveSize)
                {
                    rectTransform.sizeDelta = new Vector2(absScale.x * 100f, absScale.y * 100f);
                    Vector3 newScale = new Vector3(1f, 1f, absScale.z);
                    if (flipX) newScale.x = -1f;
                    if (flipY) newScale.y = -1f;
                    Undo.RecordObject(targetGO.transform, "Fix Scale");
                    targetGO.transform.localScale = newScale;
                }

                if (guessedSprite != null && !preserveSize)
                    image.SetNativeSize();

                record.success = true;
                record.message = guessedSprite != null
                    ? "转换成功 (Mesh → Image)"
                    : "转换成功 (Mesh → Image)，未能反查到 Sprite";
                EditorUtility.SetDirty(targetGO);
            }
            catch (System.Exception ex)
            {
                record.success = false;
                record.message = $"转换失败: {ex.Message}";
                Debug.LogError($"[{TOOL_NAME}] {record.gameObjectName} 转换失败: {ex}");
            }

            return record;
        }

        // ==================== 辅助工具 ====================
        // ==================== Helpers ====================

        /// <summary>
        /// 确保 GameObject 有 RectTransform
        /// Ensure GameObject has a RectTransform
        /// </summary>
        private RectTransform EnsureRectTransform(GameObject go)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt != null) return rt;

            Vector3 localPos = go.transform.localPosition;
            Quaternion localRot = go.transform.localRotation;
            Vector3 localScale = go.transform.localScale;

            rt = Undo.AddComponent<RectTransform>(go);
            rt.localPosition = localPos;
            rt.localRotation = localRot;
            rt.localScale = localScale;
            return rt;
        }

        /// <summary>
        /// 获取内置 Quad 网格（带缓存）
        /// Get built-in Quad mesh with caching
        /// </summary>
        private Mesh GetQuadMesh()
        {
            if (_cachedQuadMesh != null) return _cachedQuadMesh;

            // 优先尝试从内置资源加载
            // Prefer built-in resource first
            Mesh builtin = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            if (builtin != null)
            {
                _cachedQuadMesh = builtin;
                return _cachedQuadMesh;
            }

            // 兜底：创建临时 Quad 提取网格
            // Fallback: create temporary Quad and extract mesh
            GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _cachedQuadMesh = tempQuad.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(tempQuad);
            return _cachedQuadMesh;
        }

        /// <summary>
        /// 从材质贴图反查 Sprite
        /// Try to find a Sprite from material's main texture
        /// </summary>
        private Sprite TryFindSpriteFromMaterial(Material mat)
        {
            if (mat == null) return null;

            Texture tex = null;
            if (mat.HasProperty("_MainTex")) tex = mat.GetTexture("_MainTex");
            if (tex == null && mat.HasProperty("_BaseMap")) tex = mat.GetTexture("_BaseMap");
            if (tex == null) return null;

            string texPath = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(texPath)) return null;

            // 该贴图可能本身是 Sprite 资源
            // The texture itself may be a Sprite asset
            Sprite directSprite = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
            if (directSprite != null) return directSprite;

            // 扫描该路径下所有 Sprite 子资源，取第一个
            // Scan all sub-Sprite assets at the path, return the first
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(texPath);
            foreach (var asset in allAssets)
            {
                if (asset is Sprite sp) return sp;
            }

            return null;
        }

        /// <summary>
        /// 从材质获取颜色
        /// Get color from material
        /// </summary>
        private Color GetMaterialColor(Material mat, Color fallback)
        {
            if (mat == null) return fallback;
            if (mat.HasProperty("_Color")) return mat.GetColor("_Color");
            if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
            if (mat.HasProperty("_TintColor")) return mat.GetColor("_TintColor");
            return fallback;
        }

        /// <summary>
        /// 将颜色应用到 MeshRenderer 的材质实例
        /// Apply color to MeshRenderer's material instance
        /// </summary>
        private void ApplyColorToMaterial(MeshRenderer mr, Color color)
        {
            if (mr == null || mr.sharedMaterial == null) return;
            if (color == Color.white) return;

            // 使用 MaterialPropertyBlock 避免修改共享材质
            // Use MaterialPropertyBlock to avoid modifying shared material
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            mr.GetPropertyBlock(mpb);
            if (mr.sharedMaterial.HasProperty("_Color"))
                mpb.SetColor("_Color", color);
            else if (mr.sharedMaterial.HasProperty("_BaseColor"))
                mpb.SetColor("_BaseColor", color);
            else if (mr.sharedMaterial.HasProperty("_TintColor"))
                mpb.SetColor("_TintColor", color);
            mr.SetPropertyBlock(mpb);
        }

        /// <summary>
        /// 创建默认的 Unlit 材质
        /// Create a default Unlit material
        /// </summary>
        private Material CreateDefaultUnlitMaterial(Texture mainTex)
        {
            Shader shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader) { name = "Moshi_GeneratedMat" };
            if (mainTex != null)
            {
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", mainTex);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
            }
            return mat;
        }

        /// <summary>
        /// 获取物体完整路径
        /// Get full hierarchy path of GameObject
        /// </summary>
        private string GetFullPath(GameObject go)
        {
            string path = go.name;
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
