using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Retains files touched by a synchronous save so a later failure cannot leave half a batch on disk.</summary>
    internal sealed class PlayerPresetSaveSnapshot
    {
        #region State

        private readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
        private readonly List<(ScriptableObject Asset, string Json, bool Dirty)> assets = new List<(ScriptableObject, string, bool)>();

        #endregion

        #region Methods

        #region Capture

        /// <summary>Captures preset files and unsaved in-memory values before any batch write.</summary>
        /// <param name="batch">Prepared properties whose source assets remain unchanged at this point.</param>
        internal PlayerPresetSaveSnapshot(PlayerPresetBatch batch)
        {
            // A previously dirty asset must retain its unsaved values if this confirmation later fails.
            foreach (SerializedObject item in batch.Items)
            {
                ScriptableObject asset = (ScriptableObject)item.targetObject;
                Capture(AssetDatabase.GetAssetPath(asset));
                assets.Add((asset, JsonUtility.ToJson(asset), EditorUtility.IsDirty(asset)));
            }
        }

        /// <summary>Records an existing prefab or a new generated destination immediately before it can be written.</summary>
        /// <param name="path">Exact asset path owned by this confirmation.</param>
        internal void Capture(string path)
        {
            // Null bytes distinguish a new asset from an existing empty file.
            if (!string.IsNullOrEmpty(path) && !files.ContainsKey(path))
                files.Add(path, File.Exists(path) ? File.ReadAllBytes(path) : null);
        }

        #endregion

        #region Recovery

        /// <summary>Restores recorded files after native Undo has restored scene objects and preset values.</summary>
        internal void Restore()
        {
            // Only exact paths recorded before their write can be restored or removed.
            foreach (KeyValuePair<string, byte[]> file in files)
                if (file.Value == null)
                {
                    if (File.Exists(file.Key) && !AssetDatabase.DeleteAsset(file.Key))
                        throw new IOException("Could not remove the incomplete asset: " + file.Key);
                }
                else
                {
                    File.WriteAllBytes(file.Key, file.Value);
                    AssetDatabase.ImportAsset(file.Key, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }

            // Import restores disk data; reapply any pre-existing unsaved preset state on top of it.
            foreach ((ScriptableObject Asset, string Json, bool Dirty) asset in assets)
                if (asset.Asset != null && asset.Dirty)
                {
                    JsonUtility.FromJsonOverwrite(asset.Json, asset.Asset);
                    EditorUtility.SetDirty(asset.Asset);
                }
        }

        #endregion

        #endregion
    }
}
