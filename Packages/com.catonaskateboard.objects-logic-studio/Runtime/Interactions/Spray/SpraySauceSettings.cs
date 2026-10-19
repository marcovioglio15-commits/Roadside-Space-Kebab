using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses held emission or a complete burst started by one button press.</summary>
    public enum SprayEmissionMode { Hold, TimedBurst }

    /// <summary>Controls repeated presses during preparation or automatic emission.</summary>
    public enum SprayRepeatPolicy { Ignore, ResetDuration, RepeatSqueeze, RepeatSqueezeAndReset }

    /// <summary>Stores a nozzle pose independently of mesh and collider transforms.</summary>
    [Serializable]
    public sealed class SprayNozzle
    {
        #region Fields
        [Header("Nozzle Pose")]
        [Tooltip("Outlet position in object-local coordinates.")]
        public Vector3 Position;
        [Tooltip("Outlet rotation in local Euler degrees; its forward axis emits liquid.")]
        public Vector3 Rotation;
        #endregion
        #region Methods
        /// <summary>Checks pose coordinates before preview handles or physical emission use them.</summary>
        /// <returns>True for finite local position and rotation.</returns>
        public bool IsValid()
        {
            return InteractionValues.Finite(Position) && InteractionValues.Finite(Rotation);
        }
        #endregion
    }

    /// <summary>Defines continuous physical emission, optional capacity and surface reactions.</summary>
    [Serializable]
    public sealed class SpraySauceSettings
    {
        #region Fields
        [Header("Input")]
        [Tooltip("Hold emits while pressed. Timed Burst completes the optional squeeze before emitting automatically for its duration.")]
        public SprayEmissionMode Emission;
        [Tooltip("Seconds of actual liquid emission per button-triggered burst; squeeze time does not consume this duration.")]
        [Range(0.05f, 30f)]
        public float BurstDuration = 1f;
        [Tooltip("Ignore consumes extra presses without changing the burst. Reset Duration refills its timer. Repeat Squeeze briefly releases and reapplies pressure, optionally resetting the timer as well.")]
        public SprayRepeatPolicy Repeat = SprayRepeatPolicy.Ignore;
        [Tooltip("Allow emission only while this object occupies the player's carry slot.")]
        public bool HeldOnly = true;
        [Tooltip("Reach and aiming used when emission does not require carrying the object.")]
        public TransferTargetSettings Target = new TransferTargetSettings();
        [Header("Nozzle")]
        [Tooltip("Object-local outlet pose edited in the nozzle layout window.")]
        public SprayNozzle Nozzle = new SprayNozzle();
        [Tooltip("Physical samples emitted each second along the continuous jet. The shared pool bounds simultaneous liquid bodies.")]
        [Range(1f, 120f)]
        public float Rate = 50f;
        [Tooltip("Minimum and maximum initial speed in metres per second.")]
        public Vector2 Speed = new Vector2(7f, 8f);
        [Tooltip("Multiplier for the initial forward speed. Higher values launch the jet farther before it falls, without changing emission rate or inherited movement.")]
        [Range(0.1f, 5f)]
        public float LaunchPower = 1f;
        [Tooltip("Cone half-angle in degrees.")]
        [Range(0f, 45f)]
        public float Spread = 1.5f;
        [Tooltip("Continuity of the liquid column. Higher values reduce speed, radius and direction variation between neighbouring samples; zero emits separate droplets.")]
        [Range(0f, 1f)]
        public float Cohesion = 0.95f;
        [Tooltip("Seconds of flight before the continuous column breaks into droplets. Gravity suspension preserves momentum but does not stop this breakup timer.")]
        [Range(0.01f, 5f)]
        public float BreakupTime = 0.65f;
        [Tooltip("Maximum stretch between adjacent samples relative to their current travel speed. Falling liquid can accelerate without breaking the column; unusually large gaps still disconnect.")]
        [Range(1f, 10f)]
        public float StretchLimit = 3f;
        [Tooltip("Minimum and maximum physical droplet radius in metres.")]
        public Vector2 Radius = new Vector2(0.01f, 0.012f);
        [Tooltip("Mass of each droplet in kilograms.")]
        [Range(0.0001f, 0.1f)]
        public float Mass = 0.003f;
        [Tooltip("Linear damping of airborne droplets.")]
        [Range(0f, 5f)]
        public float Damping = 0.1f;
        [Tooltip("Maximum airborne lifetime in seconds.")]
        [Range(0.1f, 10f)]
        public float Lifetime = 3f;
        [Tooltip("Fraction of the outlet's movement inherited by newly emitted droplets.")]
        [Range(0f, 2f)]
        public float InheritVelocity = 1f;
        [Tooltip("Allow gravity generators to suspend emitted liquid and apply their forces. Disable to keep normal liquid gravity even when the source is suspended. Captured separately for each emitted sample.")]
        public bool AllowGravitySuspension = true;
        [Tooltip("Layers that can receive liquid. The source hierarchy is always excluded.")]
        public LayerMask Layers = ~0;
        [Header("Capacity")]
        [Tooltip("Stop after the configured total emission time; disabling the component does not refill it.")]
        public bool Limited;
        [Tooltip("Available seconds of actual emission for a newly spawned object.")]
        [Range(0.1f, 120f)]
        public float Capacity = 10f;
        [Header("Surface")]
        [Tooltip("Leave smoothly fading marks on contacted surfaces.")]
        public bool LeaveTrail = true;
        [Tooltip("Minimum seconds between surface deposits from this source. Exposure timing still receives every droplet hit.")]
        [Range(0.01f, 1f)]
        public float DepositInterval = 0.08f;
        [Tooltip("Colour composition, wetness and surface deposit appearance.")]
        public SurfaceTrailSettings Trail = new SurfaceTrailSettings
        {
            Liquidity = 1f, Shape = SurfaceMarkShape.Rounded, DetectColors = false,
            Colors = new[] { new Color(0.75f, 0.06f, 0.025f), new Color(0.95f, 0.18f, 0.04f) },
            Patches = 1, Radius = new Vector2(0.025f, 0.045f), Scatter = 0.01f
        };
        [Tooltip("Apply appearance changes after sustained exposure on matching objects.")]
        public bool ChangeAppearance;
        [Tooltip("Ordered identity filters and exposure thresholds. The first matching rule controls each receiving object.")]
        public SpraySurfaceRule[] Rules = Array.Empty<SpraySurfaceRule>();
        [Header("Squeeze")]
        [Tooltip("Use Elastic Deformation to gently compress the source while emitting.")]
        public bool Squeeze = true;
        [Tooltip("Object-local compression axis.")]
        public Vector3 SqueezeAxis = Vector3.right;
        [Tooltip("Compression fraction while the nozzle is emitting.")]
        [Range(0f, 0.65f)]
        public float Compression = 0.025f;
        [Tooltip("Seconds to reach or release the authored compression.")]
        [Range(0.01f, 2f)]
        public float SqueezeDuration = 0.15f;
        #endregion
        #region Methods
        #region Validation
        /// <summary>Rejects invalid emission data without changing authored values.</summary>
        /// <param name="warning">Receives the first invalid configuration.</param>
        /// <returns>True when physics, appearance and capacity settings are complete.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Select a supported emission and repeat policy; a timed burst needs a positive duration.";
            if (Emission is not (SprayEmissionMode.Hold or SprayEmissionMode.TimedBurst)
                || Emission == SprayEmissionMode.TimedBurst && (!InteractionValues.Positive(BurstDuration)
                    || Repeat is not (SprayRepeatPolicy.Ignore or SprayRepeatPolicy.ResetDuration or SprayRepeatPolicy.RepeatSqueeze or SprayRepeatPolicy.RepeatSqueezeAndReset)))
                return false;
            warning = "Use a valid nozzle, positive rate (up to 120), radius, speed, launch power, mass and lifetime. A coherent jet also needs positive breakup time and stretch limit of at least one.";
            if (Nozzle == null || !InteractionValues.Finite(Nozzle.Position) || !InteractionValues.Finite(Nozzle.Rotation)
                || !InteractionValues.Positive(Rate) || Rate > 120f || !SurfaceValues.Range(Speed)
                || !InteractionValues.Positive(LaunchPower) || !float.IsFinite(Speed.y * LaunchPower)
                || !SurfaceValues.Range(Radius) || !InteractionValues.Positive(Mass) || !InteractionValues.Positive(Lifetime)
                || !SurfaceValues.Nonnegative(Damping) || !SurfaceValues.Nonnegative(InheritVelocity)
                || !SurfaceValues.Nonnegative(Spread) || Spread > 89f || !SurfaceValues.Unit(Cohesion) || Layers.value == 0
                || Cohesion > 0f && (!InteractionValues.Positive(BreakupTime) || !float.IsFinite(StretchLimit) || StretchLimit < 1f))
                return false;
            warning = "Capacity must be positive; squeeze needs a nonzero axis, compression below 65 percent and a positive duration.";
            if (Limited && !InteractionValues.Positive(Capacity) || UsesDeposits && !InteractionValues.Positive(DepositInterval)
                || Squeeze && (!InteractionValues.Finite(SqueezeAxis) || SqueezeAxis.sqrMagnitude < 0.000001f
                    || !SurfaceValues.Nonnegative(Compression) || Compression > 0.65f || !InteractionValues.Positive(SqueezeDuration)))
                return false;
            warning = "Configure the liquid appearance and targeting settings.";
            if (Trail == null || !Trail.TryValidate(out warning, UsesDeposits) || !HeldOnly && (Target == null || !Target.TryValidate(out warning)))
                return false;
            warning = "Add at least one complete surface rule when appearance changes are enabled.";
            if (ChangeAppearance)
            {
                if (Rules == null || Rules.Length == 0)
                    return false;
                foreach (SpraySurfaceRule rule in Rules)
                    if (rule == null || !rule.TryValidate(out warning))
                        return false;
            }
            warning = string.Empty;
            return true;
        }
        #endregion
        #endregion
        #region Properties
        /// <summary>Whether normal deposits or an identity rule needs surface geometry.</summary>
        public bool UsesDeposits
        {
            get
            {
                if (LeaveTrail)
                    return true;
                if (ChangeAppearance && Rules != null)
                    foreach (SpraySurfaceRule rule in Rules)
                        if (rule != null && rule.TrailMode != SprayTrailMode.Normal)
                            return true;
                return false;
            }
        }
        #endregion
    }
}
