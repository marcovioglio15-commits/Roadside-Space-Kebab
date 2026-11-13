using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Selects optional controller states for one Day Flow visitor.</summary>
    [Serializable]
    public sealed class SpawnFlowAnimatorSettings
    {
        #region Fields

        [Header("Character Animation")]
        [Tooltip("Animator on the spawned prefab. Its controller supplies the selectable clips. Leave empty to keep normal prefab animation.")]
        public Animator Target;
        [Tooltip("Optional looping state during walk-in and walk-out.")]
        public SpawnFlowAnimatorState Walk = new SpawnFlowAnimatorState();
        [Tooltip("Optional looping state while waiting and after payment.")]
        public SpawnFlowAnimatorState Idle = new SpawnFlowAnimatorState();
        [Tooltip("Optional one-shot state when Collect Prices releases a positive payment. Departure waits for it to finish.")]
        public SpawnFlowAnimatorState Pay = new SpawnFlowAnimatorState();
        [Tooltip("Optional one-shot state when Self Eject applies its impulse. Overrides payment and walking.")]
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
            || Pay is { Selected: true } || Death is { Selected: true };

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks prefab ownership and role prerequisites without changing saved choices.</summary>
        /// <param name="prefab">Visitor prefab selected by the step.</param>
        /// <param name="warning">Receives the first incomplete animation selection.</param>
        /// <returns>True when animation is unused or every selected state is usable.</returns>
        public bool TryValidate(GameObject prefab, out string warning)
        {
            // Empty roles preserve the existing transform-only flow.
            warning = string.Empty;
            if (!Enabled)
                return true;
            warning = "Use a Blend Duration from zero to 0.5 seconds and a positive Walk Reference Speed up to five metres per second.";
            if (!float.IsFinite(BlendDuration) || BlendDuration < 0f || BlendDuration > 0.5f
                || SynchronizeWalk && (!float.IsFinite(WalkReferenceSpeed) || WalkReferenceSpeed < 0.1f || WalkReferenceSpeed > 5f))
                return false;
            warning = "Walk synchronization requires a Walk state with a float Speed Multiplier. Select that state again after changing its controller.";
            if (SynchronizeWalk && (Walk?.Clip == null || string.IsNullOrWhiteSpace(Walk.SpeedParameter)))
                return false;
            warning = "Select an enabled Animator with a controller on this prefab.";
            if (Target == null || prefab == null || !Target.enabled || !Target.transform.IsChildOf(prefab.transform)
                || Target.runtimeAnimatorController == null)
                return false;
            for (Transform branch = Target.transform; branch != null; branch = branch.parent)
                if (!branch.gameObject.activeSelf)
                    return false;
            warning = "Select controller states with clips for the configured animation roles.";
            AnimationClip[] clips = Target.runtimeAnimatorController.animationClips;
            if (!Valid(Walk, clips) || !Valid(Idle, clips) || !Valid(Pay, clips) || !Valid(Death, clips))
                return false;
            warning = "Walk and Idle need looping clips. Pay and Death need non-looping clips.";
            if (Walk?.Clip != null && !Walk.Clip.isLooping || Idle?.Clip != null && !Idle.Clip.isLooping
                || Pay?.Clip != null && Pay.Clip.isLooping || Death?.Clip != null && Death.Clip.isLooping)
                return false;
            warning = "Pay requires Modify By Contact with consumption and Collect Prices on this prefab.";
            if (Pay?.Clip != null && !SupportsPay(prefab))
                return false;
            warning = "Death requires an enabled Self Eject interaction on this prefab.";
            if (Death?.Clip != null && !SupportsDeath(prefab))
                return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Checks the controller reference stored by a nonempty role.</summary>
        /// <param name="state">Optional role selection.</param>
        /// <param name="clips">Controller clips read once for this validation.</param>
        /// <returns>True when empty or linked to a clip in the selected controller.</returns>
        private static bool Valid(SpawnFlowAnimatorState state, AnimationClip[] clips)
        {
            // Controller overrides expose their effective clips through the runtime controller.
            return state is not { Selected: true } || state.Clip != null && state.Clip.length > 0f
                && !string.IsNullOrWhiteSpace(state.Path) && state.Layer >= 0
                && Array.IndexOf(clips, state.Clip) >= 0;
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

    /// <summary>Stores one exact controller state and its effective animation clip.</summary>
    [Serializable]
    public sealed class SpawnFlowAnimatorState
    {
        #region Fields

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
        internal bool Selected => Clip != null || !string.IsNullOrWhiteSpace(Path);

        #endregion
    }
}
