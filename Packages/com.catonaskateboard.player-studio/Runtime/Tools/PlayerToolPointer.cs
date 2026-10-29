using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Shares pointer projection between visual aiming and instant cursor interactions.</summary>
    internal static class PlayerToolPointer
    {
        #region Methods

        #region Projection

        /// <summary>Uses the mouse in an unlocked screen view and the centered fake cursor in locked or texture views.</summary>
        /// <param name="view">Existing gameplay camera.</param>
        /// <param name="ray">Receives the current world selection ray.</param>
        /// <param name="requireViewport">Requires an in-viewport primary-display pointer for instant interactions; false preserves projectile visual aiming outside the viewport.</param>
        /// <returns>True when an enabled view and the requested pointer policy are available.</returns>
        internal static bool TryRay(Camera view, out Ray ray, bool requireViewport = true)
        {
            ray = default;
            if (view == null || !view.isActiveAndEnabled)
                return false;
            // Render textures and locked input share the preconfigured centered cursor.
            if (Cursor.lockState == CursorLockMode.Locked || view.targetTexture != null || Mouse.current == null)
            {
                ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f));
                return true;
            }
            Vector2 pixel = Mouse.current.position.ReadValue();
            if (requireViewport && (view.targetDisplay != 0 || !view.pixelRect.Contains(pixel)))
                return false;
            ray = view.ScreenPointToRay(pixel);
            return true;
        }

        #endregion

        #endregion
    }
}
