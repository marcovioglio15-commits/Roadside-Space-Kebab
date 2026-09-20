using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Shares one temporary input routing override across preview windows during an owned test.</summary>
    internal static class PlayerQuickPlayInputScope
    {
        #region State

        private static InputSettings original;
        private static InputSettings temporary;
        private static int users;

        #endregion

        #region Methods

        #region Ownership

        /// <summary>Routes gameplay input to the test without modifying the project settings asset.</summary>
        internal static void Acquire()
        {
            // Nested windows share one copy so no window can restore a copy destroyed by another.
            if (users++ > 0)
                return;
            original = InputSystem.settings;
            temporary = UnityEngine.Object.Instantiate(original);
            temporary.hideFlags = HideFlags.HideAndDontSave;
            temporary.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            temporary.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            // Input System destroys a replaced HideAndDontSave settings object; preserve its temporary default.
            HideFlags flags = original.hideFlags;
            if (flags == HideFlags.HideAndDontSave)
                original.hideFlags = flags & ~HideFlags.DontSaveInEditor;
            try
            {
                InputSystem.settings = temporary;
            }
            finally
            {
                if (original != null)
                    original.hideFlags = flags;
            }
        }

        /// <summary>Restores the original routing after the last preview releases ownership.</summary>
        internal static void Release()
        {
            // Never overwrite another system's explicit replacement of the settings object.
            if (users == 0 || --users > 0)
                return;
            if (InputSystem.settings == temporary && original != null)
                InputSystem.settings = original;
            if (temporary != null)
                UnityEngine.Object.DestroyImmediate(temporary);
            temporary = original = null;
        }

        #endregion

        #endregion
    }
}
