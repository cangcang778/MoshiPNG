#if UNITY_EDITOR
using System.Collections.Generic;
using MoshiVFXGenerator.Factory;
using UnityEngine;

namespace MoshiVFXGenerator.Blueprint
{
    public static class VFXBlueprintGenerator
    {
        public static VFXCloneResult Generate(VFXBlueprint blueprint, VFXFactoryConfig config)
        {
            if (blueprint == null)
                return new VFXCloneResult { success = false, message = "蓝图为空" };
            if (config == null)
                config = new VFXFactoryConfig();

            var recipe = new VFXRecipe
            {
                recipeName = blueprint.blueprintName,
                description = "由蓝图转换生成"
            };

            Dictionary<string, VFXBlueprintNode> nodeMap = BuildNodeMap(blueprint);
            Dictionary<string, string> parentMap = BuildConnectionMap(blueprint, nodeMap, VFXBlueprintConnectionType.Parent);
            Dictionary<string, string> subEmitterMap = BuildConnectionMap(blueprint, nodeMap, VFXBlueprintConnectionType.SubEmitter);
            Dictionary<string, string> materialMap = BuildConnectionMap(blueprint, nodeMap, VFXBlueprintConnectionType.Material);
            Dictionary<string, string> nameMap = BuildNameMap(blueprint);

            if (blueprint.nodes == null)
                return new VFXCloneResult { success = false, message = "蓝图节点为空" };

            foreach (VFXBlueprintNode node in blueprint.nodes)
            {
                if (node == null || node.nodeType == VFXBlueprintNodeType.Root) continue;
                string parentName = ResolveSourceName(parentMap, nameMap, node.id);
                string subEmitterParentName = ResolveSourceName(subEmitterMap, nameMap, node.id);
                string materialSourceName = ResolveSourceName(materialMap, nameMap, node.id);

                recipe.layers.Add(new VFXRecipeLayer
                {
                    name = ResolveNodeName(nameMap, node),
                    parentName = parentName,
                    subEmitterParentName = subEmitterParentName,
                    materialSourceName = materialSourceName,
                    kind = ConvertNodeType(node.nodeType),
                    color = node.color,
                    duration = node.duration,
                    startLifetime = node.startLifetime,
                    startSpeed = node.startSpeed,
                    startSize = node.startSize,
                    maxParticles = node.maxParticles,
                    emissionRate = node.emissionRate,
                    sortingOrder = node.sortingOrder,
                    renderQueue = node.renderQueue,
                    subEmitterType = node.subEmitterType,
                    looping = node.looping,
                    startDelay = node.startDelay,
                    shapeType = node.shapeType,
                    shapeRadius = node.shapeRadius,
                    useColorOverLifetime = node.useColorOverLifetime,
                    colorOverLifetime = node.colorOverLifetime,
                    useSizeOverLifetime = node.useSizeOverLifetime,
                    sizeOverLifetime = node.sizeOverLifetime,
                    sizeOverLifetimeMultiplier = node.sizeOverLifetimeMultiplier,
                    useTextureSheetAnimation = node.useTextureSheetAnimation,
                    textureSheetTilesX = node.textureSheetTilesX,
                    textureSheetTilesY = node.textureSheetTilesY,
                    textureSheetFrameOverTime = node.textureSheetFrameOverTime,
                    meshSize = node.meshSize,
                    trailTime = node.trailTime,
                    trailWidth = node.trailWidth,
                    localPosition = Vector3.zero
                });
            }

            if (recipe.layers.Count == 0)
                return new VFXCloneResult { success = false, message = "蓝图中没有可生成的层" };

            return VFXAssembler.GeneratePrefab(recipe, config);
        }

        private static VFXRecipeLayerKind ConvertNodeType(VFXBlueprintNodeType nodeType)
        {
            switch (nodeType)
            {
                case VFXBlueprintNodeType.MeshLayer:
                    return VFXRecipeLayerKind.Mesh;
                case VFXBlueprintNodeType.TrailLayer:
                    return VFXRecipeLayerKind.Trail;
                case VFXBlueprintNodeType.MaterialSlot:
                    return VFXRecipeLayerKind.MaterialSlot;
                case VFXBlueprintNodeType.ParticleLayer:
                case VFXBlueprintNodeType.Root:
                default:
                    return VFXRecipeLayerKind.Particle;
            }
        }

        private static Dictionary<string, VFXBlueprintNode> BuildNodeMap(VFXBlueprint blueprint)
        {
            var map = new Dictionary<string, VFXBlueprintNode>();
            if (blueprint.nodes == null) return map;
            foreach (VFXBlueprintNode node in blueprint.nodes)
            {
                if (node != null && !string.IsNullOrEmpty(node.id) && !map.ContainsKey(node.id))
                    map.Add(node.id, node);
            }
            return map;
        }

        private static Dictionary<string, string> BuildConnectionMap(VFXBlueprint blueprint, Dictionary<string, VFXBlueprintNode> nodeMap, VFXBlueprintConnectionType connectionType)
        {
            var map = new Dictionary<string, string>();
            if (blueprint.connections == null) return map;
            foreach (VFXBlueprintConnection connection in blueprint.connections)
            {
                if (connection == null || connection.connectionType != connectionType) continue;
                if (string.IsNullOrEmpty(connection.fromNodeId) || string.IsNullOrEmpty(connection.toNodeId)) continue;
                if (!nodeMap.ContainsKey(connection.fromNodeId) || !nodeMap.ContainsKey(connection.toNodeId)) continue;
                if (connection.fromNodeId == connection.toNodeId) continue;
                if (connectionType != VFXBlueprintConnectionType.SubEmitter && map.ContainsKey(connection.toNodeId)) continue;
                map[connection.toNodeId] = connection.fromNodeId;
            }
            return map;
        }

        private static Dictionary<string, string> BuildNameMap(VFXBlueprint blueprint)
        {
            var map = new Dictionary<string, string>();
            var usedNames = new HashSet<string>();
            if (blueprint.nodes == null) return map;
            foreach (VFXBlueprintNode node in blueprint.nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.id)) continue;
                string baseName = string.IsNullOrEmpty(node.name) ? node.nodeType.ToString() : node.name;
                string uniqueName = baseName;
                int suffix = 1;
                while (usedNames.Contains(uniqueName))
                    uniqueName = baseName + "_" + suffix++;
                usedNames.Add(uniqueName);
                map[node.id] = uniqueName;
            }
            return map;
        }

        private static string ResolveNodeName(Dictionary<string, string> nameMap, VFXBlueprintNode node)
        {
            if (node != null && !string.IsNullOrEmpty(node.id) && nameMap.TryGetValue(node.id, out string nodeName))
                return nodeName;
            return node != null && !string.IsNullOrEmpty(node.name) ? node.name : "Layer";
        }

        private static string ResolveSourceName(Dictionary<string, string> map, Dictionary<string, string> nameMap, string targetId)
        {
            if (map.TryGetValue(targetId, out string sourceId) && nameMap.TryGetValue(sourceId, out string sourceName))
                return sourceName;
            return string.Empty;
        }
    }
}
#endif

