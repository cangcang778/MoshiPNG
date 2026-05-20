using UnityEngine;
using UnityEditor;
using UnityEngine.Video;
using UnityEngine.UI;
using System.IO;

/// <summary>
/// Timeline视频工具 - 一键生成视频播放所需资源
/// </summary>
public class Moshi_TLVideo : EditorWindow
{
    private const string TOOL_NAME = "Timeline视频";
    private const string PREF_RT_PATH = "Moshi_TLVideo_RTPath";
    private const string PREF_PREFAB_PATH = "Moshi_TLVideo_PrefabPath";
    private const string PREF_WIDTH = "Moshi_TLVideo_Width";
    private const string PREF_HEIGHT = "Moshi_TLVideo_Height";

    // 视频文件
    private VideoClip videoClip;
    
    // 路径设置
    private string rtPath = "";
    private string prefabPath = "";
    
    // 分辨率设置
    private int width = 1920;
    private int height = 1080;
    
    // UI选项
    private bool createUIRawImage = false;
    
    // 滚动位置
    private Vector2 scrollPos;

    [MenuItem("工具/Moshi/" + TOOL_NAME)]
    public static void ShowWindow()
    {
        var window = GetWindow<Moshi_TLVideo>(TOOL_NAME);
        window.minSize = new Vector2(400, 500);
    }

    private void OnEnable()
    {
        // 加载保存的设置
        rtPath = EditorPrefs.GetString(PREF_RT_PATH, "");
        prefabPath = EditorPrefs.GetString(PREF_PREFAB_PATH, "");
        width = EditorPrefs.GetInt(PREF_WIDTH, 1920);
        height = EditorPrefs.GetInt(PREF_HEIGHT, 1080);
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Timeline视频工具", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        MoshiHelpButton.DrawHelpButtonMini(TOOL_NAME);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("一键生成视频播放所需的RenderTexture和Prefab，然后手动添加到Timeline的Control Track中。", MessageType.Info);
        
        EditorGUILayout.Space(10);
        
        // ==================== 视频文件 ====================
        EditorGUILayout.LabelField("视频文件", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        videoClip = (VideoClip)EditorGUILayout.ObjectField("视频文件", videoClip, typeof(VideoClip), false);
        if (EditorGUI.EndChangeCheck() && videoClip != null)
        {
            // 自动获取视频分辨率
            width = (int)videoClip.width;
            height = (int)videoClip.height;
        }
        
        if (videoClip != null)
        {
            EditorGUILayout.HelpBox($"视频信息：{videoClip.width}x{videoClip.height}，时长：{videoClip.length:F2}秒", MessageType.None);
        }
        
        EditorGUILayout.Space(10);
        
        // ==================== 路径设置 ====================
        EditorGUILayout.LabelField("路径设置", EditorStyles.boldLabel);
        
        // RT路径
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("RT路径", GUILayout.Width(60));
        rtPath = EditorGUILayout.TextField(rtPath);
        if (GUILayout.Button("选择", GUILayout.Width(50)))
        {
            string selected = EditorUtility.OpenFolderPanel("选择RenderTexture保存路径", "Assets", "");
            if (!string.IsNullOrEmpty(selected))
            {
                if (selected.StartsWith(Application.dataPath))
                {
                    rtPath = "Assets" + selected.Substring(Application.dataPath.Length);
                    EditorPrefs.SetString(PREF_RT_PATH, rtPath);
                }
                else
                {
                    EditorUtility.DisplayDialog("错误", "请选择Assets目录下的文件夹", "确定");
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // Prefab路径
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Prefab路径", GUILayout.Width(60));
        prefabPath = EditorGUILayout.TextField(prefabPath);
        if (GUILayout.Button("选择", GUILayout.Width(50)))
        {
            string selected = EditorUtility.OpenFolderPanel("选择Prefab保存路径", "Assets", "");
            if (!string.IsNullOrEmpty(selected))
            {
                if (selected.StartsWith(Application.dataPath))
                {
                    prefabPath = "Assets" + selected.Substring(Application.dataPath.Length);
                    EditorPrefs.SetString(PREF_PREFAB_PATH, prefabPath);
                }
                else
                {
                    EditorUtility.DisplayDialog("错误", "请选择Assets目录下的文件夹", "确定");
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // 路径警告
        if (string.IsNullOrEmpty(rtPath) || string.IsNullOrEmpty(prefabPath))
        {
            EditorGUILayout.HelpBox("请设置RT路径和Prefab路径后才能生成资源", MessageType.Warning);
        }
        
        EditorGUILayout.Space(10);
        
        // ==================== 分辨率设置 ====================
        EditorGUILayout.LabelField("分辨率设置", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("宽度", GUILayout.Width(40));
        width = EditorGUILayout.IntField(width, GUILayout.Width(80));
        EditorGUILayout.LabelField("高度", GUILayout.Width(40));
        height = EditorGUILayout.IntField(height, GUILayout.Width(80));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        // 快捷分辨率按钮
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("快捷设置", GUILayout.Width(60));
        
        if (GUILayout.Toggle(width == 1280 && height == 720, "720P", EditorStyles.miniButtonLeft))
        {
            width = 1280; height = 720;
        }
        if (GUILayout.Toggle(width == 1920 && height == 1080, "1080P", EditorStyles.miniButtonMid))
        {
            width = 1920; height = 1080;
        }
        if (GUILayout.Toggle(width == 2560 && height == 1440, "2K", EditorStyles.miniButtonMid))
        {
            width = 2560; height = 1440;
        }
        if (GUILayout.Toggle(width == 3840 && height == 2160, "4K", EditorStyles.miniButtonRight))
        {
            width = 3840; height = 2160;
        }
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        // 保存分辨率设置
        EditorPrefs.SetInt(PREF_WIDTH, width);
        EditorPrefs.SetInt(PREF_HEIGHT, height);
        
        EditorGUILayout.Space(10);
        
        // ==================== UI选项 ====================
        EditorGUILayout.LabelField("附加选项", EditorStyles.boldLabel);
        createUIRawImage = EditorGUILayout.Toggle("同时创建UI RawImage", createUIRawImage);
        if (createUIRawImage)
        {
            EditorGUILayout.HelpBox("将在场景中创建一个Canvas和RawImage用于显示视频", MessageType.Info);
        }
        
        EditorGUILayout.Space(20);
        
        // ==================== 生成按钮 ====================
        EditorGUI.BeginDisabledGroup(videoClip == null || string.IsNullOrEmpty(rtPath) || string.IsNullOrEmpty(prefabPath));
        
        if (GUILayout.Button("一键生成", GUILayout.Height(40)))
        {
            GenerateVideoResources();
        }
        
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space(10);
        
        // ==================== 使用说明 ====================
        EditorGUILayout.LabelField("使用说明", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. 拖入视频文件\n" +
            "2. 设置RT和Prefab保存路径\n" +
            "3. 调整分辨率（可选）\n" +
            "4. 点击「一键生成」\n" +
            "5. 将生成的Prefab拖入Timeline的Control Track",
            MessageType.None);
        
        EditorGUILayout.EndScrollView();
    }

    private void GenerateVideoResources()
    {
        if (videoClip == null)
        {
            EditorUtility.DisplayDialog("错误", "请先选择视频文件", "确定");
            return;
        }
        
        if (string.IsNullOrEmpty(rtPath) || string.IsNullOrEmpty(prefabPath))
        {
            EditorUtility.DisplayDialog("错误", "请设置RT路径和Prefab路径", "确定");
            return;
        }
        
        string videoName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(videoClip));
        
        // 确保目录存在
        EnsureDirectoryExists(rtPath);
        EnsureDirectoryExists(prefabPath);
        
        // 1. 创建RenderTexture
        string rtAssetPath = $"{rtPath}/{videoName}_RT.renderTexture";
        RenderTexture rt = CreateRenderTexture(rtAssetPath);
        
        if (rt == null)
        {
            EditorUtility.DisplayDialog("错误", "创建RenderTexture失败", "确定");
            return;
        }
        
        // 2. 创建视频Prefab
        string prefabAssetPath = $"{prefabPath}/{videoName}_Video.prefab";
        GameObject prefab = CreateVideoPrefab(prefabAssetPath, rt);
        
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("错误", "创建Prefab失败", "确定");
            return;
        }
        
        // 3. 可选：创建UI RawImage
        if (createUIRawImage)
        {
            CreateUIRawImage(rt, videoName);
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // 选中生成的Prefab
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        
        EditorUtility.DisplayDialog("完成", 
            $"视频资源生成完成！\n\n" +
            $"RenderTexture: {rtAssetPath}\n" +
            $"Prefab: {prefabAssetPath}\n\n" +
            $"请将Prefab拖入Timeline的Control Track中使用。",
            "确定");
    }

    private void EnsureDirectoryExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string[] folders = path.Split('/');
            string currentPath = folders[0]; // "Assets"
            
            for (int i = 1; i < folders.Length; i++)
            {
                string newPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(newPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath = newPath;
            }
        }
    }

    private RenderTexture CreateRenderTexture(string assetPath)
    {
        // 检查是否已存在
        RenderTexture existingRT = AssetDatabase.LoadAssetAtPath<RenderTexture>(assetPath);
        if (existingRT != null)
        {
            if (!EditorUtility.DisplayDialog("确认", $"RenderTexture已存在：\n{assetPath}\n\n是否覆盖？", "覆盖", "取消"))
            {
                return existingRT;
            }
            AssetDatabase.DeleteAsset(assetPath);
        }
        
        RenderTexture rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        rt.name = Path.GetFileNameWithoutExtension(assetPath);
        
        AssetDatabase.CreateAsset(rt, assetPath);
        
        Debug.Log($"[Moshi_TLVideo] 创建RenderTexture: {assetPath}");
        
        return AssetDatabase.LoadAssetAtPath<RenderTexture>(assetPath);
    }

    private GameObject CreateVideoPrefab(string assetPath, RenderTexture rt)
    {
        // 检查是否已存在
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (existingPrefab != null)
        {
            if (!EditorUtility.DisplayDialog("确认", $"Prefab已存在：\n{assetPath}\n\n是否覆盖？", "覆盖", "取消"))
            {
                return existingPrefab;
            }
            AssetDatabase.DeleteAsset(assetPath);
        }
        
        // 创建GameObject
        GameObject videoGO = new GameObject(Path.GetFileNameWithoutExtension(assetPath));
        
        // 添加VideoPlayer
        VideoPlayer videoPlayer = videoGO.AddComponent<VideoPlayer>();
        videoPlayer.clip = videoClip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = rt;
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        
        // 添加自动播放脚本
        videoGO.AddComponent<VideoAutoPlaySimple>();
        
        // 保存为Prefab
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(videoGO, assetPath);
        
        // 删除临时对象
        DestroyImmediate(videoGO);
        
        Debug.Log($"[Moshi_TLVideo] 创建Prefab: {assetPath}");
        
        return prefab;
    }

    private void CreateUIRawImage(RenderTexture rt, string videoName)
    {
        // 查找或创建Canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("VideoCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Video Canvas");
        }
        
        // 创建RawImage
        GameObject rawImageGO = new GameObject($"{videoName}_Display");
        rawImageGO.transform.SetParent(canvas.transform, false);
        
        RawImage rawImage = rawImageGO.AddComponent<RawImage>();
        rawImage.texture = rt;
        
        // 设置RectTransform为全屏
        RectTransform rectTransform = rawImageGO.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        Undo.RegisterCreatedObjectUndo(rawImageGO, "Create Video RawImage");
        
        Debug.Log($"[Moshi_TLVideo] 创建UI RawImage: {rawImageGO.name}");
    }
}
