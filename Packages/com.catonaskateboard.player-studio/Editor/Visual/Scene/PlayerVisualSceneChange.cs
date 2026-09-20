using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Validates all affected loaded visual bindings before any preset, hierarchy or pose is changed.</summary>
    internal sealed class PlayerVisualSceneChange
    {
        #region State

        private readonly PlayerTransformEditSession transformSession;
        private readonly List<PlayerVisualSceneOperation> operations = new List<PlayerVisualSceneOperation>();
        private readonly HashSet<PlayerHost> prefabTargets = new HashSet<PlayerHost>();

        #endregion

        #region Methods

        #region Preparation

        /// <summary>Retains the simultaneous root proposal for visual composition validation.</summary>
        /// <param name="transformSession">Root pose included in this same Apply request.</param>
        private PlayerVisualSceneChange(PlayerTransformEditSession transformSession)
        {
            // The candidate is read only during this synchronous confirmation.
            this.transformSession = transformSession;
        }

        /// <summary>Builds scene operations for the selected proposal and any other loaded users of the same preset.</summary>
        /// <param name="preset">Unapplied preset values, or null for scene-only editing.</param>
        /// <param name="session">Selected player's binding proposal; may be null for other callers.</param>
        /// <param name="transformSession">Root pose being confirmed with these visual edits.</param>
        /// <param name="change">Receives the complete plan only when every operation is compatible.</param>
        /// <param name="warning">Receives the first scene incompatibility without partial changes.</param>
        /// <returns>True when visual scene writes are ready to join the shared Undo group.</returns>
        public static bool TryPrepare(PlayerPresetBatch preset, PlayerVisualSceneSession session, PlayerTransformEditSession transformSession,
            out PlayerVisualSceneChange change, out string warning)
        {
            // Validation is event-driven: discovery and ownership scans happen only when Apply is requested.
            change = new PlayerVisualSceneChange(transformSession);
            warning = string.Empty;
            bool sceneDraft = session != null && session.HasChanges;
            bool visualPreset = preset.Items.Count > 0;
            if (!sceneDraft && !visualPreset)
                return true;

            if (session != null && (sceneDraft || (session.Host != null && IsAffected(session.Host, preset)))
                && !session.TryValidate(out warning))
                return false;

            // An explicit scene choice takes precedence for the selected instance only.
            if (sceneDraft && !change.TryAdd(session.Host, session.Host.GetComponent<PlayerVisualBinding>(),
                session.Managed, session.Existing, preset, out warning))
                return false;

            if (visualPreset)
                foreach (PlayerHost host in Object.FindObjectsByType<PlayerHost>(FindObjectsInactive.Include))
                {
                    if ((sceneDraft && host == session.Host) || !IsAffected(host, preset))
                        continue;

                    PlayerVisualBinding binding = host.GetComponent<PlayerVisualBinding>();
                    if (!change.TryAdd(host, binding, true, binding != null ? binding.Model : null, preset, out warning))
                        return false;
                }
            return true;
        }

        /// <summary>Limits propagation to loaded users of the edited Visual asset or changed master slot.</summary>
        /// <param name="host">Potential affected scene player.</param>
        /// <param name="preset">Candidate asset edit.</param>
        /// <returns>True only when this edit changes that player's visual configuration.</returns>
        private static bool IsAffected(PlayerHost host, PlayerPresetBatch preset)
        {
            // Body-only slot edits must not unexpectedly rewrite a visual pose.
            if (preset == null || host.MasterPreset == null || EditorUtility.IsPersistent(host)
                || !host.gameObject.scene.IsValid() || !host.gameObject.scene.isLoaded
                || (EditorSceneManager.IsPreviewScene(host.gameObject.scene)
                    && PrefabStageUtility.GetCurrentPrefabStage()?.scene != host.gameObject.scene))
                return false;

            return preset.Affects(host.MasterPreset, "visualPreset", host.MasterPreset.VisualPreset);
        }

        /// <summary>Resolves candidate data and validates one binding before adding its operation.</summary>
        /// <param name="host">Player receiving this operation.</param>
        /// <param name="binding">Current binding, or null for first-time creation.</param>
        /// <param name="managed">Whether the proposal retains management.</param>
        /// <param name="existing">Child proposed for adoption.</param>
        /// <param name="preset">Candidate asset edit included in the same confirmation.</param>
        /// <param name="warning">Receives a contextual validation warning.</param>
        /// <returns>True when this operation can execute without losing unrelated content.</returns>
        private bool TryAdd(PlayerHost host, PlayerVisualBinding binding, bool managed, GameObject existing,
            PlayerPresetBatch preset, out string warning)
        {
            // Check context and ownership even when releasing a visual.
            warning = string.Empty;
            if (host == null || host.MasterPreset == null || EditorUtility.IsPersistent(host)
                || !host.gameObject.scene.IsValid() || !host.gameObject.scene.isLoaded)
            {
                warning = "Choose a loaded scene player with an applied master before managing its visual.";
                return false;
            }
            if (!PlayerVisualPrefabUtility.TryGetPath(host, out string prefabPath, out warning))
                return false;

            PlayerVisualPreset source = preset.Slot(host.MasterPreset, "visualPreset", host.MasterPreset.VisualPreset);
            if (source == null)
                managed = false;

            bool removeModel = source == null && binding != null && binding.SourcePrefab != null;
            PlayerVisualSettings settings = default;
            if (managed || (source != null && binding != null && binding.SourcePrefab != null))
            {
                if (source == null)
                {
                    warning = "Assign and apply a Visual Slot before enabling visual management.";
                    return false;
                }

                // Read pending Visual values directly when that asset joins this Apply.
                SerializedObject proposed = preset.Find(source);
                if (proposed != null)
                {
                    if (!PlayerVisualDraft.Read(proposed).TryGetSettings(out settings, out warning))
                        return false;
                }
                else
                {
                    using SerializedObject serialized = new SerializedObject(source);
                    if (!PlayerVisualDraft.Read(serialized).TryGetSettings(out settings, out warning))
                        return false;
                }

                if (settings.Prefab == null && binding == null && existing == null)
                    return true;

                if (settings.Prefab == null && binding != null && binding.SourcePrefab != null)
                {
                    managed = false;
                    removeModel = true;
                }
            }

            bool emptyBinding = binding != null && binding.Host == host && binding.VisualRoot == null && binding.Model == null;
            if (binding != null && !emptyBinding
                && !PlayerVisualModelValidation.TryValidateBinding(binding, out warning))
                return false;
            if (removeModel && binding.Model != null && !PlayerVisualModelValidation.TryValidateRemoval(binding, out warning))
                return false;

            if (managed)
            {
                // Existing bindings retain identity; choosing a different child requires an explicit release first.
                if (binding != null && existing != binding.Model)
                {
                    warning = "Release the current visual before adopting a different child. Release preserves the old model.";
                    return false;
                }
                if (settings.Prefab == null && (binding == null || emptyBinding) && (existing == null || existing.transform.parent != host.transform))
                {
                    warning = "Choose a direct child of this player for adoption.";
                    return false;
                }
                if (!PlayerVisualModelValidation.TryValidate(settings.Prefab != null ? settings.Prefab : existing, out warning))
                    return false;
                if (binding != null && !emptyBinding && (settings.Prefab == null || settings.Prefab == binding.SourcePrefab)
                    && !PlayerVisualModelValidation.TryValidate(binding.Model, out warning))
                    return false;
                if (binding != null && !emptyBinding && settings.Prefab != null && settings.Prefab != binding.SourcePrefab
                    && !PlayerVisualModelValidation.TryValidateReplacement(binding, out warning))
                    return false;
            }

            if (managed)
            {
                // Validate the composed pose against the simultaneous root draft before writing either transform.
                Matrix4x4 frame = transformSession.Source == host && transformSession.HasChanges
                    ? transformSession.WorldMatrix : host.transform.localToWorldMatrix;
                Matrix4x4 authored = binding != null && !emptyBinding ? binding.BaseMatrix : settings.Prefab != null ? Matrix4x4.TRS(settings.Prefab.transform.localPosition, settings.Prefab.transform.localRotation, settings.Prefab.transform.localScale)
                    : Matrix4x4.TRS(existing.transform.localPosition, existing.transform.localRotation, existing.transform.localScale);
                Matrix4x4 matrix = frame * Matrix4x4.TRS(settings.Position, settings.Rotation, Vector3.one * settings.Scale) * authored;
                if (!PlayerVisualModelValidation.TryValidateMatrix(matrix, out warning))
                    return false;
            }

            PlayerHost prefabSource = prefabPath.Length > 0 ? PrefabUtility.GetCorrespondingObjectFromSource(host) : null;
            if (prefabSource == null || prefabTargets.Add(prefabSource))
                operations.Add(new PlayerVisualSceneOperation(host, binding, settings, existing, managed, removeModel, prefabPath));
            return true;
        }

        #endregion

        #region Application

        /// <summary>Runs the fully validated plan inside the save coordinator's Undo and rollback boundary.</summary>
        public void Apply()
        {
            // No operation runs until the coordinator has validated every affected preset and scene object.
            foreach (PlayerVisualSceneOperation operation in operations)
                operation.Apply();
        }

        #endregion

        #endregion
    }
}
