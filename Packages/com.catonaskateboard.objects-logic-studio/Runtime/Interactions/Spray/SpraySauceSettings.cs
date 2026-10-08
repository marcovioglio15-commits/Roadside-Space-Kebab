using System;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
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

    /// <summary>Changes an existing item's appearance after sustained contact with sauce.</summary>
    [Serializable]
    public sealed class SpraySurfaceRule
    {
        #region Fields
        [Header("Surface Rule")]
        [Tooltip("Identity flags required on the receiving object.")]
        public ObjectFlag[] Flags = Array.Empty<ObjectFlag>();
        [Tooltip("Accept any selected flag or require every selected flag.")]
        public ObjectFlagMatch Match;
        [Tooltip("Seconds of continuous wetting required before applying the appearance.")]
        public float Exposure = 0.5f;
        [Tooltip("Maximum gap between droplets before the exposure restarts.")]
        public float ContactGap = 0.2f;
        [Tooltip("Mesh, material and visibility changes on the receiving item's existing hierarchy.")]
        public ItemAppearanceSettings Appearance = new ItemAppearanceSettings();
        #endregion
    }

    /// <summary>Defines continuous physical emission, optional capacity and surface reactions.</summary>
    [Serializable]
    public sealed class SpraySauceSettings
    {
        #region Fields
        [Header("Input")]
        [Tooltip("Allow emission only while this object occupies the player's carry slot.")]
        public bool HeldOnly = true;
        [Tooltip("Reach and aiming used when emission does not require carrying the object.")]
        public TransferTargetSettings Target = new TransferTargetSettings();
        [Header("Nozzle")]
        [Tooltip("Object-local outlet pose edited in the nozzle layout window.")]
        public SprayNozzle Nozzle = new SprayNozzle();
        [Tooltip("Physical droplets emitted each second. The shared pool bounds simultaneous droplets.")]
        public float Rate = 35f;
        [Tooltip("Minimum and maximum initial speed in metres per second.")]
        public Vector2 Speed = new Vector2(4f, 5f);
        [Tooltip("Cone half-angle in degrees.")]
        public float Spread = 4f;
        [Tooltip("Visual connection between successive droplets. Zero shows separate drops; one forms a soft continuous jet without enlarging collision shapes.")]
        [Range(0f, 1f)]
        public float Cohesion = 0.8f;
        [Tooltip("Minimum and maximum physical droplet radius in metres.")]
        public Vector2 Radius = new Vector2(0.008f, 0.012f);
        [Tooltip("Mass of each droplet in kilograms.")]
        public float Mass = 0.003f;
        [Tooltip("Linear damping of airborne droplets.")]
        public float Damping = 0.1f;
        [Tooltip("Maximum airborne lifetime in seconds.")]
        public float Lifetime = 3f;
        [Tooltip("Fraction of the outlet's movement inherited by newly emitted droplets.")]
        public float InheritVelocity = 1f;
        [Tooltip("Layers that can receive liquid. The source hierarchy is always excluded.")]
        public LayerMask Layers = ~0;
        [Header("Capacity")]
        [Tooltip("Stop after the configured total emission time; disabling the component does not refill it.")]
        public bool Limited;
        [Tooltip("Available seconds of actual emission for a newly spawned object.")]
        public float Capacity = 10f;
        [Header("Surface")]
        [Tooltip("Leave smoothly fading marks on contacted surfaces.")]
        public bool LeaveTrail = true;
        [Tooltip("Minimum seconds between surface deposits from this source. Exposure timing still receives every droplet hit.")]
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
        public float Compression = 0.025f;
        [Tooltip("Seconds to reach or release the authored compression.")]
        public float SqueezeDuration = 0.15f;
        #endregion
        #region Methods
        #region Validation
        /// <summary>Rejects invalid emission data without changing authored values.</summary>
        /// <param name="warning">Receives the first invalid configuration.</param>
        /// <returns>True when physics, appearance and capacity settings are complete.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Use a valid nozzle, positive rate (up to 120), radius, speed, mass and lifetime.";
            if (Nozzle == null || !InteractionValues.Finite(Nozzle.Position) || !InteractionValues.Finite(Nozzle.Rotation)
                || !InteractionValues.Positive(Rate) || Rate > 120f || !SurfaceValues.Range(Speed)
                || !SurfaceValues.Range(Radius) || !InteractionValues.Positive(Mass) || !InteractionValues.Positive(Lifetime)
                || !SurfaceValues.Nonnegative(Damping) || !SurfaceValues.Nonnegative(InheritVelocity)
                || !SurfaceValues.Nonnegative(Spread) || Spread > 89f || !SurfaceValues.Unit(Cohesion) || Layers.value == 0)
                return false;
            warning = "Capacity must be positive; squeeze needs a nonzero axis, compression below 65 percent and a positive duration.";
            if (Limited && !InteractionValues.Positive(Capacity) || LeaveTrail && !InteractionValues.Positive(DepositInterval)
                || Squeeze && (!InteractionValues.Finite(SqueezeAxis) || SqueezeAxis.sqrMagnitude < 0.000001f
                    || !SurfaceValues.Nonnegative(Compression) || Compression > 0.65f || !InteractionValues.Positive(SqueezeDuration)))
                return false;
            warning = "Configure the liquid appearance and targeting settings.";
            if (Trail == null || !Trail.TryValidate(out warning, LeaveTrail) || !HeldOnly && (Target == null || !Target.TryValidate(out warning)))
                return false;
            warning = "Add at least one complete surface rule when appearance changes are enabled.";
            if (ChangeAppearance)
            {
                if (Rules == null || Rules.Length == 0)
                    return false;
                foreach (SpraySurfaceRule rule in Rules)
                    if (rule == null || !InteractionValues.Positive(rule.Exposure) || !InteractionValues.Positive(rule.ContactGap)
                        || rule.Match is not (ObjectFlagMatch.Any or ObjectFlagMatch.All)
                        || !ObjectFlagRules.TryValidate(rule.Flags, false, out warning) || rule.Appearance == null
                        || !rule.Appearance.HasChanges || !rule.Appearance.TryValidate(out warning))
                        return false;
            }
            warning = string.Empty;
            return true;
        }
        #endregion
        #endregion
    }
}
