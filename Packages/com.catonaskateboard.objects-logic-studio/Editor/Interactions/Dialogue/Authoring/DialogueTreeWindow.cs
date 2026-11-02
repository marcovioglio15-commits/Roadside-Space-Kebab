using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Owns a complete tree proposal with independent shared-preset transactions.</summary>
    public sealed class DialogueTreeWindow : StudioAssetWindow
    {
        #region Serialized Fields

        [Header("Context")]
        [Tooltip("Optional destination order settings used only by the tree editor.")]
        [SerializeReference]
        private OrderSettings orders;

        #endregion

        #region State

        private DialogueTreeEditor treeEditor;

        #endregion

        #region Properties

        protected override Type AssetType => typeof(DialogueTree);
        protected override bool ScrollInspector => false;

        #endregion

        #region Methods

        #region Window

        /// <summary>Opens the sheet without applying changes in the parent interaction.</summary>
        /// <param name="tree">Complete tree preset.</param>
        /// <param name="context">Optional order choices for this destination.</param>
        public static void Open(DialogueTree tree, OrderSettings context = null)
        {
            DialogueTreeWindow window = GetWindow<DialogueTreeWindow>("Dialogue Tree");
            if (!window.hasUnsavedChanges)
                window.orders = context != null ? ObjectWorkspace.Copy(context) : null;
            window.Select(tree);
            window.minSize = new Vector2(940f, 680f);
        }

        /// <summary>Gives the main and preview trees their own clipped navigation areas.</summary>
        /// <param name="editor">Cached editor for the detached sheet.</param>
        protected override void DrawInspector(UnityEditor.Editor editor)
        {
            treeEditor = (DialogueTreeEditor)editor;
            treeEditor.Draw(Mathf.Max(470f, position.height - 100f), orders);
        }

        /// <summary>Shares transaction conflicts with the canvas's bottom-left yellow warning.</summary>
        /// <param name="message">Current Apply, recovery or outside-edit issue.</param>
        protected override void DrawWarning(string message)
        {
            if (treeEditor != null && treeEditor.SetExternalWarning(message))
                Repaint();
        }

        /// <summary>Rejects incomplete entries or connections without changing their values.</summary>
        /// <param name="proposal">Detached tree.</param>
        /// <param name="issue">Receives the first graph problem.</param>
        /// <returns>True when the complete sheet can be saved.</returns>
        protected override bool Validate(ScriptableObject proposal, out string issue)
        {
            return ((DialogueTree)proposal).TryValidate(out issue, orders);
        }

        #endregion

        #endregion
    }
}
