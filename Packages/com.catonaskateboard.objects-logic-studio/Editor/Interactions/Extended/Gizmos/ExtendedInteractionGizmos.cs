using CatOnASkateboard.PlayerStudio;
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
            if ((dialogue.Settings.RequireSightToStart || dialogue.Settings.RequireSightToContinue || dialogue.Settings.HideWhenSightLost)
                && dialogue.Settings.TryValidate(out _))
            {
                // Mark the exact anchor used by the visibility check without drawing across the entire scene.
                Vector3 point = dialogue.transform.TransformPoint(dialogue.Settings.SightOffset);
                Handles.color = new Color(0.4f, 1f, 0.6f, 0.8f);
                Handles.DrawDottedLine(dialogue.transform.position, point, 4f);
                Handles.SphereHandleCap(0, point, Quaternion.identity, HandleUtility.GetHandleSize(point) * 0.06f, EventType.Repaint);
            }
            Handles.color = previous;
        }

        /// <summary>Outlines owned collider bounds and their configured contact allowance.</summary>
        /// <param name="contact">Selected contact interaction.</param>
        /// <param name="type">Unity's current gizmo drawing context.</param>
        [DrawGizmo(GizmoType.Selected)]
        private static void DrawContact(ObjectContactModifier contact, GizmoType type)
        {
            // Drawing uses current collider bounds, so replacing a collider in the prefab stays visible.
            if (!contact.DrawGizmos || contact.Settings == null)
                return;
            ObjectItem item = contact.GetComponent<ObjectItem>();
            float tolerance = 0f;
            // One outer envelope keeps overlapping modification tolerances legible.
            if (contact.Settings.Modifications != null)
                foreach (ContactModificationDefinition definition in contact.Settings.Modifications)
                    if (definition != null && definition.Settings != null && float.IsFinite(definition.Settings.ContactTolerance))
                        tolerance = Mathf.Max(tolerance, definition.Settings.ContactTolerance);
            Color previous = Gizmos.color;
            Gizmos.color = contact.IsModifying ? new Color(0.4f, 1f, 0.4f, 0.85f) : new Color(0.3f, 0.85f, 1f, 0.65f);
            foreach (Collider collider in contact.GetComponentsInChildren<Collider>())
                if (collider.enabled && collider.GetComponentInParent<ObjectItem>() == item)
                    Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size + Vector3.one * (2f * tolerance));
            Gizmos.color = previous;
            if (contact.Settings.Modifications != null)
                foreach (ContactModificationDefinition definition in contact.Settings.Modifications)
                    if (definition != null && definition.Settings?.Preparation != null && definition.Settings.Preparation.TryValidate(out _))
                        Preparation(contact.transform, definition.Settings.Preparation, definition.Settings.Name);
        }

        /// <summary>Projects snap and animation endpoints in the same reference spaces used by contact execution.</summary>
        /// <param name="owner">Modifying object's pivot.</param>
        /// <param name="settings">Validated preparation coordinates.</param>
        /// <param name="name">Modification name distinguishing overlapping guides.</param>
        private static void Preparation(Transform owner, ContactPreparationSettings settings, string name)
        {
            // Mark only authored stages and reuse the compact endpoint style from Trigger Animation.
            if (settings.Snap)
                using (new Handles.DrawingScope(new Color(0.35f, 1f, 0.65f, 0.85f)))
                {
                    Vector3 snap = CommandInteractionGizmos.Endpoint(settings.SnapSpace == ContactPoseSpace.Owner ? owner : null,
                        new PlayerToolPose { Position = settings.Position, Rotation = settings.Rotation, Scale = Vector3.one }, name + " / Snap");
                    Handles.DrawDottedLine(owner.position, snap, 4f);
                }
            if (!settings.Animate || Application.isPlaying && !settings.AnimateOther && string.IsNullOrEmpty(settings.Path))
                return;
            using Handles.DrawingScope scope = new Handles.DrawingScope(new Color(0.45f, 0.85f, 1f));
            Transform frame = settings.AnimationSpace == ContactPoseSpace.Owner ? owner : null;
            Vector3 first = CommandInteractionGizmos.Endpoint(frame, settings.StateA, name + " / A");
            Vector3 second = CommandInteractionGizmos.Endpoint(frame, settings.StateB, name + " / B");
            Handles.DrawDottedLine(first, second, 4f);
        }

        /// <summary>Shows the authored force direction without drawing lines to every affected scene body.</summary>
        /// <param name="generator">Selected gravity generator.</param>
        /// <param name="type">Unity's current gizmo context.</param>
        [DrawGizmo(GizmoType.Selected)]
        private static void DrawGravity(ObjectGravityGenerator generator, GizmoType type)
        {
            if (!generator.DrawGizmos || generator.Settings is not { Push: true, RandomDirection: false } settings
                || !float.IsFinite(settings.Direction.x) || !float.IsFinite(settings.Direction.y)
                || !float.IsFinite(settings.Direction.z) || settings.Direction.sqrMagnitude < 0.000001f)
                return;
            Vector3 direction = settings.LocalDirection ? generator.transform.TransformDirection(settings.Direction) : settings.Direction;
            using (new Handles.DrawingScope(new Color(0.85f, 0.65f, 1f, 0.85f)))
                Handles.ArrowHandleCap(0, generator.transform.position, Quaternion.LookRotation(direction),
                    HandleUtility.GetHandleSize(generator.transform.position) * 0.7f, EventType.Repaint);
        }

        /// <summary>Shows the same reach and anchor used by confirmation and gravity restoration input.</summary>
        /// <param name="interaction">Selected process with optional input.</param>
        /// <param name="type">Unity's current gizmo context.</param>
        [DrawGizmo(GizmoType.Selected)]
        private static void DrawRequest(ObjectRequestedInteraction interaction, GizmoType type)
        {
            if (!interaction.DrawGizmos || !interaction.UsesInput || interaction.Target is not { DrawGizmos: true } target
                || !target.TryValidate(out _))
                return;
            Vector3 anchor = interaction.transform.TransformPoint(target.Offset);
            using (new Handles.DrawingScope(new Color(0.3f, 0.8f, 1f, 0.7f)))
            {
                Handles.DrawWireDisc(anchor, Vector3.up, target.Distance);
                Handles.DrawDottedLine(interaction.transform.position, anchor, 4f);
                Handles.SphereHandleCap(0, anchor, Quaternion.identity, HandleUtility.GetHandleSize(anchor) * 0.06f, EventType.Repaint);
            }
        }

        #endregion

        #endregion
    }
}
