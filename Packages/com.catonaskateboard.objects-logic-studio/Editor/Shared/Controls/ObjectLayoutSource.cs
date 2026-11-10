using System;
using UnityEngine;
using Object = UnityEngine.Object;

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
        [Tooltip("Preset sample retained when its settings window closes before this layout.")]
        [SerializeReference]
        private ObjectLayoutPresetOwner presetOwner;

        #endregion

        #region Methods

        #region Binding

        /// <summary>Retains the exact caller rather than assuming the main window owns this preview.</summary>
        /// <param name="workspace">Main workspace or detached group document.</param>
        internal void Capture(ObjectWorkspace workspace)
        {
            ReleasePreset();
            document = workspace;
            key = workspace.DraftKey;
            Target = ObjectWorkspace.Copy(workspace.Target);
        }

        /// <summary>Checks whether a prefab operation would affect this preview’s source.</summary>
        /// <param name="prefabGuid">Prefab involved in the operation.</param>
        /// <param name="componentId">Exact component, or zero for the whole prefab.</param>
        /// <returns>True when the operation targets the retained source.</returns>
        internal bool Uses(string prefabGuid, long componentId)
        {
            return Target.PrefabGuid == prefabGuid && (componentId == 0
                || key != null && key.EndsWith(":" + componentId, StringComparison.Ordinal));
        }

        /// <summary>Reads or edits the retained interaction without changing the visible selection.</summary>
        /// <param name="operation">Operation on the exact interaction draft.</param>
        /// <param name="warning">Receives missing source or conflict details.</param>
        /// <returns>True when the operation completed on its original draft.</returns>
        internal bool Edit(Action<ObjectWorkspace> operation, out string warning)
        {
            warning = "Reopen the preview from its settings group.";
            // The prefab layout can still be saved after its parent settings window closes.
            ObjectWorkspace workspace = document != null ? document
                : !string.IsNullOrEmpty(Target.PrefabGuid) ? ObjectWorkspace.instance : null;
            if (workspace == null)
                return false;
            try
            {
                if (!ObjectWorkspaceDrafts.Edit(workspace, key, () =>
                {
                    if (ObjectWorkspaceDrafts.Selected(workspace) == null)
                        throw new InvalidOperationException("This interaction is no longer available.");
                    operation(workspace);
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

        /// <summary>Saves only the preview's edited placements and rebases its caller.</summary>
        /// <param name="edit">Validates and writes placements into a temporary proposal.</param>
        /// <param name="paths">Fields belonging to this preview.</param>
        /// <param name="reload">Reloads the saved layout into the preview.</param>
        /// <param name="warning">Receives a conflict or validation error.</param>
        /// <param name="fillThreshold">Optional Actual Item Fill threshold.</param>
        /// <returns>True when the preview's own section was saved.</returns>
        internal bool Apply(Action<ObjectWorkspace> edit, string[] paths, Action<ObjectWorkspace> reload, out string warning, int fillThreshold = -1)
        {
            bool applied = Edit(workspace =>
            {
                ObjectWorkspace proposal = ScriptableObject.CreateInstance<ObjectWorkspace>();
                try
                {
                    ObjectWorkspaceDraft.Capture(workspace).Restore(proposal);
                    edit(proposal);
                    if (workspace.Target.IsPreview)
                    {
                        if (presetOwner == null)
                            ObjectGroupWindow.SavePresetLayout(workspace, proposal, paths, fillThreshold);
                        else
                            presetOwner.Apply(workspace, proposal, paths, fillThreshold);
                    }
                    else
                    {
                        ObjectWorkspace retained = ObjectWorkspace.instance;
                        if (!ObjectWorkspaceDrafts.Edit(retained, key, () =>
                        {
                            if (!ObjectSectionChanges.Apply(proposal, retained, paths, out string detail, fillThreshold))
                                throw new InvalidOperationException(detail);
                            if (workspace != retained)
                            {
                                ObjectSectionChanges.Synchronize(proposal, workspace, paths, fillThreshold);
                                ObjectGroupWindow.LayoutSaved(workspace, retained);
                            }
                        }))
                            throw new InvalidOperationException("The original interaction is no longer available.");
                    }
                    reload(workspace);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(proposal);
                }
            }, out warning);
            if (applied)
                Notify();
            return applied;
        }

        /// <summary>Transfers a preset sample to its dirty layout when the settings window closes first.</summary>
        /// <param name="workspace">Document whose window is closing.</param>
        /// <param name="preview">Isolated sample owned by that window.</param>
        /// <param name="preset">Preset edited by the document.</param>
        /// <param name="group">Settings group represented by the preset.</param>
        /// <param name="stamp">Preset stamp before the remaining layout changes.</param>
        /// <param name="canSave">Whether its local links are resolved.</param>
        /// <returns>True when a layout now owns the detached sample and document.</returns>
        internal static bool KeepPreset(ObjectWorkspace workspace, GameObject preview, ObjectGroupPreset preset, ObjectGroupInfo group, string stamp, bool canSave)
        {
            if (workspace == null || preview == null)
                return false;
            ObjectLayoutSource layout = PendingPreset(workspace);
            if (layout == null)
                return false;
            layout.presetOwner = new ObjectLayoutPresetOwner { Preview = preview, Preset = preset, Group = group, Stamp = stamp, CanSave = canSave };
            return true;
        }

        /// <summary>Finds a layout that still owns edits using a particular sample.</summary>
        /// <param name="workspace">Preset proposal about to close or change samples.</param>
        /// <returns>The pending layout source, or null.</returns>
        internal static ObjectLayoutSource PendingPreset(ObjectWorkspace workspace)
        {
            // Each interaction type can open one kind of layout viewport.
            foreach (AssemblyPreviewWindow window in Resources.FindObjectsOfTypeAll<AssemblyPreviewWindow>())
                if (window.hasUnsavedChanges && window.Source.document == workspace)
                    return window.Source;
            foreach (OutletLayoutWindow window in Resources.FindObjectsOfTypeAll<OutletLayoutWindow>())
                if (window.hasUnsavedChanges && window.Source.document == workspace)
                    return window.Source;
            return null;
        }

        /// <summary>Destroys the orphaned preset sample when its layout closes or changes source.</summary>
        internal void ReleasePreset()
        {
            if (presetOwner == null)
                return;
            if (document != null)
                Object.DestroyImmediate(document);
            ObjectPreviewIdentity.Unregister(presetOwner.Preview);
            if (presetOwner.Preview != null)
                Object.DestroyImmediate(presetOwner.Preview);
            presetOwner = null;
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
