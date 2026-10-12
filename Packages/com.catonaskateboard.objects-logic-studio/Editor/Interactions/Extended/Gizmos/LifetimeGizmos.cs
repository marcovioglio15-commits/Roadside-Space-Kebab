using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Outlines existing death-volume bounds without drawing persistent scene clutter.</summary>
    internal static class LifetimeGizmos
    {
        #region Methods
        #region Selection

        /// <summary>Shows the broad-phase envelope of each owned collider when the deathbox is selected.</summary>
        /// <param name="feature">Selected death volume.</param>
        /// <param name="type">Unity's gizmo context.</param>
        [DrawGizmo(GizmoType.Selected)]
        private static void Deathbox(ObjectDeathbox feature, GizmoType type)
        {
            if (!feature.DrawGizmos)
                return;
            using Handles.DrawingScope scope = new Handles.DrawingScope(new Color(0.95f, 0.3f, 0.35f));
            // Runtime checks the precise volume after this inexpensive bounds query.
            foreach (Collider shape in feature.GetComponentsInChildren<Collider>())
                if (shape.enabled && shape.GetComponentInParent<ObjectItem>() == feature.GetComponent<ObjectItem>())
                    Handles.DrawWireCube(shape.bounds.center, shape.bounds.size);
        }

        #endregion
        #endregion
    }
}
