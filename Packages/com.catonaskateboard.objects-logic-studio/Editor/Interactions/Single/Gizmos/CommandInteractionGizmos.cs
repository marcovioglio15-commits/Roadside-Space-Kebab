using CatOnASkateboard.PlayerStudio;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows selected-object transform endpoints and impulse directions without creating preview objects.</summary>
    internal static class CommandInteractionGizmos
    {
        #region Methods

        #region Animation

        /// <summary>Draws both local endpoints in their selected target's parent frame.</summary>
        /// <param name="feature">Selected transform animation.</param>
        /// <param name="type">Unity selection context.</param>
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void Animation(ObjectTriggerAnimation feature, GizmoType type)
        {
            // Invalid settings never reach Handles geometry or silently alter the authored target.
            if (feature.Settings == null || !feature.Settings.TryValidate(out _) || !feature.Target.DrawGizmos)
                return;
            Transform target = PlayerHierarchy.Resolve(feature.transform, feature.Settings.Path);
            if (target == null)
                return;
            using Handles.DrawingScope scope = new Handles.DrawingScope(new Color(0.45f, 0.85f, 1f));
            Vector3 first = Endpoint(target.parent, feature.Settings.StateA, "State A");
            Vector3 second = Endpoint(target.parent, feature.Settings.StateB, "State B");
            Handles.DrawDottedLine(first, second, 4f);
        }

        /// <summary>Projects one local endpoint without temporarily moving the real object.</summary>
        /// <param name="parent">Coordinate frame of the selected transform, or null for a scene root.</param>
        /// <param name="pose">Local endpoint.</param>
        /// <param name="label">Short endpoint identifier.</param>
        /// <returns>World position used for the connecting guide.</returns>
        internal static Vector3 Endpoint(Transform parent, PlayerToolPose pose, string label)
        {
            // Screen-relative marker sizes remain readable at different scene-view zoom levels.
            Vector3 position = parent != null ? parent.TransformPoint(pose.Position) : pose.Position;
            Quaternion rotation = (parent != null ? parent.rotation : Quaternion.identity) * Quaternion.Euler(pose.Rotation);
            float size = HandleUtility.GetHandleSize(position) * 0.12f;
            Handles.ArrowHandleCap(0, position, rotation, size, EventType.Repaint);
            Handles.Label(position + Vector3.up * size, label);
            return position;
        }

        #endregion

        #region Ejection

        /// <summary>Displays configured launch directions from the ejector without implying a mass-independent ballistic path.</summary>
        /// <param name="feature">Selected eject interaction.</param>
        /// <param name="type">Unity selection context.</param>
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void Eject(ObjectEject feature, GizmoType type)
        {
            // Direction guides remain compact even for large impulse magnitudes.
            if (feature.Settings == null || !feature.Settings.TryValidate(out _) || !feature.Target.DrawGizmos)
                return;
            Vector3 origin = feature.Target.SolidHitOnly ? feature.transform.position : feature.transform.TransformPoint(feature.Target.Offset);
            float size = HandleUtility.GetHandleSize(origin) * 0.7f;
            using Handles.DrawingScope scope = new Handles.DrawingScope(new Color(1f, 0.65f, 0.2f));
            for (int index = 0; index < (feature.Settings.SelfEject ? 1 : feature.Settings.Rules.Length); index++)
            {
                EjectRule rule = feature.Settings.SelfEject ? feature.Settings.SelfImpulse : feature.Settings.Rules[index];
                Vector3 impulse = rule.Space == Space.Self ? feature.transform.rotation * rule.Impulse : rule.Impulse;
                Vector3 end = origin + impulse.normalized * size;
                Handles.DrawLine(origin, end);
                Handles.ConeHandleCap(0, end, Quaternion.LookRotation(impulse), size * 0.12f, EventType.Repaint);
                Handles.Label(end, "Impulse " + (index + 1) + " · " + impulse.magnitude.ToString("0.##"));
            }
        }

        #endregion

        #endregion
    }
}
