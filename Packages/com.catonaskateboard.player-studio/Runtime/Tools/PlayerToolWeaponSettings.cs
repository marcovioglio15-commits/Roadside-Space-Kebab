using System;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Selects physical projectile travel or immediate interaction with the cursor target.</summary>
    public enum PlayerToolWeaponMode { Projectile, CursorInteraction }

    /// <summary>Chooses the impact prefab's forward axis relative to the struck surface or bullet travel.</summary>
    public enum PlayerToolImpactOrientation { SurfaceNormal, TravelDirection, PrefabRotation }

    /// <summary>Chooses whether automatic placement faces the camera or retains the configured impact orientation.</summary>
    public enum PlayerToolImpactPlacementMode { FaceCamera, PreserveOrientation }

    /// <summary>Stores reusable weapon behavior without retaining scene transforms in tool assets.</summary>
    [Serializable]
    public sealed class PlayerToolWeaponSettings
    {
        #region Fields

        [Header("Firing")]
        [Tooltip("Fire a bullet, or use a valid interaction under the cursor. Cursor Interaction also works with the fake cursor.")]
        public PlayerToolWeaponMode Mode;
        [Tooltip("Hit targets beyond their interaction range. The weapon's Distance still limits the shot.")]
        public bool IgnoreInteractionRange = true;
        [Tooltip("Shots fired with one use. Stops sooner if the magazine runs out.")]
        [Range(1, 32)]
        public int ShotsPerUse = 1;
        [Tooltip("Seconds between shots in the same burst.")]
        [Range(0f, 2f)]
        public float ShotInterval = 0.08f;

        [Header("Shot Effects")]
        [Tooltip("Optional effect on the player's arm.")]
        public PlayerToolEffectSettings MuzzleEffect = new PlayerToolEffectSettings();
        [Tooltip("Optional burst at the firing point.")]
        public PlayerToolEffectSettings ShotEffect = new PlayerToolEffectSettings();

        [Header("Muzzle")]
        [Tooltip("Choose the part that holds the firing point. Leave empty to use Tools Hierarchy Root.")]
        public string MuzzlePath = string.Empty;
        [Tooltip("Offset of the firing point. Adjust it visually in Muzzle Preview.")]
        public Vector3 MuzzlePosition;
        [Tooltip("Rotation of the firing point. Positive Z points forward.")]
        public Vector3 MuzzleRotation;

        [Header("Bullet")]
        [Tooltip("Bullet prefab. Positive Z points forward. The tool handles its movement.")]
        public GameObject Bullet;
        [Tooltip("Bullet speed. In Cursor Interaction, this sets the speed used by interactions that push objects.")]
        [Range(0.1f, 300f)]
        public float Speed = 40f;
        [Tooltip("How far the shot can reach. Turn off Ignore Interaction Range to also use each target's range.")]
        [Range(0.1f, 1000f)]
        public float Distance = 100f;
        [Tooltip("Bullet collision radius. Use zero for a very thin shot.")]
        [Range(0f, 0.5f)]
        public float Radius;
        [Tooltip("Layers the shot can hit. The player and this tool's effects are ignored.")]
        public LayerMask Layers = ~0;
        [Tooltip("Let the shot hit triggers too.")]
        public bool HitTriggers;
        [Tooltip("Maximum bullets or effects active at once, for each effect type.")]
        [Range(1, 128)]
        public int Capacity = 16;

        [Header("Impact")]
        [Tooltip("Optional effect at the hit point. Cursor mode shows it only for a valid interaction.")]
        public GameObject Impact;
        [Tooltip("Move the impact effect out of nearby surfaces automatically.")]
        public bool AutomaticImpactPlacement;
        [Tooltip("Face the camera, or keep the rotation chosen below.")]
        public PlayerToolImpactPlacementMode ImpactPlacementMode;
        [Tooltip("How far the effect can overlap a surface. Use zero to keep it fully clear.")]
        [Range(0f, 0.5f)]
        public float ImpactPenetration;
        [Tooltip("Size of the impact effect.")]
        [Range(0.01f, 5f)]
        public float ImpactScale = 1f;
        [Tooltip("Seconds before the impact effect disappears.")]
        [Range(0.01f, 30f)]
        public float ImpactLifetime = 1f;
        [Tooltip("Face away from the surface, follow the shot, or keep the prefab's rotation.")]
        public PlayerToolImpactOrientation ImpactOrientation;
        [Tooltip("Keep billboard particles flat against the hit surface.")]
        public bool ImpactSurfaceAligned;
        [Tooltip("Extra rotation for the impact effect, in degrees.")]
        public Vector3 ImpactRotation;
        [Tooltip("Move the effect this far away from the hit surface.")]
        [Range(0f, 0.5f)]
        public float ImpactOffset = 0.005f;
        [Tooltip("Show the impact effect even when the bullet reaches its limit without a hit.")]
        public bool ImpactAtRange;

        [Header("Aim")]
        [Tooltip("Turn the arm toward the cursor. Uses the screen center while the cursor is locked.")]
        public bool AutoAim;
        [Tooltip("Arm pivot to rotate. It must contain the firing point. Choose a child of the player.")]
        public string AimPath = string.Empty;
        [Tooltip("How far to look for an aiming target.")]
        [Range(1f, 1000f)]
        public float AimDistance = 100f;
        [Tooltip("How quickly the arm turns. Use zero to turn instantly.")]
        [Range(0f, 40f)]
        public float AimResponse = 15f;
        [Tooltip("Move the arm back or sideways to avoid walls while aiming.")]
        public bool RepositionAim;
        [Tooltip("How far the arm can move from its starting position.")]
        [Range(0.1f, 3f)]
        public float RepositionDistance = 1.5f;
        [Tooltip("Room to keep clear around the arm and firing point.")]
        [Range(0.01f, 0.3f)]
        public float RepositionRadius = 0.06f;
        [Tooltip("How quickly the arm moves around walls. Lower values feel softer.")]
        [Range(1f, 40f)]
        public float RepositionResponse = 18f;

        [Header("Shot Alignment")]
        [Tooltip("Adjust the arm before firing to bring the impact closer to the cursor.")]
        public bool AlignBeforeShot;
        [Tooltip("Seconds to move into the firing pose.")]
        [Range(0.03f, 0.5f)]
        public float ShotAlignmentDuration = 0.12f;
        [Tooltip("Positions checked before firing. More checks can improve aim near corners, but cost more.")]
        [Range(8, 48)]
        public int ShotAlignmentSamples = 24;

        #endregion

        #region Properties

        /// <summary>Whether manual or automatic placement uses the configured impact orientation and additional rotation.</summary>
        internal bool UsesImpactOrientation => !AutomaticImpactPlacement || ImpactPlacementMode == PlayerToolImpactPlacementMode.PreserveOrientation;
        /// <summary>Whether all dependencies for the optional preparation phase are enabled.</summary>
        internal bool UsesShotAlignment => Mode == PlayerToolWeaponMode.Projectile && AlignBeforeShot && AutoAim && RepositionAim && AutomaticImpactPlacement && Impact != null;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks the selected firing mode, muzzle coordinates and enabled optional settings.</summary>
        /// <param name="warning">Receives the first invalid weapon setting.</param>
        /// <returns>True when the selected weapon mode and its enabled effects can be prepared.</returns>
        public bool TryValidate(out string warning)
        {
            // Hidden impact and aim values are checked only when those behaviors are used.
            warning = "Choose a supported weapon mode, 1 to 32 shots, a non-negative interval, finite muzzle coordinates, positive distance and a capacity from 1 to 128.";
            if (Mode is not (PlayerToolWeaponMode.Projectile or PlayerToolWeaponMode.CursorInteraction)
                || ShotsPerUse < 1 || ShotsPerUse > 32 || ShotsPerUse > 1 && !NonNegative(ShotInterval)
                || MuzzlePath == null || !Finite(MuzzlePosition) || !Finite(MuzzleRotation)
                || !Positive(Distance) || Capacity < 1 || Capacity > 128)
                return false;
            warning = "Use a positive speed. Projectile mode also needs a bullet prefab and a non-negative collision radius.";
            if (!Positive(Speed) || Mode == PlayerToolWeaponMode.Projectile && (Bullet == null || !NonNegative(Radius)))
                return false;
            if (MuzzleEffect != null && !MuzzleEffect.TryValidate(true, out warning)
                || ShotEffect != null && !ShotEffect.TryValidate(false, out warning))
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

        /// <summary>Checks configured coordinates before they reach Unity transforms.</summary>
        /// <param name="value">Position or Euler vector.</param>
        /// <returns>True for finite coordinates.</returns>
        public static bool Finite(Vector3 value)
        {
            // Squared magnitude can overflow even when individual coordinates are finite.
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        /// <summary>Checks positive distances and durations without changing the source.</summary>
        /// <param name="value">configured scalar.</param>
        /// <returns>True for finite values greater than zero.</returns>
        private static bool Positive(float value)
        {
            return float.IsFinite(value) && value > 0f;
        }

        /// <summary>Checks optional radius, response and offset values.</summary>
        /// <param name="value">configured scalar.</param>
        /// <returns>True for finite values at least zero.</returns>
        private static bool NonNegative(float value)
        {
            return float.IsFinite(value) && value >= 0f;
        }

        #endregion

        #endregion
    }
}
