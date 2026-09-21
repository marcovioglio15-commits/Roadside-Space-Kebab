using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Draws source controls without owning drafts, changing master slots or writing assets.</summary>
    internal static class PlayerStudioSourceView
    {
        #region Labels

        private static readonly GUIContent masterLabel = new GUIContent("Master", "Master to configure. Its Body Slot is edited separately from the Body's dimensions.");
        private static readonly GUIContent targetLabel = new GUIContent("Editing Body", "Exact asset opened by this session. Apply updates this shared asset, affecting every master that uses it.");

        #endregion

        #region Methods

        #region Drawing

        /// <summary>Collects a requested selection while showing only the controls used by its route.</summary>
        /// <param name="selection">Current source route and master.</param>
        /// <param name="mode">Receives the requested route.</param>
        /// <param name="master">Receives the requested master, or null in direct Body mode.</param>
        /// <param name="body">Receives the requested direct Body, or null in master mode.</param>
        /// <returns>True when a source control changed during this GUI event.</returns>
        public static bool Draw(PlayerStudioSelection selection, out PlayerStudioSourceMode mode, out PlayerMasterPreset master, out PlayerBodyPreset body)
        {
            // The master exposes every module; there is no separate Body-only editing mode.
            EditorGUI.BeginChangeCheck();
            mode = PlayerStudioSourceMode.Master;
            body = null;
            master = PlayerPresetPicker.Draw(masterLabel, selection.Master);
            return EditorGUI.EndChangeCheck();
        }

        /// <summary>Shows the exact opened Body so a changed master slot cannot hide the draft's destination.</summary>
        /// <param name="session">Session whose original target remains fixed while editing.</param>
        public static void DrawTarget(PlayerBodyEditSession session)
        {
            // This reference is informative; selecting another target belongs to the source controls.
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(targetLabel, session.Source, typeof(PlayerBodyPreset), false);
        }

        #endregion

        #endregion
    }
}
