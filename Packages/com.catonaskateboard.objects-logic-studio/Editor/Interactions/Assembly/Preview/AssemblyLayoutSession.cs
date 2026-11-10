using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits product magnets independently before merging them into the group that opened the preview.</summary>
    [Serializable]
    internal sealed class AssemblyLayoutSession
    {
        #region Fields

        [Header("Layout")]
        [Tooltip("Settings group that opened this preview.")]
        [SerializeField]
        private ObjectLayoutSource source = new ObjectLayoutSource();
        [Tooltip("Ingredient placements shown in the preview.")]
        public AssemblyProductDraft Draft = new AssemblyProductDraft();
        [Tooltip("Placements copied when this preview was opened or last applied.")]
        [SerializeField]
        private AssemblyProductSettings baseline;
        [Tooltip("Container count threshold opened by the fill preview. Negative means Assembly Product.")]
        [SerializeField]
        private int fillThreshold = -1;

        #endregion

        #region Properties

        /// <summary>Object used for geometry and ingredient guides.</summary>
        internal ObjectWorkspaceTarget Target => source.Target;
        /// <summary>Whether these placements belong to a container threshold.</summary>
        internal bool IsItemFill => fillThreshold >= 0;

        /// <summary>Whether the preview contains changes beyond its opening draft.</summary>
        internal bool HasChanges => baseline != null && Capture(Draft.Settings) != Capture(baseline);

        #endregion

        #region Methods

        #region Transactions

        /// <summary>Follows source draft changes while no local handle edits are pending.</summary>
        internal void Refresh()
        {
            if (!HasChanges)
                Discard();
        }

        /// <summary>Copies pending placements without changing the original group or prefab.</summary>
        /// <param name="workspace">Draft that opened the preview.</param>
        /// <param name="product">Product represented by the draft.</param>
        internal void Read(ObjectWorkspace workspace, ObjectAssemblyProduct product)
        {
            // Reject mismatched callers before retaining their transaction.
            if (ObjectWorkspaceDrafts.Selected(workspace) != product)
                throw new InvalidOperationException("Open this preview from the product's settings.");
            fillThreshold = -1;
            source.Capture(workspace);
            ReadDraft(workspace);
        }

        /// <summary>Opens the same preview for one container fill threshold.</summary>
        /// <param name="workspace">Container draft owning the layout.</param>
        /// <param name="threshold">Unique count identifying the selected step.</param>
        internal void ReadFill(ObjectWorkspace workspace, int threshold)
        {
            fillThreshold = threshold;
            source.Capture(workspace);
            ReadDraft(workspace);
        }

        /// <summary>Reloads placements from the original group, including its pending settings.</summary>
        internal void Discard()
        {
            source.Edit(ReadDraft, out _);
        }

        /// <summary>Merges only magnets into the source draft after checking for concurrent layout edits.</summary>
        /// <param name="warning">Receives missing source, invalid placement or conflict details.</param>
        /// <returns>True when the original group received the preview's placements.</returns>
        internal bool Apply(out string warning)
        {
            bool applied = source.Edit(workspace =>
            {
                if (IsItemFill)
                {
                    ApplyFill(workspace);
                    return;
                }
                // Compare only placements so unrelated group edits remain independent.
                AssemblyProductSettings current = workspace.Extended.Draft.AssemblyProduct.Settings;
                if (Capture(current) != Capture(baseline))
                    throw new InvalidOperationException("The layout changed in another window. Discard to reload it.");
                AssemblyProductSettings proposal = ObjectWorkspace.Copy(current);
                proposal.Magnets = ObjectWorkspace.Copy(Draft).Settings.Magnets;
                if (!AssemblyValidation.TryValidate(Target.Resolve(), proposal, out string detail))
                    throw new InvalidOperationException(detail);
                Undo.RecordObject(workspace, "Apply Magnet Layout to Draft");
                current.Magnets = proposal.Magnets;
                ReadDraft(workspace);
            }, out warning);
            if (applied)
                source.Notify();
            return applied;
        }

        #endregion

        #region Snapshots

        /// <summary>Refreshes the independent proposal and its comparison baseline from the caller.</summary>
        /// <param name="workspace">Exact product draft retained by the preview.</param>
        private void ReadDraft(ObjectWorkspace workspace)
        {
            Draft = IsItemFill ? new AssemblyProductDraft { Settings = FillPreview(FindStep(workspace).ItemFill) }
                : ObjectWorkspace.Copy(workspace.Extended.Draft.AssemblyProduct);
            baseline = ObjectWorkspace.Copy(new AssemblyProductSettings { Magnets = Draft.Settings.Magnets });
        }

        /// <summary>Applies a fill layout only if its original step and magnet values still match.</summary>
        /// <param name="workspace">Container draft receiving these placements.</param>
        private void ApplyFill(ObjectWorkspace workspace)
        {
            InventoryFillStep step = FindStep(workspace);
            if (Capture(FillPreview(step.ItemFill)) != Capture(baseline))
                throw new InvalidOperationException("This fill layout changed in another window. Discard to reload it.");
            InventoryItemFill proposal = new InventoryItemFill
            {
                VisualPrefab = step.ItemFill.VisualPrefab,
                Magnets = Array.ConvertAll(Draft.Settings.Magnets, magnet => new InventoryFillMagnet
                {
                    Position = magnet.Position,
                    Rotation = magnet.Rotation,
                    Scale = magnet.Scale
                })
            };
            if (!proposal.TryValidate(step.Count, out string warning))
                throw new InvalidOperationException(warning);
            Undo.RecordObject(workspace, "Apply Fill Magnet Layout");
            step.ItemFill = proposal;
            ReadDraft(workspace);
        }

        /// <summary>Finds the same unique container threshold after draft changes.</summary>
        /// <param name="workspace">Workspace holding the original container.</param>
        /// <returns>Fill step that owns the layout.</returns>
        private InventoryFillStep FindStep(ObjectWorkspace workspace)
        {
            InventoryFillStep match = null;
            foreach (InventoryFillStep step in workspace.Single.Draft.Container.FillSteps)
                if (step != null && step.Count == fillThreshold)
                {
                    if (match != null)
                        throw new InvalidOperationException("Fill steps need unique counts.");
                    match = step;
                }
            if (match is not { Mode: InventoryFillMode.ActualItemFill })
                throw new InvalidOperationException("The original Actual Item Fill step is no longer available.");
            return match;
        }

        /// <summary>Projects fill magnets into the existing mesh guide format without touching recipe settings.</summary>
        /// <param name="fill">Container visual layout and optional guide model.</param>
        /// <returns>Preview settings with independent positions, rotations and scales.</returns>
        private static AssemblyProductSettings FillPreview(InventoryItemFill fill)
        {
            return new AssemblyProductSettings
            {
                Magnets = Array.ConvertAll(fill.Magnets, magnet => new AssemblyMagnet
                {
                    Name = "Fill Unit",
                    PreviewPrefab = fill.VisualPrefab,
                    Position = magnet.Position,
                    Rotation = magnet.Rotation,
                    Scale = magnet.Scale
                })
            };
        }

        /// <summary>Compares only layout values so recipe and feedback changes do not cause conflicts.</summary>
        /// <param name="settings">Configuration containing the placements.</param>
        /// <returns>Serialized placement values, or empty when no layout exists.</returns>
        private static string Capture(AssemblyProductSettings settings)
        {
            return settings != null ? JsonUtility.ToJson(new AssemblyProductSettings { Magnets = settings.Magnets }) : string.Empty;
        }

        #endregion

        #endregion
    }
}
