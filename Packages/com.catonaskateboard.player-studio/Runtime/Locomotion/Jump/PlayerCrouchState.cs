using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Keeps the feet anchored while resizing the controller and checks clearance before standing.</summary>
    internal sealed class PlayerCrouchState
    {
        #region State

        private readonly Collider[] overlaps = new Collider[32];
        private CharacterController controller;
        private Vector3 center;
        private float height;
        private float step;
        private float fraction;
        private float ratio;
        private float speed;
        private float duration;
        private int layers;
        private bool toggle;
        private bool lowerCamera;
        private bool allowJump;
        private bool requested;

        #endregion
        #region Properties

        /// <summary>Whether the physical capsule is still below its standing height.</summary>
        internal bool Active => fraction > 0f;
        /// <summary>Speed interpolation follows the actual body transition.</summary>
        internal float Speed => Mathf.Lerp(1f, speed, fraction);
        /// <summary>Local camera displacement matching the lowered capsule top.</summary>
        internal float CameraOffset => lowerCamera ? -height * (1f - ratio) * fraction : 0f;
        /// <summary>Whether the current posture permits a jump request.</summary>
        internal bool CanJump => !Active || allowJump;

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Captures standing geometry and immutable numeric choices once at motor initialization.</summary>
        /// <param name="body">Existing native controller.</param>
        /// <param name="settings">Validated optional crouch configuration.</param>
        /// <param name="warning">Receives an incompatible crouched capsule height.</param>
        /// <returns>True when disabled or compatible with the standing body.</returns>
        internal bool Bind(CharacterController body, PlayerCrouchSettings settings, out string warning)
        {
            Restore();
            warning = string.Empty;
            if (settings == null || !settings.Enabled)
                return true;
            if (!settings.TryValidate(out warning))
                return false;
            if (body.height * settings.HeightRatio < body.radius * 2f)
            {
                warning = "Crouched height must be at least twice the Character Controller radius. Increase Crouch Height Ratio or adjust the Body preset.";
                return false;
            }
            controller = body;
            center = body.center;
            height = body.height;
            step = body.stepOffset;
            ratio = settings.HeightRatio;
            speed = settings.SpeedMultiplier;
            duration = settings.Transition;
            toggle = settings.Toggle;
            lowerCamera = settings.LowerCamera;
            allowJump = settings.AllowJump;
            layers = settings.ClearanceLayers;
            // Clearance respects the same layer matrix as the native player capsule.
            for (int layer = 0; layer < 32; layer++)
                if (Physics.GetIgnoreLayerCollision(body.gameObject.layer, layer))
                    layers &= ~(1 << layer);
            return true;
        }

        /// <summary>Restores the captured body at disable or explicit reinitialization.</summary>
        internal void Restore()
        {
            if (controller != null)
            {
                controller.height = height;
                controller.center = center;
                controller.stepOffset = step;
            }
            controller = null;
            requested = false;
            fraction = 0f;
        }

        #endregion
        #region Posture

        /// <summary>Consumes one button edge and changes geometry only during a requested transition.</summary>
        /// <param name="signal">Cached player-owned crouch input.</param>
        /// <param name="deltaTime">Positive scaled frame duration.</param>
        internal void Tick(PlayerButtonSignal signal, float deltaTime)
        {
            if (controller == null || deltaTime <= 0f)
                return;
            if (signal.IsActive && !signal.Interrupted)
                requested = toggle ? signal.Pressed ? !requested : requested : signal.Held;
            float next = duration > 0f ? Mathf.MoveTowards(fraction, requested ? 1f : 0f, deltaTime / duration) : requested ? 1f : 0f;
            if (Mathf.Approximately(next, fraction) || next < fraction && !HasClearance(height * Mathf.Lerp(1f, ratio, next)))
                return;
            // Preserve the bottom of the capsule and prevent step height from exceeding the shortened body.
            fraction = next;
            controller.height = height * Mathf.Lerp(1f, ratio, fraction);
            controller.center = center - Vector3.up * ((height - controller.height) * 0.5f);
            controller.stepOffset = Mathf.Min(step, controller.height - controller.radius * 2f);
        }

        /// <summary>Rejects an expansion that would overlap another solid collider.</summary>
        /// <param name="targetHeight">Proposed local capsule height.</param>
        /// <returns>True when the proposed capsule fits without intersecting overhead geometry.</returns>
        private bool HasClearance(float targetHeight)
        {
            Transform root = controller.transform;
            Vector3 scale = root.lossyScale;
            float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float skin = Mathf.Min(controller.skinWidth * Mathf.Abs(scale.y), radius * 0.05f);
            Vector3 bottom = root.TransformPoint(center - Vector3.up * (height * 0.5f));
            Vector3 up = root.up;
            int count = Physics.OverlapCapsuleNonAlloc(bottom + up * radius,
                bottom + up * (targetHeight * Mathf.Abs(scale.y) - radius), radius - skin, overlaps, layers, QueryTriggerInteraction.Ignore);
            // Saturation is conservative: no hidden collider can permit an unsafe stand-up.
            if (count == overlaps.Length)
                return false;
            for (int index = 0; index < count; index++)
                if (overlaps[index] != null && !overlaps[index].transform.IsChildOf(root))
                    return false;
            return true;
        }

        #endregion
        #endregion
    }
}
