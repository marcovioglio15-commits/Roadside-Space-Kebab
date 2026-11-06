using System;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Expires pooled bursts and reuses the oldest occupied slot instead of allocating during firing.</summary>
    internal sealed class PlayerToolEffectPool : IDisposable
    {
        #region State

        private readonly PlayerToolVisualPool visuals;
        private readonly double[] deadlines;
        private readonly float lifetime;
        private int active;
        private bool disposed;

        #endregion

        #region Properties

        /// <summary>Measured effect bounds used by impact clearance.</summary>
        internal Bounds Envelope => visuals.Envelope;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Retains a prewarmed visual pool and one fixed lifetime for all its instances.</summary>
        /// <param name="pool">Owned visual instances.</param>
        /// <param name="capacity">Validated number of pooled instances.</param>
        /// <param name="duration">Scaled lifetime of each burst.</param>
        internal PlayerToolEffectPool(PlayerToolVisualPool pool, int capacity, float duration)
        {
            visuals = pool;
            deadlines = new double[capacity];
            lifetime = duration;
        }

        /// <summary>Releases all visual instances when their player shuts down.</summary>
        public void Dispose()
        {
            disposed = true;
            visuals.Dispose();
        }

        #endregion

        #region Playback

        /// <summary>Emits a burst without letting effect capacity block an accepted shot.</summary>
        /// <param name="position">Final world position.</param>
        /// <param name="rotation">Final world orientation.</param>
        /// <param name="scale">Additional fit scale relative to the configured prefab scale.</param>
        /// <param name="parent">Optional arm or muzzle transform followed for the effect lifetime.</param>
        internal void Emit(Vector3 position, Quaternion rotation, float scale = 1f, Transform parent = null)
        {
            if (disposed)
                return;
            // Fixed-duration slots expire in emission order, including recycled slots.
            if (!visuals.HasSpace)
            {
                int oldest = 0;
                for (int index = 1; index < deadlines.Length; index++)
                    if (deadlines[index] < deadlines[oldest])
                        oldest = index;
                visuals.Return(oldest);
            }
            else
                active++;
            int slot = visuals.Rent(position, rotation, scale, parent);
            deadlines[slot] = Time.timeAsDouble + lifetime;
        }

        /// <summary>Updates occupied lifetimes only while at least one effect is visible.</summary>
        /// <param name="now">Scaled frame time.</param>
        internal void Advance(double now)
        {
            if (disposed || active == 0)
                return;
            // Compare against emission time so short lifetimes do not round to a physics tick.
            for (int index = 0; index < deadlines.Length; index++)
                if (deadlines[index] > 0d && deadlines[index] <= now)
                {
                    deadlines[index] = 0d;
                    visuals.Return(index);
                    active--;
                }
        }

        #endregion

        #endregion
    }
}
