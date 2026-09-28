using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity
{
    /// <summary>Assigns any combination of custom object flags without string lookup or generated gameplay code.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Studio Identity/Object Identity")]
    public sealed class ObjectIdentity : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Object Identity")]
        [Tooltip("Independent flags assigned at activation. An object may belong to several categories at once.")]
        [SerializeField]
        private ObjectFlag[] flags = Array.Empty<ObjectFlag>();

        #endregion

        #region State

        private readonly HashSet<ObjectFlag> current = new HashSet<ObjectFlag>();
        private int initializationSession = -1;
        private static int session;

        #endregion

        #region Properties

        /// <summary>Authored flags used by editor tools and restored on each new Play session.</summary>
        public IReadOnlyList<ObjectFlag> AuthoredFlags => flags;
        /// <summary>Current runtime membership for consumption receipts and diagnostics.</summary>
        public IReadOnlyCollection<ObjectFlag> ActiveFlags
        {
            get
            {
                // The set is initialized on demand for inactive staging objects.
                Initialize();
                return current;
            }
        }

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Reports invalid authored membership without changing the saved selection.</summary>
        private void OnValidate()
        {
            // Invalid references remain available for explicit correction and native Undo.
            if (!ObjectFlagRules.TryValidate(flags, true, out string warning))
                Debug.LogWarning(warning, this);
        }

        /// <summary>Registers this identity without discarding changes retained through pooling or storage.</summary>
        private void OnEnable()
        {
            // Re-enabling within a session keeps the current flag set.
            Initialize();
            ObjectIdentityRegistry.Register(this);
        }

        /// <summary>Removes the identity from active-player discovery while preserving its membership.</summary>
        private void OnDisable()
        {
            // Inactive ingredients can still retain their flags while owned by a product.
            ObjectIdentityRegistry.Unregister(this);
        }

        /// <summary>Invalidates retained membership before scene callbacks start a new Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            // Lazy initialization also resets inactive objects without erasing changes made in Awake.
            session++;
        }

        /// <summary>Builds membership once before its first runtime query.</summary>
        private void Initialize()
        {
            // No allocations or asset searches occur on ordinary membership checks.
            if (initializationSession == session)
                return;
            initializationSession = session;
            current.Clear();
            if (flags != null)
                foreach (ObjectFlag flag in flags)
                    if (flag != null)
                        current.Add(flag);
        }

        #endregion

        #region Matching

        /// <summary>Checks one stable flag reference independently of its display name.</summary>
        /// <param name="flag">Required flag asset.</param>
        /// <returns>True when this identity contains the requested flag.</returns>
        public bool Has(ObjectFlag flag)
        {
            // Edit-time inspection follows serialized authoring without running gameplay initialization.
            if (flag == null)
                return false;
            if (!Application.isPlaying)
                return flags != null && Array.IndexOf(flags, flag) >= 0;
            Initialize();
            return current.Contains(flag);
        }

        /// <summary>Checks a combination of flags using the requested any/all rule.</summary>
        /// <param name="required">Selected required flags.</param>
        /// <param name="match">Whether one or all flags must match.</param>
        /// <returns>True when a nonempty selection satisfies the requested match mode.</returns>
        public bool Matches(IReadOnlyList<ObjectFlag> required, ObjectFlagMatch match = ObjectFlagMatch.Any)
        {
            // Empty requirements never become implicit wildcards.
            if (required == null || required.Count == 0)
                return false;
            foreach (ObjectFlag flag in required)
                if (Has(flag) != (match == ObjectFlagMatch.All))
                    return match == ObjectFlagMatch.Any;
            return match == ObjectFlagMatch.All;
        }

        /// <summary>Finds an assigned flag on a collider branch or any parent object.</summary>
        /// <param name="branch">Collider or camera branch being examined.</param>
        /// <param name="flag">Required stable flag.</param>
        /// <returns>The nearest matching identity, or null.</returns>
        public static ObjectIdentity FindInParents(Transform branch, ObjectFlag flag)
        {
            // Parent traversal supports compound surfaces without requiring Object Item components.
            while (branch != null)
            {
                if (branch.TryGetComponent(out ObjectIdentity identity) && identity.Has(flag))
                    return identity;
                branch = branch.parent;
            }
            return null;
        }

        #endregion

        #region Changes

        /// <summary>Changes runtime membership at a successful interaction boundary.</summary>
        /// <param name="flag">Flag to replace, add, remove or toggle; unused by Clear.</param>
        /// <param name="operation">Requested membership operation.</param>
        /// <returns>True when the operation is valid and was applied.</returns>
        public bool Change(ObjectFlag flag, ObjectFlagOperation operation)
        {
            // Authored defaults are preserved; changing a runtime identity never edits an asset or prefab.
            if (!Application.isPlaying || operation != ObjectFlagOperation.Clear && flag == null)
                return false;
            Initialize();
            switch (operation)
            {
                case ObjectFlagOperation.Replace:
                    current.Clear();
                    current.Add(flag);
                    break;
                case ObjectFlagOperation.Add:
                    current.Add(flag);
                    break;
                case ObjectFlagOperation.Remove:
                    current.Remove(flag);
                    break;
                case ObjectFlagOperation.Toggle:
                    if (!current.Remove(flag))
                        current.Add(flag);
                    break;
                case ObjectFlagOperation.Clear:
                    current.Clear();
                    break;
                default:
                    return false;
            }
            return true;
        }

        #endregion

        #endregion
    }
}
