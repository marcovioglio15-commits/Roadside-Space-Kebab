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
        private readonly List<ObjectFlagState> states = new List<ObjectFlagState>();
        private int changeDepth;
        private bool changed;
        private readonly List<(UnityEngine.Object Owner, ObjectFlag Flag, ObjectFlagOperation Operation)> temporary
            = new List<(UnityEngine.Object, ObjectFlag, ObjectFlagOperation)>();
        private int initializationSession = -1;
        private static int session;

        #endregion

        #region Properties

        /// <summary>Reports committed runtime membership changes so active systems can reevaluate their filters.</summary>
        public static event Action<ObjectIdentity> Changed;

        /// <summary>Authored flags used by editor tools and restored on each new Play session.</summary>
        public IReadOnlyList<ObjectFlag> AuthoredFlags => flags;
        /// <summary>Current runtime membership for consumption receipts and diagnostics.</summary>
        public IReadOnlyCollection<ObjectFlag> ActiveFlags
        {
            get
            {
                // The set is initialized on demand for inactive staging objects.
                Initialize();
                return temporary.Count > 0 || states.Count > 0 ? effective : current;
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
            RefreshTemporary();
        }

        /// <summary>Invalidates retained membership before scene callbacks start a new Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            // Lazy initialization also resets inactive objects without erasing changes made in Awake.
            session++;
            Changed = null;
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
            states.Clear();
            changeDepth = 0;
            changed = false;
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
            return temporary.Count > 0 || states.Count > 0 ? effective.Contains(flag) : current.Contains(flag);
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
            bool removed = false;
            for (int index = temporary.Count - 1; index >= 0; index--)
                if (temporary[index].Owner == owner)
                {
                    temporary.RemoveAt(index);
                    removed = true;
                }
            if (removed)
                RefreshTemporary();
        }

        /// <summary>Groups synchronous changes so listeners see only the final flag set.</summary>
        /// <returns>A value scope to dispose at the end of the transaction.</returns>
        public ObjectIdentityChangeScope GroupChanges()
        {
            Initialize();
            return new ObjectIdentityChangeScope(this);
        }

        /// <summary>Begins a nested change group without allocating a notification object.</summary>
        internal void BeginChangeScope()
        {
            changeDepth++;
        }

        /// <summary>Releases the group and notifies once if membership was recomputed.</summary>
        internal void EndChangeScope()
        {
            changeDepth--;
            if (changeDepth == 0 && changed)
            {
                changed = false;
                Changed?.Invoke(this);
            }
        }

        /// <summary>Replaces only this owner's reversible state, ordered below contact flags.</summary>
        /// <param name="owner">Component responsible for removing this state.</param>
        /// <param name="replace">Whether underlying membership is replaced.</param>
        /// <param name="add">Validated flags included by this state.</param>
        /// <param name="remove">Validated flags excluded by this state.</param>
        /// <param name="priority">Stable state precedence; larger values apply later.</param>
        public void SetState(UnityEngine.Object owner, bool replace, ObjectFlag[] add, ObjectFlag[] remove, int priority)
        {
            // Preserve independent permanent changes and other owners' layers.
            Initialize();
            RemoveState(owner, false);
            ObjectFlagState state = new ObjectFlagState(owner, replace, add, remove, priority);
            int index = 0;
            while (index < states.Count && states[index].Priority <= priority)
                index++;
            states.Insert(index, state);
            RefreshTemporary();
        }

        /// <summary>Withdraws one reversible state without restoring an outdated global snapshot.</summary>
        /// <param name="owner">Component whose layer ends.</param>
        /// <param name="notify">Whether to recompute membership immediately.</param>
        public void RemoveState(UnityEngine.Object owner, bool notify = true)
        {
            Initialize();
            for (int index = states.Count - 1; index >= 0; index--)
                if (states[index].Owner == owner)
                    states.RemoveAt(index);
            if (notify)
                RefreshTemporary();
        }

        /// <summary>Rebuilds effective membership only when a permanent or temporary operation changes.</summary>
        private void RefreshTemporary()
        {
            // Ordinary membership queries read the cached set without allocations or owner searches.
            effective.Clear();
            if (temporary.Count > 0 || states.Count > 0)
            {
                effective.UnionWith(current);
                foreach (ObjectFlagState state in states)
                {
                    if (state.Owner == null)
                        continue;
                    if (state.Replace)
                        effective.Clear();
                    effective.ExceptWith(state.Remove);
                    effective.UnionWith(state.Add);
                }
                foreach ((UnityEngine.Object Owner, ObjectFlag Flag, ObjectFlagOperation Operation) entry in temporary)
                    if (entry.Owner != null)
                        Apply(effective, entry.Flag, entry.Operation);
            }
            if (changeDepth > 0)
                changed = true;
            else
                Changed?.Invoke(this);
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
