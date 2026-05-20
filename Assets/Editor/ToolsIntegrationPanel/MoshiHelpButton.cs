#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Moshi 工具帮助按钮通用工具类
/// </summary>
public static class MoshiHelpButton
{
    private const string DOC_ROOT = "Assets/Editor/ToolsIntegrationPanel/Documentation/";
    
    /// <summary>
    /// 工具与文档映射表（文件名与工具脚本名对应）
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<string, string> ToolDocMap = 
        new System.Collections.Generic.Dictionary<string, string>
    {
        // 工具箱总览
        { "Moshi工具箱", "Moshi_Toolbox.md" },
        // 通用工具
        { "资源收藏夹", "Moshi_Favorites.md" },
        { "批量重命名", "Moshi_BatchRename.md" },
        { "引用计数统计", "Moshi_RefCounter.md" },
        { "资源清理", "Moshi_AssetClean.md" },
        { "层级工具", "Moshi_AddParent.md" },
        // 动画相关
        { "动画片段助手", "Moshi_ClipHelper.md" },
        { "动画路径修复", "Moshi_AnimPath.md" },
        { "骨骼动画烘焙", "Moshi_SpineBake.md" },
        // 图片/贴图相关
        { "图片转面片", "Moshi_ImgToQuad.md" },
        { "SVG位置导入", "Moshi_SvgImport.md" },
        { "图片分类归档", "Moshi_ImgClassifier.md" },
        { "图片处理工具", "Moshi_TextureTool.md" },
        // 粒子系统相关
        { "粒子批量修改", "Moshi_ParticleBatch.md" },
        { "粒子排序修改", "Moshi_ParticleOrder.md" },
        { "粒子Mesh工具", "Moshi_ParticleMesh.md" },
        // 物体/路径相关
        { "物体阵列工具", "Moshi_GOArray.md" },
        { "路径工具", "Moshi_PathTool.md" },
        // Timeline相关
        { "TL批量导入", "Moshi_TLBatch.md" },
        { "TL增强", "Moshi_TLEnhance.md" },
        { "Timeline视频", "Moshi_TLVideo.md" },
        // 渲染管线
        { "渲染场景生成", "Moshi_SceneGenerator.md" },
        { "逐帧渲染", "Moshi_FrameRenderer.md" },
        // 通用/导出
        { "工具箱导出", "Moshi_Exporter.md" },
    };

    /// <summary>
    /// 在工具栏绘制帮助按钮
    /// </summary>
    /// <param name="toolName">工具显示名称</param>
    /// <param name="width">按钮宽度，默认20</param>
    public static void DrawHelpButton(string toolName, float width = 20f)
    {
        if (GUILayout.Button("?", EditorStyles.toolbarButton, GUILayout.Width(width)))
        {
            OpenToolHelp(toolName);
        }
    }

    /// <summary>
    /// 在非工具栏区域绘制帮助按钮
    /// </summary>
    /// <param name="toolName">工具显示名称</param>
    /// <param name="width">按钮宽度，默认20</param>
    public static void DrawHelpButtonMini(string toolName, float width = 20f)
    {
        if (GUILayout.Button("?", EditorStyles.miniButton, GUILayout.Width(width)))
        {
            OpenToolHelp(toolName);
        }
    }

    /// <summary>
    /// 打开工具帮助文档
    /// </summary>
    /// <param name="toolName">工具显示名称</param>
    public static void OpenToolHelp(string toolName)
    {
        string docPath = null;
        
        // 先从映射表查找
        if (ToolDocMap.TryGetValue(toolName, out string docFileName))
        {
            docPath = DOC_ROOT + docFileName;
        }
        
        // 尝试加载文档
        Object doc = null;
        if (!string.IsNullOrEmpty(docPath))
        {
            doc = AssetDatabase.LoadAssetAtPath<Object>(docPath);
            
            // 如果找不到，尝试刷新资源数据库后再查找
            if (doc == null)
            {
                AssetDatabase.Refresh();
                doc = AssetDatabase.LoadAssetAtPath<Object>(docPath);
            }
        }
        
        // 如果还是找不到，尝试直接定位到 Documentation 文件夹
        if (doc == null)
        {
            var folder = AssetDatabase.LoadAssetAtPath<Object>(DOC_ROOT.TrimEnd('/'));
            if (folder != null)
            {
                EditorGUIUtility.PingObject(folder);
                Selection.activeObject = folder;
                EditorUtility.FocusProjectWindow();
                Debug.Log($"[MoshiHelpButton] 未找到 [{toolName}] 的文档，已定位到文档文件夹。路径: {docPath}");
                return;
            }
        }
        
        if (doc != null)
        {
            EditorGUIUtility.PingObject(doc);
            Selection.activeObject = doc;
            EditorUtility.FocusProjectWindow();
            return;
        }
        
        EditorUtility.DisplayDialog("帮助文档", $"暂无 [{toolName}] 的使用说明文档\n\n尝试路径: {docPath}", "确定");
    }

    /// <summary>
    /// 获取文档路径
    /// </summary>
    public static string GetDocPath(string toolName)
    {
        if (ToolDocMap.TryGetValue(toolName, out string docFileName))
        {
            return DOC_ROOT + docFileName;
        }
        return null;
    }

    /// <summary>
    /// 检查文档是否存在
    /// </summary>
    public static bool HasDoc(string toolName)
    {
        string path = GetDocPath(toolName);
        if (string.IsNullOrEmpty(path)) return false;
        return AssetDatabase.LoadAssetAtPath<Object>(path) != null;
    }
}
#endif
