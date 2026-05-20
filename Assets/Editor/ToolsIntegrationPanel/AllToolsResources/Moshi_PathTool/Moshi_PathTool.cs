using UnityEngine;
using UnityEditor;
using Moshi.PathTool;

namespace Moshi.PathTool.Editor
{
    /// <summary>
    /// 路径工具主入口
    /// 提供菜单项访问导入器和烘焙器
    /// </summary>
    public static class Moshi_PathToolMenu
    {
        private const string MENU_ROOT = "工具/Moshi/路径工具/";
        private const string GAMEOBJECT_MENU = "GameObject/Moshi/路径工具/";

        #region 菜单项

        [MenuItem(MENU_ROOT + "路径导入器", false, 0)]
        public static void OpenPathImporter()
        {
            PathImporterWindow.ShowWindow();
        }

        [MenuItem(MENU_ROOT + "动画烘焙器", false, 1)]
        public static void OpenAnimationBaker()
        {
            PathAnimationBaker.ShowWindow();
        }

        [MenuItem(MENU_ROOT + "---", false, 10)]
        public static void Separator1() { }

        [MenuItem(MENU_ROOT + "为选中物体添加路径跟随", false, 11)]
        public static void AddPathFollowerToSelected()
        {
            if (Selection.activeGameObject != null)
            {
                PathFollower follower = Selection.activeGameObject.GetComponent<PathFollower>();
                if (follower == null)
                {
                    Undo.AddComponent<PathFollower>(Selection.activeGameObject);
                    Debug.Log($"已为 {Selection.activeGameObject.name} 添加 PathFollower 组件");
                }
                else
                {
                    Debug.LogWarning("选中的物体已有 PathFollower 组件");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "请先选中一个物体", "确定");
            }
        }

        // 分隔符已在上方注册过，避免重复注册第二个同名分隔符导致报错

        [MenuItem(MENU_ROOT + "帮助", false, 21)]
        public static void ShowHelp()
        {
            EditorUtility.DisplayDialog("路径工具帮助",
                "=== 路径工具使用说明 ===\n\n" +
                "【导入路径】\n" +
                "1. 在3ds Max中创建样条线\n" +
                "2. 转换为Mesh后导出FBX，或使用MaxScript导出CSV/JSON\n" +
                "3. 打开 路径导入器 窗口\n" +
                "4. 设置参数后点击预览，确认无误后导入\n\n" +
                "【动画烘焙】\n" +
                "1. 导入路径后，打开 动画烘焙器 窗口\n" +
                "2. 设置路径、目标物体和动画参数\n" +
                "3. 点击烘焙生成AnimationClip\n\n" +
                "【路径跟随】\n" +
                "1. 为物体添加 PathFollower 组件\n" +
                "2. 设置要跟随的路径(PathCreator)\n" +
                "3. 配置速度、跟随模式等参数\n" +
                "4. 运行时调用 Play() 开始跟随",
                "确定");
        }

        #endregion

        #region GameObject菜单

        [MenuItem(GAMEOBJECT_MENU + "添加路径跟随器", false, 0)]
        public static void AddPathFollowerFromGameObjectMenu()
        {
            AddPathFollowerToSelected();
        }

        #endregion

        #region 右键菜单

        [MenuItem("CONTEXT/PathCreator/烘焙为动画")]
        public static void BakeToAnimation(MenuCommand command)
        {
            PathCreator pathCreator = command.context as PathCreator;
            if (pathCreator == null)
                return;

            var window = EditorWindow.GetWindow<PathAnimationBaker>("路径动画烘焙器");
            var field = typeof(PathAnimationBaker).GetField("pathCreator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(window, pathCreator);
            }
        }

        [MenuItem("CONTEXT/PathCreator/打开导入器")]
        public static void OpenImporterForPath(MenuCommand command)
        {
            PathCreator pathCreator = command.context as PathCreator;
            if (pathCreator == null)
                return;

            PathImporterWindow.ShowWindow(pathCreator);
        }

        [MenuItem("CONTEXT/PathFollower/定位到路径起点")]
        public static void MoveToPathStart(MenuCommand command)
        {
            PathFollower follower = command.context as PathFollower;
            if (follower == null || follower.pathCreator == null)
                return;

            Undo.RecordObject(follower.transform, "Move to Path Start");
            follower.SetProgress(0f);
        }

        [MenuItem("CONTEXT/PathFollower/定位到路径终点")]
        public static void MoveToPathEnd(MenuCommand command)
        {
            PathFollower follower = command.context as PathFollower;
            if (follower == null || follower.pathCreator == null)
                return;

            Undo.RecordObject(follower.transform, "Move to Path End");
            follower.SetProgress(1f);
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 为物体添加路径跟随功能
        /// </summary>
        public static PathFollower SetupPathFollower(GameObject target, PathCreator path)
        {
            PathFollower follower = target.GetComponent<PathFollower>();
            if (follower == null)
            {
                follower = target.AddComponent<PathFollower>();
            }
            follower.pathCreator = path;
            return follower;
        }

        #endregion
    }
}
