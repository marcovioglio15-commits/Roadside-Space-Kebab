#if OBJECTS_LOGIC_STUDIO_DIALOGUE_GRAPH
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Owns one graph proposal with independent Apply/Discard and context-aware entry editing.</summary>
    public sealed class DialogueTreeWindow : StudioAssetWindow
    {
        #region Fields
        [Header("Authoring Context")]
        [Tooltip("Optional consuming object's order catalogs, copied for authoring only.")]
        [SerializeReference]
        private OrderSettings orders;
        #endregion
        #region Properties
        /// <summary>Shared graph asset accepted by this window.</summary>
        protected override System.Type AssetType => typeof(DialogueTree);
        /// <summary>Canvas and inspector have independent navigation inside a fixed viewport.</summary>
        protected override bool ScrollInspector => false;
        #endregion
        #region Methods
        #region Window
        /// <summary>Opens a tree without applying the parent's pending interaction changes.</summary>
        /// <param name="tree">Shared graph to edit.</param>
        /// <param name="context">Optional order catalogs from the selected object.</param>
        public static void Open(DialogueTree tree, OrderSettings context = null)
        {
            DialogueTreeWindow window = GetWindow<DialogueTreeWindow>("Dialogue Tree");
            if (!window.hasUnsavedChanges)
                window.orders = context != null ? ObjectWorkspace.Copy(context) : null;
            window.Select(tree);
            window.minSize = new Vector2(950f, 650f);
        }

        /// <summary>Gives the graph a fixed viewport above the shared Apply/Discard footer.</summary>
        /// <param name="editor">Cached editor for the detached graph.</param>
        protected override void DrawInspector(UnityEditor.Editor editor)
        {
            ((DialogueTreeEditor)editor).Draw(Mathf.Max(480f, position.height - 98f), orders);
        }

        /// <summary>Rejects broken links, cycles and unresolved graph data before saving.</summary>
        /// <param name="proposal">Detached tree.</param>
        /// <param name="issue">Receives an invalid operation or dependency.</param>
        /// <returns>True when the graph can replace its shared source.</returns>
        protected override bool Validate(ScriptableObject proposal, out string issue)
        {
            return ((DialogueTree)proposal).TryValidate(out issue, orders);
        }
        #endregion
        #endregion
    }
}
#endif
