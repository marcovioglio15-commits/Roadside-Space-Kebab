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

        #endregion

        #region Properties

        /// <summary>Object used for geometry and ingredient guides.</summary>
        internal ObjectWorkspaceTarget Target => source.Target;

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
            Draft = ObjectWorkspace.Copy(workspace.Extended.Draft.AssemblyProduct);
            baseline = ObjectWorkspace.Copy(new AssemblyProductSettings { Magnets = Draft.Settings.Magnets });
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
