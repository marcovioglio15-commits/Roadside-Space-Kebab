using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Applies one configured impulse to every eligible touching body after a player input action.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Eject Interaction")]
    public sealed class ObjectEject : ObjectCommandInteraction
    {
        #region Serialized Fields

        [Header("Ejection")]
        [Tooltip("Contact policy, flag-specific impulses and optional object lifetime.")]
        [SerializeField]
        private EjectSettings settings = new EjectSettings();

        #endregion

        #region State

        private readonly FlagContactQuery query = new FlagContactQuery();
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();
        private readonly HashSet<Rigidbody> ejected = new HashSet<Rigidbody>();
        private bool executing;

        #endregion

        #region Properties

        /// <summary>Applied flag groups, contact policy and lifetime settings.</summary>
        public EjectSettings Settings => settings;
        /// <summary>Single card represented by this component.</summary>
        public override SingleInteractionKind Kind => SingleInteractionKind.Eject;
        /// <summary>Range and aiming used by the shared input driver.</summary>
        public override TransferTargetSettings Target => settings.Target;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks input-independent contact and impulse configuration.</summary>
        /// <param name="warning">Receives the first invalid setting or missing contact shape.</param>
        /// <returns>True when this ejector can bind input.</returns>
        protected override bool TryValidateSettings(out string warning)
        {
            // The detached authoring proposal uses the same validation route.
            return TryValidate(settings, out warning);
        }

        /// <summary>Validates an imported snapshot against the ejector's actual collider hierarchy.</summary>
        /// <param name="configuration">Settings being applied.</param>
        /// <param name="warning">Receives unusable rules or missing owned geometry.</param>
        /// <returns>True when at least one supported collider can collect contacts.</returns>
        public bool TryValidate(EjectSettings configuration, out string warning)
        {
            // Static and kinematic ejectors are supported because detection does not depend on collision callbacks.
            warning = "Configure Eject settings first.";
            if (configuration == null || !configuration.TryValidate(out warning))
                return false;
            if (Item != null)
                foreach (Collider shape in GetComponentsInChildren<Collider>(true))
                    if (Item.Owns(shape.transform) && ContactDetection.Usable(shape, configuration.IncludeTriggers))
                        return true;
            warning = "Eject requires an enabled owned primitive or convex collider compatible with Include Triggers.";
            return false;
        }

        #endregion

        #region Execution

        /// <summary>Checks locks before spending any physics work on contacts.</summary>
        /// <returns>True when the ejector is eligible for input selection.</returns>
        internal override bool CanExecute()
        {
            // Contact queries run only after this object wins the performed input action.
            return !executing && Available(InteractionChannels.Eject) && Item != null && !Item.IsReserved;
        }

        /// <summary>Collects exact contacts and applies the first matching group to each distinct body.</summary>
        /// <returns>True when at least one physical object was ejected.</returns>
        internal override bool Execute()
        {
            // Complete collection precedes mutation; a saturated buffer never yields a partial ejection.
            if (!CanExecute() || !query.Collect(Item, Colliders, settings.Tolerance, settings.IncludeTriggers, contacts))
                return false;
            executing = true;
            ejected.Clear();
            try
            {
                // Rule order is authoritative even when different child colliders carry different flags.
                foreach (EjectRule rule in settings.Rules)
                    foreach (Collider contact in contacts)
                        if (contact != null && FlagContactQuery.Matches(contact.transform, rule.Flags, rule.Match))
                            Eject(contact.attachedRigidbody, rule);
                if (ejected.Count == 0)
                    return false;
                Signal(InteractionMoment.Started);
                Signal(InteractionMoment.Completed);
                return true;
            }
            finally
            {
                executing = false;
                contacts.Clear();
                ejected.Clear();
            }
        }

        /// <summary>Releases carry ownership before adding force and scheduling an optional independent lifetime.</summary>
        /// <param name="body">Physical root of one contacted object.</param>
        /// <param name="rule">First matching identity group.</param>
        private void Eject(Rigidbody body, EjectRule rule)
        {
            // Suspended ingredients, stored objects and in-progress modifications retain their ownership.
            if (body == null || ejected.Contains(body) || body.transform.IsChildOf(transform) || transform.IsChildOf(body.transform)
                || !settings.ReleaseKinematic && body.isKinematic)
                return;
            ObjectItem item = body.GetComponentInParent<ObjectItem>();
            if (item != null && (!item.isActiveAndEnabled || item.IsReserved || item.IsBlocked(InteractionChannels.Eject)))
                return;
            if (body.TryGetComponent(out ObjectGrab grab) && grab.IsHeld)
            {
                if (!settings.ReleaseKinematic)
                    return;
                grab.Cancel();
            }
            body.isKinematic = false;
            body.detectCollisions = true;
            body.WakeUp();
            if (settings.IgnoreCollisions)
                EjectCollisionGrace.Apply(body, settings.IgnoredLayers, settings.IgnoreDuration);
            body.AddForce(rule.Space == Space.Self ? transform.rotation * rule.Impulse : rule.Impulse,
                settings.Mode == EjectForceMode.Impulse ? ForceMode.Impulse : ForceMode.VelocityChange);
            ejected.Add(body);
            if (settings.Despawn)
                Destroy(item != null ? item.gameObject : body.gameObject, settings.DespawnDelay);
        }

        #endregion

        #endregion
    }
}
