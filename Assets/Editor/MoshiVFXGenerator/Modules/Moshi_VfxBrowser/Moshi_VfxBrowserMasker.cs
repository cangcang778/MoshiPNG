#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator.VfxBrowser
{
    /// <summary>
    /// 特效浏览器边框 / 圆角遮罩绘制
    /// VFX Browser border / corner mask drawing
    /// </summary>
    public static class Moshi_VfxBrowserMasker
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制内边框
        /// Draw inner border
        /// </summary>
        public static void DrawInnerBorder(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), color);
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        // 圆角遮罩纹理（静态缓存，所有格子共用）
        // Rounded corner mask textures (static cache, shared by all cells)
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        private static Texture2D _CornerMaskTL;
        private static Texture2D _CornerMaskTR;
        private static Texture2D _CornerMaskBL;
        private static Texture2D _CornerMaskBR;
        private const int CORNER_MASK_SIZE = 32;

        /// <summary>
        /// 确保圆角遮罩纹理已创建（懒加载）
        /// Ensure corner mask textures are created (lazy-loaded)
        /// </summary>
        public static void EnsureCornerMasks()
        {
            if (_CornerMaskTL != null) return;

            _CornerMaskTL = CreateCornerMask(false, false);
            _CornerMaskTR = CreateCornerMask(true, false);
            _CornerMaskBL = CreateCornerMask(false, true);
            _CornerMaskBR = CreateCornerMask(true, true);
        }

        /// <summary>
        /// 创建单个圆角遮罩纹理
        /// Create a single corner mask texture
        /// </summary>
        public static Texture2D CreateCornerMask(bool flipX, bool flipY)
        {
            int size = CORNER_MASK_SIZE;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float cx = flipX ? 0f : 1f;
            float cy = flipY ? 0f : 1f;

            var pixels = new Color32[size * size];
            for (int ty = 0; ty < size; ty++)
            {
                for (int tx = 0; tx < size; tx++)
                {
                    float u = (tx + 0.5f) / size;
                    float v = 1f - (ty + 0.5f) / size;

                    float du = u - cx;
                    float dv = v - cy;
                    float dist = Mathf.Sqrt(du * du + dv * dv);

                    float edgeSoftness = 1.5f / size;
                    float alpha = Mathf.Clamp01((dist - 1f + edgeSoftness) / (2f * edgeSoftness));

                    pixels[ty * size + tx] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>
        /// 清理圆角遮罩纹理
        /// Cleanup corner mask textures
        /// </summary>
        public static void CleanupCornerMasks()
        {
            if (_CornerMaskTL != null) { Object.DestroyImmediate(_CornerMaskTL); _CornerMaskTL = null; }
            if (_CornerMaskTR != null) { Object.DestroyImmediate(_CornerMaskTR); _CornerMaskTR = null; }
            if (_CornerMaskBL != null) { Object.DestroyImmediate(_CornerMaskBL); _CornerMaskBL = null; }
            if (_CornerMaskBR != null) { Object.DestroyImmediate(_CornerMaskBR); _CornerMaskBR = null; }
        }

        /// <summary>
        /// 绘制圆角遮罩
        /// Draw rounded corner masks
        /// </summary>
        public static void DrawRoundedCornerMask(Rect rect, float radius)
        {
            if (radius < 1f) return;
            EnsureCornerMasks();

            Color maskColor = EditorGUIUtility.isProSkin
                ? new Color(56f / 255f, 56f / 255f, 56f / 255f, 1f)
                : new Color(194f / 255f, 194f / 255f, 194f / 255f, 1f);

            Color oldColor = GUI.color;
            GUI.color = maskColor;

            float x0 = Mathf.Floor(rect.x);
            float y0 = Mathf.Floor(rect.y);
            float x1 = Mathf.Ceil(rect.xMax);
            float y1 = Mathf.Ceil(rect.yMax);
            float r = Mathf.Ceil(radius);

            GUI.DrawTexture(new Rect(x0, y0, r, r), _CornerMaskTL);
            GUI.DrawTexture(new Rect(x1 - r, y0, r, r), _CornerMaskTR);
            GUI.DrawTexture(new Rect(x0, y1 - r, r, r), _CornerMaskBL);
            GUI.DrawTexture(new Rect(x1 - r, y1 - r, r, r), _CornerMaskBR);

            GUI.color = oldColor;
        }
    }
}
#endif
