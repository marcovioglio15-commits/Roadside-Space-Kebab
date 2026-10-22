using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Blends into a forecast shooting pose before a queued bullet is allowed to leave its muzzle.</summary>
    internal sealed class PlayerToolShotAlignment
    {
        #region State

        private readonly PlayerToolWeaponSettings settings;
        private Vector2 origin;
        private Vector2 velocity;
        private Vector2 destination;
        private Vector2 motion;
        private Quaternion rotation;
        private Vector3 targetPoint;
        private Vector3 targetFrame;
        private Collider targetCollider;
        private float elapsed;
        private bool started;
        private bool valid;
        private bool replanned;

        #endregion

        #region Properties

        /// <summary>Whether a feasible preparation pose is currently being presented.</summary>
        internal bool Active => valid;
        /// <summary>Current horizontal blend velocity, retained when a changed target requires replanning.</summary>
        internal Vector2 Velocity => motion;
        /// <summary>Whether preparation is complete or no usable physical target required it.</summary>
        internal bool Ready => started && (!valid || elapsed >= settings.ShotAlignmentDuration);
        /// <summary>Quintic easing with zero acceleration at each endpoint.</summary>
        internal float Weight
        {
            get
            {
                float time = Mathf.Clamp01(elapsed / settings.ShotAlignmentDuration);
                return time * time * time * (10f + time * (-15f + time * 6f));
            }
        }

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Retains configuration without allocating per-shot arrays or visuals.</summary>
        /// <param name="weapon">Validated preparation settings.</param>
        internal PlayerToolShotAlignment(PlayerToolWeaponSettings weapon)
        {
            settings = weapon;
        }

        /// <summary>Releases preparation after emission, cancellation or parking the tool.</summary>
        internal void Reset()
        {
            started = valid = replanned = false;
            targetCollider = null;
            motion = Vector2.zero;
            elapsed = 0f;
        }

        #endregion

        #region Presentation

        /// <summary>Searches at activation and allows one smooth replan when the cursor's target changes.</summary>
        /// <param name="query">Current camera and barrel constraints.</param>
        /// <param name="projectiles">Shared impact forecaster.</param>
        /// <param name="current">Displayed horizontal offset before preparation.</param>
        /// <param name="currentVelocity">Retained horizontal motion at activation.</param>
        /// <param name="currentRotation">Displayed additive aim rotation.</param>
        /// <param name="target">Current physical cursor point.</param>
        /// <param name="localTarget">Cursor point in the current camera frame, used to detect a meaningful target change.</param>
        /// <param name="collider">Current physical cursor collider, or null for empty space.</param>
        /// <param name="delta">Scaled render-frame duration.</param>
        /// <returns>The prepared horizontal displacement for this frame.</returns>
        internal Vector2 Sample(PlayerToolAimQuery query, PlayerToolProjectiles projectiles, Vector2 current, Vector2 currentVelocity,
            Quaternion currentRotation, Vector3 target, Vector3 localTarget, Collider collider, float delta)
        {
            // One replan updates changed geometry without allowing a moving target to delay a shot indefinitely.
            if (!started || valid && !replanned && (collider != targetCollider || (target - targetPoint).sqrMagnitude > 0.0025f
                || (localTarget - targetFrame).sqrMagnitude > 0.0025f))
            {
                replanned = started;
                started = true;
                elapsed = 0f;
                origin = current;
                velocity = currentVelocity;
                rotation = currentRotation;
                targetPoint = target;
                targetFrame = localTarget;
                targetCollider = collider;
                valid = collider != null && PlayerToolShotSearch.TryFind(query, projectiles, settings, current, target, collider, out destination);
            }
            if (!valid)
                return current;
            elapsed = Mathf.Min(settings.ShotAlignmentDuration, elapsed + delta);
            float time = elapsed / settings.ShotAlignmentDuration;
            // Preserve incoming velocity while bringing both velocity and acceleration to zero at the shooting pose.
            float momentum = time * (1f + time * time * (-6f + time * (8f - 3f * time)));
            motion = (destination - origin) * (30f * time * time * (1f - time) * (1f - time) / settings.ShotAlignmentDuration)
                + velocity * (1f + time * time * (-18f + time * (32f - 15f * time)));
            return Vector2.ClampMagnitude(Vector2.LerpUnclamped(origin, destination, Weight)
                + velocity * (settings.ShotAlignmentDuration * momentum), settings.RepositionDistance);
        }

        /// <summary>Reaches exact visual aim with the same easing used for arm translation.</summary>
        /// <param name="destinationRotation">Current exact additive aim correction.</param>
        /// <returns>The prepared additive rotation.</returns>
        internal Quaternion Orient(Quaternion destinationRotation)
        {
            return Quaternion.Slerp(rotation, destinationRotation, Weight);
        }

        #endregion

        #endregion
    }
}
