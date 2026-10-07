using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits shared order definitions independently of their consuming objects.</summary>
    public sealed class OrderCatalogWindow : StudioAssetWindow
    {
        #region Properties

        /// <summary>Asset type edited by this independent session.</summary>
        protected override System.Type AssetType => typeof(OrderCatalog);

        #endregion

        #region Methods
        #region Window

        /// <summary>Opens the asset without applying any pending edits in the main tool.</summary>
        /// <param name="asset">Shared asset to edit.</param>
        public static void Open(OrderCatalog asset)
        {
            GetWindow<OrderCatalogWindow>("Order Catalog").Select(asset);
        }

        /// <summary>Checks the proposal before updating its saved source.</summary>
        /// <param name="proposal">Detached asset receiving edits.</param>
        /// <param name="issue">Receives invalid settings.</param>
        /// <returns>True when the complete proposal is valid.</returns>
        protected override bool Validate(ScriptableObject proposal, out string issue)
        {
            return ((OrderCatalog)proposal).TryValidate(out issue);
        }

        #endregion
        #endregion
    }
}
