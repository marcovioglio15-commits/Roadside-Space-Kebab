using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses a box appearance change or visual pieces positioned inside it.</summary>
    public enum InventoryFillMode { Appearance, ActualItemFill }

    /// <summary>Places one visible unit of a container's contents.</summary>
    [Serializable]
    public sealed class InventoryFillMagnet
    {
        #region Fields

        [Tooltip("Local position of the displayed piece inside the container.")]
        public Vector3 Position;
        [Tooltip("Local Euler rotation of the displayed piece.")]
        public Vector3 Rotation;
        [Tooltip("Relative scale of the displayed piece.")]
        public Vector3 Scale = Vector3.one;

        #endregion
    }

    /// <summary>Defines a layout for one container threshold without adding gameplay objects to it.</summary>
    [Serializable]
    public sealed class InventoryItemFill
    {
        #region Fields

        [Header("Actual Item Fill")]
        [Tooltip("Optional visual model for this ingredient. Leave empty to copy the actual stored or scooped item's meshes and materials.")]
        public GameObject VisualPrefab;
        [Tooltip("One placement per visible unit. The first magnets are used when the current count is below the layout size.")]
        public InventoryFillMagnet[] Magnets = Array.Empty<InventoryFillMagnet>();

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks layout size and placement without rewriting configured values.</summary>
        /// <param name="count">Number of items needed to activate this step.</param>
        /// <param name="warning">Receives an unusable layout or model reference.</param>
        /// <returns>True when the step can represent every item at its threshold.</returns>
        internal bool TryValidate(int count, out string warning)
        {
            warning = "Add a finite magnet with positive scale for each item at this threshold. Use a prefab asset as the optional model.";
            if (Magnets == null || Magnets.Length < Mathf.Max(1, count) || VisualPrefab != null && VisualPrefab.scene.IsValid())
                return false;
            foreach (InventoryFillMagnet magnet in Magnets)
                if (magnet == null || !InteractionValues.Finite(magnet.Position) || !InteractionValues.Finite(magnet.Rotation)
                    || !InteractionValues.Finite(magnet.Scale) || magnet.Scale.x <= 0f || magnet.Scale.y <= 0f || magnet.Scale.z <= 0f)
                    return false;
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
