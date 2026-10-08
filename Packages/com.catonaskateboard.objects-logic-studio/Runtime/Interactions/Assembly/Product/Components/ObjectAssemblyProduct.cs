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
            if (TryGetComponent(out ObjectGrab grab))
                grab.Dock?.Release();
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
            if (TryGetComponent(out ObjectGrab grab))
                grab.Dock?.Release();
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

        /// <summary>Checks a whole proposed start without committing one recipe ahead of its alternatives.</summary>
        /// <param name="ingredients">Ordered physical ingredients proposed for an empty recipe.</param>
        /// <param name="complete">Receives whether the proposal finishes this recipe.</param>
        /// <returns>True when all objects fit in order without overlapping ownership.</returns>
        internal bool CanAcceptSequence(IReadOnlyList<ObjectGrab> ingredients, out bool complete)
        {
            // Validate ownership before any scratch selection accesses recipe arrays.
            complete = false;
            if (ingredients.Count == 0 || !CanConsider(ingredients[0]))
                return false;
            selection.Begin(settings);
            for (int index = 0; index < ingredients.Count; index++)
            {
                ObjectGrab ingredient = ingredients[index];
                if (!CanConsider(ingredient))
                    return false;
                for (int previous = 0; previous < index; previous++)
                    if (ingredient.transform.IsChildOf(ingredients[previous].transform)
                        || ingredients[previous].transform.IsChildOf(ingredient.transform))
                        return false;
                if (!selection.Append(settings, ingredient, index + 1, out _, out _))
                    return false;
            }
            complete = selection.Completes(settings);
            return true;
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
            if (TryGetComponent(out ObjectGrab grab))
                grab.Dock?.Release();
            if (Table != null)
                Table.Release(this);
            Table = null;
        }

        /// <summary>Lets an active gravity pulse free a table-retained product without interrupting another transaction.</summary>
        internal void ReleaseForSuspension()
        {
            // Ingredient bodies belong to the composite root; only independent active products may leave the table.
            if (!initialized || !isActiveAndEnabled || !Item.isActiveAndEnabled || Item.IsConsumed || Item.IsReserved || Item.IsCarried
                || Table == null && !(TryGetComponent(out ObjectGrab grab) && grab.Dock != null))
                return;
            ReleaseTable();
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.WakeUp();
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

        /// <summary>Commits a resolved recipe proposal before activating the newly spawned product.</summary>
        /// <param name="ingredients">Ordered ingredients that distinguish this recipe from its alternatives.</param>
        /// <returns>True when every appearance and insertion was prepared before taking ownership.</returns>
        internal bool AcceptSequence(IReadOnlyList<ObjectGrab> ingredients)
        {
            // Preparation is atomic: a failed binding must leave every pending ingredient usable.
            if (gameObject.activeInHierarchy || !CanAcceptSequence(ingredients, out _))
                return false;
            Initialize();
            if (!initialized)
                return false;
            (int Magnet, ObjectFlag Flag, ItemAppearanceChanges Appearance)[] prepared
                = new (int, ObjectFlag, ItemAppearanceChanges)[ingredients.Count];
            selection.Begin(settings);
            for (int index = 0; index < ingredients.Count; index++)
            {
                if (!selection.Append(settings, ingredients[index], index + 1, out int magnet, out ObjectFlag flag)
                    || !ItemAppearanceChanges.TryPrepare(ingredients[index].Item, settings.Magnets[magnet].Appearance,
                        out ItemAppearanceChanges appearance, out _))
                    return false;
                prepared[index] = (magnet, flag, appearance);
            }
            // No ingredient can fire completion callbacks until the whole proposal has been attached.
            for (int index = 0; index < ingredients.Count; index++)
                AttachIngredient(ingredients[index], prepared[index].Magnet, prepared[index].Flag, prepared[index].Appearance);
            return true;
        }

        /// <summary>Attaches a validated ingredient and refreshes the product's physical and interaction state.</summary>
        /// <param name="grab">Ingredient whose transfer has passed validation.</param>
        /// <param name="magnet">Reserved recipe slot.</param>
        /// <param name="flag">Single recipe category counted for this insertion.</param>
        /// <param name="appearance">Prepared ingredient appearance transaction.</param>
        private void AttachIngredient(ObjectGrab grab, int magnet, ObjectFlag flag, ItemAppearanceChanges appearance)
        {
            // Deferred proposals remain inactive until all insertions have completed.
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
            ObjectGravityGenerator.IncludeSpawn(gameObject);
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

        /// <summary>Collects insertion identities only from ingredients still belonging to this assembled product.</summary>
        /// <param name="destination">Caller-owned set receiving recipe modifier identities.</param>
        internal void CollectIngredientFlags(HashSet<ObjectFlag> destination)
        {
            // Detached ingredients no longer participate; root identity changes cannot add recipe modifiers.
            foreach (ObjectAssemblyPart part in parts)
                if (part != null && part.Product == this)
                    foreach (ObjectFlag flag in part.IngredientFlags)
                        if (flag != null)
                            destination.Add(flag);
        }

        /// <summary>Captures recipe provenance before consumption deactivates the product hierarchy.</summary>
        /// <returns>Inserted ingredient flags, or an empty array for an unfinished recipe.</returns>
        internal ObjectFlag[] CaptureIngredientFlags()
        {
            if (!IsComplete)
                return System.Array.Empty<ObjectFlag>();
            HashSet<ObjectFlag> flags = new HashSet<ObjectFlag>();
            CollectIngredientFlags(flags);
            ObjectFlag[] result = new ObjectFlag[flags.Count];
            flags.CopyTo(result);
            return result;
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
                    case ObjectElasticDeformation elastic:
                        elastic.RefreshGeometry();
                        break;
                    case ObjectDirtTrail dirt:
                        dirt.RefreshGeometry();
                        break;
                    case ObjectSpraySauce spray:
                        spray.RefreshGeometry();
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
            IsComplete = AssemblyIngredientSelection.Completes(settings, counts, occupied, parts.Count > 0);
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
