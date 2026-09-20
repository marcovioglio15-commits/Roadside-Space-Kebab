using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Retains the visual prepared in Editor and applies its preset offset once when Play starts.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHost))]
    [AddComponentMenu("Player Studio/Player Visual Binding")]
    public sealed class PlayerVisualBinding : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Managed Visual")]
        [Tooltip("Player whose applied Visual preset supplies the offset.")]
        [SerializeField]
        private PlayerHost host;

        [Tooltip("Direct child receiving the offset. An adopted child keeps its existing hierarchy.")]
        [SerializeField]
        private Transform visualRoot;

        [Tooltip("Model managed by this binding; other children of the player remain independent.")]
        [SerializeField]
        private GameObject model;

        [Tooltip("Prefab used to create the managed model; empty for an adopted child.")]
        [SerializeField]
        private GameObject sourcePrefab;

        [Tooltip("Whether Player Studio created the separate Visual container and its model.")]
        [SerializeField]
        private bool ownsContainer;

        [Header("Authored Pose")]
        [Tooltip("Original local position of an adopted child, composed after the preset offset.")]
        [SerializeField]
        private Vector3 basePosition;

        [Tooltip("Original local rotation of an adopted child, composed after the preset offset.")]
        [SerializeField]
        private Quaternion baseRotation = Quaternion.identity;

        [Tooltip("Original local scale of an adopted child; the preset multiplies it uniformly.")]
        [SerializeField]
        private Vector3 baseScale = Vector3.one;

        #endregion

        #region Properties

        /// <summary>The player owning this explicit binding.</summary>
        public PlayerHost Host => host;
        /// <summary>The transform that receives the composed visual offset.</summary>
        public Transform VisualRoot => visualRoot;
        /// <summary>The model whose identity is retained across offset edits.</summary>
        public GameObject Model => model;
        /// <summary>The source of a model instantiated by the tool.</summary>
        public GameObject SourcePrefab => sourcePrefab;
        /// <summary>Only a tool-created container may be removed by releasing this binding.</summary>
        public bool OwnsContainer => ownsContainer;
        /// <summary>The authored pose that an adopted model contributes after the offset.</summary>
        public Matrix4x4 BaseMatrix => Matrix4x4.TRS(basePosition, baseRotation, baseScale);

        #endregion

        #region Methods

        #region Initialization

        /// <summary>Reads the applied preset once; model creation remains an Editor operation.</summary>
        private void Awake()
        {
            // A stale or incomplete binding must not move an unrelated child.
            if (host == null || host.gameObject != gameObject || host.MasterPreset == null
                || host.MasterPreset.VisualPreset == null || visualRoot == null || visualRoot.parent != transform || model == null
                || (ownsContainer ? model.transform.parent != visualRoot : model.transform != visualRoot))
            {
                Debug.LogWarning("The Visual binding needs its player, direct visual child, model and applied Visual preset.", this);
                return;
            }

            if (!host.MasterPreset.VisualPreset.TryGetSettings(out PlayerVisualSettings settings, out string warning))
            {
                Debug.LogWarning(warning, this);
                return;
            }

            // A changed prefab needs an Editor confirmation, never a hidden runtime replacement.
            if (settings.Prefab != sourcePrefab)
            {
                Debug.LogWarning("The Visual prefab changed. Apply it in Player Studio before entering Play.", this);
                return;
            }

            ApplyOffset(settings);
        }

        #endregion

        #region Offset

        /// <summary>Applies validated values to the visual only; the caller owns Editor Undo when applicable.</summary>
        /// <param name="settings">Validated offset composed before the retained authored pose.</param>
        public void ApplyOffset(PlayerVisualSettings settings)
        {
            // Uniform scaling keeps this composition representable without matrix decomposition.
            visualRoot.SetLocalPositionAndRotation(settings.Position + settings.Rotation * (basePosition * settings.Scale),
                settings.Rotation * baseRotation);
            visualRoot.localScale = baseScale * settings.Scale;
        }

        #endregion

        #endregion
    }
}
