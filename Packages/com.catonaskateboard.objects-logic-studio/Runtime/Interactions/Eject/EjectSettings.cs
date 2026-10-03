using System;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses mass-dependent impulse or a direct velocity increment.</summary>
    public enum EjectForceMode { Impulse, VelocityChange }

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
        [Header("Ejection")]
        [Tooltip("Apply one impulse to this object's own Rigidbody instead of searching for flagged contacts.")]
        public bool SelfEject;
        [Tooltip("Single impulse for Self Eject. Flags are unused in this mode.")]
        public EjectRule SelfImpulse = new EjectRule();
        [Header("Contact")]
        [Tooltip("Include trigger colliders on the ejector and contacted objects.")]
        public bool IncludeTriggers;
        [Tooltip("Maximum surface separation accepted as direct contact, in metres.")]
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
        public float IgnoreDuration = 0.5f;
        [Header("Despawn")]
        [Tooltip("Destroy successfully ejected objects after a delay, even if the ejector is disabled or destroyed.")]
        public bool Despawn;
        [Tooltip("Scaled seconds from ejection to destruction. Pausing game time also pauses this timer.")]
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
            warning = "Use a finite non-negative contact tolerance, a supported force mode and at least one impulse rule.";
            if (Mode is not (EjectForceMode.Impulse or EjectForceMode.VelocityChange)
                || !SelfEject && (!float.IsFinite(Tolerance) || Tolerance < 0f || Rules == null || Rules.Length == 0))
                return false;
            for (int index = 0; index < (SelfEject ? 1 : Rules.Length); index++)
            {
                EjectRule rule = SelfEject ? SelfImpulse : Rules[index];
                warning = "Each Eject rule needs flags, a finite non-zero impulse, Any or All matching, and Self or World space.";
                if (rule == null || !InteractionValues.Finite(rule.Impulse) || rule.Impulse.sqrMagnitude <= 0f
                    || rule.Space is not (Space.Self or Space.World) || rule.Match is not (ObjectFlagMatch.Any or ObjectFlagMatch.All))
                    return false;
                if (!SelfEject && !ObjectFlagRules.TryValidate(rule.Flags, false, out warning))
                    return false;
            }
            warning = IgnoreCollisions && (IgnoredLayers.value == 0 || !InteractionValues.Positive(IgnoreDuration))
                ? "Choose collision layers and a positive finite Ignore Duration."
                : Despawn && !InteractionValues.Positive(DespawnDelay) ? "Despawn Delay must be positive and finite." : string.Empty;
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
