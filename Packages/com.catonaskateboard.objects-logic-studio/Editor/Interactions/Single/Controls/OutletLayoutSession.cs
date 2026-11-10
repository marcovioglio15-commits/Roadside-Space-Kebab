using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Identifies the pose list opened in the common point preview.</summary>
    internal enum ObjectPointLayout { Nozzle, Outlets, Magnets, OrbitCenters }

    /// <summary>Wraps one nozzle or several dispenser points for independent pose editing.</summary>
    [Serializable]
    internal sealed class OutletLayout
    {
        #region Fields

        [Tooltip("Local output positions and directions shown in the preview.")]
        public DispenserOutlet[] Points = { new DispenserOutlet() };

        #endregion
    }

    /// <summary>Merges preview poses into the draft that opened it while preserving other settings.</summary>
    [Serializable]
    internal sealed class OutletLayoutSession
    {
        #region Fields

        [Header("Layout")]
        [Tooltip("Settings group that opened this preview.")]
        [SerializeField]
        private ObjectLayoutSource source = new ObjectLayoutSource();
        [Tooltip("Interaction pose list opened in this preview.")]
        public ObjectPointLayout Layout;
        [Tooltip("Prefab guide drawn at each floating supply magnet.")]
        public GameObject Portion;
        [Tooltip("Output positions and directions edited by this preview.")]
        public OutletLayout Draft = new OutletLayout();
        [Tooltip("Poses copied when the preview was opened or last applied.")]
        [SerializeField]
        private string baseline = string.Empty;

        #endregion

        #region Properties

        /// <summary>Whether the current layout accepts several points.</summary>
        internal bool Multiple => Layout != ObjectPointLayout.Nozzle;
        /// <summary>Whether the current points retain local Euler angles.</summary>
        internal bool Rotatable => Layout != ObjectPointLayout.Magnets;
        /// <summary>Short name used to identify one point in the preview.</summary>
        internal string PointName => Layout switch
        {
            ObjectPointLayout.OrbitCenters => "Orbit Center",
            ObjectPointLayout.Magnets => "Magnet",
            ObjectPointLayout.Outlets => "Outlet",
            _ => "Nozzle"
        };
        /// <summary>Object used for the preview geometry.</summary>
        internal ObjectWorkspaceTarget Target => source.Target;

        /// <summary>Whether the preview contains pose changes beyond its opening draft.</summary>
        internal bool HasChanges => baseline.Length > 0 && JsonUtility.ToJson(Draft) != baseline;

        #endregion

        #region Methods

        #region Transactions

        /// <summary>Copies the caller's current poses without changing the original group or prefab.</summary>
        /// <param name="workspace">Draft that opened the preview.</param>
        /// <param name="layout">Pose list selected by the caller.</param>
        internal void Read(ObjectWorkspace workspace, ObjectPointLayout layout)
        {
            Layout = layout;
            source.Capture(workspace);
            ReadDraft(workspace);
        }

        /// <summary>Follows changes in the source group while no local handle edits are pending.</summary>
        internal void Refresh()
        {
            if (!HasChanges)
                Discard();
        }

        /// <summary>Reloads poses from the original group, including its pending settings.</summary>
        internal void Discard()
        {
            source.Edit(ReadDraft, out _);
        }

        /// <summary>Copies only output poses into the source draft after validating them and checking conflicts.</summary>
        /// <param name="warning">Receives missing source, invalid pose or conflict details.</param>
        /// <returns>True when the original group received the preview's poses.</returns>
        internal bool Apply(out string warning)
        {
            bool applied = source.Edit(workspace =>
            {
                // Reject invalid poses without replacing the proposal or changing the source.
                if (JsonUtility.ToJson(ReadLayout(workspace)) != baseline)
                    throw new InvalidOperationException("The layout changed in another window. Discard to reload it.");
                if (Draft?.Points == null || Draft.Points.Length == 0 || !Multiple && Draft.Points.Length != 1)
                    throw new InvalidOperationException("Add at least one point. Spray Sauce uses one nozzle.");
                foreach (DispenserOutlet point in Draft.Points)
                    if (point is not { IsValid: true })
                        throw new InvalidOperationException("Use finite values for each point’s position and rotation.");
                Undo.RecordObject(workspace, "Apply Point Layout to Draft");
                switch (Layout)
                {
                    case ObjectPointLayout.OrbitCenters:
                        workspace.Extended.Draft.GravityGenerator.Float.Centers = Array.ConvertAll(Draft.Points,
                            point => new GravityOrbitCenter { Position = point.Position, Rotation = point.Rotation });
                        break;
                    case ObjectPointLayout.Magnets:
                        workspace.Single.Draft.Dispenser.ScoopDisplay.Magnets = Array.ConvertAll(Draft.Points, point => point.Position);
                        break;
                    case ObjectPointLayout.Outlets:
                        workspace.Single.Draft.Dispenser.Outlets = ObjectWorkspace.Copy(Draft).Points;
                        break;
                    case ObjectPointLayout.Nozzle:
                        workspace.Extended.Draft.SpraySauce.Nozzle = new SprayNozzle
                        {
                            Position = Draft.Points[0].Position,
                            Rotation = Draft.Points[0].Rotation
                        };
                        break;
                }
                ReadDraft(workspace);
            }, out warning);
            if (applied)
                source.Notify();
            return applied;
        }

        #endregion

        #region Snapshots

        /// <summary>Reloads independent pose values and resets the preview's change baseline.</summary>
        /// <param name="workspace">Exact draft retained by the preview.</param>
        private void ReadDraft(ObjectWorkspace workspace)
        {
            Portion = Layout == ObjectPointLayout.Magnets ? workspace.Single.Draft.Dispenser.Prefab : null;
            Draft = ReadLayout(workspace);
            baseline = JsonUtility.ToJson(Draft);
        }

        /// <summary>Copies the relevant pose fields from the caller's current proposal.</summary>
        /// <param name="workspace">Draft containing dispenser or spray settings.</param>
        /// <returns>Independent poses with their original array order.</returns>
        private OutletLayout ReadLayout(ObjectWorkspace workspace)
        {
            return new OutletLayout
            {
                Points = Layout switch
                {
                    ObjectPointLayout.OrbitCenters => Array.ConvertAll(workspace.Extended.Draft.GravityGenerator.Float.Centers,
                        center => new DispenserOutlet { Position = center.Position, Rotation = center.Rotation }),
                    ObjectPointLayout.Magnets => Array.ConvertAll(workspace.Single.Draft.Dispenser.ScoopDisplay.Magnets,
                        position => new DispenserOutlet { Position = position }),
                    ObjectPointLayout.Outlets => ObjectWorkspace.Copy(workspace.Single.Draft.Dispenser).Outlets,
                    _ => new[]
                    {
                        new DispenserOutlet
                        {
                            Position = workspace.Extended.Draft.SpraySauce.Nozzle.Position,
                            Rotation = workspace.Extended.Draft.SpraySauce.Nozzle.Rotation
                        }
                    }
                }
            };
        }

        #endregion

        #endregion
    }
}
