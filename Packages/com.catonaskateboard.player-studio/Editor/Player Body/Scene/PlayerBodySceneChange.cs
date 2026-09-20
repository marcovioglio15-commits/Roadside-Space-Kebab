using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Prepares matching scene controllers before a preset save and updates them in the same Undo group.</summary>
    internal sealed class PlayerBodySceneChange
    {
        #region State

        private readonly List<(PlayerHost Host, PlayerBodySettings Settings)> operations = new List<(PlayerHost, PlayerBodySettings)>();

        #endregion

        #region Methods

        #region Preparation

        /// <summary>Checks all loaded scene instances before the preset or any controller is changed.</summary>
        /// <param name="serialized">Preset view containing the proposed Body dimensions or master slot.</param>
        /// <param name="transformSession">Optional pose proposal included in the same confirmation.</param>
        /// <param name="change">Receives the prepared scene changes, including an empty plan when no player matches.</param>
        /// <param name="warning">Receives a candidate or instance incompatibility without changing data.</param>
        /// <returns>True when every affected native controller accepts the candidate Body.</returns>
        public static bool TryPrepare(PlayerPresetBatch serialized, PlayerTransformEditSession transformSession,
            out PlayerBodySceneChange change, out string warning)
        {
            // Read candidate properties before ApplyModifiedProperties changes the shared asset.
            change = new PlayerBodySceneChange();
            warning = string.Empty;
            foreach (PlayerHost host in UnityEngine.Object.FindObjectsByType<PlayerHost>(FindObjectsInactive.Include))
            {
                // A shared Body affects all masters using it; a slot assignment affects only its master.
                if (!IsAffected(host, serialized))
                    continue;
                if (!TryReadCandidate(serialized, host.MasterPreset, out PlayerBodySettings settings, out warning)
                    || !transformSession.TryValidateBody(host, settings, out warning))
                {
                    warning = host.gameObject.scene.name + " / " + host.name + ": " + warning;
                    change = null;
                    return false;
                }

                change.operations.Add((host, settings));
            }

            return true;
        }

        /// <summary>Combines a pending Body slot with the draft on that selected asset.</summary>
        /// <param name="batch">All properties joining the confirmation.</param>
        /// <param name="master">Master whose resulting body is requested.</param>
        /// <param name="settings">Receives the proposed collision geometry.</param>
        /// <param name="warning">Receives a missing or invalid body warning.</param>
        /// <returns>True when the combined body proposal is valid.</returns>
        internal static bool TryReadCandidate(PlayerPresetBatch batch, PlayerMasterPreset master,
            out PlayerBodySettings settings, out string warning)
        {
            // The slot is resolved before its asset values, so replacement and editing can share Apply.
            PlayerBodyPreset body = batch.Slot(master, "bodyPreset", master.BodyPreset);
            settings = default;
            warning = "Assign a Body preset before applying this master.";
            if (body == null)
                return false;
            SerializedObject candidate = batch.Find(body);
            return candidate != null ? TryReadCandidate(candidate, out settings, out warning)
                : body.TryGetSettings(out settings, out warning);
        }

        /// <summary>Reads only the geometry proposed by the two supported preset edits.</summary>
        /// <param name="serialized">Pending Body or master properties, not yet applied to their asset.</param>
        /// <param name="settings">Receives valid candidate geometry.</param>
        /// <param name="warning">Receives a missing-slot or geometry warning.</param>
        /// <returns>True when the proposed preset edit supplies a usable Body.</returns>
        internal static bool TryReadCandidate(SerializedObject serialized, out PlayerBodySettings settings, out string warning)
        {
            // Keep asset field names at this serialized save boundary.
            settings = default;
            switch (serialized.targetObject)
            {
                case PlayerBodyPreset:
                    return PlayerBodySettings.TryCreate(serialized.FindProperty("radius").floatValue,
                        serialized.FindProperty("height").floatValue, out settings, out warning);
                case PlayerMasterPreset:
                    if (serialized.FindProperty("bodyPreset").objectReferenceValue is PlayerBodyPreset body)
                        return body.TryGetSettings(out settings, out warning);

                    warning = "Assign a saved Body before applying this master.";
                    return false;
                default:
                    warning = "Scene synchronization requires a Body or master preset.";
                    return false;
            }
        }

        /// <summary>Limits synchronization to matching players in loaded scenes or the currently open prefab.</summary>
        /// <param name="host">Potential scene instance, including inactive objects.</param>
        /// <param name="asset">Body being edited or master receiving a new slot.</param>
        /// <returns>True when this native binding is part of the confirmed edit.</returns>
        private static bool IsAffected(PlayerHost host, PlayerPresetBatch asset)
        {
            // Exclude asset contents and unrelated temporary preview scenes.
            if (EditorUtility.IsPersistent(host) || !host.gameObject.scene.IsValid() || !host.gameObject.scene.isLoaded
                || host.BodyBinding != PlayerBodyBinding.CharacterController || host.MasterPreset == null)
                return false;

            if (EditorSceneManager.IsPreviewScene(host.gameObject.scene)
                && (PrefabStageUtility.GetCurrentPrefabStage() == null
                    || PrefabStageUtility.GetCurrentPrefabStage().scene != host.gameObject.scene))
                return false;

            return asset.Affects(host.MasterPreset, "bodyPreset", host.MasterPreset.BodyPreset);
        }

        #endregion

        #region Application

        /// <summary>Changes native geometry after asset application, using the caller's active Undo group.</summary>
        public void Apply()
        {
            // Complete snapshots also allow synchronous rollback if a later controller or save fails.
            foreach ((PlayerHost Host, PlayerBodySettings Settings) operation in operations)
            {
                PlayerHost host = operation.Host;
                Undo.RegisterCompleteObjectUndo(host.BodyController, "Apply Player Body");
                if (!PlayerCharacterControllerBody.TryConfigure(host.BodyController, host.transform, operation.Settings, out string warning))
                    throw new InvalidOperationException(warning);

                // Native setters need explicit prefab override recording after the write.
                if (PrefabUtility.IsPartOfPrefabInstance(host.BodyController))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(host.BodyController);
            }

            SceneView.RepaintAll();
        }

        #endregion

        #endregion
    }
}
