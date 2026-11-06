using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Configures dismantling input, the scatter impulse and optional camera movement.</summary>
    [Serializable]
    public sealed class AssemblyDismantleSettings
    {
        #region Fields

        [Header("Dismantling")]
        [Tooltip("Dedicated Button for a ready recipe. Leave empty to disable dismantling for this recipe.")]
        public InputActionReference Action;
        [Tooltip("Require a continuous hold on the dismantle button.")]
        public bool Hold;
        [Tooltip("Seconds to hold while aiming at the same recipe.")]
        [Range(0.1f, 5f)]
        public float Duration = 0.6f;
        [Tooltip("Upward speed added to each separated ingredient, in metres per second.")]
        [Range(0.1f, 4f)]
        public float Lift = 1.4f;
        [Tooltip("Outward speed added to each separated ingredient, in metres per second.")]
        [Range(0f, 3f)]
        public float Spread = 0.65f;

        [Header("Camera")]
        [Tooltip("Arc above the dish, strike vertically and spring back.")]
        public bool CameraImpact;
        [Tooltip("Total time for the flight, strike and return, in seconds.")]
        [Range(0.25f, 2f)]
        public float CameraDuration = 0.92f;
        [Tooltip("Height added to the flight arc, in metres.")]
        [Range(0.1f, 1.5f)]
        public float CameraArcHeight = 0.78f;
        [Tooltip("Minimum camera height above the dish at the strike, in metres.")]
        [Range(0.08f, 0.6f)]
        public float CameraClearance = 0.19f;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks active input, launch and feedback settings.</summary>
        /// <param name="assembly">Button used for prolonged assembly.</param>
        /// <param name="warning">Receives the first configuration problem.</param>
        /// <returns>True when dismantling is disabled or can run safely.</returns>
        internal bool TryValidate(InputActionReference assembly, out string warning)
        {
            warning = string.Empty;
            if (Action == null)
                return true;
            if (Action.action == null || Action.action.type != InputActionType.Button || Action.action.id == assembly.action.id)
                warning = "Choose a dismantle Button distinct from the assembly Button.";
            else if (Hold && !InteractionValues.Positive(Duration) || !InteractionValues.Positive(Lift)
                || !float.IsFinite(Spread) || Spread < 0f)
                warning = "Dismantling requires valid hold and launch values.";
            else if (CameraImpact && (!InteractionValues.Positive(CameraDuration) || !InteractionValues.Positive(CameraArcHeight)
                || !InteractionValues.Positive(CameraClearance)))
                warning = "Choose positive, finite values for camera duration, arc height and clearance.";
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
