using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Selects proximity to the view center or an exact cursor hit on an owned collider.</summary>
    public enum HoverTargetMode
    {
        ViewCenter,
        Cursor
    }

    /// <summary>Selects immediate appearance or movement outward from the object's projected anchor.</summary>
    public enum HoverAppearance
    {
        Instant,
        PopUp
    }

    /// <summary>Stores detection and motion settings independently for each hover interaction.</summary>
    [Serializable]
    public sealed class HoverSettings
    {
        #region Serialized Fields

        [Header("Detection")]
        [Tooltip("View Center tests the anchor's screen distance. Cursor requires a ray hit on this object's colliders.")]
        [SerializeField]
        private HoverTargetMode targetMode;

        [Tooltip("Maximum distance in world units from the tagged player root to the object anchor.")]
        [SerializeField]
        private float playerDistance = 3f;

        [Tooltip("Screen radius as a fraction of camera viewport height: 0.08 means 8%. Independent of resolution and aspect ratio.")]
        [SerializeField]
        private float centerRadius = 0.08f;

        [Tooltip("Seconds between detection queries. Zero evaluates every frame; label motion still updates every visible frame.")]
        [SerializeField]
        private float queryInterval = 0.05f;

        [Tooltip("Solid collider layers that block visibility. Include walls and other interactive objects; triggers never block sight.")]
        [SerializeField]
        private LayerMask obstacleMask = Physics.DefaultRaycastLayers;

        [Header("Placement")]
        [Tooltip("Local offset from the optional anchor, or from the object root when no anchor is assigned.")]
        [SerializeField]
        private Vector3 anchorOffset;

        [Tooltip("World-space displacement of the label above or beside the detection anchor.")]
        [SerializeField]
        private Vector3 worldOffset = new Vector3(0f, 0.25f, 0f);

        [Tooltip("Final label displacement in screen pixels, scaled against a 1080-pixel viewport height.")]
        [SerializeField]
        private Vector2 screenOffset = new Vector2(0f, 35f);

        [Header("Appearance")]
        [Tooltip("Instant displays the label at its final position. Pop Up moves and grows it from the object anchor.")]
        [SerializeField]
        private HoverAppearance appearance = HoverAppearance.PopUp;

        [Tooltip("Pop-up duration in unscaled seconds. Must be greater than zero.")]
        [SerializeField]
        private float duration = 0.18f;

        [Tooltip("Initial pop-up scale, between zero and one. Final size is the authored label size.")]
        [SerializeField]
        private float startScale = 0.2f;

        #endregion

        #region Properties

        /// <summary>Detection route chosen for this interaction.</summary>
        public HoverTargetMode TargetMode => targetMode;
        /// <summary>Maximum player-to-anchor distance in metres.</summary>
        public float PlayerDistance => playerDistance;
        /// <summary>Center tolerance relative to viewport height.</summary>
        public float CenterRadius => centerRadius;
        /// <summary>Minimum time between visibility queries.</summary>
        public float QueryInterval => queryInterval;
        /// <summary>Layers included in obstruction queries.</summary>
        public int ObstacleMask => obstacleMask;
        /// <summary>Local displacement of the detection anchor.</summary>
        public Vector3 AnchorOffset => anchorOffset;
        /// <summary>World displacement of the final label.</summary>
        public Vector3 WorldOffset => worldOffset;
        /// <summary>Additional label displacement at reference viewport height.</summary>
        public Vector2 ScreenOffset => screenOffset;
        /// <summary>Selected entry animation.</summary>
        public HoverAppearance Appearance => appearance;
        /// <summary>Entry animation length in unscaled seconds.</summary>
        public float Duration => duration;
        /// <summary>Initial scale multiplier for a pop-up.</summary>
        public float StartScale => startScale;

        #endregion

        #region Methods

        #region Construction

        /// <summary>Supplies initial values for new presets without reading assets or scene state.</summary>
        public HoverSettings()
        {
            // Field initializers define the authored defaults.
        }

        /// <summary>Captures independent runtime settings when an interaction activates.</summary>
        /// <param name="source">Validated saved settings to copy.</param>
        public HoverSettings(HoverSettings source)
        {
            // Shared preset changes cannot mutate a running interaction's captured configuration.
            targetMode = source.targetMode;
            playerDistance = source.playerDistance;
            centerRadius = source.centerRadius;
            queryInterval = source.queryInterval;
            obstacleMask = source.obstacleMask;
            anchorOffset = source.anchorOffset;
            worldOffset = source.worldOffset;
            screenOffset = source.screenOffset;
            appearance = source.appearance;
            duration = source.duration;
            startScale = source.startScale;
        }

        #endregion

        #region Validation

        /// <summary>Rejects invalid active settings without rewriting authored values.</summary>
        /// <param name="warning">Receives the first actionable configuration issue.</param>
        /// <returns>True when the selected detection and appearance can run.</returns>
        public bool TryValidate(out string warning)
        {
            // Hidden fields are checked only when their mode uses them.
            warning = string.Empty;
            if (targetMode != HoverTargetMode.ViewCenter && targetMode != HoverTargetMode.Cursor)
                warning = "Choose a supported hover target mode.";
            else if (!float.IsFinite(playerDistance) || playerDistance <= 0f || playerDistance > 100000f)
                warning = "Player Distance must be greater than zero and at most 100000 world units.";
            else if (!float.IsFinite(queryInterval) || queryInterval < 0f)
                warning = "Query Interval must be a finite, non-negative number.";
            else if (targetMode == HoverTargetMode.ViewCenter && (!float.IsFinite(centerRadius) || centerRadius < 0f || centerRadius > 1f))
                warning = "Center Radius must be between zero and one.";
            else if (obstacleMask.value == 0)
                warning = "Obstacle Mask must include the layers that can block sight.";
            else if (!IsFinite(anchorOffset) || !IsFinite(worldOffset) || !IsFinite(screenOffset))
                warning = "All offset coordinates must be finite.";
            else if (appearance != HoverAppearance.Instant && appearance != HoverAppearance.PopUp)
                warning = "Choose a supported hover appearance.";
            else if (appearance == HoverAppearance.PopUp && (!float.IsFinite(duration) || duration <= 0f
                || !float.IsFinite(startScale) || startScale < 0f || startScale > 1f))
                warning = "Pop Up requires a positive duration and Start Scale between zero and one.";
            return warning.Length == 0;
        }

        /// <summary>Checks serialized coordinates before projection or physics uses them.</summary>
        /// <param name="value">World, local or screen displacement.</param>
        /// <returns>True when every coordinate is finite.</returns>
        private static bool IsFinite(Vector3 value)
        {
            // Vector2 implicitly converts to Vector3 with a finite zero Z coordinate.
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        #endregion

        #endregion
    }
}
