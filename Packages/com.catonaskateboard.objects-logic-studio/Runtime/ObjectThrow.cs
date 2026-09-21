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
            // Use an impulse or velocity change without multiplying a one-shot action by frame time.
            base.Execute(view);
            Grab.Body.AddForce(trajectory.Direction(view.rotation) * trajectory.Strength,
                trajectory.Mode == ThrowStrengthMode.Impulse ? ForceMode.Impulse : ForceMode.VelocityChange);
            Grab.Body.angularVelocity = view.rotation * trajectory.Spin;
        }

        #endregion

        #endregion
    }
}
