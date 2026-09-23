using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws compact selected-object grab geometry and an ideal launch preview without runtime debug objects.</summary>
    internal static class SingleInteractionGizmos
    {
        #region Methods

        #region Drawing

        /// <summary>Shows grab range and an aim-relative ballistic guide when a gameplay camera is available.</summary>
        /// <param name="grab">Selected Grab component.</param>
        /// <param name="type">Selection flags supplied by Unity.</param>
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void Draw(ObjectGrab grab, GizmoType type)
        {
            // Invalid authored values are left untouched and never reach Handles calculations.
            if (!grab.DrawGizmos || grab.Settings == null || !grab.Settings.TryValidate(out _))
                return;
            Vector3 anchor = grab.WorldTarget;
            float size = HandleUtility.GetHandleSize(anchor) * 0.05f;
            using (new Handles.DrawingScope(grab.IsHeld ? new Color(0.3f, 1f, 0.55f) : new Color(1f, 0.7f, 0.2f)))
            {
                Handles.DrawWireDisc(anchor, Vector3.up, grab.Settings.Distance);
                Handles.DrawWireDisc(anchor, Vector3.right, grab.Settings.Distance);
                Handles.SphereHandleCap(0, anchor, Quaternion.identity, size, EventType.Repaint);
                Handles.Label(anchor + Vector3.up * size, "Grab · " + grab.Settings.Distance.ToString("0.##") + " m");
            }
            if (grab.IsHeld)
                using (new Handles.DrawingScope(new Color(0.3f, 1f, 0.55f)))
                {
                    // The gap between requested and resolved pose makes blocking contacts easy to inspect.
                    Handles.DrawDottedLine(grab.transform.position, grab.CarryTarget, 4f);
                    Handles.SphereHandleCap(0, grab.CarryTarget, Quaternion.identity, size, EventType.Repaint);
                    Handles.Label(grab.CarryTarget, "Carry target");
                }
            ObjectThrow launch = grab.GetComponent<ObjectThrow>();
            Camera camera = Camera.main;
            if (launch == null || !launch.enabled || camera == null || launch.Trajectory == null
                || launch.PhysicsSettings == null || !launch.Trajectory.TryValidate(out _) || !launch.PhysicsSettings.TryValidate(out _))
                return;
            Rigidbody body = grab.GetComponent<Rigidbody>();
            if (body == null)
                return;
            float mass = launch.PhysicsSettings.OverrideBody ? launch.PhysicsSettings.Mass : body.mass;
            Vector3 velocity = launch.Trajectory.Direction(camera.transform.rotation) * launch.Trajectory.Strength
                / (launch.Trajectory.Mode == ThrowStrengthMode.Impulse ? mass : 1f);
            Vector3 gravity = (launch.PhysicsSettings.OverrideBody ? launch.PhysicsSettings.Gravity : body.useGravity)
                ? Physics.gravity : Vector3.zero;
            Vector3 previous = grab.transform.position;
            using (new Handles.DrawingScope(new Color(0.3f, 0.85f, 1f)))
            {
                // This deliberately ideal guide excludes drag, contacts and frozen axes.
                for (int step = 1; step <= 20; step++)
                {
                    float time = step * 0.075f;
                    Vector3 point = grab.transform.position + velocity * time + gravity * (0.5f * time * time);
                    Handles.DrawLine(previous, point);
                    previous = point;
                }
                Handles.Label(previous, "Throw · ideal arc from MainCamera aim");
            }
        }

        #endregion

        #endregion
    }
}
