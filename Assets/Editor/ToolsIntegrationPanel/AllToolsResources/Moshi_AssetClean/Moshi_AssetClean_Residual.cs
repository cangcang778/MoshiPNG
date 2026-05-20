using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Timeline;
using UnityEngine.Playables;

namespace MoshiTools
{
    /// <summary>
    /// 资源清理工具 - 残留清理模块
    /// Asset Cleaner - Residual cleanup module
    /// </summary>
    public partial class Moshi_AssetClean
    {
        #region 残留类型枚举

        /// <summary>
        /// 残留类型
        /// Residual type
        /// </summary>
        public enum ResidualType
        {
            // Mesh残留类型
            MeshRenderer,           // 渲染模式非Mesh但有Mesh引用
            MeshShape,              // Shape模块禁用或类型不匹配但有Mesh引用
            
            // Material残留类型
            MaterialTrail,          // Trail模块禁用但有拖尾材质
            MaterialMissing,        // 材质文件丢失
            
            // 贴图残留类型
            TextureDisabledModule,  // 禁用模块中的材质贴图
            TextureShaderMismatch,  // Shader不需要的贴图属性
            
            // Timeline残留类型
            TimelineBindingMissing,    // 轨道绑定对象丢失
            TimelineClipAssetMissing,  // Clip引用的资源丢失
            TimelineExposedRefMissing, // ExposedReference引用丢失
            TimelineOrphanTrack,       // 孤立轨道（存在于文件中但不在m_Tracks列表）

            // FBX残留类型
            // FBX residual type
            FbxExternalMaterialMissing // FBX externalObjects 指向已不存在的材质 GUID
        }

        #endregion

        #region 残留数据类

        /// <summary>
        /// 残留项基类
        /// Residual item base class
        /// </summary>
        public abstract class ResidualItem
        {
            public GameObject gameObject;
            public string hierarchyPath;
            public ResidualType residualType;
            public string description;
            public bool isSelected = true;

            public abstract void Clean();
            public abstract string GetDetailInfo();
        }

        /// <summary>
        /// Mesh残留项
        /// Mesh residual item
        /// </summary>
        public class MeshResidualItem : ResidualItem
        {
            public ParticleSystem particleSystem;
            public ParticleSystemRenderer renderer;
            public Mesh mesh;
            public bool isShapeMesh;
            public int meshIndex = -1;

            public override void Clean()
            {
                if (particleSystem == null) return;

                Undo.RecordObject(particleSystem, "清理Mesh残留");

                if (isShapeMesh)
                {
                    var shape = particleSystem.shape;
                    shape.mesh = null;
                }
                else if (renderer != null)
                {
                    Undo.RecordObject(renderer, "清理Mesh残留");

                    if (meshIndex >= 0)
                    {
                        var meshes = new Mesh[4];
                        var count = renderer.GetMeshes(meshes);
                        if (meshIndex < count)
                        {
                            meshes[meshIndex] = null;
                            renderer.SetMeshes(meshes.Take(count).Where(m => m != null).ToArray());
                        }
                    }
                    else
                    {
                        renderer.mesh = null;
                        renderer.SetMeshes(new Mesh[0]);
                    }

                    EditorUtility.SetDirty(renderer);
                }

                EditorUtility.SetDirty(particleSystem);
            }

            public override string GetDetailInfo()
            {
                string meshName = mesh != null ? mesh.name : "null";
                return isShapeMesh
                    ? $"Shape模块Mesh: {meshName}"
                    : $"Renderer Mesh{(meshIndex >= 0 ? $"[{meshIndex}]" : "")}: {meshName}";
            }
        }

        /// <summary>
        /// Material残留项
        /// Material residual item
        /// </summary>
        public class MaterialResidualItem : ResidualItem
        {
            public ParticleSystem particleSystem;
            public ParticleSystemRenderer renderer;
            public Material material;
            public bool isTrailMaterial;
            public int materialIndex = -1;

            public override void Clean()
            {
                if (renderer == null) return;

                Undo.RecordObject(renderer, "清理Material残留");

                if (isTrailMaterial)
                {
                    renderer.trailMaterial = null;
                }
                else if (materialIndex >= 0)
                {
                    var materials = renderer.sharedMaterials;
                    if (materialIndex < materials.Length)
                    {
                        materials[materialIndex] = null;
                        renderer.sharedMaterials = materials;
                    }
                }

                EditorUtility.SetDirty(renderer);
            }

            public override string GetDetailInfo()
            {
                string matName = material != null ? material.name : "Missing";
                return isTrailMaterial
                    ? $"拖尾材质: {matName}"
                    : $"材质[{materialIndex}]: {matName}";
            }
        }

        /// <summary>
        /// 贴图残留项
        /// Texture residual item
        /// </summary>
        public class TextureResidualItem : ResidualItem
        {
            public Material material;
            public string propertyName;
            public Texture texture;
            public string reason;

            public override void Clean()
            {
                if (material == null) return;

                Undo.RecordObject(material, "清理贴图残留");
                material.SetTexture(propertyName, null);
                EditorUtility.SetDirty(material);
            }

            public override string GetDetailInfo()
            {
                string texName = texture != null ? texture.name : "null";
                return $"属性 {propertyName}: {texName} - {reason}";
            }
        }

        /// <summary>
        /// Timeline残留项
        /// Timeline residual item
        /// </summary>
        public class TimelineResidualItem : ResidualItem
        {
            public PlayableDirector director;
            public TimelineAsset timelineAsset;
            public TrackAsset track;
            public string trackName;
            public string bindingPropertyPath;
            public int bindingIndex = -1;
            // 孤立子对象引用（用于 TimelineOrphanTrack 清理）
            // Orphan sub-asset reference (for TimelineOrphanTrack cleanup)
            public UnityEngine.Object orphanSubAsset;

            public override void Clean()
            {
                switch (residualType)
                {
                    case ResidualType.TimelineBindingMissing:
                        if (director == null) return;
                        CleanBindingMissing();
                        break;
                    case ResidualType.TimelineClipAssetMissing:
                        Debug.LogWarning($"[资源清理] Timeline Clip资源丢失需手动处理: {(director != null ? director.name : "null")} → {trackName}");
                        break;
                    case ResidualType.TimelineExposedRefMissing:
                        if (director == null) return;
                        CleanExposedRefMissing();
                        break;
                    case ResidualType.TimelineOrphanTrack:
                        CleanOrphanTrack();
                        break;
                }
            }

            private void CleanBindingMissing()
            {
                var so = new SerializedObject(director);
                var bindings = so.FindProperty("m_SceneBindings");
                if (bindings == null) return;

                Undo.RecordObject(director, "清理Timeline绑定残留");

                if (bindingIndex >= 0 && bindingIndex < bindings.arraySize)
                {
                    var element = bindings.GetArrayElementAtIndex(bindingIndex);
                    var key = element.FindPropertyRelative("key");

                    // 判断是 key 丢失还是 value 丢失
                    // Determine if it's key missing or value missing
                    bool keyMissing = key != null &&
                                      key.objectReferenceValue == null &&
                                      key.objectReferenceInstanceIDValue != 0;

                    if (keyMissing)
                    {
                        // key 丢失 → 整条绑定记录无效，直接从数组中删除
                        // key is missing → entire binding entry is invalid, remove from array
                        bindings.DeleteArrayElementAtIndex(bindingIndex);
                    }
                    else
                    {
                        // key 正常但 value 丢失 → 只清空 value
                        // key is valid but value missing → only clear value
                        var value = element.FindPropertyRelative("value");
                        if (value != null)
                        {
                            value.objectReferenceInstanceIDValue = 0;
                            value.objectReferenceValue = null;
                        }
                    }

                    so.ApplyModifiedProperties();
                }

                EditorUtility.SetDirty(director);
            }

            private void CleanExposedRefMissing()
            {
                var so = new SerializedObject(director);
                var refs = so.FindProperty("m_ExposedReferences.m_References");
                if (refs == null) return;

                Undo.RecordObject(director, "清理Timeline ExposedRef残留");

                for (int i = refs.arraySize - 1; i >= 0; i--)
                {
                    var element = refs.GetArrayElementAtIndex(i);
                    var key = element.FindPropertyRelative("first");
                    if (key != null && key.stringValue == bindingPropertyPath)
                    {
                        refs.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(director);
            }

            /// <summary>
            /// 清理孤立轨道 - 从.playable文件中删除孤立的子对象
            /// Clean orphan track - remove orphan sub-asset from .playable file
            /// </summary>
            private void CleanOrphanTrack()
            {
                if (timelineAsset == null || orphanSubAsset == null) return;

                string assetPath = AssetDatabase.GetAssetPath(timelineAsset);
                if (string.IsNullOrEmpty(assetPath)) return;

                Undo.RecordObject(timelineAsset, "清理Timeline孤立轨道");

                // 使用 AssetDatabase 从资源文件中移除孤立子对象
                // Use AssetDatabase to remove orphan sub-asset from the asset file
                AssetDatabase.RemoveObjectFromAsset(orphanSubAsset);

                // 安全销毁对象
                // Safely destroy the object
                UnityEngine.Object.DestroyImmediate(orphanSubAsset, true);

                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath);

                EditorUtility.SetDirty(timelineAsset);

                Debug.Log($"[资源清理] 已删除孤立轨道: {trackName} (from {assetPath})");
            }

            public override string GetDetailInfo()
            {
                switch (residualType)
                {
                    case ResidualType.TimelineBindingMissing:
                        return $"轨道绑定丢失: {trackName}";
                    case ResidualType.TimelineClipAssetMissing:
                        return $"Clip资源丢失: {trackName}";
                    case ResidualType.TimelineExposedRefMissing:
                        return $"ExposedRef丢失: {bindingPropertyPath}";
                    case ResidualType.TimelineOrphanTrack:
                        return $"孤立轨道: {trackName}";
                    default:
                        return description;
                }
            }
        }

        /// <summary>
        /// FBX残留项 - externalObjects 指向不存在的材质
        /// FBX residual item - externalObjects pointing to missing material GUID
        /// </summary>
        public class FbxResidualItem : ResidualItem
        {
            public string fbxPath;                                     // FBX 资源路径
            public string fbxGuid;                                     // FBX 自身 GUID
            public string missingGuid;                                 // 丢失的材质 GUID
            public List<string> materialSlots = new List<string>();   // 映射的材质槽名称（Material #27/#28...）

            public override void Clean()
            {
                if (string.IsNullOrEmpty(fbxPath)) return;

                var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"[资源清理] 无法获取 ModelImporter: {fbxPath}");
                    return;
                }

                // 移除所有 value 为 null 的 externalObjects 映射（僵尸依赖）
                // Remove all externalObjects mappings whose value is null (zombie dependencies)
                var externals = importer.GetExternalObjectMap();
                int removedCount = 0;

                foreach (var kvp in externals)
                {
                    if (kvp.Value == null)
                    {
                        importer.RemoveRemap(kvp.Key);
                        removedCount++;
                    }
                }

                if (removedCount > 0)
                {
                    importer.SaveAndReimport();
                    Debug.Log($"[资源清理] 已清除 {removedCount} 条 Missing External Remap: {fbxPath}");
                }
            }

            public override string GetDetailInfo()
            {
                string slots = materialSlots.Count > 0 ? string.Join(", ", materialSlots) : "—";
                string guidShort = string.IsNullOrEmpty(missingGuid) || missingGuid.Length < 8
                    ? missingGuid
                    : missingGuid.Substring(0, 8) + "...";
                return $"丢失GUID: {guidShort}  →  映射槽: {slots}";
            }
        }

        /// <summary>
        /// 扫描结果容器
        /// Scan result container
        /// </summary>
        public class ScanResult
        {
            public List<MeshResidualItem> meshResiduals = new List<MeshResidualItem>();
            public List<MaterialResidualItem> materialResiduals = new List<MaterialResidualItem>();
            public List<TextureResidualItem> textureResiduals = new List<TextureResidualItem>();
            public List<TimelineResidualItem> timelineResiduals = new List<TimelineResidualItem>();
            public List<FbxResidualItem> fbxResiduals = new List<FbxResidualItem>();

            public int TotalCount => meshResiduals.Count + materialResiduals.Count + textureResiduals.Count + timelineResiduals.Count + fbxResiduals.Count;
            public int SelectedCount => meshResiduals.Count(m => m.isSelected) +
                                        materialResiduals.Count(m => m.isSelected) +
                                        textureResiduals.Count(m => m.isSelected) +
                                        timelineResiduals.Count(m => m.isSelected) +
                                        fbxResiduals.Count(m => m.isSelected);

            public void Clear()
            {
                meshResiduals.Clear();
                materialResiduals.Clear();
                textureResiduals.Clear();
                timelineResiduals.Clear();
                fbxResiduals.Clear();
            }
        }

        #endregion

        #region 残留清理字段

        private ScanResult residualScanResult = new ScanResult();

        // 检测类型开关
        // Detection type toggles
        private bool scanMesh = true;
        private bool scanMaterial = true;
        private bool scanTexture = true;
        private bool scanTimeline = true;
        private bool scanFbx = true;

        // UI状态
        // UI state
        private Vector2 residualScrollPos;
        private bool showMeshFoldout = true;
        private bool showMaterialFoldout = true;
        private bool showTextureFoldout = true;
        private bool showTimelineFoldout = true;
        private bool showFbxFoldout = true;

        #endregion

        #region 初始化

        partial void InitResidualModule()
        {
            // 初始化完成
            // Initialization complete
        }

        #endregion

        #region 残留清理标签页绘制

        /// <summary>
        /// 绘制残留清理标签页
        /// Draw residual cleanup tab
        /// </summary>
        private void DrawResidualTab()
        {
            // 说明区域
            // Description area
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("功能说明", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("检测预制体/场景/资源中未被引用的残留资源组件：", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("• Mesh残留 → MeshFilter/MeshRenderer 引用的 Mesh 为空或丢失", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Material残留 → 渲染器上的材质球为空或丢失", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• 贴图残留 → 材质球引用的贴图为空或丢失", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Timeline残留 → Timeline轨道中绑定对象丢失 / 孤立轨道清理", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• FBX残留 → FBX.meta 中外部材质重映射指向已丢失资源 (Missing External Remap)", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("💡 提示：扫描后可勾选需要清理的项，批量清理", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 扫描类型选择
            // Scan type options
            DrawResidualScanTypeOptions();

            EditorGUILayout.Space(5);

            // 操作按钮
            // Action buttons
            DrawResidualActionButtons();

            EditorGUILayout.Space(5);

            // 结果显示
            // Results display
            DrawResidualResults();
        }

        /// <summary>
        /// 绘制残留扫描类型选项
        /// Draw residual scan type options
        /// </summary>
        private void DrawResidualScanTypeOptions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("检测类型", EditorStyles.boldLabel);

            // 第一行：场景/Prefab 组件级残留
            // Row 1: Scene/Prefab component-level residuals
            EditorGUILayout.BeginHorizontal();
            scanMesh = EditorGUILayout.ToggleLeft("Mesh残留", scanMesh, GUILayout.Width(100));
            scanMaterial = EditorGUILayout.ToggleLeft("Material残留", scanMaterial, GUILayout.Width(120));
            scanTexture = EditorGUILayout.ToggleLeft("贴图残留", scanTexture, GUILayout.Width(100));
            scanTimeline = EditorGUILayout.ToggleLeft("Timeline残留", scanTimeline, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            // 第二行：资源级残留
            // Row 2: Asset-level residuals
            EditorGUILayout.BeginHorizontal();
            scanFbx = EditorGUILayout.ToggleLeft(
                new GUIContent("FBX残留", "检测 FBX.meta 中 externalObjects 指向已丢失材质的残留引用"),
                scanFbx, GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制残留操作按钮
        /// Draw residual action buttons
        /// </summary>
        private void DrawResidualActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            if (GUILayout.Button("扫描残留", GUILayout.Height(30)))
            {
                Residual_PerformScan();
            }
            GUI.backgroundColor = Color.white;

            GUI.enabled = residualScanResult.TotalCount > 0;

            GUI.backgroundColor = new Color(1f, 0.8f, 0.6f);
            if (GUILayout.Button($"清理选中 ({residualScanResult.SelectedCount})", GUILayout.Height(30)))
            {
                Residual_CleanSelected();
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button($"清理全部 ({residualScanResult.TotalCount})", GUILayout.Height(30)))
            {
                Residual_CleanAll();
            }
            GUI.backgroundColor = Color.white;

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制残留结果
        /// Draw residual results
        /// </summary>
        private void DrawResidualResults()
        {
            if (residualScanResult.TotalCount == 0)
            {
                EditorGUILayout.HelpBox("点击\"扫描残留\"按钮开始检测", MessageType.Info);
                return;
            }

            // 统计信息
            // Statistics
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"扫描结果: 共发现 {residualScanResult.TotalCount} 项残留", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"  Mesh:{residualScanResult.meshResiduals.Count}  |  Material:{residualScanResult.materialResiduals.Count}  |  贴图:{residualScanResult.textureResiduals.Count}  |  Timeline:{residualScanResult.timelineResiduals.Count}  |  FBX:{residualScanResult.fbxResiduals.Count}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 全选/取消全选
            // Select all / Deselect all
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全选", GUILayout.Width(60)))
            {
                Residual_SetAllSelected(true);
            }
            if (GUILayout.Button("取消全选", GUILayout.Width(80)))
            {
                Residual_SetAllSelected(false);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 滚动区域
            // Scroll area
            residualScrollPos = EditorGUILayout.BeginScrollView(residualScrollPos);

            if (residualScanResult.meshResiduals.Count > 0)
                DrawResidualFoldoutList("Mesh残留", ref showMeshFoldout, residualScanResult.meshResiduals);

            if (residualScanResult.materialResiduals.Count > 0)
                DrawResidualFoldoutList("Material残留", ref showMaterialFoldout, residualScanResult.materialResiduals);

            if (residualScanResult.textureResiduals.Count > 0)
                DrawResidualFoldoutList("贴图残留", ref showTextureFoldout, residualScanResult.textureResiduals);

            if (residualScanResult.timelineResiduals.Count > 0)
                DrawResidualFoldoutList("Timeline残留", ref showTimelineFoldout, residualScanResult.timelineResiduals);

            if (residualScanResult.fbxResiduals.Count > 0)
                DrawResidualFoldoutList("FBX残留", ref showFbxFoldout, residualScanResult.fbxResiduals);

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制残留折叠列表
        /// Draw residual foldout list
        /// </summary>
        private void DrawResidualFoldoutList<T>(string title, ref bool foldout, List<T> items) where T : ResidualItem
        {
            foldout = EditorGUILayout.Foldout(foldout, $"{title} ({items.Count})", true);

            if (foldout)
            {
                EditorGUI.indentLevel++;
                // 使用 ToArray 防止迭代过程中列表被修改
                // Use ToArray to prevent list modification during iteration
                foreach (var item in items.ToArray())
                {
                    DrawResidualItemRow(item);
                }
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// 绘制单条残留结果行
        /// Draw single residual result row
        /// </summary>
        private void DrawResidualItemRow(ResidualItem item)
        {
            EditorGUILayout.BeginHorizontal();

            // 选择框
            // Checkbox
            item.isSelected = EditorGUILayout.Toggle(item.isSelected, GUILayout.Width(20));

            // 对象引用 - FBX残留显示资源，其他显示GameObject
            // Object reference - FBX residuals show asset, others show GameObject
            if (item is FbxResidualItem fbxRow && !string.IsNullOrEmpty(fbxRow.fbxPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fbxRow.fbxPath);
                EditorGUILayout.ObjectField(asset, typeof(UnityEngine.Object), false, GUILayout.Width(150));
            }
            else
            {
                EditorGUILayout.ObjectField(item.gameObject, typeof(GameObject), true, GUILayout.Width(150));
            }

            // 描述
            // Description
            EditorGUILayout.LabelField(item.description, GUILayout.MinWidth(100));

            // 详细信息
            // Detail info
            EditorGUILayout.LabelField(item.GetDetailInfo(), EditorStyles.miniLabel, GUILayout.MinWidth(150));

            // 单独清理按钮
            // Single clean button
            if (GUILayout.Button("清理", GUILayout.Width(50)))
            {
                item.Clean();
                Residual_RemoveItem(item);
                Repaint();
            }

            // 定位按钮
            // Locate button
            if (GUILayout.Button("定位", GUILayout.Width(50)))
            {
                if (item is FbxResidualItem fbxLocate && !string.IsNullOrEmpty(fbxLocate.fbxPath))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fbxLocate.fbxPath);
                    if (asset != null)
                    {
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }
                }
                else if (item.gameObject != null)
                {
                    Selection.activeGameObject = item.gameObject;
                    EditorGUIUtility.PingObject(item.gameObject);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region 残留扫描逻辑

        /// <summary>
        /// 执行残留扫描
        /// Perform residual scan
        /// </summary>
        private void Residual_PerformScan()
        {
            residualScanResult.Clear();

            var gameObjects = GetTargetGameObjects();
            if (gameObjects == null || gameObjects.Length == 0) return;

            EditorUtility.DisplayProgressBar("扫描残留", "正在扫描...", 0);

            // FBX扫描去重集合，避免同一FBX被多个GO反查到
            // Scanned FBX path set to avoid duplicate scanning from multiple GOs
            var scannedFbxPaths = new HashSet<string>();

            try
            {
                int total = gameObjects.Length;
                for (int i = 0; i < total; i++)
                {
                    var obj = gameObjects[i];
                    EditorUtility.DisplayProgressBar("扫描残留", $"正在扫描: {obj.name}", (float)i / total);

                    if (scanMesh) Residual_ScanMesh(obj);
                    if (scanMaterial) Residual_ScanMaterial(obj);
                    if (scanTexture) Residual_ScanTexture(obj);
                    if (scanTimeline) Residual_ScanTimeline(obj);
                    if (scanFbx) Residual_ScanFbxInGameObject(obj, scannedFbxPaths);
                }

                // 资源级扫描：Prefab模式下指定了文件夹时，直接按文件夹扫所有FBX
                // Asset-level scan: in Prefab mode with folder specified, scan all FBX under folder
                if (scanFbx && scanScope == ScanScope.Prefab && targetFolder != null)
                {
                    Residual_ScanFbxInFolder(scannedFbxPaths);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (residualScanResult.TotalCount == 0)
            {
                EditorUtility.DisplayDialog("扫描完成", "未发现残留项", "确定");
            }
            else
            {
                Debug.Log($"[{TOOL_NAME}] 扫描完成，发现 {residualScanResult.TotalCount} 项残留 (Mesh:{residualScanResult.meshResiduals.Count} Material:{residualScanResult.materialResiduals.Count} 贴图:{residualScanResult.textureResiduals.Count} Timeline:{residualScanResult.timelineResiduals.Count} FBX:{residualScanResult.fbxResiduals.Count})");
            }
        }

        /// <summary>
        /// 扫描Mesh残留
        /// Scan mesh residuals
        /// </summary>
        private void Residual_ScanMesh(GameObject root)
        {
            var particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);

            foreach (var ps in particleSystems)
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer == null) continue;

                string hierarchyPath = GetHierarchyPath(ps.gameObject);

                // 检查Renderer Mesh残留 - 渲染模式非Mesh但有Mesh引用
                // Check Renderer Mesh residual - render mode is not Mesh but has Mesh reference
                if (renderer.renderMode != ParticleSystemRenderMode.Mesh)
                {
                    if (renderer.mesh != null)
                    {
                        residualScanResult.meshResiduals.Add(new MeshResidualItem
                        {
                            gameObject = ps.gameObject,
                            hierarchyPath = hierarchyPath,
                            residualType = ResidualType.MeshRenderer,
                            description = $"渲染模式: {renderer.renderMode}",
                            particleSystem = ps,
                            renderer = renderer,
                            mesh = renderer.mesh,
                            isShapeMesh = false,
                            meshIndex = -1
                        });
                    }

                    var meshes = new Mesh[4];
                    int meshCount = renderer.GetMeshes(meshes);
                    for (int i = 0; i < meshCount; i++)
                    {
                        if (meshes[i] != null)
                        {
                            residualScanResult.meshResiduals.Add(new MeshResidualItem
                            {
                                gameObject = ps.gameObject,
                                hierarchyPath = hierarchyPath,
                                residualType = ResidualType.MeshRenderer,
                                description = $"渲染模式: {renderer.renderMode} (多Mesh)",
                                particleSystem = ps,
                                renderer = renderer,
                                mesh = meshes[i],
                                isShapeMesh = false,
                                meshIndex = i
                            });
                        }
                    }
                }

                // 检查Shape模块Mesh残留
                // Check Shape module Mesh residual
                var shape = ps.shape;
                if (shape.mesh != null)
                {
                    bool isResidual = false;
                    string reason = "";

                    if (!shape.enabled)
                    {
                        isResidual = true;
                        reason = "Shape模块已禁用";
                    }
                    else if (shape.shapeType != ParticleSystemShapeType.Mesh &&
                             shape.shapeType != ParticleSystemShapeType.MeshRenderer &&
                             shape.shapeType != ParticleSystemShapeType.SkinnedMeshRenderer)
                    {
                        isResidual = true;
                        reason = $"Shape类型: {shape.shapeType}";
                    }

                    if (isResidual)
                    {
                        residualScanResult.meshResiduals.Add(new MeshResidualItem
                        {
                            gameObject = ps.gameObject,
                            hierarchyPath = hierarchyPath,
                            residualType = ResidualType.MeshShape,
                            description = reason,
                            particleSystem = ps,
                            renderer = renderer,
                            mesh = shape.mesh,
                            isShapeMesh = true
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 扫描Material残留
        /// Scan material residuals
        /// </summary>
        private void Residual_ScanMaterial(GameObject root)
        {
            var particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);

            foreach (var ps in particleSystems)
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer == null) continue;

                string hierarchyPath = GetHierarchyPath(ps.gameObject);

                // 检查Trail模块残留
                // Check Trail module residual
                var trails = ps.trails;
                if (!trails.enabled && renderer.trailMaterial != null)
                {
                    residualScanResult.materialResiduals.Add(new MaterialResidualItem
                    {
                        gameObject = ps.gameObject,
                        hierarchyPath = hierarchyPath,
                        residualType = ResidualType.MaterialTrail,
                        description = "Trail模块已禁用",
                        particleSystem = ps,
                        renderer = renderer,
                        material = renderer.trailMaterial,
                        isTrailMaterial = true
                    });
                }

                // 检查材质文件丢失
                // Check missing material files
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var mat = materials[i];
                    if (mat == null && i < materials.Length)
                    {
                        var so = new SerializedObject(renderer);
                        var matArrayProp = so.FindProperty("m_Materials");
                        if (matArrayProp != null && i < matArrayProp.arraySize)
                        {
                            var matProp = matArrayProp.GetArrayElementAtIndex(i);
                            if (matProp.objectReferenceValue == null && matProp.objectReferenceInstanceIDValue != 0)
                            {
                                residualScanResult.materialResiduals.Add(new MaterialResidualItem
                                {
                                    gameObject = ps.gameObject,
                                    hierarchyPath = hierarchyPath,
                                    residualType = ResidualType.MaterialMissing,
                                    description = "材质文件丢失",
                                    particleSystem = ps,
                                    renderer = renderer,
                                    material = null,
                                    isTrailMaterial = false,
                                    materialIndex = i
                                });
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 扫描贴图残留
        /// Scan texture residuals
        /// </summary>
        private void Residual_ScanTexture(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var processedMaterials = new HashSet<Material>();

            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;

                foreach (var mat in materials)
                {
                    if (mat == null || processedMaterials.Contains(mat)) continue;
                    processedMaterials.Add(mat);

                    Residual_ScanMaterialTextures(mat, renderer.gameObject);
                }

                var psRenderer = renderer as ParticleSystemRenderer;
                if (psRenderer != null && psRenderer.trailMaterial != null)
                {
                    var trailMat = psRenderer.trailMaterial;
                    if (!processedMaterials.Contains(trailMat))
                    {
                        processedMaterials.Add(trailMat);
                        Residual_ScanMaterialTextures(trailMat, renderer.gameObject);
                    }
                }
            }
        }

        /// <summary>
        /// 扫描单个材质的贴图残留
        /// Scan texture residuals in a single material
        /// </summary>
        private void Residual_ScanMaterialTextures(Material material, GameObject gameObject)
        {
            if (material == null || material.shader == null) return;

            var shader = material.shader;
            int propertyCount = ShaderUtil.GetPropertyCount(shader);

            var shaderTextureProperties = new HashSet<string>();
            var toggleProperties = new Dictionary<string, float>();

            for (int i = 0; i < propertyCount; i++)
            {
                var propType = ShaderUtil.GetPropertyType(shader, i);
                var propName = ShaderUtil.GetPropertyName(shader, i);

                if (propType == ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    shaderTextureProperties.Add(propName);
                }
                else if (propType == ShaderUtil.ShaderPropertyType.Float ||
                         propType == ShaderUtil.ShaderPropertyType.Range)
                {
                    if (material.HasProperty(propName))
                    {
                        float value = material.GetFloat(propName);
                        if (value == 0f || value == 1f)
                        {
                            toggleProperties[propName] = value;
                        }
                    }
                }
            }

            var so = new SerializedObject(material);
            var texEnvsProp = so.FindProperty("m_SavedProperties.m_TexEnvs");

            if (texEnvsProp != null && texEnvsProp.isArray)
            {
                for (int i = 0; i < texEnvsProp.arraySize; i++)
                {
                    var element = texEnvsProp.GetArrayElementAtIndex(i);
                    var nameProp = element.FindPropertyRelative("first");
                    var textureProp = element.FindPropertyRelative("second.m_Texture");

                    if (nameProp != null && textureProp != null)
                    {
                        string propName = nameProp.stringValue;
                        var texture = textureProp.objectReferenceValue as Texture;

                        if (texture == null) continue;

                        // Shader中没有对应属性
                        // Shader doesn't have the corresponding property
                        if (!shaderTextureProperties.Contains(propName))
                        {
                            residualScanResult.textureResiduals.Add(new TextureResidualItem
                            {
                                gameObject = gameObject,
                                hierarchyPath = GetHierarchyPath(gameObject),
                                residualType = ResidualType.TextureShaderMismatch,
                                description = $"材质: {material.name}",
                                material = material,
                                propertyName = propName,
                                texture = texture,
                                reason = "Shader不需要此贴图属性"
                            });
                            continue;
                        }

                        // 被Toggle禁用
                        // Disabled by Toggle
                        var controllingToggle = Residual_FindControllingToggle(propName, toggleProperties);
                        if (controllingToggle != null && toggleProperties[controllingToggle] == 0f)
                        {
                            residualScanResult.textureResiduals.Add(new TextureResidualItem
                            {
                                gameObject = gameObject,
                                hierarchyPath = GetHierarchyPath(gameObject),
                                residualType = ResidualType.TextureDisabledModule,
                                description = $"材质: {material.name}",
                                material = material,
                                propertyName = propName,
                                texture = texture,
                                reason = $"Toggle [{controllingToggle}] 已禁用"
                            });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 自动查找控制贴图的Toggle属性
        /// Auto-find controlling Toggle property for a texture
        /// </summary>
        private string Residual_FindControllingToggle(string texturePropName, Dictionary<string, float> toggleProperties)
        {
            if (toggleProperties.Count == 0) return null;

            string texCoreName = Residual_NormalizePropertyName(texturePropName);

            // 精确匹配
            // Exact match
            foreach (var toggle in toggleProperties)
            {
                string toggleCoreName = Residual_NormalizePropertyName(toggle.Key);
                if (texCoreName.Equals(toggleCoreName, StringComparison.OrdinalIgnoreCase))
                    return toggle.Key;
            }

            // 前缀匹配
            // Prefix match
            foreach (var toggle in toggleProperties)
            {
                string toggleCoreName = Residual_NormalizePropertyName(toggle.Key);
                if (!string.IsNullOrEmpty(toggleCoreName) &&
                    texCoreName.StartsWith(toggleCoreName, StringComparison.OrdinalIgnoreCase))
                    return toggle.Key;
            }

            // 包含匹配
            // Contains match
            foreach (var toggle in toggleProperties)
            {
                string toggleCoreName = Residual_NormalizePropertyName(toggle.Key);
                if (!string.IsNullOrEmpty(toggleCoreName) && toggleCoreName.Length >= 3 &&
                    texCoreName.IndexOf(toggleCoreName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return toggle.Key;
            }

            return null;
        }

        /// <summary>
        /// 标准化属性名称
        /// Normalize property name
        /// </summary>
        private string Residual_NormalizePropertyName(string propName)
        {
            if (string.IsNullOrEmpty(propName)) return "";

            string result = propName.TrimStart('_');

            var removeSuffixes = new[] { "Tex", "Texture", "Map", "Toggle", "Enable", "Use", "On", "Switch" };
            foreach (var suffix in removeSuffixes)
            {
                if (result.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    result = result.Substring(0, result.Length - suffix.Length);
                }
            }

            var removePrefixes = new[] { "Enable", "Use", "Toggle", "Is", "Has" };
            foreach (var prefix in removePrefixes)
            {
                if (result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    result = result.Substring(prefix.Length);
                }
            }

            return result.Trim('_', ' ');
        }

        /// <summary>
        /// 扫描Timeline残留
        /// Scan Timeline residuals
        /// </summary>
        private void Residual_ScanTimeline(GameObject root)
        {
            var directors = root.GetComponentsInChildren<PlayableDirector>(true);

            foreach (var director in directors)
            {
                var timelineAsset = director.playableAsset as TimelineAsset;
                if (timelineAsset == null) continue;

                // 非Prefab模式才检查绑定丢失
                // Only check binding loss in non-Prefab mode
                if (scanScope != ScanScope.Prefab)
                {
                    Residual_ScanBindings(director, timelineAsset);
                }

                Residual_ScanClipAssets(director, timelineAsset);
                Residual_ScanExposedRefs(director);
                Residual_ScanOrphanTracks(director, timelineAsset);
            }
        }

        /// <summary>
        /// 扫描轨道绑定丢失
        /// Scan track binding loss
        /// </summary>
        private void Residual_ScanBindings(PlayableDirector director, TimelineAsset timeline)
        {
            var so = new SerializedObject(director);
            var bindings = so.FindProperty("m_SceneBindings");

            if (bindings == null || !bindings.isArray) return;

            for (int i = 0; i < bindings.arraySize; i++)
            {
                var element = bindings.GetArrayElementAtIndex(i);
                var key = element.FindPropertyRelative("key");
                var value = element.FindPropertyRelative("value");

                if (key == null || value == null) continue;

                bool keyMissing = key.objectReferenceValue == null &&
                                  key.objectReferenceInstanceIDValue != 0;

                bool valueMissing = key.objectReferenceValue != null &&
                                    value.objectReferenceValue == null &&
                                    value.objectReferenceInstanceIDValue != 0;

                if (keyMissing || valueMissing)
                {
                    string trackName = keyMissing
                        ? $"[轨道丢失] ID:{key.objectReferenceInstanceIDValue}"
                        : (key.objectReferenceValue as TrackAsset)?.name ?? "未知轨道";
                    string desc = keyMissing ? "轨道资源丢失" : "绑定目标丢失";

                    residualScanResult.timelineResiduals.Add(new TimelineResidualItem
                    {
                        gameObject = director.gameObject,
                        hierarchyPath = GetHierarchyPath(director.gameObject),
                        residualType = ResidualType.TimelineBindingMissing,
                        description = desc,
                        director = director,
                        timelineAsset = timeline,
                        track = key.objectReferenceValue as TrackAsset,
                        trackName = trackName,
                        bindingIndex = i
                    });
                }
            }
        }

        /// <summary>
        /// 扫描Clip资源丢失
        /// Scan clip asset loss
        /// </summary>
        private void Residual_ScanClipAssets(PlayableDirector director, TimelineAsset timeline)
        {
            foreach (var track in timeline.GetOutputTracks())
            {
                foreach (var clip in track.GetClips())
                {
                    if (clip.asset == null)
                    {
                        residualScanResult.timelineResiduals.Add(new TimelineResidualItem
                        {
                            gameObject = director.gameObject,
                            hierarchyPath = GetHierarchyPath(director.gameObject),
                            residualType = ResidualType.TimelineClipAssetMissing,
                            description = "Clip资源丢失",
                            director = director,
                            timelineAsset = timeline,
                            track = track,
                            trackName = $"{track.name} → Clip \"{clip.displayName}\""
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 扫描ExposedReference丢失
        /// Scan ExposedReference loss
        /// </summary>
        private void Residual_ScanExposedRefs(PlayableDirector director)
        {
            var so = new SerializedObject(director);
            var refs = so.FindProperty("m_ExposedReferences.m_References");

            if (refs == null || !refs.isArray) return;

            for (int i = 0; i < refs.arraySize; i++)
            {
                var element = refs.GetArrayElementAtIndex(i);
                var key = element.FindPropertyRelative("first");
                var value = element.FindPropertyRelative("second");

                if (key == null || value == null) continue;

                if (value.objectReferenceValue == null &&
                    value.objectReferenceInstanceIDValue != 0)
                {
                    residualScanResult.timelineResiduals.Add(new TimelineResidualItem
                    {
                        gameObject = director.gameObject,
                        hierarchyPath = GetHierarchyPath(director.gameObject),
                        residualType = ResidualType.TimelineExposedRefMissing,
                        description = "ExposedRef引用丢失",
                        director = director,
                        timelineAsset = director.playableAsset as TimelineAsset,
                        bindingPropertyPath = key.stringValue
                    });
                }
            }
        }

        /// <summary>
        /// 扫描孤立轨道 - Timeline文件中存在但未被m_Tracks引用的Track对象
        /// Scan orphan tracks - Track objects that exist in .playable file but not registered in m_Tracks
        /// </summary>
        private void Residual_ScanOrphanTracks(PlayableDirector director, TimelineAsset timeline)
        {
            string assetPath = AssetDatabase.GetAssetPath(timeline);
            if (string.IsNullOrEmpty(assetPath)) return;

            // 1. 加载.playable文件中的所有子对象
            // Load all sub-assets from .playable file
            var allSubAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (allSubAssets == null || allSubAssets.Length == 0) return;

            // 2. 收集所有正常注册的轨道（包括嵌套子轨道）
            // Collect all registered tracks (including nested child tracks)
            var registeredTracks = new HashSet<TrackAsset>();
            foreach (var track in timeline.GetOutputTracks())
            {
                Residual_CollectAllTracks(track, registeredTracks);
            }
            // 也收集根级 m_Tracks（双重保险）
            // Also collect root-level m_Tracks (double insurance)
            foreach (var rootTrack in timeline.GetRootTracks())
            {
                Residual_CollectAllTracks(rootTrack, registeredTracks);
            }

            // 3. 对比：找出存在于文件中但不在正常轨道列表中的 TrackAsset
            // Compare: find TrackAssets that exist in file but not in registered track list
            foreach (var subAsset in allSubAssets)
            {
                if (subAsset == null) continue;
                if (subAsset == timeline) continue;

                var trackAsset = subAsset as TrackAsset;
                if (trackAsset != null && !registeredTracks.Contains(trackAsset))
                {
                    // 这是一个孤立轨道
                    // This is an orphan track
                    string scriptType = trackAsset.GetType().Name;

                    residualScanResult.timelineResiduals.Add(new TimelineResidualItem
                    {
                        gameObject = director.gameObject,
                        hierarchyPath = GetHierarchyPath(director.gameObject),
                        residualType = ResidualType.TimelineOrphanTrack,
                        description = "孤立轨道",
                        director = director,
                        timelineAsset = timeline,
                        track = trackAsset,
                        trackName = $"\"{trackAsset.name}\" ({scriptType})",
                        orphanSubAsset = subAsset
                    });
                }
            }
        }

        /// <summary>
        /// 递归收集所有轨道（包括子轨道）
        /// Recursively collect all tracks including child tracks
        /// </summary>
        private void Residual_CollectAllTracks(TrackAsset track, HashSet<TrackAsset> collection)
        {
            if (track == null || collection.Contains(track)) return;
            collection.Add(track);

            foreach (var childTrack in track.GetChildTracks())
            {
                Residual_CollectAllTracks(childTrack, collection);
            }
        }

        /// <summary>
        /// 扫描 GameObject 依赖链里的 FBX 残留
        /// Scan FBX residuals from GameObject dependency chain
        /// </summary>
        private void Residual_ScanFbxInGameObject(GameObject root, HashSet<string> scannedFbxPaths)
        {
            if (root == null) return;

            // MeshFilter 引用的 Mesh → FBX
            var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;
                TryScanFbxFromAsset(mf.sharedMesh, scannedFbxPaths);
            }

            // SkinnedMeshRenderer 引用的 Mesh → FBX
            var skinnedMeshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in skinnedMeshes)
            {
                if (smr.sharedMesh == null) continue;
                TryScanFbxFromAsset(smr.sharedMesh, scannedFbxPaths);
            }

            // ParticleSystem Shape/Renderer 引用的 Mesh → FBX
            var particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particleSystems)
            {
                var psr = ps.GetComponent<ParticleSystemRenderer>();
                if (psr != null && psr.mesh != null)
                    TryScanFbxFromAsset(psr.mesh, scannedFbxPaths);
                if (ps.shape.mesh != null)
                    TryScanFbxFromAsset(ps.shape.mesh, scannedFbxPaths);
            }
        }

        /// <summary>
        /// 从资源对象反查其所属FBX并扫描
        /// Reverse-lookup FBX path from an asset and scan it
        /// </summary>
        private void TryScanFbxFromAsset(UnityEngine.Object asset, HashSet<string> scannedFbxPaths)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path) || !IsFbxPath(path)) return;
            if (!scannedFbxPaths.Add(path)) return;
            Residual_ScanSingleFbx(path);
        }

        /// <summary>
        /// 按文件夹批量扫描 FBX
        /// Batch scan FBX residuals under the target folder
        /// </summary>
        private void Residual_ScanFbxInFolder(HashSet<string> scannedFbxPaths)
        {
            string folderPath = AssetDatabase.GetAssetPath(targetFolder);
            if (string.IsNullOrEmpty(folderPath)) return;

            // FBX 在 AssetDatabase 的类型是 Model
            // FBX type in AssetDatabase is "Model"
            var guids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsFbxPath(path)) continue;

                // 不包含子文件夹时过滤
                // Filter when not including subfolders
                if (!includeSubFolders)
                {
                    string dir = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
                    if (dir != folderPath.Replace("\\", "/")) continue;
                }

                if (!scannedFbxPaths.Add(path)) continue;
                Residual_ScanSingleFbx(path);
            }
        }

        /// <summary>
        /// 扫描单个 FBX 的 externalObjects 是否有 Missing
        /// Scan a single FBX for missing externalObjects
        /// </summary>
        private void Residual_ScanSingleFbx(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;

            var externals = importer.GetExternalObjectMap();
            if (externals == null || externals.Count == 0) return;

            // 按"丢失的 GUID"聚合：同一 FBX 可能有多个槽指向同一个丢失 GUID，合并为一项
            // Aggregate by missing GUID: multiple slots may point to the same missing GUID
            var missingGroups = new Dictionary<string, FbxResidualItem>();

            foreach (var kvp in externals)
            {
                // value 为 null 表示这个槽的目标资源已丢失
                // value == null means the target asset is missing
                if (kvp.Value != null) continue;

                string slotName = kvp.Key.name ?? "(unknown)";
                string missingGuid = TryReadMissingGuidFromMeta(fbxPath, slotName);
                string aggKey = !string.IsNullOrEmpty(missingGuid) ? missingGuid : $"slot:{slotName}";

                if (!missingGroups.TryGetValue(aggKey, out var item))
                {
                    item = new FbxResidualItem
                    {
                        gameObject = null,  // 资源级残留，无关联 GameObject
                        hierarchyPath = fbxPath,
                        residualType = ResidualType.FbxExternalMaterialMissing,
                        description = $"FBX外部材质丢失: {System.IO.Path.GetFileName(fbxPath)}",
                        fbxPath = fbxPath,
                        fbxGuid = AssetDatabase.AssetPathToGUID(fbxPath),
                        missingGuid = missingGuid ?? "(未知)"
                    };
                    missingGroups[aggKey] = item;
                }
                item.materialSlots.Add(slotName);
            }

            foreach (var item in missingGroups.Values)
            {
                residualScanResult.fbxResiduals.Add(item);
            }
        }

        /// <summary>
        /// 从 .meta 文件里读 externalObjects 映射表，找指定槽位对应的丢失 GUID
        /// Read .meta file to find the missing GUID referenced by a specific slot
        /// </summary>
        private string TryReadMissingGuidFromMeta(string fbxPath, string slotName)
        {
            string metaPath = fbxPath + ".meta";
            if (!System.IO.File.Exists(metaPath)) return null;

            try
            {
                string[] lines = System.IO.File.ReadAllLines(metaPath);
                bool inExternals = false;
                bool nameMatched = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];

                    if (line.TrimStart().StartsWith("externalObjects:"))
                    {
                        inExternals = true;
                        continue;
                    }
                    if (inExternals && line.Length > 0 && !line.StartsWith(" ") && !line.StartsWith("\t"))
                    {
                        // 离开 externalObjects 节
                        // Leaving externalObjects section
                        inExternals = false;
                        continue;
                    }
                    if (!inExternals) continue;

                    if (line.Contains("name: " + slotName))
                    {
                        nameMatched = true;
                        continue;
                    }
                    if (nameMatched && line.Contains("guid:"))
                    {
                        int idx = line.IndexOf("guid:");
                        string tail = line.Substring(idx + 5).Trim();
                        int comma = tail.IndexOf(',');
                        if (comma > 0) tail = tail.Substring(0, comma).Trim();
                        return tail.Trim('{', '}', ' ');
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"读取 meta 失败: {metaPath} - {e.Message}");
            }
            return null;
        }

        /// <summary>
        /// 判断路径是否 FBX（或其他 Model 类型）
        /// Check whether the path is an FBX (or other Model type)
        /// </summary>
        private bool IsFbxPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext == ".fbx" || ext == ".obj" || ext == ".dae" || ext == ".3ds";
        }

        #endregion

        #region 残留清理操作

        /// <summary>
        /// 清理选中项
        /// Clean selected items
        /// </summary>
        private void Residual_CleanSelected()
        {
            var selectedItems = Residual_GetSelectedItems();

            if (selectedItems.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有选中任何项", "确定");
                return;
            }

            int fbxCount = selectedItems.OfType<FbxResidualItem>().Count();
            string extraWarning = fbxCount > 0
                ? $"\n\n⚠️ 其中 {fbxCount} 项 FBX残留不支持 Ctrl+Z 撤销（将修改 .meta 文件），建议先提交 Git。"
                : "";

            if (!EditorUtility.DisplayDialog("确认清理",
                $"确定要清理选中的 {selectedItems.Count} 项残留吗？\n\n常规残留支持撤销(Ctrl+Z){extraWarning}",
                "确定", "取消"))
            {
                return;
            }

            Undo.SetCurrentGroupName("清理选中残留");
            int cleanedCount = 0;

            // 对 Timeline 绑定残留按 bindingIndex 倒序排列，避免删除时 index 错位
            // Sort timeline binding residuals by bindingIndex descending to avoid index shift on deletion
            var sortedItems = selectedItems
                .OrderByDescending(item =>
                    item is TimelineResidualItem tlItem ? tlItem.bindingIndex : -1)
                .ToList();

            foreach (var item in sortedItems)
            {
                try
                {
                    item.Clean();
                    cleanedCount++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"清理失败: {item.gameObject?.name} - {e.Message}");
                }
            }

            foreach (var item in sortedItems)
            {
                Residual_RemoveItem(item);
            }

            EditorUtility.DisplayDialog("清理完成", $"已清理 {cleanedCount} 项残留", "确定");
            Repaint();
        }

        /// <summary>
        /// 清理全部
        /// Clean all items
        /// </summary>
        private void Residual_CleanAll()
        {
            if (residualScanResult.TotalCount == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有残留项需要清理", "确定");
                return;
            }

            int fbxCount = residualScanResult.fbxResiduals.Count;
            string extraWarning = fbxCount > 0
                ? $"\n\n⚠️ 其中 {fbxCount} 项 FBX残留不支持 Ctrl+Z 撤销（将修改 .meta 文件），建议先提交 Git。"
                : "";

            if (!EditorUtility.DisplayDialog("确认清理",
                $"确定要清理全部 {residualScanResult.TotalCount} 项残留吗？\n\n常规残留支持撤销(Ctrl+Z){extraWarning}",
                "确定", "取消"))
            {
                return;
            }

            Undo.SetCurrentGroupName("清理全部残留");
            int cleanedCount = 0;

            var allItems = Residual_GetAllItems();

            // 对 Timeline 绑定残留按 bindingIndex 倒序排列，避免删除时 index 错位
            // Sort timeline binding residuals by bindingIndex descending to avoid index shift on deletion
            var sortedItems = allItems
                .OrderByDescending(item =>
                    item is TimelineResidualItem tlItem ? tlItem.bindingIndex : -1)
                .ToList();

            foreach (var item in sortedItems)
            {
                try
                {
                    item.Clean();
                    cleanedCount++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"清理失败: {item.gameObject?.name} - {e.Message}");
                }
            }

            residualScanResult.Clear();

            EditorUtility.DisplayDialog("清理完成", $"已清理 {cleanedCount} 项残留", "确定");
            Repaint();
        }

        /// <summary>
        /// 获取选中的残留项
        /// Get selected residual items
        /// </summary>
        private List<ResidualItem> Residual_GetSelectedItems()
        {
            var result = new List<ResidualItem>();
            result.AddRange(residualScanResult.meshResiduals.Where(m => m.isSelected));
            result.AddRange(residualScanResult.materialResiduals.Where(m => m.isSelected));
            result.AddRange(residualScanResult.textureResiduals.Where(m => m.isSelected));
            result.AddRange(residualScanResult.timelineResiduals.Where(m => m.isSelected));
            result.AddRange(residualScanResult.fbxResiduals.Where(m => m.isSelected));
            return result;
        }

        /// <summary>
        /// 获取所有残留项
        /// Get all residual items
        /// </summary>
        private List<ResidualItem> Residual_GetAllItems()
        {
            var result = new List<ResidualItem>();
            result.AddRange(residualScanResult.meshResiduals);
            result.AddRange(residualScanResult.materialResiduals);
            result.AddRange(residualScanResult.textureResiduals);
            result.AddRange(residualScanResult.timelineResiduals);
            result.AddRange(residualScanResult.fbxResiduals);
            return result;
        }

        /// <summary>
        /// 设置全部选中/取消
        /// Set all selected/deselected
        /// </summary>
        private void Residual_SetAllSelected(bool selected)
        {
            foreach (var item in residualScanResult.meshResiduals) item.isSelected = selected;
            foreach (var item in residualScanResult.materialResiduals) item.isSelected = selected;
            foreach (var item in residualScanResult.textureResiduals) item.isSelected = selected;
            foreach (var item in residualScanResult.timelineResiduals) item.isSelected = selected;
            foreach (var item in residualScanResult.fbxResiduals) item.isSelected = selected;
        }

        /// <summary>
        /// 移除一条残留项
        /// Remove a residual item
        /// </summary>
        private void Residual_RemoveItem(ResidualItem item)
        {
            if (item is MeshResidualItem meshItem)
                residualScanResult.meshResiduals.Remove(meshItem);
            else if (item is MaterialResidualItem matItem)
                residualScanResult.materialResiduals.Remove(matItem);
            else if (item is TextureResidualItem texItem)
                residualScanResult.textureResiduals.Remove(texItem);
            else if (item is TimelineResidualItem tlItem)
                residualScanResult.timelineResiduals.Remove(tlItem);
            else if (item is FbxResidualItem fbxItem)
                residualScanResult.fbxResiduals.Remove(fbxItem);
        }

        #endregion
    }
}
