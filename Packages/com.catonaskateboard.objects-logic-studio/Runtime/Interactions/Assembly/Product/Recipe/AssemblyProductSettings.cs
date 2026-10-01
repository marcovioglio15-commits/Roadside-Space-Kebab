using CatOnASkateboard.StudioIdentity;
using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Defines a shared quantity for a group of alternative ingredient flags.</summary>
    [Serializable]
    public sealed class AssemblyIngredient : ItemFlagRequirement
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
        [Tooltip("Accept any ingredient allowed by the recipe. Specific flag slots take priority over generic slots.")]
        public bool AnyIngredient = true;
        [Tooltip("Alternative recipe flags accepted by this slot. Any selected flag qualifies an ingredient.")]
        public ObjectFlag[] Flags = Array.Empty<ObjectFlag>();
        [Tooltip("Required physical insertion number, starting at 1. Zero leaves this magnet unordered. Numbered magnets are required for completion.")]
        public int Order;
        [Tooltip("Ingredient root position relative to the product root.")]
        public Vector3 Position;
        [Tooltip("Ingredient root rotation in degrees relative to the product root.")]
        public Vector3 Rotation;
        [Tooltip("Positive local scale multiplier applied to the ingredient's existing scale.")]
        public Vector3 Scale = Vector3.one;
        [Tooltip("Mesh and material replacements applied to the actual ingredient on insertion and restored when it detaches.")]
        public ItemAppearanceSettings Appearance = new ItemAppearanceSettings();
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
        [Tooltip("Minimum total ingredient units before this interaction becomes available. Uses the Grab value of each inserted object.")]
        public int MinimumIngredients = 1;
        [Tooltip("Additional required ingredient flags and positive quantities. Every listed requirement must be met.")]
        public ItemFlagRequirement[] Ingredients = Array.Empty<ItemFlagRequirement>();

        #endregion
    }

    /// <summary>Stores a product's recipe, placement slots and existing-feature unlock requirements.</summary>
    [Serializable]
    public sealed class AssemblyProductSettings
    {
        #region Fields

        [Header("Recipe")]
        [Tooltip("Groups of alternative ingredient flags sharing one quantity per row. A flag belongs to only one row. Mandatory rows must be filled; optional rows can be omitted.")]
        public AssemblyIngredient[] Ingredients = Array.Empty<AssemblyIngredient>();
        [Tooltip("Placement slots in product-local space. Each physical object occupies one slot regardless of its Grab units; provide enough slots for the intended ingredient prefabs.")]
        public AssemblyMagnet[] Magnets = Array.Empty<AssemblyMagnet>();
        [Tooltip("Existing product interactions with custom availability requirements. Unlisted interactions wait for recipe completion.")]
        public AssemblyInteractionRule[] InteractionRules = Array.Empty<AssemblyInteractionRule>();
        [Tooltip("Mesh and material changes on the product's existing hierarchy when the recipe completes. Losing completion restores its previous appearance.")]
        public ItemAppearanceSettings CompletedAppearance = new ItemAppearanceSettings();
        [Tooltip("Hide the inserted ingredients' meshes while a completed mesh replacement is active. Geometry returns when the recipe becomes incomplete or an ingredient detaches; collisions and VFX stay active.")]
        public bool HideIngredients = true;

        #endregion

        #region Properties

        /// <summary>Whether completed product meshes replace the assembled ingredients' visible geometry.</summary>
        public bool ReplacesIngredients => HideIngredients && CompletedAppearance?.Meshes is { Length: > 0 };

        #endregion
    }
}
