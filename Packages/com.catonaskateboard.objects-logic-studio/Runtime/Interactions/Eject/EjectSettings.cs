using System;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses mass-dependent impulse or a direct velocity increment.</summary>
    public enum EjectForceMode { Impulse, VelocityChange }

    /// <summary>Chooses immediate contact ejection or a timed attraction followed by a final impulse.</summary>
    public enum EjectBehavior { Impulse, BlackHole }

    /// <summary>Assigns an impulse to a group of identity flags.</summary>
    [Serializable]
    public sealed class EjectRule
    {
        #region Fields

        [Header("Ejected Objects")]
        [Tooltip("Flags on the contacted collider or its owning parents. The first matching rule supplies each body's impulse.")]
        public ObjectFlag[] Flags = Array.Empty<ObjectFlag>();
        [Tooltip("Match any selected flag or require every selected flag.")]
        public ObjectFlagMatch Match;
        [Tooltip("Impulse vector applied once per physical body. Magnitude is not normalized.")]
        public Vector3 Impulse = new Vector3(0f, 5f, 0f);
        [Tooltip("Self rotates the vector with the ejector without inheriting its scale. World uses the vector unchanged.")]
        public Space Space = Space.Self;

        #endregion
    }

    /// <summary>Configures direct-contact ejection with independent flag groups and optional delayed destruction.</summary>
    [Serializable]
    public sealed class EjectSettings
    {
        #region Fields

        [Header("Targeting")]
        [Tooltip("Player reach, aim and visibility required to press this object's eject action.")]
        public TransferTargetSettings Target = new TransferTargetSettings();
        [Header("Before Ejection")]
        [Tooltip("Request the selected Trigger Animation before applying impulses or starting Black Hole. Cancellation prevents the delayed effect.")]
        public bool AnimateBeforeEject;
        [Tooltip("Select an exact animation in a loaded gameplay scene. Its stable identity also works from prefab assets. Disable to use the local animation.")]
        public bool ExternalAnimation;
        [Tooltip("Persistent identity of the selected scene animation. Resolved from registered components when requested, without runtime scene searches.")]
        public string AnimationIdentity = string.Empty;
        [Tooltip("Wait for the automatic return and its delay when the selected animation schedules one. Otherwise the first endpoint releases Eject.")]
        public bool WaitForAnimationReturn;
        [Tooltip("Hold the first endpoint until Eject finishes, then return to the animation's initial state using its automatic return settings. Available when waiting for the full return is disabled.")]
        public bool ForceAutoReturn;
        [Tooltip("Start Black Hole this percentage before the awaited animation duration ends. Zero waits for the animation; 25 starts at 75% progress.")]
        [Range(0f, 100f)]
        public float AnimationOverlap;
        [Tooltip("Start the forced return this percentage before Black Hole ends. Zero returns at completion; 25 starts at 75% of its duration.")]
        [Range(0f, 100f)]
        public float ReturnOverlap;
        [Header("Ejection")]
        [Tooltip("Impulse ejects matching contacts immediately. Black Hole attracts filtered bodies to a selected collider before launching them.")]
        public EjectBehavior Behavior;
        [Tooltip("Destination collider, attraction, final impulse and optional camera and air feedback.")]
        public BlackHoleSettings BlackHole = new BlackHoleSettings();
        [Tooltip("Apply one impulse to this object's own Rigidbody instead of searching for flagged contacts.")]
        public bool SelfEject;
        [Tooltip("Use the matching shot's direction and speed instead of Self Impulse. Works with bullets and Cursor Interaction.")]
        public bool UseBulletImpulse;
        [Tooltip("Multiply the shot's direction and speed by this value. Impulse also accounts for the body's mass.")]
        [Range(0.001f, 10f)]
        public float BulletImpulseMultiplier = 0.1f;
        [Tooltip("Single impulse for Self Eject. Flags are unused in this mode.")]
        public EjectRule SelfImpulse = new EjectRule();
        [Tooltip("Use a temporary physics profile during Self Eject, restoring authored settings on disable. A kinematic Rigidbody is prepared by Apply if needed.")]
        public bool TemporaryPhysics;
        [Tooltip("Mass, gravity, damping, constraints and surface response used until despawn or disable.")]
        public ReleaseSettings Physics = new ReleaseSettings();
        [Header("Contact")]
        [Tooltip("Include trigger colliders on the ejector and contacted objects.")]
        public bool IncludeTriggers;
        [Tooltip("Maximum surface separation accepted as direct contact, in metres.")]
        [Range(0f, 0.1f)]
        public float Tolerance = 0.005f;
        [Tooltip("Release carried objects and make eligible kinematic bodies dynamic before applying their impulse. Disable to affect only free dynamic bodies.")]
        public bool ReleaseKinematic = true;
        [Tooltip("Impulse respects Rigidbody mass. Velocity Change adds the vector directly as velocity.")]
        public EjectForceMode Mode;
        [Tooltip("Flag groups in priority order. A body matching several groups receives only the first matching impulse.")]
        public EjectRule[] Rules = Array.Empty<EjectRule>();
        [Header("Temporary Collisions")]
        [Tooltip("Temporarily exclude selected collision layers on each successfully ejected body.")]
        public bool IgnoreCollisions;
        [Tooltip("Layers ignored only by ejected bodies during the exclusion interval. The project's collision matrix is unchanged.")]
        public LayerMask IgnoredLayers;
        [Tooltip("Scaled seconds from each impulse before its collision exclusions expire. Repeated impulses extend overlapping layer timers.")]
        [Range(0.01f, 5f)]
        public float IgnoreDuration = 0.5f;
        [Header("Despawn")]
        [Tooltip("Destroy successfully ejected objects after a delay, even if the ejector is disabled or destroyed.")]
        public bool Despawn;
        [Tooltip("Scaled seconds from the final impulse to destruction. Black Hole attraction time is excluded; pausing also pauses this timer.")]
        [Range(0.01f, 60f)]
        public float DespawnDelay = 5f;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks reusable ejection rules independently of any scene contacts.</summary>
        /// <param name="warning">Receives invalid targeting, flag groups or timing.</param>
        /// <returns>True when every impulse rule is complete.</returns>
        public bool TryValidate(out string warning)
        {
            // Empty or invalid groups cannot silently eject unrelated objects.
            warning = "Configure Eject targeting.";
            if (Target == null || !Target.TryValidate(out warning))
                return false;
            warning = "Select a scene Trigger Animation before enabling an external animation prerequisite.";
            if (AnimateBeforeEject && ExternalAnimation && string.IsNullOrEmpty(AnimationIdentity))
                return false;
            if (Behavior == EjectBehavior.BlackHole)
            {
                warning = "Animation overlap percentages must be finite and between 0 and 100.";
                if (AnimateBeforeEject && (!float.IsFinite(AnimationOverlap) || AnimationOverlap < 0f || AnimationOverlap > 100f
                    || ForceAutoReturn && !WaitForAnimationReturn && (!float.IsFinite(ReturnOverlap) || ReturnOverlap < 0f || ReturnOverlap > 100f)))
                    return false;
                warning = "Configure Black Hole settings and a supported force mode.";
                return BlackHole != null && Mode is (EjectForceMode.Impulse or EjectForceMode.VelocityChange)
                    && BlackHole.TryValidate(out warning) && ValidateLifetime(out warning);
            }
            if (Behavior != EjectBehavior.Impulse)
            {
                warning = "Choose a supported Eject behavior.";
                return false;
            }
            if (SelfEject && TemporaryPhysics && (Physics == null || !Physics.TryValidate(out warning)))
                return false;
            warning = "Bullet Impulse Multiplier must be positive and finite.";
            if (SelfEject && UseBulletImpulse && !InteractionValues.Positive(BulletImpulseMultiplier))
                return false;
            warning = "Use a finite non-negative contact tolerance, a supported force mode and at least one impulse rule.";
            if (Mode is not (EjectForceMode.Impulse or EjectForceMode.VelocityChange)
                || !SelfEject && (!float.IsFinite(Tolerance) || Tolerance < 0f || Rules == null || Rules.Length == 0))
                return false;
            for (int index = 0; index < (SelfEject ? 1 : Rules.Length); index++)
            {
                if (SelfEject && UseBulletImpulse)
                    break;
                EjectRule rule = SelfEject ? SelfImpulse : Rules[index];
                warning = "Each Eject rule needs flags, a finite non-zero impulse, Any or All matching, and Self or World space.";
                if (rule == null || !InteractionValues.Finite(rule.Impulse) || rule.Impulse.sqrMagnitude <= 0f
                    || rule.Space is not (Space.Self or Space.World) || rule.Match is not (ObjectFlagMatch.Any or ObjectFlagMatch.All))
                    return false;
                if (!SelfEject && !ObjectFlagRules.TryValidate(rule.Flags, false, out warning))
                    return false;
            }
            return ValidateLifetime(out warning);
        }

        /// <summary>Checks post-impulse exclusions and lifetime for both ejection behaviors.</summary>
        /// <param name="warning">Receives invalid collision layers or timing.</param>
        /// <returns>True when optional post-launch policies are usable.</returns>
        private bool ValidateLifetime(out string warning)
        {
            warning = IgnoreCollisions && (IgnoredLayers.value == 0 || !InteractionValues.Positive(IgnoreDuration))
                ? "Choose collision layers and a positive finite Ignore Duration."
                : Despawn && !InteractionValues.Positive(DespawnDelay) ? "Despawn Delay must be positive and finite." : string.Empty;
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
