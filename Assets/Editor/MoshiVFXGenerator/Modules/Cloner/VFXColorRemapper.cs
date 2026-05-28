#if UNITY_EDITOR
using UnityEngine;

namespace MoshiVFXGenerator.Cloner
{
    public static class VFXColorRemapper
    {
        public static void ApplyHueShift(GameObject root, float hueShift, System.Collections.Generic.List<string> excludedLayerPaths = null)
        {
            if (root == null || Mathf.Approximately(hueShift, 0f)) return;
            RemapAllColors(root, color => ShiftHue(color, hueShift), excludedLayerPaths);
        }

        public static void ApplyPalette(GameObject root, Color paletteColor, System.Collections.Generic.List<string> excludedLayerPaths = null)
        {
            if (root == null) return;
            RemapAllColors(root, color => ApplyPaletteColor(color, paletteColor), excludedLayerPaths);
        }

        private static void RemapAllColors(GameObject root, System.Func<Color, Color> remap, System.Collections.Generic.List<string> excludedLayerPaths)
        {
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ShouldSkip(root.transform, ps.transform, excludedLayerPaths)) continue;
                var main = ps.main;
                main.startColor = RemapGradient(main.startColor, remap);

                var colorModule = ps.colorOverLifetime;
                if (colorModule.enabled)
                    colorModule.color = RemapGradient(colorModule.color, remap);
            }

            foreach (TrailRenderer trail in root.GetComponentsInChildren<TrailRenderer>(true))
                trail.colorGradient = RemapGradient(trail.colorGradient, remap);

            foreach (LineRenderer line in root.GetComponentsInChildren<LineRenderer>(true))
                line.colorGradient = RemapGradient(line.colorGradient, remap);

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (ShouldSkip(root.transform, renderer.transform, excludedLayerPaths)) continue;
                foreach (Material mat in renderer.sharedMaterials)
                    RemapMaterial(mat, remap);
            }
        }

        public static Color ShiftHue(Color color, float hueShift)
        {
            if (color.a < 0.01f) return color;
            Color.RGBToHSV(color, out float h, out float s, out float v);
            if (s < 0.08f) return color;
            h = Mathf.Repeat(h + hueShift, 1f);
            Color result = Color.HSVToRGB(h, s, v, true);
            result.a = color.a;
            return result;
        }

        private static bool ShouldSkip(Transform root, Transform target, System.Collections.Generic.List<string> excludedLayerPaths)
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

        private static Color ApplyPaletteColor(Color color, Color paletteColor)
        {
            if (color.a < 0.01f) return color;
            Color.RGBToHSV(color, out _, out float s, out float v);
            if (s < 0.08f) return color;
            Color.RGBToHSV(paletteColor, out float ph, out float ps, out _);
            Color result = Color.HSVToRGB(ph, Mathf.Max(s, ps * 0.6f), v, true);
            result.a = color.a * paletteColor.a;
            return result;
        }

        private static ParticleSystem.MinMaxGradient RemapGradient(ParticleSystem.MinMaxGradient gradient, System.Func<Color, Color> remap)
        {
            switch (gradient.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(remap(gradient.color));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(remap(gradient.colorMin), remap(gradient.colorMax));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(RemapGradient(gradient.gradient, remap));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(RemapGradient(gradient.gradientMin, remap), RemapGradient(gradient.gradientMax, remap));
                default:
                    return gradient;
            }
        }

        private static Gradient RemapGradient(Gradient gradient, System.Func<Color, Color> remap)
        {
            if (gradient == null) return gradient;
            var newGradient = new Gradient();
            GradientColorKey[] colorKeys = gradient.colorKeys;
            GradientAlphaKey[] alphaKeys = gradient.alphaKeys;
            for (int i = 0; i < colorKeys.Length; i++)
                colorKeys[i].color = remap(colorKeys[i].color);
            newGradient.SetKeys(colorKeys, alphaKeys);
            return newGradient;
        }

        private static void RemapMaterial(Material mat, System.Func<Color, Color> remap)
        {
            if (mat == null) return;
            RemapMaterialColor(mat, "_Color", remap);
            RemapMaterialColor(mat, "_BaseColor", remap);
            RemapMaterialColor(mat, "_TintColor", remap);
            RemapMaterialColor(mat, "_MainColor", remap);
            RemapMaterialColor(mat, "_EmissionColor", remap);
        }

        private static void RemapMaterialColor(Material mat, string property, System.Func<Color, Color> remap)
        {
            if (!mat.HasProperty(property)) return;
            mat.SetColor(property, remap(mat.GetColor(property)));
        }
    }
}
#endif
