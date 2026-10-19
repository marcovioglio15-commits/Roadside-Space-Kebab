using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows the collector geometry, offset destination, influence range and final launch direction.</summary>
    internal static class BlackHoleGizmos
    {
        #region Methods
        #region Drawing

        /// <summary>Draws compact selected-object guides without allocating preview objects.</summary>
        /// <param name="owner">Selected ejector configured as a black hole.</param>
        internal static void Draw(ObjectEject owner)
        {
            BlackHoleSettings settings = owner.Settings.BlackHole;
            Collider collider = settings.Resolve(owner.transform);
            if (collider == null)
                return;
            Vector3 point = settings.Destination(collider);
            float marker = HandleUtility.GetHandleSize(point) * 0.08f;
            if (settings.GuideEntrance)
                using (new Handles.DrawingScope(new Color(0.3f, 1f, 0.65f)))
                {
                    Vector3 entrance = point + collider.transform.TransformVector(settings.EntranceOffset);
                    Handles.DrawDottedLine(entrance, point, 3f);
                    Handles.DrawWireDisc(entrance, (point - entrance).normalized, settings.ArrivalRadius);
                    Handles.SphereHandleCap(0, entrance, Quaternion.identity, marker, EventType.Repaint);
                    Handles.Label(entrance + Vector3.up * marker, "Duct approach");
                }
            using (new Handles.DrawingScope(new Color(0.4f, 0.8f, 1f, 0.65f)))
            {
                Handles.DrawWireCube(collider.bounds.center, collider.bounds.size);
                Handles.DrawDottedLine(collider.bounds.center, point, 4f);
                Area(owner.transform, settings, point);
                Handles.SphereHandleCap(0, point, Quaternion.identity, marker, EventType.Repaint);
            }
            using (new Handles.DrawingScope(new Color(1f, 0.7f, 0.2f)))
            {
                Handles.DrawWireDisc(point, Vector3.up, settings.ArrivalRadius);
                Vector3 impulse = settings.ImpulseSpace == Space.Self ? owner.transform.rotation * settings.Impulse : settings.Impulse;
                Vector3 end = point + impulse.normalized * marker * 8f;
                Handles.DrawLine(point, end);
                Handles.ConeHandleCap(0, end, Quaternion.LookRotation(impulse), marker * 2f, EventType.Repaint);
                Handles.Label(end, "Arrival impulse · " + impulse.magnitude.ToString("0.##"));
            }
            Handles.Label(point + Vector3.up * marker, "Black Hole · " + collider.name);
        }

        /// <summary>Draws the actual admission region, keeping world dimensions independent of source scale.</summary>
        /// <param name="owner">Ejector defining the optional area's local pose.</param>
        /// <param name="settings">Selected attraction settings.</param>
        /// <param name="destination">Default range center when the custom area is disabled.</param>
        private static void Area(Transform owner, BlackHoleSettings settings, Vector3 destination)
        {
            GravityArea area = settings.Area;
            Vector3 center = area.Enabled ? owner.TransformPoint(area.Offset) : destination;
            if (area.Enabled && area.Shape == GravityAreaShape.Box)
                using (new Handles.DrawingScope(Matrix4x4.TRS(center, owner.rotation * Quaternion.Euler(area.Rotation), Vector3.one)))
                    Handles.DrawWireCube(Vector3.zero, area.Size);
            else
            {
                float radius = area.Enabled ? area.Radius : settings.Radius;
                Handles.DrawWireDisc(center, Vector3.up, radius);
                Handles.DrawWireDisc(center, Vector3.right, radius);
                Handles.DrawWireDisc(center, Vector3.forward, radius);
            }
            if (area.Enabled)
                Handles.Label(center, "Admission area · cap " + settings.Performance.MaximumParticipants);
        }

        #endregion
        #endregion
    }
}
