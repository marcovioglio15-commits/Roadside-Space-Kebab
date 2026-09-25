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
        private readonly Dictionary<ObjectInteraction, InteractionButton> bindings = new Dictionary<ObjectInteraction, InteractionButton>();
        private readonly InteractionTargeting targeting = new InteractionTargeting();
        private Transform player;
        private ObjectGrab held;
        private int revision = -1;
        private int inputRevision = -1;
        private int sliceRevision = -1;
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
            revision = sliceRevision = -1;
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
            if (revision == SingleInteractionRegistry.Revision && sliceRevision == SliceRegistry.Revision && inputRevision == input.Revision)
                return;
            ClearBindings();
            inputRevision = input.Revision;
            revision = SingleInteractionRegistry.Revision;
            sliceRevision = SliceRegistry.Revision;
            if (input.Asset == null)
            {
                if (!missingInputReported && (SingleInteractionRegistry.Items.Count > 0 || SliceRegistry.Items.Count > 0))
                {
                    Debug.LogWarning("Input interactions need an active PlayerInput with an Input Actions asset under the Observer's player root.", observer);
                    missingInputReported = true;
                }
                return;
            }
            missingInputReported = false;

            // Bind by stable action ID against the private runtime asset, never the project asset's action instance.
            foreach (ObjectSingleInteraction feature in SingleInteractionRegistry.Items)
                BindFeature(feature, feature.Action);
            foreach (ObjectSlice feature in SliceRegistry.Items)
                BindFeature(feature, feature.Action);
        }

        /// <summary>Registers a valid single action or Slice sequence in the shared input router.</summary>
        /// <param name="feature">Enabled interaction receiving performed presses.</param>
        /// <param name="action">Authored Button reference resolved against the player's private asset.</param>
        private void BindFeature(ObjectInteraction feature, InputActionReference action)
        {
            // One router arbitrates Slice and Grab so a shared press cannot perform both.
            if (feature == null || !feature.isActiveAndEnabled)
                return;
            string warning = string.Empty;
            bool valid = feature switch
            {
                ObjectSingleInteraction single => single.TryValidate(out warning),
                ObjectSlice slice => slice.TryValidate(out warning),
                _ => false
            };
            if (!valid)
            {
                Debug.LogWarning(feature.InteractionName + ": " + warning, feature);
                return;
            }
            InteractionButton button = input.Bind(action);
            if (button != null)
                bindings.Add(feature, button);
            else
                Debug.LogWarning(feature.InteractionName + " action is not a Button in the Observer player's Input Actions asset.", feature);
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
            bool pending = false;
            foreach (InteractionButton button in bindings.Values)
                pending |= button.Pending;
            if (!pending)
                return;
            Physics.SyncTransforms();
            if (!Slice(observer))
                if (held != null)
                    Release(observer);
                else
                    Grab(observer);
            input.ClearSignals();
        }

        /// <summary>Advances the highest-priority visible Slice before considering pickup or release.</summary>
        /// <param name="observer">Player and camera supplying reach and visibility.</param>
        /// <returns>True when one step consumed this frame's performed input.</returns>
        private bool Slice(HoverObserver observer)
        {
            // Restrict geometry queries to performed presses on eligible unfinished sequences.
            ObjectSlice selected = null;
            float distance = float.PositiveInfinity;
            foreach (KeyValuePair<ObjectInteraction, InteractionButton> pair in bindings)
            {
                if (!pair.Value.Pending || pair.Key is not ObjectSlice candidate || !candidate.CanAdvance()
                    || candidate.transform.IsChildOf(observer.Player))
                    continue;
                TransferTargetSettings target = candidate.Settings.Target;
                if (targeting.Eligible(observer, candidate.transform, candidate.Colliders, candidate.transform.TransformPoint(target.Offset),
                    target.Distance, target.Mode, target.Mode == HoverTargetMode.Cursor ? float.PositiveInfinity : target.CenterRadius,
                    target.ObstacleMask, out float score)
                    && (selected == null || candidate.Settings.Priority > selected.Settings.Priority
                        || candidate.Settings.Priority == selected.Settings.Priority && Nearer(candidate, selected, score, distance)))
                {
                    selected = candidate;
                    distance = score;
                }
            }
            return selected != null && selected.Advance();
        }

        /// <summary>Chooses the highest-priority eligible release without grabbing again in the same event.</summary>
        /// <param name="observer">Camera defining the release aim.</param>
        private void Release(HoverObserver observer)
        {
            // A successful deposit consumes this event before a same-key Drop can release the item.
            ObjectContainer container = null;
            float distance = float.PositiveInfinity;
            foreach (KeyValuePair<ObjectInteraction, InteractionButton> pair in bindings)
                if (pair.Value.Pending && pair.Key is ObjectContainer candidate && candidate.CanStore(held)
                    && Eligible(observer, candidate, out float score) && Nearer(candidate, container, score, distance))
                {
                    container = candidate;
                    distance = score;
                }
            if (container != null && container.Store(held))
            {
                held = null;
                return;
            }
            // Throw wins simultaneous Drop/Throw requests; using one action for Grab and Drop provides a toggle.
            ObjectRelease selected = null;
            foreach (KeyValuePair<ObjectInteraction, InteractionButton> pair in bindings)
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
            // Only one eligible feature can claim the empty carry slot for this input event.
            ObjectInteraction selected = null;
            float distance = float.PositiveInfinity;
            foreach (KeyValuePair<ObjectInteraction, InteractionButton> pair in bindings)
            {
                if (!pair.Value.Pending || pair.Key == null || pair.Key.transform.IsChildOf(observer.Player))
                    continue;
                float score = float.PositiveInfinity;
                bool eligible = pair.Key switch
                {
                    ObjectGrab grab => grab.Available(InteractionChannels.Grab) && !grab.IsHeld && Eligible(observer, grab, out score),
                    ObjectDispenser dispenser => dispenser.CanTake() && Eligible(observer, dispenser, out score),
                    _ => false
                };
                if (eligible && Nearer(pair.Key, selected, score, distance))
                {
                    selected = pair.Key;
                    distance = score;
                }
            }
            switch (selected)
            {
                case ObjectGrab grab when grab.Begin(observer):
                    held = grab;
                    break;
                case ObjectDispenser dispenser when dispenser.TryTake(observer, out ObjectGrab supplied):
                    held = supplied;
                    break;
            }
        }

        /// <summary>Breaks equal-distance ties consistently without depending on registry enumeration order.</summary>
        /// <param name="candidate">Feature being considered.</param>
        /// <param name="selected">Previously selected feature, if any.</param>
        /// <param name="score">Candidate squared distance.</param>
        /// <param name="distance">Current best squared distance.</param>
        /// <returns>True when the candidate should replace the current selection.</returns>
        private static bool Nearer(ObjectInteraction candidate, ObjectInteraction selected, float score, float distance)
        {
            // Stable entity identity resolves overlapping eligible objects deterministically.
            return score < distance || score == distance && selected != null
                && EntityId.ToULong(candidate.GetEntityId()) < EntityId.ToULong(selected.GetEntityId());
        }

        #endregion

        #region Targeting

        /// <summary>Applies shared visible-surface targeting to a deposit or withdrawal.</summary>
        /// <param name="observer">Active camera and player.</param>
        /// <param name="target">Inventory interaction responding to an input event.</param>
        /// <param name="score">Receives squared player distance.</param>
        /// <returns>True when the target is reachable and unobstructed.</returns>
        private bool Eligible(HoverObserver observer, ObjectTransferInteraction target, out float score)
        {
            // Transfer interactions have their own range and aim settings, independent of Grab.
            TransferTargetSettings settings = target.Target;
            return targeting.Eligible(observer, target.transform, target.Colliders, target.transform.TransformPoint(settings.Offset),
                settings.Distance, settings.Mode, settings.Mode == HoverTargetMode.Cursor ? float.PositiveInfinity : settings.CenterRadius,
                settings.ObstacleMask, out score);
        }

        /// <summary>Applies independent Grab targeting even when the object has no Hover feature.</summary>
        /// <param name="observer">Observer with valid camera and player.</param>
        /// <param name="target">Grab candidate bound to a performed action.</param>
        /// <param name="score">Receives distance from the player for deterministic nearest-target selection.</param>
        /// <returns>True when the candidate is in range, visible and unobstructed.</returns>
        private bool Eligible(HoverObserver observer, ObjectGrab target, out float score)
        {
            // Test actual compound geometry as well as the authored pivot.
            GrabSettings settings = target.Settings;
            return targeting.Eligible(observer, target.transform, target.Colliders, target.WorldTarget, settings.Distance,
                settings.TargetMode, settings.TargetMode == HoverTargetMode.Cursor ? float.PositiveInfinity : settings.CenterRadius, settings.ObstacleMask, out score);
        }

        #endregion

        #endregion
    }
}
