using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Connects one independently configured hover interaction to its prefab anchor and authored label.</summary>
    [AddComponentMenu("Objects Logic Studio/Object Hover")]
    public sealed class ObjectHover : ObjectInteraction
    {
        #region Serialized Fields

        [Header("Binding")]
        [Tooltip("Optional anchor inside this prefab. Empty uses this object's transform and Anchor Offset.")]
        [SerializeField]
        private Transform anchor;

        [Tooltip("Dedicated existing label for this interaction. Create it with Objects Logic Studio before Play.")]
        [SerializeField]
        private HoverLabel label;

        [Tooltip("Reusable detection, animation and label appearance. Each interaction can select its own preset.")]
        [SerializeField]
        private HoverPreset preset;

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
        private bool carrySuppressed;
        private HoverSettings settings;

        #endregion

        #region Properties

        /// <summary>Current settings, reread after an explicit Refresh call during Play.</summary>
        public HoverSettings Settings => Application.isPlaying && settings != null ? settings
            : preset != null && preset.Configuration != null ? preset.Configuration.Settings : null;
        /// <summary>Saved configuration currently assigned to this interaction.</summary>
        public HoverPreset Preset => preset;
        /// <summary>Optional local hierarchy anchor retained independently of shared presets.</summary>
        public Transform Anchor => anchor;
        /// <summary>Dedicated UI used by this interaction.</summary>
        public HoverLabel Label => label;
        /// <summary>Current world point used for range and view-center tests.</summary>
        public Vector3 WorldAnchor => (anchor != null ? anchor : transform).TransformPoint(Settings.AnchorOffset);
        /// <summary>Whether the latest query passed every eligibility check.</summary>
        public bool IsHovered => hovered;
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
            ready = TryValidate(out string warning);
            if (ready)
            {
                settings = new HoverSettings(preset.Configuration.Settings);
                preset.Configuration.Style.Apply(label);
            }
            nextQuery = 0f;
            if (!ready)
                Debug.LogWarning(warning, this);
        }

        /// <summary>Checks the active mode and dedicated UI without mutating the prefab.</summary>
        /// <param name="warning">Receives the first issue preventing this interaction from running.</param>
        /// <returns>True when the interaction has valid settings and references.</returns>
        public bool TryValidate(out string warning)
        {
            // Settings validation deliberately leaves invalid authored values unchanged.
            warning = string.Empty;
            if (preset == null || preset.Configuration == null || label == null)
                warning = "Assign a Hover Preset and a dedicated Hover Label using Objects Logic Studio.";
            else if (anchor != null && !anchor.IsChildOf(transform))
                warning = "The hover anchor must belong to this object's prefab hierarchy.";
            else if (!preset.Configuration.TryValidate(out warning) || !label.TryValidate(transform, out warning))
                return false;
            else if (preset.Configuration.Settings.TargetMode == HoverTargetMode.Cursor && GetComponentInChildren<Collider>(true) == null
                && (Application.isPlaying || GetComponent<ObjectAssemblyProduct>() == null))
                warning = "Cursor hover requires a 3D collider on this object or one of its children.";

            // Shared graphics would make independent hover modes overwrite one another.
            if (warning.Length == 0)
                foreach (ObjectHover other in transform.root.GetComponentsInChildren<ObjectHover>(true))
                    if (other != this && other.label == label)
                    {
                        warning = "Each hover interaction requires its own Hover Label.";
                        break;
                    }
            return warning.Length == 0;
        }

        #endregion

        #region Evaluation

        /// <summary>Runs throttled detection and updates only a currently visible label.</summary>
        /// <param name="observer">Shared observer supplying camera, player and reusable query buffers.</param>
        /// <param name="time">Current unscaled time.</param>
        internal void Tick(HoverObserver observer, float time)
        {
            // Deleted UI or invalid initialization leaves the component dormant until Refresh.
            if (!ready || carrySuppressed || !Available(InteractionChannels.Hover) || label == null || !label.isActiveAndEnabled)
            {
                Hide();
                return;
            }
            if (time >= nextQuery)
            {
                nextQuery = time + settings.QueryInterval;
                bool previous = hovered;
                hovered = observer.Evaluate(this, colliders);
                if (hovered && !previous)
                {
                    Signal(InteractionMoment.Started);
                    Signal(InteractionMoment.Completed);
                }
                if (!hovered)
                    label.Hide();
            }
            // A rule may lock or replace this hover synchronously from its own start event.
            if (!Available(InteractionChannels.Hover))
                Hide();
            else if (hovered)
                label.Present(observer.View, WorldAnchor, settings, time);
        }

        /// <summary>Clears stale detection whenever the observing camera or player becomes unavailable.</summary>
        internal void Hide()
        {
            // Reset the query deadline so reacquisition does not wait for an old interval.
            hovered = false;
            nextQuery = 0f;
            if (label != null)
                label.Hide();
        }

        /// <summary>Applies carry-only visibility without changing this component's authored enabled state.</summary>
        /// <param name="suppressed">Whether a carried object requests hidden hover labels.</param>
        internal void SetCarrySuppressed(bool suppressed)
        {
            // Releasing suppression schedules fresh detection rather than reusing a stale hovered result.
            carrySuppressed = suppressed;
            Hide();
        }

        #endregion

        #endregion
    }
}
