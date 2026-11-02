using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares range, input, replay and presentation settings between a dialogue and optional branch overrides.</summary>
    [Serializable]
    public class DialoguePolicy
    {
        #region Fields
        [Header("Range")]
        [Tooltip("Default player distance in metres for starting or resuming an entry.")]
        [Range(0.1f, 20f)]
        public float Distance = 3f;
        [Tooltip("Default distance in metres that interrupts a page. Must be at least Distance.")]
        [Range(0.1f, 25f)]
        public float ExitDistance = 4f;
        [Tooltip("Higher values win when eligible dialogues compete for the shared HUD.")]
        [Range(-100, 100)]
        public int Priority;
        [Tooltip("Let the selected single actions claim a shared press before this dialogue.")]
        public bool PreferSingleActions = true;
        [Tooltip("Single actions allowed to claim the same physical press first.")]
        public DialogueActionPriority PreferredActions = DialogueActionPriority.Throw;
        [Header("Cursor")]
        [Tooltip("Require a free cursor on this object to start or resume a page.")]
        public bool RequireSightToStart;
        [Tooltip("Require a free cursor on this object to advance or close a page.")]
        public bool RequireSightToContinue;
        [Tooltip("Hide the page when the cursor leaves this object or an obstacle blocks it.")]
        public bool HideWhenSightLost;
        [Tooltip("Solid layers that can block the cursor. Player colliders, this object and triggers are excluded.")]
        public LayerMask ObstacleMask = ~0;
        [Header("Replay")]
        [Tooltip("Resume the page, restart its entry, or return to Start after an interruption.")]
        public DialogueInterruption Interruption;
        [Tooltip("Allow a completed tree to restart after leaving Exit Distance and returning.")]
        public bool ReplayOnReturn = true;
        [Tooltip("Allow a fresh Input Action press to restart a completed tree without leaving its range.")]
        public bool ReplayOnInput;
        [Header("Audio")]
        [Tooltip("Optional voice phrases and completion sound.")]
        public DialogueAudioSettings Audio = new DialogueAudioSettings();
        [Header("Dialogue Box")]
        [Tooltip("Box fade shared by every entry in this dialogue interaction.")]
        public DialogueBoxSettings Box = new DialogueBoxSettings();

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks active presentation settings without modifying invalid proposals.</summary>
        /// <param name="warning">Receives an invalid range, input priority, sound or transition.</param>
        /// <returns>True when this dialogue policy can be used.</returns>
        public bool TryValidatePolicy(out string warning)
        {
            // Branches use the same constraints as the interaction's default presentation.
            warning = "Dialogue needs valid audio, positive range and Exit Distance at least as large as Distance.";
            if (Audio == null || !Audio.IsValid() || !InteractionValues.Positive(Distance) || !InteractionValues.Positive(ExitDistance)
                || ExitDistance < Distance || Interruption is not (DialogueInterruption.Resume or DialogueInterruption.Restart or DialogueInterruption.RestartTree))
                return false;
            if (Box == null || !Box.TryValidate(out warning))
                return false;
            if (PreferSingleActions && (int)PreferredActions != -1 && (PreferredActions & ~DialogueActionPriority.All) != 0)
            {
                warning = "Choose supported single action priorities.";
                return false;
            }
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}