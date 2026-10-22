using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Wraps one nozzle or several dispenser points for independent pose-only transactions.</summary>
    [Serializable]
    internal sealed class OutletLayout
    {
        #region Fields

        [Tooltip("Independent local output poses shown in the layout preview.")]
        public DispenserOutlet[] Points = { new DispenserOutlet() };

        #endregion
    }

    /// <summary>Commits only outlet poses while preserving unrelated main-window proposals.</summary>
    [Serializable]
    internal sealed class OutletLayoutSession
    {
        #region Fields

        [Header("Layout Session")]
        [Tooltip("Source retained independently of main-window navigation.")]
        public ObjectWorkspaceTarget Target = new ObjectWorkspaceTarget();
        [Tooltip("Whether this transaction edits dispenser outlets instead of one spray nozzle.")]
        public bool Dispenser;
        [Tooltip("Detached output poses edited by this preview.")]
        public OutletLayout Draft = new OutletLayout();
        [Tooltip("Applied layout snapshot used to detect outside edits.")]
        public string Baseline = string.Empty;
        [Tooltip("Main-window layout snapshot used to detect conflicting proposals.")]
        public string WorkspaceBaseline = string.Empty;

        #endregion

        #region Properties

        /// <summary>Whether the independent proposal differs from its applied layout.</summary>
        internal bool HasChanges => Baseline.Length > 0 && JsonUtility.ToJson(Draft) != Baseline;

        #endregion

        #region Methods
        #region Transaction

        /// <summary>Copies the selected card's current proposal while retaining the source conflict baseline.</summary>
        /// <param name="workspace">Main tool containing the pending layout settings.</param>
        /// <param name="dispenser">Whether to edit the dispenser's output array.</param>
        internal void Read(ObjectWorkspace workspace, bool dispenser)
        {
            Dispenser = dispenser;
            Target.Capture(workspace.Target.Resolve(), 0);
            Draft = ReadLayout(workspace);
            Baseline = JsonUtility.ToJson(ReadLayout(null));
            WorkspaceBaseline = JsonUtility.ToJson(Draft);
        }

        /// <summary>Refreshes an unedited layout after external source changes without losing conflict detection.</summary>
        internal void Refresh()
        {
            if (HasChanges || Target.Resolve() == null)
                return;
            OutletLayout applied = ReadLayout(null);
            if (applied == null || JsonUtility.ToJson(applied) == Baseline)
                return;
            Draft = applied;
            Baseline = JsonUtility.ToJson(applied);
            if (Selected(ObjectWorkspace.instance) && JsonUtility.ToJson(ReadLayout(ObjectWorkspace.instance)) == Baseline)
                WorkspaceBaseline = Baseline;
        }

        /// <summary>Reloads the applied layout without changing other card proposals.</summary>
        internal void Discard()
        {
            OutletLayout applied = ReadLayout(null);
            if (applied == null)
                return;
            Draft = applied;
            Baseline = JsonUtility.ToJson(applied);
            if (Selected(ObjectWorkspace.instance))
                WorkspaceBaseline = JsonUtility.ToJson(ReadLayout(ObjectWorkspace.instance));
        }

        /// <summary>Validates source and main-window baselines before saving only this layout.</summary>
        /// <param name="warning">Receives missing source, invalid poses or conflicting edits.</param>
        /// <returns>True after the output poses were saved.</returns>
        internal bool Apply(out string warning)
        {
            GameObject root = Target.Resolve();
            warning = "Open the source prefab before applying its outlet layout.";
            if (!Target.IsOpen || root == null || ReadLayout(null) == null || !ObjectAuthoringSave.TryValidate(root, out warning))
                return false;
            ObjectWorkspace workspace = ObjectWorkspace.instance;
            bool selected = Selected(workspace);
            warning = "The layout changed in another editor. Discard to reload it.";
            if (JsonUtility.ToJson(ReadLayout(null)) != Baseline
                || selected && JsonUtility.ToJson(ReadLayout(workspace)) != WorkspaceBaseline)
                return false;
            warning = "Add at least one outlet with finite position and rotation values.";
            if (Draft?.Points == null || Draft.Points.Length == 0 || !Dispenser && Draft.Points.Length != 1)
                return false;
            foreach (DispenserOutlet point in Draft.Points)
                if (point is not { IsValid: true })
                    return false;
            ObjectInteraction feature = Dispenser ? root.GetComponent<ObjectDispenser>() : root.GetComponent<ObjectSpraySauce>();
            Undo.RecordObject(feature, "Apply Outlet Layout");
            Write(null, false);
            EditorUtility.SetDirty(feature);
            PrefabUtility.RecordPrefabInstancePropertyModifications(feature);
            ObjectAuthoringSave.Save(root);
            if (selected)
            {
                Undo.RecordObject(workspace, "Refresh Applied Outlets");
                Write(workspace, false);
                Write(workspace, true);
                workspace.Persist();
            }
            Discard();
            warning = string.Empty;
            return true;
        }

        #endregion
        #region Source Access

        /// <summary>Checks that a main-window card still refers to this layout's source and interaction.</summary>
        /// <param name="workspace">Current main-window transaction.</param>
        /// <returns>True for this exact card on the retained source.</returns>
        private bool Selected(ObjectWorkspace workspace)
        {
            return workspace.Target.Resolve() == Target.Resolve() && (Dispenser
                ? workspace.Single.Kind == SingleInteractionKind.Dispenser
                : workspace.Extended.Kind == ExtendedInteractionKind.SpraySauce);
        }

        /// <summary>Copies a layout from the applied component or one exact workspace snapshot.</summary>
        /// <param name="workspace">Null for the applied component; otherwise the current main-window transaction.</param>
        /// <returns>An independent layout, or null if its component was removed.</returns>
        private OutletLayout ReadLayout(ObjectWorkspace workspace)
        {
            GameObject root = Target.Resolve();
            if (root == null)
                return null;
            if (Dispenser)
            {
                DispenserSettings settings = workspace != null ? workspace.Single.Draft.Dispenser
                    : root.GetComponent<ObjectDispenser>()?.Settings;
                return settings != null ? new OutletLayout { Points = ObjectWorkspace.Copy(new OutletLayout { Points = settings.Outlets }).Points } : null;
            }
            SprayNozzle nozzle = workspace != null ? workspace.Extended.Draft.SpraySauce.Nozzle
                : root.GetComponent<ObjectSpraySauce>()?.Settings.Nozzle;
            return nozzle != null ? new OutletLayout { Points = new[] { new DispenserOutlet { Position = nozzle.Position, Rotation = nozzle.Rotation } } } : null;
        }

        /// <summary>Writes only the selected outlet fields, preserving every unrelated configuration value.</summary>
        /// <param name="workspace">Null for the component, otherwise the selected main-window transaction.</param>
        /// <param name="baseline">Whether to update the main-window applied snapshot.</param>
        private void Write(ObjectWorkspace workspace, bool baseline)
        {
            GameObject root = Target.Resolve();
            if (Dispenser)
            {
                DispenserSettings settings = workspace != null ? (baseline ? workspace.Single.Baseline : workspace.Single.Draft).Dispenser
                    : root.GetComponent<ObjectDispenser>().Settings;
                settings.Outlets = ObjectWorkspace.Copy(Draft).Points;
            }
            else
            {
                SpraySauceSettings settings = workspace != null ? (baseline ? workspace.Extended.Baseline : workspace.Extended.Draft).SpraySauce
                    : root.GetComponent<ObjectSpraySauce>().Settings;
                settings.Nozzle = new SprayNozzle { Position = Draft.Points[0].Position, Rotation = Draft.Points[0].Rotation };
            }
        }

        #endregion
        #endregion
    }
}
