#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator.Cloner
{
    public static class VFXMaterialSwapper
    {
        private static readonly string[] TextureProperties =
        {
            "_MainTex", "_BaseMap", "_MaskTex", "_NoiseTex", "_DissolveTex", "_ErosionTex", "_DistortTex"
        };

        public static Dictionary<Material, Material> CloneAndAssignMaterials(GameObject root, string materialFolder, List<string> createdAssets)
        {
            var map = new Dictionary<Material, Material>();
            if (root == null) return map;

            Moshi_VFXPrefabUtil.EnsureFolder(materialFolder);

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] sourceMaterials = renderer.sharedMaterials;
                if (sourceMaterials == null || sourceMaterials.Length == 0) continue;

                Material[] newMaterials = new Material[sourceMaterials.Length];
                bool changed = false;
                for (int i = 0; i < sourceMaterials.Length; i++)
                {
                    Material source = sourceMaterials[i];
                    if (source == null)
                    {
                        newMaterials[i] = null;
                        continue;
                    }

                    if (!map.TryGetValue(source, out Material cloned))
                    {
                        cloned = Object.Instantiate(source);
                        cloned.name = source.name + "_Gen";
                        string matPath = Moshi_VFXPrefabUtil.GetUniqueAssetPath(materialFolder + "/" + cloned.name + ".mat");
                        AssetDatabase.CreateAsset(cloned, matPath);
                        createdAssets?.Add(matPath);
                        map[source] = cloned;
                    }

                    newMaterials[i] = cloned;
                    changed = true;
                }

                if (changed)
                    renderer.sharedMaterials = newMaterials;
            }

            return map;
        }

        public static void ReplaceTextures(GameObject root, Texture replacementTexture, List<string> excludedLayerPaths = null)
        {
            if (root == null || replacementTexture == null) return;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (ShouldSkip(root.transform, renderer.transform, excludedLayerPaths)) continue;
                Material[] materials = renderer.sharedMaterials;
                if (materials == null) continue;

                foreach (Material mat in materials)
                {
                    if (mat == null) continue;
                    foreach (string property in TextureProperties)
                    {
                        if (!mat.HasProperty(property)) continue;
                        Texture oldTexture = mat.GetTexture(property);
                        if (oldTexture == null) continue;
                        mat.SetTexture(property, replacementTexture);
                    }
                }
            }
        }

        private static bool ShouldSkip(Transform root, Transform target, List<string> excludedLayerPaths)
        {
            if (excludedLayerPaths == null || excludedLayerPaths.Count == 0 || root == null || target == null) return false;
            string path = Moshi_VFXPrefabUtil.GetTransformPath(root, target);
            string relative = path.Contains("/") ? path.Substring(path.IndexOf('/') + 1) : path;
            foreach (string excluded in excludedLayerPaths)
            {
                if (string.IsNullOrEmpty(excluded)) continue;
                string ex = excluded.Contains("/") ? excluded.Substring(excluded.IndexOf('/') + 1) : excluded;
                if (relative == ex || path == excluded || target.name == excluded) return true;
            }
            return false;
        }
    }
}
#endif


