#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MoshiTools
{
    public static class UnityBridgeVFXCommands
    {
        public static UnityBridgeResult Ping(UnityBridgeCommand command)
        {
            return Success(command, "Unity Bridge 在线", new UnityBridgeResultData
            {
                unityVersion = Application.unityVersion,
                projectPath = Application.dataPath,
                bridgeVersion = UnityBridgeConstants.BridgeVersion,
                listening = Moshi_UnityBridge.IsListening
            });
        }

        public static UnityBridgeResult GetSelectionInfo(UnityBridgeCommand command)
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return Failure(command, UnityBridgeErrorCodes.TargetNotFound, "当前没有选中任何 GameObject。", "请先在 Unity Hierarchy 或 Project 中选中一个对象。", true);
            }

            UnityBridgeSelectionInfo info = BuildSelectionInfo(selected, command.payload);
            return Success(command, "已获取当前选中对象。", new UnityBridgeResultData { selection = info });
        }

        public static UnityBridgeResult CreateParticlePrefab(UnityBridgeCommand command)
        {
            UnityBridgePayload payload = command.payload ?? new UnityBridgePayload();
            string outputFolder = string.IsNullOrEmpty(command.outputFolder) ? UnityBridgeConstants.DefaultOutputFolder : command.outputFolder;
            outputFolder = UnityBridgeSafety.NormalizeAssetPath(outputFolder).TrimEnd('/');
            if (!UnityBridgeSafety.ValidateOutputFolder(outputFolder, out string error))
                return Failure(command, UnityBridgeErrorCodes.PermissionDenied, error, "请使用 Assets/MoShi/GeneratedVFX/ 作为输出目录。", true);

            string prefabFolder = outputFolder + "/Prefabs";
            string materialFolder = outputFolder + "/Materials";
            UnityBridgeSafety.EnsureAssetFolder(prefabFolder);
            UnityBridgeSafety.EnsureAssetFolder(materialFolder);

            string vfxName = UnityBridgeSafety.SanitizeFileName(payload.vfxName, "FX_CodeBuddyVFX");
            GameObject root = new GameObject(vfxName);
            Undo.RegisterCreatedObjectUndo(root, "创建 CodeBuddy 特效");

            int layerCount = 0;
            int materialCount = 0;
            int maxParticles = 0;
            string firstMaterialPath = string.Empty;

            try
            {
                UnityBridgeVFXLayer[] layers = payload.layers;
                if (layers == null || layers.Length == 0)
                    layers = CreateDefaultLayers(payload);

                for (int i = 0; i < layers.Length; i++)
                {
                    UnityBridgeVFXLayer layer = layers[i] ?? new UnityBridgeVFXLayer();
                    GameObject layerObject = new GameObject(string.IsNullOrEmpty(layer.name) ? $"Layer_{i + 1}" : layer.name);
                    layerObject.transform.SetParent(root.transform, false);
                    ParticleSystem particleSystem = layerObject.AddComponent<ParticleSystem>();
                    ParticleSystemRenderer renderer = layerObject.GetComponent<ParticleSystemRenderer>();
                    ApplyLayer(particleSystem, renderer, payload, layer, i);

                    string materialPath = CreateLayerMaterial(materialFolder, vfxName, layer, i);
                    if (!string.IsNullOrEmpty(materialPath))
                    {
                        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if (material != null)
                        {
                            renderer.sharedMaterial = material;
                            materialCount++;
                            if (string.IsNullOrEmpty(firstMaterialPath)) firstMaterialPath = materialPath;
                        }
                    }

                    layerCount++;
                    maxParticles += Mathf.Max(1, layer.particleCount);
                }

                string prefabPath = UnityBridgeSafety.GetUniqueAssetPath($"{prefabFolder}/{vfxName}.prefab");
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                UnityBridgeContext.LastCommandId = command.commandId;
                UnityBridgeContext.LastGeneratedPrefab = prefabPath;
                UnityBridgeContext.LastOutputFolder = outputFolder;

                return Success(command, $"已生成 {payload.displayName}。", new UnityBridgeResultData
                {
                    prefabPath = prefabPath,
                    materialPath = firstMaterialPath,
                    layerCount = layerCount,
                    materialCount = materialCount,
                    maxParticles = maxParticles,
                    performanceLevel = maxParticles <= 200 ? "Normal" : "Warning"
                }, new[] { "可以继续说：更亮一点", "可以继续说：更短一点", "可以继续说：改成科技风" });
            }
            finally
            {
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static UnityBridgeResult ModifySelectedParticle(UnityBridgeCommand command)
        {
            GameObject target = Selection.activeGameObject;
            if (target == null)
                return Failure(command, UnityBridgeErrorCodes.TargetNotFound, "当前没有选中任何 GameObject。", "请先选中粒子对象后再执行修改。", true);

            if (EditorUtility.IsPersistent(target))
                return Failure(command, UnityBridgeErrorCodes.PermissionDenied, "MVP 阶段不直接修改 Project 中的 Prefab 资产。", "请把 Prefab 拖到 Hierarchy 后再修改，或使用复制副本流程。", true);

            ParticleSystem[] particleSystems = target.GetComponentsInChildren<ParticleSystem>(true);
            if (particleSystems == null || particleSystems.Length == 0)
                return Failure(command, UnityBridgeErrorCodes.TargetNotFound, "选中对象下没有 ParticleSystem。", "请选择粒子对象，或先创建基础粒子 Prefab。", true);

            UnityBridgeAdjustments adjustments = command.payload != null && command.payload.adjustments != null
                ? command.payload.adjustments
                : new UnityBridgeAdjustments();

            List<string> modifiedNames = new List<string>();
            Undo.RecordObjects(particleSystems, "CodeBuddy 修改粒子参数");
            foreach (ParticleSystem ps in particleSystems)
            {
                ApplyAdjustments(ps, adjustments);
                modifiedNames.Add(UnityBridgeSafety.GetHierarchyPath(ps.transform));
                EditorUtility.SetDirty(ps);
            }

            if (target.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(target.scene);

            UnityBridgeContext.LastCommandId = command.commandId;
            return Success(command, "已修改当前选中粒子。", new UnityBridgeResultData
            {
                selection = BuildSelectionInfo(target, command.payload),
                modifiedObjects = modifiedNames.ToArray(),
                layerCount = particleSystems.Length
            });
        }

        public static UnityBridgeResult Preview(UnityBridgeCommand command)
        {
            GameObject target = Selection.activeGameObject;
            if (target == null)
                return Failure(command, UnityBridgeErrorCodes.TargetNotFound, "当前没有选中任何 GameObject。", "请先选中要预览的粒子对象。", true);

            if (UnityBridgeSafety.IsHighRiskPreviewTarget(target))
                return Failure(command, UnityBridgeErrorCodes.PermissionDenied, "目标包含 JSBehaviour / Puerts 等高风险预览对象，已跳过自动播放。", "MVP 阶段只自动预览普通粒子对象。", true);

            ParticleSystem[] particleSystems = target.GetComponentsInChildren<ParticleSystem>(true);
            if (particleSystems == null || particleSystems.Length == 0)
                return Failure(command, UnityBridgeErrorCodes.TargetNotFound, "选中对象下没有 ParticleSystem。", "请选择粒子对象后再预览。", true);

            bool restart = command.payload == null || command.payload.restart;
            bool play = command.payload == null || command.payload.play;
            foreach (ParticleSystem ps in particleSystems)
            {
                if (restart) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (play) ps.Play(true);
                EditorUtility.SetDirty(ps);
            }

            return Success(command, "已刷新粒子预览。", new UnityBridgeResultData
            {
                selection = BuildSelectionInfo(target, command.payload),
                layerCount = particleSystems.Length
            });
        }

        public static UnityBridgeResult Failure(UnityBridgeCommand command, string errorCode, string message, string suggestion, bool recoverable)
        {
            return new UnityBridgeResult
            {
                commandId = command != null ? command.commandId : string.Empty,
                commandType = command != null ? command.commandType : string.Empty,
                status = UnityBridgeStatuses.Failed,
                success = false,
                message = message,
                error = new UnityBridgeError
                {
                    errorCode = errorCode,
                    errorMessage = message,
                    recoverable = recoverable,
                    suggestion = suggestion,
                    unityLogPath = GetEditorLogPath()
                }
            };
        }

        public static UnityBridgeResult Failure(UnityBridgeCommand command, string errorCode, string message, string suggestion, bool recoverable, string stackTrace)
        {
            UnityBridgeResult result = Failure(command, errorCode, message, suggestion, recoverable);
            if (result.error != null) result.error.stackTrace = stackTrace;
            return result;
        }

        private static UnityBridgeResult Success(UnityBridgeCommand command, string message, UnityBridgeResultData data, string[] suggestions = null)
        {
            return new UnityBridgeResult
            {
                commandId = command != null ? command.commandId : string.Empty,
                commandType = command != null ? command.commandType : string.Empty,
                status = UnityBridgeStatuses.Done,
                success = true,
                message = message,
                result = data ?? new UnityBridgeResultData(),
                suggestions = suggestions ?? new string[0]
            };
        }

        private static UnityBridgeVFXLayer[] CreateDefaultLayers(UnityBridgePayload payload)
        {
            string color = payload != null && payload.colorPalette != null && payload.colorPalette.Length > 0 ? payload.colorPalette[0] : "#4AA8FF";
            return new[]
            {
                new UnityBridgeVFXLayer
                {
                    name = "Flash",
                    role = "flash",
                    particleCount = 24,
                    lifetime = 0.18f,
                    startSpeed = 0.2f,
                    startSize = 1.2f,
                    burstCount = 24,
                    color = color,
                    materialHint = "additive"
                },
                new UnityBridgeVFXLayer
                {
                    name = "Spark",
                    role = "spark",
                    particleCount = 48,
                    lifetime = 0.45f,
                    startSpeed = 2.5f,
                    startSize = 0.18f,
                    burstCount = 48,
                    color = color,
                    materialHint = "additive"
                }
            };
        }

        private static void ApplyLayer(ParticleSystem particleSystem, ParticleSystemRenderer renderer, UnityBridgePayload payload, UnityBridgeVFXLayer layer, int layerIndex)
        {
            Color color = ParseColor(layer.color, ParseColor(payload != null && payload.colorPalette != null && payload.colorPalette.Length > 0 ? payload.colorPalette[0] : "#4AA8FF", Color.cyan));
            float duration = Mathf.Clamp(payload != null ? payload.duration : 0.6f, 0.1f, 5f);
            float scale = Mathf.Max(0.01f, payload != null ? payload.scale : 1f);

            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = duration;
            main.loop = payload != null && payload.loop;
            main.startLifetime = Mathf.Clamp(layer.lifetime, 0.03f, 10f);
            main.startSpeed = Mathf.Max(0f, layer.startSpeed) * scale;
            main.startSize = Mathf.Max(0.01f, layer.startSize) * scale;
            main.startColor = color;
            main.maxParticles = Mathf.Clamp(layer.particleCount, 1, 5000);
            main.playOnAwake = false;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = Mathf.Max(0f, layer.emissionRate);
            int burstCount = Mathf.Clamp(layer.burstCount > 0 ? layer.burstCount : layer.particleCount, 1, 5000);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(burstCount, 1, short.MaxValue)) });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.shapeType = layer.role == "spark" ? ParticleSystemShapeType.Sphere : ParticleSystemShapeType.Circle;
            shape.radius = layer.role == "spark" ? 0.15f : 0.05f;

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = payload != null ? payload.sortingOrder + layerIndex : layerIndex;
        }

        private static string CreateLayerMaterial(string materialFolder, string vfxName, UnityBridgeVFXLayer layer, int layerIndex)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return string.Empty;

            Material material = new Material(shader);
            Color color = ParseColor(layer.color, Color.cyan);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);

            string materialName = UnityBridgeSafety.SanitizeFileName($"{vfxName}_{layer.name}_{layerIndex + 1}", $"{vfxName}_Mat_{layerIndex + 1}");
            string materialPath = UnityBridgeSafety.GetUniqueAssetPath($"{materialFolder}/{materialName}.mat");
            AssetDatabase.CreateAsset(material, materialPath);
            return materialPath;
        }

        private static void ApplyAdjustments(ParticleSystem ps, UnityBridgeAdjustments adjustments)
        {
            ParticleSystem.MainModule main = ps.main;
            float durationMultiplier = Mathf.Max(0.05f, adjustments.durationMultiplier <= 0f ? 1f : adjustments.durationMultiplier);
            float sizeMultiplier = Mathf.Max(0.01f, adjustments.sizeMultiplier <= 0f ? 1f : adjustments.sizeMultiplier);
            float speedMultiplier = Mathf.Max(0f, adjustments.speedMultiplier <= 0f ? 1f : adjustments.speedMultiplier);
            float brightnessMultiplier = Mathf.Max(0f, adjustments.brightnessMultiplier <= 0f ? 1f : adjustments.brightnessMultiplier);
            float countMultiplier = Mathf.Max(0.01f, adjustments.particleCountMultiplier <= 0f ? 1f : adjustments.particleCountMultiplier);

            main.duration = Mathf.Clamp(main.duration * durationMultiplier, 0.03f, 30f);
            main.startLifetime = GetCurveConstant(main.startLifetime, 0.5f) * durationMultiplier;
            main.startSize = GetCurveConstant(main.startSize, 0.5f) * sizeMultiplier;
            main.startSpeed = GetCurveConstant(main.startSpeed, 1f) * speedMultiplier;
            main.maxParticles = Mathf.Clamp(Mathf.RoundToInt(main.maxParticles * countMultiplier), 1, 10000);

            Color color = GetStartColor(ps);
            Color.RGBToHSV(color, out float h, out float s, out float v);
            if (adjustments.colorShift != null)
            {
                h = Mathf.Repeat(h + adjustments.colorShift.hueOffset, 1f);
                s = Mathf.Clamp01(s * Mathf.Max(0f, adjustments.colorShift.saturationMultiplier <= 0f ? 1f : adjustments.colorShift.saturationMultiplier));
                v = Mathf.Clamp01(v * Mathf.Max(0f, adjustments.colorShift.valueMultiplier <= 0f ? brightnessMultiplier : adjustments.colorShift.valueMultiplier * brightnessMultiplier));
            }
            else
            {
                v = Mathf.Clamp01(v * brightnessMultiplier);
            }
            main.startColor = Color.HSVToRGB(h, s, v);

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = GetCurveConstant(emission.rateOverTime, 0f) * countMultiplier;
            int burstCount = emission.burstCount;
            if (burstCount > 0)
            {
                ParticleSystem.Burst[] bursts = new ParticleSystem.Burst[burstCount];
                emission.GetBursts(bursts);
                for (int i = 0; i < bursts.Length; i++)
                {
                    short min = (short)Mathf.Clamp(Mathf.RoundToInt(bursts[i].minCount * countMultiplier), 0, short.MaxValue);
                    short max = (short)Mathf.Clamp(Mathf.RoundToInt(bursts[i].maxCount * countMultiplier), 0, short.MaxValue);
                    bursts[i] = new ParticleSystem.Burst(bursts[i].time, min, max);
                }
                emission.SetBursts(bursts);

            }
        }

        private static UnityBridgeSelectionInfo BuildSelectionInfo(GameObject go, UnityBridgePayload payload)
        {
            List<string> components = new List<string>();
            if (payload == null || payload.includeComponents)
            {
                Component[] componentArray = go.GetComponents<Component>();
                foreach (Component component in componentArray)
                    components.Add(component != null ? component.GetType().Name : "MissingComponent");
            }

            string assetPath = string.Empty;
            if (payload == null || payload.includeAssetPath)
            {
                assetPath = AssetDatabase.GetAssetPath(go);
                if (string.IsNullOrEmpty(assetPath))
                {
                    GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    if (source != null) assetPath = AssetDatabase.GetAssetPath(source);
                }
            }

            return new UnityBridgeSelectionInfo
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                hierarchyPath = payload == null || payload.includeHierarchyPath ? UnityBridgeSafety.GetHierarchyPath(go.transform) : string.Empty,
                assetPath = assetPath,
                components = components.ToArray()
            };
        }

        private static Color ParseColor(string value, Color fallback)
        {
            if (!string.IsNullOrEmpty(value) && ColorUtility.TryParseHtmlString(value, out Color color))
                return color;
            return fallback;
        }

        private static Color GetStartColor(ParticleSystem ps)
        {
            ParticleSystem.MinMaxGradient gradient = ps.main.startColor;
            return gradient.mode == ParticleSystemGradientMode.Color ? gradient.color : Color.white;
        }

        private static float GetCurveConstant(ParticleSystem.MinMaxCurve curve, float fallback)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return curve.constant;
                case ParticleSystemCurveMode.TwoConstants:
                    return curve.constantMax;
                default:
                    return fallback;
            }
        }

        private static string GetEditorLogPath()
        {
            return System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Unity/Editor/Editor.log").Replace('\\', '/');
        }
    }
}
#endif
