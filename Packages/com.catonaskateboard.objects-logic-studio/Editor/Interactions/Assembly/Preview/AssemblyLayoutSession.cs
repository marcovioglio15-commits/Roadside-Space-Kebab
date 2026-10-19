using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Retains an independent magnet proposal and merges only its layout into the product.</summary>
    [Serializable]
    internal sealed class AssemblyLayoutSession
    {
        #region Fields

        [Header("Layout Session")]
        [Tooltip("Product branch retained independently of the main window selection.")]
        public ObjectWorkspaceTarget Target = new ObjectWorkspaceTarget();
        [Tooltip("Independent product snapshot used to preview the pending magnets.")]
        public AssemblyProductDraft Draft = new AssemblyProductDraft();
        [Tooltip("Applied magnet state used to reject outside layout edits.")]
        public AssemblyProductSettings Baseline;
        [Tooltip("Initial main-window layout used to protect its pending edits.")]
        public AssemblyProductSettings WorkspaceBaseline;
        [Tooltip("Layout at the beginning of this window's current transaction.")]
        public AssemblyProductSettings Initial;

        #endregion
        #region Properties

        /// <summary>Whether this window has changed its independent layout.</summary>
        internal bool HasChanges => Initial != null && Capture(Draft.Settings) != Capture(Initial);

        #endregion
        #region Methods
        #region Transaction
        /// <summary>Follows source layout updates only while this preview has no pending handle edits.</summary>
        internal void Refresh()
        {
            GameObject root = Target.Resolve();
            if (HasChanges || root == null || !root.TryGetComponent(out ObjectAssemblyProduct product)
                || Capture(product.Settings) == Capture(Baseline))
                return;
            Draft = AssemblyProductDraft.Capture(product.Settings);
            Initial = Baseline = Snapshot(product.Settings);
            // Preserve conflicts with independently edited magnets in the main workspace.
            ObjectWorkspace workspace = ObjectWorkspace.instance;
            if (workspace.Target.Resolve() == root && workspace.Extended.Kind == ExtendedInteractionKind.AssemblyProduct
                && Capture(workspace.Extended.Draft.AssemblyProduct.Settings) == Capture(Baseline))
                WorkspaceBaseline = Baseline;
        }


        /// <summary>Starts an independent proposal from the product card's visible configuration.</summary>
        /// <param name="workspace">Main window containing the current product proposal.</param>
        /// <param name="product">Applied product component.</param>
        internal void Read(ObjectWorkspace workspace, ObjectAssemblyProduct product)
        {
            // Pending values are copied, so preview handles never mutate the main window directly.
            Target.Capture(product.gameObject, 0);
            Draft = ObjectWorkspace.Copy(workspace.Extended.Draft.AssemblyProduct);
            Baseline = Snapshot(product.Settings);
            Initial = Baseline;
            WorkspaceBaseline = Snapshot(Draft.Settings);
        }

        /// <summary>Reloads the applied layout while preserving the preview's product identity.</summary>
        internal void Discard()
        {
            // A missing prefab retains its proposal until the source becomes available again.
            GameObject root = Target.Resolve();
            if (root == null || !root.TryGetComponent(out ObjectAssemblyProduct product))
                return;
            Draft = AssemblyProductDraft.Capture(product.Settings);
            Initial = Baseline = Snapshot(product.Settings);
            ObjectWorkspace workspace = ObjectWorkspace.instance;
            if (workspace.Target.Resolve() == root && workspace.Extended.Kind == ExtendedInteractionKind.AssemblyProduct)
                WorkspaceBaseline = Snapshot(workspace.Extended.Draft.AssemblyProduct.Settings);
        }

        /// <summary>Saves only magnets after checking source and main-window conflicts.</summary>
        /// <param name="warning">Receives invalid layout or concurrent-edit details.</param>
        /// <returns>True when the product layout was committed.</returns>
        internal bool Apply(out string warning)
        {
            // Current non-layout configuration remains authoritative for validation and persistence.
            GameObject root = Target.Resolve();
            warning = "Open the original product prefab before applying this layout.";
            if (!Target.IsOpen || root == null || !root.TryGetComponent(out ObjectAssemblyProduct product)
                || !ObjectAuthoringSave.TryValidate(root, out warning))
                return false;
            ObjectWorkspace workspace = ObjectWorkspace.instance;
            bool selected = workspace.Target.Resolve() == root && workspace.Extended.Kind == ExtendedInteractionKind.AssemblyProduct;
            if (Capture(product.Settings) != Capture(Baseline) || selected && Capture(workspace.Extended.Draft.AssemblyProduct.Settings) != Capture(WorkspaceBaseline))
            {
                warning = "The layout changed in another editor. Discard to reload it.";
                return false;
            }
            AssemblyProductSettings proposal = ObjectWorkspace.Copy(product.Settings);
            proposal.Magnets = ObjectWorkspace.Copy(Draft).Settings.Magnets;
            if (!AssemblyValidation.TryValidate(root, proposal, out warning))
                return false;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            try
            {
                Undo.RecordObject(product, "Apply Magnet Layout");
                product.Settings.Magnets = proposal.Magnets;
                EditorUtility.SetDirty(product);
                PrefabUtility.RecordPrefabInstancePropertyModifications(product);
                ObjectAuthoringSave.Save(root);
                if (selected)
                {
                    // Rebase only magnets, preserving every unrelated pending interaction field.
                    Undo.RecordObject(workspace, "Refresh Applied Magnet Layout");
                    workspace.Extended.Draft.AssemblyProduct.Settings.Magnets = ObjectWorkspace.Copy(Draft).Settings.Magnets;
                    workspace.Extended.Baseline.AssemblyProduct.Settings.Magnets = ObjectWorkspace.Copy(Draft).Settings.Magnets;
                    workspace.Persist();
                }
                Discard();
                Undo.CollapseUndoOperations(group);
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                warning = "Layout Apply failed: " + exception.Message;
                return false;
            }
        }

        /// <summary>Retains native asset references so layout comparisons remain valid after restarting Unity.</summary>
        /// <param name="settings">Layout whose magnets become an independent baseline.</param>
        /// <returns>A detached magnet snapshot serialized with the preview window.</returns>
        private static AssemblyProductSettings Snapshot(AssemblyProductSettings settings)
        {
            // Strings containing temporary instance identifiers cannot serve as durable reference baselines.
            return ObjectWorkspace.Copy(new AssemblyProductSettings { Magnets = settings.Magnets });
        }

        /// <summary>Captures only magnets so unrelated product edits do not cause false conflicts.</summary>
        /// <param name="settings">Product configuration containing the layout.</param>
        /// <returns>A stable serialized layout snapshot.</returns>
        private static string Capture(AssemblyProductSettings settings)
        {
            return settings != null ? JsonUtility.ToJson(new AssemblyProductSettings { Magnets = settings.Magnets }) : string.Empty;
        }

        #endregion
        #endregion
    }
}
