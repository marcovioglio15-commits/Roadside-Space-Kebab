using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Defines the allowed quantity of one ingredient tag in an assembly recipe.</summary>
    [Serializable]
    public sealed class AssemblyIngredient : ItemTagRequirement
    {
        #region Fields

        [Tooltip("Allow this ingredient to be omitted when completing the recipe. Quantity still limits how many may be added.")]
        public bool Optional;

        #endregion
    }

    /// <summary>Defines a stable placement slot relative to the assembled product root.</summary>
    [Serializable]
    public sealed class AssemblyMagnet
    {
        #region Fields

        [Header("Magnet")]
        [Tooltip("Unique slot name used in the preview and for the inserted ingredient child. Mesh-effect paths can use this stable name.")]
        public string Name = "Magnet";
        [Tooltip("Accept any ingredient allowed by the recipe. Specific tag slots take priority over generic slots.")]
        public bool AnyIngredient = true;
        [Tooltip("Ingredient tag accepted by this specific slot.")]
        public string Tag = "Untagged";
        [Tooltip("Ingredient root position relative to the product root.")]
        public Vector3 Position;
        [Tooltip("Ingredient root rotation in degrees relative to the product root.")]
        public Vector3 Rotation;
        [Tooltip("Positive local scale multiplier applied to the ingredient's existing scale.")]
        public Vector3 Scale = Vector3.one;
#if UNITY_EDITOR
        [Tooltip("Optional ingredient prefab displayed only as a visual guide in the assembly preview. It is never spawned by gameplay assembly.")]
        public GameObject PreviewPrefab;
#endif

        #endregion
    }

    /// <summary>Releases a product feature after its ingredient and completion requirements are satisfied.</summary>
    [Serializable]
    public sealed class AssemblyInteractionRule
    {
        #region Fields

        [Header("Product Interaction")]
        [Tooltip("Existing interaction on the product prefab. References are remapped to the spawned product automatically.")]
        public ObjectInteraction Target;
        [Tooltip("Wait until every mandatory recipe ingredient has been supplied.")]
        public bool RequireComplete = true;
        [Tooltip("Minimum total number of ingredients before this interaction becomes available.")]
        public int MinimumIngredients = 1;
        [Tooltip("Additional required ingredient tags and positive quantities. Every listed requirement must be met.")]
        public ItemTagRequirement[] Ingredients = Array.Empty<ItemTagRequirement>();

        #endregion
    }

    /// <summary>Stores a product's recipe, placement slots and existing-feature unlock requirements.</summary>
    [Serializable]
    public sealed class AssemblyProductSettings
    {
        #region Fields

        [Header("Recipe")]
        [Tooltip("Distinct ingredient tags and quantities. Mandatory entries must be filled; optional entries can be omitted.")]
        public AssemblyIngredient[] Ingredients = Array.Empty<AssemblyIngredient>();
        [Tooltip("Placement slots in product-local space. Provide enough compatible slots for the complete recipe, including optional quantities.")]
        public AssemblyMagnet[] Magnets = Array.Empty<AssemblyMagnet>();
        [Tooltip("Existing product interactions with custom availability requirements. Unlisted interactions wait for recipe completion.")]
        public AssemblyInteractionRule[] InteractionRules = Array.Empty<AssemblyInteractionRule>();

        #endregion
    }
}
