using System;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Identifies the interaction cards a shot can filter without referencing their package.</summary>
    [Flags]
    public enum PlayerToolImpactInteraction
    {
        None = 0,
        Grab = 1 << 0,
        Drop = 1 << 1,
        Throw = 1 << 2,
        Dispenser = 1 << 3,
        Container = 1 << 4,
        TriggerAnimation = 1 << 5,
        Eject = 1 << 6,
        Hover = 1 << 7,
        ModifyByContact = 1 << 8,
        Dialogue = 1 << 9,
        Outline = 1 << 10,
        Unlock = 1 << 11,
        AssemblyStation = 1 << 12,
        AssemblyProduct = 1 << 13,
        SpawnManagement = 1 << 14,
        Slice = 1 << 15,
        PlayAmbient = 1 << 16,
        AvailableOrders = 1 << 17,
        ObjectDegradation = 1 << 18,
        GravityGenerator = 1 << 19,
        ElasticDeformation = 1 << 20,
        DirtTrail = 1 << 21,
        SpraySauce = 1 << 22,
        Deathbox = 1 << 23,
        PresenceCap = 1 << 24,
        All = (1 << 25) - 1
    }

    /// <summary>Supplies the interaction category of a component when a shot reaches its body.</summary>
    public interface IPlayerToolImpactTarget
    {
        /// <summary>Category exposed by this active interaction, or None when disabled.</summary>
        PlayerToolImpactInteraction ImpactInteraction { get; }
    }

    /// <summary>Lets a component on the hit body react to an equivalent physical impact.</summary>
    public interface IPlayerToolImpactReceiver
    {
        /// <summary>Reports the impulse after the weapon's filters and falloff.</summary>
        /// <param name="impulse">Equivalent impulse in newton-seconds.</param>
        void ReceiveToolImpact(float impulse);
    }

    /// <summary>Configures a physical impulse when the first hit passes the target filters.</summary>
    [Serializable]
    public sealed class PlayerToolImpactForceSettings
    {
        #region Fields

        [Header("Simulated Impact Force")]
        [Tooltip("Push the dynamic Rigidbody at the shot contact in either weapon mode.")]
        public bool Enabled;
        [Tooltip("Launch matching objects with mass-independent speed, upward lift and tumbling.")]
        public bool ExplosiveResponse;
        [Tooltip("Impulse in newton-seconds. Heavier bodies move less. The hit point can also produce spin.")]
        [Range(0.01f, 30f)]
        public float Impulse = 1.2f;
        [Tooltip("Horizontal speed added at the centre of the blast, in metres per second.")]
        [Range(0.1f, 16f)]
        public float BurstSpeed = 4.8f;
        [Tooltip("Upward speed added at the centre of the blast, in metres per second.")]
        [Range(0f, 10f)]
        public float BurstLift = 2.8f;
        [Tooltip("Tumble added at the centre of the blast, in radians per second.")]
        [Range(0f, 30f)]
        public float BurstSpin = 12f;
        [Tooltip("Let impact-based degradation count this simulated hit using the object's normal impulse threshold and cooldown.")]
        public bool AffectImpactDegradation;
        [Tooltip("Scale the impulse reported to Degradation. Stronger hits can complete multiple stages. Zero skips degradation. Launch strength is set separately.")]
        [Range(0f, 100f)]
        public float DegradationImpulseMultiplier = 50f;
        [Tooltip("Launch loose outputs from Slice steps performed by the shot. Outputs transferred to the carry slot stay held.")]
        public bool SliceShattering;
        [Tooltip("Scale the travel distance of loose Slice outputs. One uses the base launch. Spread and tumble stay the same.")]
        [Range(0.001f, 4f)]
        public float SliceDistanceMultiplier = 1f;
        [Tooltip("Skip simulated pushes, impact degradation and slice shattering for objects currently owned by Grab.")]
        public bool IgnoreCarriedObjects = true;
        [Tooltip("Require the struck collider to be on an included layer.")]
        public bool FilterLayers;
        [Tooltip("Eligible collider layers. The weapon collision mask still controls which surface stops the shot.")]
        public LayerMask Layers = ~0;
        [Tooltip("Require any of these enabled interactions on the hit body. None skips this filter.")]
        public PlayerToolImpactInteraction Interactions;
        [Tooltip("Require Object Identity flags in addition to any layer or interaction filter.")]
        public bool FilterFlags;
        [Tooltip("Flags that must match the struck object's current identity.")]
        public ObjectFlag[] Flags = Array.Empty<ObjectFlag>();
        [Tooltip("Any accepts one selected flag. All requires every selected flag.")]
        public ObjectFlagMatch FlagMatch;

        #endregion

        #region Methods

        #region Burst

        /// <summary>Samples the same lift and tumble for impacted bodies and loose Slice outputs.</summary>
        /// <param name="horizontal">Normalized world direction along the ground.</param>
        /// <param name="strength">Relative speed after impact falloff.</param>
        /// <param name="velocity">World velocity change.</param>
        /// <param name="tumble">World angular velocity change.</param>
        public void SampleBurst(Vector3 horizontal, float strength, out Vector3 velocity, out Vector3 tumble)
        {
            // A small variation keeps nearby pieces from following identical trajectories.
            velocity = (horizontal * BurstSpeed + Vector3.up * (BurstLift * UnityEngine.Random.Range(0.85f, 1.15f))) * strength;
            tumble = BurstSpin > 0f ? UnityEngine.Random.onUnitSphere * (BurstSpin * strength) : Vector3.zero;
        }

        #endregion

        #region Validation

        /// <summary>Validates only the settings used by an enabled impact force.</summary>
        /// <param name="warning">Receives the first invalid impulse or filter.</param>
        /// <returns>True when the settings can be used by a weapon.</returns>
        internal bool TryValidate(out string warning)
        {
            warning = string.Empty;
            if (!Enabled)
                return true;
            // An empty enabled filter could make a weapon silently stop applying force.
            if ((ExplosiveResponse || SliceShattering) && (!float.IsFinite(BurstSpeed) || BurstSpeed is < 0.1f or > 16f
                || !float.IsFinite(BurstLift) || BurstLift is < 0f or > 10f || !float.IsFinite(BurstSpin) || BurstSpin is < 0f or > 30f))
                warning = "Set burst speed from 0.1 to 16, lift from 0 to 10 and spin from 0 to 30.";
            else if (SliceShattering && (!float.IsFinite(SliceDistanceMultiplier) || SliceDistanceMultiplier is < 0.001f or > 4f))
                warning = "Set Slice Distance Multiplier from 0.001 to 4.";
            else if (AffectImpactDegradation && (!float.IsFinite(DegradationImpulseMultiplier) || DegradationImpulseMultiplier is < 0f or > 100f))
                warning = "Set Degradation Impulse Multiplier from 0 to 100.";
            else if (!ExplosiveResponse && (!float.IsFinite(Impulse) || Impulse <= 0f))
                warning = "Simulated impact force needs a positive finite impulse.";
            else if (FilterLayers && Layers.value == 0)
                warning = "Select at least one layer for simulated impact force.";
            else if (Interactions != (PlayerToolImpactInteraction)(-1) && (Interactions & ~PlayerToolImpactInteraction.All) != 0)
                warning = "Select supported interactions for simulated impact force.";
            else if (FilterFlags)
            {
                if (FlagMatch is ObjectFlagMatch.Any or ObjectFlagMatch.All)
                    return ObjectFlagRules.TryValidate(Flags, false, out warning);
                warning = "Select a supported flag match for simulated impact force.";
            }
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
