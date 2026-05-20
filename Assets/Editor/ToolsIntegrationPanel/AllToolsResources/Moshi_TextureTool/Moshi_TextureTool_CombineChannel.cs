using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace MoshiTools
{
    /// <summary>
    /// 图片处理工具 - 合并通道模块
    /// Texture Tool - Combine channels module
    /// </summary>
    public partial class Moshi_TextureTool
    {
        #region 合并通道 数据

        // 通道合并标签页数据
        // Channel Merge Tab Data
        private class CombineChannelsTabData
        {
            public List<Texture2D> inputTextures = new List<Texture2D>();
            public string outputName = "channel_combine";
            public SizeMode sizeMode = SizeMode.Auto;
            public int manualWidth = 128;
            public int manualHeight = 128;
            public List<Texture2D> originalPreviews = new List<Texture2D>();
            public List<Texture2D> outputPreviews = new List<Texture2D>();
            public Vector2 scroll;
            public Vector2 listScroll;
            public int nextReplaceIndex = 0;
        }

        private CombineChannelsTabData combineChannelsData = new CombineChannelsTabData();

        #endregion

        #region 合并通道 初始化/清理

        partial void InitCombineChannelModule() { }

        partial void ClearCombineChannelPreviews()
        {
            ClearPreviews(combineChannelsData.originalPreviews, combineChannelsData.outputPreviews);
        }

        #endregion

        #region 合并通道 拖放

        private void HandleCombineChannelDragDrop(UnityEngine.Object[] draggedObjects)
        {
            int added = 0;
            foreach (UnityEngine.Object draggedObject in draggedObjects)
            {
                if (added >= 4) break;
                if (draggedObject is Texture2D texture && !combineChannelsData.inputTextures.Contains(texture))
                {
                    if (combineChannelsData.inputTextures.Count < 4)
                    {
                        combineChannelsData.inputTextures.Add(texture);
                    }
                    else
                    {
                        combineChannelsData.inputTextures[combineChannelsData.nextReplaceIndex] = texture;
                        combineChannelsData.nextReplaceIndex = (combineChannelsData.nextReplaceIndex + 1) % 4;
                    }
                    added++;
                }
            }
            GenerateCombineChannelsPreviews();
        }

        #endregion

        #region 合并通道 UI

        private void DrawCombineChannelsTab()
        {
            EditorGUILayout.LabelField("输入纹理", EditorStyles.boldLabel);

            Rect dropArea = EditorGUILayout.GetControlRect(GUILayout.Height(80));
            if (dropAreaStyle != null)
            {
                GUI.Box(dropArea, "拖放纹理到这里或点击下方按钮添加", dropAreaStyle);
            }

            if (GUILayout.Button("添加选中的纹理"))
            {
                int prevCount = combineChannelsData.inputTextures.Count;
                string[] guids = Selection.assetGUIDs;
                int added = 0;

                foreach (string guid in guids)
                {
                    if (added >= 4) break;

                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex != null && !combineChannelsData.inputTextures.Contains(tex))
                    {
                        if (combineChannelsData.inputTextures.Count < 4)
                        {
                            combineChannelsData.inputTextures.Add(tex);
                        }
                        else
                        {
                            combineChannelsData.inputTextures[combineChannelsData.nextReplaceIndex] = tex;
                            combineChannelsData.nextReplaceIndex = (combineChannelsData.nextReplaceIndex + 1) % 4;
                        }
                        added++;
                    }
                }

                GenerateCombineChannelsPreviews();

                int totalSelected = Selection.assetGUIDs.Length;
                if (totalSelected > 4)
                {
                    EditorUtility.DisplayDialog("提示", $"最多添加4个纹理，已自动截取前4个", "确定");
                }
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"已选择纹理 ({combineChannelsData.inputTextures.Count}/4):");

            if (combineChannelsData.inputTextures.Count >= 4)
            {
                GUIStyle warningStyle = new GUIStyle(EditorStyles.label);
                warningStyle.normal.textColor = Color.yellow;
                EditorGUILayout.LabelField("(已达上限)", warningStyle);
            }
            EditorGUILayout.EndHorizontal();

            int itemCount = Mathf.Max(1, combineChannelsData.inputTextures.Count);
            float scrollHeight = Mathf.Min(200, 25 * itemCount + 5);

            combineChannelsData.listScroll = EditorGUILayout.BeginScrollView(
                combineChannelsData.listScroll,
                GUILayout.Height(scrollHeight));

            for (int i = 0; i < combineChannelsData.inputTextures.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                combineChannelsData.inputTextures[i] = (Texture2D)EditorGUILayout.ObjectField(
                    combineChannelsData.inputTextures[i],
                    typeof(Texture2D),
                    false,
                    GUILayout.ExpandWidth(true));

                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    combineChannelsData.inputTextures.RemoveAt(i);
                    i--;
                    GenerateCombineChannelsPreviews();
                }
                EditorGUILayout.EndHorizontal();
            }

            if (combineChannelsData.inputTextures.Count == 0)
            {
                EditorGUILayout.HelpBox("请添加纹理 (最多4个)", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("输出设置", EditorStyles.boldLabel);

            combineChannelsData.outputName = EditorGUILayout.TextField("输出名称", combineChannelsData.outputName);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("输出尺寸设置", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            combineChannelsData.sizeMode = (SizeMode)EditorGUILayout.EnumPopup("尺寸模式", combineChannelsData.sizeMode);

            if (combineChannelsData.sizeMode == SizeMode.Manual)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("宽度:", GUILayout.Width(60));
                combineChannelsData.manualWidth = EditorGUILayout.IntField(Mathf.Max(1, combineChannelsData.manualWidth));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("高度:", GUILayout.Width(60));
                combineChannelsData.manualHeight = EditorGUILayout.IntField(Mathf.Max(1, combineChannelsData.manualHeight));
                EditorGUILayout.EndHorizontal();
            }

            if (EditorGUI.EndChangeCheck())
            {
                GenerateCombineChannelsPreviews();
            }

            DrawPreviewSection(combineChannelsData.originalPreviews, combineChannelsData.outputPreviews, ref combineChannelsData.scroll);

            EditorGUILayout.Space(15);
            if (GUILayout.Button("合并通道", GUILayout.Height(30)))
            {
                StartEditorCoroutine(CombineChannelsAsync());
            }
        }

        #endregion

        #region 合并通道 逻辑

        private void GenerateCombineChannelsPreviews()
        {
            ClearPreviews(combineChannelsData.originalPreviews, combineChannelsData.outputPreviews);

            if (combineChannelsData.inputTextures.Count > 0)
            {
                foreach (var tex in combineChannelsData.inputTextures)
                {
                    if (tex != null) GenerateOriginalPreview(tex, combineChannelsData.originalPreviews);
                }

                int width = 0;
                int height = 0;

                if (combineChannelsData.sizeMode == SizeMode.Auto)
                {
                    foreach (var tex in combineChannelsData.inputTextures)
                    {
                        if (tex != null)
                        {
                            width = Mathf.Max(width, tex.width);
                            height = Mathf.Max(height, tex.height);
                        }
                    }
                }
                else
                {
                    width = combineChannelsData.manualWidth;
                    height = combineChannelsData.manualHeight;
                }

                if (width == 0 || height == 0) return;

                Texture2D combinedTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
                Color[] combinedPixels = new Color[width * height];

                List<Texture2D> channels = new List<Texture2D>();
                foreach (var tex in combineChannelsData.inputTextures)
                {
                    if (tex != null)
                    {
                        Texture2D processed = ProcessTransparentTexture(tex, width, height);
                        channels.Add(processed);
                    }
                }

                while (channels.Count < 4)
                {
                    Texture2D defaultTex = new Texture2D(width, height, TextureFormat.ARGB32, false);
                    Color defaultColor = channels.Count == 3 ? Color.white : Color.black;

                    Color[] colors = new Color[width * height];
                    for (int i = 0; i < colors.Length; i++)
                    {
                        colors[i] = defaultColor;
                    }
                    defaultTex.SetPixels(colors);
                    defaultTex.Apply();
                    channels.Add(defaultTex);
                }

                Color[] rPixels = GetTexturePixels(channels[0]);
                Color[] gPixels = GetTexturePixels(channels[1]);
                Color[] bPixels = GetTexturePixels(channels[2]);
                Color[] aPixels = GetTexturePixels(channels[3]);

                if (rPixels != null && gPixels != null && bPixels != null && aPixels != null)
                {
                    for (int i = 0; i < combinedPixels.Length; i++)
                    {
                        float r = rPixels[i].grayscale;
                        float g = gPixels[i].grayscale;
                        float b = bPixels[i].grayscale;
                        float a = aPixels[i].grayscale;
                        combinedPixels[i] = new Color(r, g, b, a);
                    }
                }

                combinedTexture.SetPixels(combinedPixels);
                combinedTexture.Apply();
                combineChannelsData.outputPreviews.Add(combinedTexture);

                foreach (var tex in channels)
                {
                    if (tex != null && !combineChannelsData.inputTextures.Contains(tex))
                    {
                        DestroyImmediate(tex);
                    }
                }
            }
        }

        private IEnumerator CombineChannelsAsync()
        {
            if (combineChannelsData.inputTextures.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "请至少添加一个纹理", "确定");
                yield break;
            }

            progressTitle = "Merge Channels...";

            string directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(combineChannelsData.inputTextures[0]));

            int width = 0;
            int height = 0;

            if (combineChannelsData.sizeMode == SizeMode.Auto)
            {
                foreach (var tex in combineChannelsData.inputTextures)
                {
                    if (tex != null)
                    {
                        width = Mathf.Max(width, tex.width);
                        height = Mathf.Max(height, tex.height);
                    }
                }
            }
            else
            {
                width = combineChannelsData.manualWidth;
                height = combineChannelsData.manualHeight;
            }

            if (width == 0 || height == 0) yield break;

            Texture2D combinedTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            Color[] combinedPixels = new Color[width * height];

            List<Texture2D> channels = new List<Texture2D>();
            int total = combineChannelsData.inputTextures.Count;
            int processed = 0;

            foreach (var tex in combineChannelsData.inputTextures)
            {
                if (tex != null)
                {
                    Texture2D processedTex = ProcessTransparentTexture(tex, width, height);
                    channels.Add(processedTex);
                }

                processed++;
                UpdateProgress($"准备通道 ({processed}/{total})...", (float)processed / total);
                yield return null;
            }

            while (channels.Count < 4)
            {
                Texture2D defaultTex = new Texture2D(width, height, TextureFormat.ARGB32, false);
                Color defaultColor = channels.Count == 3 ? Color.white : Color.black;

                Color[] colors = new Color[width * height];
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = defaultColor;
                }
                defaultTex.SetPixels(colors);
                defaultTex.Apply();
                channels.Add(defaultTex);
                yield return null;
            }

            Color[] rPixels = GetTexturePixels(channels[0]);
            Color[] gPixels = GetTexturePixels(channels[1]);
            Color[] bPixels = GetTexturePixels(channels[2]);
            Color[] aPixels = GetTexturePixels(channels[3]);

            if (rPixels != null && gPixels != null && bPixels != null && aPixels != null)
            {
                for (int i = 0; i < combinedPixels.Length; i++)
                {
                    float r = rPixels[i].grayscale;
                    float g = gPixels[i].grayscale;
                    float b = bPixels[i].grayscale;
                    float a = aPixels[i].grayscale;
                    combinedPixels[i] = new Color(r, g, b, a);

                    if (i % 10000 == 0)
                    {
                        UpdateProgress($"合并像素 {i}/{combinedPixels.Length}...", (float)i / combinedPixels.Length);
                        yield return null;
                    }
                }
            }

            combinedTexture.SetPixels(combinedPixels);
            combinedTexture.Apply();

            byte[] bytes = combinedTexture.EncodeToPNG();
            string outputPath = Path.Combine(directory, $"{combineChannelsData.outputName}.png");
            File.WriteAllBytes(outputPath, bytes);

            DestroyImmediate(combinedTexture);
            foreach (var tex in channels)
            {
                if (tex != null && !combineChannelsData.inputTextures.Contains(tex))
                {
                    DestroyImmediate(tex);
                }
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("成功", "通道合并完成", "确定");
        }

        #endregion
    }
}
