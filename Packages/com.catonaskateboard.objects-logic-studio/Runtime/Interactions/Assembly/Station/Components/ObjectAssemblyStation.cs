using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Accepts recipe ingredients through a player command or sustained contact.</summary>
    [AddComponentMenu("Objects Logic Studio/Assembly Table")]
    public sealed class ObjectAssemblyStation : ObjectExtendedInteraction
    {
        #region Serialized Fields

        [Header("Assembly Table")]
        [Tooltip("Product prefab, reach and output placement for this assembly table.")]
        [SerializeField]
        private AssemblyStationSettings settings = new AssemblyStationSettings();
        [Tooltip("Dedicated Button action resolved in the observer player's active Input Actions asset.")]
        [SerializeField]
        private InputActionReference action;
        [Tooltip("Inactive child authored by the tool so a product can receive its first ingredient before activation.")]
        [SerializeField]
        private Transform staging;

        #endregion

        #region State

        private ObjectAssemblyProduct current;
        private AssemblyContactRun contact;
        private bool contactReady;
        private string lastWarning;

        #endregion

        #region Properties

        /// <summary>Reusable table configuration.</summary>
        public AssemblyStationSettings Settings => settings;
        /// <summary>Player command used to insert a carried ingredient.</summary>
        public InputActionReference Action => action;
        /// <summary>World-space pose at which a product begins or returns.</summary>
        public Vector3 OutputPosition => transform.TransformPoint(settings.OutputPosition);
        /// <summary>Root scale inherited by a product instantiated under the authored staging transform.</summary>
        internal Vector3 OutputScale => (staging.localToWorldMatrix * settings.ProductPrefab.transform.localToWorldMatrix).lossyScale;
        /// <summary>Product still waiting on this table; pickup releases the table immediately.</summary>
        public ObjectAssemblyProduct CurrentProduct => current != null && current.isActiveAndEnabled
            && !current.Item.IsCarried && !current.Item.IsConsumed ? current : null;
        /// <summary>Identifies the table card in Object Assemble.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.AssemblyStation;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Registers this table without searching for players or binding input in every frame.</summary>
        private void OnEnable()
        {
            // The shared observer resolves the current PlayerInput for all tables.
            AssemblyInteractionRegistry.Register(this);
            InitializeContact();
        }

        /// <summary>Starts a fresh contact session after activation or a retained-scene Play entry.</summary>
        internal void InitializeContact()
        {
            // Reusing the detector also discards any elapsed dwell from the previous session.
            contact?.Reset();
            contactReady = false;
            if (settings.Trigger != AssemblyStationTrigger.InputAction)
            {
                contactReady = TryValidate(out string warning);
                if (!contactReady && lastWarning != warning)
                    Debug.LogWarning($"Assembly Station '{InteractionName}' on '{name}': {warning}", this);
                lastWarning = contactReady ? string.Empty : warning;
                if (contactReady)
                {
                    contact ??= new AssemblyContactRun();
                    contact.Bind(this);
                }
            }
        }

        /// <summary>Removes input ownership while leaving any real assembled product in the world.</summary>
        private void OnDisable()
        {
            // A table never destroys a completed or partially built product when disabled.
            AssemblyInteractionRegistry.Unregister(this);
            contact?.Reset();
            contactReady = false;
            lastWarning = string.Empty;
        }

        #endregion

        #region Contact

        /// <summary>Evaluates throttled contact only for the selected direct-contact mode.</summary>
        private void FixedUpdate()
        {
            // Input stations do no contact queries or geometry work.
            if (contactReady && settings.Trigger != AssemblyStationTrigger.InputAction)
                contact.Tick(this, Time.time);
        }

        #endregion

        #region Validation

        /// <summary>Checks the configured product, input and authored inactive staging transform.</summary>
        /// <param name="warning">Receives a missing dependency or invalid setting.</param>
        /// <returns>True when the table can assemble safely.</returns>
        public override bool TryValidate(out string warning)
        {
            // Editor proposals use the same checks as runtime input registration.
            return TryValidate(settings, action, out warning);
        }

        /// <summary>Validates detached table settings without modifying its prefab or linked product.</summary>
        /// <param name="configuration">Proposed product and placement settings.</param>
        /// <param name="command">Proposed player Button.</param>
        /// <param name="warning">Receives the first configuration issue.</param>
        /// <returns>True when all dependencies are ready.</returns>
        public bool TryValidate(AssemblyStationSettings configuration, InputActionReference command, out string warning)
        {
            // Local bindings are independent of reusable table settings.
            warning = "Configure the table settings first.";
            if (configuration == null || !configuration.TryValidate(out warning))
                return false;
            warning = "Choose a Button action from the player's Input Actions asset.";
            if (configuration.Trigger == AssemblyStationTrigger.InputAction
                && (command == null || command.action == null || command.action.type != InputActionType.Button))
                return false;
            if (configuration.Trigger != AssemblyStationTrigger.InputAction)
            {
                warning = "Contact assembly requires an Object Item and an enabled primitive or convex collider.";
                bool hasShape = false;
                foreach (Collider shape in GetComponentsInChildren<Collider>(true))
                    if (shape.GetComponentInParent<ObjectItem>() == Item && ContactDetection.Usable(shape, configuration.IncludeTriggers))
                    {
                        hasShape = true;
                        break;
                    }
                if (GetComponent<ObjectItem>() == null || !hasShape)
                    return false;
            }
            warning = "Rebuild the table's inactive staging child in Object Assemble.";
            if (staging == null || staging == transform || !staging.IsChildOf(transform) || staging.gameObject.activeSelf)
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion

        #region Assembly

        /// <summary>Releases only the product that was picked up from this table.</summary>
        /// <param name="product">Product beginning carry ownership.</param>
        internal void Release(ObjectAssemblyProduct product)
        {
            // Another table cannot accidentally clear this table's newly started product.
            if (current == product)
                current = null;
        }

        /// <summary>Checks whether this table can use the player's held object without making changes.</summary>
        /// <param name="held">Current carry-slot owner.</param>
        /// <param name="requireHeld">True for input insertion; false for physical contact insertion.</param>
        /// <returns>True for a valid next ingredient or a matching product being returned.</returns>
        internal bool CanAccept(ObjectGrab held, bool requireHeld = true)
        {
            // Availability includes independent Unlock Interactions and temporary contact restrictions.
            if (!Available(InteractionChannels.Assembly) || Item == null || Item.IsReserved || held == null || !held.isActiveAndEnabled
                || requireHeld && !held.IsHeld || !requireHeld && !AssemblyIngredientDock.Allows(this, held))
                return false;
            current = CurrentProduct;
            if (CanReturn(held, requireHeld, out _))
                return true;
            ObjectAssemblyProduct target = current != null ? current : settings.ProductPrefab.GetComponent<ObjectAssemblyProduct>();
            return target.CanAccept(held, out _, requireHeld);
        }

        /// <summary>Checks whether automatic insertion must wait for another ingredient to start this empty station.</summary>
        /// <param name="ingredient">Contact candidate that may be a completed product.</param>
        /// <returns>True when the candidate requests deferred insertion into a new recipe.</returns>
        internal bool WaitsForIngredient(ObjectGrab ingredient)
        {
            // An assembly already in progress has received the new ingredient that releases this restriction.
            return CurrentProduct == null && ingredient != null && ingredient.TryGetComponent(out ObjectAssemblyProduct product)
                && product.IsComplete && product.Settings.WaitForNextIngredient;
        }

        /// <summary>Tracks contact eligibility while allowing later ordered ingredients to wait beside a completed product.</summary>
        /// <param name="ingredient">Contact candidate.</param>
        /// <param name="waiting">Whether a completed product is waiting to start the recipe.</param>
        /// <returns>True when contact duration may accumulate for this candidate.</returns>
        internal bool CanTrackContact(ObjectGrab ingredient, bool waiting)
        {
            // Ordinary stations retain their existing slot and order checks.
            return AssemblyIngredientDock.Allows(this, ingredient) && (waiting && CurrentProduct == null
                ? settings.ProductPrefab.GetComponent<ObjectAssemblyProduct>().CanConsider(ingredient)
                : CanAccept(ingredient, false));
        }

        /// <summary>Preflights a complete two-object start without changing either ingredient.</summary>
        /// <param name="first">Completed product waiting at this empty station.</param>
        /// <param name="second">Companion ready to enter the next slot.</param>
        /// <returns>True when the station and both ordered insertions are available.</returns>
        internal bool CanBeginWith(ObjectGrab first, ObjectGrab second)
        {
            // Current ownership is rechecked at commit because several stations can share one contact surface.
            return WaitsForIngredient(first) && Available(InteractionChannels.Assembly) && Item != null && !Item.IsReserved
                && AssemblyIngredientDock.Allows(this, first) && AssemblyIngredientDock.Allows(this, second)
                && settings.ProductPrefab.GetComponent<ObjectAssemblyProduct>().CanAcceptPair(first, second);
        }

        /// <summary>Returns a carried product or inserts one ingredient into the table's current product.</summary>
        /// <param name="held">Actual object occupying the observer's carry slot.</param>
        /// <param name="requireHeld">True for input insertion; false for physical contact insertion.</param>
        /// <param name="companion">Second contact ingredient required to start with a deferred completed product.</param>
        /// <returns>True when the command performed one successful transfer.</returns>
        internal bool Execute(ObjectGrab held, bool requireHeld = true, ObjectGrab companion = null)
        {
            // A failed recipe or slot check leaves the carry slot and existing product untouched.
            if (!CanAccept(held, requireHeld)
                || companion != null && (requireHeld || !CanBeginWith(held, companion))
                || !requireHeld && companion == null && WaitsForIngredient(held))
                return false;
            if (CanReturn(held, requireHeld, out ObjectAssemblyProduct returning))
            {
                held.Cancel();
                current = returning;
                Place(current);
                return true;
            }
            bool spawned = current == null;
            if (spawned)
            {
                GameObject instance = Instantiate(settings.ProductPrefab, staging, false);
                instance.SetActive(false);
                current = instance.GetComponent<ObjectAssemblyProduct>();
                instance.transform.SetParent(null, true);
                current.Initialize();
                current.SourcePrefab = settings.ProductPrefab;
                Place(current);
            }
            bool complete = current.IsComplete;
            if (!(companion != null ? current.AcceptPair(held, companion) : current.Accept(held, requireHeld)))
            {
                if (spawned)
                {
                    Destroy(current.gameObject);
                    current = null;
                }
                return false;
            }
            if (spawned)
                current.gameObject.SetActive(true);
            current.PublishPending();
            Signal(InteractionMoment.Started);
            if (!complete && current.IsComplete)
                Signal(InteractionMoment.Completed);
            return true;
        }

        /// <summary>Separates returning this table's own product from supplying another recipe's ingredient.</summary>
        /// <param name="held">Candidate root interaction.</param>
        /// <param name="requireHeld">Only an explicit carry command can return an existing product.</param>
        /// <param name="product">Receives the matching product when return is allowed.</param>
        /// <returns>True when the table should retain the existing product instead of using it as an ingredient.</returns>
        private bool CanReturn(ObjectGrab held, bool requireHeld, out ObjectAssemblyProduct product)
        {
            // Completed products with other source prefabs continue through normal flag and magnet matching.
            product = null;
            return requireHeld && current == null && settings.AcceptReturnedProduct && held.TryGetComponent(out product)
                && product.isActiveAndEnabled && product.SourcePrefab == settings.ProductPrefab && !product.Item.IsConsumed
                && !product.Item.IsReserved && !product.Item.IsBlocked(InteractionChannels.Assembly);
        }

        /// <summary>Sets a stable world-space output pose without parenting the product to table physics.</summary>
        /// <param name="product">New or returned product.</param>
        private void Place(ObjectAssemblyProduct product)
        {
            // Product pickup captures this kinematic policy; Drop and Throw can still make it dynamic.
            product.Table = this;
            Rigidbody body = product.GetComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            product.transform.SetPositionAndRotation(OutputPosition, transform.rotation * Quaternion.Euler(settings.OutputRotation));
            body.position = product.transform.position;
            body.rotation = product.transform.rotation;
        }

        #endregion

        #endregion
    }
}
