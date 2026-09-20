using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Copies the same combined proposal validated by Apply into assets owned only by a test run.</summary>
    internal static class PlayerQuickPlayConfiguration
    {
        #region Methods

        #region Snapshot

        /// <summary>Copies selected slots and their pending module values without confirming the original assets.</summary>
        /// <param name="state">Workspace whose drafts and source assets remain unchanged.</param>
        /// <param name="folder">Temporary folder owned by this Quick Play run.</param>
        /// <returns>A complete independent master with copied action bindings.</returns>
        internal static PlayerMasterPreset Create(PlayerStudioState state, string folder)
        {
            // Apply and Quick Play must evaluate the same identities, conflicts and cross-module dependencies.
            if (state.Selection.Master == null)
                throw new InvalidOperationException("Choose a master configuration before Quick Play.");
            using PlayerPresetBatch batch = new PlayerPresetBatch();
            if (!batch.TryPrepare(state, out string warning) || !batch.TryValidate(state.Selection.Master, out warning))
                throw new InvalidOperationException(warning);
            if (state.PreviewHost != null && (!state.Transform.TryValidate(null, out warning, batch)
                || !state.Transform.TryValidateNumbers(out warning)))
                throw new InvalidOperationException(warning);
            PlayerMasterPreset candidate = UnityEngine.Object.Instantiate(state.Selection.Master);
            try
            {
                PlayerPresetDraftCopy.Apply(batch.Find(state.Selection.Master), candidate);
                PlayerMasterPreset master = PlayerConfigurationCopy.Copy(candidate, folder, false);
                using SerializedObject source = new SerializedObject(candidate);
                using SerializedObject copy = new SerializedObject(master);
                foreach (string slot in new[] { "bodyPreset", "inputPreset", "locomotionPreset", "visualPreset", "cameraPreset" })
                {
                    // A draft on a replaced slot remains pending but is not used by the proposed test configuration.
                    ScriptableObject original = source.FindProperty(slot).objectReferenceValue as ScriptableObject;
                    ScriptableObject destination = copy.FindProperty(slot).objectReferenceValue as ScriptableObject;
                    if (original == null || destination == null)
                        continue;
                    PlayerPresetDraftCopy.Apply(batch.Find(original), destination);
                    AssetDatabase.SaveAssetIfDirty(destination);
                }
                if (master.InputPreset != null)
                    PlayerConfigurationCopy.CopyActions(master.InputPreset, folder);
                AssetDatabase.SaveAssetIfDirty(master);
                return master;
            }
            finally
            {
                // The candidate carries only slot selection and never enters the Asset Database.
                UnityEngine.Object.DestroyImmediate(candidate);
            }
        }

        #endregion

        #endregion
    }
}
