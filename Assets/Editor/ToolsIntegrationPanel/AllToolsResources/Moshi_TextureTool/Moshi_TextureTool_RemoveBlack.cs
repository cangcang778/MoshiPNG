using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace MoshiTools
{
    /// <summary>
    /// 图片处理工具 - 去黑背景模块
    /// Texture Tool - Remove black background module
    /// </summary>
    public partial class Moshi_TextureTool
    {
        #region 去黑背景 数据

        // 去黑标签页数据
        // Remove Black Tab Data
        private class RemoveBlackTabData
        {
            public Texture2D inputTexture;
            public float blackThreshold = 0f;
            public float blendIntensity = 1f;
            public string outputName = "noblack_new";
            public SizeMode sizeMode = SizeMode.Auto;
            public int manualWidth = 128;
            public int manualHeight = 128;
            public List<Texture2D> originalPreviews = new List<Texture2D>();
            public List<Texture2D> outputPreviews = new List<Texture2D>();
            public Vector2 scroll;
        }

        private RemoveBlackTabData removeBlackData = new RemoveBlackTabData();

        #endregion

        #region 去黑背景 初始化/清理

        partial void InitRemoveBlackModule() { }

        partial void ClearRemoveBlackPreviews()
        {
            ClearPreviews(removeBlackData.originalPreviews, removeBlackData.outputPreviews);
        }

        #endregion

        #region 去黑背景 拖放

        private void HandleRemoveBlackDragDrop(UnityEngine.Object[] draggedObjects)
        {
            foreach (UnityEngine.Object draggedObject in draggedObjects)
            {
                if (draggedObject is Texture2D texture)
                {
                    removeBlackData.inputTexture = texture;
                    GenerateRemoveBlackPreviews();
                    break;
                }
            }
        }

        #endregion

        #region 去黑背景 UI

        private void DrawRemoveBlackTab()
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
                        removeBlackData.inputTexture = tex;
                        GenerateRemoveBlackPreviews();
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
            removeBlackData.inputTexture = (Texture2D)EditorGUILayout.ObjectField(removeBlackData.inputTexture, typeof(Texture2D), false);
            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                removeBlackData.inputTexture = null;
                GenerateRemoveBlackPreviews();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("去黑设置", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            removeBlackData.blackThreshold = EditorGUILayout.Slider("黑色阈值", removeBlackData.blackThreshold, 0f, 1f);
            removeBlackData.blendIntensity = EditorGUILayout.Slider("混合强度", removeBlackData.blendIntensity, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                GenerateRemoveBlackPreviews();
            }

            removeBlackData.outputName = EditorGUILayout.TextField("输出名称", removeBlackData.outputName);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("输出尺寸设置", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            removeBlackData.sizeMode = (SizeMode)EditorGUILayout.EnumPopup("尺寸模式", removeBlackData.sizeMode);

            if (removeBlackData.sizeMode == SizeMode.Manual)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("宽度:", GUILayout.Width(60));
                removeBlackData.manualWidth = EditorGUILayout.IntField(Mathf.Max(1, removeBlackData.manualWidth));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("高度:", GUILayout.Width(60));
                removeBlackData.manualHeight = EditorGUILayout.IntField(Mathf.Max(1, removeBlackData.manualHeight));
                EditorGUILayout.EndHorizontal();
            }

            if (EditorGUI.EndChangeCheck())
            {
                GenerateRemoveBlackPreviews();
            }

            DrawPreviewSection(removeBlackData.originalPreviews, removeBlackData.outputPreviews, ref removeBlackData.scroll);

            EditorGUILayout.Space(15);
            if (GUILayout.Button("去除黑色", GUILayout.Height(30)))
            {
                RemoveBlack();
            }
        }

        #endregion

        #region 去黑背景 逻辑

        private void GenerateRemoveBlackPreviews()
        {
            ClearPreviews(removeBlackData.originalPreviews, removeBlackData.outputPreviews);

            if (removeBlackData.inputTexture != null)
            {
                GenerateOriginalPreview(removeBlackData.inputTexture, removeBlackData.originalPreviews);

                int width = removeBlackData.inputTexture.width;
                int height = removeBlackData.inputTexture.height;

                if (removeBlackData.sizeMode == SizeMode.Manual)
                {
                    width = removeBlackData.manualWidth;
                    height = removeBlackData.manualHeight;
                }

                Texture2D processedTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);

                Texture2D source = removeBlackData.inputTexture;
                if (width != source.width || height != source.height)
                {
                    source = ResizeTexturePoint(source, width, height);
                }

                Color[] sourcePixels = GetTexturePixels(source);
                if (sourcePixels == null) return;

                Color[] processedPixels = new Color[sourcePixels.Length];
                float threshold = removeBlackData.blackThreshold;
                float blend = removeBlackData.blendIntensity;

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    Color pixel = sourcePixels[i];
                    float maxChannel = Mathf.Max(pixel.r, pixel.g, pixel.b);
                    float alpha = Mathf.Clamp01((maxChannel - threshold) / (1 - threshold));
                    float blendedAlpha = Mathf.Lerp(pixel.a, alpha, blend);
                    processedPixels[i] = new Color(pixel.r, pixel.g, pixel.b, blendedAlpha);
                }

                processedTexture.SetPixels(processedPixels);
                processedTexture.Apply();
                removeBlackData.outputPreviews.Add(processedTexture);

                if (source != removeBlackData.inputTexture)
                {
                    DestroyImmediate(source);
                }
            }
        }

        private void RemoveBlack()
        {
            if (removeBlackData.inputTexture == null)
            {
                EditorUtility.DisplayDialog("错误", "请选择纹理", "确定");
                return;
            }

            string path = AssetDatabase.GetAssetPath(removeBlackData.inputTexture);
            string directory = Path.GetDirectoryName(path);

            int width = removeBlackData.inputTexture.width;
            int height = removeBlackData.inputTexture.height;

            if (removeBlackData.sizeMode == SizeMode.Manual)
            {
                width = removeBlackData.manualWidth;
                height = removeBlackData.manualHeight;
            }

            Texture2D processedTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);

            Texture2D source = removeBlackData.inputTexture;
            if (width != source.width || height != source.height)
            {
                source = ResizeTexturePoint(source, width, height);
            }

            Color[] sourcePixels = GetTexturePixels(source);
            if (sourcePixels == null) return;

            Color[] processedPixels = new Color[sourcePixels.Length];
            float threshold = removeBlackData.blackThreshold;
            float blend = removeBlackData.blendIntensity;

            for (int i = 0; i < sourcePixels.Length; i++)
            {
                Color pixel = sourcePixels[i];
                float maxChannel = Mathf.Max(pixel.r, pixel.g, pixel.b);
                float alpha = Mathf.Clamp01((maxChannel - threshold) / (1 - threshold));
                float blendedAlpha = Mathf.Lerp(pixel.a, alpha, blend);
                processedPixels[i] = new Color(pixel.r, pixel.g, pixel.b, blendedAlpha);
            }

            processedTexture.SetPixels(processedPixels);
            processedTexture.Apply();

            byte[] bytes = processedTexture.EncodeToPNG();
            string outputPath = Path.Combine(directory, $"{removeBlackData.outputName}.png");
            File.WriteAllBytes(outputPath, bytes);

            DestroyImmediate(processedTexture);

            if (source != removeBlackData.inputTexture)
            {
                DestroyImmediate(source);
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("成功", "黑色去除完成", "确定");
        }

        #endregion
    }
}
