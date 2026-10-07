using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Transfers the catalog reference and extraction settings without duplicating the shared orders.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Available Orders Preset")]
    public sealed class AvailableOrdersPreset : ExtendedInteractionPreset
    {
        #region Fields

        [Header("Available Orders")]
        [Tooltip("Catalog, destination board and per-spawn extraction settings copied by Import.")]
        public OrderSettings Settings = new OrderSettings();

        #endregion

        #region Properties

        /// <summary>Interaction receiving this reusable configuration.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.AvailableOrders;

        #endregion
    }
}
