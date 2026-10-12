using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares one prefab occurrence across immediate activation and inactive staging without serializing runtime progress.</summary>
    internal static class ObjectSpawnHistory
    {
        #region State

        private static readonly Dictionary<GameObject, int> counts = new Dictionary<GameObject, int>();
        /// <summary>Prefab currently being instantiated, available during synchronous activation.</summary>
        internal static GameObject Source { get; private set; }
        /// <summary>Reserved occurrence for all spawn-dependent features in the current hierarchy.</summary>
        internal static int Occurrence { get; private set; }
        /// <summary>Changes only when entering a new Play session.</summary>
        internal static int Generation { get; private set; }

        #endregion
        #region Methods
        #region Lifetime

        /// <summary>Starts a fresh play-session history while allowing ordinary scene changes to retain it.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            counts.Clear();
            Source = null;
            Occurrence = 0;
            Generation++;
        }

        /// <summary>Advances a prefab or scene object's logical spawn count exactly once.</summary>
        /// <param name="source">Prefab source, or scene object for a directly activated scene lifetime.</param>
        /// <returns>The next one-based occurrence.</returns>
        internal static int Next(GameObject source)
        {
            counts.TryGetValue(source, out int previous);
            int occurrence = checked(previous + 1);
            counts[source] = occurrence;
            return occurrence;
        }

        /// <summary>Creates a world-space instance with its source available to synchronous OnEnable callbacks.</summary>
        /// <param name="prefab">Original prefab shared across spawners.</param>
        /// <param name="position">Initial world position.</param>
        /// <param name="rotation">Initial world rotation.</param>
        /// <returns>The created instance with spawn-dependent interactions prepared.</returns>
        internal static GameObject Create(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return Create(prefab, () => UnityEngine.Object.Instantiate(prefab, position, rotation));
        }

        /// <summary>Prepares staged clones before their first activation or arrival animation.</summary>
        /// <param name="prefab">Original prefab shared across days and spawners.</param>
        /// <param name="parent">Existing inactive staging transform.</param>
        /// <param name="worldPositionStays">Unity parenting policy.</param>
        /// <returns>The staged clone with its reserved occurrence.</returns>
        internal static GameObject Create(GameObject prefab, Transform parent, bool worldPositionStays)
        {
            return Create(prefab, () => UnityEngine.Object.Instantiate(prefab, parent, worldPositionStays));
        }

        /// <summary>Restores nested spawn context even when an activation callback starts another spawn.</summary>
        /// <param name="prefab">Counter key for this logical spawn.</param>
        /// <param name="factory">Instantiation operation executed within the source context.</param>
        /// <returns>The new hierarchy with order catalog selection frozen for this lifetime.</returns>
        private static GameObject Create(GameObject prefab, Func<GameObject> factory)
        {
            GameObject previousSource = Source;
            int previousOccurrence = Occurrence;
            Source = prefab;
            Occurrence = Next(prefab);
            try
            {
                GameObject instance = factory();
                foreach (ObjectItem item in instance.GetComponentsInChildren<ObjectItem>(true))
                    item.PrepareSpawn(prefab, Occurrence);
                foreach (ObjectAvailableOrders orders in instance.GetComponentsInChildren<ObjectAvailableOrders>(true))
                    orders.PrepareSpawn(prefab, Occurrence);
                foreach (ObjectDialogue dialogue in instance.GetComponentsInChildren<ObjectDialogue>(true))
                    dialogue.PrepareSpawn();
                return instance;
            }
            finally
            {
                Source = previousSource;
                Occurrence = previousOccurrence;
            }
        }

        #endregion
        #endregion
    }
}
