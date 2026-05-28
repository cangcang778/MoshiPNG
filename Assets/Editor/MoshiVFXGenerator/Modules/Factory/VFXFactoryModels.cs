#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoshiVFXGenerator.Factory
{
    public enum VFXRecipeLayerKind { Particle, Mesh, Trail, MaterialSlot }

    [Serializable]
    public class VFXRecipeLayer
    {
        public string name;
        public string parentName;
        public string subEmitterParentName;
        public string materialSourceName;
        public VFXRecipeLayerKind kind = VFXRecipeLayerKind.Particle;
        public Vector3 localPosition = Vector3.zero;
        public float duration = 1f;
        public float startLifetime = 0.5f;
        public float startSpeed = 1f;
        public float startSize = 1f;
        public int maxParticles = 80;
        public float emissionRate = 20f;
        public bool looping = false;
        public float startDelay = 0f;
        public ParticleSystemShapeType shapeType = ParticleSystemShapeType.Sphere;
        public float shapeRadius = 0.2f;
        public Color color = Color.white;
        public int sortingOrder = 0;
        public int renderQueue = -1;
        public ParticleSystemSubEmitterType subEmitterType = ParticleSystemSubEmitterType.Death;
        public bool useColorOverLifetime = false;
        public Gradient colorOverLifetime = new Gradient();
        public bool useSizeOverLifetime = false;
        public AnimationCurve sizeOverLifetime = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        public float sizeOverLifetimeMultiplier = 1f;
        public bool useTextureSheetAnimation = false;
        public int textureSheetTilesX = 4;
        public int textureSheetTilesY = 4;
        public float textureSheetFrameOverTime = 1f;
        public Vector2 meshSize = Vector2.one;
        public float trailTime = 0.6f;
        public float trailWidth = 0.12f;
    }

    [Serializable]
    public class VFXRecipe
    {
        public string recipeName;
        public string description;
        public List<VFXRecipeLayer> layers = new List<VFXRecipeLayer>();
    }

    [Serializable]
    public class VFXFactoryConfig
    {
        public string effectName = "NewVFX";
        public string outputRoot = "Assets/MoShi/GeneratedVFX";
        public Color themeColor = Color.white;
        public int selectedRecipeIndex = 0;
        public bool runQualityCheck = true;
        public bool useAssetLibraryMaterial = false;
        public string materialTag = "加法";
        public bool useAssetLibraryTexture = false;
        public string textureTag = "光斑";
        public bool useAssetLibraryMesh = false;
        public string meshTag = "Quad";
    }
}
#endif
