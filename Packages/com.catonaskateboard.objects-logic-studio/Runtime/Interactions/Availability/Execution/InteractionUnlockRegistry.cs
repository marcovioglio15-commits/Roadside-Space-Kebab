using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares one input router across active unlock rules without searching objects each frame.</summary>
    internal static class InteractionUnlockRegistry
    {
        #region State

        private static readonly List<ObjectInteractionUnlock> items = new List<ObjectInteractionUnlock>();
        private static readonly List<InputActionReference> consumed = new List<InputActionReference>();
        private static readonly InteractionInputRouter input = new InteractionInputRouter();
        private static bool dirty = true;
        private static int revision = -1;

        #endregion

        #region Properties

        /// <summary>Commands consumed by unlock rules during the current observer pass.</summary>
        internal static IReadOnlyList<InputActionReference> Consumed => consumed;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Resets retained input and discovers preserved rules once when entering Play.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            // Reinitialization supports both scene reload settings without duplicate event subscriptions.
            Reset();
            items.Clear();
            foreach (ObjectInteractionUnlock rule in Object.FindObjectsByType<ObjectInteractionUnlock>())
                if (rule.isActiveAndEnabled)
                {
                    items.Add(rule);
                    rule.Initialize();
                }
        }

        /// <summary>Adds a spawned or enabled rule to the next binding pass.</summary>
        /// <param name="rule">Rule becoming available.</param>
        internal static void Register(ObjectInteractionUnlock rule)
        {
            // The same component can register only once.
            if (!items.Contains(rule))
                items.Add(rule);
            dirty = true;
        }

        /// <summary>Removes a disabled or destroyed rule from future input evaluation.</summary>
        /// <param name="rule">Rule leaving the active catalog.</param>
        internal static void Unregister(ObjectInteractionUnlock rule)
        {
            // Rebinding also releases input subscriptions no longer needed by any rule.
            items.Remove(rule);
            dirty = true;
        }

        /// <summary>Releases input ownership when the observer loses its player.</summary>
        internal static void Reset()
        {
            // Runtime lock progress belongs to the rules, not the current observer.
            input.Reset();
            consumed.Clear();
            dirty = true;
            revision = -1;
        }

        #endregion

        #region Evaluation

        /// <summary>Resolves player Buttons at context boundaries and evaluates the active rules.</summary>
        /// <param name="observer">Single active observer supplying the player.</param>
        internal static void Tick(HoverObserver observer)
        {
            // An empty category adds no PlayerInput discovery work.
            consumed.Clear();
            if (items.Count == 0)
                return;
            input.Refresh(observer.Player);
            if (dirty || revision != input.Revision)
            {
                input.ClearBindings();
                foreach (ObjectInteractionUnlock rule in items)
                    if (rule != null)
                        rule.Bind(input);
                dirty = false;
                revision = input.Revision;
            }
            if (input.Usable && Time.timeScale > 0f)
                foreach (ObjectInteractionUnlock rule in items)
                    if (rule != null)
                        rule.Tick(observer.Player);
            input.ClearSignals();
        }

        /// <summary>Marks an unlock press so it cannot also trigger another interaction on this frame.</summary>
        /// <param name="action">Authored action used by a satisfied input condition.</param>
        internal static void Consume(InputActionReference action)
        {
            // Several rules may intentionally share a command; other interaction drivers suppress it afterward.
            if (!consumed.Contains(action))
                consumed.Add(action);
        }

        #endregion

        #endregion
    }
}
