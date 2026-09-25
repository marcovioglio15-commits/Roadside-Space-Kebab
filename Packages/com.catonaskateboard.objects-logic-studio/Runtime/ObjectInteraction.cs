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

        [Header("Start VFX")]
        [Tooltip("Independent optional start effect for this exact interaction component.")]
        [SerializeField]
        private InteractionVfxSettings visualEffect = new InteractionVfxSettings();

        [Tooltip("Prefab identities and remapped roots used by external spawn completion rules.")]
        [SerializeField]
        [HideInInspector]
        private CompletionSourceBinding[] completionBindings = Array.Empty<CompletionSourceBinding>();

        #endregion

        #region State

        private HashSet<UnityEngine.Object> unlockOwners;
        private ObjectItem item;
        private int itemSession = -1;
        private bool effectChecked;
        private bool effectReady;
        private static int session;

        #endregion

        #region Properties

        /// <summary>Identifies this component in the prefab workspace.</summary>
        public string InteractionName => interactionName;
        /// <summary>Independent start effect configured on this interaction, never shared with another card.</summary>
        public InteractionVfxSettings VisualEffect => visualEffect;
        /// <summary>Positive predefined duration available to automatic VFX timing, or zero for untimed interactions.</summary>
        internal virtual float VfxDuration => 0f;
        /// <summary>Whether a timed effect's source still owns an active or retained operation.</summary>
        internal virtual bool VfxRunning => isActiveAndEnabled;
        /// <summary>Whether a timed effect must preserve its remaining duration and playback.</summary>
        internal virtual bool VfxPaused => false;

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
            {
                interaction.unlockOwners?.Clear();
                interaction.effectChecked = false;
            }
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

        /// <summary>Resolves an editor-authored prefab identity to this clone's corresponding instance root.</summary>
        /// <param name="identity">Stable component identity selected by a spawn condition.</param>
        /// <param name="root">Receives the runtime root remapped by Unity during prefab instantiation.</param>
        /// <returns>True when this interaction belongs to the requested prefab source.</returns>
        internal bool TryResolveCompletionRoot(string identity, out Transform root)
        {
            // Multiple bindings preserve independent links to nested and containing prefab assets.
            foreach (CompletionSourceBinding binding in completionBindings)
                if (binding != null && binding.Identity == identity && binding.Root != null)
                {
                    root = binding.Root;
                    return true;
                }
            root = null;
            return false;
        }

        /// <summary>Publishes only a successful start or committed completion.</summary>
        /// <param name="moment">Lifecycle boundary reached by this interaction.</param>
        internal void Signal(InteractionMoment moment)
        {
            // Cancellations and interrupted transitions never emit completion.
            tagChange?.Apply(Item != null ? Item.gameObject : gameObject, moment);
            if (moment == InteractionMoment.Started)
                StartEffect();
            Signaled?.Invoke(this, moment);
        }

        /// <summary>Validates once and starts this component's own optional visual effect.</summary>
        private void StartEffect()
        {
            // Every successful start owns an independent finite effect lifetime.
            if (visualEffect == null || !visualEffect.Enabled)
                return;
            if (!effectChecked)
            {
                effectChecked = true;
                effectReady = visualEffect.TryValidate(VfxDuration > 0f, out string warning);
                if (!effectReady)
                    Debug.LogWarning(warning, this);
            }
            if (!effectReady)
                return;
            InteractionVfxInstance.Create(this, visualEffect, visualEffect.AutoTiming ? VfxDuration : visualEffect.Duration);
        }

        #endregion

        #endregion
    }
}
