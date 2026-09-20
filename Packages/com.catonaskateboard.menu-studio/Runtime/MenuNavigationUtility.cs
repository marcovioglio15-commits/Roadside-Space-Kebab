using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Links existing vertical controls while skipping options hidden by current settings.</summary>
    public static class MenuNavigationUtility
    {
        #region Methods
        #region Navigation
        /// <summary>Updates navigation neighbors after generation or a tab visibility change.</summary>
        /// <param name="controls">Controls in visual order.</param>
        /// <param name="wrap">Wrap the first and last reachable controls.</param>
        /// <param name="activeOnly">Skip currently inactive or disabled controls at runtime.</param>
        public static void Link(IReadOnlyList<Selectable> controls, bool wrap, bool activeOnly)
        {
            // Navigation structs update only when structure or visibility changes.
            for (int index = 0; index < controls.Count; index++)
                if (controls[index] != null)
                    controls[index].navigation = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        selectOnUp = Find(controls, index, -1, wrap, activeOnly),
                        selectOnDown = Find(controls, index, 1, wrap, activeOnly)
                    };
        }

        /// <summary>Finds the next available neighbor without allocating a filtered list.</summary>
        /// <param name="controls">Ordered selectable list.</param>
        /// <param name="origin">Starting control index.</param>
        /// <param name="direction">Search direction.</param>
        /// <param name="wrap">Allow crossing either boundary.</param>
        /// <param name="activeOnly">Require active interactable controls.</param>
        /// <returns>The next selectable or null at an unwrapped boundary.</returns>
        private static Selectable Find(IReadOnlyList<Selectable> controls, int origin, int direction, bool wrap, bool activeOnly)
        {
            // A bounded scan also handles tabs with zero or one reachable option.
            int candidate = origin;
            for (int step = 0; step < controls.Count; step++)
            {
                candidate += direction;
                if (candidate < 0 || candidate >= controls.Count)
                {
                    if (!wrap)
                        return null;
                    candidate = (candidate + controls.Count) % controls.Count;
                }
                Selectable selectable = controls[candidate];
                if (selectable != null && (!activeOnly || selectable.isActiveAndEnabled && selectable.IsInteractable()))
                    return selectable;
            }
            return null;
        }
        #endregion
        #endregion
    }

    /// <summary>Stores each tab's complete focus order, including shared Apply and Back buttons.</summary>
    [Serializable]
    public sealed class MenuTabNavigation
    {
        #region Fields
        [Tooltip("Preauthored controls in vertical order for this tab.")]
        public Selectable[] Controls = Array.Empty<Selectable>();
        #endregion
    }
}
