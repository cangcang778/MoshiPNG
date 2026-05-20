using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ParticleMeshTools
{
    /// <summary>
    /// Mesh替换对话框窗口
    /// </summary>
    public class MeshReplacementWindow : EditorWindow
    {
        private Mesh originalMesh;
        private Mesh newMesh;
        private List<MeshPreviewItem> targetItems;
        private Moshi_ParticleMesh parentWindow;
        private bool preserveProperties = true;
        
        public void Initialize(Mesh original, List<MeshPreviewItem> items, Moshi_ParticleMesh parent)
        {
            originalMesh = original;
            targetItems = items;
            parentWindow = parent;
            newMesh = original;
            
            titleContent = new GUIContent("批量替换Mesh");
            minSize = new Vector2(400, 200);
            maxSize = new Vector2(400, 200);
        }
        
        private void OnGUI()
        {
            if (originalMesh == null || targetItems == null)
            {
                Close();
                return;
            }
            
            EditorGUILayout.LabelField("批量替换Mesh", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // 原始Mesh
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("原始Mesh:", GUILayout.Width(80));
            EditorGUILayout.ObjectField(originalMesh, typeof(Mesh), false, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();
            
            // 新Mesh
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("新Mesh:", GUILayout.Width(80));
            newMesh = EditorGUILayout.ObjectField(newMesh, typeof(Mesh), false, GUILayout.Width(200)) as Mesh;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 选项
            preserveProperties = EditorGUILayout.Toggle("保持原有属性", preserveProperties);
            
            EditorGUILayout.Space();
            
            // 影响范围
            EditorGUILayout.LabelField($"将影响 {targetItems.Count} 个组件:");
            EditorGUI.indentLevel++;
            foreach (var item in targetItems)
            {
                EditorGUILayout.LabelField($"• {item.gameObject.name} ({item.GetTypeDisplayName()})");
            }
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space();
            
            // 按钮
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("取消"))
            {
                Close();
            }
            
            GUI.enabled = newMesh != null && newMesh != originalMesh;
            if (GUILayout.Button("确定替换"))
            {
                PerformReplacement();
                Close();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void PerformReplacement()
        {
            if (newMesh == null || targetItems == null) return;
            
            int successCount = MeshPreviewAnalyzer.BatchReplaceMesh(targetItems, originalMesh, newMesh, preserveProperties);
            
            EditorUtility.DisplayDialog("替换完成", 
                $"成功替换了 {successCount}/{targetItems.Count} 个组件的Mesh", "确定");
            
            // 刷新父窗口
            if (parentWindow != null)
            {
                parentWindow.RefreshAll();
            }
        }
    }
}