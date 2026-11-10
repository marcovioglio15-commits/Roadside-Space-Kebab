using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Finds touching item roots, including kinematic carried objects that produce no collision callbacks.</summary>
    internal sealed class ContactDetection
    {
        #region State

        private readonly Collider[] buffer = new Collider[128];
        private Collider[] owned;
        private static int synchronizedFrame = -1;

        #endregion

        #region Methods

        #region Binding

        /// <summary>Captures colliders at activation or an explicit geometry refresh.</summary>
        /// <param name="item">Item whose owned collider hierarchy is queried.</param>
        internal void Bind(ObjectItem item)
        {
            // A collider replacement on a prefab is discovered on its next instance activation.
            List<Collider> colliders = new List<Collider>();
            foreach (Collider collider in item.GetComponentsInChildren<Collider>(true))
                if (collider.GetComponentInParent<ObjectItem>() == item)
                    colliders.Add(collider);
            owned = colliders.ToArray();
        }

        #endregion

        #region Queries

        /// <summary>Collects physical item contacts before the modifier evaluates identity eligibility.</summary>
        /// <param name="item">Owner excluded from results.</param>
        /// <param name="settings">Distance tolerance and trigger policy.</param>
        /// <param name="contacts">Reusable destination cleared before the query.</param>
        internal void Query(ObjectItem item, ContactModificationRule settings, HashSet<ObjectItem> contacts)
        {
            // Keep a retained counterpart detectable after a completed effect changes its identity.
            Query(item, settings.ContactTolerance, settings.IncludeTriggers, contacts, filters: settings);
        }

        /// <summary>Collects physical item roots in the owner's physics scene for caller-specific filtering.</summary>
        /// <param name="item">Owner excluded from the results.</param>
        /// <param name="tolerance">Allowed surface separation.</param>
        /// <param name="includeTriggers">Whether trigger surfaces participate.</param>
        /// <param name="contacts">Reusable result set.</param>
        /// <param name="extensions">Temporary surfaces that contribute contacts for this owner.</param>
        /// <param name="filters">Optional rules for excluding bodies controlled by other interactions.</param>
        internal void Query(ObjectItem item, float tolerance, bool includeTriggers, HashSet<ObjectItem> contacts,
            IReadOnlyList<Collider> extensions = null, ContactModificationRule filters = null)
        {
            // Multiple passive features share one scripted-transform synchronization per render frame.
            contacts.Clear();
            if (synchronizedFrame != Time.frameCount)
            {
                Physics.SyncTransforms();
                synchronizedFrame = Time.frameCount;
            }
            PhysicsScene scene = item.gameObject.scene.GetPhysicsScene();
            foreach (Collider collider in owned)
                if (!Scan(scene, item, collider, tolerance, includeTriggers, contacts, filters))
                {
                    contacts.Clear();
                    return;
                }
            if (extensions != null)
                for (int index = 0; index < extensions.Count; index++)
                    if (!Scan(scene, item, extensions[index], tolerance, includeTriggers, contacts, filters))
                    {
                        contacts.Clear();
                        return;
                    }
        }

        /// <summary>Collects actual contacts against one station or ingredient surface.</summary>
        /// <param name="scene">Physics scene containing both shapes.</param>
        /// <param name="item">Primary owner excluded from the results.</param>
        /// <param name="surface">Shape temporarily contributing to the owner.</param>
        /// <param name="tolerance">Maximum separation in metres.</param>
        /// <param name="includeTriggers">Whether trigger shapes participate.</param>
        /// <param name="contacts">Unique candidates found so far.</param>
        /// <param name="filters">Optional rules for excluding bodies controlled by other interactions.</param>
        /// <returns>False if the overlap buffer filled and results cannot be trusted.</returns>
        private bool Scan(PhysicsScene scene, ObjectItem item, Collider surface, float tolerance, bool includeTriggers,
            HashSet<ObjectItem> contacts, ContactModificationRule filters)
        {
            // Skip excluded source shapes before querying the scene.
            if (!Usable(surface, includeTriggers) || filters != null && ExcludesMotion(surface, item, filters))
                return true;
            Bounds bounds = surface.bounds;
            int count = scene.OverlapSphere(bounds.center, bounds.extents.magnitude + tolerance, buffer, ~0,
                includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);
            if (count == buffer.Length)
                return false;

            // A compound ingredient contributes only one contact, even when several shapes touch.
            int layer = surface.gameObject.layer;
            for (int index = 0; index < count; index++)
            {
                Collider other = buffer[index];
                if (other == surface || !Usable(other, includeTriggers)
                    || other.GetComponentInParent<ObjectItem>() is not ObjectItem candidate
                    || candidate == item || !candidate.isActiveAndEnabled || candidate.IsConsumed || contacts.Contains(candidate)
                    || Physics.GetIgnoreLayerCollision(layer, other.gameObject.layer) || Physics.GetIgnoreCollision(surface, other))
                    continue;
                if ((filters == null || !ExcludesMotion(other, candidate, filters)) && Touches(surface, other, tolerance))
                    contacts.Add(candidate);
            }
            return true;
        }

        /// <summary>Applies the same motion filters to measured contact and collision callbacks.</summary>
        /// <param name="shape">Collider on either side of the contact.</param>
        /// <param name="item">Item owning this contact shape and its release history.</param>
        /// <param name="settings">Independent exclusions and the Gravity Generator release grace.</param>
        /// <returns>True when an enabled filter excludes the current physical owner.</returns>
        internal static bool ExcludesMotion(Collider shape, ObjectItem item, ContactModificationRule settings)
        {
            // Skip body lookups for unfiltered rules. The release exception applies only to gravity.
            if (!settings.IgnoreBlackHole && !settings.IgnoreGravityGenerator)
                return false;
            Rigidbody body = shape.attachedRigidbody;
            return body != null && (settings.IgnoreBlackHole && BlackHoleRun.Contains(body)
                || settings.IgnoreGravityGenerator && !body.isKinematic && GravitySuspension.Contains(body)
                    && (settings.GravityReleaseGrace <= 0f || Time.timeAsDouble - item.LastPlayerReleaseTime >= settings.GravityReleaseGrace));
        }

        /// <summary>Checks collider availability and supported exact-distance geometry.</summary>
        /// <param name="collider">Potential contact shape.</param>
        /// <param name="triggers">Whether trigger shapes are eligible.</param>
        /// <returns>True for active primitive or convex mesh geometry.</returns>
        internal static bool Usable(Collider collider, bool triggers)
        {
            // ClosestPoint supports these shapes and avoids treating a concave mesh's bounds as solid contact.
            return collider != null && collider.enabled && collider.gameObject.activeInHierarchy && (triggers || !collider.isTrigger)
                && collider is BoxCollider or SphereCollider or CapsuleCollider or MeshCollider { convex: true, sharedMesh: not null };
        }

        /// <summary>Measures actual shape proximity instead of accepting broad-phase bounds as contact.</summary>
        /// <param name="first">Owned shape.</param>
        /// <param name="second">Matching participant's shape.</param>
        /// <param name="tolerance">Maximum contact separation in metres.</param>
        /// <returns>True when shapes overlap or their surface points are within tolerance.</returns>
        internal static bool Touches(Collider first, Collider second, float tolerance)
        {
            // Exact penetration handles overlap; closest-point refinement handles solver and carry clearance.
            if (Physics.ComputePenetration(first, first.transform.position, first.transform.rotation,
                second, second.transform.position, second.transform.rotation, out _, out _))
                return true;
            Vector3 point = first.ClosestPoint(second.bounds.center);
            for (int pass = 0; pass < 4; pass++)
            {
                Vector3 other = second.ClosestPoint(point);
                point = first.ClosestPoint(other);
                if ((point - other).sqrMagnitude <= tolerance * tolerance)
                    return true;
            }
            return false;
        }

        #endregion

        #endregion
    }
}
