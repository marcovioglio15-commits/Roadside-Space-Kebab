using CatOnASkateboard.StudioIdentity;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

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
        [Tooltip("Wait for mandatory ingredients and the configured finalization input or prolonged assembly to complete.")]
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
        [Tooltip("Give this assembled product a base sale price when a consuming Modify By Contact collects prices.")]
        public bool PriceEnabled;
        [Tooltip("Base price per consumed logical product unit. A fulfilled order's price override has priority.")]
        [Range(0f, 10000f)]
        public float Price = 5f;
        [Tooltip("Keep the product incomplete and allow compatible recipe extensions on the same table until the held assembly action finishes, then commit completed appearance, flags and product interactions.")]
        public bool ProlongedAssembly;
        [Tooltip("Independent assembly input, charging behavior and HUD appearance. Used only by Prolonged Assembly.")]
        public AssemblyChargeSettings Charge = new AssemblyChargeSettings();
        [Tooltip("Keep the first ingredient inside the product. With Prolonged Assembly, defer this lock until successful completion. Otherwise disable to allow retrieval while it is the sole ingredient and the product is not finalized.")]
        public bool LockFirstIngredient = true;
        [Tooltip("Apply completed appearance and release product interaction gates only when the player first requests Grab with empty hands after mandatory ingredients are present.")]
        public bool FinalizeOnGrab = true;
        [Tooltip("Grab button used to finalize a product without its own Grab interaction. Products with Grab reuse its input and targeting.")]
        public InputActionReference FinalizeAction;
        [Tooltip("Targeting for finalization when the product has no Grab interaction.")]
        public TransferTargetSettings FinalizeTarget = new TransferTargetSettings();
        [Tooltip("Snap this completed product to the next recipe's magnet while keeping its interactions usable. Another compatible ingredient starts assembly; Grab releases the snap. Explicit input insertion remains immediate.")]
        public bool WaitForNextIngredient;
        [Tooltip("Groups of alternative ingredient flags sharing one quantity per row. A flag belongs to only one row. Mandatory rows must be filled; optional rows can be omitted.")]
        public AssemblyIngredient[] Ingredients = Array.Empty<AssemblyIngredient>();
        [Tooltip("Placement slots in product-local space. Each object occupies one slot regardless of its Grab units. On completion, only colliders authored on the product prefab remain active.")]
        public AssemblyMagnet[] Magnets = Array.Empty<AssemblyMagnet>();
        [Tooltip("Existing product interactions with custom availability requirements. Unlisted interactions wait for finalization; rules without Require Complete can allow partial use.")]
        public AssemblyInteractionRule[] InteractionRules = Array.Empty<AssemblyInteractionRule>();
        [Tooltip("Mesh and material changes on the product's existing hierarchy when the recipe completes. Losing completion restores its previous appearance.")]
        public ItemAppearanceSettings CompletedAppearance = new ItemAppearanceSettings();
        [Tooltip("Hide ingredient meshes while a completed mesh replacement is active. They return when the recipe becomes incomplete or an ingredient detaches. Product colliders and ingredient VFX remain active.")]
        public bool HideIngredients = true;

        #endregion

        #region Properties

        /// <summary>Whether completed product meshes replace the assembled ingredients' visible geometry.</summary>
        public bool ReplacesIngredients => HideIngredients && CompletedAppearance?.Meshes is { Length: > 0 };

        #endregion
    }
}
