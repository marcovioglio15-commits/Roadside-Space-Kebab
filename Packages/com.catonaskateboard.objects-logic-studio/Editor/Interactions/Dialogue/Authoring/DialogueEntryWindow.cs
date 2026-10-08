using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Owns independent Apply/Discard editing for one shared dialogue entry.</summary>
    public sealed class DialogueEntryWindow : StudioAssetWindow
    {
        #region Fields

        [Tooltip("Optional catalog supplying order-name choices; editing context only, never stored in the entry.")]
        [SerializeField]
        private OrderCatalog catalog;
        [Tooltip("Whether the requesting interaction uses the Consumption trigger.")]
        [SerializeField]
        private bool consumption = true;
        [Tooltip("Whether the requesting interaction uses weighted entry selection.")]
        [SerializeField]
        private bool weighted = true;

        #endregion
        #region Properties

        /// <summary>Type accepted by this window's asset selector.</summary>
        protected override System.Type AssetType => typeof(DialogueEntry);

        #endregion
        #region Methods
        #region Window

        /// <summary>Opens an entry without applying the main tool or replacing an unsaved entry proposal.</summary>
        /// <param name="asset">Shared entry to edit.</param>
        /// <param name="orders">Optional catalog from the consuming object's Available Orders.</param>
        /// <param name="filter">Whether to show consumption conditions.</param>
        /// <param name="weight">Whether to show the selection weight.</param>
        public static void Open(DialogueEntry asset, OrderCatalog orders = null, bool filter = true, bool weight = true)
        {
            DialogueEntryWindow window = GetWindow<DialogueEntryWindow>("Dialogue Entry");
            if (!window.hasUnsavedChanges)
            {
                window.catalog = orders;
                window.consumption = filter;
                window.weighted = weight;
            }
            window.Select(asset);
        }

        /// <summary>Supplies optional order choices without persisting a dependency on a sample object.</summary>
        /// <param name="editor">Cached inspector for the detached entry.</param>
        protected override void DrawInspector(UnityEditor.Editor editor)
        {
            if (consumption)
                catalog = (OrderCatalog)StudioFieldGUI.ObjectField(new GUIContent("Order Catalog",
                    "Catalog used to select named orders. Leave empty for consumed flags on objects without Available Orders."), catalog, typeof(OrderCatalog), false);
            ((DialogueEntryEditor)editor).Draw(catalog, consumption, weighted);
        }

        /// <summary>Validates the entry before replacing its shared saved asset.</summary>
        /// <param name="proposal">Detached entry containing pending edits.</param>
        /// <param name="issue">Receives invalid pages, weight or consumption conditions.</param>
        /// <returns>True when the edited entry is complete for this context.</returns>
        protected override bool Validate(ScriptableObject proposal, out string issue)
        {
            return ((DialogueEntry)proposal).TryValidate(out issue, consumption, weighted,
                catalog != null ? new OrderSettings { Catalog = catalog } : null);
        }

        #endregion
        #endregion
    }
}
