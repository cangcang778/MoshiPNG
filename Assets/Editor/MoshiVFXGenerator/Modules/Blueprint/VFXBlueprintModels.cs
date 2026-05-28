#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoshiVFXGenerator.Blueprint
{
    public enum VFXBlueprintNodeType { Root, ParticleLayer, MeshLayer, TrailLayer, MaterialSlot }
    public enum VFXBlueprintConnectionType { Parent, SubEmitter, Material }

    [Serializable]
    public class VFXBlueprintNode
    {
        public string id;
        public string name;
        public VFXBlueprintNodeType nodeType;
        public Vector2 position;
        public string presetName;
        public Color color = Color.white;
        public float duration = 1f;
        public float startLifetime = 0.5f;
        public float startSpeed = 1f;
        public float startSize = 1f;
        public int maxParticles = 80;
        public float emissionRate = 20f;
        public int sortingOrder = 0;
        public int renderQueue = -1;
        public ParticleSystemSubEmitterType subEmitterType = ParticleSystemSubEmitterType.Death;
        public bool looping = false;
        public float startDelay = 0f;
        public ParticleSystemShapeType shapeType = ParticleSystemShapeType.Sphere;
        public float shapeRadius = 0.2f;
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
    public class VFXBlueprintConnection
    {
        public string fromNodeId;
        public string toNodeId;
        public VFXBlueprintConnectionType connectionType = VFXBlueprintConnectionType.Parent;
    }

    [Serializable]
    public class VFXBlueprint
    {
        public string blueprintName = "NewBlueprint";
        public List<VFXBlueprintNode> nodes = new List<VFXBlueprintNode>();
        public List<VFXBlueprintConnection> connections = new List<VFXBlueprintConnection>();
    }
}
#endif
