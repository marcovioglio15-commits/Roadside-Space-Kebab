using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows selected Slice reach and the next cut's prefab origins without creating preview instances.</summary>
    internal static class SliceGizmos
    {
        #region Methods

        #region Drawing

        /// <summary>Draws unobtrusive range and output guides only for the selected interaction.</summary>
        /// <param name="slice">Selected component supplying settings and runtime progress.</param>
        /// <param name="type">Unity's selected-object gizmo context.</param>
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void Draw(ObjectSlice slice, GizmoType type)
        {
            // Invalid authored values remain editable and never reach scene-view drawing matrices.
            if (!slice.DrawGizmos || slice.Settings?.Target == null || !slice.Settings.TryValidate(out _))
                return;
            using Handles.DrawingScope scope = new Handles.DrawingScope(new Color(0.35f, 0.85f, 1f, 0.7f));
            Vector3 point = slice.transform.TransformPoint(slice.Settings.Target.Offset);
            Handles.DrawWireDisc(point, Vector3.up, slice.Settings.Target.Distance);
            int index = Application.isPlaying ? slice.CompletedSteps : 0;
            if (index >= slice.Settings.Steps.Length)
                return;
            foreach (SliceSpawn spawn in slice.Settings.Steps[index].Spawns)
            {
                Vector3 position = slice.transform.TransformPoint(spawn.Position);
                Handles.DrawDottedLine(point, position, 4f);
                Handles.SphereHandleCap(0, position, Quaternion.identity, HandleUtility.GetHandleSize(position) * 0.06f, EventType.Repaint);
            }
        }

        #endregion

        #endregion
    }
}
