#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator.Factory
{
    public static class VFXAssembler
    {
        public static VFXCloneResult GeneratePrefab(VFXRecipe recipe, VFXFactoryConfig config)
        {
            var result = new VFXCloneResult();
            if (recipe == null)
            {
                result.success = false;
                result.message = "配方为空";
                return result;
            }
            if (config == null)
                config = new VFXFactoryConfig();

            string effectName = Moshi_VFXPrefabUtil.SanitizeFileName(string.IsNullOrEmpty(config.effectName) ? recipe.recipeName : config.effectName);
            VFXQualityReport preflight = Moshi_VFXPreflightCheck.CheckOutputConfig(config.outputRoot, effectName);
            if (preflight.HasCriticalIssue())
            {
                result.success = false;
                result.message = "生成前预检失败，请查看质检报告";
                result.qualityReport = preflight;
                return result;
            }

            string rootFolder = config.outputRoot.TrimEnd('/') + "/" + effectName;
            string prefabFolder = rootFolder + "/Prefab";
            string materialFolder = rootFolder + "/Material";
            string meshFolder = rootFolder + "/Mesh";
            string reportFolder = rootFolder + "/Report";

            GameObject root = null;
            try
            {
                Moshi_VFXPrefabUtil.EnsureFolder(prefabFolder);
                Moshi_VFXPrefabUtil.EnsureFolder(materialFolder);
                Moshi_VFXPrefabUtil.EnsureFolder(meshFolder);
                Moshi_VFXPrefabUtil.EnsureFolder(reportFolder);

                root = new GameObject(effectName);
                CreateLayers(root.transform, recipe.layers, config, materialFolder, meshFolder, result.createdAssets);

                string prefabPath = Moshi_VFXPrefabUtil.GetUniqueAssetPath(prefabFolder + "/" + effectName + ".prefab");
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                result.createdAssets.Add(prefabPath);
                result.prefabPath = prefabPath;
                result.variantName = effectName;

                if (config.runQualityCheck)
                    result.qualityReport = Moshi_VFXQualityCheck.CheckPrefab(prefabPath);

                result.success = true;
                result.message = "生成成功";
                WriteGenerateReport(reportFolder, recipe, config, result);
            }
            catch (Exception ex)
            {
                result.success = false;
                result.message = ex.Message;
                Moshi_VFXPrefabUtil.DeleteCreatedAssets(result.createdAssets);
            }
            finally
            {
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }

            AddHistory(recipe, config, result);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        private static void CreateLayers(Transform root, List<VFXRecipeLayer> layers, VFXFactoryConfig config, string materialFolder, string meshFolder, List<string> createdAssets)
        {
            var created = new Dictionary<string, CreatedLayerInfo>();
            if (layers == null) return;
            foreach (VFXRecipeLayer layer in layers)
            {
                if (layer == null) continue;
                Transform parent = root;
                if (!string.IsNullOrEmpty(layer.parentName) && created.TryGetValue(layer.parentName, out CreatedLayerInfo foundParent))
                    parent = foundParent.transform;

                CreatedLayerInfo layerInfo = CreateLayer(parent, layer, config, materialFolder, meshFolder, createdAssets);
                if (!string.IsNullOrEmpty(layer.name) && !created.ContainsKey(layer.name))
                    created.Add(layer.name, layerInfo);
            }

            ApplyMaterialLinks(layers, created);
            ApplySubEmitterLinks(layers, created);
        }

        private static CreatedLayerInfo CreateLayer(Transform parent, VFXRecipeLayer layer, VFXFactoryConfig config, string materialFolder, string meshFolder, List<string> createdAssets)
        {
            GameObject go = new GameObject(layer.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = layer.localPosition;

            switch (layer.kind)
            {
                case VFXRecipeLayerKind.Mesh:
                    return CreateMeshLayer(go, layer, config, materialFolder, meshFolder, createdAssets);
                case VFXRecipeLayerKind.Trail:
                    return CreateTrailLayer(go, layer, config, materialFolder, createdAssets);
                case VFXRecipeLayerKind.MaterialSlot:
                    return CreateMaterialSlot(go, layer, config, materialFolder, createdAssets);
                case VFXRecipeLayerKind.Particle:
                default:
                    return CreateParticleLayer(go, layer, config, materialFolder, createdAssets);
            }
        }

        private static CreatedLayerInfo CreateParticleLayer(GameObject go, VFXRecipeLayer layer, VFXFactoryConfig config, string materialFolder, List<string> createdAssets)
        {
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = Mathf.Max(0.03f, layer.duration);
            main.loop = layer.looping;
            main.startDelay = Mathf.Max(0f, layer.startDelay);
            main.startLifetime = Mathf.Max(0.03f, layer.startLifetime);
            main.startSpeed = Mathf.Max(0f, layer.startSpeed);
            main.startSize = Mathf.Max(0.001f, layer.startSize);
            main.startColor = MultiplyColor(layer.color, config.themeColor);
            main.maxParticles = Mathf.Max(8, layer.maxParticles);
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = Mathf.Max(0f, layer.emissionRate);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = layer.shapeType;
            shape.radius = Mathf.Max(0.001f, layer.shapeRadius);

            ApplyColorOverLifetime(ps, layer, config);
            ApplySizeOverLifetime(ps, layer);
            ApplyTextureSheetAnimation(ps, layer);

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            Material material = CreateMaterial(layer.name, MultiplyColor(layer.color, config.themeColor), materialFolder, createdAssets, config, layer.renderQueue);
            ApplyRendererSettings(renderer, layer);
            renderer.sharedMaterial = material;
            return new CreatedLayerInfo { transform = go.transform, particleSystem = ps, renderer = renderer, material = material };
        }

        private static CreatedLayerInfo CreateMeshLayer(GameObject go, VFXRecipeLayer layer, VFXFactoryConfig config, string materialFolder, string meshFolder, List<string> createdAssets)
        {
            MeshFilter filter = go.AddComponent<MeshFilter>();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            Mesh mesh = config != null && config.useAssetLibraryMesh ? PickMeshByTag(config.meshTag) : null;
            if (mesh == null)
                mesh = CreateQuadMesh(layer.name, layer.meshSize, meshFolder, createdAssets);
            Material material = CreateMaterial(layer.name, MultiplyColor(layer.color, config.themeColor), materialFolder, createdAssets, config, layer.renderQueue);
            ApplyRendererSettings(renderer, layer);
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            return new CreatedLayerInfo { transform = go.transform, renderer = renderer, material = material };
        }

        private static CreatedLayerInfo CreateTrailLayer(GameObject go, VFXRecipeLayer layer, VFXFactoryConfig config, string materialFolder, List<string> createdAssets)
        {
            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            Material material = CreateMaterial(layer.name, MultiplyColor(layer.color, config.themeColor), materialFolder, createdAssets, config, layer.renderQueue);
            ApplyRendererSettings(trail, layer);
            trail.sharedMaterial = material;
            trail.time = Mathf.Max(0.01f, layer.trailTime);
            trail.startWidth = Mathf.Max(0.001f, layer.trailWidth);
            trail.endWidth = 0f;
            trail.startColor = MultiplyColor(layer.color, config.themeColor);
            trail.endColor = new Color(layer.color.r, layer.color.g, layer.color.b, 0f);
            return new CreatedLayerInfo { transform = go.transform, renderer = trail, material = material };
        }

        private static CreatedLayerInfo CreateMaterialSlot(GameObject go, VFXRecipeLayer layer, VFXFactoryConfig config, string materialFolder, List<string> createdAssets)
        {
            Material material = CreateMaterial(layer.name, MultiplyColor(layer.color, config.themeColor), materialFolder, createdAssets, config, layer.renderQueue);
            return new CreatedLayerInfo { transform = go.transform, material = material };
        }

        private static void ApplyMaterialLinks(List<VFXRecipeLayer> layers, Dictionary<string, CreatedLayerInfo> created)
        {
            foreach (VFXRecipeLayer layer in layers)
            {
                if (string.IsNullOrEmpty(layer.materialSourceName)) continue;
                if (!created.TryGetValue(layer.name, out CreatedLayerInfo target)) continue;
                if (!created.TryGetValue(layer.materialSourceName, out CreatedLayerInfo source)) continue;
                if (target.renderer == null || source.material == null) continue;
                target.renderer.sharedMaterial = source.material;
                target.material = source.material;
            }
        }

        private static void ApplySubEmitterLinks(List<VFXRecipeLayer> layers, Dictionary<string, CreatedLayerInfo> created)
        {
            foreach (VFXRecipeLayer layer in layers)
            {
                if (string.IsNullOrEmpty(layer.subEmitterParentName)) continue;
                if (!created.TryGetValue(layer.name, out CreatedLayerInfo child)) continue;
                if (!created.TryGetValue(layer.subEmitterParentName, out CreatedLayerInfo parent)) continue;
                if (child.particleSystem == null || parent.particleSystem == null) continue;

                var childEmission = child.particleSystem.emission;
                childEmission.enabled = false;

                var subEmitters = parent.particleSystem.subEmitters;
                subEmitters.enabled = true;
                subEmitters.AddSubEmitter(child.particleSystem, layer.subEmitterType, ParticleSystemSubEmitterProperties.InheritNothing);
            }
        }

        private static void ApplyRendererSettings(Renderer renderer, VFXRecipeLayer layer)
        {
            if (renderer == null || layer == null) return;
            renderer.sortingOrder = layer.sortingOrder;
        }

        private static void ApplyColorOverLifetime(ParticleSystem ps, VFXRecipeLayer layer, VFXFactoryConfig config)
        {
            var colorModule = ps.colorOverLifetime;
            colorModule.enabled = layer.useColorOverLifetime;
            if (!layer.useColorOverLifetime) return;

            Gradient gradient = CloneGradient(layer.colorOverLifetime);
            if (gradient.colorKeys == null || gradient.colorKeys.Length == 0)
                gradient = CreateDefaultGradient(MultiplyColor(layer.color, config.themeColor));
            colorModule.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void ApplySizeOverLifetime(ParticleSystem ps, VFXRecipeLayer layer)
        {
            var sizeModule = ps.sizeOverLifetime;
            sizeModule.enabled = layer.useSizeOverLifetime;
            if (!layer.useSizeOverLifetime) return;

            AnimationCurve curve = layer.sizeOverLifetime != null && layer.sizeOverLifetime.length > 0
                ? new AnimationCurve(layer.sizeOverLifetime.keys)
                : AnimationCurve.Linear(0f, 1f, 1f, 0f);
            sizeModule.size = new ParticleSystem.MinMaxCurve(Mathf.Max(0.001f, layer.sizeOverLifetimeMultiplier), curve);
        }

        private static void ApplyTextureSheetAnimation(ParticleSystem ps, VFXRecipeLayer layer)
        {
            var textureSheet = ps.textureSheetAnimation;
            textureSheet.enabled = layer.useTextureSheetAnimation;
            if (!layer.useTextureSheetAnimation) return;

            textureSheet.mode = ParticleSystemAnimationMode.Grid;
            textureSheet.animation = ParticleSystemAnimationType.WholeSheet;
            textureSheet.numTilesX = Mathf.Max(1, layer.textureSheetTilesX);
            textureSheet.numTilesY = Mathf.Max(1, layer.textureSheetTilesY);
            textureSheet.frameOverTime = Mathf.Max(0f, layer.textureSheetFrameOverTime);
            textureSheet.cycleCount = 1;
        }

        private static Gradient CloneGradient(Gradient source)
        {
            var gradient = new Gradient();
            if (source == null) return gradient;
            gradient.SetKeys(source.colorKeys, source.alphaKeys);
            gradient.mode = source.mode;
            return gradient;
        }

        private static Gradient CreateDefaultGradient(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        private static Mesh CreateQuadMesh(string layerName, Vector2 size, string meshFolder, List<string> createdAssets)
        {
            float halfX = Mathf.Max(0.001f, size.x) * 0.5f;
            float halfY = Mathf.Max(0.001f, size.y) * 0.5f;
            Mesh mesh = new Mesh { name = "Moshi_" + Moshi_VFXPrefabUtil.SanitizeFileName(layerName) + "_Mesh" };
            mesh.vertices = new[]
            {
                new Vector3(-halfX, -halfY, 0f),
                new Vector3( halfX, -halfY, 0f),
                new Vector3(-halfX,  halfY, 0f),
                new Vector3( halfX,  halfY, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string path = Moshi_VFXPrefabUtil.GetUniqueAssetPath(meshFolder + "/" + mesh.name + ".asset");
            AssetDatabase.CreateAsset(mesh, path);
            createdAssets?.Add(path);
            return mesh;
        }

        private static Material CreateMaterial(string layerName, Color color, string materialFolder, List<string> createdAssets, VFXFactoryConfig config, int renderQueue)
        {
            Material sourceMaterial = config != null && config.useAssetLibraryMaterial ? PickMaterialByTag(config.materialTag) : null;
            Material mat;
            if (sourceMaterial != null)
            {
                mat = UnityEngine.Object.Instantiate(sourceMaterial);
                mat.name = "Moshi_" + Moshi_VFXPrefabUtil.SanitizeFileName(layerName) + "_" + sourceMaterial.name;
            }
            else
            {
                Shader shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Standard");
                mat = new Material(shader) { name = "Moshi_" + Moshi_VFXPrefabUtil.SanitizeFileName(layerName) + "_Mat" };
            }

            SetColorIfExists(mat, "_Color", color);
            SetColorIfExists(mat, "_BaseColor", color);
            SetColorIfExists(mat, "_TintColor", color);
            if (renderQueue >= 0)
                mat.renderQueue = renderQueue;
            if (config != null && config.useAssetLibraryTexture)
                ApplyTextureIfPossible(mat, PickTextureByTag(config.textureTag));
            string path = Moshi_VFXPrefabUtil.GetUniqueAssetPath(materialFolder + "/" + mat.name + ".mat");
            AssetDatabase.CreateAsset(mat, path);
            createdAssets?.Add(path);
            return mat;
        }

        private static Material PickMaterialByTag(string tag)
        {
            foreach (VFXAssetInfo info in Moshi_VFXAssetDB.Instance.assets)
            {
                if (info.kind != VFXAssetKind.Material) continue;
                if (IsTagMatched(info, tag))
                {
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(info.path);
                    if (mat != null) return mat;
                }
            }
            return null;
        }

        private static Texture PickTextureByTag(string tag)
        {
            foreach (VFXAssetInfo info in Moshi_VFXAssetDB.Instance.assets)
            {
                if (info.kind != VFXAssetKind.Texture) continue;
                if (IsTagMatched(info, tag))
                {
                    Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(info.path);
                    if (texture != null) return texture;
                }
            }
            return null;
        }

        private static Mesh PickMeshByTag(string tag)
        {
            foreach (VFXAssetInfo info in Moshi_VFXAssetDB.Instance.assets)
            {
                if (info.kind != VFXAssetKind.Mesh) continue;
                if (IsTagMatched(info, tag))
                {
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(info.path);
                    if (mesh != null) return mesh;
                }
            }
            return null;
        }

        private static bool IsTagMatched(VFXAssetInfo info, string tag)
        {
            return info != null && (string.IsNullOrEmpty(tag) || info.mainTag == tag || info.tags.Contains(tag));
        }

        private static void ApplyTextureIfPossible(Material mat, Texture texture)
        {
            if (mat == null || texture == null) return;
            string[] textureProperties = { "_MainTex", "_BaseMap", "_MaskTex", "_NoiseTex", "_DissolveTex", "_ErosionTex", "_DistortTex" };
            foreach (string property in textureProperties)
            {
                if (mat.HasProperty(property))
                {
                    mat.SetTexture(property, texture);
                    return;
                }
            }
        }

        private static Color MultiplyColor(Color a, Color b)
        {
            return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        }

        private static void SetColorIfExists(Material mat, string property, Color color)
        {
            if (mat.HasProperty(property)) mat.SetColor(property, color);
        }

        private static void WriteGenerateReport(string reportFolder, VFXRecipe recipe, VFXFactoryConfig config, VFXCloneResult result)
        {
            string safeName = Moshi_VFXPrefabUtil.SanitizeFileName(string.IsNullOrEmpty(config.effectName) ? recipe.recipeName : config.effectName);
            string reportPath = Moshi_VFXPrefabUtil.GetUniqueAssetPath(reportFolder + "/" + safeName + "_生成报告.json");
            string json = JsonUtility.ToJson(new VFXGenerateReportData
            {
                recipeName = recipe.recipeName,
                effectName = safeName,
                prefabPath = result.prefabPath,
                success = result.success,
                message = result.message,
                qualityReport = result.qualityReport,
                createdAssets = result.createdAssets
            }, true);
            File.WriteAllText(reportPath, json);
            result.reportPath = reportPath;
            result.createdAssets.Add(reportPath);
        }

        private static void AddHistory(VFXRecipe recipe, VFXFactoryConfig config, VFXCloneResult result)
        {
            Moshi_VFXHistory.Instance.AddRecord(new VFXHistoryRecord
            {
                operation = "配方生成",
                sourcePath = recipe.recipeName,
                outputPath = result.prefabPath,
                reportPath = result.reportPath,
                successCount = result.success ? 1 : 0,
                failCount = result.success ? 0 : 1,
                configJson = JsonUtility.ToJson(config),
                randomSeed = 0,
                createdAssets = result.createdAssets != null ? new List<string>(result.createdAssets) : new List<string>()
            });
        }

        private class CreatedLayerInfo
        {
            public Transform transform;
            public ParticleSystem particleSystem;
            public Renderer renderer;
            public Material material;
        }

        [Serializable]
        private class VFXGenerateReportData
        {
            public string recipeName;
            public string effectName;
            public string prefabPath;
            public bool success;
            public string message;
            public VFXQualityReport qualityReport;
            public List<string> createdAssets;
        }
    }
}
#endif

