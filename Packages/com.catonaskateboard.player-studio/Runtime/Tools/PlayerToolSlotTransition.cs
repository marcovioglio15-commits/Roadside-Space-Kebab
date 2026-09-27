using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Precomputes one direct or circular slot transition at a switch boundary.</summary>
    internal readonly struct PlayerToolSlotTransition
    {
        #region State

        private readonly PlayerToolPose from;
        private readonly PlayerToolPose to;
        private readonly Quaternion startRotation;
        private readonly Quaternion endRotation;
        private readonly Quaternion correction;
        private readonly Vector3 pivot;
        private readonly Vector3 axis;
        private readonly Vector3 radial;
        private readonly float angle;
        private readonly float radius;
        private readonly float startRadius;
        private readonly float startHeight;
        private readonly float endHeight;
        private readonly bool circular;

        #endregion

        #region Methods

        #region Transition

        /// <summary>Captures endpoints and wheel geometry before the transition starts.</summary>
        /// <param name="initial">Current child pose.</param>
        /// <param name="destination">Target slot pose.</param>
        /// <param name="settings">Module defining optional circular travel.</param>
        internal PlayerToolSlotTransition(PlayerToolPose initial, PlayerToolPose destination, PlayerToolsPreset settings)
        {
            // Trigonometry and projection are computed once per switch, not once per frame.
            from = initial;
            to = destination;
            startRotation = Quaternion.Euler(initial.Rotation);
            endRotation = Quaternion.Euler(destination.Rotation);
            circular = settings.Layout == PlayerToolLayout.Cyclic && settings.SlotMotion == PlayerToolSlotMotion.AroundPivot;
            pivot = settings.Pivot;
            axis = settings.Axis.normalized;
            Vector3 startRadial = Vector3.ProjectOnPlane(initial.Position - pivot, axis);
            startRadius = startRadial.magnitude;
            radial = startRadius > 0f ? startRadial / startRadius : Vector3.zero;
            Vector3 targetRadial = Vector3.ProjectOnPlane(destination.Position - pivot, axis);
            radius = targetRadial.magnitude;
            startHeight = Vector3.Dot(initial.Position - pivot, axis);
            endHeight = Vector3.Dot(destination.Position - pivot, axis);
            angle = Vector3.SignedAngle(radial, targetRadial, axis);
            if (settings.Clockwise && angle > 0.001f)
                angle -= 360f;
            else if (!settings.Clockwise && angle < -0.001f)
                angle += 360f;
            correction = Quaternion.Inverse(Quaternion.AngleAxis(angle, axis) * startRotation) * endRotation;
        }

        /// <summary>Applies slot travel and preserves the requested turn direction for circular motion.</summary>
        /// <param name="target">Bound visual child.</param>
        /// <param name="amount">Eased normalized progress.</param>
        internal void Apply(Transform target, float amount)
        {
            // Exact endpoints prevent accumulated drift over repeated wheel cycles.
            Vector3 position = Vector3.Lerp(from.Position, to.Position, amount);
            Quaternion rotation = Quaternion.Slerp(startRotation, endRotation, amount);
            if (circular && startRadius > 0.001f && amount < 1f)
            {
                Quaternion turn = Quaternion.AngleAxis(angle * amount, axis);
                position = pivot + turn * radial * Mathf.Lerp(startRadius, radius, amount)
                    + axis * Mathf.Lerp(startHeight, endHeight, amount);
                rotation = turn * startRotation * Quaternion.Slerp(Quaternion.identity, correction, amount);
            }
            target.SetLocalPositionAndRotation(position, rotation);
            target.localScale = Vector3.Lerp(from.Scale, to.Scale, amount);
        }

        #endregion

        #endregion
    }
}
