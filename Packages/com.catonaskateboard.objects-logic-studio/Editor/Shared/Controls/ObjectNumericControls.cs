using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Pairs numeric entries with useful sliders without rewriting invalid proposals.</summary>
    internal static class ObjectNumericControls
    {
        #region Methods

        #region Fields

        /// <summary>Draws vector axes with independent numeric entries and sliders.</summary>
        /// <param name="property">Serialized Vector3.</param>
        /// <param name="minimum">Slider's lower guide.</param>
        /// <param name="maximum">Slider's upper guide.</param>
        /// <param name="caption">Optional caption describing this vector's reference.</param>
        internal static void Vector(SerializedProperty property, float minimum = -5f, float maximum = 5f, string caption = null)
        {
            // Keep native values outside the slider guide until the user edits them.
            EditorGUILayout.LabelField(new GUIContent(caption ?? property.displayName, property.tooltip));
            using StudioIndentScope indent = new StudioIndentScope();
            foreach (string axis in new[] { "x", "y", "z" })
            {
                SerializedProperty coordinate = property.FindPropertyRelative(axis);
                EditorGUI.BeginChangeCheck();
                float value = StudioFieldGUI.Slider(new GUIContent(axis.ToUpperInvariant(), property.tooltip), coordinate.floatValue, minimum, maximum);
                if (EditorGUI.EndChangeCheck())
                    coordinate.floatValue = value;
            }
        }

        /// <summary>Draws a serialized integer using a slider and native numeric entry.</summary>
        /// <param name="property">Serialized integer.</param>
        /// <param name="minimum">Lower slider guide.</param>
        /// <param name="maximum">Upper slider guide.</param>
        internal static void Integer(SerializedProperty property, int minimum = 0, int maximum = 100)
        {
            // Only explicit input commits a change to the draft.
            EditorGUI.BeginChangeCheck();
            int value = EditorGUILayout.IntSlider(new GUIContent(property.displayName, property.tooltip), property.intValue, minimum, maximum);
            if (EditorGUI.EndChangeCheck())
                property.intValue = value;
        }

        #endregion

        #endregion
    }
}
