using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace MoshiTools
{
    /// <summary>
    /// 图片处理工具 - 拆分通道模块
    /// Texture Tool - Split channels module
    /// </summary>
    public partial class Moshi_TextureTool
    {
        #region 拆分通道 数据

        // 通道拆分标签页数据
        // Channel Split Tab Data
        private class SplitChannelsTabData
        {
            public Texture2D inputTexture;
            public string outputName = "channel_split";
            public SizeMode sizeMode = SizeMode.Auto;
            public int manualWidth = 128;
            public int manualHeight = 128;
            public List<Texture2D> originalPreviews = new List<Texture2D>();
            public List<Texture2D> outputPreviews = new List<Texture2D>();
            public Vector2 scroll;
        }

        private SplitChannelsTabData splitChannelsData = new SplitChannelsTabData();

        #endregion

        #region 拆分通道 初始化/清理

        partial void InitSplitChannelModule() { }

        partial void ClearSplitChannelPreviews()
        {
            ClearPreviews(splitChannelsData.originalPreviews, splitChannelsData.outputPreviews);
        }

        #endregion

        #region 拆分通道 拖放

        private void HandleSplitChannelDragDrop(UnityEngine.Object[] draggedObjects)
        {
            foreach (UnityEngine.Object draggedObject in draggedObjects)
            {
                if (draggedObject is Texture2D texture)
                {
                    splitChannelsData.inputTexture = texture;
                    GenerateSplitChannelsPreviews();
                    break;
                }
            }
        }

        #endregion

        #region 拆分通道 UI

        private void DrawSplitChannelsTab()
        {
            EditorGUILayout.LabelField("输入纹理", EditorStyles.boldLabel);

            Rect dropArea = EditorGUILayout.GetControlRect(GUILayout.Height(80));
            if (dropAreaStyle != null)
            {
                GUI.Box(dropArea, "拖放纹理到这里或点击下方按钮添加", dropAreaStyle);
            }

            if (GUILayout.Button("添加选中的纹理"))
            {
                string[] guids = Selection.assetGUIDs;
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex != null)
                    {
                        splitChannelsData.inputTexture = tex;
                        GenerateSplitChannelsPreviews();
                        if (Selection.assetGUIDs.Length > 1)
                        {
                            EditorUtility.DisplayDialog("提示", "只能添加一个纹理，已自动选择第一个", "确定");
                        }
                        break;
                    }
                }
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("当前纹理:", GUILayout.Width(80));
            splitChannelsData.inputTexture = (Texture2D)EditorGUILayout.ObjectField(splitChannelsData.inputTexture, typeof(Texture2D), false);
            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                splitChannelsData.inputTexture = null;
                GenerateSplitChannelsPreviews();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("输出设置", EditorStyles.boldLabel);

            splitChannelsData.outputName = EditorGUILayout.TextField("输出名称", splitChannelsData.outputName);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("输出尺寸设置", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            splitChannelsData.sizeMode = (SizeMode)EditorGUILayout.EnumPopup("尺寸模式", splitChannelsData.sizeMode);

            if (splitChannelsData.sizeMode == SizeMode.Manual)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("宽度:", GUILayout.Width(60));
                splitChannelsData.manualWidth = EditorGUILayout.IntField(Mathf.Max(1, splitChannelsData.manualWidth));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("高度:", GUILayout.Width(60));
                splitChannelsData.manualHeight = EditorGUILayout.IntField(Mathf.Max(1, splitChannelsData.manualHeight));
                EditorGUILayout.EndHorizontal();
            }

            if (EditorGUI.EndChangeCheck())
            {
                GenerateSplitChannelsPreviews();
            }

            DrawPreviewSection(splitChannelsData.originalPreviews, splitChannelsData.outputPreviews, ref splitChannelsData.scroll);

            EditorGUILayout.Space(15);
            if (GUILayout.Button("拆分通道", GUILayout.Height(30)))
            {
                StartEditorCoroutine(SplitChannelsAsync());
            }
        }

        #endregion

        #region 拆分通道 逻辑

        private void GenerateSplitChannelsPreviews()
        {
            ClearPreviews(splitChannelsData.originalPreviews, splitChannelsData.outputPreviews);

            if (splitChannelsData.inputTexture != null)
            {
                GenerateOriginalPreview(splitChannelsData.inputTexture, splitChannelsData.originalPreviews);

                int width = splitChannelsData.inputTexture.width;
                int height = splitChannelsData.inputTexture.height;

                if (splitChannelsData.sizeMode == SizeMode.Manual)
                {
                    width = splitChannelsData.manualWidth;
                    height = splitChannelsData.manualHeight;
                }

                Texture2D source = splitChannelsData.inputTexture;
                if (width != source.width || height != source.height)
                {
                    source = ResizeTexture(source, width, height);
                }

                Color[] sourcePixels = GetTexturePixels(source);
                if (sourcePixels == null) return;

                int pixelCount = sourcePixels.Length;
                Color[] redPixels = new Color[pixelCount];
                Color[] greenPixels = new Color[pixelCount];
                Color[] bluePixels = new Color[pixelCount];
                Color[] alphaPixels = new Color[pixelCount];

                for (int i = 0; i < pixelCount; i++)
                {
                    Color pixel = sourcePixels[i];
                    redPixels[i] = new Color(pixel.r, pixel.r, pixel.r, 1f);
                    greenPixels[i] = new Color(pixel.g, pixel.g, pixel.g, 1f);
                    bluePixels[i] = new Color(pixel.b, pixel.b, pixel.b, 1f);
                    alphaPixels[i] = new Color(pixel.a, pixel.a, pixel.a, 1f);
                }

                Texture2D redChannel = GetFromTexturePool(width, height) ?? new Texture2D(width, height, TextureFormat.ARGB32, false);
                redChannel.SetPixels(redPixels);
                redChannel.Apply();

                Texture2D greenChannel = GetFromTexturePool(width, height) ?? new Texture2D(width, height, TextureFormat.ARGB32, false);
                greenChannel.SetPixels(greenPixels);
                greenChannel.Apply();

                Texture2D blueChannel = GetFromTexturePool(width, height) ?? new Texture2D(width, height, TextureFormat.ARGB32, false);
                blueChannel.SetPixels(bluePixels);
                blueChannel.Apply();

                Texture2D alphaChannel = GetFromTexturePool(width, height) ?? new Texture2D(width, height, TextureFormat.ARGB32, false);
                alphaChannel.SetPixels(alphaPixels);
                alphaChannel.Apply();

                splitChannelsData.outputPreviews.Add(redChannel);
                splitChannelsData.outputPreviews.Add(greenChannel);
                splitChannelsData.outputPreviews.Add(blueChannel);
                splitChannelsData.outputPreviews.Add(alphaChannel);

                if (source != splitChannelsData.inputTexture)
                {
                    ReturnToTexturePool(source);
                }
            }
        }

        private IEnumerator SplitChannelsAsync()
        {
            if (splitChannelsData.inputTexture == null)
            {
                EditorUtility.DisplayDialog("错误", "请选择纹理", "确定");
                yield break;
            }

            progressTitle = "Split Channel...";

            string path = AssetDatabase.GetAssetPath(splitChannelsData.inputTexture);
            string directory = Path.GetDirectoryName(path);

            int width = splitChannelsData.inputTexture.width;
            int height = splitChannelsData.inputTexture.height;

            if (splitChannelsData.sizeMode == SizeMode.Manual)
            {
                width = splitChannelsData.manualWidth;
                height = splitChannelsData.manualHeight;
            }

            Texture2D source = splitChannelsData.inputTexture;
            if (width != source.width || height != source.height)
            {
                source = ResizeTexture(source, width, height);
            }

            Color[] sourcePixels = GetTexturePixels(source);
            if (sourcePixels == null) yield break;

            int pixelCount = sourcePixels.Length;
            Color[] redPixels = new Color[pixelCount];
            Color[] greenPixels = new Color[pixelCount];
            Color[] bluePixels = new Color[pixelCount];
            Color[] alphaPixels = new Color[pixelCount];

            for (int i = 0; i < pixelCount; i++)
            {
                Color pixel = sourcePixels[i];
                redPixels[i] = new Color(pixel.r, pixel.r, pixel.r, 1f);
                greenPixels[i] = new Color(pixel.g, pixel.g, pixel.g, 1f);
                bluePixels[i] = new Color(pixel.b, pixel.b, pixel.b, 1f);
                alphaPixels[i] = new Color(pixel.a, pixel.a, pixel.a, 1f);

                if (i % 10000 == 0)
                {
                    UpdateProgress($"处理像素 {i}/{pixelCount}...", (float)i / pixelCount);
                    yield return null;
                }
            }

            Texture2D redChannel = new Texture2D(width, height, TextureFormat.ARGB32, false);
            redChannel.SetPixels(redPixels);
            redChannel.Apply();

            Texture2D greenChannel = new Texture2D(width, height, TextureFormat.ARGB32, false);
            greenChannel.SetPixels(greenPixels);
            greenChannel.Apply();

            Texture2D blueChannel = new Texture2D(width, height, TextureFormat.ARGB32, false);
            blueChannel.SetPixels(bluePixels);
            blueChannel.Apply();

            Texture2D alphaChannel = new Texture2D(width, height, TextureFormat.ARGB32, false);
            alphaChannel.SetPixels(alphaPixels);
            alphaChannel.Apply();

            byte[] bytes = redChannel.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(directory, $"{splitChannelsData.outputName}_R.png"), bytes);

            bytes = greenChannel.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(directory, $"{splitChannelsData.outputName}_G.png"), bytes);

            bytes = blueChannel.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(directory, $"{splitChannelsData.outputName}_B.png"), bytes);

            bytes = alphaChannel.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(directory, $"{splitChannelsData.outputName}_A.png"), bytes);

            DestroyImmediate(redChannel);
            DestroyImmediate(greenChannel);
            DestroyImmediate(blueChannel);
            DestroyImmediate(alphaChannel);

            if (source != splitChannelsData.inputTexture)
            {
                DestroyImmediate(source);
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("成功", "通道拆分完成", "确定");
        }

        #endregion
    }
}
