#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using MoshiVFXGenerator.Factory;
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator.Blueprint
{
    public class Moshi_VFXBlueprintWindow : EditorWindow
    {
        private const string TOOL_NAME = "特效蓝图";
        private const string BLUEPRINT_PRESET_DIR = "Assets/Editor/MoshiVFXGenerator/Presets/Blueprints";

        private VFXBlueprint blueprint = new VFXBlueprint();
        private VFXFactoryConfig config = new VFXFactoryConfig();
        private VFXCloneResult lastResult;
        private Vector2 canvasOffset;
        private int selectedIndex = -1;
        private int draggingIndex = -1;
        private int connectionStartIndex = -1;
        private VFXBlueprintConnectionType currentConnectionType = VFXBlueprintConnectionType.Parent;
        private HashSet<int> selectedIndices = new HashSet<int>();
        private Vector2 dragOffset;
        private Vector2 lastMousePosition;

        [MenuItem("工具/Moshi特效生成器/特效蓝图")]
        public static void ShowWindow()
        {
            var window = GetWindow<Moshi_VFXBlueprintWindow>(TOOL_NAME);
            window.minSize = new Vector2(760, 520);
        }

        private void OnEnable()
        {
            if (blueprint.nodes.Count == 0)
                AddNode(new Vector2(80f, 100f));
            config.outputRoot = Moshi_VFXGenSettings.Instance.defaultOutputRoot;
            config.effectName = blueprint.blueprintName;
        }

        private void OnGUI()
        {
            DrawToolbar();
            Rect canvasRect = new Rect(0f, 36f, position.width, position.height - 36f);
            DrawCanvas(canvasRect);
            HandleCanvasEvents(canvasRect);
            if (GUI.changed) Repaint();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            blueprint.blueprintName = GUILayout.TextField(blueprint.blueprintName, EditorStyles.toolbarTextField, GUILayout.Width(160));
            config.effectName = GUILayout.TextField(string.IsNullOrEmpty(config.effectName) ? blueprint.blueprintName : config.effectName, EditorStyles.toolbarTextField, GUILayout.Width(160));
            if (GUILayout.Button("添加节点", EditorStyles.toolbarButton, GUILayout.Width(70)))
                AddNode(VFXNodePresets.CreateCore(new Vector2(80f + blueprint.nodes.Count * 30f, 100f + blueprint.nodes.Count * 24f)));
            if (GUILayout.Button("预设", EditorStyles.toolbarButton, GUILayout.Width(55)))
                ShowPresetMenu(new Vector2(80f + blueprint.nodes.Count * 30f, 100f + blueprint.nodes.Count * 24f));
            DrawConnectionTypeToolbar();
            if (GUILayout.Button("复制节点", EditorStyles.toolbarButton, GUILayout.Width(70)))
                DuplicateSelectedNode();
            if (GUILayout.Button("删除节点", EditorStyles.toolbarButton, GUILayout.Width(70)))
                DeleteSelectedNode();
            if (GUILayout.Button("自动排布", EditorStyles.toolbarButton, GUILayout.Width(70)))
                AutoLayout();
            if (GUILayout.Button("清除连线", EditorStyles.toolbarButton, GUILayout.Width(70)))
                blueprint.connections.Clear();
            if (GUILayout.Button("生成", EditorStyles.toolbarButton, GUILayout.Width(60)))
                Generate();
            if (GUILayout.Button("保存JSON", EditorStyles.toolbarButton, GUILayout.Width(75)))
                SaveJson();
            if (GUILayout.Button("加载JSON", EditorStyles.toolbarButton, GUILayout.Width(75)))
                LoadJson();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConnectionTypeToolbar()
        {
            GUILayout.Label("连线", EditorStyles.miniLabel, GUILayout.Width(30));
            if (GUILayout.Toggle(currentConnectionType == VFXBlueprintConnectionType.Parent, "父子", EditorStyles.toolbarButton, GUILayout.Width(42)))
                currentConnectionType = VFXBlueprintConnectionType.Parent;
            if (GUILayout.Toggle(currentConnectionType == VFXBlueprintConnectionType.SubEmitter, "子发", EditorStyles.toolbarButton, GUILayout.Width(42)))
                currentConnectionType = VFXBlueprintConnectionType.SubEmitter;
            if (GUILayout.Toggle(currentConnectionType == VFXBlueprintConnectionType.Material, "材质", EditorStyles.toolbarButton, GUILayout.Width(42)))
                currentConnectionType = VFXBlueprintConnectionType.Material;
        }

        private void DrawCanvas(Rect canvasRect)
        {
            GUI.Box(canvasRect, GUIContent.none);
            VFXNodeRenderer.DrawGrid(canvasRect, 24f, new Color(0f, 0f, 0f, 0.12f));
            VFXNodeRenderer.DrawGrid(canvasRect, 96f, new Color(0f, 0f, 0f, 0.22f));
            DrawConnections();

            BeginWindows();
            for (int i = 0; i < blueprint.nodes.Count; i++)
                VFXNodeRenderer.DrawNode(blueprint.nodes[i], selectedIndices.Contains(i));
            EndWindows();

            if (lastResult != null)
            {
                Rect resultRect = new Rect(canvasRect.xMax - 270f, canvasRect.y + 12f, 250f, 92f);
                GUILayout.BeginArea(resultRect, EditorStyles.helpBox);
                EditorGUILayout.LabelField(lastResult.success ? "生成成功" : "生成失败", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(lastResult.message, EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(lastResult.prefabPath) && GUILayout.Button("定位Prefab"))
                {
                    Object obj = AssetDatabase.LoadAssetAtPath<Object>(lastResult.prefabPath);
                    if (obj != null) EditorGUIUtility.PingObject(obj);
                }
                GUILayout.EndArea();
            }
        }

        private void HandleCanvasEvents(Rect canvasRect)
        {
            Event e = Event.current;
            if (!canvasRect.Contains(e.mousePosition)) return;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                selectedIndex = HitTest(e.mousePosition);
                if (e.alt && selectedIndex >= 0)
                {
                    HandleAltConnection(selectedIndex);
                    GUI.changed = true;
                    e.Use();
                    return;
                }

                UpdateSelection(selectedIndex, e.shift || e.control);
                draggingIndex = selectedIndex;
                lastMousePosition = e.mousePosition;
                if (draggingIndex >= 0)
                    dragOffset = e.mousePosition - blueprint.nodes[draggingIndex].position;
                GUI.changed = true;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0 && draggingIndex >= 0)
            {
                if (selectedIndices.Count > 1 && selectedIndices.Contains(draggingIndex))
                {
                    Vector2 delta = e.mousePosition - lastMousePosition;
                    foreach (int index in selectedIndices)
                    {
                        if (index >= 0 && index < blueprint.nodes.Count)
                            blueprint.nodes[index].position += delta;
                    }
                    lastMousePosition = e.mousePosition;
                }
                else
                {
                    blueprint.nodes[draggingIndex].position = e.mousePosition - dragOffset;
                }
                GUI.changed = true;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                draggingIndex = -1;
            }
            else if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Delete)
                {
                    DeleteSelectedNode();
                    GUI.changed = true;
                    e.Use();
                }
                else if (e.control && e.keyCode == KeyCode.D)
                {
                    DuplicateSelectedNode();
                    GUI.changed = true;
                    e.Use();
                }
            }
            else if (e.type == EventType.ContextClick)
            {
                GenericMenu menu = new GenericMenu();
                Vector2 pos = e.mousePosition;
                menu.AddItem(new GUIContent("添加/核心"), false, () => AddNode(VFXNodePresets.CreateCore(pos)));
                menu.AddItem(new GUIContent("添加/光晕"), false, () => AddNode(VFXNodePresets.CreateGlow(pos)));
                menu.AddItem(new GUIContent("添加/星点"), false, () => AddNode(VFXNodePresets.CreateSpark(pos)));
                menu.AddItem(new GUIContent("添加/烟雾"), false, () => AddNode(VFXNodePresets.CreateSmoke(pos)));
                menu.AddItem(new GUIContent("添加/网格"), false, () => AddNode(VFXNodePresets.CreateMesh(pos)));
                menu.AddItem(new GUIContent("添加/拖尾"), false, () => AddNode(VFXNodePresets.CreateTrail(pos)));
                menu.AddItem(new GUIContent("添加/材质槽"), false, () => AddNode(VFXNodePresets.CreateMaterialSlot(pos)));
                if (selectedIndex >= 0)
                {
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("设为连线起点"), false, () => connectionStartIndex = selectedIndex);
                    menu.AddItem(new GUIContent("复制选中节点"), false, DuplicateSelectedNode);
                    menu.AddItem(new GUIContent("删除选中节点"), false, DeleteSelectedNode);
                }
                menu.ShowAsContext();
                e.Use();
            }
        }

        private void UpdateSelection(int index, bool additive)
        {
            if (!additive) selectedIndices.Clear();
            if (index < 0)
            {
                selectedIndex = -1;
                return;
            }

            if (additive && selectedIndices.Contains(index))
                selectedIndices.Remove(index);
            else
                selectedIndices.Add(index);
            selectedIndex = index;
        }

        private int HitTest(Vector2 mousePosition)
        {
            for (int i = blueprint.nodes.Count - 1; i >= 0; i--)
            {
                if (VFXNodeRenderer.GetNodeRect(blueprint.nodes[i]).Contains(mousePosition))
                    return i;
            }
            return -1;
        }

        private void AddNode(Vector2 position)
        {
            AddNode(VFXNodePresets.CreateCore(position));
        }

        private void AddNode(VFXBlueprintNode node)
        {
            if (node == null) return;
            if (string.IsNullOrEmpty(node.id))
                node.id = System.Guid.NewGuid().ToString("N");
            if (string.IsNullOrEmpty(node.name))
                node.name = "ParticleLayer" + (blueprint.nodes.Count + 1);
            blueprint.nodes.Add(node);
            selectedIndex = blueprint.nodes.Count - 1;
            selectedIndices.Clear();
            selectedIndices.Add(selectedIndex);
        }

        private void DeleteSelectedNode()
        {
            if (selectedIndices.Count == 0 && selectedIndex >= 0)
                selectedIndices.Add(selectedIndex);
            if (selectedIndices.Count == 0) return;

            var indices = new List<int>(selectedIndices);
            indices.Sort((a, b) => b.CompareTo(a));
            foreach (int index in indices)
            {
                if (index < 0 || index >= blueprint.nodes.Count) continue;
                string id = blueprint.nodes[index].id;
                blueprint.nodes.RemoveAt(index);
                blueprint.connections.RemoveAll(c => c.fromNodeId == id || c.toNodeId == id);
            }
            selectedIndices.Clear();
            selectedIndex = -1;
            connectionStartIndex = -1;
        }

        private void DuplicateSelectedNode()
        {
            if (selectedIndices.Count == 0 && selectedIndex >= 0)
                selectedIndices.Add(selectedIndex);
            if (selectedIndices.Count == 0) return;

            var newSelection = new HashSet<int>();
            foreach (int index in selectedIndices)
            {
                if (index < 0 || index >= blueprint.nodes.Count) continue;
                AddNode(CloneNode(blueprint.nodes[index]));
                newSelection.Add(blueprint.nodes.Count - 1);
            }
            selectedIndices = newSelection;
            selectedIndex = blueprint.nodes.Count > 0 ? blueprint.nodes.Count - 1 : -1;
        }

        private VFXBlueprintNode CloneNode(VFXBlueprintNode source)
        {
            return new VFXBlueprintNode
            {
                id = System.Guid.NewGuid().ToString("N"),
                name = source.name + "_Copy",
                nodeType = source.nodeType,
                position = source.position + new Vector2(32f, 32f),
                presetName = source.presetName,
                color = source.color,
                duration = source.duration,
                startLifetime = source.startLifetime,
                startSpeed = source.startSpeed,
                startSize = source.startSize,
                maxParticles = source.maxParticles,
                emissionRate = source.emissionRate,
                sortingOrder = source.sortingOrder,
                renderQueue = source.renderQueue,
                subEmitterType = source.subEmitterType,
                looping = source.looping,
                startDelay = source.startDelay,
                shapeType = source.shapeType,
                shapeRadius = source.shapeRadius,
                useColorOverLifetime = source.useColorOverLifetime,
                colorOverLifetime = source.colorOverLifetime,
                useSizeOverLifetime = source.useSizeOverLifetime,
                sizeOverLifetime = source.sizeOverLifetime,
                sizeOverLifetimeMultiplier = source.sizeOverLifetimeMultiplier,
                useTextureSheetAnimation = source.useTextureSheetAnimation,
                textureSheetTilesX = source.textureSheetTilesX,
                textureSheetTilesY = source.textureSheetTilesY,
                textureSheetFrameOverTime = source.textureSheetFrameOverTime,
                meshSize = source.meshSize,
                trailTime = source.trailTime,
                trailWidth = source.trailWidth
            };
        }

        private void AutoLayout()
        {
            int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 80f) / 230f));
            for (int i = 0; i < blueprint.nodes.Count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                blueprint.nodes[i].position = new Vector2(40f + col * 230f, 80f + row * 220f);
            }
        }

        private void HandleAltConnection(int index)
        {
            if (connectionStartIndex < 0)
            {
                connectionStartIndex = index;
                return;
            }

            if (connectionStartIndex != index)
                AddConnection(connectionStartIndex, index, currentConnectionType);
            connectionStartIndex = -1;
        }

        private void AddConnection(int fromIndex, int toIndex, VFXBlueprintConnectionType connectionType)
        {
            if (fromIndex < 0 || toIndex < 0 || fromIndex >= blueprint.nodes.Count || toIndex >= blueprint.nodes.Count) return;
            string fromId = blueprint.nodes[fromIndex].id;
            string toId = blueprint.nodes[toIndex].id;
            if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId) || fromId == toId) return;
            if (connectionType == VFXBlueprintConnectionType.Parent && WouldCreateCycle(fromId, toId)) return;

            if (connectionType == VFXBlueprintConnectionType.Parent)
                blueprint.connections.RemoveAll(c => c.toNodeId == toId && c.connectionType == VFXBlueprintConnectionType.Parent);
            blueprint.connections.Add(new VFXBlueprintConnection { fromNodeId = fromId, toNodeId = toId, connectionType = connectionType });
        }

        private bool WouldCreateCycle(string fromId, string toId)
        {
            string current = fromId;
            int guard = 0;
            while (!string.IsNullOrEmpty(current) && guard++ < 128)
            {
                if (current == toId) return true;
                VFXBlueprintConnection parent = blueprint.connections.Find(c => c.toNodeId == current);
                current = parent != null ? parent.fromNodeId : null;
            }
            return false;
        }

        private void DrawConnections()
        {
            foreach (VFXBlueprintConnection connection in blueprint.connections)
            {
                VFXBlueprintNode from = FindNode(connection.fromNodeId);
                VFXBlueprintNode to = FindNode(connection.toNodeId);
                VFXNodeRenderer.DrawConnection(from, to, GetConnectionColor(connection.connectionType));
            }
        }

        private Color GetConnectionColor(VFXBlueprintConnectionType connectionType)
        {
            switch (connectionType)
            {
                case VFXBlueprintConnectionType.SubEmitter:
                    return new Color(1f, 0.45f, 0.85f, 0.95f);
                case VFXBlueprintConnectionType.Material:
                    return new Color(1f, 0.85f, 0.25f, 0.95f);
                case VFXBlueprintConnectionType.Parent:
                default:
                    return new Color(0.25f, 0.65f, 1f, 0.95f);
            }
        }

        private VFXBlueprintNode FindNode(string id)
        {
            return blueprint.nodes.Find(n => n.id == id);
        }

        private void ShowPresetMenu(Vector2 position)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("核心"), false, () => AddNode(VFXNodePresets.CreateCore(position)));
            menu.AddItem(new GUIContent("光晕"), false, () => AddNode(VFXNodePresets.CreateGlow(position)));
            menu.AddItem(new GUIContent("星点"), false, () => AddNode(VFXNodePresets.CreateSpark(position)));
            menu.AddItem(new GUIContent("烟雾"), false, () => AddNode(VFXNodePresets.CreateSmoke(position)));
            menu.AddItem(new GUIContent("网格"), false, () => AddNode(VFXNodePresets.CreateMesh(position)));
            menu.AddItem(new GUIContent("拖尾"), false, () => AddNode(VFXNodePresets.CreateTrail(position)));
            menu.AddItem(new GUIContent("材质槽"), false, () => AddNode(VFXNodePresets.CreateMaterialSlot(position)));
            menu.ShowAsContext();
        }

        private void Generate()
        {
            config.effectName = string.IsNullOrEmpty(config.effectName) ? blueprint.blueprintName : config.effectName;
            config.outputRoot = string.IsNullOrEmpty(config.outputRoot) ? Moshi_VFXGenSettings.Instance.defaultOutputRoot : config.outputRoot;
            lastResult = VFXBlueprintGenerator.Generate(blueprint, config);
        }

        private void SaveJson()
        {
            Moshi_VFXPrefabUtil.EnsureFolder(BLUEPRINT_PRESET_DIR);
            string path = EditorUtility.SaveFilePanel("保存蓝图", GetAbsoluteAssetPath(BLUEPRINT_PRESET_DIR), blueprint.blueprintName, "json");
            if (!string.IsNullOrEmpty(path))
            {
                VFXBlueprintSerializer.Save(path, blueprint);
                AssetDatabase.Refresh();
            }
        }

        private void LoadJson()
        {
            Moshi_VFXPrefabUtil.EnsureFolder(BLUEPRINT_PRESET_DIR);
            string path = EditorUtility.OpenFilePanel("加载蓝图", GetAbsoluteAssetPath(BLUEPRINT_PRESET_DIR), "json");
            VFXBlueprint loaded = VFXBlueprintSerializer.Load(path);
            if (loaded != null)
            {
                blueprint = loaded;
                config.effectName = blueprint.blueprintName;
                selectedIndex = -1;
            }
        }

        private string GetAbsoluteAssetPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }
    }
}
#endif
