using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Keeps a layout preview attached to the draft that opened it, including parked interactions.</summary>
    [Serializable]
    internal sealed class ObjectLayoutSource
    {
        #region Fields

        [Header("Source")]
        [Tooltip("Object used by the preview camera.")]
        public ObjectWorkspaceTarget Target = new ObjectWorkspaceTarget();
        [Tooltip("Draft that receives the preview's changes.")]
        [SerializeField]
        private ObjectWorkspace document;
        [Tooltip("Interaction that opened the preview.")]
        [SerializeField]
        private string key;

        #endregion

        #region Methods

        #region Binding

        /// <summary>Retains the exact caller rather than assuming the main window owns this preview.</summary>
        /// <param name="workspace">Main workspace or detached group document.</param>
        internal void Capture(ObjectWorkspace workspace)
        {
            document = workspace;
            key = workspace.DraftKey;
            Target = ObjectWorkspace.Copy(workspace.Target);
        }

        /// <summary>Reads or edits the retained interaction without changing the visible selection.</summary>
        /// <param name="operation">Operation on the exact interaction draft.</param>
        /// <param name="warning">Receives missing source or conflict details.</param>
        /// <returns>True when the operation completed on its original draft.</returns>
        internal bool Edit(Action<ObjectWorkspace> operation, out string warning)
        {
            warning = "Reopen the preview from its settings group.";
            if (document == null)
                return false;
            try
            {
                // A parked draft may still exist after the main window selects another interaction.
                if (!ObjectWorkspaceDrafts.Edit(document, key, () =>
                {
                    if (ObjectWorkspaceDrafts.Selected(document) == null)
                        throw new InvalidOperationException("This interaction is no longer available.");
                    operation(document);
                }))
                    return false;
                warning = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                warning = exception.Message;
                return false;
            }
        }

        /// <summary>Refreshes controls after a preview changes its source document.</summary>
        internal void Notify()
        {
            ObjectGroupWindow.RefreshDocument(document);
        }

        #endregion

        #endregion
    }
}
