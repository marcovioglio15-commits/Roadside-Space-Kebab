using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows selected transfer reach, visible storage slots and randomized spawn placement.</summary>
    internal static class TransferSpawnGizmos
    {
        #region Methods

        #region Drawing

        /// <summary>Draws only the selected object's valid transfer configuration.</summary>
        /// <param name="feature">Selected inventory interaction.</param>
        /// <param name="type">Unity selection flags.</param>
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void Transfer(ObjectTransferInteraction feature, GizmoType type)
        {
            // Finite validation keeps invalid authored values out of Handles calculations.
            if (feature.Target == null || !feature.Target.DrawGizmos || !feature.Target.TryValidate(out _))
                return;
            using Handles.DrawingScope scope = new Handles.DrawingScope(new Color(0.3f, 0.85f, 1f));
            Vector3 target = feature.transform.TransformPoint(feature.Target.Offset);
            Handles.DrawWireDisc(target, Vector3.up, feature.Target.Distance);
            Handles.Label(target, feature.Kind + " · " + feature.Target.Distance.ToString("0.##") + " m");
            switch (feature)
            {
                case ObjectDispenser dispenser when dispenser.Settings.TryValidate(out _):
                    Pose(dispenser.transform, Vector3.zero, dispenser.Settings.OutputRotation, "Dispenser pickup origin");
                    break;
                case ObjectContainer container when container.Settings.KeepVisible && container.Settings.TryValidate(out _):
                    int count = Application.isPlaying ? container.StoredCount : container.Settings.Unlimited ? 3 : Mathf.Min(12, container.Settings.Capacity);
                    for (int index = 0; index < count; index++)
                        Pose(container.transform, container.Settings.Position + container.Settings.Spacing * index,
                            container.Settings.Rotation, "Slot " + (index + 1));
                    break;
            }
        }

        /// <summary>Shows the spawn centre and uniform horizontal placement radius without drawing prefab duplicates.</summary>
        /// <param name="feature">Selected spawn rule.</param>
        /// <param name="type">Unity selection flags.</param>
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void Spawn(ObjectSpawnManager feature, GizmoType type)
        {
            // Output radius is measured in world metres and does not inherit parent scale.
            if (!feature.DrawGizmos || feature.Settings == null || !feature.Settings.TryValidate(out _))
                return;
            using Handles.DrawingScope scope = new Handles.DrawingScope(new Color(0.5f, 1f, 0.45f));
            Pose(feature.transform, feature.Settings.Position, feature.Settings.Rotation, "Spawn output");
            Handles.DrawWireDisc(feature.transform.TransformPoint(feature.Settings.Position), feature.transform.up, feature.Settings.Radius);
        }

        /// <summary>Draws a compact orientation marker at an authored local pose.</summary>
        /// <param name="root">Object defining the output frame.</param>
        /// <param name="position">Local output point.</param>
        /// <param name="rotation">Local Euler rotation.</param>
        /// <param name="label">Concise output or slot label.</param>
        private static void Pose(Transform root, Vector3 position, Vector3 rotation, string label)
        {
            // Screen-relative sizes stay readable while navigating the scene.
            Vector3 world = root.TransformPoint(position);
            float size = HandleUtility.GetHandleSize(world) * 0.12f;
            Handles.ArrowHandleCap(0, world, root.rotation * Quaternion.Euler(rotation), size, EventType.Repaint);
            Handles.Label(world + Vector3.up * size, label);
        }

        #endregion

        #endregion
    }
}
