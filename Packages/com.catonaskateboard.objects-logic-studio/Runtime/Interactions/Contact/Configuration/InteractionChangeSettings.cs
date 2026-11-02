using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Lists interaction families accepted by runtime contact edits, including availability rules.</summary>
    public enum InteractionRuntimeKind { Hover, Grab, Drop, Throw, Dispenser, Container, Dialogue, Slice, Contact, Outline, Spawn, AssemblyStation, AssemblyProduct, Ambient, AvailableOrders, TriggerAnimation, Eject, ObjectDegradation, GravityGenerator, ElasticDeformation, DirtTrail, SpraySauce, Deathbox, PresenceCap, Unlock }
    /// <summary>Operations applied atomically when a contact modification completes.</summary>
    public enum InteractionChangeOperation { Add, Remove, ApplyPreset }

    /// <summary>Finds an exact local card without storing a reference to another prefab instance.</summary>
    [Serializable]
    public sealed class RuntimeInteractionReference
    {
        #region Fields

        [Header("Target Interaction")]
        [Tooltip("Whether an existing interaction was selected from the menu.")]
        public bool Assigned;
        [Tooltip("Interaction family used by this selection.")]
        public InteractionRuntimeKind Kind;
        [Tooltip("Remove every card of this type. A missing type has no effect.")]
        public bool AllOfType;
        [Tooltip("Selected card name. Set by the interaction menu.")]
        [HideInInspector]
        public string Name = string.Empty;
        [Tooltip("Occurrence among cards with this kind and name. Set by the interaction menu.")]
        [HideInInspector]
        public int Occurrence;

        #endregion

        #region Methods

        #region Binding

        /// <summary>Resolves this portable menu selection at a transaction boundary.</summary>
        /// <param name="root">Participant receiving the modification.</param>
        /// <returns>The selected card, or null when it is missing.</returns>
        internal ObjectInteraction Resolve(GameObject root)
        {
            if (!Assigned)
                return null;
            int occurrence = 0;
            foreach (ObjectInteraction feature in root.GetComponents<ObjectInteraction>())
                if (InteractionRuntimeCatalog.KindOf(feature) == Kind && feature.InteractionName == Name && occurrence++ == Occurrence)
                    return InteractionRuntimeState.IsRemoved(feature) ? null : feature;
            return null;
        }

        #endregion

        #endregion
    }

    /// <summary>Defines one preset-based addition, removal or import for a contact participant.</summary>
    [Serializable]
    public sealed class InteractionChangeSettings
    {
        #region Fields

        [Header("Interaction Change")]
        [Tooltip("Add a card from a preset, remove a card, or import a preset into an existing card.")]
        public InteractionChangeOperation Operation;
        [Tooltip("Interaction family to create. Used only by Add.")]
        public InteractionRuntimeKind AddKind;
        [Tooltip("Name assigned to a newly added card.")]
        public string Name = "Interaction";
        [Tooltip("Existing card selected from the affected object's interaction menu.")]
        public RuntimeInteractionReference Target = new RuntimeInteractionReference();
#if UNITY_EDITOR
        [Tooltip("Settings-group presets applied in order. The same assets are used by the interaction's group windows.")]
        public ScriptableObject[] GroupPresets = Array.Empty<ScriptableObject>();
        [Tooltip("Whether the prepared groups require a start Button for an added interaction.")]
        [HideInInspector]
        public bool RequiresAction = true;
        [Tooltip("Last group compilation issue. Runtime data remains unchanged until the selection is valid.")]
        [HideInInspector]
        public string GroupWarning = string.Empty;
        [Tooltip("Interaction family used by the last group compilation.")]
        [HideInInspector]
        public InteractionRuntimeKind CompiledKind;
#endif
        [Tooltip("Prepared group values used by runtime contact imports. Updated in editor from the selected groups.")]
        [HideInInspector]
        public InteractionGroupPatch[] Groups = Array.Empty<InteractionGroupPatch>();
        [Tooltip("Existing Button identifier for newly added input interactions. Existing bindings are preserved during preset import.")]
        public UnityEngine.InputSystem.InputActionReference Action;
        [Tooltip("Page advance Button for a newly added Dialogue.")]
        public UnityEngine.InputSystem.InputActionReference AdvanceAction;

        #endregion

        #region Properties

        /// <summary>Interaction family accepting the selected preset.</summary>
        public InteractionRuntimeKind Kind => Operation == InteractionChangeOperation.Add ? AddKind : Target.Kind;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks reusable edit blocks without resolving a particular participant.</summary>
        /// <param name="warning">Receives a missing target or preset.</param>
        /// <returns>True when the operation has complete reusable data.</returns>
        internal bool TryValidate(out string warning)
        {
            warning = "Choose a supported interaction operation and target.";
            if (Operation is not (InteractionChangeOperation.Add or InteractionChangeOperation.Remove or InteractionChangeOperation.ApplyPreset)
                || Target == null || Kind is < InteractionRuntimeKind.Hover or > InteractionRuntimeKind.Unlock
                || Operation != InteractionChangeOperation.Add && !Target.Assigned)
                return false;
            if (Operation == InteractionChangeOperation.ApplyPreset && Target.AllOfType)
            {
                warning = "Choose an exact interaction for Apply Preset.";
                return false;
            }
            warning = "Assign matching settings-group presets.";
            if (Operation != InteractionChangeOperation.Remove
                && (Groups == null || Groups.Length == 0 || Array.Exists(Groups, group => group == null || !group.Accepts(Kind))))
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
