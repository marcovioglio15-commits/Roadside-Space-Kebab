using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Chooses authored Euler travel, the shortest orientation change, or a directed local-axis turn.</summary>
    public enum TransformRotationDirection { AsAuthored, Shortest, Clockwise, CounterClockwise }

    /// <summary>Shares explicit rotation direction between transform interactions and player tool animations.</summary>
    public static class TransformRotation
    {
        #region Methods

        #region Sampling

        /// <summary>Builds Euler travel once before interpolation, retaining explicitly authored full revolutions.</summary>
        /// <param name="from">Initial local Euler angles.</param>
        /// <param name="to">Destination local Euler angles.</param>
        /// <param name="direction">Clockwise uses negative angles around each changed positive local axis.</param>
        /// <returns>Signed Euler travel ending at the requested orientation.</returns>
        public static Vector3 Delta(Vector3 from, Vector3 to, TransformRotationDirection direction)
        {
            // Each changing axis has an explicit sign; identical angles do not introduce an unwanted full turn.
            return new Vector3(Delta(from.x, to.x, direction), Delta(from.y, to.y, direction), Delta(from.z, to.z, direction));
        }

        /// <summary>Samples either a shortest orientation path or the configured directed Euler arc.</summary>
        /// <param name="from">Starting local Euler angles.</param>
        /// <param name="to">Destination local Euler angles.</param>
        /// <param name="amount">Normalized transition progress.</param>
        /// <param name="direction">Requested rotation policy.</param>
        /// <returns>A local orientation reaching the exact destination.</returns>
        public static Quaternion Sample(Vector3 from, Vector3 to, float amount, TransformRotationDirection direction)
        {
            // Quaternion interpolation remains available for unrestricted shortest-path motion.
            return direction == TransformRotationDirection.Shortest
                ? Quaternion.SlerpUnclamped(Quaternion.Euler(from), Quaternion.Euler(to), amount)
                : Quaternion.Euler(from + Delta(from, to, direction) * amount);
        }

        /// <summary>Chooses a signed axis arc without changing the destination or discarding requested full turns.</summary>
        /// <param name="from">Initial angle in degrees.</param>
        /// <param name="to">Destination angle in degrees.</param>
        /// <param name="direction">Requested signed travel.</param>
        /// <returns>Degrees to add to the initial angle.</returns>
        private static float Delta(float from, float to, TransformRotationDirection direction)
        {
            // Multiples of a full turn retain their magnitude when reversing their authored sign.
            float delta = to - from;
            switch (direction)
            {
                case TransformRotationDirection.Shortest:
                    return Mathf.DeltaAngle(from, to);
                case TransformRotationDirection.Clockwise when delta > 0f:
                    return delta % 360f == 0f ? -delta : delta % 360f - 360f * (1f + Mathf.Floor(delta / 360f));
                case TransformRotationDirection.CounterClockwise when delta < 0f:
                    return delta % 360f == 0f ? -delta : delta % 360f + 360f * (1f + Mathf.Floor(-delta / 360f));
                default:
                    return delta;
            }
        }

        #endregion

        #region Validation

        /// <summary>Rejects unknown serialized rotation modes without modifying presets.</summary>
        /// <param name="direction">Authored rotation policy.</param>
        /// <returns>True for a supported rotation policy.</returns>
        public static bool IsValid(TransformRotationDirection direction)
        {
            // Explicit choices also work in builds without reflection.
            return direction is TransformRotationDirection.AsAuthored or TransformRotationDirection.Shortest
                or TransformRotationDirection.Clockwise or TransformRotationDirection.CounterClockwise;
        }

        #endregion

        #endregion
    }
}
