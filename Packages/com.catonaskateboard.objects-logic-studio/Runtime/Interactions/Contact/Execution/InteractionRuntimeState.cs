using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Owns staged creation, safe contact edit dispatch and restoration at Play boundaries.</summary>
    public static class InteractionRuntimeState
    {
        #region Types

        /// <summary>Defers component activation callbacks while initial runtime settings are assigned.</summary>
        internal readonly struct StageScope : IDisposable
        {
            #region Methods

            /// <summary>Releases the current nested creation scope.</summary>
            public void Dispose()
            {
                staging--;
            }

            #endregion
        }

        #endregion

        #region State

        private static readonly Dictionary<ObjectInteraction, InteractionRuntimeSnapshot> originals = new Dictionary<ObjectInteraction, InteractionRuntimeSnapshot>();
        private static readonly List<ObjectInteraction> added = new List<ObjectInteraction>();
        private static readonly HashSet<ObjectInteraction> removed = new HashSet<ObjectInteraction>();
        private static readonly Queue<InteractionChangeRun> pending = new Queue<InteractionChangeRun>();
        private static int staging;
        private static bool flushing;

        #endregion

        #region Properties

        /// <summary>Whether a typed factory is assigning settings before an interaction may initialize.</summary>
        internal static bool IsStaging => staging > 0;

        #endregion

        #region Methods

        #region Transactions

        /// <summary>Whether the card was logically removed while preserving its original references for Edit mode.</summary>
        /// <param name="feature">Existing card to check.</param>
        /// <returns>True when a committed runtime removal owns the card.</returns>
        internal static bool IsRemoved(ObjectInteraction feature)
        {
            return removed.Contains(feature);
        }

        /// <summary>Marks logical removal without destroying an original component needed for restoration.</summary>
        /// <param name="feature">Changed card.</param>
        /// <param name="value">Whether the card is currently removed.</param>
        internal static void SetRemoved(ObjectInteraction feature, bool value)
        {
            if (value)
                removed.Add(feature);
            else
                removed.Remove(feature);
        }

        /// <summary>Finds a removed unique card that can be added again without breaking saved references.</summary>
        /// <param name="root">Affected object.</param>
        /// <param name="kind">Requested family.</param>
        /// <returns>Reusable removed card, or null.</returns>
        internal static ObjectInteraction FindRemoved(GameObject root, InteractionRuntimeKind kind)
        {
            foreach (ObjectInteraction feature in removed)
                if (feature != null && feature.gameObject == root && InteractionRuntimeCatalog.KindOf(feature) == kind)
                    return feature;
            return null;
        }

        /// <summary>Starts a nonallocating scope that suppresses premature lifecycle work.</summary>
        /// <returns>A scope to dispose after assigning and disabling new components.</returns>
        internal static StageScope Stage()
        {
            staging++;
            return new StageScope();
        }

        /// <summary>Retains the first configuration for restoration after this Play session.</summary>
        /// <param name="feature">Existing card about to change.</param>
        internal static void Remember(ObjectInteraction feature)
        {
            if (feature != null && !added.Contains(feature) && !originals.ContainsKey(feature))
                originals.Add(feature, new InteractionRuntimeSnapshot(feature));
        }

        /// <summary>Tracks newly added cards so none remain after Play without scene reload.</summary>
        /// <param name="feature">Fully configured new runtime card.</param>
        internal static void Include(ObjectInteraction feature)
        {
            added.Add(feature);
        }

        /// <summary>Queues one prepared batch until the modifier finishes iterating its active rules.</summary>
        /// <param name="run">Prepared participant edit batch.</param>
        internal static void Enqueue(InteractionChangeRun run)
        {
            if (run != null)
                pending.Enqueue(run);
        }

        /// <summary>Applies queued batches outside the modifier's rule and completion callback stacks.</summary>
        internal static void Flush()
        {
            if (flushing || pending.Count == 0)
                return;
            flushing = true;
            try
            {
                int budget = 128;
                while (pending.Count > 0 && budget-- > 0)
                    pending.Dequeue().Apply();
            }
            finally
            {
                flushing = false;
            }
        }

        #endregion

        #region Session

        /// <summary>Restores serialized defaults even when the editor retains scene objects between Play sessions.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void RestoreSession()
        {
            pending.Clear();
            using (Stage())
            {
                foreach (InteractionRuntimeSnapshot snapshot in originals.Values)
                {
                    snapshot.Restore(false);
                    if (snapshot.Feature != null)
                        snapshot.Feature.enabled = snapshot.Enabled;
                }
                for (int index = added.Count - 1; index >= 0; index--)
                    if (added[index] != null)
                        UnityEngine.Object.DestroyImmediate(added[index]);
            }
            originals.Clear();
            added.Clear();
            removed.Clear();
            flushing = false;
        }

        #endregion

        #endregion
    }
}
