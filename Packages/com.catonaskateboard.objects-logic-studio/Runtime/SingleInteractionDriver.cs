using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Arbitrates one held object and routes performed actions from the Observer player's private input asset.</summary>
    internal sealed class SingleInteractionDriver
    {
        #region State

        private readonly InteractionInputRouter input = new InteractionInputRouter();
        private readonly Dictionary<ObjectSingleInteraction, InteractionButton> bindings = new Dictionary<ObjectSingleInteraction, InteractionButton>();
        private readonly HoverPhysics physics = new HoverPhysics();
        private Transform player;
        private ObjectGrab held;
        private int revision = -1;
        private int inputRevision = -1;
        private bool missingInputReported;

        #endregion

        #region Properties

        /// <summary>The occupied carry slot, cleared immediately when its object stops being held.</summary>
        internal ObjectGrab Held => held != null && held.IsHeld ? held : null;

        #endregion

        #region Methods

        #region Ownership

        /// <summary>Releases all subscriptions and temporary carry ownership when observation stops.</summary>
        internal void Reset()
        {
            // Context loss cannot strand a body in its held physics state.
            if (held != null)
                held.Cancel();
            held = null;
            ClearBindings();
            player = null;
            input.Reset();
            inputRevision = -1;
            missingInputReported = false;
        }

        /// <summary>Detaches input subscriptions before rebuilding the registered feature associations.</summary>
        private void ClearBindings()
        {
            // Disposing signals never changes PlayerInput's maps or device pairing.
            input.ClearBindings();
            bindings.Clear();
            revision = -1;
        }

        /// <summary>Resolves PlayerInput only when player ownership changes or a missing component is retried.</summary>
        /// <param name="observer">Scene context selected by the existing Observer.</param>
        private void Bind(HoverObserver observer)
        {
            // Player spawn and respawn use the same tagged-root selection as hover detection.
            if (player != observer.Player)
            {
                Reset();
                player = observer.Player;
            }
            input.Refresh(player);
            if (revision == SingleInteractionRegistry.Revision && inputRevision == input.Revision)
                return;
            ClearBindings();
            inputRevision = input.Revision;
            revision = SingleInteractionRegistry.Revision;
            if (input.Asset == null)
            {
                if (!missingInputReported && SingleInteractionRegistry.Items.Count > 0)
                {
                    Debug.LogWarning("Single interactions need an active PlayerInput with an Input Actions asset under the Observer's player root.", observer);
                    missingInputReported = true;
                }
                return;
            }
            missingInputReported = false;

            // Bind by stable action ID against the private runtime asset, never the project asset's action instance.
            foreach (ObjectSingleInteraction feature in SingleInteractionRegistry.Items)
            {
                if (feature == null || !feature.isActiveAndEnabled)
                    continue;
                if (!feature.TryValidate(out string warning))
                {
                    Debug.LogWarning(feature.Kind + ": " + warning, feature);
                    continue;
                }
                InteractionButton button = input.Bind(feature.Action);
                if (button == null)
                {
                    Debug.LogWarning(feature.Kind + " action is not a Button in the Observer player's Input Actions asset.", feature);
                    continue;
                }
                bindings.Add(feature, button);
            }
        }

        #endregion

        #region Dispatch

        /// <summary>Processes one pickup or release after the gameplay camera has finished moving.</summary>
        /// <param name="observer">Shared camera/player context.</param>
        /// <param name="consumed">Action already used by dialogue in this frame, if any.</param>
        internal void Tick(HoverObserver observer, InputActionReference consumed = null)
        {
            // Rebinding is triggered by ownership and registry changes, not by ordinary repaints or frames.
            Bind(observer);
            input.Consume(AssemblyInteractionRegistry.Consumed);
            input.Consume(consumed);
            foreach (InputActionReference action in InteractionUnlockRegistry.Consumed)
                input.Consume(action);
            if (held != null && !held.IsHeld)
                held = null;
            if (!input.Usable || Time.timeScale <= 0f)
            {
                input.ClearSignals();
                return;
            }
            if (held != null)
                Release(observer);
            else
                Grab(observer);
            input.ClearSignals();
        }

        /// <summary>Chooses the highest-priority eligible release without grabbing again in the same event.</summary>
        /// <param name="observer">Camera defining the release aim.</param>
        private void Release(HoverObserver observer)
        {
            // Throw wins simultaneous Drop/Throw requests; using one action for Grab and Drop provides a toggle.
            ObjectRelease selected = null;
            foreach (KeyValuePair<ObjectSingleInteraction, InteractionButton> pair in bindings)
                if (pair.Value.Pending && pair.Key is ObjectRelease release && release.Available(InteractionChannels.Release)
                    && release.Grab == held && (selected == null || release.Kind == SingleInteractionKind.Throw))
                    selected = release;
            if (selected == null)
                return;
            selected.Signal(InteractionMoment.Started);
            selected.Execute(observer.View.transform);
            selected.Signal(InteractionMoment.Completed);
            held = null;
        }

        /// <summary>Selects the nearest eligible object for the performed Grab action.</summary>
        /// <param name="observer">Camera, player and visibility context.</param>
        private void Grab(HoverObserver observer)
        {
            // Expensive visibility queries run only on a performed Grab, not continuously while idle.
            ObjectGrab selected = null;
            float distance = float.PositiveInfinity;
            foreach (KeyValuePair<ObjectSingleInteraction, InteractionButton> pair in bindings)
                if (pair.Value.Pending && pair.Key is ObjectGrab candidate && candidate.Available(InteractionChannels.Grab)
                    && !candidate.IsHeld && !candidate.transform.IsChildOf(observer.Player)
                    && Eligible(observer, candidate, out float score)
                    && (score < distance || score == distance && selected != null
                        && EntityId.ToULong(candidate.GetEntityId()) < EntityId.ToULong(selected.GetEntityId())))
                {
                    selected = candidate;
                    distance = score;
                }
            if (selected == null)
                return;
            if (selected.Begin(observer))
                held = selected;
        }

        #endregion

        #region Targeting

        /// <summary>Applies independent Grab targeting even when the object has no Hover feature.</summary>
        /// <param name="observer">Observer with valid camera and player.</param>
        /// <param name="target">Grab candidate bound to a performed action.</param>
        /// <param name="score">Receives distance from the player for deterministic nearest-target selection.</param>
        /// <returns>True when the candidate is in range, visible and unobstructed.</returns>
        private bool Eligible(HoverObserver observer, ObjectGrab target, out float score)
        {
            // Range is measured from the player; a third-person camera does not extend reach.
            Vector3 point = target.WorldTarget;
            score = (point - observer.Player.position).sqrMagnitude;
            GrabSettings settings = target.Settings;
            Camera view = observer.View;
            if (score > settings.Distance * settings.Distance || (view.cullingMask & (1 << target.gameObject.layer)) == 0)
                return false;
            Vector3 projected = view.WorldToScreenPoint(point);
            Rect viewport = view.pixelRect;
            if (projected.z < view.nearClipPlane || projected.z > view.farClipPlane || !viewport.Contains(projected))
                return false;
            switch (settings.TargetMode)
            {
                case HoverTargetMode.ViewCenter:
                    float radius = viewport.height * settings.CenterRadius;
                    if (((Vector2)projected - viewport.center).sqrMagnitude > radius * radius)
                        return false;
                    break;
                case HoverTargetMode.Cursor:
                    if (Mouse.current == null || Cursor.lockState == CursorLockMode.Locked || view.targetDisplay != 0
                        || !viewport.Contains(Mouse.current.position.ReadValue())
                        || !HoverPhysics.TryCursorHit(view.ScreenPointToRay(Mouse.current.position.ReadValue()), target.Colliders,
                            view.cullingMask, Vector3.Distance(view.transform.position, observer.Player.position) + settings.Distance, out point))
                        return false;
                    break;
                default:
                    return false;
            }
            return physics.HasSight(observer, target.transform, point, settings.ObstacleMask);
        }

        #endregion

        #endregion
    }
}
