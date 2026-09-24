using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows compact assembly reach, output orientation and named ingredient slots on selection.</summary>
    internal static class AssemblyGizmos
    {
        #region Methods

        #region Guides

        /// <summary>Draws the table output and player reach in world units.</summary>
        /// <param name="station">Selected assembly table.</param>
        /// <param name="type">Unity selection drawing context.</param>
        [DrawGizmo(GizmoType.Selected)]
        private static void DrawStation(ObjectAssemblyStation station, GizmoType type)
        {
            // Preview staging stays invisible; these guides describe the actual world-space output instead.
            if (!station.DrawGizmos || station.Settings == null)
                return;
            using (new Handles.DrawingScope(new Color(0.2f, 0.8f, 1f, 0.75f)))
            {
                if (station.Settings.Distance > 0f && !float.IsInfinity(station.Settings.Distance))
                    Handles.DrawWireDisc(station.OutputPosition, Vector3.up, station.Settings.Distance);
                float size = HandleUtility.GetHandleSize(station.OutputPosition) * 0.2f;
                Handles.ArrowHandleCap(0, station.OutputPosition, station.transform.rotation * Quaternion.Euler(station.Settings.OutputRotation), size, EventType.Repaint);
                Handles.Label(station.OutputPosition, "Assembly output");
            }
        }

        /// <summary>Draws validated local magnet positions with restrained selected-object markers.</summary>
        /// <param name="product">Selected recipe owner.</param>
        /// <param name="type">Unity selection drawing context.</param>
        [DrawGizmo(GizmoType.Selected)]
        private static void DrawProduct(ObjectAssemblyProduct product, GizmoType type)
        {
            // Invalid edited values remain in the inspector without entering a gizmo transform matrix.
            if (!product.DrawGizmos || product.Settings?.Magnets == null)
                return;
            using (new Handles.DrawingScope(new Color(1f, 0.72f, 0.18f, 0.9f), product.transform.localToWorldMatrix))
                foreach (AssemblyMagnet magnet in product.Settings.Magnets)
                    if (AssemblyValidation.ValidMagnet(magnet))
                    {
                        Handles.DrawWireDisc(magnet.Position, Quaternion.Euler(magnet.Rotation) * Vector3.up, 0.04f);
                        Handles.Label(magnet.Position, magnet.Name + (magnet.AnyIngredient ? " · Any recipe ingredient" : " · " + magnet.Tag));
                    }
        }

        #endregion

        #endregion
    }
}
