using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>
    /// Keeps validated capsule dimensions from a preset or an editor draft.
    /// Each player keeps its own value without changing the shared asset.
    /// </summary>
    public readonly struct PlayerBodySettings
    {
        #region Properties

        /// <summary>Capsule radius in metres.</summary>
        public float Radius { get; }

        /// <summary>Total capsule height in metres, including both rounded ends.</summary>
        public float Height { get; }

        /// <summary>Local capsule centre when the player origin is at its feet.</summary>
        public Vector3 Center => Vector3.up * (Height * 0.5f);

        #endregion

        #region Methods

        #region Construction

        /// <summary>
        /// Stores dimensions after TryCreate has checked their validity.
        /// </summary>
        /// <param name="radius">Validated capsule radius in metres.</param>
        /// <param name="height">Validated total capsule height in metres.</param>
        private PlayerBodySettings(float radius, float height)
        {
            // Copy values only; the settings keep no reference to the preset.
            Radius = radius;
            Height = height;
        }

        #endregion

        #region Validation

        /// <summary>
        /// Creates a valid body value for both saved presets and editor drafts.
        /// Rejected dimensions are reported without being corrected.
        /// </summary>
        /// <param name="radius">Requested capsule radius in metres.</param>
        /// <param name="height">Requested total capsule height in metres.</param>
        /// <param name="settings">Receives valid dimensions, or the default value on failure.</param>
        /// <param name="warning">Receives the reason for failure, or an empty string on success.</param>
        /// <returns>True when both dimensions describe a valid capsule.</returns>
        public static bool TryCreate(float radius, float height, out PlayerBodySettings settings, out string warning)
        {
            // Leave unusable data and a clear message when validation fails.
            settings = default;
            warning = string.Empty;

            // Keep one geometric rule shared by presets and unsaved drafts.
            if (!float.IsFinite(radius) || !float.IsFinite(height) || radius <= 0f || height < radius * 2f)
            {
                warning = "Body dimensions must be finite, with radius greater than zero and height at least twice the radius.";
                return false;
            }

            // Construct an independent value only after all checks succeed.
            settings = new PlayerBodySettings(radius, height);
            return true;
        }

        #endregion

        #endregion
    }
}
