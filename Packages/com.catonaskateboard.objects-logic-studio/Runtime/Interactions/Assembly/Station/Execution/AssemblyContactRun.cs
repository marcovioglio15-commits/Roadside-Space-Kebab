using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Tracks uninterrupted ingredient contact without requiring Rigidbody collision callbacks.</summary>
    internal sealed class AssemblyContactRun
    {
        #region State

        private readonly ContactDetection detection = new ContactDetection();
        private readonly HashSet<ObjectItem> contacts = new HashSet<ObjectItem>();
        private readonly Dictionary<ObjectItem, float> started = new Dictionary<ObjectItem, float>();
        private readonly List<ObjectItem> removed = new List<ObjectItem>();
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
            contacts.Clear();
            started.Clear();
            removed.Clear();
            nextQuery = 0f;
        }

        #endregion

        #region Evaluation

        /// <summary>Commits at most one eligible ingredient after sustained geometric contact.</summary>
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
            detection.Query(station.Item, settings.ContactTolerance, settings.IncludeTriggers, null, contacts);
            removed.Clear();
            foreach (ObjectItem item in started.Keys)
                if (item == null || !contacts.Contains(item))
                    removed.Add(item);
            foreach (ObjectItem item in removed)
                started.Remove(item);
            ObjectGrab selected = null;
            float earliest = float.PositiveInfinity;
            foreach (ObjectItem item in contacts)
            {
                if (!item.TryGetComponent(out ObjectGrab ingredient) || !station.CanAccept(ingredient, false))
                {
                    started.Remove(item);
                    continue;
                }
                if (!started.TryGetValue(item, out float began))
                {
                    began = time;
                    started.Add(item, began);
                }
                if (time - began < settings.ContactDuration || began > earliest)
                    continue;
                if (began == earliest && selected != null
                    && EntityId.ToULong(ingredient.GetEntityId()) > EntityId.ToULong(selected.GetEntityId()))
                    continue;
                selected = ingredient;
                earliest = began;
            }
            // Recheck ownership on commit so another station cannot consume the same ingredient twice.
            if (selected != null && station.Execute(selected, false))
                started.Remove(selected.GetComponent<ObjectItem>());
        }

        #endregion

        #endregion
    }
}
