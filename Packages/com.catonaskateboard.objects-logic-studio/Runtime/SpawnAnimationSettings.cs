using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Animates a newly generated object into its final pose before releasing its original interactions.</summary>
    [Serializable]
    public sealed class SpawnAnimationSettings
    {
        #region Fields

        [Header("Spawn Animation")]
        [Tooltip("Animate generated prefabs before enabling any of their authored interactions or collisions.")]
        public bool Enabled;
        [Tooltip("Animation duration in game-time seconds. Other interaction locks remain intact after completion.")]
        public float Duration = 0.5f;
        [Tooltip("Starting offset relative to the generated object's final orientation, in metres.")]
        public Vector3 Offset = new Vector3(0f, -0.3f, 0f);
        [Tooltip("Starting Euler rotation added to the generated object's final rotation.")]
        public Vector3 Rotation;
        [Tooltip("Positive starting scale multiplier relative to the prefab's final scale.")]
        public Vector3 Scale = new Vector3(0.05f, 0.05f, 0.05f);
        [Tooltip("Transform progress over normalized time. The curve must start at (0, 0) and end at (1, 1).")]
        public AnimationCurve Progress = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks animation timing and endpoint continuity without altering the curve.</summary>
        /// <param name="warning">Receives the first invalid timing, pose or curve endpoint.</param>
        /// <returns>True when disabled or safe to animate into the final prefab pose.</returns>
        public bool TryValidate(out string warning)
        {
            // Continuity at the endpoint prevents a visible snap when normal physics resumes.
            warning = string.Empty;
            if (!Enabled)
                return true;
            if (!InteractionValues.Positive(Duration) || !InteractionValues.Finite(Offset) || !InteractionValues.Finite(Rotation)
                || !InteractionValues.Finite(Scale) || Scale.x <= 0f || Scale.y <= 0f || Scale.z <= 0f)
                warning = "Use a positive finite duration and scale multipliers, with finite starting offsets and angles.";
            else if (Progress == null || Progress.length < 2 || !Mathf.Approximately(Progress[0].time, 0f)
                || !Mathf.Approximately(Progress[0].value, 0f) || !Mathf.Approximately(Progress[Progress.length - 1].time, 1f)
                || !Mathf.Approximately(Progress[Progress.length - 1].value, 1f))
                warning = "Spawn animation progress must begin at (0, 0) and end at (1, 1).";
            else
                foreach (Keyframe key in Progress.keys)
                    if (!InteractionValues.Finite(key.time) || !InteractionValues.Finite(key.value)
                        || float.IsNaN(key.inTangent) || float.IsNaN(key.outTangent))
                    {
                        warning = "Spawn animation keys must contain finite times and values, without invalid tangents.";
                        break;
                    }
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
