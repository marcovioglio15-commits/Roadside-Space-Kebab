using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Tracks a physical body's floating path from its entry pose in the generator's frame.</summary>
    internal readonly struct GravityFloatMotion
    {
        #region State

        private readonly Vector3 anchor;
        private readonly Vector3 center;
        private readonly Vector3 axis;
        private readonly double start;

        #endregion

        #region Methods

        #region Motion

        /// <summary>Captures a stable starting height and nearest orbit center when the body becomes free to float.</summary>
        /// <param name="body">Suspended dynamic body.</param>
        /// <param name="frame">Generator whose pose defines the local float path.</param>
        /// <param name="settings">Validated continuous motion.</param>
        internal GravityFloatMotion(Rigidbody body, Transform frame, GravityFloatSettings settings)
        {
            anchor = frame.InverseTransformPoint(body.worldCenterOfMass);
            center = default;
            axis = Vector3.up;
            start = Time.fixedTimeAsDouble;
            if (settings.Rotation != GravityFloatRotation.Orbit)
                return;

            // Compare world distances so non-uniform generator scale does not change the nearest center.
            float nearest = float.PositiveInfinity;
            foreach (GravityOrbitCenter candidate in settings.Centers)
            {
                float distance = (frame.TransformPoint(candidate.Position) - body.worldCenterOfMass).sqrMagnitude;
                if (distance >= nearest)
                    continue;
                nearest = distance;
                center = candidate.Position;
                axis = Quaternion.Euler(candidate.Rotation) * Vector3.up;
            }
        }

        /// <summary>Applies damped acceleration toward the float path while allowing normal solver contacts.</summary>
        /// <param name="body">Body currently assigned to this motion.</param>
        /// <param name="frame">Generator frame, which may move independently of the body.</param>
        /// <param name="settings">Float path and acceleration limits.</param>
        internal void Tick(Rigidbody body, Transform frame, GravityFloatSettings settings)
        {
            double time = Time.fixedTimeAsDouble - start;
            Vector3 target = frame.TransformPoint(anchor);
            if (settings.Rotation == GravityFloatRotation.Orbit)
            {
                // Rotate a world-space radius to keep its length even under non-uniform scale.
                Vector3 worldCenter = frame.TransformPoint(center);
                Quaternion orbit = Quaternion.AngleAxis((float)(time * settings.OrbitSpeed % 360d), frame.TransformDirection(axis));
                target = worldCenter + orbit * (target - worldCenter);
            }
            if (settings.Height > 0f)
                target += frame.up * (settings.Height * Mathf.Sin((float)(time * settings.Frequency % 1d) * (2f * Mathf.PI)));

            // Use a damped spring so other bodies can displace the floating object without transform snapping.
            Vector3 acceleration = (target - body.worldCenterOfMass) * 32f - body.linearVelocity * 7f;
            body.AddForce(Vector3.ClampMagnitude(acceleration, settings.Acceleration), ForceMode.Acceleration);
            if (settings.Rotation == GravityFloatRotation.Spin)
            {
                Vector3 spin = frame.up * (settings.SpinSpeed * Mathf.Deg2Rad);
                body.AddTorque((spin - body.angularVelocity) * 6f, ForceMode.Acceleration);
            }
        }

        #endregion

        #endregion
    }
}

