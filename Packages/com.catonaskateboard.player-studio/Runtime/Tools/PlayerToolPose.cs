using System;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Stores a local transform pose shared by tool slots and animation keys.</summary>
    [Serializable]
    public struct PlayerToolPose
    {
        #region Fields

        [Header("Local Pose")]
        [Tooltip("Position relative to the animated child's parent.")]
        public Vector3 Position;
        [Tooltip("Local Euler angles in degrees. Animation keys preserve full turns between values.")]
        public Vector3 Rotation;
        [Tooltip("Local scale of the animated child; every axis must be positive.")]
        public Vector3 Scale;

        #endregion

        #region Properties

        /// <summary>Untranslated, unrotated pose with unit scale.</summary>
        public static PlayerToolPose Identity => new PlayerToolPose { Scale = Vector3.one };

        #endregion

        #region Methods

        #region Sampling

        /// <summary>Captures a child before a transition or an editor recording.</summary>
        /// <param name="target">Transform whose local values are captured.</param>
        /// <returns>An independent value snapshot.</returns>
        public static PlayerToolPose Read(Transform target)
        {
            // Capture local values so moving the player does not change the animation.
            return new PlayerToolPose { Position = target.localPosition, Rotation = target.localEulerAngles, Scale = target.localScale };
        }

        /// <summary>Applies a sampled pose without touching the player's body.</summary>
        /// <param name="target">Visual child receiving the pose.</param>
        public void Apply(Transform target)
        {
            // Position and rotation share one native transform write.
            target.SetLocalPositionAndRotation(Position, Quaternion.Euler(Rotation));
            target.localScale = Scale;
        }

        /// <summary>Interpolates keys in local space, including authored rotations beyond one revolution.</summary>
        /// <param name="from">Previous key.</param>
        /// <param name="to">Next key.</param>
        /// <param name="amount">Normalized interpolation weight.</param>
        /// <returns>The sampled local pose.</returns>
        public static PlayerToolPose Interpolate(PlayerToolPose from, PlayerToolPose to, float amount)
        {
            // Euler interpolation retains explicitly recorded multi-turn motion.
            return new PlayerToolPose
            {
                Position = Vector3.LerpUnclamped(from.Position, to.Position, amount),
                Rotation = Vector3.LerpUnclamped(from.Rotation, to.Rotation, amount),
                Scale = Vector3.LerpUnclamped(from.Scale, to.Scale, amount)
            };
        }

        #endregion

        #region Validation

        /// <summary>Rejects invalid values without altering the authored pose.</summary>
        /// <returns>True for finite values and strictly positive scale axes.</returns>
        public bool IsValid()
        {
            // Invalid editor input remains available for correction.
            return Finite(Position) && Finite(Rotation) && Finite(Scale) && Scale.x > 0f && Scale.y > 0f && Scale.z > 0f;
        }

        /// <summary>Checks every vector axis before using a transform or interpolation.</summary>
        /// <param name="value">Vector supplied by a preset.</param>
        /// <returns>True when no axis contains infinity or NaN.</returns>
        private static bool Finite(Vector3 value)
        {
            // Unity transforms cannot safely consume non-finite coordinates.
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        #endregion

        #endregion
    }
}
