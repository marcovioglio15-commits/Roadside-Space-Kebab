using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Accepts one carried ingredient per input and creates products from one configured prefab.</summary>
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

        #endregion

        #region Properties

        /// <summary>Reusable table configuration.</summary>
        public AssemblyStationSettings Settings => settings;
        /// <summary>Player command used to insert a carried ingredient.</summary>
        public InputActionReference Action => action;
        /// <summary>World-space pose at which a product begins or returns.</summary>
        public Vector3 OutputPosition => transform.TransformPoint(settings.OutputPosition);
        /// <summary>Product still waiting on this table; pickup releases the table immediately.</summary>
        public ObjectAssemblyProduct CurrentProduct => current != null && current.gameObject.activeInHierarchy
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
        }

        /// <summary>Removes input ownership while leaving any real assembled product in the world.</summary>
        private void OnDisable()
        {
            // A table never destroys a completed or partially built product when disabled.
            AssemblyInteractionRegistry.Unregister(this);
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
            if (command == null || command.action == null || command.action.type != InputActionType.Button)
                return false;
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
        /// <returns>True for a valid next ingredient or a matching product being returned.</returns>
        internal bool CanAccept(ObjectGrab held)
        {
            // Availability includes independent Unlock Interactions and temporary contact restrictions.
            if (!Available(InteractionChannels.Assembly) || Item.IsReserved || held == null || !held.IsHeld)
                return false;
            current = CurrentProduct;
            if (held.TryGetComponent(out ObjectAssemblyProduct returning))
                return current == null && settings.AcceptReturnedProduct && returning.SourcePrefab == settings.ProductPrefab
                    && !returning.Item.IsReserved && !returning.Item.IsBlocked(InteractionChannels.Assembly);
            ObjectAssemblyProduct target = current != null ? current : settings.ProductPrefab.GetComponent<ObjectAssemblyProduct>();
            return target.CanAccept(held, out int magnet);
        }

        /// <summary>Returns a carried product or inserts one ingredient into the table's current product.</summary>
        /// <param name="held">Actual object occupying the observer's carry slot.</param>
        /// <returns>True when the command performed one successful transfer.</returns>
        internal bool Execute(ObjectGrab held)
        {
            // A failed recipe or slot check leaves the carry slot and existing product untouched.
            if (!CanAccept(held))
                return false;
            if (held.TryGetComponent(out ObjectAssemblyProduct returning))
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
            if (!current.Accept(held))
            {
                if (spawned)
                    Destroy(current.gameObject);
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
