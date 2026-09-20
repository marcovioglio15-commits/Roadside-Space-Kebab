using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Draws compact serialized controls while retaining each field's authored tooltip.</summary>
    public static class StudioFields
    {
        #region Methods
        #region Fields
        /// <summary>Draws a series of fields relative to one serialized object or list element.</summary>
        /// <param name="parent">Serialized container holding the fields.</param>
        /// <param name="names">Direct child field names in display order.</param>
        public static void Draw(SerializedProperty parent, params string[] names)
        {
            // Reuse Unity's native Undo-aware widgets and tooltips.
            foreach (string name in names)
            {
                SerializedProperty property = parent.FindPropertyRelative(name);
                if (property != null)
                    EditorGUILayout.PropertyField(property, true);
            }
        }

        /// <summary>Draws named top-level fields on a preset.</summary>
        /// <param name="data">Serialized preset draft.</param>
        /// <param name="names">Top-level field names in display order.</param>
        public static void Draw(SerializedObject data, params string[] names)
        {
            // Do not rebuild inspectors or create editors on every repaint.
            foreach (string name in names)
            {
                SerializedProperty property = data.FindProperty(name);
                if (property != null)
                    EditorGUILayout.PropertyField(property, true);
            }
        }

        /// <summary>Displays a compact command with an explicit operation tooltip.</summary>
        /// <param name="label">Visible command text.</param>
        /// <param name="tooltip">Scope of the operation.</param>
        /// <returns>Whether the command was clicked.</returns>
        public static bool Button(string label, string tooltip)
        {
            // GUIContent makes otherwise hidden editor behavior discoverable on hover.
            return GUILayout.Button(new GUIContent(label, tooltip));
        }
        #endregion
        #endregion
    }
}
