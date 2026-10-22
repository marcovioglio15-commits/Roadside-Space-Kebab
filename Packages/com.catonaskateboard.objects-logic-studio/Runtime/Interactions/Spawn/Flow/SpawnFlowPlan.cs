using System;
using CatOnASkateboard.MenuStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Defines the ordered days and scene transitions owned by one Spawn Management flow.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Day Flow Plan", fileName = "DayFlowPlan")]
    public sealed class SpawnFlowPlan : MenuTransitionProfile
    {
        #region Fields

        [Header("Days")]
        [Tooltip("Ordered days. Each scene runs its own spawn count and step selection.")]
        public SpawnFlowDay[] Days = Array.Empty<SpawnFlowDay>();
        [Tooltip("Return to the first day after the last one. Otherwise load Main Menu.")]
        public bool Loop = true;
        [Tooltip("When consecutive days use the current scene, preserve it and return the player to the initial pose under the black fade. Different scene destinations still load normally.")]
        public bool PreserveScene = true;
        [Tooltip("Scene loaded after the final day when Loop is disabled.")]
        public string MainMenu = string.Empty;
        [Tooltip("Optional pause-menu scene loaded additively for every day.")]
        public string PauseScene = string.Empty;
        [Header("Black Fade")]
        [Tooltip("Unscaled seconds to cover the current scene with black.")]
        [Range(0f, 5f)]
        public float FadeOut = 0.5f;
        [Tooltip("Minimum unscaled seconds held at black after the destination loads.")]
        [Range(0f, 10f)]
        public float BlackDelay = 0.2f;
        [Tooltip("Unscaled seconds to reveal the destination scene.")]
        [Range(0f, 5f)]
        public float FadeIn = 0.5f;
        [Header("Randomness")]
        [Tooltip("Use a repeatable stream for counts and step selection throughout the run.")]
        public bool FixedSeed;
        [Tooltip("Seed used when Fixed Seed is enabled.")]
        public int Seed = 1;

        #endregion

        #region Properties

        /// <summary>Shares the same fade-out timing with the main menu's Play command.</summary>
        public override float FadeOutSeconds => FadeOut;
        /// <summary>Shares the same black hold timing with the main menu's Play command.</summary>
        public override float BlackSeconds => BlackDelay;
        /// <summary>Shares the same reveal timing with the main menu's Play command.</summary>
        public override float FadeInSeconds => FadeIn;

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks the complete plan without changing authored values.</summary>
        /// <param name="warning">Receives the first missing scene, step or invalid duration.</param>
        /// <returns>True when every day can run.</returns>
        public bool TryValidate(out string warning)
        {
            // Build availability is checked separately so assets can be authored before scene registration.
            warning = "Add at least one day and assign Main Menu when Loop is disabled.";
            if (Days == null || Days.Length == 0 || !Loop && string.IsNullOrWhiteSpace(MainMenu))
                return false;
            warning = "Fade durations and Black Delay must be finite and nonnegative.";
            if (!Duration(FadeOut) || !Duration(FadeIn) || !Duration(BlackDelay))
                return false;
            foreach (SpawnFlowDay day in Days)
                if (day == null || !day.TryValidate(out warning))
                    return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Checks timing shared by delays and transform keyframes.</summary>
        /// <param name="value">Authored number of seconds.</param>
        /// <returns>True for a finite nonnegative duration.</returns>
        internal static bool Duration(float value)
        {
            // Zero explicitly requests an immediate authored transition.
            return float.IsFinite(value) && value >= 0f;
        }

        #endregion
        #endregion
    }

    /// <summary>Configures one day's spawn budget and reusable ordered or random step templates.</summary>
    [Serializable]
    public sealed class SpawnFlowDay
    {
        #region Fields

        [Header("Day")]
        [Tooltip("Day name shown in the plan editor and scene gizmos.")]
        public string Name = "Day";
        [Tooltip("Gameplay scene for this day, selected through the scene picker.")]
        public string Scene = string.Empty;
        [Tooltip("Wait for the selected scene interaction to complete after day start before starting the first spawn delay.")]
        public bool WaitBeforeFirstSpawn;
        [Tooltip("Interaction in this day's gameplay scene whose completion opens the first spawn.")]
        public SceneInteractionReference StartInteraction = new SceneInteractionReference();
        [Tooltip("Animate scene Trigger Animations back to their initial state using Auto Return Duration and Rotation, even with Auto Return disabled. Day completion waits for every return; no interaction events are emitted.")]
        public DayAnimationReset ResetAnimations;
        [Tooltip("Exact scene Trigger Animations reset when Selected is chosen.")]
        public SceneInteractionReference[] AnimationsToReset = Array.Empty<SceneInteractionReference>();
        [Tooltip("Choose the spawn count once at day start, including both range endpoints.")]
        public bool RandomCount;
        [Tooltip("Spawn Count ends after the configured visits. Timer stops new spawns after Day Duration and ends after the current visit finishes. No timer UI is created.")]
        public DayLengthMode LengthMode;
        [Tooltip("Scaled seconds from opening the first-spawn gate, including spawn delays, arrivals and visits. Pause stops the timer. An active visitor finishes normally after expiry.")]
        [Range(1f, 3600f)]
        public float DayDuration = 300f;
        [Tooltip("Number of completed spawn steps before the day ends.")]
        public int Count = 1;
        [Tooltip("Smallest number of spawn steps for this day.")]
        public int Minimum = 1;
        [Tooltip("Largest number of spawn steps for this day, inclusive.")]
        public int Maximum = 3;
        [Tooltip("Sequence repeats the authored list until the count is reached. Random modes draw a template for each spawn.")]
        public SpawnSelection Selection = SpawnSelection.Sequence;
        [Tooltip("Spawn templates, each with its own prefab, completion interaction and animation paths.")]
        public SpawnFlowStep[] Steps = Array.Empty<SpawnFlowStep>();

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks count limits and every template participating in this day.</summary>
        /// <param name="warning">Receives the first invalid day setting.</param>
        /// <returns>True when the day can select and complete its requested spawns.</returns>
        public bool TryValidate(out string warning)
        {
            // Counts are positive; an empty day cannot cause an endless scene-loading loop.
            warning = "Each day needs a scene, positive spawn counts and at least one step.";
            if (string.IsNullOrWhiteSpace(Scene) || Steps == null || Steps.Length == 0
                || LengthMode == DayLengthMode.SpawnCount && (RandomCount ? Minimum <= 0 || Maximum < Minimum || Maximum == int.MaxValue : Count <= 0))
                return false;
            warning = "Choose a supported day length mode and a positive finite Day Duration for Timer mode.";
            if (LengthMode is not (DayLengthMode.SpawnCount or DayLengthMode.Timer)
                || LengthMode == DayLengthMode.Timer && !InteractionValues.Positive(DayDuration))
                return false;
            warning = "Choose a supported spawn selection mode.";
            if (Selection is not (SpawnSelection.Sequence or SpawnSelection.UniformRandom or SpawnSelection.WeightedRandom))
                return false;
            warning = "Select a valid first-spawn interaction and supported animation reset policy.";
            if (WaitBeforeFirstSpawn && StartInteraction is not { IsValid: true }
                || ResetAnimations is not (DayAnimationReset.None or DayAnimationReset.All or DayAnimationReset.Selected))
                return false;
            if (ResetAnimations == DayAnimationReset.Selected)
            {
                warning = "Select at least one scene Trigger Animation to reset.";
                if (AnimationsToReset == null || AnimationsToReset.Length == 0)
                    return false;
                foreach (SceneInteractionReference animation in AnimationsToReset)
                    if (animation is not { IsValid: true } || animation.Interaction.Kind != InteractionTemplateKind.TriggerAnimation)
                        return false;
            }
            bool weighted = false;
            foreach (SpawnFlowStep step in Steps)
            {
                if (step == null || !step.TryValidate(out warning))
                    return false;
                weighted |= step.Weight > 0f;
            }
            warning = Selection == SpawnSelection.WeightedRandom && !weighted ? "At least one step needs a positive weight." : string.Empty;
            return warning.Length == 0;
        }

        #endregion
        #endregion
    }

    /// <summary>Selects which scene animations return to their initial endpoint when a day finishes.</summary>
    public enum DayAnimationReset { None, All, Selected }

    /// <summary>Chooses a fixed visit budget or a hidden timed window for admitting new visitors.</summary>
    public enum DayLengthMode { SpawnCount, Timer }
}
