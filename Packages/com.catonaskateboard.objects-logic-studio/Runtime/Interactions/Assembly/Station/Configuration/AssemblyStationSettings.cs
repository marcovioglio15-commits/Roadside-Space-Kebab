using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses a player command or sustained ingredient contact.</summary>
    public enum AssemblyStationTrigger { InputAction, IngredientContact, ImmediateContact }

    /// <summary>Connects an assembly table to one already configured product prefab.</summary>
    [Serializable]
    public sealed class AssemblyStationSettings
    {
        #region Fields

        [Header("Assembly Table")]
        [Tooltip("Existing product prefab containing Assembly Product, its recipe and its configured interactions.")]
        public GameObject ProductPrefab;
        [Tooltip("Insert by input, sustained contact or immediate contact. Recipes sharing a first ingredient on the same object keep it snapped and usable until later ingredients distinguish the recipe. Different input bindings select their recipes explicitly.")]
        public AssemblyStationTrigger Trigger;
        [Tooltip("Seconds of uninterrupted ingredient contact required before insertion.")]
        public float ContactDuration = 0.5f;
        [Tooltip("Seconds between contact queries. Smaller values detect short separations more accurately.")]
        public float ContactQueryInterval = 0.02f;
        [Tooltip("Maximum surface separation accepted as contact, in metres.")]
        public float ContactTolerance = 0.005f;
        [Tooltip("Include trigger colliders when detecting ingredient contact.")]
        public bool IncludeTriggers;
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
            warning = "Choose an Assembly Product prefab.";
            if (ProductPrefab == null)
                return false;
            warning = $"'{ProductPrefab.name}' must reference a prefab asset, not a scene instance.";
            if (ProductPrefab.scene.IsValid())
                return false;
            warning = $"'{ProductPrefab.name}' needs an enabled Assembly Product component on its root. Configure its recipe in Object Assemble.";
            if (!ProductPrefab.TryGetComponent(out ObjectAssemblyProduct product) || !product.enabled)
                return false;
            if (!product.TryValidate(out warning))
                return false;
            warning = "Choose a trigger with positive range or contact timing, non-negative contact tolerance and finite output pose.";
            if (Trigger is not (AssemblyStationTrigger.InputAction or AssemblyStationTrigger.IngredientContact or AssemblyStationTrigger.ImmediateContact)
                || Trigger == AssemblyStationTrigger.InputAction && !InteractionValues.Positive(Distance)
                || Trigger == AssemblyStationTrigger.IngredientContact && !InteractionValues.Positive(ContactDuration)
                || Trigger != AssemblyStationTrigger.InputAction && (!InteractionValues.Positive(ContactQueryInterval)
                    || !float.IsFinite(ContactTolerance) || ContactTolerance < 0f)
                || !InteractionValues.Finite(OutputPosition)
                || !InteractionValues.Finite(OutputRotation))
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
