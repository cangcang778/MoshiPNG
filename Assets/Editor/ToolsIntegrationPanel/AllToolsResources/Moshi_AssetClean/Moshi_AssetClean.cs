using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace MoshiTools
{
    /// <summary>
    /// 资源清理工具 - 空材质检测 / 残留清理 / 粒子缩放修复 / 空粒子清理 / 贴图检查
    /// Asset Cleaner - Null material check / Residual cleanup / Particle scaling fix / Empty PS cleanup / Texture check (5-in-1)
    /// </summary>
    public partial class Moshi_AssetClean : EditorWindow
    {
        private const string TOOL_NAME = "资源清理";

        #region 共享枚举

        /// <summary>
        /// 标签页模式
        /// Tab mode
        /// </summary>
        private enum TabMode
        {
            NullMaterial,   // 空材质检测
            Residual,       // 残留清理
            ScaleFix,       // 缩放修复
            EmptyPS,        // 空粒子清理
            TexCheck        // 贴图检查
        }

        /// <summary>
        /// 扫描范围
        /// Scan scope
        /// </summary>
        private enum ScanScope
        {
            Selection,  // 选中对象
            Scene,      // 当前场景
            Prefab      // 指定预设/文件夹
        }

        #endregion

        #region 共享字段

        // 标签页
        // Tab
        private TabMode currentTab = TabMode.NullMaterial;

        // 扫描设置
        // Scan settings
        private ScanScope scanScope = ScanScope.Selection;
        private GameObject targetPrefab;
        private DefaultAsset targetFolder;
        private bool includeSubFolders = true;

        #endregion

        #region 窗口生命周期

        [MenuItem("工具/Moshi/" + TOOL_NAME)]
        public static void ShowWindow()
        {
            var window = GetWindow<Moshi_AssetClean>(TOOL_NAME);
            window.minSize = new Vector2(580, 450);
        }

        private void OnEnable()
        {
            InitNullMatModule();
            InitResidualModule();
            InitScaleFixModule();
            InitEmptyPSModule();
            InitTexCheckModule();
        }

        private void OnDisable()
        {
            // 清理资源
            // Cleanup resources
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

        #endregion

        #region OnGUI 总控

        private void OnGUI()
        {
            // 标题栏
            // Title bar
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(TOOL_NAME, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("空材质检测 / 残留清理 / 粒子缩放修复 / 空粒子清理 / 贴图检查", EditorStyles.miniLabel);
            EditorGUILayout.Space(5);

            // 标签页按钮组
            // Tab button group
            DrawTabButtons();

            EditorGUILayout.Space(5);

            // 共享扫描设置（贴图检查模块使用独立的文件夹选择，不显示共享设置）
            // Shared scan settings (TexCheck uses its own folder selection, skip shared settings)
            if (currentTab != TabMode.TexCheck)
            {
                DrawSharedScanSettings();
                EditorGUILayout.Space(5);
            }

            // 按标签页分发绘制
            // Dispatch drawing by tab
            switch (currentTab)
            {
                case TabMode.NullMaterial:
                    DrawNullMatTab();
                    break;
                case TabMode.Residual:
                    DrawResidualTab();
                    break;
                case TabMode.ScaleFix:
                    DrawScaleFixTab();
                    break;
                case TabMode.EmptyPS:
                    DrawEmptyPSTab();
                    break;
                case TabMode.TexCheck:
                    DrawTexCheckTab();
                    break;
            }
        }

        /// <summary>
        /// 绘制标签页按钮组
        /// Draw tab button group
        /// </summary>
        private void DrawTabButtons()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Toggle(currentTab == TabMode.NullMaterial, "空材质检测", EditorStyles.miniButtonLeft))
            {
                if (currentTab != TabMode.NullMaterial)
                {
                    currentTab = TabMode.NullMaterial;
                }
            }
            if (GUILayout.Toggle(currentTab == TabMode.Residual, "残留清理", EditorStyles.miniButtonMid))
            {
                if (currentTab != TabMode.Residual)
                {
                    currentTab = TabMode.Residual;
                }
            }
            if (GUILayout.Toggle(currentTab == TabMode.ScaleFix, "缩放修复", EditorStyles.miniButtonMid))
            {
                if (currentTab != TabMode.ScaleFix)
                {
                    currentTab = TabMode.ScaleFix;
                }
            }
            if (GUILayout.Toggle(currentTab == TabMode.EmptyPS, "空粒子清理", EditorStyles.miniButtonMid))
            {
                if (currentTab != TabMode.EmptyPS)
                {
                    currentTab = TabMode.EmptyPS;
                }
            }
            if (GUILayout.Toggle(currentTab == TabMode.TexCheck, "贴图检查", EditorStyles.miniButtonRight))
            {
                if (currentTab != TabMode.TexCheck)
                {
                    currentTab = TabMode.TexCheck;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制共享扫描设置
        /// Draw shared scan settings
        /// </summary>
        private void DrawSharedScanSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("扫描设置", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            // 扫描范围按钮组
            // Scan scope button group
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("扫描范围", GUILayout.Width(60));

            if (GUILayout.Toggle(scanScope == ScanScope.Selection, "选中对象", EditorStyles.miniButtonLeft))
            {
                scanScope = ScanScope.Selection;
            }
            if (GUILayout.Toggle(scanScope == ScanScope.Scene, "当前场景", EditorStyles.miniButtonMid))
            {
                scanScope = ScanScope.Scene;
            }
            if (GUILayout.Toggle(scanScope == ScanScope.Prefab, "指定预设", EditorStyles.miniButtonRight))
            {
                scanScope = ScanScope.Prefab;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // 根据扫描范围显示额外字段
            // Show extra fields based on scan scope
            if (scanScope == ScanScope.Prefab)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("预设目标", GUILayout.Width(60));
                targetPrefab = (GameObject)EditorGUILayout.ObjectField("", targetPrefab, typeof(GameObject), false);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("扫描文件夹", GUILayout.Width(60));
                targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("", targetFolder, typeof(DefaultAsset), false);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(64);
                includeSubFolders = EditorGUILayout.ToggleLeft("包含子文件夹", includeSubFolders, GUILayout.Width(120));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region 共享方法

        /// <summary>
        /// 统一获取扫描目标GameObject数组
        /// Get target GameObjects based on scan scope
        /// </summary>
        private GameObject[] GetTargetGameObjects()
        {
            switch (scanScope)
            {
                case ScanScope.Selection:
                    var selected = Selection.gameObjects;
                    if (selected == null || selected.Length == 0)
                    {
                        EditorUtility.DisplayDialog(TOOL_NAME, "请先在Hierarchy中选中物体！", "确定");
                        return null;
                    }
                    return selected;

                case ScanScope.Scene:
                    var rootObjects = new List<GameObject>();
                    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    scene.GetRootGameObjects(rootObjects);
                    if (rootObjects.Count == 0)
                    {
                        EditorUtility.DisplayDialog(TOOL_NAME, "当前场景为空！", "确定");
                        return null;
                    }
                    return rootObjects.ToArray();

                case ScanScope.Prefab:
                    return GetPrefabTargets();

                default:
                    return null;
            }
        }

        /// <summary>
        /// 获取Prefab/文件夹模式下的扫描对象
        /// Get scan targets in Prefab/folder mode
        /// </summary>
        private GameObject[] GetPrefabTargets()
        {
            var results = new List<GameObject>();

            // 如果指定了Prefab，直接加入
            // If a Prefab is specified, add it directly
            if (targetPrefab != null)
            {
                results.Add(targetPrefab);
            }

            // 如果指定了文件夹，搜索其中的Prefab
            // If a folder is specified, search for Prefabs in it
            if (targetFolder != null)
            {
                string folderPath = AssetDatabase.GetAssetPath(targetFolder);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });

                    foreach (string guid in prefabGuids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);

                        // 不包含子文件夹时，过滤仅当前目录
                        // Filter to current directory only when not including subfolders
                        if (!includeSubFolders)
                        {
                            string dir = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
                            if (dir != folderPath.Replace("\\", "/"))
                                continue;
                        }

                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (prefab != null)
                            results.Add(prefab);
                    }
                }
            }

            if (results.Count == 0)
            {
                EditorUtility.DisplayDialog(TOOL_NAME, "请指定Prefab或文件夹！", "确定");
                return null;
            }

            return results.ToArray();
        }

        /// <summary>
        /// 获取物体在Hierarchy中的路径
        /// Get object's Hierarchy path
        /// </summary>
        private string GetHierarchyPath(GameObject obj)
        {
            if (obj == null) return "";

            string path = obj.name;
            Transform parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        #endregion

        #region Partial 初始化桩方法（各模块各自实现）

        /// <summary>
        /// 空材质检测模块初始化
        /// </summary>
        partial void InitNullMatModule();

        /// <summary>
        /// 残留清理模块初始化
        /// </summary>
        partial void InitResidualModule();

        /// <summary>
        /// 缩放修复模块初始化
        /// </summary>
        partial void InitScaleFixModule();

        /// <summary>
        /// 空粒子清理模块初始化
        /// </summary>
        partial void InitEmptyPSModule();

        /// <summary>
        /// 贴图检查模块初始化
        /// </summary>
        partial void InitTexCheckModule();

        #endregion
    }
}
