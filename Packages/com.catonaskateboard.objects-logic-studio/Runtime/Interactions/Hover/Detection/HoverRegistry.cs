using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares active hover components with one observer without repeated scene discovery.</summary>
    internal static class HoverRegistry
    {
        #region State

        private static readonly List<ObjectHover> interactions = new List<ObjectHover>();

        #endregion

        #region Methods

        #region Registration

        /// <summary>Rebuilds registration once when entering Play, including disabled domain and scene reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            // With scene reload disabled, OnEnable is not a reliable reset boundary.
            interactions.Clear();
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

        /// <summary>Evaluates the active set after the player camera has finished moving.</summary>
        /// <param name="observer">Owner of the current first-person view.</param>
        internal static void Tick(HoverObserver observer)
        {
            // Reverse traversal also tolerates Unity objects destroyed during scene unloading.
            float time = Time.unscaledTime;
            for (int index = interactions.Count - 1; index >= 0; index--)
                if (interactions[index] == null)
                    interactions.RemoveAt(index);
                else
                    interactions[index].Tick(observer, time);
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
