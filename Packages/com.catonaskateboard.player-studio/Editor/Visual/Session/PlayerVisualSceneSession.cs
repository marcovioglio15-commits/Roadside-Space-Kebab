using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Proposes scene binding changes without creating, moving or deleting the real visual.</summary>
    [Serializable]
    internal sealed class PlayerVisualSceneSession
    {
        #region Serialized State

        [Header("Context")]
        [Tooltip("Scene player whose visual binding can be changed by this proposal.")]
        [SerializeField]
        private PlayerHost host;

        [Tooltip("Master captured before proposing a scene binding change.")]
        [SerializeField]
        private PlayerMasterPreset originalMaster;

        [Tooltip("Applied Visual slot captured before editing the instance.")]
        [SerializeField]
        private PlayerVisualPreset originalPreset;

        [Tooltip("Whether the captured Visual slot existed before a possible external deletion.")]
        [SerializeField]
        private bool hadPreset;

        [Tooltip("Binding captured before editing, including a missing reference after deletion.")]
        [SerializeField]
        private PlayerVisualBinding originalBinding;

        [Tooltip("Whether a binding existed before external changes.")]
        [SerializeField]
        private bool hadBinding;

        [Tooltip("Binding fields captured to detect outside changes before Apply.")]
        [SerializeField]
        private string bindingStamp = string.Empty;

        [Tooltip("Model subtree captured to detect changes that a visual Apply would overwrite.")]
        [SerializeField]
        private string hierarchyStamp = string.Empty;

        [Header("Proposal")]
        [Tooltip("Enable managed offsets, or release management while preserving the model.")]
        [SerializeField]
        private bool managed;

        [Tooltip("Existing direct child to adopt when no source prefab is selected.")]
        [SerializeField]
        private GameObject existing;

        [Tooltip("Existing child identity captured when the scene session opened.")]
        [SerializeField]
        private GameObject originalExisting;

        [Tooltip("Pose of a newly chosen child before offsets are applied.")]
        [SerializeField]
        private Matrix4x4 adoptedMatrix = Matrix4x4.identity;

        [Tooltip("Newly chosen child's subtree captured before Apply.")]
        [SerializeField]
        private string adoptedStamp = string.Empty;

        [Tooltip("Explicit request to apply the current saved visual settings to this instance again.")]
        [SerializeField]
        private bool synchronize;

        [Header("Play Recovery")]
        [Tooltip("Saved player identity used only to restore scene references after Play.")]
        [SerializeField]
        private string hostIdentity;

        [Tooltip("Original binding identity retained while Play recreates scene objects.")]
        [SerializeField]
        private string bindingIdentity;

        [Tooltip("Applied model identity retained across a Play round trip.")]
        [SerializeField]
        private string originalExistingIdentity;

        [Tooltip("Proposed adoption target identity retained across a Play round trip.")]
        [SerializeField]
        private string existingIdentity;

        #endregion

        #region Properties

        /// <summary>The scene instance owned by this proposal.</summary>
        public PlayerHost Host => host;
        /// <summary>The original binding, never inferred from object names.</summary>
        public PlayerVisualBinding Binding => originalBinding;
        /// <summary>Whether the proposal manages a visual.</summary>
        public bool Managed => managed;
        /// <summary>The direct child proposed for adoption.</summary>
        public GameObject Existing => existing;
        /// <summary>Pending scene choices survive loss of their original references.</summary>
        public bool HasChanges => synchronize || managed != hadBinding || existing != originalExisting;
        /// <summary>Authored pose used to show adoption before a binding exists.</summary>
        public Matrix4x4 AdoptedMatrix => adoptedMatrix;

        #endregion

        #region Methods

        #region Draft

        /// <summary>Refreshes a clean context without abandoning a pending binding proposal.</summary>
        /// <param name="player">Matching selected scene player, or null for an asset-only route.</param>
        public void Refresh(PlayerHost player)
        {
            // A pending proposal keeps the original source even if it disappears externally.
            if (HasChanges)
                return;

            host = player;
            Discard();
        }

        /// <summary>Reloads binding references and baseline without changing the scene.</summary>
        public void Discard()
        {
            // Only this read path replaces the outside-edit baseline.
            originalMaster = host != null ? host.MasterPreset : null;
            originalPreset = originalMaster != null ? originalMaster.VisualPreset : null;
            hadPreset = originalPreset != null;
            originalBinding = host != null ? host.GetComponent<PlayerVisualBinding>() : null;
            hadBinding = originalBinding != null;
            managed = hadBinding;
            synchronize = false;
            originalExisting = hadBinding ? originalBinding.Model : null;
            bindingStamp = PlayerVisualModelValidation.CaptureBinding(originalBinding);
            hierarchyStamp = hadBinding && originalBinding.VisualRoot != null
                ? PlayerVisualModelValidation.Capture(originalBinding.VisualRoot.gameObject) : string.Empty;
            SetDraft(managed, originalExisting);
        }

        /// <summary>Changes the binding proposal after the window records Undo.</summary>
        /// <param name="enabled">Whether the visual will be managed after Apply.</param>
        /// <param name="child">Direct child to adopt when the preset has no prefab.</param>
        public void SetDraft(bool enabled, GameObject child)
        {
            // The scene is only read; adoption itself happens during the shared confirmation.
            managed = enabled;
            if (existing != child || !HasChanges)
            {
                existing = child;
                adoptedMatrix = child != null ? Matrix4x4.TRS(child.transform.localPosition,
                    child.transform.localRotation, child.transform.localScale) : Matrix4x4.identity;
                adoptedStamp = PlayerVisualModelValidation.Capture(child);
            }
        }

        /// <summary>Stages a deliberate reapplication after direct Inspector edits or loading another scene.</summary>
        public void RequestSynchronization()
        {
            // This remains a draft operation and participates in the window's Apply, Discard and Undo.
            synchronize = true;
        }

        #endregion

        #region Validation

        /// <summary>Checks captured references and subtree state before a binding or offset write.</summary>
        /// <param name="warning">Receives the first outside-edit or scene-context conflict.</param>
        /// <returns>True when the original scene state still matches this proposal.</returns>
        public bool TryValidate(out string warning)
        {
            // A lost or closed scene cannot redirect the operation to another object.
            warning = string.Empty;
            if (host == null || EditorUtility.IsPersistent(host) || !host.gameObject.scene.IsValid()
                || !host.gameObject.scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode)
                warning = "Visual scene editing requires its loaded player in Edit mode.";
            else if (host.MasterPreset != originalMaster || (hadPreset && originalPreset == null)
                || (originalMaster != null && originalMaster.VisualPreset != originalPreset))
                warning = "The player's master or Visual slot changed outside this session. Discard before applying its binding.";
            else if ((hadBinding && originalBinding == null) || host.GetComponent<PlayerVisualBinding>() != originalBinding
                || (hadBinding && (PlayerVisualModelValidation.CaptureBinding(originalBinding) != bindingStamp
                    || PlayerVisualModelValidation.Capture(originalBinding.VisualRoot != null ? originalBinding.VisualRoot.gameObject : null) != hierarchyStamp)))
                warning = "The managed visual changed outside this session. Discard to reload the current hierarchy.";
            else if (existing != originalExisting && (existing == null
                || PlayerVisualModelValidation.Capture(existing) != adoptedStamp))
                warning = "The proposed child changed outside this session. Discard and choose it again.";

            return warning.Length == 0;
        }

        #endregion

        #region Play Recovery

        /// <summary>Remembers references before Play without replacing the proposal or conflict baseline.</summary>
        public void CaptureSceneReferences()
        {
            // Capture before native objects can be replaced by Unity's Play transition.
            hostIdentity = PlayerStudioSceneReference.Capture(host);
            bindingIdentity = PlayerStudioSceneReference.Capture(originalBinding);
            originalExistingIdentity = PlayerStudioSceneReference.Capture(originalExisting);
            existingIdentity = PlayerStudioSceneReference.Capture(existing);
        }

        /// <summary>Reconnects saved scene objects after Play while preserving all pending values and baselines.</summary>
        public void RestoreSceneReferences()
        {
            // A missing identity stays missing, so Apply can report the conflict instead of redirecting it.
            if (host == null)
                host = PlayerStudioSceneReference.Resolve<PlayerHost>(hostIdentity);
            if (originalBinding == null)
                originalBinding = PlayerStudioSceneReference.Resolve<PlayerVisualBinding>(bindingIdentity);
            if (originalExisting == null)
                originalExisting = PlayerStudioSceneReference.Resolve<GameObject>(originalExistingIdentity);
            if (existing == null)
                existing = PlayerStudioSceneReference.Resolve<GameObject>(existingIdentity);
        }

        #endregion

        #endregion
    }
}
