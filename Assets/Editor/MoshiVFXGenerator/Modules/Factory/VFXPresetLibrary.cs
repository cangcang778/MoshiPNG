#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace MoshiVFXGenerator.Factory
{
    public static class VFXPresetLibrary
    {
        public static List<VFXRecipe> GetDefaultRecipes()
        {
            return new List<VFXRecipe>
            {
                CreateBasicFlash(),
                CreateRingSpread(),
                CreateSmallExplosion(),
                CreateTrailGlow(),
                CreateCardShine()
            };
        }

        private static VFXRecipe CreateBasicFlash()
        {
            return new VFXRecipe
            {
                recipeName = "基础闪光",
                description = "核心亮点 + 光晕 + 星点",
                layers = new List<VFXRecipeLayer>
                {
                    new VFXRecipeLayer { name = "Core", duration = 0.5f, startLifetime = 0.25f, startSpeed = 0.2f, startSize = 0.55f, maxParticles = 18, emissionRate = 80f, shapeType = ParticleSystemShapeType.Sphere, shapeRadius = 0.05f, color = new Color(1f, 0.9f, 0.45f, 1f) },
                    new VFXRecipeLayer { name = "Glow", duration = 0.8f, startLifetime = 0.55f, startSpeed = 0.4f, startSize = 1.2f, maxParticles = 28, emissionRate = 30f, shapeType = ParticleSystemShapeType.Circle, shapeRadius = 0.25f, color = new Color(1f, 0.65f, 0.2f, 0.55f) },
                    new VFXRecipeLayer { name = "Spark", duration = 0.7f, startLifetime = 0.45f, startSpeed = 2.3f, startSize = 0.12f, maxParticles = 80, emissionRate = 120f, shapeType = ParticleSystemShapeType.Sphere, shapeRadius = 0.12f, color = new Color(1f, 0.8f, 0.25f, 1f) }
                }
            };
        }

        private static VFXRecipe CreateRingSpread()
        {
            return new VFXRecipe
            {
                recipeName = "圆环扩散",
                description = "圆形发射 + 外扩光点",
                layers = new List<VFXRecipeLayer>
                {
                    new VFXRecipeLayer { name = "Ring", duration = 1.1f, startLifetime = 0.8f, startSpeed = 1.4f, startSize = 0.28f, maxParticles = 96, emissionRate = 90f, shapeType = ParticleSystemShapeType.Circle, shapeRadius = 0.35f, color = new Color(0.35f, 0.75f, 1f, 0.9f) },
                    new VFXRecipeLayer { name = "OuterSpark", duration = 1.0f, startLifetime = 0.55f, startSpeed = 2.0f, startSize = 0.08f, maxParticles = 120, emissionRate = 130f, shapeType = ParticleSystemShapeType.Circle, shapeRadius = 0.4f, color = new Color(0.7f, 0.95f, 1f, 1f) }
                }
            };
        }

        private static VFXRecipe CreateTrailGlow()
        {
            return new VFXRecipe
            {
                recipeName = "拖尾光效",
                description = "拖尾层 + 亮点粒子",
                layers = new List<VFXRecipeLayer>
                {
                    new VFXRecipeLayer { name = "Trail", kind = VFXRecipeLayerKind.Trail, trailTime = 0.8f, trailWidth = 0.18f, color = new Color(0.35f, 0.85f, 1f, 1f) },
                    new VFXRecipeLayer { name = "Spark", parentName = "Trail", duration = 0.9f, startLifetime = 0.35f, startSpeed = 1.8f, startSize = 0.08f, maxParticles = 70, emissionRate = 80f, color = new Color(0.8f, 1f, 1f, 1f) }
                }
            };
        }

        private static VFXRecipe CreateCardShine()
        {
            return new VFXRecipe
            {
                recipeName = "卡牌闪光",
                description = "网格光面 + 边缘星点",
                layers = new List<VFXRecipeLayer>
                {
                    new VFXRecipeLayer { name = "CardGlow", kind = VFXRecipeLayerKind.Mesh, meshSize = new Vector2(1.2f, 1.6f), color = new Color(1f, 0.85f, 0.25f, 0.45f) },
                    new VFXRecipeLayer { name = "EdgeSpark", parentName = "CardGlow", duration = 1.1f, startLifetime = 0.45f, startSpeed = 1.6f, startSize = 0.06f, maxParticles = 96, emissionRate = 100f, shapeType = ParticleSystemShapeType.Circle, shapeRadius = 0.55f, color = new Color(1f, 0.95f, 0.45f, 1f) }
                }
            };
        }

        private static VFXRecipe CreateSmallExplosion()
        {
            return new VFXRecipe
            {
                recipeName = "小爆炸",
                description = "爆点 + 火花 + 烟雾",
                layers = new List<VFXRecipeLayer>
                {
                    new VFXRecipeLayer { name = "Burst", duration = 0.45f, startLifetime = 0.35f, startSpeed = 1.8f, startSize = 0.5f, maxParticles = 40, emissionRate = 120f, shapeType = ParticleSystemShapeType.Sphere, shapeRadius = 0.08f, color = new Color(1f, 0.45f, 0.15f, 1f) },
                    new VFXRecipeLayer { name = "Sparks", duration = 0.65f, startLifetime = 0.45f, startSpeed = 3.2f, startSize = 0.09f, maxParticles = 120, emissionRate = 180f, shapeType = ParticleSystemShapeType.Sphere, shapeRadius = 0.12f, color = new Color(1f, 0.75f, 0.2f, 1f) },
                    new VFXRecipeLayer { name = "Smoke", duration = 1.2f, startLifetime = 0.9f, startSpeed = 0.45f, startSize = 0.8f, maxParticles = 42, emissionRate = 35f, shapeType = ParticleSystemShapeType.Sphere, shapeRadius = 0.2f, color = new Color(0.55f, 0.45f, 0.38f, 0.45f) }
                }
            };
        }
    }
}
#endif
