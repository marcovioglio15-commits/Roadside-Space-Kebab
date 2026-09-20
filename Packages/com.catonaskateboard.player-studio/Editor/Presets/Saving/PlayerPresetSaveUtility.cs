using System;
using System.IO;
using UnityEditor;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Confirms the preset, affected controllers and optional Transform draft in one Undo group.</summary>
    internal static class PlayerPresetSaveUtility
    {
        #region Methods

        #region Saving

        /// <summary>Confirms all prepared modules and scene proposals within one validation and Undo boundary.</summary>
        /// <param name="batch">Independent unapplied properties for every changed preset.</param>
        /// <param name="transformSession">Root pose proposal.</param>
        /// <param name="visualSession">Selected visual binding proposal.</param>
        /// <param name="cameraSession">Selected camera binding proposal.</param>
        /// <param name="warning">Receives a validation or save failure while retaining the drafts.</param>
        /// <returns>True after all preset writes and scene changes have completed.</returns>
        public static bool TrySaveBatch(PlayerPresetBatch batch, PlayerTransformEditSession transformSession,
            PlayerVisualSceneSession visualSession, PlayerCameraSceneSession cameraSession, out string warning)
        {
            // The caller validates the draft and baseline before preparing these properties.
            warning = string.Empty;
            foreach (SerializedObject serialized in batch.Items)
                if (!AssetDatabase.IsOpenForEdit(serialized.targetObject)
                    || (File.GetAttributes(AssetDatabase.GetAssetPath(serialized.targetObject)) & FileAttributes.ReadOnly) != 0)
                {
                    warning = "A preset is not available for editing. Check its file or version control status.";
                    return false;
                }

            // Reject incompatible scene instances before saving the preset or changing any capsule.
            if (!PlayerPrefabConfiguration.TryPrepare(batch, visualSession, cameraSession, out PlayerPrefabConfiguration prefabChange, out warning)
                || !transformSession.TryValidate(null, out warning, batch)
                || !PlayerBodySceneChange.TryPrepare(batch, transformSession, out PlayerBodySceneChange sceneChange, out warning)
                || !PlayerVisualSceneChange.TryPrepare(batch, visualSession, transformSession, out PlayerVisualSceneChange visualChange, out warning)
                || !PlayerCameraSceneChange.TryPrepare(batch, cameraSession, visualSession, out PlayerCameraSceneChange cameraChange, out warning))
                return false;

            // Isolate this asset edit from previous and subsequent Undo operations.
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            PlayerPresetSaveSnapshot snapshot = null;
            try
            {
                // File rollback complements native Undo if a later prefab or preset save fails.
                snapshot = new PlayerPresetSaveSnapshot(batch);
                prefabChange.CaptureFiles(snapshot);
                // The pose is confirmed before configuring bodies against their validated final pose.
                Undo.SetCurrentGroupName("Apply Player Session");
                transformSession.ApplyValidated();
                foreach (SerializedObject serialized in batch.Items)
                    serialized.ApplyModifiedProperties();
                prefabChange.Configure();
                sceneChange?.Apply();
                visualChange.Apply();
                cameraChange.Apply();
                // Finish deferred scene snapshots before prefab application remaps component references.
                Undo.FlushUndoRecordObjects();
                prefabChange.Save(snapshot);
                foreach (SerializedObject serialized in batch.Items)
                {
                    AssetDatabase.SaveAssetIfDirty(serialized.targetObject);
                    if (EditorUtility.IsDirty(serialized.targetObject))
                        throw new IOException("Unity could not finish saving the preset.");
                }

                Undo.CollapseUndoOperations(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                // Restore the in-memory preset and scene snapshots while the caller retains its draft.
                Undo.RevertAllDownToGroup(undoGroup);
                warning = "Session was not applied: " + exception.Message;
                try
                {
                    snapshot?.Restore();
                }
                catch (Exception recovery)
                {
                    warning += " File recovery also failed: " + recovery.Message;
                }
                return false;
            }
            finally
            {
                // A later Inspector edit must not join this save's Undo group.
                Undo.IncrementCurrentGroup();
            }
        }

        #endregion

        #endregion
    }
}
