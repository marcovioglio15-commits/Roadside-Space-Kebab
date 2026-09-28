using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity
{
    /// <summary>Finds active identities through a registration set instead of repeated scene scans.</summary>
    public static class ObjectIdentityRegistry
    {
        #region State

        private static readonly HashSet<ObjectIdentity> active = new HashSet<ObjectIdentity>();

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Rebuilds scene identities once after entering Play, including retained scene objects.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            // Components reset their membership lazily before their first query in this session.
            active.Clear();
            foreach (ObjectIdentity identity in Object.FindObjectsByType<ObjectIdentity>(FindObjectsInactive.Include))
                if (identity.isActiveAndEnabled)
                    active.Add(identity);
        }

        /// <summary>Adds an enabled identity to active lookup.</summary>
        /// <param name="identity">Component becoming available.</param>
        internal static void Register(ObjectIdentity identity)
        {
            // Hash-set insertion tolerates repeated activation callbacks.
            active.Add(identity);
        }

        /// <summary>Removes a disabled or destroyed identity from active lookup.</summary>
        /// <param name="identity">Component leaving the active scene set.</param>
        internal static void Unregister(ObjectIdentity identity)
        {
            // Membership on the component itself remains unchanged.
            active.Remove(identity);
        }

        #endregion

        #region Queries

        /// <summary>Resolves an unambiguous active object when no matching camera ancestor exists.</summary>
        /// <param name="flag">Required player or object flag.</param>
        /// <returns>The sole active matching identity, or null for zero or multiple matches.</returns>
        public static ObjectIdentity FindUnique(ObjectFlag flag)
        {
            // A cached registry avoids allocating scene-query arrays during observer recovery.
            ObjectIdentity selected = null;
            foreach (ObjectIdentity identity in active)
                if (identity != null && identity.isActiveAndEnabled && identity.Has(flag))
                {
                    if (selected != null)
                        return null;
                    selected = identity;
                }
            return selected;
        }

        #endregion

        #endregion
    }
}
