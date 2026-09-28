using CatOnASkateboard.StudioIdentity;
using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Optionally changes an item's project flag at a successful interaction boundary.</summary>
    [Serializable]
    public sealed class InteractionFlagChange
    {
        #region Fields

        [Header("Flag Change")]
        [Tooltip("Change the owning item's flag when this interaction reaches the selected lifecycle boundary.")]
        public bool Enabled;
        [Tooltip("Project flag assigned to the item root, or the interaction object when it has no Object Item.")]
        public ObjectFlag Flag;
        [Tooltip("Replace, add, remove or toggle a flag, or clear all flags on this item.")]
        public ObjectFlagOperation Operation;
        [Tooltip("Apply at successful start or after committed completion. Cancellation never counts as completion.")]
        public InteractionMoment Moment = InteractionMoment.Completed;

        #endregion

        #region State

        [NonSerialized]
        private bool reported;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks the configured flag against stable asset references without changing it.</summary>
        /// <param name="owner">Object used to validate the flag through its authored identity.</param>
        /// <param name="warning">Receives an undefined flag or unsupported event.</param>
        /// <returns>True when the optional change can be applied.</returns>
        public bool TryValidate(GameObject owner, out string warning)
        {
            // Disabled changes require no flag lookup.
            warning = string.Empty;
            if (!Enabled)
                return true;
            if (Moment is not (InteractionMoment.Started or InteractionMoment.Completed))
            {
                warning = "Choose an existing project flag and a valid flag-change event.";
                return false;
            }
            return TryValidateFlag(owner, Flag, out warning, Operation);
        }

        /// <summary>Validates a project flag shared by owner changes and contact-counterpart changes.</summary>
        /// <param name="owner">Object used for membership validation.</param>
        /// <param name="flag">Proposed project flag.</param>
        /// <param name="warning">Receives a missing owner or undefined flag.</param>
        /// <param name="operation">Requested membership change; Clear does not need a flag.</param>
        /// <returns>True when the flag can be assigned without a runtime exception.</returns>
        internal static bool TryValidateFlag(GameObject owner, ObjectFlag flag, out string warning, ObjectFlagOperation operation = ObjectFlagOperation.Replace)
        {
            // References and authored components are checked without string lookup or runtime creation.
            warning = "Assign an Object Identity and a valid flag operation to the target.";
            if (owner == null || owner.GetComponent<ObjectIdentity>() == null || !ObjectFlagRules.IsValidOperation(operation))
                return false;
            if (operation != ObjectFlagOperation.Clear && (flag == null || !flag.TryValidate(out warning)))
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion

        #region Application

        /// <summary>Applies a flag change once per matching successful event.</summary>
        /// <param name="owner">Owning item root receiving the flag.</param>
        /// <param name="moment">Lifecycle boundary reached by the interaction.</param>
        internal void Apply(GameObject owner, InteractionMoment moment)
        {
            // A missing flag asset produces one warning instead of interrupting gameplay callbacks.
            if (!Enabled || moment != Moment)
                return;
            if (!TryApplyFlag(owner, Flag, out string warning, Operation))
            {
                if (!reported)
                    Debug.LogWarning(warning, owner);
                reported = true;
                return;
            }
            reported = false;
        }

        /// <summary>Assigns a validated flag without changing any other property on the target.</summary>
        /// <param name="owner">Item root receiving the flag.</param>
        /// <param name="flag">New flag from the project catalog.</param>
        /// <param name="warning">Receives an invalid target or flag.</param>
        /// <param name="operation">Membership operation applied at this boundary.</param>
        /// <returns>True when the target already has the flag or it was assigned successfully.</returns>
        internal static bool TryApplyFlag(GameObject owner, ObjectFlag flag, out string warning, ObjectFlagOperation operation = ObjectFlagOperation.Replace)
        {
            // Mutations preserve the serialized defaults for the next Play session.
            return TryValidateFlag(owner, flag, out warning, operation)
                && owner.GetComponent<ObjectIdentity>().Change(flag, operation);
        }

        #endregion

        #endregion
    }
}
