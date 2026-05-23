using UnityEngine;
using UnityEditor;
using System.IO;

namespace MoshiTools
{
    /// <summary>
    /// Moshi 项目基础结构快速创建工具
    /// 在Project窗口文件夹空白处右键，一键创建特效项目标准文件夹结构
    /// </summary>
    public static class Moshi_ProjectSetup
    {
        private const string MENU_PATH = "Assets/一键创建基础项目";
        private const int MENU_PRIORITY = 100;

        /// <summary>
        /// 标准特效项目文件夹结构
        /// </summary>
        private static readonly string[] FolderStructure = new string[]
        {
            "Prefabs",           // 预制体
            "Materials",         // 材质
            "Textures",          // 贴图
            "Mesh",              // 模型
            "Animations",        // 动画
        };

        [MenuItem(MENU_PATH, false, MENU_PRIORITY)]
        private static void CreateBasicProjectStructure()
        {
            // 获取当前选中的文件夹路径
            string targetPath = GetSelectedFolderPath();
            
            if (string.IsNullOrEmpty(targetPath))
            {
                EditorUtility.DisplayDialog("提示", "请先在Project窗口中选择一个文件夹", "确定");
                return;
            }

            // 在目标路径下创建 New Folder
            string newFolderPath = Path.Combine(targetPath, "New Folder");
            
            // 如果已存在，添加序号
            int index = 1;
            string originalPath = newFolderPath;
            while (Directory.Exists(newFolderPath))
            {
                newFolderPath = $"{originalPath} {index}";
                index++;
            }

            // 创建主文件夹
            Directory.CreateDirectory(newFolderPath);
            
            // 创建所有子文件夹
            int createdCount = 0;
            foreach (string folder in FolderStructure)
            {
                string fullPath = Path.Combine(newFolderPath, folder);
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    createdCount++;
                }
            }

            // 刷新资源数据库
            AssetDatabase.Refresh();

            // 选中新创建的文件夹
            string relativePath = newFolderPath.Replace("\\", "/").Replace(Application.dataPath, "Assets");
            Object folderObj = AssetDatabase.LoadAssetAtPath<Object>(relativePath);
            if (folderObj != null)
            {
                Selection.activeObject = folderObj;
                EditorGUIUtility.PingObject(folderObj);
            }

            EditorUtility.DisplayDialog("完成", 
                $"已在 {relativePath} 创建基础项目结构\n\n共创建 {createdCount} 个文件夹", 
                "确定");
        }

        /// <summary>
        /// 获取当前在Project窗口中选中的文件夹路径
        /// </summary>
        private static string GetSelectedFolderPath()
        {
            // 如果有选中的对象，尝试获取其所在文件夹
            if (Selection.activeObject != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                
                if (!string.IsNullOrEmpty(assetPath))
                {
                    // 如果是文件夹，直接返回
                    if (Directory.Exists(assetPath))
                    {
                        return Path.Combine(Application.dataPath, assetPath.Replace("Assets/", "").Replace("Assets", ""));
                    }
                    // 如果是文件，返回其所在文件夹
                    else
                    {
                        string dir = Path.GetDirectoryName(assetPath);
                        return Path.Combine(Application.dataPath, dir.Replace("Assets/", "").Replace("Assets", ""));
                    }
                }
            }

            // 默认返回 Assets 目录
            return Application.dataPath;
        }

        /// <summary>
        /// 菜单项验证 - 只有在Project窗口中才显示
        /// </summary>
        [MenuItem(MENU_PATH, true, MENU_PRIORITY)]
        private static bool ValidateCreateBasicProjectStructure()
        {
            // 在Project窗口中始终可用
            return true;
        }
    }
}
