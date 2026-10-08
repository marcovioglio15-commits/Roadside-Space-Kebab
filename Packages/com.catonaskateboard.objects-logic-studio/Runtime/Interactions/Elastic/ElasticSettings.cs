using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Scales a visual squash response from the velocity change delivered by a collision.</summary>
    [Serializable]
    public sealed class ElasticSettings
    {
        #region Fields

        [Header("Impact")]
        [Tooltip("React to physical collision impulses. Spray compression can be used independently.")]
        public bool Collisions = true;
        [Tooltip("Collision layers that can trigger the response.")]
        public LayerMask Layers = ~0;
        [Tooltip("Minimum impulse divided by object mass, in metres per second, before deformation starts.")]
        public float MinimumImpact = 0.5f;
        [Tooltip("Velocity change that reaches the maximum compression.")]
        public float FullImpact = 6f;
        [Tooltip("Maximum visual compression along the impact axis. Colliders keep their original shape.")]
        [Range(0f, 0.65f)]
        public float Compression = 0.065f;
        [Tooltip("Seconds before the impact response returns exactly to its original shape.")]
        public float Duration = 0.35f;
        [Tooltip("Squash and rebound cycles over the response duration.")]
        public float Oscillations = 1.5f;
        [Tooltip("Exponential decay of the rebound. Higher values settle earlier.")]
        public float Damping = 4f;
        [Tooltip("Blend toward volume-preserving expansion perpendicular to compression.")]
        [Range(0f, 1f)]
        public float Volume = 0.7f;
        [Tooltip("Minimum seconds between accepted impacts, avoiding repeated feedback from resting contacts.")]
        public float Cooldown = 0.06f;
        [Tooltip("Keep a stronger current response when a weaker impact arrives.")]
        public bool StrongestWins = true;
        [Header("Geometry")]
        [Tooltip("Deformation centre relative to the interaction object's pivot.")]
        public Vector3 Pivot;
        [Tooltip("Optional visual branch relative to this object. Empty includes all owned meshes, without moving physics transforms.")]
        public string Path = string.Empty;

        #endregion
        #region Methods
        #region Validation

        /// <summary>Reports invalid response settings without replacing authored values.</summary>
        /// <param name="warning">Receives the first invalid setting.</param>
        /// <returns>True when the response is finite and cannot collapse its geometry.</returns>
        public bool TryValidate(out string warning)
        {
            // Compression is bounded by validation, not silently rewritten during authoring.
            warning = "Use finite impact thresholds, Full Impact above Minimum Impact, positive duration and oscillations, and compression in [0, 0.65].";
            if (!SurfaceValues.Nonnegative(MinimumImpact) || !float.IsFinite(FullImpact) || FullImpact <= MinimumImpact
                || !InteractionValues.Positive(Duration) || !InteractionValues.Positive(Oscillations)
                || !SurfaceValues.Nonnegative(Damping) || !SurfaceValues.Nonnegative(Cooldown)
                || !SurfaceValues.Unit(Volume) || !SurfaceValues.Unit(Compression) || Compression > 0.65f
                || !InteractionValues.Finite(Pivot) || Collisions && Layers.value == 0)
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
