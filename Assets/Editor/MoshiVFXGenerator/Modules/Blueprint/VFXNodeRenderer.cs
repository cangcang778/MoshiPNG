#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator.Blueprint
{
    public static class VFXNodeRenderer
    {
        public static Rect GetNodeRect(VFXBlueprintNode node)
        {
            return new Rect(node.position.x, node.position.y, 190f, 190f);
        }

        public static void DrawNode(VFXBlueprintNode node, bool selected)
        {
            Rect rect = GetNodeRect(node);
            Color oldColor = GUI.color;
            GUI.color = selected ? new Color(0.65f, 0.85f, 1f, 1f) : new Color(0.82f, 0.82f, 0.82f, 1f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = oldColor;

            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f));
            EditorGUILayout.LabelField(node.name, EditorStyles.boldLabel);
            node.name = EditorGUILayout.TextField(node.name);
            EditorGUILayout.LabelField(node.nodeType.ToString(), EditorStyles.miniLabel);
            node.color = EditorGUILayout.ColorField("颜色", node.color);
            if (node.nodeType == VFXBlueprintNodeType.MeshLayer)
            {
                node.meshSize = EditorGUILayout.Vector2Field("尺寸", node.meshSize);
            }
            else if (node.nodeType == VFXBlueprintNodeType.TrailLayer)
            {
                node.trailTime = EditorGUILayout.FloatField("时间", node.trailTime);
                node.trailWidth = EditorGUILayout.FloatField("宽度", node.trailWidth);
            }
            else if (node.nodeType != VFXBlueprintNodeType.MaterialSlot)
            {
                node.duration = EditorGUILayout.FloatField("持续", node.duration);
                node.startLifetime = EditorGUILayout.FloatField("生命", node.startLifetime);
                node.startSpeed = EditorGUILayout.FloatField("速度", node.startSpeed);
                node.startSize = EditorGUILayout.FloatField("大小", node.startSize);
                node.maxParticles = EditorGUILayout.IntField("粒子", node.maxParticles);
                node.emissionRate = EditorGUILayout.FloatField("发射", node.emissionRate);
            }
            GUILayout.EndArea();
        }

        public static void DrawConnection(VFXBlueprintNode from, VFXBlueprintNode to, Color color)
        {
            if (from == null || to == null) return;
            Rect fromRect = GetNodeRect(from);
            Rect toRect = GetNodeRect(to);
            Vector3 start = new Vector3(fromRect.xMax, fromRect.center.y, 0f);
            Vector3 end = new Vector3(toRect.xMin, toRect.center.y, 0f);
            Vector3 startTan = start + Vector3.right * 60f;
            Vector3 endTan = end + Vector3.left * 60f;

            Handles.BeginGUI();
            Handles.DrawBezier(start, end, startTan, endTan, color, null, 3f);
            Handles.color = color;
            Handles.DrawSolidDisc(end, Vector3.forward, 4f);
            Handles.EndGUI();
        }

        public static void DrawGrid(Rect rect, float spacing, Color color)
        {
            Handles.BeginGUI();
            Color oldColor = Handles.color;
            Handles.color = color;

            for (float x = rect.x; x < rect.xMax; x += spacing)
                Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.yMax));
            for (float y = rect.y; y < rect.yMax; y += spacing)
                Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.xMax, y));

            Handles.color = oldColor;
            Handles.EndGUI();
        }
    }
}
#endif
