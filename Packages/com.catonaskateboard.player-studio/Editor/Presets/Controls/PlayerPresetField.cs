using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Draws preset values inside foldouts without repeating serialized Header decorations.</summary>
    internal static class PlayerPresetField
    {
        #region Methods

        /// <summary>Draws an action reference inside its existing foldout without repeating its serialized Header.</summary>
        /// <param name="serialized">Temporary Input preset edited by the session.</param>
        /// <param name="name">Serialized action role to display.</param>
        internal static void DrawAction(SerializedObject serialized, string name)
        {
            // The popup retains the original reference identity until an explicit selection.
            PlayerInputActionMenu.Draw(serialized, name);
        }

        /// <summary>Uses native field controls and the tooltip defined beside the serialized data.</summary>
        /// <param name="parent">Settings block containing the field.</param>
        /// <param name="name">Serialized field to edit.</param>
        internal static void Draw(SerializedProperty parent, string name)
        {
            // Known scalar controls omit only decorators, preserving serialized values and tooltips.
            using SerializedProperty property = parent.FindPropertyRelative(name);
            GUIContent label = new GUIContent(property.displayName, property.tooltip);
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    property.boolValue = EditorGUILayout.Toggle(label, property.boolValue);
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = EditorGUILayout.FloatField(label, property.floatValue);
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = EditorGUILayout.Popup(label, property.enumValueIndex, property.enumDisplayNames);
                    break;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = EditorGUILayout.Vector2Field(label, property.vector2Value);
                    break;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = EditorGUILayout.Vector3Field(label, property.vector3Value);
                    break;
                default:
                    EditorGUILayout.PropertyField(property, label, true);
                    break;
            }
        }

        #endregion
    }
}
