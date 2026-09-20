using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Coordinates pause and cursor ownership across simultaneously loaded menu hosts.</summary>
    internal static class MenuSession
    {
        #region Fields
        private static readonly HashSet<MenuHost> owners = new HashSet<MenuHost>();
        private static readonly HashSet<MenuHost> paused = new HashSet<MenuHost>();
        private static float previousScale;
        private static CursorLockMode previousLock;
        private static bool previousVisible;
        #endregion

        #region Methods
        #region Ownership
        /// <summary>Resets static ownership when entering Play mode with domain reload disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            // Runtime hosts rebuild ownership through their normal activation lifecycle.
            owners.Clear();
            paused.Clear();
        }

        /// <summary>Acquires cursor visibility and optional time-scale pause.</summary>
        /// <param name="owner">Menu opening a visible panel.</param>
        internal static void Acquire(MenuHost owner)
        {
            // The first owner captures state; later overlays cannot overwrite that baseline.
            if (!owners.Add(owner))
                return;
            if (owners.Count == 1)
            {
                previousLock = Cursor.lockState;
                previousVisible = Cursor.visible;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (owner.Kind != MenuKind.Pause || !paused.Add(owner))
                return;
            if (paused.Count == 1)
                previousScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        /// <summary>Restores state only after the last relevant owner releases it.</summary>
        /// <param name="owner">Menu being closed, disabled or destroyed.</param>
        internal static void Release(MenuHost owner)
        {
            // Disabling a paused host or unloading its scene must never leave time frozen.
            if (paused.Remove(owner) && paused.Count == 0)
                Time.timeScale = previousScale;
            if (!owners.Remove(owner) || owners.Count > 0)
                return;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }
        #endregion
        #endregion
    }
}
