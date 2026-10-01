using CatOnASkateboard.StudioIdentity;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Requires physical contact with a collider belonging to one of the selected project flags.</summary>
    [Serializable]
    public sealed class FlagContactRequirement
    {
        #region Fields

        [Header("Required Contact")]
        [Tooltip("Allow this interaction only while its collider touches an object with one of the selected flags.")]
        public bool Enabled;
        [Tooltip("Accepted project flags on the contacted collider or one of its parents.")]
        public ObjectFlag[] Flags = Array.Empty<ObjectFlag>();
        [Tooltip("Require any selected flag or all selected flags on the contacted object or its parents.")]
        public ObjectFlagMatch Match;
        [Tooltip("Maximum surface separation accepted as direct contact, in metres.")]
        public float Tolerance = 0.005f;
        [Tooltip("Allow trigger colliders to satisfy the required contact.")]
        public bool IncludeTriggers;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks selected flags and contact distance without modifying saved values.</summary>
        /// <param name="warning">Receives an incomplete flag selection or invalid tolerance.</param>
        /// <returns>True when disabled or configured with distinct flag names and finite tolerance.</returns>
        public bool TryValidate(out string warning)
        {
            // Native flag existence is checked against the destination object before activation.
            warning = string.Empty;
            if (!Enabled)
                return true;
            warning = "Select at least one distinct contact flag and a finite non-negative tolerance.";
            if (Flags == null || Flags.Length == 0 || !float.IsFinite(Tolerance) || Tolerance < 0f)
                return false;
            if (Match is not (ObjectFlagMatch.Any or ObjectFlagMatch.All))
                return false;
            return ObjectFlagRules.TryValidate(Flags, false, out warning);
        }

        #endregion

        #endregion
    }

    /// <summary>Checks exact flagged contact at an interaction boundary using a reusable physics buffer.</summary>
    internal sealed class FlagContactQuery
    {
        #region State

        private readonly Collider[] buffer = new Collider[128];

        #endregion

        #region Methods

        #region Contact

        /// <summary>Accepts one matching surface without requiring a Rigidbody or Object Item on the counterpart.</summary>
        /// <param name="item">Interaction owner excluded from counterpart results.</param>
        /// <param name="owned">Colliders captured when the owner was activated.</param>
        /// <param name="settings">Validated flag and distance requirement.</param>
        /// <returns>True when disabled or touching an eligible flagged surface.</returns>
        internal bool Allows(ObjectItem item, Collider[] owned, FlagContactRequirement settings)
        {
            // Queries run only for a requested interaction, after scripted transforms have settled.
            if (!settings.Enabled)
                return true;
            return Query(item, owned, settings.Tolerance, settings.IncludeTriggers, null, settings);
        }

        /// <summary>Collects each touching collider for commands that must operate on every matching physical body.</summary>
        /// <param name="item">Owner excluded from contacts.</param>
        /// <param name="owned">Cached owner colliders.</param>
        /// <param name="tolerance">Maximum allowed surface separation.</param>
        /// <param name="includeTriggers">Whether trigger geometry participates.</param>
        /// <param name="contacts">Reusable destination receiving distinct colliders.</param>
        /// <returns>True when the complete query fit its buffer; false cancels an incomplete result.</returns>
        internal bool Collect(ObjectItem item, Collider[] owned, float tolerance, bool includeTriggers, HashSet<Collider> contacts)
        {
            // Shared geometry checks keep Eject and contact-gated Slice consistent.
            contacts.Clear();
            return Query(item, owned, tolerance, includeTriggers, contacts, null);
        }

        /// <summary>Runs one exact contact implementation for first-match checks and complete collections.</summary>
        /// <param name="item">Excluded interaction owner.</param>
        /// <param name="owned">Cached collider hierarchy.</param>
        /// <param name="tolerance">Permitted surface gap.</param>
        /// <param name="includeTriggers">Trigger participation policy.</param>
        /// <param name="contacts">Collection destination, or null for a first-match query.</param>
        /// <param name="filter">Optional flag filter for first-match queries.</param>
        /// <returns>Whether collection succeeded, or a requested first match was found.</returns>
        private bool Query(ObjectItem item, Collider[] owned, float tolerance, bool includeTriggers,
            HashSet<Collider> contacts, FlagContactRequirement filter)
        {
            // Full buffers fail closed, preventing an arbitrary subset of touching objects from being ejected.
            Physics.SyncTransforms();
            foreach (Collider collider in owned)
            {
                if (!ContactDetection.Usable(collider, includeTriggers) || !item.Owns(collider.transform))
                    continue;
                Bounds bounds = collider.bounds;
                int count = item.gameObject.scene.GetPhysicsScene().OverlapSphere(bounds.center,
                    bounds.extents.magnitude + tolerance, buffer, ~0,
                    includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);
                if (count == buffer.Length)
                {
                    contacts?.Clear();
                    return false;
                }
                for (int index = 0; index < count; index++)
                {
                    Collider other = buffer[index];
                    if (!ContactDetection.Usable(other, includeTriggers) || item.Owns(other.transform)
                        || Physics.GetIgnoreLayerCollision(collider.gameObject.layer, other.gameObject.layer)
                        || Physics.GetIgnoreCollision(collider, other)
                        || filter != null && !Matches(other.transform, filter.Flags, filter.Match))
                        continue;
                    if (!ContactDetection.Touches(collider, other, tolerance))
                        continue;
                    if (contacts == null)
                        return true;
                    contacts.Add(other);
                }
            }
            return contacts != null;
        }

        /// <summary>Checks the collider and its parents so flagged table roots can own compound surfaces.</summary>
        /// <param name="branch">Contacted collider transform.</param>
        /// <param name="flags">Accepted project flags.</param>
        /// <param name="match">Any or all selected flags.</param>
        /// <returns>True when ancestors satisfy the selected flags.</returns>
        internal static bool Matches(Transform branch, ObjectFlag[] flags, ObjectFlagMatch match)
        {
            // Each flag may belong to the collider itself or an owning parent.
            foreach (ObjectFlag flag in flags)
            {
                bool found = ObjectIdentity.FindInParents(branch, flag) != null;
                if (match == ObjectFlagMatch.Any && found)
                    return true;
                if (match == ObjectFlagMatch.All && !found)
                    return false;
            }
            return flags.Length > 0 && match == ObjectFlagMatch.All;
        }

        #endregion

        #endregion
    }
}
