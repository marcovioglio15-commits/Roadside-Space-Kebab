using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Connects an assembly table to one already configured product prefab.</summary>
    [Serializable]
    public sealed class AssemblyStationSettings
    {
        #region Fields

        [Header("Assembly Table")]
        [Tooltip("Existing product prefab containing Assembly Product, its recipe and its configured interactions.")]
        public GameObject ProductPrefab;
        [Tooltip("Maximum player distance in metres for adding the currently carried ingredient.")]
        public float Distance = 3f;
        [Tooltip("Require a clear line of sight from the player's camera to the table's output position.")]
        public bool RequireSight = true;
        [Tooltip("Layers that can obstruct the player's view of the assembly table.")]
        public LayerMask Obstacles = ~0;
        [Tooltip("Product root position relative to the table when beginning or returning an assembly.")]
        public Vector3 OutputPosition = new Vector3(0f, 0.5f, 0f);
        [Tooltip("Product root rotation in degrees relative to the table.")]
        public Vector3 OutputRotation;
        [Tooltip("Allow a carried product from this same prefab to be returned to an empty table for further assembly.")]
        public bool AcceptReturnedProduct = true;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Validates reusable product and output settings before preset updates or table activation.</summary>
        /// <param name="warning">Receives a missing configured prefab or invalid placement.</param>
        /// <returns>True when the product reference and table values are usable.</returns>
        public bool TryValidate(out string warning)
        {
            // Local action and staging bindings are validated by the table component separately.
            warning = "Choose a product prefab with an enabled Assembly Product component.";
            if (ProductPrefab == null || ProductPrefab.scene.IsValid()
                || !ProductPrefab.TryGetComponent(out ObjectAssemblyProduct product) || !product.enabled)
                return false;
            if (!product.TryValidate(out warning))
                return false;
            warning = "Use a positive finite range and finite output position and rotation.";
            if (!InteractionValues.Positive(Distance) || !InteractionValues.Finite(OutputPosition)
                || !InteractionValues.Finite(OutputRotation))
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
