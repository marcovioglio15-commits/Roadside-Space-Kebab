using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits a day plan in its own independent Apply/Discard session.</summary>
    public sealed class SpawnFlowPlanWindow : StudioAssetWindow
    {
        #region Properties

        /// <summary>Asset type edited by this independent session.</summary>
        protected override System.Type AssetType => typeof(SpawnFlowPlan);

        #endregion

        #region Methods
        #region Window

        /// <summary>Opens the asset without applying any pending edits in the main tool.</summary>
        /// <param name="asset">Shared asset to edit.</param>
        public static void Open(SpawnFlowPlan asset)
        {
            GetWindow<SpawnFlowPlanWindow>("Day Flow Plan").Select(asset);
        }

        /// <summary>Checks the proposal before updating its saved source.</summary>
        /// <param name="proposal">Detached asset receiving edits.</param>
        /// <param name="issue">Receives invalid settings.</param>
        /// <returns>True when the complete proposal is valid.</returns>
        protected override bool Validate(ScriptableObject proposal, out string issue)
        {
            return ((SpawnFlowPlan)proposal).TryValidate(out issue);
        }

        #endregion
        #endregion
    }
}
