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

        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        private readonly List<ObjectAssemblyPart> parts = new List<ObjectAssemblyPart>();
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
        public IReadOnlyDictionary<string, int> IngredientCounts => counts;
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
            if (!TryValidate(out string warning))
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
            initialized = destroying = pendingStart = pendingCompletion = IsComplete = false;
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
        /// <returns>True when the complete transfer is eligible.</returns>
        internal bool CanAccept(ObjectGrab grab, out int magnet)
        {
            // A reserved product or ingredient must finish its visual transaction before changing ownership.
            RefreshSession();
            magnet = -1;
            if (!enabled || IsLocked || Item == null || Item.IsConsumed || Item.IsReserved || Item.IsCarried
                || Item.IsBlocked(InteractionChannels.Assembly) || grab == null || !grab.IsHeld
                || grab.GetComponent<ObjectAssemblyProduct>() != null || grab.transform.IsChildOf(transform)
                || !TryValidate(out string warning) || !ObjectGrab.ValidateBody(grab.gameObject, out warning)
                || grab.GetComponentsInChildren<Joint>(true).Length > 0)
                return false;
            foreach (ObjectItem ingredient in grab.GetComponentsInChildren<ObjectItem>(true))
                if (ingredient.IsConsumed || ingredient.IsReserved || ingredient.IsBlocked(InteractionChannels.Assembly))
                    return false;
            foreach (Collider collider in grab.GetComponentsInChildren<Collider>(true))
                if (collider.enabled && collider is not (BoxCollider or SphereCollider or CapsuleCollider or MeshCollider { convex: true, sharedMesh: not null }))
                    return false;
            // Specific slots are selected before generic ones, preserving room for every allowed tag.
            string tag = grab.gameObject.tag;
            bool allowed = false;
            foreach (AssemblyIngredient ingredient in settings.Ingredients)
                if (ingredient.Tag == tag && grab.Units > 0 && grab.Units <= ingredient.Count - Count(tag))
                {
                    allowed = true;
                    break;
                }
            if (!allowed)
                return false;
            for (int index = 0; index < settings.Magnets.Length; index++)
                if ((!initialized || !occupied[index]) && !settings.Magnets[index].AnyIngredient && settings.Magnets[index].Tag == tag
                    && CanFinishAfterInsertion(index, tag, grab.Units))
                {
                    magnet = index;
                    return true;
                }
            for (int index = 0; index < settings.Magnets.Length; index++)
                if ((!initialized || !occupied[index]) && settings.Magnets[index].AnyIngredient
                    && CanFinishAfterInsertion(index, tag, grab.Units))
                {
                    magnet = index;
                    return true;
                }
            return false;
        }

        /// <summary>Preserves at least one compatible slot for each mandatory tag still needing units.</summary>
        /// <param name="selected">Magnet occupied by the proposed physical ingredient.</param>
        /// <param name="tag">Incoming ingredient tag.</param>
        /// <param name="units">Units supplied by this insertion.</param>
        /// <returns>True when remaining mandatory units can still receive physical ingredients.</returns>
        private bool CanFinishAfterInsertion(int selected, string tag, int units)
        {
            // Unknown future item values need only one slot per unfinished tag; never assume one unit per object.
            int generic = 0;
            int required = 0;
            for (int index = 0; index < settings.Magnets.Length; index++)
                if (index != selected && (!initialized || !occupied[index]) && settings.Magnets[index].AnyIngredient)
                    generic++;
            foreach (AssemblyIngredient ingredient in settings.Ingredients)
            {
                if (ingredient.Optional || Count(ingredient.Tag) + (ingredient.Tag == tag ? units : 0) >= ingredient.Count)
                    continue;
                bool specific = false;
                for (int index = 0; index < settings.Magnets.Length && !specific; index++)
                    specific = index != selected && (!initialized || !occupied[index])
                        && !settings.Magnets[index].AnyIngredient && settings.Magnets[index].Tag == ingredient.Tag;
                if (!specific)
                    required++;
            }
            return required <= generic;
        }

        #endregion

        #region Assembly

        /// <summary>Frees the table as soon as pickup begins, even if the product is dropped before another input.</summary>
        internal void ReleaseTable()
        {
            // A later ingredient starts a new product unless this one is explicitly returned.
            if (Table != null)
                Table.Release(this);
            Table = null;
        }

        /// <summary>Attaches exactly one carried ingredient after every recipe and ownership check succeeds.</summary>
        /// <param name="grab">Ingredient currently occupying the observer's carry slot.</param>
        /// <returns>True when the ingredient became part of this product.</returns>
        internal bool Accept(ObjectGrab grab)
        {
            // No source component is destroyed; the part retains its standalone configuration.
            if (!CanAccept(grab, out int magnet))
                return false;
            Initialize();
            bool first = parts.Count == 0;
            bool complete = IsComplete;
            ObjectAssemblyPart part = grab.GetComponent<ObjectAssemblyPart>();
            if (part == null)
                part = grab.gameObject.AddComponent<ObjectAssemblyPart>();
            part.Attach(this, grab, magnet);
            parts.Add(part);
            foreach (ObjectInteraction feature in features)
                if (feature is ObjectOutline outline)
                    outline.AddPart(part);
            occupied[magnet] = true;
            counts.TryGetValue(part.IngredientTag, out int count);
            counts[part.IngredientTag] = count + part.Units;
            IngredientUnits += part.Units;
            RefreshGeometry();
            RefreshLocks();
            pendingStart |= first;
            pendingCompletion |= !complete && IsComplete;
            if (gameObject.activeInHierarchy)
                PublishPending();
            return true;
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
            counts[part.IngredientTag] -= part.Units;
            IngredientUnits -= part.Units;
            foreach (ObjectInteraction feature in features)
                if (feature is ObjectOutline outline)
                    outline.RemovePart(part);
            RefreshGeometry();
            RefreshLocks();
        }

        /// <summary>Reads a recipe count without exposing mutable progress.</summary>
        /// <param name="tag">Ingredient tag captured at insertion.</param>
        /// <returns>The logical units supplied by attached ingredients bearing that recipe tag.</returns>
        public int Count(string tag)
        {
            // An absent tag never acts as a wildcard.
            return tag != null && counts.TryGetValue(tag, out int count) ? count : 0;
        }

        /// <summary>Updates body mass and geometry caches only when the composition changes.</summary>
        private void RefreshGeometry()
        {
            // Original ingredient rigidbodies are dormant; product-owned proxy colliders supply the compound body.
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
                if (!ingredient.Optional && Count(ingredient.Tag) < ingredient.Count)
                    IsComplete = false;
            foreach (ObjectInteraction feature in features)
            {
                if (feature == null || feature == this || feature is ObjectInteractionUnlock)
                    continue;
                bool available = IsComplete;
                foreach (AssemblyInteractionRule rule in settings.InteractionRules)
                    if (rule.Target == feature)
                    {
                        available = rule.RequireComplete ? IsComplete : IngredientUnits >= rule.MinimumIngredients;
                        foreach (ItemTagRequirement requirement in rule.Ingredients)
                            available &= Count(requirement.Tag) >= requirement.Count;
                        break;
                    }
                feature.SetLocked(this, !available);
            }
        }

        #endregion

        #endregion
    }
}
