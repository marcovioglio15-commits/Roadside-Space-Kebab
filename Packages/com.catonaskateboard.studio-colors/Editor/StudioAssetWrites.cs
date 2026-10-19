using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Verifies explicit asset writes and restores their previous content when Unity cannot save.</summary>
    public static class StudioAssetWrites
    {
        #region Methods
        #region Validation

        /// <summary>Checks the asset database and physical file before a tool changes saved data.</summary>
        /// <param name="asset">Existing project asset receiving an explicit write.</param>
        /// <param name="warning">Receives a missing-file or permission diagnostic.</param>
        /// <returns>True when the project asset is currently writable.</returns>
        public static bool CanWrite(UnityEngine.Object asset, out string warning)
        {
            warning = "Select a writable project asset before saving.";
            if (asset == null || !EditorUtility.IsPersistent(asset) || !AssetDatabase.IsOpenForEdit(asset)
                || EditorApplication.isPlayingOrWillChangePlaymode)
                return false;
            try
            {
                string path = AssetDatabase.GetAssetPath(asset);
                if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
                    return false;
                warning = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                warning = "Cannot access the asset file: " + exception.Message;
                return false;
            }
        }

        #endregion
        #region Transactions

        /// <summary>Applies validated fields with native Undo, checking completion before callers advance their baselines.</summary>
        /// <param name="asset">Writable destination whose identity and name remain unchanged.</param>
        /// <param name="label">Undo label for this explicit save.</param>
        /// <param name="apply">Copies only the proposed configuration into the destination.</param>
        public static void Save(ScriptableObject asset, string label, Action apply)
        {
            if (!CanWrite(asset, out string warning))
                throw new IOException(warning);
            // Preserve both disk and unsaved native state; either may differ before this transaction.
            string path = AssetDatabase.GetAssetPath(asset);
            byte[] file = File.ReadAllBytes(path);
            string original = EditorJsonUtility.ToJson(asset);
            HideFlags flags = asset.hideFlags;
            string name = asset.name;
            bool dirty = EditorUtility.IsDirty(asset);
            try
            {
                Undo.RecordObject(asset, label);
                apply();
                asset.hideFlags = flags;
                asset.name = name;
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                if (EditorUtility.IsDirty(asset))
                    throw new IOException("Unity could not complete the asset write.");
            }
            catch (Exception failure)
            {
                // Restore a partially written file before restoring any previously unsaved native edits.
                try
                {
                    if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(file))
                    {
                        File.WriteAllBytes(path, file);
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    }
                    EditorJsonUtility.FromJsonOverwrite(original, asset);
                    if (dirty)
                        EditorUtility.SetDirty(asset);
                    else
                        EditorUtility.ClearDirty(asset);
                }
                catch (Exception recovery)
                {
                    throw new IOException(failure.Message + " File recovery also failed: " + recovery.Message, failure);
                }
                throw;
            }
        }

        #endregion
        #endregion
    }
}
