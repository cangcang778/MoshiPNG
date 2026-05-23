using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace MoshiTools
{
    /// <summary>
    /// 图片处理工具 - UnMult模块 (AE UnMult插件效果)
    /// Texture Tool - UnMult module (AE UnMult plugin effect)
    /// </summary>
    public partial class Moshi_TextureTool
    {
        #region UnMult 数据

        // UnMult标签页数据 - AE UnMult插件效果
        // UnMult Tab Data - AE UnMult plugin effect
        private class UnMultTabData
        {
            public Texture2D inputTexture;
            public string outputName = "unmult_new";
            public SizeMode sizeMode = SizeMode.Auto;
            public int manualWidth = 128;
            public int manualHeight = 128;
            public List<Texture2D> originalPreviews = new List<Texture2D>();
            public List<Texture2D> outputPreviews = new List<Texture2D>();
            public Vector2 scroll;
        }

        private UnMultTabData unMultData = new UnMultTabData();

        #endregion

        #region UnMult 初始化/清理

        partial void InitUnMultModule() { }

        partial void ClearUnMultPreviews()
        {
            ClearPreviews(unMultData.originalPreviews, unMultData.outputPreviews);
        }

        #endregion

        #region UnMult 拖放

        private void HandleUnMultDragDrop(UnityEngine.Object[] draggedObjects)
        {
            foreach (UnityEngine.Object draggedObject in draggedObjects)
            {
                if (draggedObject is Texture2D texture)
                {
                    unMultData.inputTexture = texture;
                    GenerateUnMultPreviews();
                    break;
                }
            }
        }

        #endregion

        #region UnMult UI

        private void DrawUnMultTab()
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
                        unMultData.inputTexture = tex;
                        GenerateUnMultPreviews();
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
            unMultData.inputTexture = (Texture2D)EditorGUILayout.ObjectField(unMultData.inputTexture, typeof(Texture2D), false);
            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                unMultData.inputTexture = null;
                GenerateUnMultPreviews();
            }
            EditorGUILayout.EndHorizontal();

            // 显示说明
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("UnMult效果说明：\n• 取RGB最大值作为新的Alpha值\n• 反转预乘：将颜色值除以Alpha\n• 黑色像素(0,0,0)变为完全透明\n• 适用于发光特效、火焰、烟雾等素材", MessageType.Info);

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("输出设置", EditorStyles.boldLabel);

            unMultData.outputName = EditorGUILayout.TextField("输出名称", unMultData.outputName);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("输出尺寸设置", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            unMultData.sizeMode = (SizeMode)EditorGUILayout.EnumPopup("尺寸模式", unMultData.sizeMode);

            if (unMultData.sizeMode == SizeMode.Manual)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("宽度:", GUILayout.Width(60));
                unMultData.manualWidth = EditorGUILayout.IntField(Mathf.Max(1, unMultData.manualWidth));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("高度:", GUILayout.Width(60));
                unMultData.manualHeight = EditorGUILayout.IntField(Mathf.Max(1, unMultData.manualHeight));
                EditorGUILayout.EndHorizontal();
            }

            if (EditorGUI.EndChangeCheck())
            {
                GenerateUnMultPreviews();
            }

            DrawPreviewSection(unMultData.originalPreviews, unMultData.outputPreviews, ref unMultData.scroll);

            EditorGUILayout.Space(15);
            if (GUILayout.Button("执行UnMult", GUILayout.Height(30)))
            {
                ProcessUnMult();
            }
        }

        #endregion

        #region UnMult 逻辑

        private void GenerateUnMultPreviews()
        {
            ClearPreviews(unMultData.originalPreviews, unMultData.outputPreviews);

            if (unMultData.inputTexture != null)
            {
                GenerateOriginalPreview(unMultData.inputTexture, unMultData.originalPreviews);

                int width = unMultData.inputTexture.width;
                int height = unMultData.inputTexture.height;

                if (unMultData.sizeMode == SizeMode.Manual)
                {
                    width = unMultData.manualWidth;
                    height = unMultData.manualHeight;
                }

                Texture2D processedTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);

                Texture2D source = unMultData.inputTexture;
                if (width != source.width || height != source.height)
                {
                    source = ResizeTexturePoint(source, width, height);
                }

                Color[] sourcePixels = GetTexturePixels(source);
                if (sourcePixels == null) return;

                Color[] processedPixels = new Color[sourcePixels.Length];

                // UnMult算法：取RGB最大值作为Alpha，然后反转预乘
                // UnMult algorithm: Take RGB max as Alpha, then unpremultiply
                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    Color pixel = sourcePixels[i];

                    float maxChannel = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
                    float newAlpha = maxChannel;

                    float r = (newAlpha > 0.001f) ? pixel.r / newAlpha : 0f;
                    float g = (newAlpha > 0.001f) ? pixel.g / newAlpha : 0f;
                    float b = (newAlpha > 0.001f) ? pixel.b / newAlpha : 0f;

                    processedPixels[i] = new Color(r, g, b, newAlpha);
                }

                processedTexture.SetPixels(processedPixels);
                processedTexture.Apply();
                unMultData.outputPreviews.Add(processedTexture);

                if (source != unMultData.inputTexture)
                {
                    DestroyImmediate(source);
                }
            }
        }

        private void ProcessUnMult()
        {
            if (unMultData.inputTexture == null)
            {
                EditorUtility.DisplayDialog("错误", "请选择纹理", "确定");
                return;
            }

            string path = AssetDatabase.GetAssetPath(unMultData.inputTexture);
            string directory = Path.GetDirectoryName(path);

            int width = unMultData.inputTexture.width;
            int height = unMultData.inputTexture.height;

            if (unMultData.sizeMode == SizeMode.Manual)
            {
                width = unMultData.manualWidth;
                height = unMultData.manualHeight;
            }

            Texture2D processedTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);

            Texture2D source = unMultData.inputTexture;
            if (width != source.width || height != source.height)
            {
                source = ResizeTexturePoint(source, width, height);
            }

            Color[] sourcePixels = GetTexturePixels(source);
            if (sourcePixels == null) return;

            Color[] processedPixels = new Color[sourcePixels.Length];

            for (int i = 0; i < sourcePixels.Length; i++)
            {
                Color pixel = sourcePixels[i];

                float maxChannel = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
                float newAlpha = maxChannel;

                float r = (newAlpha > 0.001f) ? pixel.r / newAlpha : 0f;
                float g = (newAlpha > 0.001f) ? pixel.g / newAlpha : 0f;
                float b = (newAlpha > 0.001f) ? pixel.b / newAlpha : 0f;

                processedPixels[i] = new Color(r, g, b, newAlpha);
            }

            processedTexture.SetPixels(processedPixels);
            processedTexture.Apply();

            byte[] bytes = processedTexture.EncodeToPNG();
            string outputPath = Path.Combine(directory, $"{unMultData.outputName}.png");
            File.WriteAllBytes(outputPath, bytes);

            DestroyImmediate(processedTexture);

            if (source != unMultData.inputTexture)
            {
                DestroyImmediate(source);
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("成功", "UnMult处理完成", "确定");
        }

        #endregion
    }
}
