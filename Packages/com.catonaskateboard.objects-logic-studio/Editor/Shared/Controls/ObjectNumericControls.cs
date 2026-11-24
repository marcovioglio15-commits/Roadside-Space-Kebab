using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Pairs numeric entries with useful sliders without rewriting invalid proposals.</summary>
    internal static class ObjectNumericControls
    {
        #region Methods

        #region Fields

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
