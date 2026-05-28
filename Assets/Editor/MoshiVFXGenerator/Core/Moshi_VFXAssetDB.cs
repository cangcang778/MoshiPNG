#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator
{
    public class Moshi_VFXAssetDB : ScriptableObject
    {
        private const string ASSET_PATH = "Assets/Editor/MoshiVFXGenerator/Settings/MoshiVFXAssetDatabase.asset";

        public List<VFXAssetInfo> assets = new List<VFXAssetInfo>();
        public string lastScanTime;

        public static Moshi_VFXAssetDB Instance
        {
            get
            {
                var asset = AssetDatabase.LoadAssetAtPath<Moshi_VFXAssetDB>(ASSET_PATH);
                if (asset != null) return asset;

                Moshi_VFXPrefabUtil.EnsureFolder("Assets/Editor/MoshiVFXGenerator/Settings");
                asset = CreateInstance<Moshi_VFXAssetDB>();
                AssetDatabase.CreateAsset(asset, ASSET_PATH);
                AssetDatabase.SaveAssets();
                return asset;
            }
        }

        public void Scan(List<string> folders)
        {
            Dictionary<string, VFXAssetInfo> previous = BuildPreviousMap();
            assets.Clear();
            string[] validFolders = GetValidFolders(folders);
            AddAssets(validFolders, "t:Shader", VFXAssetKind.Shader, previous);
            AddAssets(validFolders, "t:Material", VFXAssetKind.Material, previous);
            AddAssets(validFolders, "t:Texture", VFXAssetKind.Texture, previous);
            AddAssets(validFolders, "t:Mesh", VFXAssetKind.Mesh, previous);
            AddAssets(validFolders, "t:Prefab", VFXAssetKind.Prefab, previous);
            lastScanTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        public int CountByKind(VFXAssetKind kind)
        {
            return assets.FindAll(a => a.kind == kind).Count;
        }

        public void SetMainTag(VFXAssetInfo info, string tag)
        {
            if (info == null) return;
            info.mainTag = string.IsNullOrEmpty(tag) ? "未分类" : tag;
            if (!info.tags.Contains(info.mainTag))
                info.tags.Insert(0, info.mainTag);
            Save();
        }

        public void SetTags(VFXAssetInfo info, string tagsText)
        {
            if (info == null) return;
            ApplyTags(info, tagsText, true);
            Save();
        }

        public void SetFavorite(VFXAssetInfo info, bool favorite)
        {
            if (info == null) return;
            info.favorite = favorite;
            Save();
        }

        public int BatchAppendTag(VFXAssetKind kindFilter, string searchText, string tagFilter, bool favoritesOnly, string tag)
        {
            if (string.IsNullOrEmpty(tag)) return 0;
            int count = 0;
            foreach (VFXAssetInfo info in assets)
            {
                if (!IsMatched(info, kindFilter, searchText, tagFilter, favoritesOnly)) continue;
                if (!info.tags.Contains(tag)) info.tags.Add(tag);
                info.mainTag = info.tags.Count > 0 ? info.tags[0] : tag;
                count++;
            }
            if (count > 0) Save();
            return count;
        }

        public int BatchSetMainTag(VFXAssetKind kindFilter, string searchText, string tagFilter, bool favoritesOnly, string tag)
        {
            if (string.IsNullOrEmpty(tag)) return 0;
            int count = 0;
            foreach (VFXAssetInfo info in assets)
            {
                if (!IsMatched(info, kindFilter, searchText, tagFilter, favoritesOnly)) continue;
                info.mainTag = tag;
                if (!info.tags.Contains(tag)) info.tags.Insert(0, tag);
                count++;
            }
            if (count > 0) Save();
            return count;
        }

        public void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        private void AddAssets(string[] folders, string filter, VFXAssetKind kind, Dictionary<string, VFXAssetInfo> previous)
        {
            string[] guids = folders.Length > 0 ? AssetDatabase.FindAssets(filter, folders) : AssetDatabase.FindAssets(filter);
            var known = new HashSet<string>();
            foreach (VFXAssetInfo info in assets)
                known.Add(GetKey(info.guid, info.kind));

            foreach (string guid in guids)
            {
                string key = GetKey(guid, kind);
                if (known.Contains(key)) continue;

                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                var info = new VFXAssetInfo
                {
                    guid = guid,
                    path = path,
                    name = Path.GetFileNameWithoutExtension(path),
                    kind = kind,
                    fileSize = File.Exists(path) ? new FileInfo(path).Length : 0
                };
                FillTags(info);
                RestoreManualInfo(info, previous);
                assets.Add(info);
                known.Add(key);
            }
        }

        private string[] GetValidFolders(List<string> folders)
        {
            var result = new List<string>();
            if (folders != null)
            {
                foreach (string folder in folders)
                {
                    if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder))
                        result.Add(folder);
                }
            }
            return result.ToArray();
        }

        private Dictionary<string, VFXAssetInfo> BuildPreviousMap()
        {
            var map = new Dictionary<string, VFXAssetInfo>();
            foreach (VFXAssetInfo info in assets)
            {
                if (info == null || string.IsNullOrEmpty(info.guid)) continue;
                map[GetKey(info.guid, info.kind)] = info;
            }
            return map;
        }

        private void RestoreManualInfo(VFXAssetInfo info, Dictionary<string, VFXAssetInfo> previous)
        {
            if (info == null || previous == null) return;
            if (!previous.TryGetValue(GetKey(info.guid, info.kind), out VFXAssetInfo oldInfo)) return;
            info.favorite = oldInfo.favorite;
            if (oldInfo.tags != null && oldInfo.tags.Count > 0)
            {
                info.tags = new List<string>(oldInfo.tags);
                info.mainTag = string.IsNullOrEmpty(oldInfo.mainTag) ? info.tags[0] : oldInfo.mainTag;
            }
        }

        private static string GetKey(string guid, VFXAssetKind kind)
        {
            return guid + "_" + kind;
        }

        private void ApplyTags(VFXAssetInfo info, string tagsText, bool replace)
        {
            if (replace) info.tags.Clear();
            if (!string.IsNullOrEmpty(tagsText))
            {
                string[] parts = tagsText.Split(',', '，', ';', '；');
                foreach (string part in parts)
                {
                    string tag = part.Trim();
                    if (!string.IsNullOrEmpty(tag) && !info.tags.Contains(tag))
                        info.tags.Add(tag);
                }
            }
            info.mainTag = info.tags.Count > 0 ? info.tags[0] : "未分类";
        }

        private bool IsMatched(VFXAssetInfo info, VFXAssetKind kindFilter, string searchText, string tagFilter, bool favoritesOnly)
        {
            if (info == null) return false;
            if (favoritesOnly && !info.favorite) return false;
            if (kindFilter != VFXAssetKind.Unknown && info.kind != kindFilter) return false;
            if (!string.IsNullOrEmpty(searchText))
            {
                string lower = searchText.ToLowerInvariant();
                string target = (info.name + " " + info.path + " " + info.shaderName).ToLowerInvariant();
                if (!target.Contains(lower)) return false;
            }
            if (!string.IsNullOrEmpty(tagFilter))
            {
                if (!ContainsTag(info, tagFilter)) return false;
            }
            return true;
        }

        private bool ContainsTag(VFXAssetInfo info, string tag)
        {
            if (!string.IsNullOrEmpty(info.mainTag) && info.mainTag.Contains(tag)) return true;
            foreach (string item in info.tags)
            {
                if (!string.IsNullOrEmpty(item) && item.Contains(tag)) return true;
            }
            return false;
        }

        private void FillTags(VFXAssetInfo info)
        {
            string lower = (info.name + " " + info.path).ToLowerInvariant();
            switch (info.kind)
            {
                case VFXAssetKind.Material:
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(info.path);
                    if (mat != null && mat.shader != null)
                    {
                        info.shaderName = mat.shader.name;
                        AddShaderTags(info, mat.shader.name.ToLowerInvariant());
                    }
                    break;
                case VFXAssetKind.Shader:
                    AddShaderTags(info, lower);
                    break;
                case VFXAssetKind.Texture:
                    AddTextureTags(info, lower);
                    break;
                case VFXAssetKind.Mesh:
                    AddNameTag(info, lower, "quad", "Quad");
                    AddNameTag(info, lower, "ring", "圆环");
                    AddNameTag(info, lower, "circle", "圆形");
                    break;
                case VFXAssetKind.Prefab:
                    info.mainTag = "Prefab";
                    break;
            }

            if (string.IsNullOrEmpty(info.mainTag))
                info.mainTag = info.tags.Count > 0 ? info.tags[0] : "未分类";
        }

        private void AddShaderTags(VFXAssetInfo info, string lower)
        {
            AddNameTag(info, lower, "add", "加法");
            AddNameTag(info, lower, "alpha", "透明");
            AddNameTag(info, lower, "trans", "透明");
            AddNameTag(info, lower, "dissolve", "溶解");
            AddNameTag(info, lower, "erosion", "溶解");
            AddNameTag(info, lower, "uv", "UV流动");
            AddNameTag(info, lower, "mask", "遮罩");
            AddNameTag(info, lower, "distort", "扭曲");
            AddNameTag(info, lower, "fresnel", "菲涅尔");
        }

        private void AddTextureTags(VFXAssetInfo info, string lower)
        {
            AddNameTag(info, lower, "noise", "噪声");
            AddNameTag(info, lower, "mask", "遮罩");
            AddNameTag(info, lower, "grad", "渐变");
            AddNameTag(info, lower, "gradient", "渐变");
            AddNameTag(info, lower, "shape", "形状");
            AddNameTag(info, lower, "star", "星光");
            AddNameTag(info, lower, "flare", "光斑");
            AddNameTag(info, lower, "sequence", "序列帧");
        }

        private void AddNameTag(VFXAssetInfo info, string lower, string key, string tag)
        {
            if (!lower.Contains(key) || info.tags.Contains(tag)) return;
            info.tags.Add(tag);
            if (string.IsNullOrEmpty(info.mainTag)) info.mainTag = tag;
        }
    }
}
#endif
