// Spine 宏定义自动管理器
// Spine define symbol auto-manager
// 自动检测项目中是否存在 Spine 插件，并管理 HAS_SPINE 宏定义
// Auto-detects Spine plugin presence and manages HAS_SPINE define symbol
//
// 工作原理 / How it works:
//   1. 通过 [InitializeOnLoad] 在编辑器启动/脚本重编译时自动运行
//   2. 检测 "spine-unity" 程序集是否存在（通过 CompilationPipeline）
//   3. 存在 → 自动添加 HAS_SPINE 宏
//   4. 不存在 → 自动移除 HAS_SPINE 宏
//   5. 仅在宏定义实际需要变更时才写入，避免不必要的重编译
//
// 注意 / Note:
//   版本适配（3.8/4.2/4.3+）由 SpineCompat.cs 在运行时通过反射自动检测，
//   不再需要 SPINE_4_3 等宏定义，彻底避免了编译期鸡生蛋问题。
//   Version adaptation (3.8/4.2/4.3+) is handled at runtime by SpineCompat.cs
//   via reflection, no SPINE_4_3 defines needed, avoiding chicken-and-egg issues.
using UnityEditor;
using UnityEditor.Compilation;
using System.Linq;

namespace MoshiTools
{
    /// <summary>
    /// Spine 宏定义自动管理器
    /// Automatically manages HAS_SPINE scripting define symbol
    /// based on whether spine-unity assembly exists
    /// </summary>
    [InitializeOnLoad]
    public static class SpineDefineManager
    {
        private const string SPINE_DEFINE = "HAS_SPINE";
        private const string SPINE_ASSEMBLY_NAME = "spine-unity";

        static SpineDefineManager()
        {
            // 延迟执行，确保 CompilationPipeline 数据已就绪
            // Delay execution to ensure CompilationPipeline data is ready
            EditorApplication.delayCall += CheckAndUpdateDefine;
        }

        /// <summary>
        /// 手动触发重新检测（供 UI 按钮调用）
        /// Manually trigger re-detection (for UI button)
        /// </summary>
        public static void ForceCheck()
        {
            CheckAndUpdateDefineInternal();
        }

        /// <summary>
        /// 检测 Spine 是否存在并更新宏定义
        /// Check if Spine exists and update define symbol
        /// </summary>
        private static void CheckAndUpdateDefine()
        {
            CheckAndUpdateDefineInternal();
        }

        private static void CheckAndUpdateDefineInternal()
        {
            bool spineExists = IsSpinePresent();

            var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

            // 获取当前宏定义
            // Get current define symbols
            string definesStr = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            var defines = definesStr.Split(';').Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
            bool hasSpineDefine = defines.Contains(SPINE_DEFINE);

            bool changed = false;

            if (spineExists && !hasSpineDefine)
            {
                // Spine 存在但宏不存在 → 添加
                // Spine exists but define doesn't → add it
                defines.Add(SPINE_DEFINE);
                changed = true;
                UnityEngine.Debug.Log($"[Moshi] 检测到 Spine 插件，已自动添加 {SPINE_DEFINE} 宏定义");
            }
            else if (!spineExists && hasSpineDefine)
            {
                // Spine 不存在但宏存在 → 移除
                // Spine doesn't exist but define does → remove it
                defines.Remove(SPINE_DEFINE);
                changed = true;
                UnityEngine.Debug.Log($"[Moshi] 未检测到 Spine 插件，已自动移除 {SPINE_DEFINE} 宏定义");
            }

            // 清理旧版遗留的 SPINE_4_3 宏（如果存在）
            // Clean up legacy SPINE_4_3 define if present
            if (defines.Contains("SPINE_4_3"))
            {
                defines.Remove("SPINE_4_3");
                changed = true;
                UnityEngine.Debug.Log("[Moshi] 已移除旧版 SPINE_4_3 宏定义（版本适配已改为运行时自动检测）");
            }

            // 仅在有变更时写入，避免不必要的重编译
            // Only write when changed, avoid unnecessary recompilation
            if (changed)
            {
                string newDefines = string.Join(";", defines);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, newDefines);
            }
        }

        /// <summary>
        /// 检测 spine-unity 程序集是否存在
        /// Check if spine-unity assembly exists
        /// </summary>
        private static bool IsSpinePresent()
        {
            // 方式1: 通过 CompilationPipeline 检测程序集
            // Method 1: Check assembly via CompilationPipeline
            var assemblies = CompilationPipeline.GetAssemblies();
            if (assemblies.Any(a => a.name == SPINE_ASSEMBLY_NAME))
                return true;

            // 方式2: 备用 - 直接检测类型（通过已加载的程序集）
            // Method 2: Fallback - check type existence via loaded assemblies
            var allAssemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in allAssemblies)
            {
                if (assembly.GetName().Name == SPINE_ASSEMBLY_NAME)
                    return true;
            }

            return false;
        }
    }
}
