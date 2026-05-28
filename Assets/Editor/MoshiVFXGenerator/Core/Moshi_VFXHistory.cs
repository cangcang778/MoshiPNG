#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MoshiVFXGenerator
{
    public class Moshi_VFXHistory : ScriptableObject
    {
        private const string ASSET_PATH = "Assets/Editor/MoshiVFXGenerator/Settings/MoshiVFXHistory.asset";
        private const int MAX_RECORDS = 200;

        public List<VFXHistoryRecord> records = new List<VFXHistoryRecord>();

        public static Moshi_VFXHistory Instance
        {
            get
            {
                var asset = AssetDatabase.LoadAssetAtPath<Moshi_VFXHistory>(ASSET_PATH);
                if (asset != null) return asset;

                Moshi_VFXPrefabUtil.EnsureFolder("Assets/Editor/MoshiVFXGenerator/Settings");
                asset = CreateInstance<Moshi_VFXHistory>();
                AssetDatabase.CreateAsset(asset, ASSET_PATH);
                AssetDatabase.SaveAssets();
                return asset;
            }
        }

        public void AddRecord(VFXHistoryRecord record)
        {
            if (record == null) return;
            if (string.IsNullOrEmpty(record.time))
                record.time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            records.Insert(0, record);
            while (records.Count > MAX_RECORDS)
                records.RemoveAt(records.Count - 1);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        public void RemoveRecord(VFXHistoryRecord record)
        {
            if (record == null) return;
            records.Remove(record);
            Save();
        }

        public void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        public void Clear()
        {
            records.Clear();
            Save();
        }
    }
}
#endif
