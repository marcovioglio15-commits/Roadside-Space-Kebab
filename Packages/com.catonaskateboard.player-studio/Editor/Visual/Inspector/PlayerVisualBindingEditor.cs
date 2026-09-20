using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Shows managed references without offering untracked edits to the binding's ownership data.</summary>
    [CustomEditor(typeof(PlayerVisualBinding))]
    internal sealed class PlayerVisualBindingEditor : UnityEditor.Editor
    {
        #region Labels

        private static readonly GUIContent editLabel = new GUIContent("Edit in Player Studio", "Open this player's Visual tab. Pending changes in an existing session must be resolved first.");

        #endregion

        #region Methods

        #region Inspector

        /// <summary>Displays applied references and routes editing through the window's shared confirmation.</summary>
        public override void OnInspectorGUI()
        {
            // Ownership and authored pose are maintained by the Apply operation, not direct field editing.
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("host"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("visualRoot"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("model"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sourcePrefab"));
            }

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(target)))
                if (GUILayout.Button(editLabel))
                    PlayerStudioWindow.OpenVisual(((PlayerVisualBinding)target).Host);
        }

        #endregion

        #endregion
    }
}
