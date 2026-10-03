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
                {
                    items.Add(station);
                    station.InitializeContact();
                }
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

        #region Recipe Alternatives

        /// <summary>Shares pending placements across recipes driven by the same table interaction mode.</summary>
        /// <param name="station">Station collecting candidate ingredients.</param>
        /// <param name="ingredients">Reusable destination receiving distinct pending objects.</param>
        internal static void CollectPending(ObjectAssemblyStation station, List<ObjectGrab> ingredients)
        {
            // Only registered sibling components participate; different physical tables stay independent.
            foreach (ObjectAssemblyStation other in items)
                if (SharesChoices(station, other))
                    other.Pending.Collect(ingredients);
        }

        /// <summary>Preserves a manual withdrawal for every competing contact recipe on the same table.</summary>
        /// <param name="station">Station evaluating a physical contact.</param>
        /// <param name="item">Object moved by a different interaction.</param>
        /// <returns>True while a sibling preview is waiting for the object to leave contact.</returns>
        internal static bool IsWithdrawn(ObjectAssemblyStation station, ObjectItem item)
        {
            // An alternative recipe must not immediately undo a movement that released another preview.
            foreach (ObjectAssemblyStation other in items)
                if (SharesChoices(station, other) && other.Pending.IsWithdrawn(item))
                    return true;
            return false;
        }

        /// <summary>Checks whether a proposed prefix still belongs to more than one available recipe.</summary>
        /// <param name="station">Station proposing a new product.</param>
        /// <param name="ingredients">Ordered physical prefix being evaluated.</param>
        /// <param name="complete">Whether this prefix already completes the proposing recipe.</param>
        /// <returns>True while another available recipe keeps this choice unresolved or takes priority.</returns>
        internal static bool IsAmbiguous(ObjectAssemblyStation station, IReadOnlyList<ObjectGrab> ingredients, bool complete)
        {
            // A completed shorter recipe wins over an unfinished extension; identical completed recipes use stable identity.
            foreach (ObjectAssemblyStation other in items)
            {
                if (other == station || !SharesChoices(station, other) || !other.CanPlan
                    || !other.Template.CanAcceptSequence(ingredients, out bool otherComplete))
                    continue;
                if (station.Settings.ProductPrefab == other.Settings.ProductPrefab || complete && otherComplete)
                {
                    if (EntityId.ToULong(other.GetEntityId()) < EntityId.ToULong(station.GetEntityId()))
                        return true;
                }
                else if (!complete)
                    return true;
            }
            return false;
        }

        /// <summary>Groups automatic recipes and shared input commands without merging distinct explicit commands.</summary>
        /// <param name="first">Proposing station.</param>
        /// <param name="second">Potential alternative on the same GameObject.</param>
        /// <returns>True when both stations represent alternatives for the same player action or contact surface.</returns>
        private static bool SharesChoices(ObjectAssemblyStation first, ObjectAssemblyStation second)
        {
            // Different input bindings explicitly select a recipe and therefore need no ingredient arbitration.
            return second != null && second.isActiveAndEnabled && first.gameObject == second.gameObject
                && (first.Settings.Trigger != AssemblyStationTrigger.InputAction && second.Settings.Trigger != AssemblyStationTrigger.InputAction
                    || first.Settings.Trigger == AssemblyStationTrigger.InputAction && second.Settings.Trigger == AssemblyStationTrigger.InputAction
                        && first.Action != null && second.Action != null && first.Action.action != null && second.Action.action != null
                        && first.Action.action.id == second.Action.action.id);
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
                if (station == null || station.Settings.Trigger != AssemblyStationTrigger.InputAction)
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
