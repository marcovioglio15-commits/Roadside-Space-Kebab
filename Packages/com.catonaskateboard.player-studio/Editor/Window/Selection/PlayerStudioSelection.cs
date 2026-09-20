using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Keeps the editing route stable while preset sessions prepare their drafts for shared confirmation.</summary>
    [Serializable]
    internal sealed class PlayerStudioSelection
    {
        #region Serialized State

        [Header("Source")]
        [Tooltip("Whether to open a Body directly or resolve it through a master.")]
        [SerializeField]
        private PlayerStudioSourceMode mode = PlayerStudioSourceMode.Master;

        [Tooltip("Master whose active Body was opened. Apply checks that the slot still points to that Body.")]
        [SerializeField]
        private PlayerMasterPreset master;

        [Tooltip("Pending Body assignment, kept separate from the Body dimension draft.")]
        [SerializeField]
        private PlayerMasterEditSession masterSession = new PlayerMasterEditSession();

        #endregion

        #region Properties

        /// <summary>The route currently used to select the Body.</summary>
        public PlayerStudioSourceMode Mode => mode;

        /// <summary>The selected master, or null when editing a Body directly.</summary>
        public PlayerMasterPreset Master => master;

        /// <summary>The master slot draft; dimensions remain owned by the separate Body session.</summary>
        public PlayerMasterEditSession MasterSession => masterSession;

        #endregion

        #region Methods

        #region Selection

        /// <summary>Changes the route only after the Body session accepts the new persistent target.</summary>
        /// <param name="newMode">Requested route to the editing target.</param>
        /// <param name="newMaster">Master to resolve when the requested route uses a master.</param>
        /// <param name="newBody">Body to open when the requested route uses a Body directly.</param>
        /// <param name="session">Existing draft whose pending edits must be preserved.</param>
        /// <param name="warning">Receives the reason selection was refused.</param>
        /// <returns>True when both the selection and its Body target have been updated.</returns>
        public bool TrySelect(PlayerStudioSourceMode newMode, PlayerMasterPreset newMaster, PlayerBodyPreset newBody, PlayerBodyEditSession session, out string warning)
        {
            // A source switch must not replace either kind of pending edit.
            warning = string.Empty;
            if (HasChanges(session))
            {
                warning = "Apply or discard the current draft before changing the source.";
                return false;
            }

            // Resolve only the reference; invalid dimensions remain editable in the draft.
            switch (newMode)
            {
                case PlayerStudioSourceMode.Body:
                    newMaster = null;
                    break;
                case PlayerStudioSourceMode.Master:
                    if (newMaster != null && !EditorUtility.IsPersistent(newMaster))
                    {
                        warning = "Select a master saved in the Project window.";
                        return false;
                    }

                    newBody = newMaster != null ? newMaster.BodyPreset : null;
                    break;
                default:
                    warning = "Select a supported editing source.";
                    return false;
            }

            // The Body session rejects a switch with pending edits or a temporary target.
            if (!session.TryOpen(newBody, out warning))
                return false;

            // Commit the route only after opening succeeded, so a refusal changes neither.
            mode = newMode;
            master = newMaster;
            masterSession.Open(master);
            return true;
        }

        /// <summary>Reports either pending operation without treating panel visibility as a data change.</summary>
        /// <param name="session">Body dimension session paired with this selection.</param>
        /// <returns>True when dimensions or the master assignment are pending.</returns>
        public bool HasChanges(PlayerBodyEditSession session)
        {
            // The window uses one pending indicator and one close-save path for both operations.
            return session.HasChanges || masterSession.HasChanges;
        }

        /// <summary>Checks the master still identifies the draft's target before showing or applying it.</summary>
        /// <param name="session">Body draft to compare with the current master slot.</param>
        /// <param name="warning">Receives a missing-source or changed-slot warning.</param>
        /// <returns>True when the selected route still leads to an existing draft target.</returns>
        public bool TryValidateTarget(PlayerBodyEditSession session, out string warning)
        {
            // Compare asset identity even when both Bodies contain identical dimensions.
            warning = string.Empty;
            if (mode == PlayerStudioSourceMode.Master)
            {
                if (master == null)
                    warning = "The master is unavailable. Discard the draft before choosing another source.";
                else if (master.BodyPreset != session.Source)
                    warning = "The master's Body slot changed. Discard to load its current Body; the draft is retained until then.";
            }

            // An empty or deleted Body cannot receive an Apply through either route.
            if (warning.Length == 0 && session.Source == null)
                warning = "No Body is available. Assign one in the master Inspector, or select a Body directly.";

            return warning.Length == 0;
        }

        #endregion

        #region Synchronization

        /// <summary>Reloads the current route only when doing so cannot replace pending edits.</summary>
        /// <param name="session">Clean drafts follow current assets; dirty drafts remain untouched.</param>
        /// <param name="warning">Receives any target-opening warning.</param>
        public void Refresh(PlayerBodyEditSession session, out string warning)
        {
            // Focus, project changes and Undo use this same non-destructive refresh.
            warning = string.Empty;
            if (!masterSession.HasChanges && masterSession.Source != master)
                masterSession.Open(master);

            if (!HasChanges(session))
                TrySelect(mode, master, session.Source, session, out warning);
        }

        /// <summary>Abandons the Body draft and resolves the master's current slot, without writing assets.</summary>
        /// <param name="session">Draft to discard and reopen through the current route.</param>
        /// <param name="warning">Receives any warning from reopening the current target.</param>
        public void Discard(PlayerBodyEditSession session, out string warning)
        {
            // Clear pending dimensions before following a slot changed outside this window.
            session.Discard();
            masterSession.Discard();
            Refresh(session, out warning);
        }

        #endregion

        #endregion
    }
}
