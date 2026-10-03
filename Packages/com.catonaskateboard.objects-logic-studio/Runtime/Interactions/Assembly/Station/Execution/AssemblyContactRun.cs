using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Tracks uninterrupted ingredient contact without requiring Rigidbody collision callbacks.</summary>
    internal sealed class AssemblyContactRun
    {
        #region State

        private readonly ContactDetection detection = new ContactDetection();
        private readonly AssemblyRecipeProposal proposal = new AssemblyRecipeProposal();
        private ObjectAssemblyStation owner;
        private readonly HashSet<ObjectItem> contacts = new HashSet<ObjectItem>();
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
            Reset();
            owner = station;
            detection.Bind(station.Item);
        }

        /// <summary>Forgets contacts when the interaction is unavailable or disabled.</summary>
        internal void Reset()
        {
            // Reusable collections keep their capacity between interactions.
            owner?.Pending.Release();
            contacts.Clear();
            started.Clear();
            removed.Clear();
            candidates.Clear();
            ready.Clear();
            nextQuery = 0f;
        }

        #endregion

        #region Evaluation

        /// <summary>Resolves shared recipe prefixes before committing a contact assembly.</summary>
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
            station.Pending.Refresh(contacts);
            candidates.Clear();
            AssemblyInteractionRegistry.CollectPending(station, candidates);
            foreach (ObjectGrab ingredient in candidates)
                contacts.Add(ingredient.Item);
            removed.Clear();
            foreach (ObjectItem item in started.Keys)
                if (item == null || !contacts.Contains(item))
                    removed.Add(item);
            foreach (ObjectItem item in removed)
                started.Remove(item);
            // Share visual contacts while preserving each recipe's own duration and flag requirements.
            candidates.Clear();
            ready.Clear();
            foreach (ObjectItem item in contacts)
                if (item != null && !AssemblyInteractionRegistry.IsWithdrawn(station, item)
                    && item.TryGetComponent(out ObjectGrab ingredient))
                    candidates.Add(ingredient);
                else
                    started.Remove(item);
            foreach (ObjectGrab ingredient in candidates)
            {
                ObjectItem item = ingredient.Item;
                if (!(station.CurrentProduct == null ? station.CanPreview(ingredient) : station.CanAccept(ingredient, false)))
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
                {
                    int index = 0;
                    while (index < ready.Count && !Precedes(ingredient, ready[index]))
                        index++;
                    ready.Insert(index, ingredient);
                }
            }
            if (ready.Count == 0)
                return;
            if (station.CurrentProduct != null)
            {
                // Once chosen, a product continues through its own existing ingredient and slot rules.
                foreach (ObjectGrab ingredient in ready)
                    if (station.Execute(ingredient, false))
                    {
                        started.Remove(ingredient.Item);
                        break;
                    }
                return;
            }
            proposal.Build(station, ready);
            if (proposal.Ingredients.Count == 0)
                return;
            // One transaction attaches the whole discriminating prefix, including shared snapped ingredients.
            if (station.ExecuteSequence(proposal.Ingredients))
                foreach (ObjectGrab ingredient in proposal.Ingredients)
                    started.Remove(ingredient.Item);
            else
                station.Pending.Show(station, proposal.Ingredients);
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
