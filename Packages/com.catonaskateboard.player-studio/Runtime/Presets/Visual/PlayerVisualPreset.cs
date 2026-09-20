using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Stores reusable visual source and offset data without creating or changing scene objects.</summary>
    [CreateAssetMenu(fileName = "PlayerVisual", menuName = "Player Studio/Visual Preset")]
    public sealed class PlayerVisualPreset : ScriptableObject
    {
        #region Serialized Fields

        [Header("Source")]
        [Tooltip("Static source prefab. Clearing an applied source removes its managed model when confirmed in Player Studio.")]
        [SerializeField]
        private GameObject prefab;

        [Header("Local Offset")]
        [Tooltip("Position offset before the authored model pose, in metres. Does not change collider dimensions.")]
        [SerializeField]
        private Vector3 position = Vector3.zero;

        [Tooltip("Rotation offset in degrees before the authored model pose. The source prefab asset stays unchanged.")]
        [SerializeField]
        private Vector3 eulerAngles = Vector3.zero;

        [Tooltip("Uniform multiplier for the visual model. Must be finite and positive; does not scale the player root or collider.")]
        [SerializeField]
        private float scale = 1f;

        #endregion

        #region Methods

        #region Configuration

        /// <summary>Creates an offset snapshot without instantiating the optional source prefab.</summary>
        /// <param name="settings">Receives validated visual values, or default on failure.</param>
        /// <param name="warning">Receives a numeric incompatibility without changing the asset.</param>
        /// <returns>True when the authored offset is usable; prefab asset identity is checked separately in Editor.</returns>
        public bool TryGetSettings(out PlayerVisualSettings settings, out string warning)
        {
            // Keep numeric rules shared between asset reads and future session drafts.
            return PlayerVisualSettings.TryCreate(prefab, position, eulerAngles, scale, out settings, out warning);
        }

        #endregion

        #region Unity Callbacks

        /// <summary>Reports invalid numbers after an Inspector change without adjusting the model or preset.</summary>
        private void OnValidate()
        {
            // Local validation has no scene effects and never resolves or creates a visual instance.
            if (!TryGetSettings(out _, out string warning))
                Debug.LogWarning(warning, this);
        }

        #endregion

        #endregion
    }
}
