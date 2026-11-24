using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Selects independent controller or transform animations for one Day Flow visitor.</summary>
    [Serializable]
    public sealed class SpawnFlowAnimatorSettings
    {
        #region Fields

        [Header("Character Animation")]
        [Tooltip("Animator on the spawned prefab. Its controller supplies the selectable clips. Leave empty to keep normal prefab animation.")]
        public Animator Target;
        [Tooltip("Looping animation during movement along walk-in and walk-out paths.")]
        public SpawnFlowAnimatorState Walk = new SpawnFlowAnimatorState();
        [Tooltip("Looping animation while waiting, during path delays and after payment.")]
        public SpawnFlowAnimatorState Idle = new SpawnFlowAnimatorState();
        [Tooltip("Looping animation while this visitor's dialogue text is visible in the HUD. Overrides Idle and Walk. Pay and Death take priority.")]
        public SpawnFlowAnimatorState Talk = new SpawnFlowAnimatorState { Mode = SpawnFlowAnimationMode.None };
        [Tooltip("One-shot animation when Collect Prices releases a positive payment. Departure waits for it to finish.")]
        public SpawnFlowAnimatorState Pay = new SpawnFlowAnimatorState();
        [Tooltip("One-shot animation when Self Eject applies its impulse. Overrides payment and walking.")]
        public SpawnFlowAnimatorState Death = new SpawnFlowAnimatorState();
        [Header("Playback")]
        [Tooltip("Seconds used to blend between motion and payment states. Zero changes them immediately. Death always starts immediately.")]
        [Range(0f, 0.5f)]
        public float BlendDuration;
        [Tooltip("Match Walk's float Speed Multiplier to actual path travel. Idle, Pay and Death keep their normal playback rates.")]
        public bool SynchronizeWalk;
        [Tooltip("World metres per second matching Walk at its original speed and prefab scale. Lower values increase the gait rate.")]
        [Range(0.1f, 5f)]
        public float WalkReferenceSpeed = 1.3f;

        #endregion

        #region Properties

        /// <summary>Whether this step takes control of any animation state.</summary>
        internal bool Enabled => Walk is { Selected: true } || Idle is { Selected: true }
            || Talk is { Selected: true } || Pay is { Selected: true } || Death is { Selected: true };

        /// <summary>Whether selected roles require the shared prefab Animator.</summary>
        internal bool UsesAnimator => Walk is { ControllerSelected: true } || Idle is { ControllerSelected: true }
            || Talk is { ControllerSelected: true } || Pay is { ControllerSelected: true } || Death is { ControllerSelected: true };

        /// <summary>Whether setup needs to resolve local transform tracks.</summary>
        internal bool UsesTransforms => Walk is { Mode: SpawnFlowAnimationMode.TransformKeyframes }
            || Idle is { Mode: SpawnFlowAnimationMode.TransformKeyframes } || Pay is { Mode: SpawnFlowAnimationMode.TransformKeyframes }
            || Death is { Mode: SpawnFlowAnimationMode.TransformKeyframes } || Talk is { Mode: SpawnFlowAnimationMode.TransformKeyframes };

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks prefab ownership and role prerequisites without changing saved choices.</summary>
        /// <param name="prefab">Visitor prefab selected by the step.</param>
        /// <param name="warning">Receives the first incomplete animation selection.</param>
        /// <returns>True when animation is unused or every selected state is usable.</returns>
        public bool TryValidate(GameObject prefab, out string warning)
        {
            // Inactive modes retain their settings and do not require an Animator.
            if (!ValidRole(Walk, prefab, true, out warning) || !ValidRole(Idle, prefab, true, out warning)
                || !ValidRole(Talk, prefab, true, out warning)
                || !ValidRole(Pay, prefab, false, out warning) || !ValidRole(Death, prefab, false, out warning))
                return false;
            if (!Enabled)
                return true;
            warning = "Use a Blend Duration from zero to 0.5 seconds and a positive Walk Reference Speed up to five metres per second.";
            if (UsesAnimator && (!float.IsFinite(BlendDuration) || BlendDuration < 0f || BlendDuration > 0.5f)
                || Walk is { ControllerSelected: true } && SynchronizeWalk
                && (!float.IsFinite(WalkReferenceSpeed) || WalkReferenceSpeed < 0.1f || WalkReferenceSpeed > 5f))
                return false;
            warning = "Walk synchronization requires a Walk state with a float Speed Multiplier. Select that state again after changing its controller.";
            if (Walk is { ControllerSelected: true } && SynchronizeWalk
                && (Walk.Clip == null || string.IsNullOrWhiteSpace(Walk.SpeedParameter)))
                return false;
            if (UsesAnimator && !ValidAnimator(prefab, out warning))
                return false;
            warning = "Talk requires an enabled Dialogue interaction on this prefab.";
            if (Talk is { Selected: true } && !SupportsTalk(prefab))
                return false;
            warning = "Pay requires Modify By Contact with consumption and Collect Prices on this prefab.";
            if (Pay is { Selected: true } && !SupportsPay(prefab))
                return false;
            warning = "Death requires an enabled Self Eject interaction on this prefab.";
            if (Death is { Selected: true } && !SupportsDeath(prefab))
                return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Checks the shared controller only when a role uses a controller state.</summary>
        /// <param name="prefab">Visitor hierarchy containing the Animator.</param>
        /// <param name="warning">Receives the first invalid controller binding.</param>
        /// <returns>True when the Animator and its selected clips are available.</returns>
        private bool ValidAnimator(GameObject prefab, out string warning)
        {
            // Transform roles remain usable on prefabs without any animation controller.
            warning = "Select an enabled Animator with a controller on this prefab.";
            if (Target == null || prefab == null || !Target.enabled || !Target.transform.IsChildOf(prefab.transform)
                || Target.runtimeAnimatorController == null)
                return false;
            for (Transform branch = Target.transform; branch != null; branch = branch.parent)
                if (!branch.gameObject.activeSelf)
                    return false;
            warning = "Select controller states with clips for the configured animation roles.";
            AnimationClip[] clips = Target.runtimeAnimatorController.animationClips;
            if (!Valid(Walk, clips) || !Valid(Idle, clips) || !Valid(Talk, clips) || !Valid(Pay, clips) || !Valid(Death, clips))
                return false;
            warning = "Walk, Idle and Talk need looping clips. Pay and Death need non-looping clips.";
            if (Walk is { ControllerSelected: true } && !Walk.Clip.isLooping || Idle is { ControllerSelected: true } && !Idle.Clip.isLooping
                || Talk is { ControllerSelected: true } && !Talk.Clip.isLooping
                || Pay is { ControllerSelected: true } && Pay.Clip.isLooping || Death is { ControllerSelected: true } && Death.Clip.isLooping)
                return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Checks the active mode while leaving other mode settings untouched.</summary>
        /// <param name="state">Optional animation role.</param>
        /// <param name="prefab">Hierarchy supplying transform targets.</param>
        /// <param name="looping">Whether this role repeats until interrupted.</param>
        /// <param name="warning">Receives the first invalid mode or transform track.</param>
        /// <returns>True when the role can be played or is unused.</returns>
        private static bool ValidRole(SpawnFlowAnimatorState state, GameObject prefab, bool looping, out string warning)
        {
            // A role validates only the data needed by its selected playback mode.
            warning = string.Empty;
            if (state == null)
                return true;
            switch (state.Mode)
            {
                case SpawnFlowAnimationMode.Animator:
                case SpawnFlowAnimationMode.None:
                    return true;
                case SpawnFlowAnimationMode.TransformKeyframes:
                    return SpawnFlowTransformTrack.TryValidate(state.Tracks, prefab, looping, out warning);
                default:
                    warning = "Choose a supported character animation mode.";
                    return false;
            }
        }

        /// <summary>Checks the controller reference stored by a nonempty role.</summary>
        /// <param name="state">Optional role selection.</param>
        /// <param name="clips">Controller clips read once for this validation.</param>
        /// <returns>True when empty or linked to a clip in the selected controller.</returns>
        private static bool Valid(SpawnFlowAnimatorState state, AnimationClip[] clips)
        {
            // Controller overrides expose their effective clips through the runtime controller.
            return state is not { ControllerSelected: true } || state.Clip != null && state.Clip.length > 0f
                && !string.IsNullOrWhiteSpace(state.Path) && state.Layer >= 0
                && Array.IndexOf(clips, state.Clip) >= 0;
        }

        /// <summary>Checks whether this visitor can own a dialogue text box.</summary>
        /// <param name="prefab">Visitor hierarchy inspected during setup.</param>
        /// <returns>True when the Talk selector is relevant.</returns>
        public static bool SupportsTalk(GameObject prefab)
        {
            // Dialogue capability is inspected outside animation frame updates.
            if (prefab != null)
                foreach (ObjectDialogue dialogue in prefab.GetComponentsInChildren<ObjectDialogue>(true))
                    if (dialogue.enabled)
                        return true;
            return false;
        }

        /// <summary>Finds an enabled collector that can consume priced items.</summary>
        /// <param name="prefab">Visitor hierarchy to inspect outside frame updates.</param>
        /// <returns>True when the Pay selector is relevant.</returns>
        public static bool SupportsPay(GameObject prefab)
        {
            // Interaction capabilities are inspected only during setup and validation.
            if (prefab != null)
                foreach (ObjectContactModifier contact in prefab.GetComponentsInChildren<ObjectContactModifier>(true))
                    if (contact.enabled && contact.Settings is { ConsumesAny: true, Price: { CollectPrice: true } })
                        return true;
            return false;
        }

        /// <summary>Finds an enabled impulse interaction targeting its own body.</summary>
        /// <param name="prefab">Visitor hierarchy to inspect outside frame updates.</param>
        /// <returns>True when the Death selector is relevant.</returns>
        public static bool SupportsDeath(GameObject prefab)
        {
            // Contact ejection and Black Hole do not represent this visitor's death.
            if (prefab != null)
                foreach (ObjectEject eject in prefab.GetComponentsInChildren<ObjectEject>(true))
                    if (eject.enabled && eject.Settings is { SelfEject: true, Behavior: EjectBehavior.Impulse })
                        return true;
            return false;
        }

        #endregion
        #endregion
    }

    /// <summary>Retains controller and transform settings for one independently selected animation mode.</summary>
    [Serializable]
    public sealed class SpawnFlowAnimatorState
    {
        #region Fields

        [Header("Playback Mode")]
        [Tooltip("Animator plays a controller state. Transform Keyframes animates root or child offsets. None leaves this role unused.")]
        public SpawnFlowAnimationMode Mode;
        [Tooltip("Parallel transform tracks. Idle, Walk and Talk loop; Pay and Death play once. Tracks can loop independently. End loops at identity for a seamless return.")]
        public SpawnFlowTransformTrack[] Tracks = Array.Empty<SpawnFlowTransformTrack>();
        [Header("Controller State")]
        [Tooltip("Full controller path selected with its clip. Empty leaves this role unused.")]
        public string Path = string.Empty;
        [Tooltip("Clip used by the selected state, including controller overrides.")]
        public AnimationClip Clip;
        [Tooltip("Controller layer containing the selected state.")]
        [Range(0, 31)]
        public int Layer;
        [Tooltip("Float Speed Multiplier captured from this controller state. Used only when Walk synchronization is enabled.")]
        public string SpeedParameter = string.Empty;

        #endregion

        #region Properties

        /// <summary>Whether the role has a selection, including a missing clip that needs correction.</summary>
        internal bool Selected => Mode == SpawnFlowAnimationMode.TransformKeyframes || ControllerSelected;

        /// <summary>Whether the active mode contains a controller selection requiring validation.</summary>
        internal bool ControllerSelected => Mode == SpawnFlowAnimationMode.Animator && (Clip != null || !string.IsNullOrWhiteSpace(Path));

        #endregion
    }
}
