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
        [Tooltip("Persistent scene connection assigned when this animation is selected by Eject. Save the scene after assigning a new connection.")]
        [SerializeField]
        [HideInInspector]
        private string sceneIdentity = string.Empty;

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
        private bool destinationAtB;
        private bool returnPending;
        private bool automatic;
        private float returnWait;
        private float duration;
        private bool proximityInside;
        private bool proximityCompleted;
        private bool paused;
        private bool resetPending;
        private ObjectInteraction source;
        private bool interactionPending;
        private bool requestedCycle;
        private TriggerAnimationPlayback playback;
        private TriggerAnimationReset reset;

        #endregion

        #region Properties

        /// <summary>Locally applied endpoint configuration.</summary>
        public TriggerAnimationSettings Settings => settings;
        /// <summary>Persistent scene connection used by Eject prefabs.</summary>
        public string SceneIdentity => sceneIdentity;
        /// <summary>Whether a transition currently owns the transform.</summary>
        public bool IsRunning => running || (reset?.IsRunning ?? false);
        /// <summary>Whether the last completed endpoint is State B.</summary>
        public bool IsAtB => atB;
        /// <summary>Single card represented by this component.</summary>
        public override SingleInteractionKind Kind => SingleInteractionKind.TriggerAnimation;
        /// <summary>Only input activation modes subscribe to a player button.</summary>
        public override bool UsesInput => settings.UsesInput;
        /// <summary>Reach and aiming configuration used by the shared input driver.</summary>
        public override TransferTargetSettings Target => settings.Target;
        /// <summary>Animation length offered to automatic start-effect timing.</summary>
        internal override float VfxDuration => automatic ? settings.AutoReturnDuration : settings.Duration;
        /// <summary>Whether a start effect still belongs to an active transition.</summary>
        internal override bool VfxRunning => running || paused;
        /// <summary>Whether tool or interaction locks temporarily pause the current transition.</summary>
        internal override bool VfxPaused => paused || running && !Available(InteractionChannels.Animation);

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Registers input and initializes one validated target before its first interaction.</summary>
        protected override void OnEnable()
        {
            // Runtime initialization never changes a prefab while its edit workspace is open.
            base.OnEnable();
            if (Application.isPlaying && reset == null)
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
            foreach (ObjectTriggerAnimation interaction in FindObjectsByType<ObjectTriggerAnimation>(FindObjectsInactive.Include))
            {
                interaction.reset?.Dispose();
                if (interaction.isActiveAndEnabled)
                {
                    interaction.Release();
                    interaction.Initialize();
                }
            }
        }

        /// <summary>Captures the original pose and applies the configured initial endpoint.</summary>
        /// <param name="preservePose">Keep the live transform when releasing an externally driven reset.</param>
        private void Initialize(bool preservePose = false)
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
            if (!preservePose)
                (atB ? settings.StateB : settings.StateA).Apply(animated);
            initialized = true;
            SceneAnimationRegistry.Register(this);
            // Resolve once per activation; unrelated interaction events perform only a reference comparison.
            if (settings.Activation == TriggerAnimationActivation.Interaction)
            {
                source = settings.Source.Resolve(gameObject);
                Signaled -= Observe;
                Signaled += Observe;
            }
        }

        /// <summary>Restores only the transform owned by this animation and releases its temporary item reservation.</summary>
        private void Release()
        {
            SceneAnimationRegistry.Unregister(this);
            Signaled -= Observe;
            source = null;
            interactionPending = requestedCycle = false;
            playback?.Cancel();
            playback = null;
            // Disabling other interactions never restores unrelated parent or camera transforms.
            if (initialized && animated != null && reset == null)
                original.Apply(animated);
            Item?.Release(this);
            initialized = running = false;
            returnPending = automatic = false;
            proximityInside = proximityCompleted = paused = resetPending = false;
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
            if (configuration.Activation == TriggerAnimationActivation.Interaction && configuration.Source.Resolve(gameObject) == null)
            {
                warning = "Select an existing source interaction on the same GameObject as Trigger Animation.";
                return false;
            }
            Transform target = PlayerHierarchy.Resolve(transform, configuration.Path);
            if (target == null || Item == null || !Item.Owns(target))
                warning = "Choose an unambiguous transform owned by this object.";
            else if (target.TryGetComponent(out Rigidbody body) && !body.isKinematic)
                warning = "Animate a child transform or use a kinematic Rigidbody on the animation target.";
            return warning.Length == 0;
        }

        #endregion

        #region Execution

        /// <summary>Cancels the current cycle and prepares an event-free return driven by the day manager.</summary>
        /// <returns>A retained reset handle, or null when return settings or the target are invalid.</returns>
        internal TriggerAnimationReset ResetToInitial()
        {
            if (reset != null)
                return reset;
            // Day-end returns also use these values when ordinary Auto Return is disabled.
            if (!TryValidate(settings, out string warning) || !float.IsFinite(settings.AutoReturnDuration)
                || settings.AutoReturnDuration < 0f || !TransformRotation.IsValid(settings.AutoReturnRotation))
            {
                Debug.LogWarning("Day Flow cannot reset Trigger Animation: " + (warning.Length > 0 ? warning
                    : "Choose a finite non-negative Auto Return Duration and a supported Auto Return Rotation."), this);
                return null;
            }
            // Invalidate delayed Eject requests before clearing animation ownership.
            running = paused = returnPending = automatic = false;
            interactionPending = requestedCycle = resetPending = proximityInside = proximityCompleted = false;
            TriggerAnimationPlayback request = playback;
            playback = null;
            request?.Cancel();
            Item?.Release(this);
            elapsed = returnWait = 0f;
            Transform target = initialized && animated != null ? animated : PlayerHierarchy.Resolve(transform, settings.Path);
            reset = new TriggerAnimationReset(this, target);
            return reset;
        }

        /// <summary>Records reset arrival without publishing gameplay completion or scheduling another automatic cycle.</summary>
        /// <param name="request">Reset that reached its initial endpoint.</param>
        internal void CompleteReset(TriggerAnimationReset request)
        {
            if (reset == request)
                atB = destinationAtB = settings.StartAtB;
        }

        /// <summary>Allows normal activation again after the day transition or reset cancellation.</summary>
        /// <param name="request">Reset whose external owner has released it.</param>
        internal void ReleaseReset(TriggerAnimationReset request)
        {
            if (reset != request)
                return;
            reset = null;
            // A component re-enabled during its reset initializes only after the live return has finished.
            if (!initialized && isActiveAndEnabled)
                Initialize(true);
        }

        /// <summary>Checks local locks, current animation ownership and conflicts with root carrying.</summary>
        /// <returns>True when a new endpoint transition may start.</returns>
        internal override bool CanExecute()
        {
            // Repeated input during a transition cannot flip the destination halfway through it.
            return reset == null && initialized && animated != null && !running && !(playback?.AwaitingEffect ?? false) && Available(InteractionChannels.Animation)
                && Item != null && !Item.IsReserved && (animated != Item.transform || !Item.IsCarried);
        }

        /// <summary>Captures the live pose and begins travelling to the opposite completed endpoint.</summary>
        /// <returns>True when one transition successfully acquired its item.</returns>
        internal override bool Execute()
        {
            // Root motion blocks pickup; child-only motion can continue on an already carried item.
            if (requestedCycle)
                return false;
            return Begin(false);
        }

        /// <summary>Starts one externally requested transition whose completion can safely release an Eject effect.</summary>
        /// <param name="request">Receives a result tied to this transition, including zero-duration completion.</param>
        /// <param name="waitForReturn">Wait for the scheduled automatic return, if this transition has one.</param>
        /// <param name="forceAutoReturn">Hold an already reached endpoint until Eject requests the authored return.</param>
        /// <returns>True when the animation accepted ownership for this request.</returns>
        internal bool TryRequest(out TriggerAnimationPlayback request, bool waitForReturn = false, bool forceAutoReturn = false)
        {
            // Requests do not join unrelated motion or wait for a proximity entry boundary.
            request = null;
            if (paused || (playback?.Retained ?? false) || returnPending && atB == settings.StartAtB || !CanExecute())
                return false;
            playback = request = new TriggerAnimationPlayback(this, waitForReturn, forceAutoReturn && !waitForReturn);
            requestedCycle = true;
            interactionPending = false;
            // An already-open endpoint satisfies the prerequisite without toggling it back first.
            if (atB != settings.StartAtB)
            {
                if (request.HoldEndpoint)
                {
                    returnPending = false;
                    request.Complete(false);
                }
                else if (waitForReturn && settings.AutoReturn)
                {
                    if (!returnPending)
                        returnWait = settings.ReturnDelay;
                    returnPending = true;
                    request.Advance(settings.Duration + settings.ReturnDelay - returnWait);
                    request.Complete(true);
                }
                else
                    request.Complete(false);
                return true;
            }
            if (Begin(false))
                return true;
            request.Cancel();
            requestedCycle = false;
            return false;
        }

        /// <summary>Accepts one matching local event and defers only while another interaction owns the item.</summary>
        /// <param name="interaction">Interaction that emitted the event.</param>
        /// <param name="moment">Actual event phase emitted by that component.</param>
        private void Observe(ObjectInteraction interaction, InteractionMoment moment)
        {
            // Eject already requests this animation explicitly when its prerequisite is enabled.
            if (reset != null || interaction != source || moment != settings.Moment || !initialized || running || paused || returnPending || requestedCycle
                || !isActiveAndEnabled || IsLocked || !ToolAllowed || interaction is ObjectEject eject && eject.Settings.AnimateBeforeEject && eject.Prerequisite == this)
                return;
            interactionPending = true;
            if (CanExecute())
                Begin(false);
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
            interactionPending = false;
            returnPending = false;
            from = PlayerToolPose.Read(animated);
            if (Quaternion.Angle(Quaternion.Euler(from.Rotation), Quaternion.Euler((atB ? settings.StateB : settings.StateA).Rotation)) < 0.001f)
                from.Rotation = (atB ? settings.StateB : settings.StateA).Rotation;
            destinationAtB = returning ? settings.StartAtB : !atB;
            destination = destinationAtB ? settings.StateB : settings.StateA;
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
            // The day manager advances resets even when this component is disabled and prevents proximity retriggers.
            if (reset != null)
                return;
            // Destroying the animated branch cancels its prerequisite instead of retaining Eject indefinitely.
            if (initialized && animated == null)
            {
                Release();
                return;
            }
            if (initialized && !requestedCycle && settings.Activation == TriggerAnimationActivation.Proximity && Time.deltaTime > 0f)
                UpdateProximity();
            if (!requestedCycle && settings.Activation == TriggerAnimationActivation.Proximity
                && !proximityInside && settings.OnExit is TriggerAnimationExit.Pause or TriggerAnimationExit.Reset)
                return;
            // The owner's own reservation blocks competing animations, not this retained operation.
            if (animated == null || !Available(InteractionChannels.Animation) || Time.deltaTime <= 0f)
                return;
            if (!running)
            {
                if (interactionPending && CanExecute())
                    Begin(false);
                else if (returnPending && CanExecute())
                {
                    playback?.Advance(Mathf.Min(Time.deltaTime, Mathf.Max(0f, returnWait)));
                    returnWait -= Time.deltaTime;
                    if (returnWait <= 0f)
                        Begin(true);
                }
                return;
            }
            playback?.Advance(Mathf.Min(Time.deltaTime, Mathf.Max(0f, duration - elapsed)));
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
            atB = destinationAtB;
            running = false;
            returnPending = settings.AutoReturn && atB != settings.StartAtB && !(playback?.HoldEndpoint ?? false);
            returnWait = settings.ReturnDelay;
            Item.Release(this);
            playback?.Complete(returnPending);
            if (!returnPending && !(playback?.AwaitingEffect ?? false))
                requestedCycle = false;
            if (settings.Activation == TriggerAnimationActivation.Proximity && !returnPending)
                proximityCompleted = !settings.Loop || !proximityInside;
            Signal(InteractionMoment.Completed);
        }

        /// <summary>Releases a held request and optionally schedules a return from the live pose.</summary>
        /// <param name="request">Playback handle owned by the requesting Eject.</param>
        /// <param name="forceReturn">Whether to return to the configured initial endpoint regardless of Auto Return.</param>
        internal void ReleaseRequest(TriggerAnimationPlayback request, bool forceReturn)
        {
            if (playback != request)
                return;
            if (forceReturn && (running || atB != settings.StartAtB))
            {
                // Interrupt the current leg at its live pose; Begin(true) explicitly targets the initial endpoint.
                running = false;
                paused = false;
                Item?.Release(this);
                returnPending = true;
                returnWait = 0f;
                requestedCycle = true;
                playback = null;
                return;
            }
            if (!running && !returnPending)
                requestedCycle = false;
            playback = null;
        }

        #endregion

        #region Proximity

        /// <summary>Detects range boundaries without scene searches and advances one proximity cycle at a time.</summary>
        private void UpdateProximity()
        {
            Transform player = HoverObserver.ActivePlayer;
            float distance = proximityInside ? settings.ExitDistance : settings.ProximityDistance;
            bool inside = player != null && (player.position - transform.position).sqrMagnitude <= distance * distance;
            if (inside != proximityInside)
            {
                proximityInside = inside;
                if (!inside)
                {
                    ExitProximity();
                    return;
                }
                proximityCompleted = false;
                // A finishing cycle keeps ownership on reentry; paused cycles may explicitly resume.
                if (!running && !(returnPending && settings.OnExit is TriggerAnimationExit.Finish or TriggerAnimationExit.ReturnToStart)
                    && (settings.OnExit != TriggerAnimationExit.Pause || !settings.ResumeOnReentry || !paused && !returnPending))
                    resetPending = true;
            }
            if (resetPending)
            {
                if (!CanExecute())
                    return;
                ResetProximity();
            }
            if (!inside || proximityCompleted || !Available(InteractionChannels.Animation))
                return;
            // Resume only after reacquiring ownership released while the player was away.
            if (paused && CanExecute() && Item.Acquire(this, animated == Item.transform ? InteractionChannels.Grab : InteractionChannels.None))
            {
                paused = false;
                running = true;
            }
            else if (!paused && !running && !returnPending)
                Begin(false);
        }

        /// <summary>Applies the authored exit policy without publishing a false completion.</summary>
        private void ExitProximity()
        {
            // A visit that never acquired the item cannot reset another interaction's transform.
            if (!running && !paused && !returnPending && atB == settings.StartAtB)
            {
                resetPending = false;
                return;
            }
            switch (settings.OnExit)
            {
                case TriggerAnimationExit.Pause:
                    paused = running;
                    running = false;
                    Item.Release(this);
                    break;
                case TriggerAnimationExit.Reset:
                    if (Item.IsOwnedBy(this) || CanExecute())
                        ResetProximity();
                    else
                        resetPending = true;
                    break;
                case TriggerAnimationExit.ReturnToStart:
                    running = paused = returnPending = false;
                    Item.Release(this);
                    returnPending = true;
                    returnWait = 0f;
                    break;
                case TriggerAnimationExit.Finish:
                    break;
            }
        }

        /// <summary>Restores the initial endpoint before restarting a new proximity cycle.</summary>
        private void ResetProximity()
        {
            running = paused = returnPending = resetPending = false;
            Item.Release(this);
            atB = settings.StartAtB;
            (atB ? settings.StateB : settings.StateA).Apply(animated);
        }

        /// <summary>Draws entry and exit boundaries only for a selected proximity animation.</summary>
        private void OnDrawGizmosSelected()
        {
            if (settings == null || settings.Activation != TriggerAnimationActivation.Proximity)
                return;
            Gizmos.color = new Color(0.25f, 0.85f, 0.65f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, settings.ProximityDistance);
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, settings.ExitDistance);
        }

        #endregion

        #endregion
    }
}
