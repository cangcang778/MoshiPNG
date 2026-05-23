using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace MoshiTools
{
    /// <summary>
    /// 图片处理工具 - 合并序列图模块
    /// Texture Tool - Merge sprite sheet module
    /// </summary>
    public partial class Moshi_TextureTool
    {
        #region 合并序列图 数据

        private class MergeTabData
        {
            public List<Texture2D> inputTextures = new List<Texture2D>();
            public int columns = 1;
            public int rows = 1;
            public string outputName = "merge_new";
            public SizeMode sizeMode = SizeMode.Auto;
            public int manualWidth = 512;
            public int manualHeight = 512;
            public List<Texture2D> originalPreviews = new List<Texture2D>();
            public List<Texture2D> outputPreviews = new List<Texture2D>();
            public Vector2 scroll;
            public Vector2 listScroll;
        }

        private MergeTabData mergeData = new MergeTabData();

        #endregion

        #region 合并序列图 初始化/清理

        partial void InitMergeModule() { }

        partial void ClearMergePreviews()
        {
            ClearPreviews(mergeData.originalPreviews, mergeData.outputPreviews);
        }

        #endregion

        #region 合并序列图 拖放

        private void HandleMergeDragDrop(UnityEngine.Object[] draggedObjects)
        {
            foreach (UnityEngine.Object draggedObject in draggedObjects)
            {
                if (draggedObject is Texture2D texture &&
                    !mergeData.inputTextures.Contains(texture))
                {
                    mergeData.inputTextures.Add(texture);
                }
            }
            GenerateMergePreviews();
        }

        #endregion

        #region 合并序列图 UI

        private void DrawMergeTab()
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
                    if (tex != null && !mergeData.inputTextures.Contains(tex))
                    {
                        mergeData.inputTextures.Add(tex);
                    }
                }
                GenerateMergePreviews();
            }

            EditorGUILayout.LabelField($"已选择纹理 ({mergeData.inputTextures.Count}):");

            int itemCount = Mathf.Max(1, mergeData.inputTextures.Count);
            float scrollHeight = Mathf.Min(200, 25 * itemCount + 5);

            mergeData.listScroll = EditorGUILayout.BeginScrollView(
                mergeData.listScroll,
                GUILayout.Height(scrollHeight));

            for (int i = 0; i < mergeData.inputTextures.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                mergeData.inputTextures[i] = (Texture2D)EditorGUILayout.ObjectField(
                    mergeData.inputTextures[i],
                    typeof(Texture2D),
                    false,
                    GUILayout.ExpandWidth(true));

                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    mergeData.inputTextures.RemoveAt(i);
                    i--;
                    GenerateMergePreviews();
                }
                EditorGUILayout.EndHorizontal();
            }

            if (mergeData.inputTextures.Count == 0)
            {
                EditorGUILayout.HelpBox("请添加纹理", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("合并设置", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            mergeData.columns = EditorGUILayout.IntField("列数", Mathf.Max(1, mergeData.columns));
            mergeData.rows = EditorGUILayout.IntField("行数", Mathf.Max(1, mergeData.rows));
            if (EditorGUI.EndChangeCheck())
            {
                GenerateMergePreviews();
            }

            mergeData.outputName = EditorGUILayout.TextField("输出名称", mergeData.outputName);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("输出尺寸设置", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            mergeData.sizeMode = (SizeMode)EditorGUILayout.EnumPopup("尺寸模式", mergeData.sizeMode);

            if (mergeData.sizeMode == SizeMode.Manual)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("宽度:", GUILayout.Width(60));
                mergeData.manualWidth = EditorGUILayout.IntField(Mathf.Max(1, mergeData.manualWidth));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("高度:", GUILayout.Width(60));
                mergeData.manualHeight = EditorGUILayout.IntField(Mathf.Max(1, mergeData.manualHeight));
                EditorGUILayout.EndHorizontal();
            }

            if (EditorGUI.EndChangeCheck())
            {
                GenerateMergePreviews();
            }

            DrawPreviewSection(mergeData.originalPreviews, mergeData.outputPreviews, ref mergeData.scroll);

            EditorGUILayout.Space(15);
            if (GUILayout.Button("合并纹理", GUILayout.Height(30)))
            {
                MergeTextures();
            }
        }

        #endregion

        #region 合并序列图 逻辑

        private void GenerateMergePreviews()
        {
            ClearPreviews(mergeData.originalPreviews, mergeData.outputPreviews);

            if (mergeData.inputTextures.Count > 0 && mergeData.columns > 0 && mergeData.rows > 0)
            {
                foreach (var tex in mergeData.inputTextures)
                {
                    if (tex != null) GenerateOriginalPreview(tex, mergeData.originalPreviews);
                }

                int outputWidth = 0;
                int outputHeight = 0;

                if (mergeData.sizeMode == SizeMode.Auto)
                {
                    int maxCellWidth = 0;
                    int maxCellHeight = 0;
                    foreach (var tex in mergeData.inputTextures)
                    {
                        if (tex != null)
                        {
                            maxCellWidth = Mathf.Max(maxCellWidth, tex.width);
                            maxCellHeight = Mathf.Max(maxCellHeight, tex.height);
                        }
                    }
                    outputWidth = maxCellWidth * mergeData.columns;
                    outputHeight = maxCellHeight * mergeData.rows;
                }
                else
                {
                    outputWidth = mergeData.manualWidth;
                    outputHeight = mergeData.manualHeight;
                }

                const int MAX_TEXTURE_SIZE = 16384;
                outputWidth = Mathf.Clamp(outputWidth, 1, MAX_TEXTURE_SIZE);
                outputHeight = Mathf.Clamp(outputHeight, 1, MAX_TEXTURE_SIZE);

                if (outputWidth <= 0 || outputHeight <= 0) return;

                Texture2D mergedTexture = new Texture2D(outputWidth, outputHeight, TextureFormat.ARGB32, false);
                Color[] mergedPixels = new Color[outputWidth * outputHeight];

                int tileWidth = outputWidth / mergeData.columns;
                int tileHeight = outputHeight / mergeData.rows;

                int index = 0;
                for (int y = 0; y < mergeData.rows && index < mergeData.inputTextures.Count; y++)
                {
                    for (int x = 0; x < mergeData.columns && index < mergeData.inputTextures.Count; x++)
                    {
                        Texture2D tex = mergeData.inputTextures[index];
                        if (tex == null)
                        {
                            index++;
                            continue;
                        }

                        Texture2D resized = ResizeTexturePoint(tex, tileWidth, tileHeight);
                        Color[] resizedPixels = resized.GetPixels();

                        int startX = x * tileWidth;
                        int startY = (mergeData.rows - 1 - y) * tileHeight;

                        for (int py = 0; py < tileHeight; py++)
                        {
                            int mergedIndex = (startY + py) * outputWidth + startX;
                            int resizedIndex = py * tileWidth;
                            System.Array.Copy(resizedPixels, resizedIndex, mergedPixels, mergedIndex, tileWidth);
                        }

                        DestroyImmediate(resized);
                        index++;
                    }
                }

                mergedTexture.SetPixels(mergedPixels);
                mergedTexture.Apply();
                mergeData.outputPreviews.Add(mergedTexture);
            }
        }

        private void MergeTextures()
        {
            if (mergeData.inputTextures.Count == 0 || mergeData.columns <= 0 || mergeData.rows <= 0)
            {
                EditorUtility.DisplayDialog("错误", "请添加纹理并设置有效的合并参数", "确定");
                return;
            }

            string directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(mergeData.inputTextures[0]));

            int outputWidth = 0;
            int outputHeight = 0;

            if (mergeData.sizeMode == SizeMode.Auto)
            {
                int maxCellWidth = 0;
                int maxCellHeight = 0;
                foreach (var tex in mergeData.inputTextures)
                {
                    if (tex != null)
                    {
                        maxCellWidth = Mathf.Max(maxCellWidth, tex.width);
                        maxCellHeight = Mathf.Max(maxCellHeight, tex.height);
                    }
                }
                outputWidth = maxCellWidth * mergeData.columns;
                outputHeight = maxCellHeight * mergeData.rows;
            }
            else
            {
                outputWidth = mergeData.manualWidth;
                outputHeight = mergeData.manualHeight;
            }

            const int MAX_TEXTURE_SIZE = 16384;
            outputWidth = Mathf.Clamp(outputWidth, 1, MAX_TEXTURE_SIZE);
            outputHeight = Mathf.Clamp(outputHeight, 1, MAX_TEXTURE_SIZE);

            if (outputWidth <= 0 || outputHeight <= 0) return;

            Texture2D mergedTexture = new Texture2D(outputWidth, outputHeight, TextureFormat.ARGB32, false);
            Color[] mergedPixels = new Color[outputWidth * outputHeight];

            int tileWidth = outputWidth / mergeData.columns;
            int tileHeight = outputHeight / mergeData.rows;

            int index = 0;
            for (int y = 0; y < mergeData.rows && index < mergeData.inputTextures.Count; y++)
            {
                for (int x = 0; x < mergeData.columns && index < mergeData.inputTextures.Count; x++)
                {
                    Texture2D tex = mergeData.inputTextures[index];
                    if (tex == null)
                    {
                        index++;
                        continue;
                    }

                    Texture2D resized = ResizeTexturePoint(tex, tileWidth, tileHeight);
                    Color[] resizedPixels = resized.GetPixels();

                    int startX = x * tileWidth;
                    int startY = (mergeData.rows - 1 - y) * tileHeight;

                    for (int py = 0; py < tileHeight; py++)
                    {
                        int mergedIndex = (startY + py) * outputWidth + startX;
                        int resizedIndex = py * tileWidth;
                        System.Array.Copy(resizedPixels, resizedIndex, mergedPixels, mergedIndex, tileWidth);
                    }

                    DestroyImmediate(resized);
                    index++;
                }
            }

            mergedTexture.SetPixels(mergedPixels);
            mergedTexture.Apply();

            byte[] bytes = mergedTexture.EncodeToPNG();
            string outputPath = Path.Combine(directory, $"{mergeData.outputName}.png");
            File.WriteAllBytes(outputPath, bytes);

            DestroyImmediate(mergedTexture);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("成功", "纹理合并完成", "确定");
        }

        #endregion
    }
}
