#if UNITY_EDITOR
using UnityEngine;

namespace MoshiVFXGenerator.Blueprint
{
    public static class VFXNodePresets
    {
        public static VFXBlueprintNode CreateCore(Vector2 position)
        {
            return Create("核心", "Core", position, new Color(1f, 0.82f, 0.28f, 1f), 0.7f, 0.35f, 0.8f, 0.35f, 60, 80f);
        }

        public static VFXBlueprintNode CreateGlow(Vector2 position)
        {
            return Create("光晕", "Glow", position, new Color(0.45f, 0.85f, 1f, 0.55f), 1.0f, 0.75f, 0.35f, 1.0f, 36, 30f);
        }

        public static VFXBlueprintNode CreateSpark(Vector2 position)
        {
            return Create("星点", "Spark", position, new Color(1f, 0.92f, 0.4f, 1f), 0.8f, 0.45f, 2.5f, 0.1f, 120, 150f);
        }

        public static VFXBlueprintNode CreateSmoke(Vector2 position)
        {
            return Create("烟雾", "Smoke", position, new Color(0.55f, 0.48f, 0.42f, 0.45f), 1.3f, 0.95f, 0.45f, 0.8f, 48, 32f);
        }

        public static VFXBlueprintNode CreateMesh(Vector2 position)
        {
            var node = Create("网格", "MeshLayer", position, new Color(0.4f, 0.85f, 1f, 0.75f), 1f, 0.5f, 0f, 1f, 8, 0f);
            node.nodeType = VFXBlueprintNodeType.MeshLayer;
            node.meshSize = new Vector2(1f, 1f);
            return node;
        }

        public static VFXBlueprintNode CreateTrail(Vector2 position)
        {
            var node = Create("拖尾", "TrailLayer", position, new Color(1f, 0.65f, 0.2f, 1f), 1f, 0.5f, 0f, 1f, 8, 0f);
            node.nodeType = VFXBlueprintNodeType.TrailLayer;
            node.trailTime = 0.7f;
            node.trailWidth = 0.12f;
            return node;
        }

        public static VFXBlueprintNode CreateMaterialSlot(Vector2 position)
        {
            var node = Create("材质槽", "MaterialSlot", position, new Color(1f, 0.85f, 0.25f, 1f), 1f, 0.5f, 0f, 1f, 8, 0f);
            node.nodeType = VFXBlueprintNodeType.MaterialSlot;
            return node;
        }

        private static VFXBlueprintNode Create(string presetName, string name, Vector2 position, Color color, float duration, float lifetime, float speed, float size, int maxParticles, float emission)
        {
            return new VFXBlueprintNode
            {
                id = System.Guid.NewGuid().ToString("N"),
                name = name,
                presetName = presetName,
                nodeType = VFXBlueprintNodeType.ParticleLayer,
                position = position,
                color = color,
                duration = duration,
                startLifetime = lifetime,
                startSpeed = speed,
                startSize = size,
                maxParticles = maxParticles,
                emissionRate = emission
            };
        }
    }
}
#endif
