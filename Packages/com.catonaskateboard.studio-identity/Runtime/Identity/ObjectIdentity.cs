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
        private readonly HashSet<ObjectFlag> effective = new HashSet<ObjectFlag>();
        private readonly List<(UnityEngine.Object Owner, ObjectFlag Flag, ObjectFlagOperation Operation)> temporary
            = new List<(UnityEngine.Object, ObjectFlag, ObjectFlagOperation)>();
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
                return temporary.Count > 0 ? effective : current;
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
            // Permanent membership survives pooling; temporary contact membership ends with activation.
            ObjectIdentityRegistry.Unregister(this);
            temporary.Clear();
            effective.Clear();
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
            temporary.Clear();
            effective.Clear();
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
            return temporary.Count > 0 ? effective.Contains(flag) : current.Contains(flag);
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
            if (!Application.isPlaying || !ObjectFlagRules.IsValidOperation(operation) || operation != ObjectFlagOperation.Clear && flag == null)
                return false;
            Initialize();
            Apply(current, flag, operation);
            RefreshTemporary();
            return true;
        }

        /// <summary>Applies one owner's temporary membership change without overwriting permanent flags.</summary>
        /// <param name="owner">Interaction responsible for removing this change.</param>
        /// <param name="flag">Flag used by the operation; unused by Clear.</param>
        /// <param name="operation">Temporary membership operation.</param>
        /// <returns>True when the operation was accepted.</returns>
        public bool SetTemporary(UnityEngine.Object owner, ObjectFlag flag, ObjectFlagOperation operation)
        {
            // Separate owners compose in activation order; replacing an existing entry preserves its priority.
            if (!Application.isPlaying || owner == null || !ObjectFlagRules.IsValidOperation(operation)
                || operation != ObjectFlagOperation.Clear && flag == null)
                return false;
            Initialize();
            for (int index = 0; index < temporary.Count; index++)
                if (temporary[index].Owner == owner)
                {
                    temporary[index] = (owner, flag, operation);
                    RefreshTemporary();
                    return true;
                }
            temporary.Add((owner, flag, operation));
            RefreshTemporary();
            return true;
        }

        /// <summary>Removes only the specified owner's temporary membership operation.</summary>
        /// <param name="owner">Interaction ending its contact or being disabled.</param>
        public void RemoveTemporary(UnityEngine.Object owner)
        {
            // Remaining contact zones and permanent changes continue to define the visible membership.
            Initialize();
            for (int index = temporary.Count - 1; index >= 0; index--)
                if (temporary[index].Owner == owner)
                    temporary.RemoveAt(index);
            RefreshTemporary();
        }

        /// <summary>Rebuilds effective membership only when a permanent or temporary operation changes.</summary>
        private void RefreshTemporary()
        {
            // Ordinary membership queries read the cached set without allocations or owner searches.
            if (temporary.Count == 0)
                return;
            effective.Clear();
            effective.UnionWith(current);
            foreach ((UnityEngine.Object Owner, ObjectFlag Flag, ObjectFlagOperation Operation) entry in temporary)
                if (entry.Owner != null)
                    Apply(effective, entry.Flag, entry.Operation);
        }

        /// <summary>Applies a validated operation to either base or temporary membership.</summary>
        /// <param name="membership">Destination set.</param>
        /// <param name="flag">Selected flag.</param>
        /// <param name="operation">Validated operation.</param>
        private static void Apply(HashSet<ObjectFlag> membership, ObjectFlag flag, ObjectFlagOperation operation)
        {
            // One implementation keeps permanent and temporary operations consistent.
            switch (operation)
            {
                case ObjectFlagOperation.Replace:
                    membership.Clear();
                    membership.Add(flag);
                    break;
                case ObjectFlagOperation.Add:
                    membership.Add(flag);
                    break;
                case ObjectFlagOperation.Remove:
                    membership.Remove(flag);
                    break;
                case ObjectFlagOperation.Toggle:
                    if (!membership.Remove(flag))
                        membership.Add(flag);
                    break;
                case ObjectFlagOperation.Clear:
                    membership.Clear();
                    break;
            }
        }

        #endregion

        #endregion
    }
}
