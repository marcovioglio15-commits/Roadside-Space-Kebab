using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Provides a common component family for independently enabled interactions on one prefab.</summary>
    public abstract class ObjectInteraction : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Interaction")]
        [Tooltip("Name shown in Objects Logic Studio to distinguish interactions on the same object.")]
        [SerializeField]
        private string interactionName = "Interaction";

        [Tooltip("Optional item tag change at a successful start or completion.")]
        [SerializeField]
        private InteractionTagChange tagChange = new InteractionTagChange();

        #endregion

        #region State

        private HashSet<UnityEngine.Object> unlockOwners;
        private ObjectItem item;
        private int itemSession = -1;
        private static int session;

        #endregion

        #region Properties

        /// <summary>Identifies this component in the prefab workspace.</summary>
        public string InteractionName => interactionName;
        /// <summary>Optional tag change configured independently for this interaction.</summary>
        public InteractionTagChange TagChange => tagChange;
        /// <summary>Whether any active unlock rule still owns a lock on this feature.</summary>
        public bool IsLocked => unlockOwners != null && unlockOwners.Count > 0;
        /// <summary>Successful interaction lifecycle events used by prefab-local unlock rules.</summary>
        public static event Action<ObjectInteraction, InteractionMoment> Signaled;

        /// <summary>Optional shared item state; older hover and single-action prefabs remain usable without it.</summary>
        internal ObjectItem Item
        {
            get
            {
                // Refresh once per Play session, including prefab dependency edits with domain reload disabled.
                if (itemSession != session)
                {
                    item = GetComponentInParent<ObjectItem>(true);
                    itemSession = session;
                }
                return item;
            }
        }

        #endregion

        #region Methods

        #region Availability

        /// <summary>Invalidates cached item ownership when Play retains managed component fields.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetItemCache()
        {
            // Existing components resolve new or replaced prefab dependencies on their first availability check.
            session++;
            Signaled = null;
            foreach (ObjectInteraction interaction in FindObjectsByType<ObjectInteraction>(FindObjectsInactive.Include))
                interaction.unlockOwners?.Clear();
        }

        /// <summary>Combines authored component availability with temporary item restrictions.</summary>
        /// <param name="channels">Interaction family currently being attempted.</param>
        /// <returns>True when the interaction may participate.</returns>
        internal bool Available(InteractionChannels channels)
        {
            // A missing Object Item preserves the behavior of existing prefab features.
            return isActiveAndEnabled && !IsLocked && (Item == null || !Item.IsBlocked(channels));
        }

        /// <summary>Adds or removes one rule's independent lock without changing the authored enabled state.</summary>
        /// <param name="owner">Unlock rule owning this restriction.</param>
        /// <param name="locked">Whether the rule is still waiting for its conditions.</param>
        internal void SetLocked(UnityEngine.Object owner, bool locked)
        {
            // Most interactions never need a lock collection.
            if (locked)
            {
                unlockOwners ??= new HashSet<UnityEngine.Object>();
                unlockOwners.Add(owner);
            }
            else
                unlockOwners?.Remove(owner);
        }

        /// <summary>Publishes only a successful start or committed completion.</summary>
        /// <param name="moment">Lifecycle boundary reached by this interaction.</param>
        internal void Signal(InteractionMoment moment)
        {
            // Cancellations and interrupted transitions never emit completion.
            tagChange?.Apply(Item != null ? Item.gameObject : gameObject, moment);
            Signaled?.Invoke(this, moment);
        }

        #endregion

        #endregion
    }
}
