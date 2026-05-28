#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator.Cloner
{
    public static class VFXPrefabScanner
    {
        public static VFXPrefabInfo Scan(GameObject prefab)
        {
            var info = new VFXPrefabInfo();
            if (prefab == null) return info;

            info.prefabPath = AssetDatabase.GetAssetPath(prefab);
            info.prefabName = prefab.name;

            ParticleSystem[] particleSystems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var materials = new HashSet<Material>();

            info.particleSystemCount = particleSystems.Length;
            info.rendererCount = renderers.Length;

            foreach (ParticleSystem ps in particleSystems)
            {
                var main = ps.main;
                info.totalMaxParticles += main.maxParticles;
                info.layers.Add(new VFXLayerInfo
                {
                    path = Moshi_VFXPrefabUtil.GetTransformPath(prefab.transform, ps.transform),
                    name = ps.name,
                    componentType = "ParticleSystem",
                    maxParticles = main.maxParticles,
                    looping = main.loop
                });
            }

            foreach (Renderer renderer in renderers)
            {
                Material firstMat = null;
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat == null) continue;
                    materials.Add(mat);
                    if (firstMat == null) firstMat = mat;
                }

                string shaderName = firstMat != null && firstMat.shader != null ? firstMat.shader.name : string.Empty;
                info.layers.Add(new VFXLayerInfo
                {
                    path = Moshi_VFXPrefabUtil.GetTransformPath(prefab.transform, renderer.transform),
                    name = renderer.name,
                    componentType = renderer.GetType().Name,
                    materialPath = firstMat != null ? AssetDatabase.GetAssetPath(firstMat) : string.Empty,
                    shaderName = shaderName,
                    missingMaterial = firstMat == null
                });
            }

            foreach (MeshFilter meshFilter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null)
                {
                    info.layers.Add(new VFXLayerInfo
                    {
                        path = Moshi_VFXPrefabUtil.GetTransformPath(prefab.transform, meshFilter.transform),
                        name = meshFilter.name,
                        componentType = "MeshFilter",
                        missingMesh = true
                    });
                }
            }

            info.materialCount = materials.Count;
            return info;
        }
    }
}
#endif
