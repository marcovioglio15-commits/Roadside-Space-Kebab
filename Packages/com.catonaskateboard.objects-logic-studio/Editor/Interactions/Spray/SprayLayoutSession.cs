using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Applies only a nozzle pose while retaining unrelated main-window changes.</summary>
    [Serializable]
    internal sealed class SprayLayoutSession
    {
        #region Fields
        [Header("Layout Session")]
        [Tooltip("Emitter object retained independently of workspace navigation.")]
        public ObjectWorkspaceTarget Target = new ObjectWorkspaceTarget();
        [Tooltip("Independent nozzle proposal.")]
        public SprayNozzle Draft = new SprayNozzle();
        [Tooltip("Applied nozzle snapshot used to detect outside edits.")]
        public string Baseline = string.Empty;
        [Tooltip("Main-window nozzle snapshot used to detect conflicting edits.")]
        public string WorkspaceBaseline = string.Empty;
        #endregion
        #region Properties
        internal bool HasChanges => Baseline.Length > 0 && JsonUtility.ToJson(Draft) != Baseline;
        #endregion
        #region Methods
        #region Transaction
        /// <summary>Copies the selected draft without sharing its mutable nozzle object.</summary>
        /// <param name="workspace">Current main-window transaction.</param>
        internal void Read(ObjectWorkspace workspace)
        {
            GameObject root = workspace.Target.Resolve();
            if (root == null || !root.TryGetComponent(out ObjectSpraySauce spray))
                return;
            Target.Capture(root, 0);
            Draft = ObjectWorkspace.Copy(workspace.Extended.Draft.SpraySauce.Nozzle);
            Baseline = JsonUtility.ToJson(spray.Settings.Nozzle);
            WorkspaceBaseline = JsonUtility.ToJson(Draft);
        }
        /// <summary>Reloads the applied pose without discarding unrelated workspace settings.</summary>
        internal void Discard()
        {
            GameObject root = Target.Resolve();
            if (root == null || !root.TryGetComponent(out ObjectSpraySauce spray))
                return;
            Draft = ObjectWorkspace.Copy(spray.Settings.Nozzle);
            Baseline = JsonUtility.ToJson(Draft);
            ObjectWorkspace workspace = ObjectWorkspace.instance;
            if (workspace.Target.Resolve() == root && workspace.Extended.Kind == ExtendedInteractionKind.SpraySauce)
                WorkspaceBaseline = JsonUtility.ToJson(workspace.Extended.Draft.SpraySauce.Nozzle);
        }
        /// <summary>Commits the pose only after source and workspace conflict checks.</summary>
        /// <param name="warning">Receives validation or conflict details.</param>
        /// <returns>True when the source nozzle has been saved.</returns>
        internal bool Apply(out string warning)
        {
            GameObject root = Target.Resolve();
            warning = "Open the source prefab before applying its nozzle layout.";
            if (!Target.IsOpen || root == null || !root.TryGetComponent(out ObjectSpraySauce spray)
                || !ObjectAuthoringSave.TryValidate(root, out warning))
                return false;
            ObjectWorkspace workspace = ObjectWorkspace.instance;
            bool selected = workspace.Target.Resolve() == root && workspace.Extended.Kind == ExtendedInteractionKind.SpraySauce;
            warning = "The nozzle changed in another editor. Discard to reload it.";
            if (JsonUtility.ToJson(spray.Settings.Nozzle) != Baseline
                || selected && JsonUtility.ToJson(workspace.Extended.Draft.SpraySauce.Nozzle) != WorkspaceBaseline)
                return false;
            warning = "Use finite nozzle position and rotation values.";
            if (!Draft.IsValid())
                return false;
            Undo.RecordObject(spray, "Apply Nozzle Layout");
            spray.Settings.Nozzle = ObjectWorkspace.Copy(Draft);
            EditorUtility.SetDirty(spray);
            PrefabUtility.RecordPrefabInstancePropertyModifications(spray);
            ObjectAuthoringSave.Save(root);
            if (selected)
            {
                Undo.RecordObject(workspace, "Refresh Applied Nozzle");
                workspace.Extended.Draft.SpraySauce.Nozzle = ObjectWorkspace.Copy(Draft);
                workspace.Extended.Baseline.SpraySauce.Nozzle = ObjectWorkspace.Copy(Draft);
                workspace.Persist();
            }
            Discard();
            warning = string.Empty;
            return true;
        }
        #endregion
        #endregion
    }
}
