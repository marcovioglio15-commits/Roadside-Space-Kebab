using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Collects independent preset drafts and resolves their combined configuration before any writes.</summary>
    internal sealed class PlayerPresetBatch : IDisposable
    {
        #region State

        private readonly List<SerializedObject> items = new List<SerializedObject>();

        #endregion

        #region Properties

        /// <summary>Prepared asset properties owned by this synchronous confirmation.</summary>
        internal IReadOnlyList<SerializedObject> Items => items;

        #endregion

        #region Methods

        #region Collection

        /// <summary>Adds a prepared asset only when it contains an actual draft.</summary>
        /// <param name="preset">Optional unapplied properties.</param>
        private void Add(SerializedObject preset)
        {
            // Clean sessions return null rather than a redundant asset operation.
            if (preset != null)
                items.Add(preset);
        }

        /// <summary>Prepares every dirty module without requiring other tabs to Apply first.</summary>
        /// <param name="state">Workspace with independently retained drafts.</param>
        /// <param name="warning">Receives the first conflict, retaining every draft.</param>
        /// <returns>True when all asset baselines and raw values are valid.</returns>
        internal bool TryPrepare(PlayerStudioState state, out string warning)
        {
            // Each module keeps its original destination even while other modules are edited.
            warning = string.Empty;
            if (state.Selection.MasterSession.HasChanges)
            {
                if (!state.Selection.MasterSession.TryPrepareApply(out SerializedObject master, out warning))
                    return false;
                Add(master);
            }
            if (state.Body.HasChanges)
            {
                if (!state.Selection.TryValidateTarget(state.Body, out warning)
                    || !state.Body.TryPrepareApply(out SerializedObject body, out warning))
                    return false;
                Add(body);
            }
            if (state.Locomotion.HasChanges)
            {
                if (!state.Locomotion.TryPrepareApply(state.Selection.Master, out SerializedObject locomotion, out warning))
                    return false;
                Add(locomotion);
            }
            if (state.Visual.HasChanges)
            {
                if (!state.Visual.TryPrepareApply(state.Selection.Master, out SerializedObject visual, out warning))
                    return false;
                Add(visual);
            }
            if (!state.Input.TryPrepare(state.Selection.Master != null ? state.Selection.Master.InputPreset : null,
                    out SerializedObject input, out warning))
                return false;
            Add(input);
            if (!state.Camera.TryPrepare(state.Selection.Master != null ? state.Selection.Master.CameraPreset : null,
                    out SerializedObject camera, out warning))
                return false;
            Add(camera);
            return true;
        }

        /// <summary>Releases only properties allocated for this batch.</summary>
        public void Dispose()
        {
            // Prepared data must not survive its conflict checks into another Editor event.
            foreach (SerializedObject item in items)
                item.Dispose();
            items.Clear();
        }

        #endregion

        #region Resolution

        /// <summary>Finds the pending properties for one persistent asset.</summary>
        /// <param name="asset">Original asset identity.</param>
        /// <returns>Its prepared properties, or null when unchanged.</returns>
        internal SerializedObject Find(UnityEngine.Object asset)
        {
            // The batch is small and only queried at explicit validation boundaries.
            foreach (SerializedObject item in items)
                if (item.targetObject == asset)
                    return item;
            return null;
        }

        /// <summary>Reads a proposed master slot while retaining unchanged references.</summary>
        /// <typeparam name="T">Preset type expected by the slot.</typeparam>
        /// <param name="master">Original master.</param>
        /// <param name="slot">Serialized slot name.</param>
        /// <param name="applied">Currently assigned module.</param>
        /// <returns>The proposed or unchanged module asset.</returns>
        internal T Slot<T>(PlayerMasterPreset master, string slot, T applied) where T : ScriptableObject
        {
            // A deliberately cleared slot must remain null.
            SerializedObject candidate = Find(master);
            return candidate != null ? candidate.FindProperty(slot).objectReferenceValue as T : applied;
        }

        /// <summary>Tests whether a slot or its selected module has pending changes.</summary>
        /// <typeparam name="T">Preset type stored in this slot.</typeparam>
        /// <param name="master">Original master identity.</param>
        /// <param name="slot">Serialized slot name.</param>
        /// <param name="applied">Currently assigned module.</param>
        /// <returns>True when this batch affects the resulting module configuration.</returns>
        internal bool Affects<T>(PlayerMasterPreset master, string slot, T applied) where T : ScriptableObject
        {
            // A module no longer used by this player may still be saved for other users of that asset.
            T proposed = Slot(master, slot, applied);
            return proposed != applied || proposed != null && Find(proposed) != null;
        }

        /// <summary>Validates a complete proposed master using disposable copies of every changed module.</summary>
        /// <param name="master">Persistent master to evaluate.</param>
        /// <param name="warning">Receives an incompatible combination of settings.</param>
        /// <returns>True when all modules work together after this batch.</returns>
        internal bool TryValidate(PlayerMasterPreset master, out string warning)
        {
            // No shared asset is temporarily overwritten during combined validation.
            PlayerMasterPreset candidate = UnityEngine.Object.Instantiate(master);
            List<ScriptableObject> copies = new List<ScriptableObject>();
            try
            {
                PlayerPresetDraftCopy.Apply(Find(master), candidate);
                using SerializedObject slots = new SerializedObject(candidate);
                foreach (string slot in new[] { "bodyPreset", "inputPreset", "locomotionPreset", "visualPreset", "cameraPreset" })
                {
                    SerializedProperty property = slots.FindProperty(slot);
                    if (!(property.objectReferenceValue is ScriptableObject source) || Find(source) == null)
                        continue;
                    ScriptableObject copy = UnityEngine.Object.Instantiate(source);
                    copies.Add(copy);
                    PlayerPresetDraftCopy.Apply(Find(source), copy);
                    property.objectReferenceValue = copy;
                }
                slots.ApplyModifiedPropertiesWithoutUndo();
                return PlayerCreationUtility.TryValidate(candidate, out _, out warning, false);
            }
            finally
            {
                // Release candidates even when validation rejects a cross-module dependency.
                foreach (ScriptableObject copy in copies)
                    UnityEngine.Object.DestroyImmediate(copy);
                UnityEngine.Object.DestroyImmediate(candidate);
            }
        }

        #endregion

        #endregion
    }
}
