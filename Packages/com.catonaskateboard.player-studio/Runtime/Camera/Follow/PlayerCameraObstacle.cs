using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Shortens a third-person camera arm using reusable query buffers and the player's physics scene.</summary>
    internal sealed class PlayerCameraObstacle
    {
        #region State

        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly Collider[] overlaps = new Collider[32];
        private float armLength;
        private bool hasArm;

        #endregion

        #region Methods

        #region Resolution

        /// <summary>Keeps the camera outside obstacles while ignoring its own player hierarchy.</summary>
        /// <param name="host">Player whose colliders are excluded.</param>
        /// <param name="settings">Validated query layer, radius and clearance values.</param>
        /// <param name="target">World point from which the camera arm extends.</param>
        /// <param name="desired">Unobstructed proposed camera position.</param>
        /// <param name="duration">Elapsed frame time used only for outward arm recovery.</param>
        /// <returns>The closest safe point along the arm; the target is used when query capacity is exceeded.</returns>
        public Vector3 Resolve(PlayerHost host, PlayerCameraSettings settings, Vector3 target, Vector3 desired, float duration)
        {
            // This query is fundamental only for enabled third-person collision avoidance.
            PhysicsScene physics = host.gameObject.scene.GetPhysicsScene();
            int count = physics.OverlapSphere(target, settings.CollisionRadius, overlaps, settings.ObstacleMask, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length)
                return Collapse(target);
            for (int index = 0; index < count; index++)
                if (!overlaps[index].transform.IsChildOf(host.transform))
                    return Collapse(target);

            Vector3 direction = desired - target;
            float distance = direction.magnitude;
            if (distance <= 0f)
                return Collapse(target);
            count = physics.SphereCast(target, settings.CollisionRadius, direction / distance, hits, distance,
                settings.ObstacleMask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length)
                return Collapse(target);
            for (int index = 0; index < count; index++)
                if (!hits[index].transform.IsChildOf(host.transform))
                    distance = Mathf.Min(distance, Mathf.Max(0f, hits[index].distance - settings.CollisionPadding));
            // Obstacle entry is immediate; only outward recovery receives an optional response time.
            armLength = hasArm && distance > armLength && settings.ObstacleReturnTime > 0f
                ? Mathf.Lerp(armLength, distance, 1f - Mathf.Exp(-duration / settings.ObstacleReturnTime)) : distance;
            hasArm = true;
            return target + direction.normalized * armLength;
        }

        /// <summary>Forgets the previous arm after a configuration or teleport boundary.</summary>
        public void Reset()
        {
            // Reinitialization starts from the next safe collision result.
            hasArm = false;
        }

        /// <summary>Records a fully obstructed arm before returning its focus point.</summary>
        /// <param name="target">Safe fallback at the focus.</param>
        /// <returns>The collapsed arm position.</returns>
        private Vector3 Collapse(Vector3 target)
        {
            // Recovery must start at zero after an overlap, rather than the previous unobstructed length.
            armLength = 0f;
            hasArm = true;
            return target;
        }

        #endregion

        #endregion
    }
}
