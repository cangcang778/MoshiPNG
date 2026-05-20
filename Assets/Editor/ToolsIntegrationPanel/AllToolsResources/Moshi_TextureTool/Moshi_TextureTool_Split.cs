using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace MoshiTools
{
    /// <summary>
    /// 图片处理工具 - 拆分序列图模块
    /// Texture Tool - Split sprite sheet module
    /// </summary>
    public partial class Moshi_TextureTool
    {
        #region 拆分序列图 数据

        // 拆分标签页数据
        // Split Tab Data
        private class SplitTabData
        {
            public Texture2D inputTexture;
            public int columns = 1;
            public int rows = 1;
            public string outputName = "split_new";
            public SizeMode sizeMode = SizeMode.Auto;
            public int manualWidth = 64;
            public int manualHeight = 64;
            public List<Texture2D> originalPreviews = new List<Texture2D>();
            public List<Texture2D> outputPreviews = new List<Texture2D>();
            public Vector2 scroll;
        }

        private SplitTabData splitData = new SplitTabData();

        #endregion

        #region 拆分序列图 初始化/清理

        partial void InitSplitModule() { }

        partial void ClearSplitPreviews()
        {
            ClearPreviews(splitData.originalPreviews, splitData.outputPreviews);
        }

        #endregion

        #region 拆分序列图 拖放

        private void HandleSplitDragDrop(UnityEngine.Object[] draggedObjects)
        {
            foreach (UnityEngine.Object draggedObject in draggedObjects)
            {
                if (draggedObject is Texture2D texture)
                {
                    splitData.inputTexture = texture;
                    GenerateSplitPreviews();
                    break;
                }
            }
        }

        #endregion

        #region 拆分序列图 UI

        private void DrawSplitTab()
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
                        splitData.inputTexture = tex;
                        GenerateSplitPreviews();
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
            splitData.inputTexture = (Texture2D)EditorGUILayout.ObjectField(splitData.inputTexture, typeof(Texture2D), false);
            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                splitData.inputTexture = null;
                GenerateSplitPreviews();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("拆分设置", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            splitData.columns = EditorGUILayout.IntField("列数", Mathf.Max(1, splitData.columns));
            splitData.rows = EditorGUILayout.IntField("行数", Mathf.Max(1, splitData.rows));
            if (EditorGUI.EndChangeCheck())
            {
                GenerateSplitPreviews();
            }

            splitData.outputName = EditorGUILayout.TextField("输出名称", splitData.outputName);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("输出尺寸设置", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            splitData.sizeMode = (SizeMode)EditorGUILayout.EnumPopup("尺寸模式", splitData.sizeMode);

            if (splitData.sizeMode == SizeMode.Manual)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("宽度:", GUILayout.Width(60));
                splitData.manualWidth = EditorGUILayout.IntField(Mathf.Max(1, splitData.manualWidth));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("高度:", GUILayout.Width(60));
                splitData.manualHeight = EditorGUILayout.IntField(Mathf.Max(1, splitData.manualHeight));
                EditorGUILayout.EndHorizontal();
            }

            if (EditorGUI.EndChangeCheck())
            {
                GenerateSplitPreviews();
            }

            DrawPreviewSection(splitData.originalPreviews, splitData.outputPreviews, ref splitData.scroll);

            EditorGUILayout.Space(15);
            if (GUILayout.Button("拆分纹理", GUILayout.Height(30)))
            {
                StartEditorCoroutine(SplitTextureAsync());
            }
        }

        #endregion

        #region 拆分序列图 逻辑

        private void GenerateSplitPreviews()
        {
            ClearPreviews(splitData.originalPreviews, splitData.outputPreviews);

            if (splitData.inputTexture != null)
            {
                GenerateOriginalPreview(splitData.inputTexture, splitData.originalPreviews);

                if (splitData.columns > 0 && splitData.rows > 0)
                {
                    int originalSpriteWidth = splitData.inputTexture.width / splitData.columns;
                    int originalSpriteHeight = splitData.inputTexture.height / splitData.rows;

                    int targetWidth = splitData.sizeMode == SizeMode.Auto ?
                        originalSpriteWidth : splitData.manualWidth;
                    int targetHeight = splitData.sizeMode == SizeMode.Auto ?
                        originalSpriteHeight : splitData.manualHeight;

                    Color[] allPixels = GetTexturePixels(splitData.inputTexture);
                    if (allPixels == null) return;

                    for (int y = splitData.rows - 1; y >= 0; y--)
                    {
                        for (int x = 0; x < splitData.columns; x++)
                        {
                            Color[] spritePixels = new Color[originalSpriteWidth * originalSpriteHeight];
                            int startX = x * originalSpriteWidth;
                            int startY = y * originalSpriteHeight;

                            for (int py = 0; py < originalSpriteHeight; py++)
                            {
                                int sourceIndex = (startY + py) * splitData.inputTexture.width + startX;
                                int destIndex = py * originalSpriteWidth;
                                System.Array.Copy(allPixels, sourceIndex, spritePixels, destIndex, originalSpriteWidth);
                            }

                            Texture2D finalSprite = GetFromTexturePool(originalSpriteWidth, originalSpriteHeight);
                            if (finalSprite == null)
                            {
                                finalSprite = new Texture2D(originalSpriteWidth, originalSpriteHeight, TextureFormat.ARGB32, false);
                            }

                            finalSprite.SetPixels(spritePixels);
                            finalSprite.Apply();

                            if (targetWidth != originalSpriteWidth || targetHeight != originalSpriteHeight)
                            {
                                Texture2D resized = ResizeTexture(finalSprite, targetWidth, targetHeight);
                                ReturnToTexturePool(finalSprite);
                                finalSprite = resized;
                            }

                            splitData.outputPreviews.Add(finalSprite);
                        }
                    }
                }
            }
        }

        private IEnumerator SplitTextureAsync()
        {
            if (splitData.inputTexture == null || splitData.columns <= 0 || splitData.rows <= 0)
            {
                EditorUtility.DisplayDialog("错误", "请设置有效的纹理和拆分参数", "确定");
                yield break;
            }

            progressTitle = "Split Texture...";

            string path = AssetDatabase.GetAssetPath(splitData.inputTexture);
            string directory = Path.GetDirectoryName(path);

            int originalSpriteWidth = splitData.inputTexture.width / splitData.columns;
            int originalSpriteHeight = splitData.inputTexture.height / splitData.rows;

            int targetWidth = splitData.sizeMode == SizeMode.Auto ?
                originalSpriteWidth : splitData.manualWidth;
            int targetHeight = splitData.sizeMode == SizeMode.Auto ?
                originalSpriteHeight : splitData.manualHeight;

            int totalSprites = splitData.columns * splitData.rows;
            int processed = 0;

            Color[] allPixels = GetTexturePixels(splitData.inputTexture);
            if (allPixels == null) yield break;

            for (int y = splitData.rows - 1; y >= 0; y--)
            {
                for (int x = 0; x < splitData.columns; x++)
                {
                    Color[] spritePixels = new Color[originalSpriteWidth * originalSpriteHeight];
                    int startX = x * originalSpriteWidth;
                    int startY = y * originalSpriteHeight;

                    for (int py = 0; py < originalSpriteHeight; py++)
                    {
                        int sourceIndex = (startY + py) * splitData.inputTexture.width + startX;
                        int destIndex = py * originalSpriteWidth;
                        System.Array.Copy(allPixels, sourceIndex, spritePixels, destIndex, originalSpriteWidth);
                    }

                    Texture2D finalSprite = new Texture2D(originalSpriteWidth, originalSpriteHeight, TextureFormat.ARGB32, false);
                    finalSprite.SetPixels(spritePixels);
                    finalSprite.Apply();

                    if (targetWidth != originalSpriteWidth || targetHeight != originalSpriteHeight)
                    {
                        Texture2D resized = ResizeTexture(finalSprite, targetWidth, targetHeight);
                        DestroyImmediate(finalSprite);
                        finalSprite = resized;
                    }

                    byte[] bytes = finalSprite.EncodeToPNG();
                    string outputPath = Path.Combine(directory, $"{splitData.outputName}_{y * splitData.columns + x}.png");
                    File.WriteAllBytes(outputPath, bytes);

                    DestroyImmediate(finalSprite);

                    processed++;
                    UpdateProgress($"处理中 ({processed}/{totalSprites})...", (float)processed / totalSprites);

                    if (processed % 5 == 0) yield return null;
                }
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("成功", $"纹理已拆分为 {totalSprites} 个部分", "确定");
        }

        #endregion
    }
}
