using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Tracks enabled dialogue components without repeated scene searches during arbitration.</summary>
    internal static class DialogueRegistry
    {
        #region State

        private static readonly List<ObjectDialogue> items = new List<ObjectDialogue>();

        #endregion

        #region Properties

        /// <summary>Enabled dialogue features, including multiple components on the same item.</summary>
        internal static IReadOnlyList<ObjectDialogue> Items => items;
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
            foreach (ObjectDialogue item in Object.FindObjectsByType<ObjectDialogue>())
                if (item.isActiveAndEnabled)
                {
                    item.Initialize();
                    Register(item);
                }
        }

        /// <summary>Registers an activated component once.</summary>
        /// <param name="item">Dialogue becoming available.</param>
        internal static void Register(ObjectDialogue item)
        {
            // OnEnable and Play recovery can discover the same component.
            if (items.Contains(item))
                return;
            items.Add(item);
            Revision++;
        }

        /// <summary>Removes a disabled or destroyed dialogue from future arbitration.</summary>
        /// <param name="item">Dialogue no longer available.</param>
        internal static void Unregister(ObjectDialogue item)
        {
            // A no-op removal does not invalidate other components' bindings.
            if (items.Remove(item))
                Revision++;
        }

        #endregion

        #endregion
    }
}
