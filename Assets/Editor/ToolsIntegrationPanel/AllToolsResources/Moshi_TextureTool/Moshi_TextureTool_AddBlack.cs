using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace MoshiTools
{
    /// <summary>
    /// 图片处理工具 - 加黑背景模块
    /// Texture Tool - Add black background module
    /// </summary>
    public partial class Moshi_TextureTool
    {
        #region 加黑背景 数据

        // 加黑背景标签页数据
        // Add Black Background Tab Data
        private class AddBlackBackgroundTabData
        {
            public Texture2D inputTexture;
            public string outputName = "blackbg_new";
            public SizeMode sizeMode = SizeMode.Auto;
            public int manualWidth = 128;
            public int manualHeight = 128;
            public List<Texture2D> originalPreviews = new List<Texture2D>();
            public List<Texture2D> outputPreviews = new List<Texture2D>();
            public Vector2 scroll;
        }

        private AddBlackBackgroundTabData addBlackBgData = new AddBlackBackgroundTabData();

        #endregion

        #region 加黑背景 初始化/清理

        partial void InitAddBlackModule() { }

        partial void ClearAddBlackPreviews()
        {
            ClearPreviews(addBlackBgData.originalPreviews, addBlackBgData.outputPreviews);
        }

        #endregion

        #region 加黑背景 拖放

        private void HandleAddBlackDragDrop(UnityEngine.Object[] draggedObjects)
        {
            foreach (UnityEngine.Object draggedObject in draggedObjects)
            {
                if (draggedObject is Texture2D texture)
                {
                    addBlackBgData.inputTexture = texture;
                    GenerateAddBlackBackgroundPreviews();
                    break;
                }
            }
        }

        #endregion

        #region 加黑背景 UI

        private void DrawAddBlackBackgroundTab()
        {
            EditorGUILayout.LabelField("输入纹理", EditorStyles.boldLabel);

            Rect dropArea = EditorGUILayout.GetControlRect(GUILayout.Height(80));
            if (dropAreaStyle != null)
            {
                GUI.Box(dropArea, "拖放透明纹理到这里或点击下方按钮添加", dropAreaStyle);
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
                        addBlackBgData.inputTexture = tex;
                        GenerateAddBlackBackgroundPreviews();
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
            addBlackBgData.inputTexture = (Texture2D)EditorGUILayout.ObjectField(addBlackBgData.inputTexture, typeof(Texture2D), false);
            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                addBlackBgData.inputTexture = null;
                GenerateAddBlackBackgroundPreviews();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("输出设置", EditorStyles.boldLabel);

            addBlackBgData.outputName = EditorGUILayout.TextField("输出名称", addBlackBgData.outputName);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("输出尺寸设置", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            addBlackBgData.sizeMode = (SizeMode)EditorGUILayout.EnumPopup("尺寸模式", addBlackBgData.sizeMode);

            if (addBlackBgData.sizeMode == SizeMode.Manual)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("宽度:", GUILayout.Width(60));
                addBlackBgData.manualWidth = EditorGUILayout.IntField(Mathf.Max(1, addBlackBgData.manualWidth));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("高度:", GUILayout.Width(60));
                addBlackBgData.manualHeight = EditorGUILayout.IntField(Mathf.Max(1, addBlackBgData.manualHeight));
                EditorGUILayout.EndHorizontal();
            }

            if (EditorGUI.EndChangeCheck())
            {
                GenerateAddBlackBackgroundPreviews();
            }

            DrawPreviewSection(addBlackBgData.originalPreviews, addBlackBgData.outputPreviews, ref addBlackBgData.scroll);

            EditorGUILayout.Space(15);
            if (GUILayout.Button("添加黑色背景", GUILayout.Height(30)))
            {
                AddBlackBackground();
            }
        }

        #endregion

        #region 加黑背景 逻辑

        private void GenerateAddBlackBackgroundPreviews()
        {
            ClearPreviews(addBlackBgData.originalPreviews, addBlackBgData.outputPreviews);

            if (addBlackBgData.inputTexture != null)
            {
                GenerateOriginalPreview(addBlackBgData.inputTexture, addBlackBgData.originalPreviews);

                int width = addBlackBgData.inputTexture.width;
                int height = addBlackBgData.inputTexture.height;

                if (addBlackBgData.sizeMode == SizeMode.Manual)
                {
                    width = addBlackBgData.manualWidth;
                    height = addBlackBgData.manualHeight;
                }

                Texture2D processedTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);

                Texture2D source = addBlackBgData.inputTexture;
                if (width != source.width || height != source.height)
                {
                    source = ResizeTexture(source, width, height);
                }

                Color[] sourcePixels = GetTexturePixels(source);
                if (sourcePixels == null) return;

                Color[] processedPixels = new Color[sourcePixels.Length];

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    Color pixel = sourcePixels[i];
                    processedPixels[i] = new Color(
                        pixel.r * pixel.a,
                        pixel.g * pixel.a,
                        pixel.b * pixel.a,
                        1.0f
                    );
                }

                processedTexture.SetPixels(processedPixels);
                processedTexture.Apply();
                addBlackBgData.outputPreviews.Add(processedTexture);

                if (source != addBlackBgData.inputTexture)
                {
                    DestroyImmediate(source);
                }
            }
        }

        private void AddBlackBackground()
        {
            if (addBlackBgData.inputTexture == null)
            {
                EditorUtility.DisplayDialog("错误", "请选择纹理", "确定");
                return;
            }

            string path = AssetDatabase.GetAssetPath(addBlackBgData.inputTexture);
            string directory = Path.GetDirectoryName(path);

            int width = addBlackBgData.inputTexture.width;
            int height = addBlackBgData.inputTexture.height;

            if (addBlackBgData.sizeMode == SizeMode.Manual)
            {
                width = addBlackBgData.manualWidth;
                height = addBlackBgData.manualHeight;
            }

            Texture2D processedTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);

            Texture2D source = addBlackBgData.inputTexture;
            if (width != source.width || height != source.height)
            {
                source = ResizeTexture(source, width, height);
            }

            Color[] sourcePixels = GetTexturePixels(source);
            if (sourcePixels == null) return;

            Color[] processedPixels = new Color[sourcePixels.Length];

            for (int i = 0; i < sourcePixels.Length; i++)
            {
                Color pixel = sourcePixels[i];
                processedPixels[i] = new Color(
                    pixel.r * pixel.a,
                    pixel.g * pixel.a,
                    pixel.b * pixel.a,
                    1.0f
                );
            }

            processedTexture.SetPixels(processedPixels);
            processedTexture.Apply();

            byte[] bytes = processedTexture.EncodeToPNG();
            string outputPath = Path.Combine(directory, $"{addBlackBgData.outputName}.png");
            File.WriteAllBytes(outputPath, bytes);

            DestroyImmediate(processedTexture);

            if (source != addBlackBgData.inputTexture)
            {
                DestroyImmediate(source);
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("成功", "黑色背景添加完成", "确定");
        }

        #endregion
    }
}
