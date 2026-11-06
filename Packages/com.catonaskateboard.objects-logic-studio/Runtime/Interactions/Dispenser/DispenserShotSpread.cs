using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Builds an outward burst with enough lift and tumble for flat and heavy outputs.</summary>
    internal readonly struct DispenserShotSpread
    {
        #region State

        private readonly Vector3 incoming;
        private readonly float phase;
        private readonly float speed;
        private readonly float launchScale;

        #endregion

        #region Methods

        #region Launch

        /// <summary>Captures a small shot bias and a strong minimum launch speed.</summary>
        /// <param name="velocity">Incoming contact velocity in world space.</param>
        /// <param name="distanceMultiplier">Relative free-flight distance for the burst.</param>
        internal DispenserShotSpread(Vector3 velocity, float distanceMultiplier) : this()
        {
            // The shot influences the fan but cannot send it straight back into the dispenser.
            float magnitude = velocity.magnitude;
            if (!float.IsFinite(magnitude) || magnitude < 0.001f)
                return;
            incoming = Vector3.ProjectOnPlane(velocity, Vector3.up).normalized;
            speed = 4.3f + Mathf.Min(magnitude * 0.065f, 1.7f);
            phase = Random.Range(-7f, 7f);
            // Keep the fan's angles and tumble while scaling free-flight range at equal heights.
            launchScale = Mathf.Sqrt(distanceMultiplier);
        }

        /// <summary>Fans outputs away from the outlet with distinct speeds, lift and roll.</summary>
        /// <param name="pivot">Dispenser world pivot.</param>
        /// <param name="outlet">World position of the selected outlet.</param>
        /// <param name="fallback">Dispenser forward when the outlet lies on the vertical axis.</param>
        /// <param name="index">Zero-based output index in this shot.</param>
        /// <param name="count">Number of outputs requested by the shot.</param>
        /// <param name="velocity">World velocity change, or zero when scatter is disabled.</param>
        /// <param name="tumble">World angular velocity change, or zero when scatter is disabled.</param>
        internal void GetLaunch(Vector3 pivot, Vector3 outlet, Vector3 fallback, int index, int count, out Vector3 velocity, out Vector3 tumble)
        {
            velocity = tumble = Vector3.zero;
            if (speed <= 0f)
                return;
            // The room-facing outlet sets the forward half-plane. The shot can skew the fan within it.
            Vector3 away = Vector3.ProjectOnPlane(outlet - pivot, Vector3.up).normalized;
            if (away.sqrMagnitude < 0.001f)
                away = incoming.sqrMagnitude > 0.001f ? incoming : Vector3.ProjectOnPlane(fallback, Vector3.up).normalized;
            if (away.sqrMagnitude < 0.001f)
                away = Vector3.forward;
            Vector3 axis = (away + incoming * 0.28f).normalized;
            float lane = count > 1 ? index / (count - 1f) : 0.5f;
            Vector3 direction = Quaternion.AngleAxis(Mathf.Lerp(-68f, 68f, lane) + phase, Vector3.up) * axis;
            float outward = Vector3.Dot(direction, away);
            if (outward < 0.2f)
                direction = (direction + away * (0.2f - outward)).normalized;
            // Alternating roll keeps the outline of a flat plate visible during its flight.
            float lift = 2.75f + Mathf.Repeat(index * 0.618f, 1f) * 1.45f;
            velocity = (direction * speed + Vector3.up * lift) * launchScale;
            tumble = Vector3.Cross(Vector3.up, direction) * (index % 2 == 0 ? 13f : -13f) + Vector3.up * 4f;
        }

        #endregion

        #endregion
    }
}
