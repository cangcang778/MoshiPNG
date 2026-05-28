#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator.VfxBrowser
{
    /// <summary>
    /// 特效浏览器坐标轴 Gizmo 绘制
    /// VFX Browser axis gizmo drawing
    /// </summary>
    public static class Moshi_VfxBrowserAxis
    {
        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制坐标轴 Gizmo
        /// Draw axis gizmo
        /// </summary>
        public static void DrawAxisGizmo(Rect cellRect, Moshi_VfxBrowserContext context)
        {
            if (Event.current.type != EventType.Repaint) return;

            float camPitch = context.CamPitch;
            float camYaw = context.CamYaw;

            float gizmoSize = Mathf.Min(cellRect.width, cellRect.height) * 0.25f;
            float axisLen = gizmoSize * 0.42f;
            float labelOffset = 11f;

            Vector2 screenOffset = context.GetEffectScreenOffset(cellRect.height);
            Vector2 center = cellRect.center + screenOffset;

            Quaternion camRot = Quaternion.Euler(camPitch, camYaw, 0);
            Matrix4x4 viewMatrix = Matrix4x4.Rotate(Quaternion.Inverse(camRot));

            Vector3 xWorld = viewMatrix.MultiplyVector(Vector3.right);
            Vector3 yWorld = viewMatrix.MultiplyVector(Vector3.up);
            Vector3 zWorld = viewMatrix.MultiplyVector(Vector3.forward);

            Vector2 xScreen = new Vector2(xWorld.x, -xWorld.y) * axisLen;
            Vector2 yScreen = new Vector2(yWorld.x, -yWorld.y) * axisLen;
            Vector2 zScreen = new Vector2(zWorld.x, -zWorld.y) * axisLen;

            float xDepth = xWorld.z;
            float yDepth = yWorld.z;
            float zDepth = zWorld.z;

            var axes = new (Vector2 dir, Color color, string label, float depth)[]
            {
                (xScreen, new Color(1f, 0.2f, 0.2f), "X", xDepth),
                (yScreen, new Color(0.4f, 1f, 0.2f), "Y", yDepth),
                (zScreen, new Color(0.3f, 0.5f, 1f), "Z", zDepth),
            };
            System.Array.Sort(axes, (a, b) => a.depth.CompareTo(b.depth));

            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = Mathf.Max(9, (int)(gizmoSize * 0.22f)),
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };

            foreach (var (dir, color, label, depth) in axes)
            {
                float alpha = Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(-1f, 1f, depth));
                Color lineColor = new Color(color.r, color.g, color.b, alpha);

                Vector2 endPoint = center + dir;

                DrawGUILine(center, endPoint, lineColor, 3f);
                DrawFilledCircle(endPoint, 4f, lineColor);

                Vector2 labelDir = dir.normalized;
                Vector2 labelPos = endPoint + labelDir * labelOffset;
                Rect labelRect = new Rect(labelPos.x - 8, labelPos.y - 8, 16, 16);

                Color oldColor = GUI.color;
                GUI.color = lineColor;
                labelStyle.normal.textColor = lineColor;
                GUI.Label(labelRect, label, labelStyle);
                GUI.color = oldColor;
            }
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 绘制 GUI 空间线段
        /// Draw line in GUI space
        /// </summary>
        public static void DrawGUILine(Vector2 from, Vector2 to, Color color, float width)
        {
            if (Event.current.type != EventType.Repaint) return;

            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.01f) return;

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            Matrix4x4 savedMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, from);
            EditorGUI.DrawRect(new Rect(from.x, from.y - width * 0.5f, length, width), color);
            GUI.matrix = savedMatrix;
        }

        //──────────────────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// 近似绘制实心圆
        /// Draw filled circle approximated with rotated rects
        /// </summary>
        public static void DrawFilledCircle(Vector2 center, float radius, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;

            int steps = 9;
            float angleStep = 180f / steps;
            float diameter = radius * 2f;
            float thickness = 2f * radius * Mathf.Sin(angleStep * Mathf.Deg2Rad) * 1.15f;

            for (int i = 0; i < steps; i++)
            {
                Matrix4x4 savedMatrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(i * angleStep, center);
                EditorGUI.DrawRect(new Rect(center.x - radius, center.y - thickness * 0.5f, diameter, thickness), color);
                GUI.matrix = savedMatrix;
            }
        }
    }
}
#endif
