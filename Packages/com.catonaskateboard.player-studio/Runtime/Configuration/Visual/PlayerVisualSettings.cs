using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Captures a visual source and local offset without moving a player or creating a model.</summary>
    public readonly struct PlayerVisualSettings
    {
        #region Properties

        /// <summary>Optional prefab source; an existing visual can supply the model when this is empty.</summary>
        public GameObject Prefab { get; }

        /// <summary>Local offset relative to the player, independent of collider dimensions.</summary>
        public Vector3 Position { get; }

        /// <summary>Local orientation correction, converted once from the authored Euler angles.</summary>
        public Quaternion Rotation { get; }

        /// <summary>Positive multiplier for the visual container, not the physical root.</summary>
        public float Scale { get; }

        #endregion

        #region Methods

        #region Construction

        /// <summary>Stores validated values without cloning the referenced prefab asset.</summary>
        /// <param name="prefab">Optional source for a visual instance.</param>
        /// <param name="position">Finite local position offset.</param>
        /// <param name="rotation">Finite local orientation correction.</param>
        /// <param name="scale">Positive finite uniform scale.</param>
        private PlayerVisualSettings(GameObject prefab, Vector3 position, Quaternion rotation, float scale)
        {
            // The snapshot stores values; ownership of a scene instance belongs to a separate binding.
            Prefab = prefab;
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        #endregion

        #region Validation

        /// <summary>Validates offset numbers without changing the preset, prefab or scene.</summary>
        /// <param name="prefab">Optional source reference; Editor validation checks whether it is a prefab asset.</param>
        /// <param name="position">Local position offset in metres.</param>
        /// <param name="eulerAngles">Local rotation offset in degrees; authored values are not wrapped or replaced.</param>
        /// <param name="scale">Requested uniform visual scale, independent of the player root.</param>
        /// <param name="settings">Receives a validated snapshot, or default on failure.</param>
        /// <param name="warning">Receives the invalid number requirement without repairing the input.</param>
        /// <returns>True when the offset and its derived rotation can be represented.</returns>
        public static bool TryCreate(GameObject prefab, Vector3 position, Vector3 eulerAngles, float scale,
            out PlayerVisualSettings settings, out string warning)
        {
            // Keep invalid input available to the editor instead of substituting a plausible pose.
            settings = default;
            warning = string.Empty;
            if (!IsFinite(position) || !IsFinite(eulerAngles) || !float.IsFinite(scale) || scale <= 0f)
            {
                warning = "Visual offsets must be finite and Scale must be greater than zero.";
                return false;
            }

            // Convert only after validating the input; extreme finite angles can still overflow conversion.
            Quaternion rotation = Quaternion.Euler(eulerAngles);
            if (!float.IsFinite(rotation.x) || !float.IsFinite(rotation.y)
                || !float.IsFinite(rotation.z) || !float.IsFinite(rotation.w))
            {
                warning = "Visual rotation cannot be represented. Reduce the entered angles explicitly.";
                return false;
            }

            settings = new PlayerVisualSettings(prefab, position, rotation, scale);
            return true;
        }

        /// <summary>Uses the same component checks for position and authored rotation.</summary>
        /// <param name="value">Vector whose components must not be NaN or infinite.</param>
        /// <returns>True when all three components are finite.</returns>
        private static bool IsFinite(Vector3 value)
        {
            // Numeric validation needs no temporary arrays or per-component allocations.
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        #endregion

        #endregion
    }
}
