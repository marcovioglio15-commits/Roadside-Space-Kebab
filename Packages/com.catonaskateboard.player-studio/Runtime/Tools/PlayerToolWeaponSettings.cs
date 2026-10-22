using System;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Chooses the impact prefab's forward axis relative to the struck surface or bullet travel.</summary>
    public enum PlayerToolImpactOrientation { SurfaceNormal, TravelDirection, PrefabRotation }

    /// <summary>Chooses whether automatic placement faces the camera or retains the configured impact orientation.</summary>
    public enum PlayerToolImpactPlacementMode { FaceCamera, PreserveOrientation }

    /// <summary>Stores reusable weapon behavior without retaining scene transforms in tool assets.</summary>
    [Serializable]
    public sealed class PlayerToolWeaponSettings
    {
        #region Fields

        [Header("Muzzle")]
        [Tooltip("Existing transform below Tools Hierarchy Root that carries the muzzle offset. Empty selects that root.")]
        public string MuzzlePath = string.Empty;
        [Tooltip("Muzzle position relative to its selected transform, editable through Muzzle Preview.")]
        public Vector3 MuzzlePosition;
        [Tooltip("Muzzle rotation relative to its selected transform. Positive Z is the firing direction.")]
        public Vector3 MuzzleRotation;

        [Header("Bullet")]
        [Tooltip("Bullet prefab facing positive Z. Use visual components and optional kinematic bodies; movement is handled by Tools.")]
        public GameObject Bullet;
        [Tooltip("Straight travel speed in world units per scaled second.")]
        [Range(0.1f, 300f)]
        public float Speed = 40f;
        [Tooltip("Maximum world distance travelled before the bullet returns to its pool.")]
        [Range(0.1f, 1000f)]
        public float Distance = 100f;
        [Tooltip("Swept collision radius in world units. Zero uses a ray, suitable for very small bullets.")]
        [Range(0f, 0.5f)]
        public float Radius;
        [Tooltip("Only colliders on these layers stop bullets. The firing player and its pooled effects are ignored.")]
        public LayerMask Layers = ~0;
        [Tooltip("Whether trigger colliders on the selected layers also stop bullets.")]
        public bool HitTriggers;
        [Tooltip("Maximum simultaneous bullets and impact effects per player for this tool. Objects are allocated once at initialization.")]
        [Range(1, 128)]
        public int Capacity = 16;

        [Header("Impact")]
        [Tooltip("Optional impact prefab reused at collision points for the configured lifetime.")]
        public GameObject Impact;
        [Tooltip("Find free space for the impact along the incoming shot trajectory. Placement Mode chooses its orientation; the manual offset is unused.")]
        public bool AutomaticImpactPlacement;
        [Tooltip("Face Camera turns the effect toward the view. Preserve Orientation keeps Impact Orientation and Impact Rotation while finding free space.")]
        public PlayerToolImpactPlacementMode ImpactPlacementMode;
        [Tooltip("Allowed overlap in world units when fitting the measured effect bounds. Zero keeps full clearance. Bullet collisions remain unchanged.")]
        [Range(0f, 0.5f)]
        public float ImpactPenetration;
        [Tooltip("Uniform multiplier for the pooled impact's size and particle travel. The source prefab remains unchanged.")]
        [Range(0.01f, 5f)]
        public float ImpactScale = 1f;
        [Tooltip("Scaled seconds before an impact effect returns to its pool.")]
        [Range(0.01f, 30f)]
        public float ImpactLifetime = 1f;
        [Tooltip("Orient positive Z along the surface normal, along bullet travel, or retain the prefab rotation.")]
        public PlayerToolImpactOrientation ImpactOrientation;
        [Tooltip("Align ordinary billboard particles with the impact transform, keeping surface flashes on the struck plane instead of facing the camera.")]
        public bool ImpactSurfaceAligned;
        [Tooltip("Additional Euler rotation applied after the selected impact orientation.")]
        public Vector3 ImpactRotation;
        [Tooltip("Offset from a struck surface along its normal to avoid overlapping the surface.")]
        [Range(0f, 0.5f)]
        public float ImpactOffset = 0.005f;
        [Tooltip("Also show the impact prefab when the bullet reaches its maximum distance without hitting anything.")]
        public bool ImpactAtRange;

        [Header("Aim")]
        [Tooltip("Rotate an existing visual toward the camera pointer, or screen centre while the cursor is locked.")]
        public bool AutoAim;
        [Tooltip("Visual pivot below Tools Hierarchy Root. It must contain the muzzle transform and must not be the player root.")]
        public string AimPath = string.Empty;
        [Tooltip("Maximum camera ray distance used to find the cursor target.")]
        [Range(1f, 1000f)]
        public float AimDistance = 100f;
        [Tooltip("Rotation response per scaled second. Zero aligns immediately. Bullets always travel toward the resolved cursor target.")]
        [Range(0f, 40f)]
        public float AimResponse = 15f;
        [Tooltip("Retract or shift the aim pivot sideways on the player's horizontal plane to clear solids while retaining cursor aim.")]
        public bool RepositionAim;
        [Tooltip("Maximum horizontal displacement from the authored arm pose in world units.")]
        [Range(0.1f, 3f)]
        public float RepositionDistance = 1.5f;
        [Tooltip("Clearance radius around the pivot-to-muzzle segment used to check solid obstacles.")]
        [Range(0.01f, 0.3f)]
        public float RepositionRadius = 0.06f;
        [Tooltip("Response of the damped arm movement when avoiding obstacles and returning to its authored position. Lower values give slower, softer motion.")]
        [Range(1f, 40f)]
        public float RepositionResponse = 18f;

        [Header("Shot Alignment")]
        [Tooltip("Before firing, move the arm to a visible clear pose whose predicted impact comes closest to the cursor target. Requires Auto Aim, Reposition Aim and automatic impact placement.")]
        public bool AlignBeforeShot;
        [Tooltip("Seconds to reach the selected shooting pose before emitting the bullet. A changed target can trigger one additional smooth adjustment.")]
        [Range(0.03f, 0.5f)]
        public float ShotAlignmentDuration = 0.12f;
        [Tooltip("Horizontal search samples used before local refinement. More samples can find a closer impact near corners but require more physics queries.")]
        [Range(8, 48)]
        public int ShotAlignmentSamples = 24;

        #endregion

        #region Properties

        /// <summary>Whether manual or automatic placement uses the authored impact orientation and additional rotation.</summary>
        internal bool UsesImpactOrientation => !AutomaticImpactPlacement || ImpactPlacementMode == PlayerToolImpactPlacementMode.PreserveOrientation;
        /// <summary>Whether all dependencies for the optional preparation phase are enabled.</summary>
        internal bool UsesShotAlignment => AlignBeforeShot && AutoAim && RepositionAim && AutomaticImpactPlacement && Impact != null;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Rejects unusable projectiles, muzzle coordinates and enabled optional settings.</summary>
        /// <param name="warning">Receives the first invalid weapon setting.</param>
        /// <returns>True when a bounded straight projectile pool can be created.</returns>
        public bool TryValidate(out string warning)
        {
            // Hidden impact and aim values are checked only when those behaviors are used.
            warning = "Assign a bullet prefab, finite muzzle coordinates, positive speed/distance and a capacity from 1 to 128.";
            if (Bullet == null || MuzzlePath == null || !Finite(MuzzlePosition) || !Finite(MuzzleRotation)
                || !Positive(Speed) || !Positive(Distance) || !NonNegative(Radius) || Capacity < 1 || Capacity > 128)
                return false;
            warning = "Use positive impact scale/lifetime, supported placement/orientation modes, finite rotation and non-negative enabled overlap/offset values.";
            if (Impact != null && (!Positive(ImpactScale) || !Positive(ImpactLifetime)
                || AutomaticImpactPlacement && ImpactPlacementMode is not (PlayerToolImpactPlacementMode.FaceCamera or PlayerToolImpactPlacementMode.PreserveOrientation)
                || AutomaticImpactPlacement && !NonNegative(ImpactPenetration)
                || UsesImpactOrientation && (!Finite(ImpactRotation) || ImpactOrientation is not (PlayerToolImpactOrientation.SurfaceNormal
                    or PlayerToolImpactOrientation.TravelDirection or PlayerToolImpactOrientation.PrefabRotation))
                || !AutomaticImpactPlacement && !NonNegative(ImpactOffset)))
                return false;
            warning = "Auto Aim needs a hierarchy pivot, positive distance and non-negative response. Enabled repositioning needs positive distance, radius and response. All values must be finite.";
            if (AutoAim && (AimPath == null || !Positive(AimDistance) || !NonNegative(AimResponse)
                || RepositionAim && (!Positive(RepositionDistance) || !Positive(RepositionRadius) || !Positive(RepositionResponse))))
                return false;
            warning = "Shot alignment needs a positive finite duration and a sample count from 8 to 48.";
            if (UsesShotAlignment && (!Positive(ShotAlignmentDuration) || ShotAlignmentSamples < 8 || ShotAlignmentSamples > 48))
                return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Checks authored coordinates before they reach Unity transforms.</summary>
        /// <param name="value">Position or Euler vector.</param>
        /// <returns>True for finite coordinates.</returns>
        public static bool Finite(Vector3 value)
        {
            // Squared magnitude can overflow even when individual coordinates are finite.
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        /// <summary>Checks positive distances and durations without changing the source.</summary>
        /// <param name="value">Authored scalar.</param>
        /// <returns>True for finite values greater than zero.</returns>
        private static bool Positive(float value)
        {
            return float.IsFinite(value) && value > 0f;
        }

        /// <summary>Checks optional radius, response and offset values.</summary>
        /// <param name="value">Authored scalar.</param>
        /// <returns>True for finite values at least zero.</returns>
        private static bool NonNegative(float value)
        {
            return float.IsFinite(value) && value >= 0f;
        }

        #endregion

        #endregion
    }
}
