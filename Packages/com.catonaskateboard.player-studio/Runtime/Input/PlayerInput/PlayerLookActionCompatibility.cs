using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Separates displacement bindings from continuous look values at configuration boundaries.</summary>
    public static class PlayerLookActionCompatibility
    {
        #region Methods

        #region Validation

        /// <summary>Checks look units from binding layouts without requiring a connected device.</summary>
        /// <param name="action">Vector action to inspect before subscribing or presenting it in a menu.</param>
        /// <param name="displacement">True for pointer delta; false for a continuous rate such as a stick.</param>
        /// <returns>True when the action shape and every configured binding have the requested units.</returns>
        public static bool IsCompatible(InputAction action, bool displacement)
        {
            // Mixed pointer and stick actions cannot be scaled consistently by a single camera role.
            if (action == null || action.type == InputActionType.Button || action.expectedControlType != "Vector2")
                return false;
            foreach (InputBinding binding in action.bindings)
            {
                if (binding.isPartOfComposite || string.IsNullOrEmpty(binding.effectivePath))
                    continue;
                string layout = binding.isComposite ? null : InputControlPath.TryGetControlLayout(binding.effectivePath);
                bool delta = layout != null && (layout == "Delta" || InputSystem.IsFirstLayoutBasedOnSecond(layout, "Delta"));
                if (delta != displacement)
                    return false;
            }
            return true;
        }

        #endregion

        #endregion
    }
}
