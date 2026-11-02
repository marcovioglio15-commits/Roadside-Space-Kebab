using System;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses a complete reversible flag state for one inventory threshold.</summary>
    public enum InventoryFlagMode { KeepBase, Replace, AddRemove }

    /// <summary>Keeps fill flags separate from permanent identity and temporary contact changes.</summary>
    [Serializable]
    public sealed class InventoryFlagSettings
    {
        #region Fields

        [Header("Fill Flags")]
        [Tooltip("Keep underlying flags, replace them, or add and remove selected flags while this step is active.")]
        public InventoryFlagMode Mode;
        [Tooltip("Flags assigned by Replace or added by Add Remove.")]
        public ObjectFlag[] Flags = Array.Empty<ObjectFlag>();
        [Tooltip("Flags excluded while this Add Remove step is active.")]
        public ObjectFlag[] Remove = Array.Empty<ObjectFlag>();

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks the active flag branch without rewriting its selection.</summary>
        /// <param name="warning">Receives invalid flags or mode.</param>
        /// <returns>True when the state can be applied.</returns>
        internal bool TryValidate(out string warning)
        {
            warning = "Choose a supported fill flag mode.";
            if (Mode is not (InventoryFlagMode.KeepBase or InventoryFlagMode.Replace or InventoryFlagMode.AddRemove))
                return false;
            warning = string.Empty;
            return Mode == InventoryFlagMode.KeepBase || ObjectFlagRules.TryValidate(Flags, true, out warning)
                && (Mode != InventoryFlagMode.AddRemove || ObjectFlagRules.TryValidate(Remove, true, out warning));
        }

        #endregion

        #endregion
    }
}
