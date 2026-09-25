using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Tracks enabled slice components without repeated scene searches during arbitration.</summary>
    internal static class SliceRegistry
    {
        #region State

        private static readonly List<ObjectSlice> items = new List<ObjectSlice>();

        #endregion

        #region Properties

        /// <summary>Enabled slice features, including multiple components on the same item.</summary>
        internal static IReadOnlyList<ObjectSlice> Items => items;
        /// <summary>Changes at registration boundaries so drivers rebuild input bindings only when necessary.</summary>
        internal static int Revision { get; private set; }

        #endregion

        #region Methods

        #region Registration

        /// <summary>Restores registration and progress at Play entry when reload options preserve scene objects.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            // Discovery runs once per Play entry, independent of ordinary frame updates.
            items.Clear();
            Revision++;
            foreach (ObjectSlice item in Object.FindObjectsByType<ObjectSlice>())
                if (item.isActiveAndEnabled)
                {
                    item.Initialize();
                    Register(item);
                }
        }

        /// <summary>Registers an activated component once.</summary>
        /// <param name="item">Slice becoming available.</param>
        internal static void Register(ObjectSlice item)
        {
            // OnEnable and Play recovery can discover the same component.
            if (items.Contains(item))
                return;
            items.Add(item);
            Revision++;
        }

        /// <summary>Removes a disabled or destroyed slice from future arbitration.</summary>
        /// <param name="item">Slice no longer available.</param>
        internal static void Unregister(ObjectSlice item)
        {
            // A no-op removal does not invalidate other components' bindings.
            if (items.Remove(item))
                Revision++;
        }

        #endregion

        #endregion
    }
}
