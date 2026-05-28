#if UNITY_EDITOR
using System.IO;
using UnityEngine;

namespace MoshiVFXGenerator.Factory
{
    public static class VFXRecipeSerializer
    {
        public static void Save(string path, VFXRecipe recipe)
        {
            if (string.IsNullOrEmpty(path) || recipe == null) return;
            File.WriteAllText(path, JsonUtility.ToJson(recipe, true));
        }

        public static VFXRecipe Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            return JsonUtility.FromJson<VFXRecipe>(File.ReadAllText(path));
        }
    }
}
#endif
