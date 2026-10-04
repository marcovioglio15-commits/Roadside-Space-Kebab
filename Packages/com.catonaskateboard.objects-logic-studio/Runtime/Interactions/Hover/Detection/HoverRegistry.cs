using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares active hover components with one observer without repeated scene discovery.</summary>
    internal static class HoverRegistry
    {
        #region State

        private static readonly List<ObjectHover> interactions = new List<ObjectHover>();
        private static readonly List<ObjectHover> frame = new List<ObjectHover>();

        #endregion

        #region Methods

        #region Registration

        /// <summary>Rebuilds registration once when entering Play, including disabled domain and scene reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            // With scene reload disabled, OnEnable is not a reliable reset boundary.
            interactions.Clear();
            frame.Clear();
            foreach (HoverObserver observer in Object.FindObjectsByType<HoverObserver>())
                observer.RefreshContext();
            foreach (ObjectHover interaction in Object.FindObjectsByType<ObjectHover>())
                if (interaction.isActiveAndEnabled)
                {
                    interaction.Refresh();
                    Register(interaction);
                }
        }

        /// <summary>Registers an enabled interaction at an activation boundary.</summary>
        /// <param name="interaction">Component becoming available.</param>
        internal static void Register(ObjectHover interaction)
        {
            // Bootstrapping and OnEnable may both reach the same component.
            if (!interactions.Contains(interaction))
                interactions.Add(interaction);
        }

        /// <summary>Removes a component before its UI or hierarchy is released.</summary>
        /// <param name="interaction">Component leaving the active set.</param>
        internal static void Unregister(ObjectHover interaction)
        {
            // Removing a missing entry is harmless during script reload and teardown.
            interactions.Remove(interaction);
        }

        #endregion

        #region Dispatch

        /// <summary>Reads visible hover ownership without searching an item's hierarchy during pickup selection.</summary>
        /// <param name="target">Interaction whose eligible pickup is being compared.</param>
        /// <returns>True when an available Hover on this item currently owns its label.</returns>
        internal static bool IsHovered(ObjectInteraction target)
        {
            // Fading labels and locked interactions cannot claim pickup priority.
            foreach (ObjectHover interaction in interactions)
                if (interaction != null && (interaction.transform == target.transform || target.Item != null && interaction.Item == target.Item) && interaction.IsHovered
                    && interaction.Available(InteractionChannels.Hover))
                    return true;
            return false;
        }

        /// <summary>Evaluates the active set after the player camera has finished moving.</summary>
        /// <param name="observer">Owner of the current first-person view.</param>
        internal static void Tick(HoverObserver observer)
        {
            // Snapshot membership so hover events may enable or remove other interactions safely.
            float time = Time.unscaledTime;
            frame.Clear();
            for (int index = interactions.Count - 1; index >= 0; index--)
                if (interactions[index] == null)
                    interactions.RemoveAt(index);
                else
                    frame.Add(interactions[index]);

            // Keep collider raycasts aligned with transform-driven movement before the observation pass.
            if (frame.Count > 0)
                Physics.SyncTransforms();

            // Compare live screen positions; expensive visibility queries keep each hover's own interval.
            ObjectHover selected = null;
            float score = float.PositiveInfinity;
            foreach (ObjectHover interaction in frame)
                if (interaction.Evaluate(observer, time) && interaction.Settings.TargetMode != HoverDetectionMode.Cursor)
                {
                    float candidate = interaction.Settings.TargetMode == HoverDetectionMode.CenterCollider ? 0f
                        : ((Vector2)observer.View.WorldToScreenPoint(interaction.WorldAnchor) - observer.View.pixelRect.center).sqrMagnitude;
                    if (Closer(interaction, selected, candidate, score))
                    {
                        selected = interaction;
                        score = candidate;
                    }
                }

            // Release the previous winner before the new one emits events; Pop In may finish independently.
            foreach (ObjectHover interaction in frame)
                if (interaction != null && interaction.Settings != null && interaction.Settings.TargetMode != HoverDetectionMode.Cursor
                    && interaction != selected)
                    interaction.Present(observer, time, false);
            foreach (ObjectHover interaction in frame)
                if (interaction != null && interaction.Settings != null
                    && (interaction == selected || interaction.Settings.TargetMode == HoverDetectionMode.Cursor))
                    interaction.Present(observer, time, true);
        }

        /// <summary>Chooses the closest screen anchor without allowing grace or equal-score ties to cause flicker.</summary>
        /// <param name="candidate">Eligible centre-targeted hover.</param>
        /// <param name="selected">Best candidate so far.</param>
        /// <param name="score">Candidate squared pixel distance from viewport centre.</param>
        /// <param name="best">Previously selected squared pixel distance.</param>
        /// <returns>True when this candidate should own the centre hover.</returns>
        private static bool Closer(ObjectHover candidate, ObjectHover selected, float score, float best)
        {
            // A real detection takes precedence over an old winner retained only by its release delay.
            if (selected == null)
                return true;
            if (candidate.Detected != selected.Detected)
                return candidate.Detected;
            if (score != best)
                return score < best;
            if (candidate.Settings.TargetMode == HoverDetectionMode.CenterCollider && selected.Settings.TargetMode == HoverDetectionMode.CenterCollider
                && candidate.HitDistance != selected.HitDistance)
                return candidate.HitDistance < selected.HitDistance;
            if (candidate.IsHovered != selected.IsHovered)
                return candidate.IsHovered;
            return EntityId.ToULong(candidate.GetEntityId()) < EntityId.ToULong(selected.GetEntityId());
        }

        /// <summary>Clears visible labels when observation stops or its context is missing.</summary>
        internal static void HideAll()
        {
            // This runs on context transitions, not as a redundant hidden-frame loop.
            foreach (ObjectHover interaction in interactions)
                if (interaction != null)
                    interaction.Hide();
        }

        #endregion

        #endregion
    }
}
