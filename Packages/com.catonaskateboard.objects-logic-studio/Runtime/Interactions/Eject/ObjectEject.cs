using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Applies one impulse to this object's body or eligible touching bodies after a player input action.</summary>
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
        private readonly BlackHoleRun attraction = new BlackHoleRun();
        private readonly EjectBodyState temporary = new EjectBodyState();
        private TriggerAnimationPlayback pendingAnimation;

        #endregion

        #region Properties

        /// <summary>Applied flag groups, contact policy and lifetime settings.</summary>
        public EjectSettings Settings => settings;
        /// <summary>Animation requested by the active prerequisite policy.</summary>
        internal ObjectTriggerAnimation Prerequisite => settings.ExternalAnimation ? SceneAnimationRegistry.Resolve(settings.AnimationIdentity) : GetComponent<ObjectTriggerAnimation>();
        /// <summary>Single card represented by this component.</summary>
        public override SingleInteractionKind Kind => SingleInteractionKind.Eject;
        /// <summary>Range and aiming used by the shared input driver.</summary>
        public override TransferTargetSettings Target => settings.Target;
        /// <summary>Defers Day Flow departure during a temporary self-launch prerequisite and its scheduled despawn.</summary>
        internal bool AwaitingDespawn => settings.Despawn && (temporary.Active || pendingAnimation is { Cancelled: false }
            && settings.Behavior == EjectBehavior.Impulse && settings.SelfEject && settings.TemporaryPhysics);

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Restores temporary launch overrides before a pooled object can be reused.</summary>
        protected override void OnDisable()
        {
            // An interrupted prerequisite must never launch a reused or re-enabled object later.
            CancelAnimation();
            attraction.Cancel();
            temporary.Restore();
            base.OnDisable();
        }

        /// <summary>Advances active attraction and restores destination contacts after each final launch.</summary>
        private void FixedUpdate()
        {
            // Physics starts only after an actual endpoint, including pauses and zero-duration transitions.
            if (pendingAnimation != null)
            {
                if (pendingAnimation.Cancelled)
                    pendingAnimation = null;
                else if (pendingAnimation.Completed && CanApplyEffect())
                {
                    pendingAnimation.Release();
                    pendingAnimation = null;
                    ApplyEffect();
                }
            }
            if (attraction.Pending && attraction.Tick(Time.fixedDeltaTime))
                Signal(InteractionMoment.Completed);
        }

        /// <summary>Restores retained physics when entering Play without scene reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetAttractions()
        {
            foreach (ObjectEject ejector in FindObjectsByType<ObjectEject>(FindObjectsInactive.Include))
            {
                ejector.CancelAnimation();
                ejector.attraction.Cancel();
            }
        }

        /// <summary>Clears a pending prerequisite without interrupting unrelated automatic return settings.</summary>
        private void CancelAnimation()
        {
            pendingAnimation?.Cancel();
            pendingAnimation = null;
        }

        #endregion

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
        /// <param name="connection">Optional detached scene connection supplied by the authoring session.</param>
        /// <returns>True when the object has the body or contact geometry required by its mode.</returns>
        public bool TryValidate(EjectSettings configuration, out string warning, ObjectTriggerAnimation connection = null)
        {
            // Static and kinematic ejectors are supported because detection does not depend on collision callbacks.
            warning = "Configure Eject settings first.";
            if (configuration == null || !configuration.TryValidate(out warning))
                return false;
            if (configuration.AnimateBeforeEject)
            {
                ObjectTriggerAnimation selected = configuration.ExternalAnimation
                    ? connection != null ? connection : SceneAnimationRegistry.Resolve(configuration.AnimationIdentity)
                    : GetComponent<ObjectTriggerAnimation>();
                warning = "Assign an enabled scene Trigger Animation, or enable the local animation.";
                if (selected != null)
                {
                    if (!selected.enabled || !selected.TryValidate(selected.Settings, out warning))
                        return false;
                }
                // Bind input even while a remote endpoint is disabled or its additive scene is not loaded yet.
                // Execution resolves it again and cannot apply an effect without a successful animation request.
                else if (!configuration.ExternalAnimation)
                    return false;
                warning = string.Empty;
            }
            if (configuration.Behavior == EjectBehavior.BlackHole)
            {
                Collider destination = configuration.BlackHole.Resolve(transform);
                warning = "Select an enabled destination collider inside the ejector hierarchy.";
                if (destination == null || !destination.enabled || !destination.gameObject.activeInHierarchy)
                    return false;
                if (configuration.BlackHole.Air)
                {
                    Transform air = CatOnASkateboard.PlayerStudio.PlayerHierarchy.Resolve(transform, configuration.BlackHole.AirPath);
                    warning = "Assign an existing active ParticleSystem inside the ejector for air suction.";
                    if (air == null || !air.gameObject.activeInHierarchy || air.GetComponent<ParticleSystem>() == null)
                        return false;
                }
                warning = string.Empty;
                return true;
            }
            if (configuration.SelfEject)
                return ObjectGrab.ValidateBody(gameObject, out warning, configuration.TemporaryPhysics && !Application.isPlaying);
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
            return pendingAnimation == null && CanApplyEffect();
        }

        /// <summary>Accepts one activation, optionally requesting the selected animation before any physics changes.</summary>
        /// <returns>True when ejection starts or its animation prerequisite accepts the request.</returns>
        internal override bool Execute()
        {
            if (!CanExecute())
                return false;
            // Contact collection is deferred so moving geometry is evaluated at its final pose.
            if (!settings.AnimateBeforeEject)
                return ApplyEffect();
            ObjectTriggerAnimation selected = Prerequisite;
            if (selected == null || !selected.TryRequest(out pendingAnimation, settings.WaitForAnimationReturn))
                return false;
            // Start listeners may disable Eject synchronously before TryRequest returns its result.
            if (isActiveAndEnabled)
                return true;
            CancelAnimation();
            return false;
        }

        /// <summary>Checks physical ownership again when a delayed animation result becomes available.</summary>
        /// <returns>True when the current item may apply its Eject effect.</returns>
        private bool CanApplyEffect()
        {
            return !executing && !attraction.Active && !temporary.Active && Available(InteractionChannels.Eject)
                && Item != null && !Item.IsReserved;
        }

        /// <summary>Collects final contacts and applies impulses or starts attraction after all prerequisites pass.</summary>
        /// <returns>True when at least one body was ejected or a Black Hole run began.</returns>
        private bool ApplyEffect()
        {
            // Complete collection precedes mutation; a saturated buffer never yields a partial ejection.
            if (settings.Behavior == EjectBehavior.BlackHole)
            {
                attraction.Begin(this);
                Signal(InteractionMoment.Started);
                return true;
            }
            if (!settings.SelfEject && !query.Collect(Item, Colliders, settings.Tolerance, settings.IncludeTriggers, contacts))
                return false;
            executing = true;
            ejected.Clear();
            try
            {
                // Rule order is authoritative even when different child colliders carry different flags.
                if (settings.SelfEject)
                    Eject(GetComponent<Rigidbody>(), settings.SelfImpulse);
                else
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
            if (body == null || ejected.Contains(body) || body.gameObject.isStatic
                || !settings.SelfEject && (body.transform.IsChildOf(transform) || transform.IsChildOf(body.transform))
                || !settings.ReleaseKinematic && !(settings.SelfEject && settings.TemporaryPhysics) && body.isKinematic)
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
            if (settings.SelfEject && settings.TemporaryPhysics)
                temporary.Begin(body, settings.Physics);
            body.isKinematic = false;
            body.detectCollisions = true;
            body.WakeUp();
            if (settings.IgnoreCollisions)
                EjectCollisionGrace.Apply(body, settings.IgnoredLayers, settings.IgnoreDuration);
            // Both force modes are single physics impulses; no Update loop reapplies the force.
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
