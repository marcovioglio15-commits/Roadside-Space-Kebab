using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares one sampled deformation between mesh vertices and attached visual outlets.</summary>
    internal readonly struct ElasticPose
    {
        #region State
        private readonly Vector3 pivot;
        private readonly Vector3 impactAxis;
        private readonly Vector3 pressureAxis;
        private readonly Vector2 impactScale;
        private readonly Vector2 pressureScale;
        private readonly Vector2 impactNormalScale;
        private readonly Vector2 pressureNormalScale;
        #endregion
        #region Methods
        #region Construction

        /// <summary>Samples axial scales once for the frame's impact and sustained pressure.</summary>
        /// <param name="pivot">Deformation centre in owner coordinates.</param>
        /// <param name="impactAxis">Normalized impact axis.</param>
        /// <param name="impact">Current signed impact compression.</param>
        /// <param name="volume">Perpendicular volume compensation.</param>
        /// <param name="pressureAxis">Normalized squeeze axis.</param>
        /// <param name="pressure">Current sustained compression.</param>
        internal ElasticPose(Vector3 pivot, Vector3 impactAxis, float impact, float volume, Vector3 pressureAxis, float pressure)
        {
            this.pivot = pivot;
            this.impactAxis = impactAxis;
            this.pressureAxis = pressureAxis;
            impactScale = Scale(impact, volume);
            pressureScale = Scale(pressure, volume);
            impactNormalScale = new Vector2(1f / impactScale.x, 1f / impactScale.y);
            pressureNormalScale = new Vector2(1f / pressureScale.x, 1f / pressureScale.y);
        }

        #endregion
        #region Sampling

        /// <summary>Moves a vertex or nozzle position through the same owner-local deformation.</summary>
        /// <param name="point">Undeformed owner-local position.</param>
        /// <returns>Position relative to the unchanged gameplay root.</returns>
        internal Vector3 Point(Vector3 point)
        {
            return Vector(point - pivot) + pivot;
        }

        /// <summary>Deforms a tangent or emission direction without translating it.</summary>
        /// <param name="vector">Undeformed owner-local direction.</param>
        /// <returns>Deformed direction, retaining its scale until the caller normalizes it.</returns>
        internal Vector3 Vector(Vector3 vector)
        {
            return Stretch(Stretch(vector, impactAxis, impactScale), pressureAxis, pressureScale);
        }

        /// <summary>Applies inverse-transpose scales to preserve surface lighting.</summary>
        /// <param name="normal">Owner-local surface normal.</param>
        /// <returns>Deformed normal before conversion back to mesh space.</returns>
        internal Vector3 Normal(Vector3 normal)
        {
            return Stretch(Stretch(normal, impactAxis, impactNormalScale), pressureAxis, pressureNormalScale);
        }

        /// <summary>Computes axial compression and optional perpendicular volume compensation.</summary>
        /// <param name="compression">Signed squash amount.</param>
        /// <param name="volume">Blend toward preserved volume.</param>
        /// <returns>Parallel and perpendicular scale factors.</returns>
        private static Vector2 Scale(float compression, float volume)
        {
            float axial = 1f - compression;
            return new Vector2(axial, Mathf.Lerp(1f, 1f / Mathf.Sqrt(axial), volume));
        }

        /// <summary>Scales components parallel and perpendicular to one normalized axis.</summary>
        /// <param name="value">Position offset, tangent or normal.</param>
        /// <param name="axis">Unit deformation axis.</param>
        /// <param name="scale">Parallel and perpendicular multipliers.</param>
        /// <returns>The transformed vector.</returns>
        private static Vector3 Stretch(Vector3 value, Vector3 axis, Vector2 scale)
        {
            return value * scale.y + axis * (Vector3.Dot(value, axis) * (scale.x - scale.y));
        }

        #endregion
        #endregion
    }
}
