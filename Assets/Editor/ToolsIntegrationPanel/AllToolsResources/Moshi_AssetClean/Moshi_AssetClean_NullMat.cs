using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace MoshiTools
{
    /// <summary>
    /// 资源清理工具 - 空材质检测模块
    /// Asset Cleaner - Null material detection module
    /// </summary>
    public partial class Moshi_AssetClean
    {
        #region 空材质数据结构

        /// <summary>
        /// 空材质条目数据
        /// Null material entry data
        /// </summary>
        [Serializable]
        private class NullMaterialEntry
        {
            public GameObject gameObject;       // 问题物体
            public GameObject rootPrefab;       // 所属预制体（Prefab模式）
            public string objectPath;           // Hierarchy路径
            public string rendererTypeName;     // Renderer类型显示名
            public string slotName;             // 槽位描述（如 "材质[0]"、"拖尾材质"）
            public int materialIndex;           // 材质槽索引
            public Renderer renderer;           // Renderer组件引用
            public bool isTrailMaterial;        // 是否为粒子拖尾材质
            public int hierarchyOrder;          // Hierarchy遍历顺序
        }

        #endregion

        #region 空材质字段

        // 检测类型开关
        // Detection type toggles
        private bool checkParticleRenderer = true;
        private bool checkMeshRenderer = true;
        private bool checkSkinnedMeshRenderer = true;
        private bool checkSpriteRenderer = true;
        private bool checkLineTrailRenderer = true;
        private bool checkParticleTrail = true;

        // 扫描结果
        // Scan results
        private List<NullMaterialEntry> nullMatResults = new List<NullMaterialEntry>();
        private bool nullMatHasScanned = false;
        private int totalNullCount = 0;

        // 各类型统计
        // Statistics per type
        private int particleRendererCount = 0;
        private int meshRendererCount = 0;
        private int skinnedMeshCount = 0;
        private int spriteRendererCount = 0;
        private int lineTrailCount = 0;
        private int particleTrailCount = 0;

        // UI状态
        // UI state
        private Vector2 nullMatScrollPos;

        #endregion

        #region 初始化

        partial void InitNullMatModule()
        {
            // 初始化完成
            // Initialization complete
        }

        #endregion

        #region 空材质标签页绘制

        /// <summary>
        /// 绘制空材质检测标签页
        /// Draw null material detection tab
        /// </summary>
        private void DrawNullMatTab()
        {
            // 说明区域
            // Description area
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("功能说明", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("检测选中对象/场景/预设中各类渲染器的材质引用：", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("• 材质球为空（Missing 或未赋值） → 标记为问题项", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• 材质球数组中存在空槽位 → 标记为问题项", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• 支持6种渲染器类型：粒子/网格/蒙皮/精灵/线条拖尾/粒子拖尾", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("💡 提示：可在下方勾选需要检测的渲染器类型", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 检测类型区域
            // Detection type area
            DrawNullMatDetectionTypes();

            EditorGUILayout.Space(5);

            // 扫描按钮
            // Scan button
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            if (GUILayout.Button("开始扫描", GUILayout.Height(30)))
            {
                ExecuteNullMatScan();
            }
            GUI.backgroundColor = originalColor;

            EditorGUILayout.Space(5);

            // 结果显示
            // Results display
            DrawNullMatResults();
        }

        /// <summary>
        /// 绘制检测类型
        /// Draw detection types
        /// </summary>
        private void DrawNullMatDetectionTypes()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("检测类型", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            // 第一行
            // First row
            EditorGUILayout.BeginHorizontal();
            checkParticleRenderer = EditorGUILayout.ToggleLeft("粒子渲染器", checkParticleRenderer, GUILayout.Width(100));
            checkMeshRenderer = EditorGUILayout.ToggleLeft("网格渲染器", checkMeshRenderer, GUILayout.Width(100));
            checkSkinnedMeshRenderer = EditorGUILayout.ToggleLeft("蒙皮渲染器", checkSkinnedMeshRenderer, GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // 第二行
            // Second row
            EditorGUILayout.BeginHorizontal();
            checkSpriteRenderer = EditorGUILayout.ToggleLeft("精灵渲染器", checkSpriteRenderer, GUILayout.Width(100));
            checkLineTrailRenderer = EditorGUILayout.ToggleLeft("线条/拖尾", checkLineTrailRenderer, GUILayout.Width(100));
            checkParticleTrail = EditorGUILayout.ToggleLeft("粒子拖尾", checkParticleTrail, GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制空材质结果列表
        /// Draw null material results list
        /// </summary>
        private void DrawNullMatResults()
        {
            if (!nullMatHasScanned)
            {
                EditorGUILayout.HelpBox("点击\"开始扫描\"按钮开始检测", MessageType.Info);
                return;
            }

            if (nullMatResults.Count == 0)
            {
                EditorGUILayout.HelpBox("✅ 全部材质正常，未发现空材质！", MessageType.Info);
                return;
            }

            // 统计信息
            // Statistics
            EditorGUILayout.LabelField($"共发现 {totalNullCount} 处空材质", EditorStyles.boldLabel);

            // 分类型小统计
            // Per-type mini stats
            var statsBuilder = new System.Text.StringBuilder();
            if (particleRendererCount > 0) statsBuilder.Append($"粒子渲染器:{particleRendererCount} ");
            if (particleTrailCount > 0) statsBuilder.Append($"粒子拖尾:{particleTrailCount} ");
            if (meshRendererCount > 0) statsBuilder.Append($"网格渲染器:{meshRendererCount} ");
            if (skinnedMeshCount > 0) statsBuilder.Append($"蒙皮渲染器:{skinnedMeshCount} ");
            if (spriteRendererCount > 0) statsBuilder.Append($"精灵渲染器:{spriteRendererCount} ");
            if (lineTrailCount > 0) statsBuilder.Append($"线条/拖尾:{lineTrailCount} ");
            if (statsBuilder.Length > 0)
            {
                EditorGUILayout.LabelField(statsBuilder.ToString(), EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(3);

            // 导出 & 选中按钮
            // Export & select buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("复制名称", EditorStyles.miniButtonLeft, GUILayout.Width(70)))
            {
                NullMat_ExportToClipboard();
            }
            if (GUILayout.Button("导出文件", EditorStyles.miniButtonMid, GUILayout.Width(70)))
            {
                NullMat_ExportToFile();
            }
            if (GUILayout.Button("一键选中", EditorStyles.miniButtonRight, GUILayout.Width(70)))
            {
                NullMat_SelectAll();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // 结果列表
            // Result list
            nullMatScrollPos = EditorGUILayout.BeginScrollView(nullMatScrollPos);

            foreach (var entry in nullMatResults)
            {
                DrawNullMatResultItem(entry);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制单条空材质结果行
        /// Draw single null material result row
        /// </summary>
        private void DrawNullMatResultItem(NullMaterialEntry entry)
        {
            if (entry.gameObject == null) return;

            var originalColor = GUI.backgroundColor;

            EditorGUILayout.BeginHorizontal();

            // 1. GameObject 引用字段
            // GameObject field
            EditorGUILayout.ObjectField("", entry.gameObject, typeof(GameObject), true, GUILayout.Width(150));

            // 2. 空材质字段 — 红色背景高亮
            // Null material field - red background
            GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
            EditorGUILayout.ObjectField("", (Material)null, typeof(Material), false, GUILayout.Width(150));
            GUI.backgroundColor = originalColor;

            // 3. 槽位描述
            // Slot description
            EditorGUILayout.LabelField(entry.slotName, EditorStyles.miniLabel, GUILayout.Width(60));

            // 4. 彩色类型标签
            // Colored type label
            GUI.backgroundColor = GetNullMatTypeColor(entry.rendererTypeName);
            EditorGUILayout.LabelField(entry.rendererTypeName, EditorStyles.miniButton, GUILayout.Width(120));
            GUI.backgroundColor = originalColor;

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region 空材质扫描逻辑

        /// <summary>
        /// 执行空材质扫描
        /// Execute null material scan
        /// </summary>
        private void ExecuteNullMatScan()
        {
            ClearNullMatResults();
            nullMatHasScanned = true;

            var targets = GetTargetGameObjects();
            if (targets == null)
            {
                nullMatHasScanned = false;
                return;
            }

            int order = 0;
            int total = targets.Length;

            for (int i = 0; i < total; i++)
            {
                var target = targets[i];
                if (target == null) continue;

                // 进度条（仅Prefab/文件夹模式或多选时显示）
                // Progress bar (only show for Prefab/folder mode or multi-select)
                if (total > 1 || scanScope == ScanScope.Prefab)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(TOOL_NAME,
                        $"正在扫描空材质: {target.name} ({i + 1}/{total})",
                        (float)i / total))
                    {
                        break;
                    }
                }

                NullMat_ScanGameObjectRecursive(target, ref order, 
                    scanScope == ScanScope.Prefab ? target : null);
            }

            if (total > 1 || scanScope == ScanScope.Prefab)
                EditorUtility.ClearProgressBar();

            // 更新统计
            // Update statistics
            NullMat_UpdateStatistics();
        }

        /// <summary>
        /// 递归扫描GameObject及其子物体
        /// Recursively scan GameObject and its children
        /// </summary>
        private void NullMat_ScanGameObjectRecursive(GameObject obj, ref int order, GameObject rootPrefab)
        {
            if (obj == null) return;

            NullMat_CheckAllRenderers(obj, order, rootPrefab);
            order++;

            for (int i = 0; i < obj.transform.childCount; i++)
            {
                NullMat_ScanGameObjectRecursive(obj.transform.GetChild(i).gameObject, ref order, rootPrefab);
            }
        }

        /// <summary>
        /// 检查一个GameObject上的所有Renderer组件
        /// Check all Renderer components on a GameObject
        /// </summary>
        private void NullMat_CheckAllRenderers(GameObject obj, int order, GameObject rootPrefab)
        {
            if (obj == null) return;

            // 1. ParticleSystemRenderer
            if (checkParticleRenderer || checkParticleTrail)
            {
                var ps = obj.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
                    if (psRenderer != null && checkParticleRenderer)
                    {
                        NullMat_CheckSharedMaterials(psRenderer, obj, "粒子系统渲染器", order, rootPrefab);
                    }

                    // 检查粒子拖尾材质
                    // Check particle trail material
                    if (checkParticleTrail && psRenderer != null)
                    {
                        var trails = ps.trails;
                        if (trails.enabled)
                        {
                            if (psRenderer.trailMaterial == null)
                            {
                                nullMatResults.Add(new NullMaterialEntry
                                {
                                    gameObject = obj,
                                    rootPrefab = rootPrefab,
                                    objectPath = GetHierarchyPath(obj),
                                    rendererTypeName = "粒子系统拖尾",
                                    slotName = "拖尾材质",
                                    materialIndex = -1,
                                    renderer = psRenderer,
                                    isTrailMaterial = true,
                                    hierarchyOrder = order
                                });
                            }
                        }
                    }
                }
            }

            // 2. MeshRenderer
            if (checkMeshRenderer)
            {
                var meshRenderer = obj.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    if (obj.GetComponent<ParticleSystem>() == null)
                    {
                        NullMat_CheckSharedMaterials(meshRenderer, obj, "MeshRenderer", order, rootPrefab);
                    }
                }
            }

            // 3. SkinnedMeshRenderer
            if (checkSkinnedMeshRenderer)
            {
                var skinnedMesh = obj.GetComponent<SkinnedMeshRenderer>();
                if (skinnedMesh != null)
                {
                    NullMat_CheckSharedMaterials(skinnedMesh, obj, "SkinnedMesh", order, rootPrefab);
                }
            }

            // 4. SpriteRenderer
            if (checkSpriteRenderer)
            {
                var spriteRenderer = obj.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    NullMat_CheckSharedMaterials(spriteRenderer, obj, "SpriteRenderer", order, rootPrefab);
                }
            }

            // 5. LineRenderer & TrailRenderer
            if (checkLineTrailRenderer)
            {
                var lineRenderer = obj.GetComponent<LineRenderer>();
                if (lineRenderer != null)
                {
                    NullMat_CheckSharedMaterials(lineRenderer, obj, "LineRenderer", order, rootPrefab);
                }

                var trailRenderer = obj.GetComponent<TrailRenderer>();
                if (trailRenderer != null)
                {
                    NullMat_CheckSharedMaterials(trailRenderer, obj, "TrailRenderer", order, rootPrefab);
                }
            }
        }

        /// <summary>
        /// 检查Renderer的sharedMaterials数组中是否有null
        /// Check if sharedMaterials array has null entries
        /// </summary>
        private void NullMat_CheckSharedMaterials(Renderer renderer, GameObject obj, string typeName, int order, GameObject rootPrefab)
        {
            if (renderer == null) return;

            var materials = renderer.sharedMaterials;
            if (materials == null) return;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null)
                {
                    nullMatResults.Add(new NullMaterialEntry
                    {
                        gameObject = obj,
                        rootPrefab = rootPrefab,
                        objectPath = GetHierarchyPath(obj),
                        rendererTypeName = typeName,
                        slotName = $"材质[{i}]",
                        materialIndex = i,
                        renderer = renderer,
                        isTrailMaterial = false,
                        hierarchyOrder = order
                    });
                }
            }
        }

        #endregion

        #region 空材质辅助方法

        /// <summary>
        /// 清空空材质结果
        /// Clear null material results
        /// </summary>
        private void ClearNullMatResults()
        {
            nullMatResults.Clear();
            nullMatHasScanned = false;
            totalNullCount = 0;
            particleRendererCount = 0;
            meshRendererCount = 0;
            skinnedMeshCount = 0;
            spriteRendererCount = 0;
            lineTrailCount = 0;
            particleTrailCount = 0;
        }

        /// <summary>
        /// 更新空材质统计数据
        /// Update null material statistics
        /// </summary>
        private void NullMat_UpdateStatistics()
        {
            totalNullCount = nullMatResults.Count;
            particleRendererCount = nullMatResults.Count(r => r.rendererTypeName == "粒子系统渲染器");
            particleTrailCount = nullMatResults.Count(r => r.rendererTypeName == "粒子系统拖尾");
            meshRendererCount = nullMatResults.Count(r => r.rendererTypeName == "MeshRenderer");
            skinnedMeshCount = nullMatResults.Count(r => r.rendererTypeName == "SkinnedMesh");
            spriteRendererCount = nullMatResults.Count(r => r.rendererTypeName == "SpriteRenderer");
            lineTrailCount = nullMatResults.Count(r => r.rendererTypeName == "LineRenderer" || r.rendererTypeName == "TrailRenderer");
        }

        /// <summary>
        /// 获取类型对应的颜色
        /// Get type color
        /// </summary>
        private Color GetNullMatTypeColor(string typeName)
        {
            switch (typeName)
            {
                case "粒子系统渲染器": return new Color(0.7f, 1f, 0.7f);
                case "粒子系统拖尾":   return new Color(0.9f, 1f, 0.7f);
                case "MeshRenderer":   return new Color(0.7f, 0.8f, 1f);
                case "SkinnedMesh":    return new Color(1f, 0.8f, 0.7f);
                case "SpriteRenderer": return new Color(0.9f, 0.7f, 1f);
                case "LineRenderer":
                case "TrailRenderer":  return new Color(1f, 0.7f, 0.8f);
                default:               return Color.white;
            }
        }

        /// <summary>
        /// 构建导出文本
        /// Build export text
        /// </summary>
        private string NullMat_BuildExportText()
        {
            var sb = new System.Text.StringBuilder();
            var names = new HashSet<string>();
            foreach (var entry in nullMatResults)
            {
                if (entry.gameObject == null) continue;
                names.Add(entry.gameObject.name);
            }
            foreach (string name in names)
            {
                sb.AppendLine(name);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 导出到剪贴板
        /// Export to clipboard
        /// </summary>
        private void NullMat_ExportToClipboard()
        {
            string text = NullMat_BuildExportText();
            EditorGUIUtility.systemCopyBuffer = text;
            Debug.Log($"[{TOOL_NAME}] 已复制 {totalNullCount} 条记录到剪贴板");
        }

        /// <summary>
        /// 导出到文件
        /// Export to file
        /// </summary>
        private void NullMat_ExportToFile()
        {
            string defaultName = $"空材质检测_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string path = EditorUtility.SaveFilePanel("导出空材质检测报告", "", defaultName, "txt");
            if (string.IsNullOrEmpty(path)) return;

            string text = NullMat_BuildExportText();
            System.IO.File.WriteAllText(path, text, System.Text.Encoding.UTF8);
            Debug.Log($"[{TOOL_NAME}] 已导出报告到: {path}");
            EditorUtility.RevealInFinder(path);
        }

        /// <summary>
        /// 一键选中所有空材质物体
        /// Select all objects with null materials
        /// </summary>
        private void NullMat_SelectAll()
        {
            if (nullMatResults == null || nullMatResults.Count == 0)
            {
                Debug.LogWarning($"[{TOOL_NAME}] 没有检测结果，请先扫描");
                return;
            }

            var objectSet = new HashSet<GameObject>();
            foreach (var entry in nullMatResults)
            {
                if (entry.gameObject != null)
                    objectSet.Add(entry.gameObject);
            }

            if (objectSet.Count == 0)
            {
                Debug.LogWarning($"[{TOOL_NAME}] 结果中的物体已被删除");
                return;
            }

            var objectArray = new GameObject[objectSet.Count];
            objectSet.CopyTo(objectArray);
            Selection.objects = objectArray;
            Debug.Log($"[{TOOL_NAME}] 已选中 {objectArray.Length} 个物体");
        }

        #endregion
    }
}
