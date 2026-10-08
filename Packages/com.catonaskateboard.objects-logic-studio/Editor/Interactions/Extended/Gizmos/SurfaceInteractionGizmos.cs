using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows compact guides for physical surface effects without filling the scene view.</summary>
    internal static class SurfaceInteractionGizmos
    {
        #region Methods
        #region Guides
        /// <summary>Marks the nozzle and emission cone in the same coordinates used at runtime.</summary>
        /// <param name="spray">Selected emitter.</param>
        /// <param name="type">Current gizmo drawing context.</param>
        [DrawGizmo(GizmoType.Selected)]
        private static void Spray(ObjectSpraySauce spray, GizmoType type)
        {
            if (!spray.DrawGizmos || spray.Settings?.Nozzle == null || !spray.Settings.Nozzle.IsValid())
                return;
            Vector3 point = spray.transform.TransformPoint(spray.Settings.Nozzle.Position);
            Quaternion rotation = spray.transform.rotation * Quaternion.Euler(spray.Settings.Nozzle.Rotation);
            using Handles.DrawingScope scope = new Handles.DrawingScope(new Color(0.25f, 0.9f, 0.75f));
            float length = HandleUtility.GetHandleSize(point) * 0.4f;
            Handles.ArrowHandleCap(0, point, rotation, length, EventType.Repaint);
            if (float.IsFinite(spray.Settings.Spread) && spray.Settings.Spread >= 0f && spray.Settings.Spread < 90f)
                Handles.DrawWireDisc(point + rotation * Vector3.forward * length, rotation * Vector3.forward,
                    Mathf.Tan(spray.Settings.Spread * Mathf.Deg2Rad) * length);
        }
        /// <summary>Shows the visual deformation pivot independently of the physics pivot.</summary>
        /// <param name="elastic">Selected response component.</param>
        /// <param name="type">Current gizmo drawing context.</param>
        [DrawGizmo(GizmoType.Selected)]
        private static void Elastic(ObjectElasticDeformation elastic, GizmoType type)
        {
            if (!elastic.DrawGizmos || !elastic.Settings.TryValidate(out _))
                return;
            Vector3 point = elastic.transform.TransformPoint(elastic.Settings.Pivot);
            using Handles.DrawingScope scope = new Handles.DrawingScope(new Color(0.95f, 0.65f, 0.25f));
            Handles.DrawWireDisc(point, elastic.transform.up, HandleUtility.GetHandleSize(point) * 0.04f);
        }
        #endregion
        #endregion
    }
}
