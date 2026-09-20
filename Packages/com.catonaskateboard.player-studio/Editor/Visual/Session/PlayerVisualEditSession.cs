using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Retains a Visual asset draft and checks its original source immediately before Apply.</summary>
    [Serializable]
    internal sealed class PlayerVisualEditSession
    {
        #region Serialized State

        [Header("Source")]
        [Tooltip("Applied Visual slot whose contents are being edited.")]
        [SerializeField]
        private PlayerVisualPreset source;

        [Header("Baseline and Draft")]
        [Tooltip("Raw values captured before editing; used to reject outside changes.")]
        [SerializeField]
        private PlayerVisualDraft baseline;

        [Tooltip("Unconfirmed source and offset, retained independently of tab visibility.")]
        [SerializeField]
        private PlayerVisualDraft draft;

        #endregion

        #region Properties

        /// <summary>The only preset that can receive these edits.</summary>
        public PlayerVisualPreset Source => source;
        /// <summary>Value snapshot exposed to controls and preview.</summary>
        public PlayerVisualDraft Draft => draft;
        /// <summary>Pending values survive missing assets and hidden tabs.</summary>
        public bool HasChanges => !draft.Matches(baseline);

        #endregion

        #region Methods

        #region Draft

        /// <summary>Follows the applied slot only while doing so cannot overwrite pending input.</summary>
        /// <param name="master">Selected master, or null for direct Body editing.</param>
        public void Refresh(PlayerMasterPreset master)
        {
            // Outside slot changes leave an unfinished proposal bound to its original asset.
            if (HasChanges)
                return;

            source = master != null ? master.VisualPreset : null;
            Discard();
        }

        /// <summary>Reloads raw asset values without writing over outside edits.</summary>
        public void Discard()
        {
            // A deleted source clears this draft only after an explicit discard.
            baseline = default;
            if (source != null)
            {
                using SerializedObject serialized = new SerializedObject(source);
                baseline = PlayerVisualDraft.Read(serialized);
            }
            draft = baseline;
        }

        /// <summary>Updates the proposal after its owning window has recorded Undo.</summary>
        /// <param name="value">Raw source and offset proposal.</param>
        public void SetDraft(PlayerVisualDraft value)
        {
            // Only the serialized session changes; the asset remains untouched.
            draft = value;
        }

        #endregion

        #region Confirmation

        /// <summary>Rejects lost sources, redirected slots and outside edits before preparing any write.</summary>
        /// <param name="master">Master that must still identify the opened Visual preset.</param>
        /// <param name="changes">Receives properties for the shared save path, or null when clean.</param>
        /// <param name="warning">Receives a conflict or configuration warning.</param>
        /// <returns>True when the draft can join the session confirmation.</returns>
        public bool TryPrepareApply(PlayerMasterPreset master, out SerializedObject changes, out string warning)
        {
            // A restored draft cannot write to a different slot or during Play.
            changes = null;
            warning = string.Empty;
            if (EditorApplication.isPlayingOrWillChangePlaymode || master == null || source == null
                || master.VisualPreset != source || !EditorUtility.IsPersistent(source))
            {
                warning = "The Visual slot is unavailable or changed. Discard to reload its current source in Edit mode.";
                return false;
            }

            if (!draft.TryGetSettings(out _, out warning) || !HasChanges)
                return warning.Length == 0;

            // Compare raw values rather than converted rotations, which would hide full-turn edits.
            SerializedObject serialized = new SerializedObject(source);
            if (!PlayerVisualDraft.Read(serialized).Matches(baseline))
            {
                serialized.Dispose();
                warning = "The Visual preset changed outside this session. Discard to reload its current values.";
                return false;
            }

            draft.Write(serialized);
            changes = serialized;
            return true;
        }

        #endregion

        #endregion
    }
}
