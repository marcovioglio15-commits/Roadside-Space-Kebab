using System;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Serializable camera options copied at initialization; validation never changes entered values.</summary>
    [Serializable]
    public struct PlayerCameraSettings
    {
        #region Serialized Fields

        [Header("View")]
        [Tooltip("First person uses the target point; third person follows behind it; fixed uses a world pose.")]
        [SerializeField]
        private PlayerCameraMode mode;

        [Tooltip("Direct follows the focus immediately; Damped smooths player translation. Enable Damp Orbit to also soften third-person rotation.")]
        [SerializeField]
        private PlayerCameraFollow follow;

        [Tooltip("Positive response per second for focus movement. Higher values reduce follow lag; look sensitivity is unaffected.")]
        [SerializeField]
        private float followResponse;

        [Tooltip("Also damp the orbit rotation around the focus. Disable to keep look rotation immediate while smoothing player movement.")]
        [SerializeField]
        private bool dampOrbit;

        [Tooltip("Target offset relative to the assigned target, or the player root when no target is assigned.")]
        [SerializeField]
        private Vector3 targetOffset;

        [Tooltip("Third-person distance behind the target in metres.")]
        [SerializeField]
        private float distance;

        [Tooltip("Initial yaw relative to the player and pitch in degrees.")]
        [SerializeField]
        private Vector2 initialAngles;

        [Header("Look")]
        [Tooltip("Allow assigned Look Delta and Look Rate actions to rotate the view.")]
        [SerializeField]
        private bool lookEnabled;

        [Tooltip("Degrees per mouse delta unit on each axis. Displacement is consumed once and is never multiplied by frame time. 0.16 gives 160 degrees for 1000 units.")]
        [SerializeField]
        private Vector2 deltaSensitivity;

        [Tooltip("Degrees per second at full stick deflection on each axis. The normalized stick value is integrated over frame time; 160 gives a full turn in 2.25 seconds.")]
        [SerializeField]
        private Vector2 rateSensitivity;

        [Tooltip("Smooth view rotation over elapsed time. Adds turning lag, including during reversals; disable for immediate mouse response.")]
        [SerializeField]
        private bool smoothLook;

        [Tooltip("Positive response time in seconds. Smaller values stay closer to raw input; larger values smooth more and respond later.")]
        [SerializeField]
        private float lookSmoothingTime;

        [Tooltip("Shorten smoothing during fast turns to reduce lag while retaining gentle filtering for small movements.")]
        [SerializeField]
        private bool adaptiveLookSmoothing;

        [Tooltip("Angular speed in degrees per second that halves the smoothing time. Smaller values favor rapid response.")]
        [SerializeField]
        private float lookSmoothingSpeed;

        [Tooltip("Reverse vertical look without changing bindings.")]
        [SerializeField]
        private bool invertY;

        [Tooltip("Ordered pitch limits in degrees, strictly inside -90 and 90.")]
        [SerializeField]
        private Vector2 pitchLimits;

        [Tooltip("Restrict yaw around the initial player heading.")]
        [SerializeField]
        private bool limitYaw;

        [Tooltip("Minimum and maximum yaw relative to the initial player heading.")]
        [SerializeField]
        private Vector2 yawLimits;

        [Tooltip("Lock the pointer to the view center while active. The optional Cursor Toggle action releases it.")]
        [SerializeField]
        private bool lockCursor;

        [Tooltip("Display a centered cursor graphic while the pointer is locked. Unity hides the native cursor in Locked mode.")]
        [SerializeField]
        private bool showCenteredCursor;

        [Tooltip("Optional centered cursor texture. Cursor Scale multiplies its pixel dimensions; empty uses a contrasting crosshair.")]
        [SerializeField]
        private Texture2D cursorTexture;

        [Tooltip("Positive multiplier for the texture's pixel dimensions or the default 14-pixel crosshair. One retains the original size.")]
        [SerializeField]
        private float cursorScale;

        [Header("Lens")]
        [Tooltip("Vertical field of view in degrees; must be between 1 and 179.")]
        [SerializeField]
        private float fieldOfView;

        [Tooltip("Positive near clipping distance smaller than the far distance.")]
        [SerializeField]
        private float nearClip;

        [Tooltip("Far clipping distance in metres.")]
        [SerializeField]
        private float farClip;

        [Header("Obstacles")]
        [Tooltip("Shorten the third-person camera arm when geometry blocks it.")]
        [SerializeField]
        private bool avoidObstacles;

        [Tooltip("Camera obstacle layers; the player hierarchy is ignored.")]
        [SerializeField]
        private LayerMask obstacleMask;

        [Tooltip("Positive camera clearance radius in metres.")]
        [SerializeField]
        private float collisionRadius;

        [Tooltip("Nonnegative clearance between camera and obstacles.")]
        [SerializeField]
        private float collisionPadding;

        [Tooltip("Seconds to ease the camera arm back out after an obstacle clears. Zero restores the full arm immediately; approaching obstacles always shorten it immediately.")]
        [SerializeField]
        private float obstacleReturnTime;

        [Header("Fixed View")]
        [Tooltip("World position used by Fixed mode.")]
        [SerializeField]
        private Vector3 fixedPosition;

        [Tooltip("World rotation used when a fixed camera does not track the target.")]
        [SerializeField]
        private Vector3 fixedEuler;

        [Tooltip("Rotate a fixed-position camera toward the player target.")]
        [SerializeField]
        private bool fixedTrackTarget;

        [Header("Player Presentation")]
        [Tooltip("Interpret movement on world axes, player axes or camera yaw.")]
        [SerializeField]
        private PlayerMovementFrame movementFrame;

        [Tooltip("Keep authored visual yaw, face achieved movement or face camera yaw.")]
        [SerializeField]
        private PlayerModelFacing modelFacing;

        [Tooltip("Positive maximum visual turning speed in degrees per second.")]
        [SerializeField]
        private float turnSpeed;

        [Tooltip("Hide configured model renderers only for this first-person player camera.")]
        [SerializeField]
        private bool hideVisualInFirstPerson;

        #endregion

        #region Properties

        /// <summary>First person uses the target point; third person follows behind it; fixed uses a world pose.</summary>
        public readonly PlayerCameraMode Mode => mode;

        /// <summary>Direct follows immediately; Damped approaches the target over time.</summary>
        public readonly PlayerCameraFollow Follow => follow;

        /// <summary>Positive exponential response per second for Damped following.</summary>
        public readonly float FollowResponse => followResponse;

        /// <summary>Whether positional following also softens orbit rotation.</summary>
        public readonly bool DampOrbit => dampOrbit;

        /// <summary>Target offset relative to the assigned target, or the player root when no target is assigned.</summary>
        public readonly Vector3 TargetOffset => targetOffset;

        /// <summary>Third-person distance behind the target in metres.</summary>
        public readonly float Distance => distance;

        /// <summary>Initial yaw relative to the player and pitch in degrees.</summary>
        public readonly Vector2 InitialAngles => initialAngles;

        /// <summary>Allow assigned Look Delta and Look Rate actions to rotate the view.</summary>
        public readonly bool LookEnabled => lookEnabled;

        /// <summary>Degrees per input unit for Look Delta, typically pointer delta.</summary>
        public readonly Vector2 DeltaSensitivity => deltaSensitivity;

        /// <summary>Degrees per second per input unit for Look Rate, typically a stick.</summary>
        public readonly Vector2 RateSensitivity => rateSensitivity;

        /// <summary>Whether displayed look angles gradually approach accumulated input.</summary>
        public readonly bool SmoothLook => smoothLook;

        /// <summary>Look response time in seconds; independent of positional follow.</summary>
        public readonly float LookSmoothingTime => lookSmoothingTime;

        /// <summary>Whether fast turns shorten the configured smoothing interval.</summary>
        public readonly bool AdaptiveLookSmoothing => adaptiveLookSmoothing;

        /// <summary>Angular speed that halves the smoothing interval.</summary>
        public readonly float LookSmoothingSpeed => lookSmoothingSpeed;

        /// <summary>Reverse vertical look without changing bindings.</summary>
        public readonly bool InvertY => invertY;

        /// <summary>Ordered pitch limits in degrees, strictly inside -90 and 90.</summary>
        public readonly Vector2 PitchLimits => pitchLimits;

        /// <summary>Restrict yaw around the initial player heading.</summary>
        public readonly bool LimitYaw => limitYaw;

        /// <summary>Minimum and maximum yaw relative to the initial player heading.</summary>
        public readonly Vector2 YawLimits => yawLimits;

        /// <summary>Lock the pointer to the view center while active. The optional Cursor Toggle action releases it.</summary>
        public readonly bool LockCursor => lockCursor;

        /// <summary>Draw a visible pointer graphic while native capture is active.</summary>
        public readonly bool ShowCenteredCursor => showCenteredCursor;

        /// <summary>Optional graphic drawn at the camera viewport center.</summary>
        public readonly Texture2D CursorTexture => cursorTexture;

        /// <summary>Uniform size multiplier for the selected centered cursor graphic.</summary>
        public readonly float CursorScale => cursorScale;

        /// <summary>Vertical field of view in degrees; must be between 1 and 179.</summary>
        public readonly float FieldOfView => fieldOfView;

        /// <summary>Positive near clipping distance smaller than the far distance.</summary>
        public readonly float NearClip => nearClip;

        /// <summary>Far clipping distance in metres.</summary>
        public readonly float FarClip => farClip;

        /// <summary>Shorten the third-person camera arm when geometry blocks it.</summary>
        public readonly bool AvoidObstacles => avoidObstacles;

        /// <summary>Camera obstacle layers; the player hierarchy is ignored.</summary>
        public readonly LayerMask ObstacleMask => obstacleMask;

        /// <summary>Positive camera clearance radius in metres.</summary>
        public readonly float CollisionRadius => collisionRadius;

        /// <summary>Nonnegative clearance between camera and obstacles.</summary>
        public readonly float CollisionPadding => collisionPadding;

        /// <summary>Response time for restoring the unobstructed camera arm.</summary>
        public readonly float ObstacleReturnTime => obstacleReturnTime;

        /// <summary>World position used by Fixed mode.</summary>
        public readonly Vector3 FixedPosition => fixedPosition;

        /// <summary>World rotation used when a fixed camera does not track the target.</summary>
        public readonly Vector3 FixedEuler => fixedEuler;

        /// <summary>Rotate a fixed-position camera toward the player target.</summary>
        public readonly bool FixedTrackTarget => fixedTrackTarget;

        /// <summary>Interpret movement on world axes, player axes or camera yaw.</summary>
        public readonly PlayerMovementFrame MovementFrame => movementFrame;

        /// <summary>Keep authored visual yaw, face achieved movement or face camera yaw.</summary>
        public readonly PlayerModelFacing ModelFacing => modelFacing;

        /// <summary>Positive maximum visual turning speed in degrees per second.</summary>
        public readonly float TurnSpeed => turnSpeed;

        /// <summary>Hide configured model renderers only for this first-person player camera.</summary>
        public readonly bool HideVisualInFirstPerson => hideVisualInFirstPerson;

        /// <summary>Supplies a complete third-person configuration for new assets.</summary>
        public static PlayerCameraSettings Default => new PlayerCameraSettings
        {
            mode = PlayerCameraMode.ThirdPerson,
            follow = PlayerCameraFollow.Damped,
            followResponse = 12f,
            targetOffset = new Vector3(0f, 1.6f, 0f),
            distance = 4f,
            initialAngles = new Vector2(0f, 15f),
            lookEnabled = true,
            deltaSensitivity = new Vector2(0.12f, 0.12f),
            rateSensitivity = new Vector2(160f, 120f),
            smoothLook = false,
            lookSmoothingTime = 0.012f,
            lookSmoothingSpeed = 180f,
            invertY = false,
            pitchLimits = new Vector2(-80f, 80f),
            limitYaw = false,
            yawLimits = new Vector2(-180f, 180f),
            lockCursor = true,
            showCenteredCursor = false,
            cursorTexture = null,
            cursorScale = 1f,
            fieldOfView = 65f,
            nearClip = 0.05f,
            farClip = 500f,
            avoidObstacles = true,
            obstacleMask = ~0,
            collisionRadius = 0.15f,
            collisionPadding = 0.05f,
            obstacleReturnTime = 0.08f,
            fixedPosition = new Vector3(0f, 5f, -8f),
            fixedEuler = new Vector3(20f, 0f, 0f),
            fixedTrackTarget = true,
            movementFrame = PlayerMovementFrame.Camera,
            modelFacing = PlayerModelFacing.Movement,
            turnSpeed = 540f,
            hideVisualInFirstPerson = true
        };

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks active options before Apply or runtime initialization.</summary>
        /// <param name="warning">Receives the first incompatibility without rewriting it.</param>
        /// <returns>True when the selected camera behavior is usable.</returns>
        public readonly bool TryValidate(out string warning)
        {
            // Hidden settings retain their values and do not block unrelated modes.
            warning = string.Empty;
            if (mode < PlayerCameraMode.FirstPerson || mode > PlayerCameraMode.Fixed
                || mode != PlayerCameraMode.Fixed && (follow < PlayerCameraFollow.Direct || follow > PlayerCameraFollow.Damped)
                || movementFrame < PlayerMovementFrame.World || movementFrame > PlayerMovementFrame.Camera
                || modelFacing < PlayerModelFacing.Authored || modelFacing > PlayerModelFacing.Camera)
                warning = "Choose supported camera, follow, movement and facing modes.";
            else if ((mode != PlayerCameraMode.Fixed || fixedTrackTarget) && !Finite(targetOffset)
                || !float.IsFinite(fieldOfView) || fieldOfView <= 1f || fieldOfView >= 179f
                || !Positive(nearClip) || !Positive(farClip) || farClip <= nearClip)
                warning = "Use finite camera target and lens values, 1 < FOV < 179 and 0 < Near < Far.";
            else if (mode != PlayerCameraMode.Fixed && follow == PlayerCameraFollow.Damped && !Positive(followResponse))
                warning = "Damped follow needs a finite positive response.";
            else if (mode == PlayerCameraMode.Fixed && (!Finite(fixedPosition) || !fixedTrackTarget && !Finite(fixedEuler)))
                warning = "Fixed camera position and rotation must be finite.";
            else if (mode != PlayerCameraMode.Fixed && (!Finite(initialAngles) || lookEnabled && (!Finite(pitchLimits)
                || pitchLimits.x <= -90f || pitchLimits.y >= 90f || pitchLimits.x > pitchLimits.y
                || initialAngles.y < pitchLimits.x || initialAngles.y > pitchLimits.y
                || (limitYaw && (!Finite(yawLimits) || yawLimits.x > yawLimits.y
                    || initialAngles.x < yawLimits.x || initialAngles.x > yawLimits.y)))))
                warning = "Starting angles must lie within ordered limits; pitch must stay inside -90 and 90 degrees.";
            else if (mode != PlayerCameraMode.Fixed && lookEnabled
                && (!Finite(deltaSensitivity) || !Finite(rateSensitivity) || deltaSensitivity.x < 0f || deltaSensitivity.y < 0f
                    || rateSensitivity.x < 0f || rateSensitivity.y < 0f))
                warning = "Look sensitivities must be finite and nonnegative.";
            else if (mode != PlayerCameraMode.Fixed && lookEnabled && smoothLook && !Positive(lookSmoothingTime))
                warning = "Smooth Look needs a finite positive response time in seconds.";
            else if (mode != PlayerCameraMode.Fixed && lookEnabled && smoothLook && adaptiveLookSmoothing && !Positive(lookSmoothingSpeed))
                warning = "Adaptive smoothing needs a finite positive angular speed.";
            else if (mode != PlayerCameraMode.Fixed && lookEnabled && lockCursor && showCenteredCursor && !Positive(cursorScale))
                warning = "Centered Cursor requires a finite positive Cursor Scale.";
            else if (mode == PlayerCameraMode.ThirdPerson && (!Positive(distance)
                || (avoidObstacles && (!Positive(collisionRadius) || !float.IsFinite(collisionPadding) || collisionPadding < 0f))))
                warning = "Distance and collision radius must be positive; clearance must be nonnegative.";
            else if (mode == PlayerCameraMode.ThirdPerson && avoidObstacles
                && (!float.IsFinite(obstacleReturnTime) || obstacleReturnTime < 0f))
                warning = "Obstacle return time must be finite and nonnegative.";
            else if (modelFacing != PlayerModelFacing.Authored && !Positive(turnSpeed))
                warning = "Visual turning speed must be finite and positive.";
            return warning.Length == 0;
        }

        /// <summary>Checks distances and response values at initialization boundaries.</summary>
        /// <param name="value">Unmodified input value.</param>
        /// <returns>True for finite values greater than zero.</returns>
        private static bool Positive(float value)
        {
            // Runtime interpolation consumes already validated settings.
            return float.IsFinite(value) && value > 0f;
        }

        /// <summary>Checks each position or angle component without normalization.</summary>
        /// <param name="value">Vector to inspect; Vector2 converts with a zero third component.</param>
        /// <returns>True when every component is finite.</returns>
        private static bool Finite(Vector3 value)
        {
            // Invalid data stays editable rather than being clamped in the asset.
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        #endregion

        #endregion
    }
}
