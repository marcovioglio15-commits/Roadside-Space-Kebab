using CatOnASkateboard.PlayerStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Runs cached alignment and transform stages while the contact transaction owns both participants.</summary>
    internal sealed class ContactPreparationRun
    {
        #region State

        private enum Stage { Snap, AwaitInput, Forward, Ready, Return, Finished }
        private ContactPreparationSettings settings;
        private Transform owner;
        private Transform counterpart;
        private Transform animated;
        private Rigidbody body;
        private CarryBodyState physics;
        private PlayerToolPose original;
        private Matrix4x4 ownerFrame;
        private Quaternion ownerRotation;
        private bool rootAnimation;
        private Vector3 position;
        private Quaternion rotation;
        private float elapsed;
        private Stage stage;
        private bool bound;

        #endregion
        #region Properties

        /// <summary>Whether the observer may deliver this process's confirmation button.</summary>
        internal bool Waiting => bound && stage == Stage.AwaitInput;
        /// <summary>Whether post-modification motion still owns the item.</summary>
        internal bool Returning => bound && stage == Stage.Return;
        /// <summary>Whether preparation has reached the effect boundary.</summary>
        internal bool Ready => !bound || stage == Stage.Ready;

        #endregion
        #region Methods
        #region Stages

        /// <summary>Captures pre-snap physics and the animation target once per transaction.</summary>
        /// <param name="root">Owner whose pivot defines the snap.</param>
        /// <param name="other">Reserved counterpart.</param>
        /// <param name="configuration">Validated preparation stages.</param>
        internal void Begin(Transform root, ObjectItem other, ContactPreparationSettings configuration)
        {
            settings = configuration;
            owner = root;
            ownerFrame = root.localToWorldMatrix;
            ownerRotation = root.rotation;
            counterpart = other.transform;
            position = counterpart.position;
            rotation = counterpart.rotation;
            animated = settings.Animate ? PlayerHierarchy.Resolve(settings.AnimateOther ? counterpart : root, settings.Path) : null;
            rootAnimation = animated == counterpart;
            if (animated != null)
                original = PlayerToolPose.Read(animated);
            // Pickup has already been cancelled by the transaction's temporary Grab restriction.
            body = settings.HoldsOther ? other.GetComponent<Rigidbody>() : null;
            if (body != null)
            {
                physics = new CarryBodyState(body);
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.isKinematic = true;
                body.useGravity = false;
            }
            bound = true;
            elapsed = 0f;
            stage = settings.Snap ? Stage.Snap : Stage.Forward;
            if (!settings.Snap)
                StartAnimation();
            Tick(0f);
        }

        /// <summary>Advances the current stage and retains the snapped pose during effects.</summary>
        /// <param name="delta">Scaled elapsed time for this frame.</param>
        internal void Tick(float delta)
        {
            if (!bound)
                return;
            elapsed += delta;
            // Align the parent before posing an animated child in world or owner space.
            if (settings.Snap && (!rootAnimation || stage == Stage.AwaitInput) && stage != Stage.Snap
                && counterpart != null && counterpart.gameObject.activeInHierarchy)
                Align(1f);
            switch (stage)
            {
                case Stage.Snap:
                    float snap = settings.Instant ? 1f : Mathf.Clamp01(elapsed / settings.Duration);
                    Align(snap);
                    if (snap >= 1f)
                    {
                        stage = settings.RequireInput ? Stage.AwaitInput : Stage.Forward;
                        if (!settings.RequireInput)
                            StartAnimation();
                    }
                    break;
                case Stage.Forward:
                case Stage.Return:
                    bool returning = stage == Stage.Return;
                    float duration = returning ? settings.ReturnDuration : settings.AnimationDuration;
                    float progress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    if (animated != null)
                        ApplyPose(PlayerToolPose.Interpolate(returning ? settings.StateB : settings.StateA, returning ? settings.StateA : settings.StateB,
                            Mathf.SmoothStep(0f, 1f, progress), returning ? settings.ReturnRotation : settings.ForwardRotation));
                    if (progress >= 1f)
                        stage = returning ? Stage.Finished : Stage.Ready;
                    break;
                case Stage.Ready:
                case Stage.Finished:
                    if (animated != null)
                        ApplyPose(stage == Stage.Ready ? settings.StateB : settings.StateA);
                    break;
            }
            // An animated owner pivot may have moved after the initial alignment above.
            if (settings.Snap && animated == owner && stage != Stage.Snap
                && counterpart != null && counterpart.gameObject.activeInHierarchy)
                Align(1f);
        }

        /// <summary>Consumes one confirmation without replaying alignment.</summary>
        /// <returns>True when the process was awaiting this input.</returns>
        internal bool Confirm()
        {
            if (!Waiting)
                return false;
            StartAnimation();
            return true;
        }

        /// <summary>Begins the optional post-effect return while reservations remain held.</summary>
        /// <returns>True when a return stage must finish before release.</returns>
        internal bool Return()
        {
            if (!bound || !settings.Animate || !settings.Return || animated == null)
                return false;
            stage = Stage.Return;
            elapsed = 0f;
            return true;
        }

        /// <summary>Restores temporary physics and optionally reverses an interrupted preparation pose.</summary>
        /// <param name="cancelled">Restore original transforms when effects have not committed.</param>
        internal void Release(bool cancelled)
        {
            if (!bound)
                return;
            if (body != null)
                physics.Restore(body, false);
            if (cancelled)
            {
                if (animated != null)
                    original.Apply(animated);
                if (settings.Snap && counterpart != null)
                    counterpart.SetPositionAndRotation(position, rotation);
            }
            else if (stage == Stage.Return && animated != null)
                ApplyPose(settings.StateA);
            bound = false;
            body = null;
            animated = counterpart = owner = null;
        }

        /// <summary>Enters the first animation endpoint or directly enables effects.</summary>
        private void StartAnimation()
        {
            elapsed = 0f;
            stage = settings.Animate ? Stage.Forward : Stage.Ready;
            if (animated != null)
                ApplyPose(settings.StateA);
        }

        /// <summary>Resolves endpoints in the authored space without feeding an animated owner's pose back into itself.</summary>
        /// <param name="pose">Interpolated endpoint relative to the owner or the world.</param>
        private void ApplyPose(PlayerToolPose pose)
        {
            // Other targets follow a moving owner; animating the owner root retains its starting frame.
            bool relative = settings.AnimationSpace == ContactPoseSpace.Owner;
            Matrix4x4 frame = animated == owner ? ownerFrame : owner.localToWorldMatrix;
            Quaternion rotation = animated == owner ? ownerRotation : owner.rotation;
            animated.SetPositionAndRotation(relative ? frame.MultiplyPoint3x4(pose.Position) : pose.Position,
                (relative ? rotation : Quaternion.identity) * Quaternion.Euler(pose.Rotation));
            animated.localScale = pose.Scale;
        }

        /// <summary>Follows the current owner pivot while interpolating from the initial counterpart pose.</summary>
        /// <param name="progress">Snap completion fraction.</param>
        private void Align(float progress)
        {
            if (counterpart != null && owner != null)
                counterpart.SetPositionAndRotation(Vector3.Lerp(position,
                    settings.SnapSpace == ContactPoseSpace.Owner ? owner.TransformPoint(settings.Position) : settings.Position, progress),
                    Quaternion.Slerp(rotation, (settings.SnapSpace == ContactPoseSpace.Owner ? owner.rotation : Quaternion.identity)
                        * Quaternion.Euler(settings.Rotation), progress));
        }

        #endregion
        #endregion
    }
}
