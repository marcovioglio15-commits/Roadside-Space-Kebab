using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Optionally changes an item's project tag at a successful interaction boundary.</summary>
    [Serializable]
    public sealed class InteractionTagChange
    {
        #region Fields

        [Header("Tag Change")]
        [Tooltip("Change the owning item's tag when this interaction reaches the selected lifecycle boundary.")]
        public bool Enabled;
        [Tooltip("Project tag assigned to the item root, or the interaction object when it has no Object Item.")]
        public string Tag = "Untagged";
        [Tooltip("Apply at successful start or after committed completion. Cancellation never counts as completion.")]
        public InteractionMoment Moment = InteractionMoment.Completed;

        #endregion

        #region State

        [NonSerialized]
        private string reportedTag;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks the configured tag against Unity's project catalog without changing it.</summary>
        /// <param name="owner">Object used to validate the tag through Unity's native API.</param>
        /// <param name="warning">Receives an undefined tag or unsupported event.</param>
        /// <returns>True when the optional change can be applied.</returns>
        public bool TryValidate(GameObject owner, out string warning)
        {
            // Disabled changes require no tag lookup.
            warning = string.Empty;
            if (!Enabled)
                return true;
            if (owner == null || string.IsNullOrWhiteSpace(Tag) || Moment is not (InteractionMoment.Started or InteractionMoment.Completed))
                warning = "Choose an existing project tag and a valid tag-change event.";
            else
                try
                {
                    owner.CompareTag(Tag);
                }
                catch (UnityException)
                {
                    warning = "The interaction's replacement tag is not defined in this project.";
                }
            return warning.Length == 0;
        }

        #endregion

        #region Application

        /// <summary>Applies a tag change once per matching successful event.</summary>
        /// <param name="owner">Owning item root receiving the tag.</param>
        /// <param name="moment">Lifecycle boundary reached by the interaction.</param>
        internal void Apply(GameObject owner, InteractionMoment moment)
        {
            // A removed project tag produces one warning instead of interrupting gameplay callbacks.
            if (!Enabled || moment != Moment)
                return;
            if (!TryValidate(owner, out string warning))
            {
                if (reportedTag != Tag)
                    Debug.LogWarning(warning, owner);
                reportedTag = Tag;
                return;
            }
            reportedTag = null;
            if (!owner.CompareTag(Tag))
                owner.tag = Tag;
        }

        #endregion

        #endregion
    }
}
