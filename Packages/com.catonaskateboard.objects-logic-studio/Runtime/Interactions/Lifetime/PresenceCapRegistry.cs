using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Counts current identities once per cleanup interval and removes oldest eligible excess objects.</summary>
    internal static class PresenceCapRegistry
    {
        #region State

        private static readonly List<ObjectPresenceCap> active = new List<ObjectPresenceCap>();
        private static readonly List<ObjectPresenceCap> snapshot = new List<ObjectPresenceCap>();
        private static readonly Dictionary<ObjectFlag, int> counts = new Dictionary<ObjectFlag, int>();
        private static readonly Plane[] planes = new Plane[6];
        private static float nextQuery;

        #endregion
        #region Methods
        #region Membership

        /// <summary>Clears transient lists before scene activation in each Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            active.Clear();
            snapshot.Clear();
            counts.Clear();
            nextQuery = 0f;
        }

        /// <summary>Restores membership when Play mode retains enabled scene components.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            foreach (ObjectPresenceCap feature in Object.FindObjectsByType<ObjectPresenceCap>())
                if (feature.isActiveAndEnabled && feature.TryValidate(out _))
                    Register(feature);
        }

        /// <summary>Retains activation order for deterministic oldest-first cleanup.</summary>
        /// <param name="feature">Object becoming eligible for population management.</param>
        internal static void Register(ObjectPresenceCap feature)
        {
            if (!active.Contains(feature))
                active.Add(feature);
        }

        /// <summary>Removes a candidate on disable or despawn.</summary>
        /// <param name="feature">Object leaving gameplay.</param>
        internal static void Unregister(ObjectPresenceCap feature) => active.Remove(feature);

        /// <summary>Excludes nested ingredient and inventory identities from independent world populations.</summary>
        /// <param name="identity">Active registered identity.</param>
        /// <returns>True when the identity belongs to an independent root.</returns>
        internal static bool Independent(ObjectIdentity identity)
        {
            return identity != null && identity.isActiveAndEnabled
                && (identity.transform.parent == null || identity.transform.parent.GetComponentInParent<ObjectItem>() == null)
                && (!identity.TryGetComponent(out ObjectItem item) || item.isActiveAndEnabled && !item.IsConsumed);
        }

        #endregion
        #region Cleanup

        /// <summary>Counts flags and services all caps at most four times per scaled second.</summary>
        internal static void Tick()
        {
            Camera view = HoverObserver.ActiveView;
            if (Time.timeScale <= 0f || Time.time < nextQuery || view == null || !view.isActiveAndEnabled)
                return;
            nextQuery = Time.time + 0.25f;
            counts.Clear();
            foreach (ObjectIdentity identity in ObjectIdentityRegistry.Items)
                if (Independent(identity))
                    foreach (ObjectFlag flag in identity.ActiveFlags)
                    {
                        counts.TryGetValue(flag, out int count);
                        counts[flag] = count + 1;
                    }
            GeometryUtility.CalculateFrustumPlanes(view, planes);
            snapshot.Clear();
            snapshot.AddRange(active);
            // Despawn callbacks can change the registry; iterate a reused snapshot instead.
            foreach (ObjectPresenceCap feature in snapshot)
            {
                if (feature == null || !feature.isActiveAndEnabled || !feature.Removable(view, planes))
                    continue;
                bool excess = false;
                foreach (ObjectFlag flag in feature.Flags)
                    excess |= flag != null && feature.Identity.Has(flag) && counts.TryGetValue(flag, out int count) && count > feature.Settings.Maximum;
                if (!excess)
                    continue;
                foreach (ObjectFlag flag in feature.Identity.ActiveFlags)
                    if (counts.TryGetValue(flag, out int count))
                        counts[flag] = count - 1;
                ObjectDespawn.Remove(feature.gameObject);
            }
        }

        #endregion
        #endregion
    }
}
