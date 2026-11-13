using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows the first or active day's visitor origins and animation paths on selection.</summary>
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
                Vector3 arrival = Path(step.WalkIn, origin, origin, orientation, new Color(0.3f, 0.85f, 1f), size);
                Path(step.WalkOut, arrival, origin, orientation, new Color(1f, 0.65f, 0.2f), size);
            }
        }

        /// <summary>Connects configured keyframes and labels their relative travel times.</summary>
        /// <param name="animation">Arrival or departure path.</param>
        /// <param name="start">Initial world position, including the preceding arrival path.</param>
        /// <param name="origin">World spawn origin.</param>
        /// <param name="orientation">World spawn orientation.</param>
        /// <param name="color">Path colour identifying its phase.</param>
        /// <param name="size">Screen-scaled marker size.</param>
        /// <returns>The final world position, or the unchanged start when disabled.</returns>
        private static Vector3 Path(SpawnFlowAnimation animation, Vector3 start, Vector3 origin, Quaternion orientation, Color color, float size)
        {
            // Departure starts from the live pose at runtime; this guide uses the preceding arrival endpoint.
            if (!animation.Enabled)
                return start;
            using Handles.DrawingScope scope = new Handles.DrawingScope(color);
            bool world = animation.Space == SpawnFlowSpace.World;
            Vector3 from = world ? start : Quaternion.Inverse(orientation) * (start - origin);
            Vector3 entryVelocity = Vector3.zero;
            Vector3 previous = start;
            for (int index = 0; index < animation.Keyframes.Length; index++)
            {
                SpawnFlowKeyframe frame = animation.Keyframes[index];
                Vector3 exitVelocity = SpawnFlowPathSampling.ExitVelocity(animation, index, from);
                // Sample the same curve as runtime so continuous corners remain visible before Play.
                for (int sample = 1; sample <= 16; sample++)
                {
                    Vector3 position = SpawnFlowPathSampling.Position(animation.Interpolation, from, frame.Pose.Position,
                        entryVelocity, exitVelocity, frame.Duration, sample / 16f);
                    Vector3 worldPosition = world ? position : origin + orientation * position;
                    Handles.DrawLine(previous, worldPosition);
                    previous = worldPosition;
                }
                Vector3 point = previous;
                Handles.SphereHandleCap(0, point, Quaternion.identity, size, EventType.Repaint);
                Quaternion rotation = (world ? Quaternion.identity : orientation) * Quaternion.Euler(frame.Pose.Rotation);
                Handles.ArrowHandleCap(0, point, rotation, size * 3f, EventType.Repaint);
                Handles.Label(point + Vector3.up * size, (index + 1) + " · " + frame.Duration.ToString("0.##") + " s");
                previous = point;
                from = frame.Pose.Position;
                entryVelocity = exitVelocity;
            }
            return previous;
        }

        #endregion
        #endregion
    }
}
