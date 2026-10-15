using System;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Limits concurrent objects by their own identity or an explicitly selected flag group.</summary>
    [Serializable]
    public sealed class PresenceCapSettings
    {
        #region Fields

        [Header("Presence Cap")]
        [Tooltip("Maximum active independent objects sharing each selected identity flag. Offscreen excess objects are removed first.")]
        public int Maximum = 20;
        [Tooltip("Use this object's current identity flags. Disable to choose which flags define its population limit.")]
        public bool UseOwnFlags = true;
        [Tooltip("Allow removal of objects marked Static, or objects with no Rigidbody and no other interaction. Disabled by default to protect fixed scene geometry.")]
        public bool IncludeFixedObjects;
        [Tooltip("Flags counted independently when Use Own Flags is disabled. Exceeding any selected flag's cap makes this object eligible.")]
        public ObjectFlag[] Flags = Array.Empty<ObjectFlag>();
        [Tooltip("Seconds continuously outside the gameplay camera frame before an excess object can despawn. Carried, reserved and player objects remain protected.")]
        [Range(0f, 10f)]
        public float OffscreenDelay = 0.25f;

        #endregion
        #region Methods
        #region Validation

        /// <summary>Rejects unusable limits without rewriting configured values.</summary>
        /// <param name="warning">Receives invalid limits or flags.</param>
        /// <returns>True for a positive cap and finite nonnegative delay.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Presence Cap needs a positive maximum and finite nonnegative offscreen delay.";
            if (Maximum <= 0 || !float.IsFinite(OffscreenDelay) || OffscreenDelay < 0f)
                return false;
            warning = string.Empty;
            return UseOwnFlags || ObjectFlagRules.TryValidate(Flags, false, out warning);
        }

        #endregion
        #endregion
    }
}
