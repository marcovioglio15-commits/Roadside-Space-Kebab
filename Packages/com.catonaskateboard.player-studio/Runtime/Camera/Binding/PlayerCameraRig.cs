using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Follows one player with an existing camera, using a cached preset and explicitly assigned input.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Player Studio/Player Camera Rig")]
    public sealed class PlayerCameraRig : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Connection")]
        [Tooltip("Player on this GameObject whose Camera slot supplies the view configuration.")]
        [SerializeField]
        private PlayerHost host;

        [Tooltip("Existing camera moved by this rig. It must not be the player root or an ancestor of it.")]
        [SerializeField]
        private Camera view;

        [Tooltip("Existing input owner used to resolve optional look and cursor action IDs.")]
        [SerializeField]
        private PlayerInput input;

        [Header("Targets")]
        [Tooltip("Optional camera focus anchor. Empty uses the player root plus the preset target offset.")]
        [SerializeField]
        private Transform target;

        [Tooltip("Optional managed model used for visual yaw and first-person visibility.")]
        [SerializeField]
        private PlayerVisualBinding visual;

        [Tooltip("Optional motor supplying achieved movement for model facing.")]
        [SerializeField]
        private PlayerCharacterControllerMotor motor;

        #endregion

        #region State

        private readonly PlayerCameraInput commands = new PlayerCameraInput();
        private readonly PlayerCameraObstacle obstacles = new PlayerCameraObstacle();
        private readonly PlayerCameraVisibility visibility = new PlayerCameraVisibility();
        private PlayerCameraSettings settings;
        private Quaternion authoredModelRotation;
        private float initialHeading;
        private Vector2 angles;
        private PlayerCameraLookState look;
        private PlayerCameraFollowState follow;
        private Quaternion orbit;
        private bool initialized;
        private bool hasStarted;
        private bool capturesCursor;
        private bool ownsCursor;
        private bool inputFocused = true;
        private bool previousVisible;
        private CursorLockMode previousLock;
        private string initializationWarning = string.Empty;

        #endregion

        #region Properties

        /// <summary>Player that owns this camera configuration.</summary>
        public PlayerHost Host => host;
        /// <summary>Existing camera configured by Editor Apply and followed during Play.</summary>
        public Camera View => view;
        /// <summary>Optional focus anchor; null means the host transform.</summary>
        public Transform Target => target;
        /// <summary>The movement frame captured at activation.</summary>
        public PlayerMovementFrame MovementFrame => initialized ? settings.MovementFrame : PlayerMovementFrame.World;
        /// <summary>Horizontal view heading used by the motor without querying camera transforms repeatedly.</summary>
        public Quaternion Heading => Quaternion.Euler(0f, settings.Mode == PlayerCameraMode.Fixed && view != null
            ? view.transform.eulerAngles.y : settings.Mode == PlayerCameraMode.ThirdPerson
                && settings.Follow == PlayerCameraFollow.Damped && settings.DampOrbit ? orbit.eulerAngles.y : initialHeading + angles.x, 0f);
        /// <summary>Last initialization warning, suitable for Inspector diagnostics.</summary>
        public string InitializationWarning => initializationWarning;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Initializes after PlayerInput and the host have completed their Awake and OnEnable calls.</summary>
        private void Start()
        {
            // First activation waits for the existing input owner; no runtime objects are created.
            hasStarted = true;
            InitializeAndReport();
        }

        /// <summary>Reinitializes a rig explicitly re-enabled during Play.</summary>
        private void OnEnable()
        {
            // Initial activation is handled by Start to avoid ordering dependencies.
            if (hasStarted)
                InitializeAndReport();
        }

        /// <summary>Releases this rig's subscriptions and restores cursor and renderer state.</summary>
        private void OnDisable()
        {
            // The input asset, camera object and model remain owned by the player hierarchy.
            initialized = false;
            commands.Dispose();
            visibility.Dispose();
            RestoreCursor();
        }

        /// <summary>Draws an optional centered pointer only while this rig owns active capture.</summary>
        private void OnGUI()
        {
            // Released focus and disabled views must never leave a floating marker over another screen.
            if (initialized && capturesCursor && settings.ShowCenteredCursor && Application.isFocused
                && Cursor.lockState == CursorLockMode.Locked && view != null && view.isActiveAndEnabled)
                PlayerCenteredCursor.Draw(view, settings.CursorTexture);
        }

        /// <summary>Consumes look before movement so the motor uses this frame's camera heading.</summary>
        private void Update()
        {
            // Disabled cameras do not capture cursor input or move presentation transforms.
            if (!initialized || host == null)
                return;
            if (!inputFocused)
            {
                DiscardLook();
                return;
            }
            if (Time.deltaTime <= 0f)
            {
                DiscardLook();
                return;
            }
            if (view == null || !view.isActiveAndEnabled)
            {
                DiscardLook();
                RestoreCursor();
                return;
            }
            if (settings.LookEnabled && settings.Mode != PlayerCameraMode.Fixed)
            {
                Vector2 change = commands.Read(settings, Time.deltaTime, out bool toggle, out bool interrupted);
                if (interrupted)
                    look.DiscardPending();
                if (toggle && settings.LockCursor)
                {
                    SetCursor(!capturesCursor);
                    look.DiscardPending();
                    return;
                }
                if ((!settings.LockCursor || capturesCursor && Cursor.lockState == CursorLockMode.Locked)
                    && float.IsFinite(change.x) && float.IsFinite(change.y))
                    angles = look.Advance(change, settings, Time.deltaTime);
                else
                    look.DiscardPending();
            }
        }

        /// <summary>Clears pending motion when the application loses or regains pointer focus.</summary>
        /// <param name="hasFocus">Current application focus, supplied by Unity.</param>
        private void OnApplicationFocus(bool hasFocus)
        {
            // Neither accumulated input nor a filtered tail should survive an Alt-Tab transition.
            DiscardLook();
        }

        /// <summary>Stops queued input and filtered rotation at a camera interruption boundary.</summary>
        private void DiscardLook()
        {
            // Input displacement and displayed motion must restart together after a pause or focus change.
            commands.DiscardPending();
            look.DiscardPending();
        }

        /// <summary>Follows the final player and model pose after movement, without delaying pointer rotation.</summary>
        private void LateUpdate()
        {
            // Input is already consumed; following never reads it again or waits for a physics tick.
            if (!initialized || host == null || view == null || !view.isActiveAndEnabled || Time.deltaTime <= 0f)
                return;
            UpdateVisualFacing();
            Vector3 focus = (target != null ? target : host.transform).TransformPoint(settings.TargetOffset);
            Vector3 followedFocus = settings.Mode != PlayerCameraMode.Fixed && settings.Follow == PlayerCameraFollow.Damped
                ? follow.Advance(focus, settings.FollowResponse, Time.deltaTime) : focus;
            CalculatePose(settings, followedFocus, initialHeading, angles, out Vector3 position, out Quaternion rotation);
            // Orbit damping is optional and independent from following the player's translation.
            if (settings.Mode == PlayerCameraMode.ThirdPerson && settings.Follow == PlayerCameraFollow.Damped && settings.DampOrbit)
            {
                orbit = Quaternion.Slerp(orbit, rotation, 1f - Mathf.Exp(-settings.FollowResponse * Time.deltaTime));
                rotation = orbit;
                position = followedFocus - rotation * Vector3.forward * settings.Distance;
            }
            else
                orbit = rotation;
            // Collision uses the real player focus; smoothing never lengthens an obstructed camera arm.
            if (settings.Mode == PlayerCameraMode.ThirdPerson && settings.AvoidObstacles)
                position = obstacles.Resolve(host, settings, focus, position, Time.deltaTime);
            view.transform.SetPositionAndRotation(position, rotation);
        }

        #endregion

        #region Configuration

        /// <summary>Reports one configuration warning when activation cannot complete.</summary>
        private void InitializeAndReport()
        {
            // Runtime never polls invalid presets looking for changes.
            if (!TryInitialize(out string warning))
                Debug.LogWarning(warning, this);
        }

        /// <summary>Captures the current preset after an explicit runtime configuration change.</summary>
        /// <param name="warning">Receives invalid references or camera settings.</param>
        /// <returns>True when this rig is ready to follow its player.</returns>
        public bool TryInitialize(out string warning)
        {
            // Reinitialization is explicit and releases prior callbacks before reading new roles.
            initialized = false;
            commands.Dispose();
            visibility.Dispose();
            RestoreCursor();
            if (!TryValidateSetup(out warning) || host.MasterPreset.CameraPreset == null
                || !host.MasterPreset.CameraPreset.TryGetSettings(out settings, out warning))
            {
                initializationWarning = warning.Length > 0 ? warning : "Assign a Camera preset to the player master.";
                warning = initializationWarning;
                return false;
            }
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                warning = initializationWarning = "Camera runtime initialization requires an active rig in Play mode.";
                return false;
            }
            if (settings.LookEnabled && settings.Mode != PlayerCameraMode.Fixed
                && !commands.Connect(input, host.MasterPreset.InputPreset, out warning))
            {
                initializationWarning = warning;
                return false;
            }

            ApplyConfiguration(settings);
            initialHeading = host.transform.eulerAngles.y;
            angles = settings.InitialAngles;
            look.Reset(angles);
            if (visual != null && visual.VisualRoot != null && host.MasterPreset.VisualPreset != null
                && host.MasterPreset.VisualPreset.TryGetSettings(out PlayerVisualSettings modelSettings, out _))
                authoredModelRotation = modelSettings.Rotation * visual.BaseMatrix.rotation;
            if (settings.Mode == PlayerCameraMode.FirstPerson && settings.HideVisualInFirstPerson)
                visibility.Connect(view, visual != null ? visual.VisualRoot : null);
            if (inputFocused && settings.Mode != PlayerCameraMode.Fixed && settings.LookEnabled && settings.LockCursor)
                SetCursor(true);
            initialized = true;
            initializationWarning = warning = string.Empty;
            return true;
        }

        /// <summary>Checks references shared by runtime initialization and Editor confirmation.</summary>
        /// <param name="warning">Receives an unsafe hierarchy or missing dependency.</param>
        /// <returns>True when moving this camera cannot move the player's root or target.</returns>
        public bool TryValidateSetup(out string warning)
        {
            // Explicit references avoid camera searches or Camera.main lookups during gameplay.
            warning = string.Empty;
            if (host == null || host.gameObject != gameObject || host.MasterPreset == null || view == null)
                warning = "Connect this root's Player Host, its master and an existing camera.";
            else if (host.transform.IsChildOf(view.transform) || (target != null && target.IsChildOf(view.transform)))
                warning = "The camera must not contain the player or the focus target in its hierarchy.";
            else if (visual != null && visual.Host != host)
                warning = "Camera visual binding must belong to the same player.";
            return warning.Length == 0;
        }

        /// <summary>Applies validated lens and initial pose values; Editor callers must register Undo first.</summary>
        /// <param name="configuration">Validated preset snapshot.</param>
        public void ApplyConfiguration(PlayerCameraSettings configuration)
        {
            // This also supplies immediate Edit-mode feedback after a confirmed camera preset edit.
            view.fieldOfView = configuration.FieldOfView;
            view.nearClipPlane = configuration.NearClip;
            view.farClipPlane = configuration.FarClip;
            view.orthographic = false;
            Vector3 focus = (target != null ? target : host.transform).TransformPoint(configuration.TargetOffset);
            CalculatePose(configuration, focus, host.transform.eulerAngles.y, configuration.InitialAngles,
                out Vector3 position, out Quaternion rotation);
            follow.Reset(focus);
            obstacles.Reset();
            orbit = rotation;
            view.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Computes the camera pose without moving objects or reading input.</summary>
        /// <param name="configuration">Validated mode, offset and distance values.</param>
        /// <param name="focus">World target after applying the configured target offset.</param>
        /// <param name="heading">Starting player yaw in degrees.</param>
        /// <param name="look">Current yaw offset and pitch.</param>
        /// <param name="position">Receives the desired camera world position.</param>
        /// <param name="rotation">Receives the desired world rotation.</param>
        public static void CalculatePose(PlayerCameraSettings configuration, Vector3 focus, float heading, Vector2 look,
            out Vector3 position, out Quaternion rotation)
        {
            // The same calculation is used for Editor Apply and runtime initialization.
            rotation = Quaternion.Euler(look.y, heading + look.x, 0f);
            position = configuration.Mode switch
            {
                PlayerCameraMode.FirstPerson => focus,
                PlayerCameraMode.ThirdPerson => focus - rotation * Vector3.forward * configuration.Distance,
                _ => configuration.FixedPosition
            };
            if (configuration.Mode == PlayerCameraMode.Fixed)
                rotation = configuration.FixedTrackTarget && (focus - position).sqrMagnitude > 0.000001f
                    ? Quaternion.LookRotation(focus - position, Vector3.up) : Quaternion.Euler(configuration.FixedEuler);
        }

        #endregion

        #region Presentation

        /// <summary>Suspends view input and cursor capture when an owning viewport loses focus.</summary>
        /// <param name="focused">Whether this player's viewport currently accepts input.</param>
        public void SetInputFocus(bool focused)
        {
            // Focus ownership also clears buffered pointer motion and the unfinished smoothing tail.
            inputFocused = focused;
            DiscardLook();
            if (!focused)
                RestoreCursor();
            else if (initialized && settings.Mode != PlayerCameraMode.Fixed && settings.LookEnabled && settings.LockCursor)
                SetCursor(true);
        }

        /// <summary>Turns the model independently of collider orientation, using a cached authored correction.</summary>
        private void UpdateVisualFacing()
        {
            // No model search is performed during camera updates.
            if (visual == null || visual.VisualRoot == null || settings.ModelFacing == PlayerModelFacing.Authored)
                return;
            Vector3 forward = settings.ModelFacing == PlayerModelFacing.Camera ? Heading * Vector3.forward
                : motor != null ? motor.ActualVelocity : Vector3.zero;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.000001f)
                return;
            visual.VisualRoot.rotation = Quaternion.RotateTowards(visual.VisualRoot.rotation,
                Quaternion.LookRotation(forward, Vector3.up) * authoredModelRotation, settings.TurnSpeed * Time.deltaTime);
        }

        /// <summary>Changes cursor capture only after activation or its assigned toggle action.</summary>
        /// <param name="capture">Whether this camera should capture pointer movement.</param>
        private void SetCursor(bool capture)
        {
            // Cursor state is process-wide; the original state is restored when this rig releases ownership.
            if (capture && !ownsCursor)
            {
                previousLock = Cursor.lockState;
                previousVisible = Cursor.visible;
                ownsCursor = true;
            }
            capturesCursor = capture;
            if (!capture)
                look.DiscardPending();
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }

        /// <summary>Restores the cursor state that existed before this rig activated.</summary>
        private void RestoreCursor()
        {
            // A rig that never captured the cursor must not overwrite another system's state.
            if (!ownsCursor)
                return;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
            ownsCursor = false;
            capturesCursor = false;
        }

        #endregion

        #endregion
    }
}
