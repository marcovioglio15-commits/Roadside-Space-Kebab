using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Tracks activation boundaries so input bindings need no repeated scene searches.</summary>
    internal static class SingleInteractionRegistry
    {
        #region State

        private static readonly List<ObjectSingleInteraction> items = new List<ObjectSingleInteraction>();

        #endregion

        #region Properties

        /// <summary>Currently enabled features in registration order.</summary>
        internal static IReadOnlyList<ObjectSingleInteraction> Items => items;
        /// <summary>Changes whenever cached input associations need rebuilding.</summary>
        internal static int Revision { get; private set; }

        #endregion

        #region Methods

        #region Registration

        /// <summary>Revalidates existing bindings after an explicit compound-geometry change.</summary>
        internal static void Invalidate()
        {
            // Assembly may make a previously collider-free product grabbable.
            Revision++;
        }

        /// <summary>Recovers enabled components when domain or scene reload is disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            // Discovery occurs once at Play entry, never during ordinary frames.
            items.Clear();
            Revision++;
            foreach (ObjectSingleInteraction item in Object.FindObjectsByType<ObjectSingleInteraction>())
                if (item.isActiveAndEnabled)
                    Register(item);
        }

        /// <summary>Makes an enabled feature available to the observer.</summary>
        /// <param name="item">Newly active component.</param>
        internal static void Register(ObjectSingleInteraction item)
        {
            // Startup recovery and OnEnable may both discover the same component.
            if (items.Contains(item))
                return;
            items.Add(item);
            Revision++;
        }

        /// <summary>Invalidates input associations after a component stops participating.</summary>
        /// <param name="item">Disabled or destroyed component.</param>
        internal static void Unregister(ObjectSingleInteraction item)
        {
            // A no-op removal must not rebuild every remaining binding.
            if (items.Remove(item))
                Revision++;
        }

        #endregion

        #endregion
    }
}
