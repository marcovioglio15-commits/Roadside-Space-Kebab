using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Owns one reserved transfer and its bounded held-object movement.</summary>
    internal sealed class ScoopRun
    {
        #region State

        private readonly ObjectContainer receiver;
        private readonly ObjectDispenser source;
        private readonly ObjectGrab grab;
        private readonly ObjectItem output;
        private readonly ScoopAnimationSettings settings;
        private readonly ScoopKeyframe[] frames;
        private readonly Pose[] targets;
        private readonly Vector3[] scales;
        private readonly Transform visual;
        private readonly Vector3 initialScale;
        private readonly int commitStep;
        private Pose from;
        private Pose goal;
        private Vector3 fromScale;
        private float started;
        private int step;
        private bool committed;

        #endregion

        #region Properties

        /// <summary>Whether this run still owns its source reservation and carry pose.</summary>
        internal bool Active { get; private set; } = true;

        #endregion

        #region Methods

        #region Preparation

        /// <summary>Captures scoop keyframes once. The return segment follows the live grab pose.</summary>
        /// <param name="receiver">Container receiving the portion.</param>
        /// <param name="source">Reserved supply.</param>
        /// <param name="grab">Existing carry owner.</param>
        /// <param name="output">Optional stored original.</param>
        /// <param name="visual">Optional physics-free scale target.</param>
        private ScoopRun(ObjectContainer receiver, ObjectDispenser source, ObjectGrab grab, ObjectItem output, Transform visual)
        {
            this.receiver = receiver;
            this.source = source;
            this.grab = grab;
            this.output = output;
            this.visual = visual;
            settings = receiver.Settings.ScoopAnimation;
            from = new Pose(grab.transform.position, grab.transform.rotation);
            initialScale = visual != null ? visual.localScale : Vector3.one;
            fromScale = initialScale;
            ScoopPath path = new ScoopPath(settings, from, new Pose(source.transform.position, source.transform.rotation),
                grab.CarryPose, initialScale);
            targets = path.Targets;
            frames = path.Frames;
            scales = path.Scales;
            commitStep = path.CommitStep;
        }

        /// <summary>Checks the optional scale target before reserving movement.</summary>
        /// <param name="root">Container root.</param>
        /// <param name="settings">Optional animation configuration.</param>
        /// <param name="warning">Receives an unsafe scale binding.</param>
        /// <returns>True when scale targets cannot alter physics geometry.</returns>
        internal static bool ValidateVisual(Transform root, ScoopAnimationSettings settings, out string warning)
        {
            warning = string.Empty;
            if (settings == null || !settings.Enabled || !(settings.Mode == ScoopAnimationMode.RelativeChain && settings.Approach is { Resize: true })
                && !System.Array.Exists(settings.Keyframes, frame => frame.Resize))
                return true;
            Transform visual = string.IsNullOrEmpty(settings.VisualPath) ? null : root.Find(settings.VisualPath);
            warning = "Choose a visual child without colliders or rigidbodies for scoop scaling.";
            if (visual == null || visual == root || visual.GetComponentInChildren<Collider>(true) != null
                || visual.GetComponentInChildren<Rigidbody>(true) != null)
                return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Prepares validated targets without creating another object.</summary>
        /// <param name="receiver">Reserved container.</param>
        /// <param name="source">Reserved dispenser.</param>
        /// <param name="grab">Current carry owner.</param>
        /// <param name="output">Optional stored original.</param>
        /// <param name="run">Receives the prepared movement.</param>
        /// <param name="warning">Receives invalid movement data.</param>
        /// <returns>True when the sequence can start.</returns>
        internal static bool TryCreate(ObjectContainer receiver, ObjectDispenser source, ObjectGrab grab, ObjectItem output,
            out ScoopRun run, out string warning)
        {
            run = null;
            if (!receiver.Settings.ScoopAnimation.TryValidate(out warning)
                || !ValidateVisual(receiver.transform, receiver.Settings.ScoopAnimation, out warning))
                return false;
            Transform visual = string.IsNullOrEmpty(receiver.Settings.ScoopAnimation.VisualPath) ? null
                : receiver.transform.Find(receiver.Settings.ScoopAnimation.VisualPath);
            run = new ScoopRun(receiver, source, grab, output, visual);
            return true;
        }

        #endregion

        #region Movement

        /// <summary>Starts scaled timing or commits an unanimated transfer immediately.</summary>
        internal void Start()
        {
            if (!Active)
                return;
            started = Time.time;
            if (targets.Length == 0)
            {
                Commit();
                Finish();
            }
        }

        /// <summary>Computes a target for Grab's existing collision-constrained pose writer.</summary>
        /// <param name="carry">Current grab pose in the camera or player frame.</param>
        /// <param name="pose">Receives this segment's interpolated world pose.</param>
        /// <returns>True while this run still owns movement.</returns>
        internal bool Target(Pose carry, out Pose pose)
        {
            pose = from;
            // Once the portion is committed, an exhausted source can no longer interrupt the return.
            if (!Active || receiver == null || !receiver.isActiveAndEnabled || !grab.IsHeld
                || !committed && (source == null || !source.isActiveAndEnabled))
            {
                Cancel();
                return false;
            }
            float duration = Duration();
            if (Time.time - started > duration + settings.ObstacleMargin)
            {
                Debug.LogWarning("Scoop could not reach its target before the movement deadline.", receiver);
                Cancel();
                return false;
            }
            // Cache the exact endpoint tested after Grab applies this frame's collision constraints.
            goal = step < frames.Length ? targets[step] : carry;
            float progress = Mathf.Clamp01((Time.time - started) / duration);
            if (step >= frames.Length || frames[step].Easing == ScoopEasing.Smooth)
                progress = Mathf.SmoothStep(0f, 1f, progress);
            pose = new Pose(Vector3.Lerp(from.position, goal.position, progress),
                Quaternion.Slerp(from.rotation, goal.rotation, progress));
            if (visual != null)
                visual.localScale = Vector3.Lerp(fromScale, scales[step], progress);
            return true;
        }

        /// <summary>Advances only when the actual collision-constrained body reaches the target.</summary>
        /// <param name="pose">Actual body pose after Grab resolves world collisions.</param>
        internal void Reached(Pose pose)
        {
            if (!Active || Time.time - started < Duration() || (pose.position - goal.position).sqrMagnitude > 0.0004f
                || Quaternion.Angle(pose.rotation, goal.rotation) > 2f)
                return;
            if (step == commitStep && !Commit())
            {
                Cancel();
                return;
            }
            from = pose;
            fromScale = scales[step];
            started = Time.time;
            if (++step >= targets.Length)
                Finish();
        }

        /// <summary>Reads the active segment's duration without rebuilding targets.</summary>
        /// <returns>Positive scaled duration.</returns>
        private float Duration()
        {
            return step < frames.Length ? frames[step].Duration : settings.ReturnDuration;
        }

        #endregion

        #region Completion

        /// <summary>Commits reserved supply and logical fill once.</summary>
        /// <returns>True when the portion is already committed or commits now.</returns>
        private bool Commit()
        {
            if (committed)
                return true;
            if (!Active || source == null || receiver == null)
                return false;
            // Batch flag notifications until both inventory counts represent the same transfer.
            using (source.Item.Identity.GroupChanges())
            using (receiver.Item.Identity.GroupChanges())
            {
                int amount = source.CommitScoop(receiver, output);
                if (amount <= 0)
                    return false;
                committed = true;
                receiver.CommitScoop(amount);
            }
            if (source != null)
                source.Signal(InteractionMoment.Completed);
            if (receiver != null)
                receiver.Signal(InteractionMoment.Completed);
            return true;
        }

        /// <summary>Cancels remaining movement while preserving a portion already committed.</summary>
        internal void Cancel()
        {
            if (!Active)
                return;
            if (!committed && visual != null)
                visual.localScale = initialScale;
            Finish();
        }

        /// <summary>Releases ownership without changing the held object's final pose.</summary>
        private void Finish()
        {
            if (!Active)
                return;
            Active = false;
            if (source != null)
                source.ReleaseScoop(receiver);
            if (receiver != null)
                receiver.EndScoop();
            if (grab != null)
                grab.EndScoop();
        }

        #endregion

        #endregion
    }
}
