using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Validates and applies capsule dimensions without moving the player or creating components.</summary>
    public static class PlayerCharacterControllerBody
    {
        #region Methods

        #region Validation

        /// <summary>Checks geometry and native integration before any CharacterController property is written.</summary>
        /// <param name="controller">Explicit native component that must belong to the owner itself.</param>
        /// <param name="owner">Player root, upright and at unit world scale.</param>
        /// <param name="settings">Requested Body dimensions; default or invalid data is rejected.</param>
        /// <param name="warning">Receives the first incompatibility without correcting any value.</param>
        /// <returns>True when the component can receive these dimensions.</returns>
        public static bool TryValidate(CharacterController controller, Transform owner, PlayerBodySettings settings, out string warning)
        {
            // Current-pose callers and draft-pose callers share the same binding rules.
            return TryValidate(controller, owner, settings, owner != null ? owner.lossyScale : Vector3.zero,
                owner != null ? owner.up : Vector3.zero, out warning);
        }

        /// <summary>Checks a proposed world pose without temporarily moving the real player.</summary>
        /// <param name="controller">Explicit native component on the owner.</param>
        /// <param name="owner">Root used to check component ownership and Rigidbody conflicts.</param>
        /// <param name="settings">Proposed capsule dimensions.</param>
        /// <param name="worldScale">World scale of the proposed root.</param>
        /// <param name="worldUp">Up direction of the proposed root rotation.</param>
        /// <param name="warning">Receives an incompatibility without changing any object.</param>
        /// <returns>True when the candidate pose and geometry satisfy the native binding.</returns>
        public static bool TryValidate(CharacterController controller, Transform owner, PlayerBodySettings settings,
            Vector3 worldScale, Vector3 worldUp, out string warning)
        {
            // Public callers may supply default settings, so do not assume the struct is valid.
            if (!PlayerBodySettings.TryCreate(settings.Radius, settings.Height, out _, out warning))
                return false;

            // Keep the capsule on the same root whose feet define the Body centre.
            if (controller == null || owner == null || controller.transform != owner)
            {
                warning = "Assign a CharacterController on the same GameObject as the Player Host.";
                return false;
            }

            // This first native binding supports a vertical capsule, including yaw rotation only.
            if (worldScale != Vector3.one || worldUp != Vector3.up)
            {
                warning = "The CharacterController body requires unit world scale and an upright root. Rotate or scale a visual child instead.";
                return false;
            }

            // A Rigidbody on the root or an ancestor would introduce another source of body motion.
            if (owner.GetComponentInParent<Rigidbody>(true) != null)
            {
                warning = "Remove the Rigidbody from this root or its ancestors before using the CharacterController body binding.";
                return false;
            }

            // Unity rejects a step offset above the requested height; never lower it implicitly.
            if (!float.IsFinite(controller.stepOffset) || controller.stepOffset < 0f || controller.stepOffset > settings.Height)
            {
                warning = "CharacterController Step Offset must be finite, non-negative and no greater than the Body height.";
                return false;
            }

            return true;
        }

        #endregion

        #region Configuration

        /// <summary>Applies a valid Body explicitly, leaving solver tuning, enabled state and pose unchanged.</summary>
        /// <param name="controller">Existing native capsule to configure.</param>
        /// <param name="owner">Player root used to verify the component and pose.</param>
        /// <param name="settings">Body dimensions to apply to radius, height and centre.</param>
        /// <param name="warning">Receives the reason the operation was refused.</param>
        /// <returns>True when validation succeeded and the geometry was assigned.</returns>
        public static bool TryConfigure(CharacterController controller, Transform owner, PlayerBodySettings settings, out string warning)
        {
            // Complete all checks before assigning even the first dimension.
            if (!TryValidate(controller, owner, settings, out warning))
                return false;

            ApplyValidated(controller, settings);
            return true;
        }

        /// <summary>Writes the three owned properties after validation in the same initialization or sync operation.</summary>
        /// <param name="controller">Existing component already accepted by TryValidate.</param>
        /// <param name="settings">Validated dimensions used by that check.</param>
        internal static void ApplyValidated(CharacterController controller, PlayerBodySettings settings)
        {
            // Radius precedes total height; centre keeps the player's root at its feet.
            controller.radius = settings.Radius;
            controller.height = settings.Height;
            controller.center = settings.Center;
        }

        #endregion

        #endregion
    }
}
