using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows the selected dispenser's scoop route for the currently held container.</summary>
    internal static class ScoopGizmos
    {
        #region Methods

        #region Drawing

        /// <summary>Draws reached poses, orientation and the exact fill marker without changing scene transforms.</summary>
        /// <param name="source">Selected supply pivot.</param>
        /// <param name="type">Unity's selected gizmo context.</param>
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void Draw(ObjectDispenser source, GizmoType type)
        {
            ObjectGrab held = Object.FindAnyObjectByType<HoverObserver>()?.HeldObject;
            ObjectContainer receiver = held?.CarryContainers != null ? System.Array.Find(held.CarryContainers,
                candidate => candidate != null && candidate.isActiveAndEnabled && candidate.Settings.ScoopFromDispenser
                    && source.MatchesScoop(candidate)) : null;
            if (receiver == null || !receiver.Settings.ScoopAnimation.Enabled || !receiver.Settings.ScoopAnimation.TryValidate(out _))
                return;
            ScoopAnimationSettings settings = receiver.Settings.ScoopAnimation;
            Pose initial = new Pose(receiver.transform.position, receiver.transform.rotation);
            ScoopPath path = new ScoopPath(settings, initial, new Pose(source.transform.position, source.transform.rotation),
                held.CarryPose, Vector3.one);
            Vector3 previous = initial.position;
            // Colors distinguish the fill boundary while labels remain sufficient without color.
            using (new Handles.DrawingScope(new Color(0.35f, 0.75f, 0.9f)))
                for (int index = 0; index < path.Targets.Length; index++)
                {
                    Pose target = path.Targets[index];
                    float size = HandleUtility.GetHandleSize(target.position) * 0.055f;
                    Handles.DrawDottedLine(previous, target.position, 5f);
                    Handles.color = index == path.CommitStep ? new Color(0.95f, 0.72f, 0.25f) : new Color(0.35f, 0.75f, 0.9f);
                    Handles.SphereHandleCap(0, target.position, Quaternion.identity, size, EventType.Repaint);
                    Handles.ArrowHandleCap(0, target.position, target.rotation, size * 3f, EventType.Repaint);
                    Handles.Label(target.position + Vector3.up * size, (index < path.Frames.Length ? path.Frames[index].Name : "Return")
                        + (index == path.CommitStep ? " · Fill" : string.Empty));
                    previous = target.position;
                }
        }

        #endregion

        #endregion
    }
}
