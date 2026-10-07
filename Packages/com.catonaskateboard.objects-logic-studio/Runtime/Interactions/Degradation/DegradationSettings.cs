using System;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses elapsed scaled time or discrete sufficiently strong impacts.</summary>
    public enum DegradationMode { Time, Impacts }
    /// <summary>Controls when a grabbable object's degradation may progress.</summary>
    public enum DegradationCarryPolicy { FromSpawn, AfterFirstGrab, OnlyWhileCarried }

    /// <summary>Defines the next appearance and identity state reached after one degradation interval.</summary>
    [Serializable]
    public sealed class DegradationStep
    {
        #region Fields

        [Header("Step")]
        [Tooltip("Name identifying this degradation stage.")]
        public string Name = "Stage";
        [Tooltip("Scaled seconds before entering this step, measured from the preceding step.")]
        public float Duration = 10f;
        [Tooltip("Qualifying impacts required after the preceding step.")]
        public int Impacts = 1;
        [Tooltip("Optional mesh and material replacements applied when this step is reached.")]
        public ItemAppearanceSettings Appearance = new ItemAppearanceSettings();
        [Tooltip("Change the object's identity when entering this stage.")]
        public bool ChangeFlag;
        [Tooltip("Identity flag used by this step, except for Clear.")]
        public ObjectFlag Flag;
        [Tooltip("Identity operation applied once when this step is entered.")]
        public ObjectFlagOperation Operation;

        #endregion
    }

    /// <summary>Places one fragment prefab relative to the degraded object at final destruction.</summary>
    [Serializable]
    public sealed class DegradationFragment
    {
        #region Fields

        [Header("Fragment")]
        [Tooltip("Active prefab root spawned once when the final degradation stage destroys its owner.")]
        public GameObject Prefab;
        [Tooltip("Fragment position relative to the object's final pivot.")]
        public Vector3 Position;
        [Tooltip("Fragment rotation relative to the object's final orientation.")]
        public Vector3 Rotation;

        #endregion
    }

    /// <summary>Configures a finite degradation sequence with optional final destruction.</summary>
    [Serializable]
    public sealed class DegradationSettings
    {
        #region Fields

        [Header("Progress")]
        [Tooltip("Advance using scaled time or qualifying collision impulses.")]
        public DegradationMode Mode;
        [Tooltip("Begin at spawn, after the first successful Grab, or progress only while carried. Carry modes require Grab on this object.")]
        public DegradationCarryPolicy Carry;
        [Tooltip("Minimum impulse in newton-seconds counted as an impact. Kinematic carry estimates it from mass and the normal speed stopped by an obstacle.")]
        public float MinimumImpulse = 1f;
        [Tooltip("Minimum scaled seconds between counted impacts; simultaneous compound contacts count only once.")]
        public float ImpactInterval = 0.1f;
        [Tooltip("Stages entered in array order, each with its own time or impact requirement.")]
        public DegradationStep[] Steps = Array.Empty<DegradationStep>();
        [Header("Final Stage")]
        [Tooltip("Destroy the object after entering its final stage and publishing completion.")]
        public bool DestroyAtEnd;
        [Tooltip("Optional fragment prefabs created only at final destruction. No UI is generated.")]
        public DegradationFragment[] Fragments = Array.Empty<DegradationFragment>();

        #endregion
        #region Methods
        #region Validation

        /// <summary>Checks enabled timing, identity changes and fragment data without changing values.</summary>
        /// <param name="warning">Receives the first invalid stage or output.</param>
        /// <returns>True when the reusable sequence is complete.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Choose supported degradation and carry modes, with at least one step.";
            if (Mode is not (DegradationMode.Time or DegradationMode.Impacts)
                || Carry is not (DegradationCarryPolicy.FromSpawn or DegradationCarryPolicy.AfterFirstGrab or DegradationCarryPolicy.OnlyWhileCarried)
                || Steps == null || Steps.Length == 0)
                return false;
            warning = "Impact degradation needs a positive finite impulse and a non-negative finite interval.";
            if (Mode == DegradationMode.Impacts && (!InteractionValues.Positive(MinimumImpulse) || !float.IsFinite(ImpactInterval) || ImpactInterval < 0f))
                return false;
            foreach (DegradationStep step in Steps)
            {
                warning = "Each degradation step needs valid appearance, a positive duration or impact count, and a valid enabled flag change.";
                if (step == null || step.Appearance == null || (Mode == DegradationMode.Time ? !InteractionValues.Positive(step.Duration) : step.Impacts <= 0)
                    || step.ChangeFlag && (!ObjectFlagRules.IsValidOperation(step.Operation) || step.Operation != ObjectFlagOperation.Clear && step.Flag == null))
                    return false;
                if (!step.Appearance.TryValidate(out warning))
                    return false;
            }
            if (DestroyAtEnd)
            {
                warning = "Choose active fragment prefab roots with finite local poses.";
                if (Fragments == null)
                    return false;
                foreach (DegradationFragment fragment in Fragments)
                    if (fragment == null || !SpawnManagementSettings.Prefab(fragment.Prefab) || !fragment.Prefab.activeSelf
                        || !InteractionValues.Finite(fragment.Position) || !InteractionValues.Finite(fragment.Rotation))
                        return false;
            }
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
