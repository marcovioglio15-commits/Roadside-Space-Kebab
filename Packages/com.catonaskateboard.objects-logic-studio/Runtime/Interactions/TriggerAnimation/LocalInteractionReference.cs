using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains a portable binding to one interaction on the same object, without scene or prefab-stage references.</summary>
    [Serializable]
    public sealed class LocalInteractionReference
    {
        #region Fields

        [Tooltip("Whether a source interaction has been selected in the local interaction menu.")]
        public bool Assigned;
        [Tooltip("Type of the selected interaction on this object.")]
        public InteractionTemplateKind Kind;
        [Tooltip("Authored interaction name used to distinguish cards of the same type.")]
        public string Name = string.Empty;
        [Tooltip("Position among cards with this type and name, captured by the selection menu.")]
        [Range(0, 100)]
        public int Occurrence;

        #endregion

        #region Properties

        /// <summary>Whether the descriptor contains a supported kind and a usable local occurrence.</summary>
        public bool IsValid => Assigned && Kind is >= InteractionTemplateKind.Hover and <= InteractionTemplateKind.PresenceCap
            && Name != null && Occurrence >= 0;

        #endregion

        #region Methods

        #region Binding

        /// <summary>Resolves only this object's components when the animation initializes or its draft is validated.</summary>
        /// <param name="owner">Exact object containing the source interaction.</param>
        /// <returns>The selected local component, or null when the binding is missing.</returns>
        public ObjectInteraction Resolve(GameObject owner)
        {
            // Matching names remain stable when unrelated cards are added or reordered.
            if (owner == null || !IsValid)
                return null;
            int occurrence = 0;
            foreach (ObjectInteraction candidate in owner.GetComponents<ObjectInteraction>())
                if (InteractionTemplateKinds.Of(candidate) == Kind && candidate.InteractionName == Name && occurrence++ == Occurrence)
                    return candidate;
            return null;
        }

        /// <summary>Captures a local menu choice as values that survive prefab duplication and preset import.</summary>
        /// <param name="source">Component selected from the same object.</param>
        /// <returns>A detached binding to the selected card.</returns>
        public static LocalInteractionReference Capture(ObjectInteraction source)
        {
            // Unsupported rule components cannot become animation sources.
            if (source == null || InteractionTemplateKinds.Of(source) is not InteractionTemplateKind kind)
                return new LocalInteractionReference();
            LocalInteractionReference reference = new LocalInteractionReference { Assigned = true, Kind = kind, Name = source.InteractionName };
            foreach (ObjectInteraction candidate in source.GetComponents<ObjectInteraction>())
            {
                if (candidate == source)
                    break;
                if (InteractionTemplateKinds.Of(candidate) == kind && candidate.InteractionName == reference.Name)
                    reference.Occurrence++;
            }
            return reference;
        }

        #endregion

        #endregion
    }
}
