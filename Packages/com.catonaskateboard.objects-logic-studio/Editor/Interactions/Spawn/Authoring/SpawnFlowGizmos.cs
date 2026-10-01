using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows the first or active day's authored visitor origins and animation paths on selection.</summary>
    internal static class SpawnFlowGizmos
    {
        #region Methods
        #region Drawing

        /// <summary>Draws compact walk-in and walk-out guides for the selected manager.</summary>
        /// <param name="manager">Scene manager defining the plan's placement frame.</param>
        /// <param name="type">Unity selection flags.</param>
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void Draw(ObjectSpawnManager manager, GizmoType type)
        {
            // Invalid plans stay editable without passing nonfinite positions to Handles.
            if (!manager.DrawGizmos || manager.Settings.Mode != SpawnManagementMode.DayFlow
                || manager.Settings.Flow == null || !manager.Settings.Flow.TryValidate(out _))
                return;
            SpawnFlowPlan plan = manager.Settings.Flow;
            int day = Application.isPlaying && manager.CurrentDay > 0 ? manager.CurrentDay - 1 : 0;
            if (day >= plan.Days.Length)
                return;
            foreach (SpawnFlowStep step in plan.Days[day].Steps)
            {
                Vector3 origin = manager.transform.TransformPoint(step.Position);
                Quaternion orientation = manager.transform.rotation * Quaternion.Euler(step.Rotation);
                float size = HandleUtility.GetHandleSize(origin) * 0.04f;
                Handles.Label(origin + Vector3.up * size, plan.Days[day].Name + " · " + step.Name);
                Path(step.WalkIn, origin, orientation, new Color(0.3f, 0.85f, 1f), size);
                Path(step.WalkOut, origin, orientation, new Color(1f, 0.65f, 0.2f), size);
            }
        }

        /// <summary>Connects configured keyframes and labels their relative travel times.</summary>
        /// <param name="animation">Arrival or departure path.</param>
        /// <param name="origin">World spawn origin.</param>
        /// <param name="orientation">World spawn orientation.</param>
        /// <param name="color">Path colour identifying its phase.</param>
        /// <param name="size">Screen-scaled marker size.</param>
        private static void Path(SpawnFlowAnimation animation, Vector3 origin, Quaternion orientation, Color color, float size)
        {
            // Departure starts from the live pose at runtime; this guide shows its authored destinations.
            if (!animation.Enabled)
                return;
            using Handles.DrawingScope scope = new Handles.DrawingScope(color);
            Vector3 previous = origin;
            for (int index = 0; index < animation.Keyframes.Length; index++)
            {
                SpawnFlowKeyframe frame = animation.Keyframes[index];
                Vector3 point = animation.Space == SpawnFlowSpace.World ? frame.Pose.Position : origin + orientation * frame.Pose.Position;
                Handles.DrawDottedLine(previous, point, 4f);
                Handles.SphereHandleCap(0, point, Quaternion.identity, size, EventType.Repaint);
                Handles.Label(point + Vector3.up * size, (index + 1) + " · " + frame.Duration.ToString("0.##") + " s");
                previous = point;
            }
        }

        #endregion
        #endregion
    }
}
