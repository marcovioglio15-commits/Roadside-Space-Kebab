using CatOnASkateboard.StudioIdentity;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Owns one composite recipe and gates the existing interactions on its product prefab.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-12000)]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Objects Logic Studio/Assembly Product")]
    public sealed class ObjectAssemblyProduct : ObjectExtendedInteraction
    {
        #region Serialized Fields

        [Header("Assembly Product")]
        [Tooltip("Ingredient quantities, placement magnets and requirements for existing product interactions.")]
        [SerializeField]
        private AssemblyProductSettings settings = new AssemblyProductSettings();

        #endregion

        #region State

        private readonly Dictionary<ObjectFlag, int> counts = new Dictionary<ObjectFlag, int>();
        private readonly List<ObjectAssemblyPart> parts = new List<ObjectAssemblyPart>();
        private readonly AssemblyIngredientSelection selection = new AssemblyIngredientSelection();
        private bool recipeValidated;
        private ObjectInteraction[] features;
        private bool[] occupied;
        private Rigidbody body;
        private float baseMass;
        private bool initialized;
        private int initializationSession = -1;
        private static int session;
        private bool destroying;
        private bool pendingStart;
        private bool pendingCompletion;
        private string lastWarning;
        private ItemAppearanceChanges completedAppearance;
        private bool completedAppearanceApplied;

        #endregion

        #region Properties

        /// <summary>Recipe and local placement configuration shared by instances of this prefab.</summary>
        public AssemblyProductSettings Settings => settings;
        /// <summary>Source prefab assigned by the spawning table, used when returning an unfinished product.</summary>
        public GameObject SourcePrefab { get; internal set; }
        /// <summary>Table currently retaining this product, cleared at the pickup boundary.</summary>
        internal ObjectAssemblyStation Table { get; set; }
        /// <summary>Optional visual magnet preview that leaves this completed product independently usable.</summary>
        internal AssemblyIngredientDock Dock { get; set; }
        /// <summary>Number of actual ingredient roots currently attached to the product.</summary>
        public int IngredientCount => parts.Count;
        /// <summary>Total logical units supplied by the attached physical ingredients.</summary>
        public long IngredientUnits { get; private set; }
        /// <summary>Whether all mandatory quantities have been supplied and at least one ingredient is present.</summary>
        public bool IsComplete { get; private set; }
        /// <summary>Recipe counts recorded independently of consumption receipts.</summary>
        public IReadOnlyDictionary<ObjectFlag, int> IngredientCounts => counts;
        /// <summary>Identifies product configuration in the assembly category.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.AssemblyProduct;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Invalidates recipe caches once per Play session, including retained scene objects.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            // Pooling within one session keeps progress; returning to Play rereads the authored recipe.
            session++;
        }

        /// <summary>Initializes locks before any other product feature becomes active.</summary>
        private void OnEnable()
        {
            // Re-enabling a pooled product retains its real ingredient hierarchy and counts.
            Initialize();
            if (initialized)
                RefreshLocks();
        }

        /// <summary>Publishes initial assembly events after all prefab-local unlock rules have subscribed.</summary>
        private void Start()
        {
            // The table normally publishes immediately after activation; scene-authored products use this boundary.
            PublishPending();
        }

        /// <summary>Releases temporary magnet placement before pooling or transferring ingredient ownership.</summary>
        private void OnDisable()
        {
            // Completed recipe progress remains intact when the product is reenabled.
            Dock?.Release();
        }

        /// <summary>Reapplies assembly-owned locks once when entering Play with scene reload disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            // Existing composition remains authoritative; only per-session interaction ownership is refreshed.
            foreach (ObjectAssemblyProduct product in FindObjectsByType<ObjectAssemblyProduct>())
                if (product.isActiveAndEnabled)
                {
                    product.Initialize();
                    if (product.initialized)
                        product.RefreshLocks();
                }
        }

        /// <summary>Prevents child teardown from rebuilding a product that is being destroyed.</summary>
        private void OnDestroy()
        {
            // Release only locks owned by this assembly component if the object itself survives.
            destroying = true;
            foreach (ObjectAssemblyPart part in parts)
                if (part != null)
                    part.SetGeometryHidden(false);
            if (features == null)
                return;
            foreach (ObjectInteraction feature in features)
                if (feature != null)
                    feature.SetLocked(this, false);
        }

        /// <summary>Captures product-owned features once, before any ingredient is parented.</summary>
        internal void Initialize()
        {
            // An inactive product can be prepared under the table's authored staging transform.
            RefreshSession();
            if (initialized)
                return;
            features = GetComponentsInChildren<ObjectInteraction>(true);
            if (!TryValidate(out string warning) || !ItemAppearanceChanges.TryPrepare(Item, settings.CompletedAppearance, out completedAppearance, out warning))
            {
                foreach (ObjectInteraction feature in features)
                    if (feature != this && feature is not ObjectInteractionUnlock)
                        feature.SetLocked(this, true);
                if (lastWarning != warning)
                    Debug.LogWarning(warning, this);
                lastWarning = warning;
                return;
            }
            initialized = true;
            body = GetComponent<Rigidbody>();
            baseMass = body.mass;
            occupied = new bool[settings.Magnets.Length];
            RefreshLocks();
        }

        /// <summary>Discards only progress retained from a previous Play session before accessing recipe arrays.</summary>
        private void RefreshSession()
        {
            // Runtime-created ingredients are removed by Unity on leaving Play, even when scene reload is disabled.
            if (initializationSession == session)
                return;
            initializationSession = session;
            Dock?.Release();
            foreach (ObjectAssemblyPart part in parts)
                if (part != null)
                    part.SetGeometryHidden(false);
            if (completedAppearanceApplied)
                completedAppearance?.Restore();
            completedAppearance = null;
            completedAppearanceApplied = false;
            initialized = destroying = pendingStart = pendingCompletion = IsComplete = false;
            recipeValidated = false;
            counts.Clear();
            IngredientUnits = 0;
            parts.Clear();
            Table = null;
            SourcePrefab = null;
            lastWarning = string.Empty;
        }

        #endregion

        #region Validation

        /// <summary>Checks the recipe and physical root without modifying authored settings.</summary>
        /// <param name="warning">Receives a missing body, invalid recipe or impossible unlock requirement.</param>
        /// <returns>True when this prefab can own assembled ingredients.</returns>
        public override bool TryValidate(out string warning)
        {
            // Nested live bodies are rejected separately by ingredient and carry validation.
            Rigidbody owner = GetComponent<Rigidbody>();
            warning = "Assembly Product needs one non-static Rigidbody root without nested live bodies or joints.";
            return !gameObject.isStatic && owner != null && !ObjectGrab.HasNestedBody(gameObject, owner)
                && GetComponentsInChildren<Joint>(true).Length == 0
                && (transform.parent == null || transform.parent.GetComponentInParent<Rigidbody>(true) == null)
                && AssemblyValidation.TryValidate(gameObject, settings, out warning);
        }

        /// <summary>Finds an available slot without consuming an ingredient or changing its carry state.</summary>
        /// <param name="grab">Currently held object proposed as an ingredient.</param>
        /// <param name="magnet">Receives the compatible empty slot index.</param>
        /// <param name="requireHeld">Whether the ingredient must occupy the player's carry slot.</param>
        /// <returns>True when the complete transfer is eligible.</returns>
        internal bool CanAccept(ObjectGrab grab, out int magnet, bool requireHeld = true)
        {
            // Use the same deterministic selection for preview and committed insertion.
            return TrySelectIngredient(grab, out magnet, out _, requireHeld);
        }

        /// <summary>Assigns exactly one recipe category to an ingredient carrying several flags.</summary>
        /// <param name="grab">Incoming physical ingredient.</param>
        /// <param name="magnet">Receives the compatible slot.</param>
        /// <param name="flag">Receives the single category counted by this recipe.</param>
        /// <param name="requireHeld">Whether the ingredient must be carried.</param>
        /// <returns>True when a compatible insertion is available.</returns>
        private bool TrySelectIngredient(ObjectGrab grab, out int magnet, out ObjectFlag flag, bool requireHeld)
        {
            // Single insertion and deferred starts use the same selection rules.
            magnet = -1;
            flag = null;
            return CanReceive(grab, requireHeld) && selection.TrySelect(settings, counts, initialized ? occupied : null,
                parts.Count + 1, grab, out magnet, out flag);
        }

        /// <summary>Checks shared ownership and recipe requirements before considering an ingredient's magnet.</summary>
        /// <param name="grab">Incoming ingredient.</param>
        /// <param name="requireHeld">Whether insertion requires the player's carry slot.</param>
        /// <returns>True when recipe selection can proceed without bypassing an existing transaction.</returns>
        private bool CanReceive(ObjectGrab grab, bool requireHeld)
        {
            // Refresh retained session state before reading composition or ingredient eligibility.
            RefreshSession();
            return enabled && !IsLocked && ToolAllowed && Item != null && !Item.IsConsumed && !Item.IsReserved && !Item.IsCarried
                && !Item.IsBlocked(InteractionChannels.Assembly) && AssemblyIngredientValidation.Allows(this, grab, requireHeld)
                && ValidateRecipe();
        }

        /// <summary>Tracks a later ordered ingredient while a completed product waits for a companion.</summary>
        /// <param name="grab">Contact candidate for an empty recipe.</param>
        /// <returns>True when ownership and recipe membership allow the candidate to wait.</returns>
        internal bool CanConsider(ObjectGrab grab)
        {
            // A populated product continues through ordinary insertion checks.
            return CanReceive(grab, false) && parts.Count == 0 && AssemblyIngredientSelection.Matches(settings, grab);
        }

        /// <summary>Checks a two-object start without spawning a product or locking either ingredient.</summary>
        /// <param name="first">Completed product waiting at the station.</param>
        /// <param name="second">New compatible ingredient.</param>
        /// <returns>True when both ingredients can occupy the first two magnets in recipe order.</returns>
        internal bool CanAcceptPair(ObjectGrab first, ObjectGrab second)
        {
            // Distinct roots prevent one physical ingredient from filling two slots.
            return PairAllowed(first, second) && selection.TrySelectPair(settings, first, second, out _, out _, out _, out _);
        }

        /// <summary>Validates both independent roots before an empty recipe starts with two ingredients.</summary>
        /// <param name="first">First physical ingredient.</param>
        /// <param name="second">Additional physical ingredient.</param>
        /// <returns>True when neither ingredient overlaps the other's ownership hierarchy.</returns>
        private bool PairAllowed(ObjectGrab first, ObjectGrab second)
        {
            // Candidate eligibility includes consumption, reservations and existing assembly ownership.
            return first != second && CanReceive(first, false) && CanReceive(second, false) && parts.Count == 0
                && !first.transform.IsChildOf(second.transform) && !second.transform.IsChildOf(first.transform);
        }

        /// <summary>Validates the fixed recipe once before runtime contact polling begins.</summary>
        /// <returns>True when the current session has a usable recipe and product root.</returns>
        private bool ValidateRecipe()
        {
            // Failed configuration remains retryable; successful checks need no recurring layout allocations.
            if (!Application.isPlaying)
                return TryValidate(out _);
            return recipeValidated || (recipeValidated = TryValidate(out _));
        }

        #endregion

        #region Assembly

        /// <summary>Frees the table as soon as pickup begins, even if the product is dropped before another input.</summary>
        internal void ReleaseTable()
        {
            // A later ingredient starts a new product unless this one is explicitly returned.
            Dock?.Release();
            if (Table != null)
                Table.Release(this);
            Table = null;
        }

        /// <summary>Attaches exactly one carried ingredient after every recipe and ownership check succeeds.</summary>
        /// <param name="grab">Ingredient currently occupying the observer's carry slot.</param>
        /// <param name="requireHeld">Whether insertion comes from the player carry slot.</param>
        /// <returns>True when the ingredient became part of this product.</returns>
        internal bool Accept(ObjectGrab grab, bool requireHeld = true)
        {
            // No source component is destroyed; the part retains its standalone configuration.
            if (!TrySelectIngredient(grab, out int magnet, out ObjectFlag flag, requireHeld))
                return false;
            Initialize();
            if (!initialized || !ItemAppearanceChanges.TryPrepare(grab.Item, settings.Magnets[magnet].Appearance,
                out ItemAppearanceChanges appearance, out _))
                return false;
            AttachIngredient(grab, magnet, flag, appearance);
            return true;
        }

        /// <summary>Commits both prepared ingredients before the station activates its new product.</summary>
        /// <param name="first">Completed product that has remained independently usable.</param>
        /// <param name="second">Companion that starts the next recipe.</param>
        /// <returns>True when both objects were validated and attached to the inactive product.</returns>
        internal bool AcceptPair(ObjectGrab first, ObjectGrab second)
        {
            // Prepare both appearances before any carry, physics or interaction state is changed.
            if (gameObject.activeInHierarchy || !PairAllowed(first, second)
                || !selection.TrySelectPair(settings, first, second, out int firstMagnet, out ObjectFlag firstFlag,
                    out int secondMagnet, out ObjectFlag secondFlag))
                return false;
            Initialize();
            if (!initialized || !ItemAppearanceChanges.TryPrepare(first.Item, settings.Magnets[firstMagnet].Appearance,
                    out ItemAppearanceChanges firstAppearance, out _)
                || !ItemAppearanceChanges.TryPrepare(second.Item, settings.Magnets[secondMagnet].Appearance,
                    out ItemAppearanceChanges secondAppearance, out _))
                return false;
            AttachIngredient(first, firstMagnet, firstFlag, firstAppearance);
            AttachIngredient(second, secondMagnet, secondFlag, secondAppearance);
            return true;
        }

        /// <summary>Attaches a validated ingredient and refreshes the product's physical and interaction state.</summary>
        /// <param name="grab">Ingredient whose transfer has passed validation.</param>
        /// <param name="magnet">Reserved recipe slot.</param>
        /// <param name="flag">Single recipe category counted for this insertion.</param>
        /// <param name="appearance">Prepared ingredient appearance transaction.</param>
        private void AttachIngredient(ObjectGrab grab, int magnet, ObjectFlag flag, ItemAppearanceChanges appearance)
        {
            // Deferred pairs remain inactive until both insertions have completed.
            bool first = parts.Count == 0;
            bool complete = IsComplete;
            ObjectAssemblyPart part = grab.GetComponent<ObjectAssemblyPart>();
            if (part == null)
                part = grab.gameObject.AddComponent<ObjectAssemblyPart>();
            part.Attach(this, grab, magnet, appearance, flag);
            parts.Add(part);
            foreach (ObjectInteraction feature in features)
                if (feature is ObjectOutline outline)
                    outline.AddPart(part);
            occupied[magnet] = true;
            counts.TryGetValue(part.IngredientFlag, out int count);
            counts[part.IngredientFlag] = count + part.Units;
            IngredientUnits += part.Units;
            RefreshLocks();
            RefreshGeometry();
            pendingStart |= first;
            pendingCompletion |= !complete && IsComplete;
            if (gameObject.activeInHierarchy)
                PublishPending();
        }

        /// <summary>Emits committed ingredient events after the complete product hierarchy has become active.</summary>
        internal void PublishPending()
        {
            // Clear each flag before notifying listeners, which may start another interaction synchronously.
            if (pendingStart)
            {
                pendingStart = false;
                Signal(InteractionMoment.Started);
            }
            if (pendingCompletion)
            {
                pendingCompletion = false;
                Signal(InteractionMoment.Completed);
            }
        }

        /// <summary>Updates quantity, mass and locks after an ingredient is detached or destroyed externally.</summary>
        /// <param name="part">Previously attached ingredient leaving this product.</param>
        internal void Remove(ObjectAssemblyPart part)
        {
            // Partial teardown never restores another ingredient or removes its collision branch.
            if (destroying || !parts.Remove(part))
                return;
            occupied[part.MagnetIndex] = false;
            counts[part.IngredientFlag] -= part.Units;
            IngredientUnits -= part.Units;
            foreach (ObjectInteraction feature in features)
                if (feature is ObjectOutline outline)
                    outline.RemovePart(part);
            RefreshLocks();
            RefreshGeometry();
        }

        /// <summary>Reads a recipe count without exposing mutable progress.</summary>
        /// <param name="flag">Ingredient flag captured at insertion.</param>
        /// <returns>The logical units supplied by attached ingredients bearing that recipe flag.</returns>
        public int Count(ObjectFlag flag)
        {
            // An absent flag never acts as a wildcard.
            return flag != null && counts.TryGetValue(flag, out int count) ? count : 0;
        }

        /// <summary>Updates body mass and geometry caches only when the composition changes.</summary>
        private void RefreshGeometry()
        {
            // Ingredient mass remains part of the root body after its temporary collision shapes are retired.
            float mass = baseMass;
            foreach (ObjectAssemblyPart part in parts)
                mass += part.Mass;
            body.mass = mass;
            body.ResetCenterOfMass();
            body.ResetInertiaTensor();
            foreach (ObjectInteraction feature in features)
                switch (feature)
                {
                    case ObjectHover hover when hover.isActiveAndEnabled:
                        hover.Refresh();
                        break;
                    case ObjectContactModifier contact when contact.isActiveAndEnabled:
                        contact.RefreshGeometry();
                        break;
                    case ObjectGrab grab when grab.isActiveAndEnabled:
                        grab.RefreshCarryGeometry();
                        break;
                }
            SingleInteractionRegistry.Invalidate();
        }

        /// <summary>Applies assembly-owned locks while preserving independent unlock rules.</summary>
        private void RefreshLocks()
        {
            // Unlisted interactions wait for all mandatory ingredients; explicit rules may enable partial use.
            if (features == null)
                return;
            IsComplete = parts.Count > 0;
            foreach (AssemblyIngredient ingredient in settings.Ingredients)
                if (!ingredient.Optional && AssemblyCapacity.Count(counts, ingredient.Flags) < ingredient.Count)
                    IsComplete = false;
            for (int index = 0; index < settings.Magnets.Length; index++)
                if (settings.Magnets[index].Order > 0 && !occupied[index])
                    IsComplete = false;
            if (completedAppearanceApplied != IsComplete)
            {
                if (IsComplete)
                    completedAppearance.Commit();
                else
                    completedAppearance.Restore();
                completedAppearanceApplied = IsComplete;
            }
            // Optional ingredients inserted after completion also inherit the final presentation immediately.
            foreach (ObjectAssemblyPart part in parts)
            {
                part.SetGeometryHidden(IsComplete && settings.ReplacesIngredients);
                part.SetPhysicsSuppressed(IsComplete);
            }
            foreach (ObjectInteraction feature in features)
            {
                if (feature == null || feature == this || feature is ObjectInteractionUnlock)
                    continue;
                bool available = IsComplete;
                foreach (AssemblyInteractionRule rule in settings.InteractionRules)
                    if (rule.Target == feature)
                    {
                        available = rule.RequireComplete ? IsComplete : IngredientUnits >= rule.MinimumIngredients;
                        foreach (ItemFlagRequirement requirement in rule.Ingredients)
                            available &= AssemblyCapacity.Count(counts, requirement.Flags) >= requirement.Count;
                        break;
                    }
                feature.SetLocked(this, !available);
            }
        }

        #endregion

        #endregion
    }
}
