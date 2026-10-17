using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Connects object-local hover detection and style to the scene's shared label.</summary>
    [AddComponentMenu("Objects Logic Studio/Object Hover")]
    public sealed class ObjectHover : ObjectInteraction
    {
        #region Serialized Fields

        [Header("Binding")]
        [Tooltip("Optional anchor inside this prefab. Empty uses this object's transform and Anchor Offset.")]
        [SerializeField]
        private Transform anchor;

        [Tooltip("Optional reusable source. Import copies detection, animation and label appearance into this interaction.")]
        [SerializeField]
        private HoverPreset preset;

        [Tooltip("Local Hover snapshot. Import copies a preset here; later preset edits do not change this interaction.")]
        [SerializeField]
        private HoverConfiguration configuration = new HoverConfiguration();

        [Header("Debug")]
        [Tooltip("Draw the anchor, player range and label world offset only while this object is selected.")]
        [SerializeField]
        private bool drawGizmos = true;

        #endregion

        #region State

        private Collider[] colliders;
        private bool ready;
        private float nextQuery;
        private bool hovered;
        private bool detected;
        private float hitDistance;
        private bool eligible;
        private bool carrySuppressed;
        private HoverSettings settings;
        private Rigidbody body;
        private ObjectGrab grab;
        private Vector3 previousPosition;
        private float previousTime;
        private float lastDetected;
        private float speed;
        private bool moving;

        #endregion

        #region Properties

        /// <summary>Current settings, reread after an explicit Refresh call during Play.</summary>
        public HoverSettings Settings => Application.isPlaying && settings != null ? settings
            : configuration != null ? configuration.Settings : null;
        /// <summary>Saved configuration currently assigned to this interaction.</summary>
        public HoverPreset Preset => preset;
        /// <summary>Local settings snapshot, independent of the last selected reusable preset.</summary>
        public HoverConfiguration Configuration => configuration;
        /// <summary>Optional local hierarchy anchor retained independently of shared presets.</summary>
        public Transform Anchor => anchor;
        /// <summary>Current world point used for range and view-center tests.</summary>
        public Vector3 WorldAnchor => (anchor != null ? anchor : transform).TransformPoint(Settings.AnchorOffset);
        /// <summary>Whether this interaction owns an active hover after selection and availability checks.</summary>
        public bool IsHovered => hovered;
        /// <summary>Latest detection result, distinguishing a visible candidate from release-delay grace.</summary>
        internal bool Detected => detected;
        /// <summary>Squared distance of the latest collider hit, used to choose the frontmost centre target.</summary>
        internal float HitDistance => hitDistance;
        /// <summary>Whether editor debug geometry is requested.</summary>
        public bool DrawGizmos => drawGizmos;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Captures owned colliders once when an interaction becomes active.</summary>
        private void OnEnable()
        {
            // Components registered before the observer starts need no scene searches.
            Refresh();
            HoverRegistry.Register(this);
        }

        /// <summary>Removes the interaction and its visible text immediately.</summary>
        private void OnDisable()
        {
            // Unregistration also supports pooled or destroyed prefab instances.
            Hide();
            HoverRegistry.Unregister(this);
        }

        #endregion

        #region Configuration

        /// <summary>Revalidates an explicit runtime edit and recaches colliders after hierarchy changes.</summary>
        public void Refresh()
        {
            // Discovery and warnings happen at activation or an explicit refresh, never per frame.
            Hide();
            colliders = GetComponentsInChildren<Collider>(true);
            body = GetComponentInParent<Rigidbody>();
            grab = GetComponentInParent<ObjectGrab>();
            previousPosition = transform.position;
            previousTime = Time.time;
            speed = 0f;
            moving = false;
            carrySuppressed = grab != null && grab.IsHeld && !grab.Settings.ShowHover;
            ready = TryValidate(out string warning);
            if (ready)
                settings = new HoverSettings(configuration.Settings);
            nextQuery = 0f;
            if (!ready)
                Debug.LogWarning(warning, this);
        }

        /// <summary>Checks detection and local style without requiring UI on the object.</summary>
        /// <param name="warning">Receives the first issue preventing this interaction from running.</param>
        /// <returns>True when the interaction has valid settings and references.</returns>
        public bool TryValidate(out string warning)
        {
            // Settings validation deliberately leaves invalid authored values unchanged.
            warning = string.Empty;
            if (configuration == null)
                warning = "Configure Hover settings using Objects Logic Studio.";
            else if (anchor != null && !anchor.IsChildOf(transform))
                warning = "The hover anchor must belong to this object's prefab hierarchy.";
            else if (!configuration.TryValidate(out warning))
                return false;
            else if (configuration.Settings.TargetMode != HoverDetectionMode.ViewCenter && GetComponentInChildren<Collider>(true) == null
                && GetComponent<ObjectAssemblyProduct>() == null)
                warning = "Collider hover requires a 3D collider on this object or one of its children.";

            return warning.Length == 0;
        }

        #endregion

        #region Evaluation

        /// <summary>Queries eligibility without emitting events before the registry chooses a centre target.</summary>
        /// <param name="observer">Shared observer supplying camera, player and reusable query buffers.</param>
        /// <param name="time">Current unscaled time.</param>
        /// <returns>True for a detected candidate or the previous winner within its release delay.</returns>
        internal bool Evaluate(HoverObserver observer, float time)
        {
            // Invalid initialization leaves the component dormant until Refresh.
            if (!ready)
            {
                Hide();
                return false;
            }
            UpdateMotion();
            bool suspended = carrySuppressed || settings.SuspendCarried && grab != null && grab.IsHeld
                || moving || !Available(InteractionChannels.Hover);
            if (suspended)
            {
                detected = false;
                nextQuery = 0f;
            }
            else if (time >= nextQuery)
            {
                nextQuery = time + settings.QueryInterval;
                detected = observer.Evaluate(this, colliders, out hitDistance);
                if (detected)
                    lastDetected = time;
            }
            eligible = !suspended && (detected || hovered && time - lastDetected < settings.ReleaseDelay);
            return eligible;
        }

        /// <summary>Commits selection and updates entry or exit animation after competing hovers are resolved.</summary>
        /// <param name="selected">Whether this candidate may activate after centre arbitration.</param>
        internal void Present(bool selected)
        {
            // Callbacks on another selected interaction may have removed this component's UI or availability.
            if (!isActiveAndEnabled || !ready)
            {
                Hide();
                return;
            }
            bool previous = hovered;
            hovered = selected && eligible && Available(InteractionChannels.Hover);
            if (hovered && !previous)
            {
                Signal(InteractionMoment.Started);
                Signal(InteractionMoment.Completed);
            }
            // A rule may lock or replace this hover synchronously from its own start event.
            if (this == null || !isActiveAndEnabled)
                return;
            if (!Available(InteractionChannels.Hover))
                hovered = false;
        }

        /// <summary>Tracks physics or authored motion with hysteresis around the configured suspension speed.</summary>
        private void UpdateMotion()
        {
            // Dynamic bodies provide continuous velocity between physics ticks; scripted motion uses the live transform.
            if (!settings.SuspendMoving)
                return;
            float elapsed = Time.time - previousTime;
            if (elapsed <= 0f)
                return;
            float measured = body != null && !body.isKinematic ? body.linearVelocity.magnitude
                : Vector3.Distance(transform.position, previousPosition) / elapsed;
            previousPosition = transform.position;
            previousTime = Time.time;
            speed = Mathf.Lerp(speed, measured, 1f - Mathf.Exp(-elapsed / 0.04f));
            moving = speed > settings.SpeedThreshold * (moving ? 0.85f : 1f);
        }

        /// <summary>Clears stale detection whenever the observing camera or player becomes unavailable.</summary>
        internal void Hide()
        {
            // Reset the query deadline so reacquisition does not wait for an old interval.
            hovered = false;
            detected = false;
            eligible = false;
            nextQuery = 0f;
            HoverRegistry.Release(this);
        }

        /// <summary>Applies carry-only visibility without changing this component's authored enabled state.</summary>
        /// <param name="suppressed">Whether a carried object requests hidden hover labels.</param>
        internal void SetCarrySuppressed(bool suppressed)
        {
            // Releasing suppression schedules fresh detection rather than reusing a stale hovered result.
            if (carrySuppressed == suppressed)
                return;
            carrySuppressed = suppressed;
            nextQuery = 0f;
        }

        #endregion

        #endregion
    }
}
