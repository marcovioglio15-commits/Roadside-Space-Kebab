using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Routes one performed assembly command to the nearest eligible table.</summary>
    internal static class AssemblyInteractionRegistry
    {
        #region State

        private static readonly List<ObjectAssemblyStation> items = new List<ObjectAssemblyStation>();
        private static readonly Dictionary<ObjectAssemblyStation, InteractionButton> bindings = new Dictionary<ObjectAssemblyStation, InteractionButton>();
        private static readonly InteractionInputRouter input = new InteractionInputRouter();
        private static readonly HoverPhysics physics = new HoverPhysics();
        private static bool dirty = true;
        private static int revision = -1;

        #endregion

        #region Properties

        /// <summary>Assembly action used this frame, suppressed in subsequent dialogue and single-action routing.</summary>
        internal static InputActionReference Consumed { get; private set; }

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Rebuilds retained scene registrations once per Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            // Normal activation and disabled-domain-reload sessions use the same registry.
            Reset();
            items.Clear();
            foreach (ObjectAssemblyStation station in Object.FindObjectsByType<ObjectAssemblyStation>())
                if (station.isActiveAndEnabled)
                    items.Add(station);
        }

        /// <summary>Includes a table in the next input binding pass.</summary>
        /// <param name="station">Table becoming active.</param>
        internal static void Register(ObjectAssemblyStation station)
        {
            // Registration is idempotent across startup callbacks.
            if (!items.Contains(station))
                items.Add(station);
            dirty = true;
        }

        /// <summary>Removes a disabled table from input arbitration.</summary>
        /// <param name="station">Table leaving the active scene.</param>
        internal static void Unregister(ObjectAssemblyStation station)
        {
            // Remaining products retain their own hierarchy and progress.
            items.Remove(station);
            dirty = true;
        }

        /// <summary>Clears player subscriptions after observer context changes.</summary>
        internal static void Reset()
        {
            // This does not reset recipe progress on any table or product.
            input.Reset();
            bindings.Clear();
            Consumed = null;
            dirty = true;
            revision = -1;
        }

        #endregion

        #region Input

        /// <summary>Dispatches at most one valid transfer from the player's actual carry slot.</summary>
        /// <param name="observer">Active player and final camera context.</param>
        internal static void Tick(HoverObserver observer)
        {
            // Empty categories perform no input discovery or physics work.
            Consumed = null;
            if (items.Count == 0)
                return;
            input.Refresh(observer.Player);
            if (dirty || revision != input.Revision)
                Bind();
            foreach (InputActionReference action in InteractionUnlockRegistry.Consumed)
                input.Consume(action);
            ObjectAssemblyStation selected = null;
            float nearest = float.PositiveInfinity;
            if (input.Usable && Time.timeScale > 0f && observer.HeldObject != null)
                foreach (KeyValuePair<ObjectAssemblyStation, InteractionButton> pair in bindings)
                {
                    if (!pair.Value.Pending || pair.Key == null)
                        continue;
                    ObjectAssemblyStation station = pair.Key;
                    float distance = (station.OutputPosition - observer.Player.position).sqrMagnitude;
                    if (distance > station.Settings.Distance * station.Settings.Distance || distance > nearest
                        || !station.CanAccept(observer.HeldObject)
                        || station.Settings.RequireSight && !physics.HasSight(observer, station.transform,
                            station.OutputPosition, station.Settings.Obstacles))
                        continue;
                    if (distance == nearest && selected != null
                        && EntityId.ToULong(station.GetEntityId()) > EntityId.ToULong(selected.GetEntityId()))
                        continue;
                    selected = station;
                    nearest = distance;
                }
            if (selected != null && selected.Execute(observer.HeldObject))
                Consumed = selected.Action;
            input.ClearSignals();
        }

        /// <summary>Validates table dependencies and binds only project action identifiers.</summary>
        private static void Bind()
        {
            // This pass runs on registry or PlayerInput asset changes, never on ordinary idle frames.
            input.ClearBindings();
            bindings.Clear();
            foreach (ObjectAssemblyStation station in items)
            {
                if (station == null)
                    continue;
                if (!station.TryValidate(out string warning))
                {
                    if (station != null)
                        Debug.LogWarning("Object Assemble: " + warning, station);
                    continue;
                }
                InteractionButton button = input.Bind(station.Action);
                if (button != null)
                    bindings.Add(station, button);
            }
            dirty = false;
            revision = input.Revision;
        }

        #endregion

        #endregion
    }
}
