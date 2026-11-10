using CatOnASkateboard.AudioStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Releases the held body with aim-relative launch strength, elevation, yaw and spin.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ObjectGrab))]
    [AddComponentMenu("Objects Logic Studio/Object Throw")]
    public sealed class ObjectThrow : ObjectRelease
    {
        #region Serialized Fields

        [Header("Throw")]
        [Tooltip("Launch strength, aim-relative direction and angular velocity.")]
        [SerializeField]
        private ThrowSettings trajectory = new ThrowSettings();

        #endregion

        #region Properties

        /// <summary>Identifies the Throw feature in the tool.</summary>
        public override SingleInteractionKind Kind => SingleInteractionKind.Throw;
        /// <summary>Launch parameters applied after the common release transition.</summary>
        public ThrowSettings Trajectory => trajectory;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Validates the launch in addition to its Grab dependency and release profile.</summary>
        /// <param name="warning">Receives the first invalid setting.</param>
        /// <returns>True when the complete Throw configuration is usable.</returns>
        protected override bool TryValidateSettings(out string warning)
        {
            // Shared release validation keeps Drop and Throw requirements consistent.
            if (!base.TryValidateSettings(out warning))
                return false;
            warning = "Throw trajectory settings are missing.";
            return trajectory != null && trajectory.TryValidate(out warning);
        }

        #endregion

        #region Launch

        /// <summary>Launches once from rest after release physics has established mass and constraints.</summary>
        /// <param name="view">Gameplay camera defining the launch frame.</param>
        internal override void Execute(Transform view)
        {
            Quaternion aim = view.rotation;
            Vector3 direction = trajectory.Direction(aim);
            SliceCarryGroup group = GetComponent<SliceCarryGroup>();
            // A sequential throw launches the actual output while the carrier keeps the other pieces.
            if (Grab.IsHeld && group != null && group.TryReleasePiece(direction, PhysicsSettings, out Rigidbody piece))
            {
                if (piece.TryGetComponent(out ObjectGrab output))
                    output.RecordThrow();
                Launch(piece, direction, aim, false);
                return;
            }

            // Use an impulse or velocity change without multiplying a one-shot action by frame time.
            bool carried = Grab.IsHeld;
            base.Execute(view);
            if (!carried || Grab.IsHeld)
                return;
            Grab.RecordThrow();
            Launch(Grab.Body, direction, aim, group != null);
        }

        /// <summary>Applies the configured launch to the body already restored by the release.</summary>
        /// <param name="body">Released body with its final mass and constraints.</param>
        /// <param name="direction">Normalized throw direction in world space.</param>
        /// <param name="aim">Camera rotation used to orient spin.</param>
        /// <param name="separatingGroup">Write velocity immediately so a grouped throw can distribute it to its pieces.</param>
        private void Launch(Rigidbody body, Vector3 direction, Quaternion aim, bool separatingGroup)
        {
            Vector3 launch = direction * trajectory.Strength;
            bool impulse = trajectory.Mode == ThrowStrengthMode.Impulse;
            // A slice carrier separates in this input frame, before PhysX could integrate a queued impulse.
            if (separatingGroup)
                body.linearVelocity = launch / (impulse ? body.mass : 1f);
            else
                body.AddForce(launch, impulse ? ForceMode.Impulse : ForceMode.VelocityChange);
            body.angularVelocity = aim * trajectory.Spin;
            if (trajectory.Sound)
                StudioAudio.Play("sfx_throw", body.transform);
        }

        #endregion

        #endregion
    }
}
