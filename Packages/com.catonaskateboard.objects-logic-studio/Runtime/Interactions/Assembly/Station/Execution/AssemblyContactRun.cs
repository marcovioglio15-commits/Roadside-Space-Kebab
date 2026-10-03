using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Tracks uninterrupted ingredient contact without requiring Rigidbody collision callbacks.</summary>
    internal sealed class AssemblyContactRun
    {
        #region State

        private readonly ContactDetection detection = new ContactDetection();
        private readonly AssemblyIngredientDock dock = new AssemblyIngredientDock();
        private readonly HashSet<ObjectItem> contacts = new HashSet<ObjectItem>();
        private readonly HashSet<ObjectItem> withdrawn = new HashSet<ObjectItem>();
        private readonly Dictionary<ObjectItem, float> started = new Dictionary<ObjectItem, float>();
        private readonly List<ObjectItem> removed = new List<ObjectItem>();
        private readonly List<ObjectGrab> candidates = new List<ObjectGrab>();
        private readonly List<ObjectGrab> ready = new List<ObjectGrab>();
        private float nextQuery;

        #endregion

        #region Methods

        #region Binding

        /// <summary>Captures the station's colliders and clears progress after activation.</summary>
        /// <param name="station">Table whose collider surfaces detect ingredients.</param>
        internal void Bind(ObjectAssemblyStation station)
        {
            // Disabled/re-enabled stations never inherit elapsed time from a previous contact.
            detection.Bind(station.Item);
            Reset();
        }

        /// <summary>Forgets contacts when the interaction is unavailable or disabled.</summary>
        internal void Reset()
        {
            // Reusable collections keep their capacity between interactions.
            dock.Release();
            contacts.Clear();
            withdrawn.Clear();
            started.Clear();
            removed.Clear();
            candidates.Clear();
            ready.Clear();
            nextQuery = 0f;
        }

        #endregion

        #region Evaluation

        /// <summary>Commits one contact transfer, including both ingredients of a deferred recipe start.</summary>
        /// <param name="station">Station whose recipe and tool restrictions must still be satisfied.</param>
        /// <param name="time">Scaled time supplied by the station physics update.</param>
        internal void Tick(ObjectAssemblyStation station, float time)
        {
            // Pausing preserves elapsed game time; losing availability cancels accumulated contact.
            if (!station.Available(InteractionChannels.Assembly) || station.Item.IsReserved)
            {
                Reset();
                return;
            }
            if (Time.timeScale <= 0f || time < nextQuery)
                return;
            AssemblyStationSettings settings = station.Settings;
            nextQuery = time + settings.ContactQueryInterval;
            detection.Query(station.Item, settings.ContactTolerance, settings.IncludeTriggers, contacts);
            RefreshDock(station);
            removed.Clear();
            foreach (ObjectItem item in started.Keys)
                if (item == null || !contacts.Contains(item))
                    removed.Add(item);
            foreach (ObjectItem item in removed)
                started.Remove(item);
            // Deferred products retain their own interactions while displaying their future magnet placement.
            candidates.Clear();
            ready.Clear();
            bool waiting = false;
            foreach (ObjectItem item in contacts)
            {
                if (withdrawn.Contains(item))
                    continue;
                if (item != null && item.TryGetComponent(out ObjectGrab ingredient))
                {
                    candidates.Add(ingredient);
                    waiting |= station.WaitsForIngredient(ingredient);
                }
                else
                    started.Remove(item);
            }
            foreach (ObjectGrab ingredient in candidates)
            {
                ObjectItem item = ingredient.Item;
                if (!station.CanTrackContact(ingredient, waiting))
                {
                    started.Remove(item);
                    continue;
                }
                if (!started.TryGetValue(item, out float began))
                {
                    began = time;
                    started.Add(item, began);
                }
                if (settings.Trigger != AssemblyStationTrigger.IngredientContact || time - began >= settings.ContactDuration)
                    ready.Add(ingredient);
            }
            ObjectGrab selected = null;
            ObjectGrab companion = null;
            ObjectGrab awaiting = null;
            foreach (ObjectGrab ingredient in ready)
            {
                if (!Precedes(ingredient, selected) || waiting && !station.CanAccept(ingredient, false))
                    continue;
                bool requiresCompanion = station.WaitsForIngredient(ingredient);
                ObjectGrab next = requiresCompanion ? FindCompanion(station, ingredient) : null;
                if (requiresCompanion && next == null)
                {
                    if (Precedes(ingredient, awaiting))
                        awaiting = ingredient;
                    continue;
                }
                selected = ingredient;
                companion = next;
            }
            // Recheck ownership on commit so another station cannot consume the same ingredient twice.
            if (selected != null && station.Execute(selected, false, companion))
            {
                started.Remove(selected.Item);
                if (companion != null)
                    started.Remove(companion.Item);
                dock.Release();
            }
            else if (awaiting != null)
                dock.Snap(station, awaiting);
        }

        /// <summary>Retains a visual magnet contact and lets externally moved ingredients leave without snapping back.</summary>
        /// <param name="station">Station that owns this contact run.</param>
        private void RefreshDock(ObjectAssemblyStation station)
        {
            // A released preview can become eligible again after physically leaving the contact surface.
            ObjectItem released = dock.Refresh(station);
            if (released != null)
                withdrawn.Add(released);
            removed.Clear();
            foreach (ObjectItem item in withdrawn)
                if (item == null || !contacts.Contains(item))
                    removed.Add(item);
            foreach (ObjectItem item in removed)
            {
                withdrawn.Remove(item);
                started.Remove(item);
            }
            // Magnets may intentionally place the ingredient above or outside the trigger volume.
            if (dock.Ingredient != null)
                contacts.Add(dock.Ingredient.Item);
        }

        /// <summary>Chooses the earliest ready companion that satisfies the recipe after the deferred first ingredient.</summary>
        /// <param name="station">Empty station receiving the pair.</param>
        /// <param name="first">Completed product still independently usable.</param>
        /// <returns>A compatible distinct ingredient, or null while the product must remain free.</returns>
        private ObjectGrab FindCompanion(ObjectAssemblyStation station, ObjectGrab first)
        {
            // Only ready contacts participate, so each object must satisfy its own sustained-contact duration.
            ObjectGrab selected = null;
            foreach (ObjectGrab candidate in ready)
                if (candidate != first && Precedes(candidate, selected) && station.CanBeginWith(first, candidate))
                    selected = candidate;
            return selected;
        }

        /// <summary>Keeps selection stable across unordered physics query results.</summary>
        /// <param name="candidate">Eligible contact being considered.</param>
        /// <param name="selected">Current selection, or null before any candidate qualifies.</param>
        /// <returns>True when contact start time and then entity identity place the candidate first.</returns>
        private bool Precedes(ObjectGrab candidate, ObjectGrab selected)
        {
            // Earlier contacts win; exact ties use the same entity ordering as ordinary insertion.
            return selected == null || started[candidate.Item] < started[selected.Item]
                || started[candidate.Item] == started[selected.Item]
                    && EntityId.ToULong(candidate.GetEntityId()) < EntityId.ToULong(selected.GetEntityId());
        }

        #endregion

        #endregion
    }
}
