using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Moves an existing native controller from cached input and an immutable locomotion snapshot.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Player Studio/Character Controller Motor")]
    public sealed class PlayerCharacterControllerMotor : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Connection")]
        [Tooltip("Player Host on this GameObject, using the Character Controller binding and an assigned Locomotion preset.")]
        [SerializeField]
        private PlayerHost host;

        [Tooltip("Existing input bridge connected to this same Host. The motor consumes its cache without looking up actions or bindings.")]
        [SerializeField]
        private PlayerInputBridge input;

        #endregion

        #region Runtime State

        private PlayerLocomotionSettings settings;
        private CharacterController controller;
        private PlayerCameraRig cameraRig;
        private Vector3 requestedVelocity;
        private Vector3 actualVelocity;
        private PlayerJumpState jumpState;
        private readonly PlayerAudioMotion audio = new PlayerAudioMotion();
        private readonly PlayerFloatingMotion floating = new PlayerFloatingMotion();
        private float walkingStepOffset;
        private Vector3 moveVelocity;
        private bool wasGrounded;
        private float verticalVelocity;
        private CollisionFlags collisions;
        private bool initialized;
        private bool hasStarted;
        private bool reportedInvalidInput;
        private string initializationWarning = string.Empty;

        #endregion

        #region Properties

        /// <summary>Velocity requested by acceleration and braking, before collision response.</summary>
        public Vector3 RequestedVelocity => requestedVelocity;

        /// <summary>Displacement achieved by the last motor update divided by its duration; zero while idle or disabled.</summary>
        public Vector3 ActualVelocity => actualVelocity;

        /// <summary>Contact below reported by the most recent Move while not ascending; not a predictive ground probe.</summary>
        public bool IsGrounded => (collisions & CollisionFlags.Below) != 0 && verticalVelocity <= 0f;

        /// <summary>Collision directions from the single combined movement request.</summary>
        public CollisionFlags Collisions => collisions;

        /// <summary>Integrated vertical speed: positive while rising, negative while falling, zero on support.</summary>
        public float VerticalVelocity => verticalVelocity;

        /// <summary>Most recent initialization issue, retained for Editor diagnostics.</summary>
        public string InitializationWarning => initializationWarning;
        /// <summary>Whether external sources currently replace locomotion with weightless body simulation.</summary>
        public bool IsGravitySuspended => floating.Active;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Waits for component initialization before capturing the configured body and locomotion.</summary>
        private void Start()
        {
            // Awake on the host has already configured the existing CharacterController.
            hasStarted = true;
            InitializeAndReport();
        }

        /// <summary>Reinitializes a motor that has already completed its first activation.</summary>
        private void OnEnable()
        {
            // First activation waits for Start to avoid depending on component order in the prefab.
            if (hasStarted)
                InitializeAndReport();
        }

        /// <summary>Stops the motor without enabling, disabling or destroying the controller or input owner.</summary>
        private void OnDisable()
        {
            // Re-enabling begins from rest instead of replaying a previous velocity.
            initialized = false;
            RestoreStepOffset();
            floating.Reset();
            ResetMotion();
        }

        /// <summary>Runs one native movement request per active frame, with no asset reads or action lookups.</summary>
        private void Update()
        {
            // Disabled or missing dependencies must not retain motion for their next activation.
            if (!initialized)
                return;
            if (Time.deltaTime <= 0f)
            {
                input?.ConsumeJump();
                jumpState.Reset();
                return;
            }
            if (controller == null || !controller.enabled)
            {
                ResetMotion();
                return;
            }

            // A custom input processor may produce invalid numbers; report once per invalid interval.
            Vector2 command = input != null ? input.Movement : Vector2.zero;
            if (!float.IsFinite(command.x) || !float.IsFinite(command.y))
            {
                ResetMotion();
                if (!reportedInvalidInput)
                    Debug.LogWarning("Movement input must contain finite values. The motor is paused until valid input returns.", this);
                reportedInvalidInput = true;
                return;
            }

            reportedInvalidInput = false;
            PlayerButtonSignal jump = input != null ? input.ConsumeJump() : default;
            if (!jump.IsActive || jump.Interrupted)
                jumpState.Reset();
            if (!floating.Active && jump.IsActive && jumpState.TryStart(jump.Pressed, IsGrounded, Time.timeAsDouble, settings.Jump))
                verticalVelocity = settings.Jump.LaunchSpeed;

            // Convert intent before acceleration so stored velocity always remains in world space.
            if (cameraRig != null && cameraRig.Host == host && cameraRig.MovementFrame != PlayerMovementFrame.World)
            {
                Quaternion frame = cameraRig.MovementFrame == PlayerMovementFrame.Camera ? cameraRig.Heading
                    : Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                Vector3 world = frame * new Vector3(command.x, 0f, command.y);
                command = new Vector2(world.x, world.z);
            }
            Vector3 displacement;
            if (floating.Active)
            {
                Vector3 direction = new Vector3(command.x, 0f, command.y);
                if (floating.Settings.FollowCameraPitch && cameraRig != null && cameraRig.View != null)
                    direction = cameraRig.View.transform.rotation * new Vector3(input.Movement.x, 0f, input.Movement.y);
                displacement = floating.Advance(direction, jump, Time.deltaTime);
                requestedVelocity = new Vector3(floating.Velocity.x, 0f, floating.Velocity.z);
                verticalVelocity = floating.Velocity.y;
            }
            else
            {
                displacement = PlayerPlanarMotion.Advance(ref requestedVelocity, command, settings, Time.deltaTime);
                displacement.y = PlayerVerticalMotion.Advance(ref verticalVelocity, IsGrounded, settings.Gravity, Time.deltaTime);
            }
            actualVelocity = Vector3.zero;
            wasGrounded = IsGrounded;
            collisions = CollisionFlags.None;
            if (!float.IsFinite(displacement.x) || !float.IsFinite(displacement.y) || !float.IsFinite(displacement.z))
            {
                initializationWarning = "Movement exceeded the supported numeric range. Review the preset and explicitly reinitialize the motor.";
                initialized = false;
                ResetMotion();
                Debug.LogWarning(initializationWarning, this);
                return;
            }

            if (displacement.Equals(Vector3.zero))
                return;

            // Only CharacterController resolves collision and writes the root; no Transform teleport is used.
            Vector3 previousPosition = transform.position;
            moveVelocity = displacement / Time.deltaTime;
            collisions = controller.Move(displacement);
            if (floating.Active)
                verticalVelocity = floating.Velocity.y;
            else if (IsGrounded || ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f))
                verticalVelocity = 0f;
            actualVelocity = (transform.position - previousPosition) / Time.deltaTime;
            audio.Tick(actualVelocity, !floating.Active && IsGrounded, cameraRig != null && cameraRig.HeadTiltActive);
        }

        #endregion

        #region Configuration

        /// <summary>Captures configuration at activation and reports one initialization warning on failure.</summary>
        private void InitializeAndReport()
        {
            // Reuse the explicit initialization path for first activation and later re-enabling.
            if (!TryInitialize(out string warning))
                Debug.LogWarning(warning, this);
        }

        /// <summary>Refreshes the runtime snapshot after an explicit configuration change; starts from rest.</summary>
        /// <param name="warning">Receives the first missing reference or incompatibility.</param>
        /// <returns>True when this active motor is ready to consume its assigned input during Play.</returns>
        public bool TryInitialize(out string warning)
        {
            RestoreStepOffset();
            floating.Reset();
            // Invalid setup stays disabled internally; do not add components or repair presets automatically here.
            ResetMotion();
            initialized = false;
            warning = string.Empty;
            if (!Application.isPlaying || !isActiveAndEnabled || host == null || host.gameObject != gameObject
                || host.BodyBinding != PlayerBodyBinding.CharacterController || host.BodyController == null
                || input == null || input.Host != host || host.MasterPreset == null || host.MasterPreset.LocomotionPreset == null)
                warning = "Connect this root's native Player Host, its Input Bridge and a Locomotion preset before starting the motor.";
            else if (host.TryGetBodySettings(out _, out warning)
                && host.MasterPreset.LocomotionPreset.TryGetSettings(out settings, out warning))
            {
                // Jumping requires an explicit Button mapping; a Body-only player may keep it unassigned.
                if (settings.Jump.Enabled && (host.MasterPreset.InputPreset == null
                    || !host.MasterPreset.InputPreset.TryGetJumpId(out System.Guid jumpId, out warning)
                    || jumpId == System.Guid.Empty))
                    warning = "Enable Jump only after assigning a Button action to the Input preset's Jump field.";
                else
                {
                    controller = host.BodyController;
                    // The optional rig shares this root; cache it once without a circular serialized dependency.
                    cameraRig = GetComponent<PlayerCameraRig>();
                    audio.Bind(host.MasterPreset.LocomotionPreset.Audio, transform);
                    initialized = true;
                }
            }

            initializationWarning = warning;
            return initialized;
        }

        #endregion

        #region Audio

        /// <summary>Receives the active camera's footstep phase without a separate timing approximation.</summary>
        internal void PlayFootstep()
        {
            // A disabled motor cannot emit camera-driven walking sounds.
            if (initialized && isActiveAndEnabled && !floating.Active)
                audio.Step();
        }

        /// <summary>Uses contacts already generated by the native controller movement.</summary>
        /// <param name="hit">Actual controller contact.</param>
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            floating.Contact(hit.normal, hit.rigidbody);
            // Capture requested impact speed before collision response reduces achieved motion.
            if (initialized)
                audio.Contact(hit, moveVelocity, wasGrounded);
        }

        #endregion

        #region Motion State

        /// <summary>Temporarily replaces gravity with a leased virtual-body simulation using the existing controller.</summary>
        /// <param name="owner">Source that must release the suspension when its effect ends.</param>
        /// <param name="configuration">Virtual mass, damping, controls and contact policy.</param>
        /// <param name="force">World impulse for zero duration, otherwise world acceleration.</param>
        /// <param name="duration">Sustained-force seconds; zero applies one impulse.</param>
        /// <returns>True when the initialized motor acquired this source.</returns>
        public bool SuspendGravity(Object owner, PlayerFloatSettings configuration, Vector3 force, float duration)
        {
            // Invalid requests leave ordinary locomotion and other suspension owners intact.
            if (!initialized || !isActiveAndEnabled || controller == null || !controller.enabled || owner == null
                || configuration == null || !configuration.TryValidate(out _) || !float.IsFinite(duration) || duration < 0f
                || !float.IsFinite(force.x) || !float.IsFinite(force.y) || !float.IsFinite(force.z))
                return false;
            Vector3 initial = actualVelocity;
            if (IsGrounded)
                initial.y = 0f;
            jumpState.Reset();
            if (!floating.Active)
            {
                walkingStepOffset = controller.stepOffset;
                controller.stepOffset = 0f;
            }
            return floating.Acquire(owner, configuration, initial, force, duration);
        }

        /// <summary>Restores normal movement only when this was the final active suspension source.</summary>
        /// <param name="owner">Source whose pulse has ended or been cancelled.</param>
        public void RestoreGravity(Object owner)
        {
            bool suspended = floating.Active;
            if (!floating.Release(owner, out Vector3 velocity))
                return;
            if (suspended && controller != null)
                controller.stepOffset = walkingStepOffset;
            requestedVelocity = new Vector3(velocity.x, 0f, velocity.z);
            verticalVelocity = settings.Gravity.Enabled ? Mathf.Max(-settings.Gravity.TerminalSpeed, velocity.y) : 0f;
            collisions = CollisionFlags.None;
            jumpState.Reset();
        }

        /// <summary>Restores ground stepping before discarding a suspended motor's state.</summary>
        private void RestoreStepOffset()
        {
            if (floating.Active && controller != null)
                controller.stepOffset = walkingStepOffset;
        }

        /// <summary>Clears requested and achieved velocity without changing scene position.</summary>
        private void ResetMotion()
        {
            // Stopping the motor must not leave a stale value in its diagnostics.
            jumpState.Reset();
            input?.ConsumeJump();
            verticalVelocity = 0f;
            collisions = CollisionFlags.None;
            requestedVelocity = Vector3.zero;
            actualVelocity = Vector3.zero;
        }

        #endregion

        #endregion
    }
}
