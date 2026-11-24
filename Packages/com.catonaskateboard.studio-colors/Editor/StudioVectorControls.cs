using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Shares precise vector entries and axis sliders across Studio layout previews.</summary>
    public static class StudioVectorControls
    {
        #region Methods
        #region Drawing

        /// <summary>Draws vector axes with independent numeric entries and sliders.</summary>
        /// <param name="property">Serialized Vector3.</param>
        /// <param name="minimum">Slider's lower guide.</param>
        /// <param name="maximum">Slider's upper guide.</param>
        /// <param name="caption">Optional caption describing this vector's reference.</param>
        public static void Draw(SerializedProperty property, float minimum = -5f, float maximum = 5f, string caption = null)
        {
            // Keep native values outside the slider guide until the user edits them.
            EditorGUILayout.LabelField(new GUIContent(caption ?? property.displayName, property.tooltip));
            using StudioIndentScope indent = new StudioIndentScope();
            for (int index = 0; index < 3; index++)
            {
                string axis = index switch { 0 => "x", 1 => "y", _ => "z" };
                SerializedProperty coordinate = property.FindPropertyRelative(axis);
                EditorGUI.BeginChangeCheck();
                float value = StudioFieldGUI.Slider(new GUIContent(axis.ToUpperInvariant(), property.tooltip), coordinate.floatValue, minimum, maximum);
                if (EditorGUI.EndChangeCheck())
                    coordinate.floatValue = value;
            }
        }

        #endregion
        #endregion
    }
}
