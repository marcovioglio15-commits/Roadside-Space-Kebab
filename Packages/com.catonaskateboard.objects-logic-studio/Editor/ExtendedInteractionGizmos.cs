using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws compact selected-object guides for dialogue range and contact tolerance.</summary>
    internal static class ExtendedInteractionGizmos
    {
        #region Methods

        #region Guides

        /// <summary>Shows activation and interruption distances without covering the object with filled volumes.</summary>
        /// <param name="dialogue">Selected dialogue component.</param>
        /// <param name="type">Unity's current gizmo drawing context.</param>
        [DrawGizmo(GizmoType.Selected)]
        private static void DrawDialogue(ObjectDialogue dialogue, GizmoType type)
        {
            // Rings remain in world units even when the prefab has a scaled root.
            if (!dialogue.DrawGizmos || dialogue.Settings == null)
                return;
            Color previous = Handles.color;
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            if (dialogue.Settings.Distance > 0f)
                Handles.DrawWireDisc(dialogue.transform.position, Vector3.up, dialogue.Settings.Distance);
            Handles.color = new Color(1f, 0.65f, 0.25f, 0.65f);
            if (dialogue.Settings.ExitDistance > 0f)
                Handles.DrawWireDisc(dialogue.transform.position, Vector3.up, dialogue.Settings.ExitDistance);
            Handles.color = previous;
        }

        /// <summary>Outlines owned collider bounds and their configured contact allowance.</summary>
        /// <param name="contact">Selected passive interaction.</param>
        /// <param name="type">Unity's current gizmo drawing context.</param>
        [DrawGizmo(GizmoType.Selected)]
        private static void DrawContact(ObjectContactModifier contact, GizmoType type)
        {
            // Drawing uses current collider bounds, so replacing a collider in the prefab stays visible.
            if (!contact.DrawGizmos || contact.Settings == null)
                return;
            ObjectItem item = contact.GetComponent<ObjectItem>();
            Color previous = Gizmos.color;
            Gizmos.color = contact.IsModifying ? new Color(0.4f, 1f, 0.4f, 0.85f) : new Color(0.3f, 0.85f, 1f, 0.65f);
            foreach (Collider collider in contact.GetComponentsInChildren<Collider>())
                if (collider.enabled && collider.GetComponentInParent<ObjectItem>() == item)
                    Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size + Vector3.one * (2f * Mathf.Max(0f, contact.Settings.ContactTolerance)));
            Gizmos.color = previous;
        }

        #endregion

        #endregion
    }
}
