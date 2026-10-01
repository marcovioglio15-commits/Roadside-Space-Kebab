using CatOnASkateboard.PlayerStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Toggles an existing transform between two authored states without an Animator or runtime hierarchy creation.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Trigger Animation")]
    public sealed class ObjectTriggerAnimation : ObjectCommandInteraction
    {
        #region Serialized Fields

        [Header("Transform Animation")]
        [Tooltip("Hierarchy target, endpoint poses, timing and independent rotation directions.")]
        [SerializeField]
        private TriggerAnimationSettings settings = new TriggerAnimationSettings();

        #endregion

        #region State

        private Transform animated;
        private PlayerToolPose original;
        private PlayerToolPose from;
        private PlayerToolPose destination;
        private TransformRotationDirection direction;
        private float elapsed;
        private bool initialized;
        private bool running;
        private bool atB;
        private bool returnPending;
        private bool automatic;
        private float returnWait;
        private float duration;

        #endregion

        #region Properties

        /// <summary>Locally applied endpoint configuration.</summary>
        public TriggerAnimationSettings Settings => settings;
        /// <summary>Whether a transition currently owns the transform.</summary>
        public bool IsRunning => running;
        /// <summary>Whether the last completed endpoint is State B.</summary>
        public bool IsAtB => atB;
        /// <summary>Single card represented by this component.</summary>
        public override SingleInteractionKind Kind => SingleInteractionKind.TriggerAnimation;
        /// <summary>Reach and aiming configuration used by the shared input driver.</summary>
        public override TransferTargetSettings Target => settings.Target;
        /// <summary>Animation length offered to automatic start-effect timing.</summary>
        internal override float VfxDuration => automatic ? settings.AutoReturnDuration : settings.Duration;
        /// <summary>Whether a start effect still belongs to an active transition.</summary>
        internal override bool VfxRunning => running;
        /// <summary>Whether tool or interaction locks temporarily pause the current transition.</summary>
        internal override bool VfxPaused => running && !Available(InteractionChannels.Animation);

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Registers input and initializes one validated target before its first interaction.</summary>
        protected override void OnEnable()
        {
            // Runtime initialization never changes a prefab while its edit workspace is open.
            base.OnEnable();
            if (Application.isPlaying)
                Initialize();
        }

        /// <summary>Cancels ownership and restores the activation pose when the component leaves play.</summary>
        protected override void OnDisable()
        {
            // A cancelled transition emits no completion event.
            Release();
            base.OnDisable();
        }

        /// <summary>Resets retained managed state when entering Play without scene reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetSession()
        {
            // Find once per Play session, never during the input or animation loop.
            foreach (ObjectTriggerAnimation interaction in FindObjectsByType<ObjectTriggerAnimation>())
                if (interaction.isActiveAndEnabled)
                {
                    interaction.Release();
                    interaction.Initialize();
                }
        }

        /// <summary>Captures the original pose and applies the configured initial endpoint.</summary>
        private void Initialize()
        {
            // A missing target leaves this component dormant until it is re-enabled with valid settings.
            if (!TryValidate(settings, out string warning))
            {
                Debug.LogWarning(warning, this);
                return;
            }
            animated = PlayerHierarchy.Resolve(transform, settings.Path);
            original = PlayerToolPose.Read(animated);
            atB = settings.StartAtB;
            (atB ? settings.StateB : settings.StateA).Apply(animated);
            initialized = true;
        }

        /// <summary>Restores only the transform owned by this animation and releases its temporary item reservation.</summary>
        private void Release()
        {
            // Disabling other interactions never restores unrelated parent or camera transforms.
            if (initialized && animated != null)
                original.Apply(animated);
            Item?.Release(this);
            initialized = running = false;
            returnPending = automatic = false;
        }

        #endregion

        #region Validation

        /// <summary>Uses the same endpoint and ownership checks as the detached editor proposal.</summary>
        /// <param name="warning">Receives the first unusable setting or target.</param>
        /// <returns>True when runtime input can bind this animation.</returns>
        protected override bool TryValidateSettings(out string warning)
        {
            // Input validation is provided by the shared single-interaction base.
            return TryValidate(settings, out warning);
        }

        /// <summary>Checks destination hierarchy ownership and rejects competing dynamic-body animation.</summary>
        /// <param name="configuration">Detached or applied animation settings.</param>
        /// <param name="warning">Receives missing targets or a conflicting dynamic Rigidbody.</param>
        /// <returns>True when the selected transform can be controlled directly.</returns>
        public bool TryValidate(TriggerAnimationSettings configuration, out string warning)
        {
            // Child visuals under a dynamic object remain usable; moving that dynamic body's own transform does not.
            warning = "Configure the transform animation first.";
            if (configuration == null || !configuration.TryValidate(out warning))
                return false;
            Transform target = PlayerHierarchy.Resolve(transform, configuration.Path);
            if (target == null || Item == null || !Item.Owns(target))
                warning = "Choose an unambiguous transform owned by this object.";
            else if (target.TryGetComponent(out Rigidbody body) && !body.isKinematic)
                warning = "Animate a child transform or use a kinematic Rigidbody on the animation target.";
            return warning.Length == 0;
        }

        #endregion

        #region Execution

        /// <summary>Checks local locks, current animation ownership and conflicts with root carrying.</summary>
        /// <returns>True when a new endpoint transition may start.</returns>
        internal override bool CanExecute()
        {
            // Repeated input during a transition cannot flip the destination halfway through it.
            return initialized && animated != null && !running && Available(InteractionChannels.Animation)
                && Item != null && !Item.IsReserved && (animated != Item.transform || !Item.IsCarried);
        }

        /// <summary>Captures the live pose and begins travelling to the opposite completed endpoint.</summary>
        /// <returns>True when one transition successfully acquired its item.</returns>
        internal override bool Execute()
        {
            // Root motion blocks pickup; child-only motion can continue on an already carried item.
            return Begin(false);
        }

        /// <summary>Starts a manual toggle or a scheduled return through the same ownership boundary.</summary>
        /// <param name="returning">Whether to use the independent automatic return settings.</param>
        /// <returns>True when the target was acquired and the transition started.</returns>
        private bool Begin(bool returning)
        {
            // A pending return waits safely while another interaction owns the item.
            if (!CanExecute() || !Item.Acquire(this, animated == Item.transform ? InteractionChannels.Grab : InteractionChannels.None))
                return false;
            automatic = returning;
            returnPending = false;
            from = PlayerToolPose.Read(animated);
            if (Quaternion.Angle(Quaternion.Euler(from.Rotation), Quaternion.Euler((atB ? settings.StateB : settings.StateA).Rotation)) < 0.001f)
                from.Rotation = (atB ? settings.StateB : settings.StateA).Rotation;
            destination = atB ? settings.StateA : settings.StateB;
            direction = automatic ? settings.AutoReturnRotation : atB ? settings.ReturnRotation : settings.ForwardRotation;
            duration = automatic ? settings.AutoReturnDuration : settings.Duration;
            elapsed = 0f;
            running = true;
            Signal(InteractionMoment.Started);
            if (running && duration <= 0f)
                Complete();
            return true;
        }

        /// <summary>Advances only an active transition, pausing game time and independent interaction locks.</summary>
        private void Update()
        {
            // The owner's own reservation blocks competing animations, not this retained operation.
            if (animated == null || !Available(InteractionChannels.Animation) || Time.deltaTime <= 0f)
                return;
            if (!running)
            {
                if (returnPending && CanExecute())
                {
                    returnWait -= Time.deltaTime;
                    if (returnWait <= 0f)
                        Begin(true);
                }
                return;
            }
            elapsed += Time.deltaTime;
            if (elapsed >= duration)
                Complete();
            else
                PlayerToolPose.Interpolate(from, destination, Mathf.SmoothStep(0f, 1f, elapsed / duration), direction).Apply(animated);
        }

        /// <summary>Commits the exact endpoint before publishing completion to unlock and spawn rules.</summary>
        private void Complete()
        {
            // Exact endpoints prevent drift across repeated toggles.
            destination.Apply(animated);
            atB = !atB;
            running = false;
            returnPending = settings.AutoReturn && atB != settings.StartAtB;
            returnWait = settings.ReturnDelay;
            Item.Release(this);
            Signal(InteractionMoment.Completed);
        }

        #endregion

        #endregion
    }
}
