using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Removes runtime-only card and settings changes before returning to Edit mode.</summary>
    [InitializeOnLoad]
    internal static class InteractionRuntimeCleanup
    {
        #region Methods

        #region Session

        /// <summary>Registers one cleanup callback after each editor assembly reload.</summary>
        static InteractionRuntimeCleanup()
        {
            EditorApplication.playModeStateChanged += Changed;
        }

        /// <summary>Restores copied settings before disabled scene reload can preserve them outside Play.</summary>
        /// <param name="state">Current editor Play transition.</param>
        private static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                InteractionRuntimeState.RestoreSession();
        }

        #endregion

        #endregion
    }
}
