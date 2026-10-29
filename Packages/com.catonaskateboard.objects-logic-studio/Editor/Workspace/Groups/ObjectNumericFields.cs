using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Gives numeric object settings slider assistance without changing values on repaint.</summary>
    internal static class ObjectNumericFields
    {
        #region Methods

        #region Drawing

        /// <summary>Draws numeric fields and vector axes with precise entry beside a suggested interval.</summary>
        /// <param name="property">Serialized setting.</param>
        /// <param name="label">Caption and tooltip.</param>
        /// <returns>True when the field was drawn or is outside the focused group.</returns>
        internal static bool Draw(SerializedProperty property, GUIContent label)
        {
            if (!ObjectGroupScope.Allows(property))
                return true;
            label.tooltip = ObjectFieldText.Tooltip(property);
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                case SerializedPropertyType.Integer:
                    Scalar(property, label, Bounds(property));
                    return true;
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector4:
                case SerializedPropertyType.Vector2Int:
                case SerializedPropertyType.Vector3Int:
                    property.isExpanded = StudioArrayGUI.Foldout(property, label);
                    if (!property.isExpanded)
                        return true;
                    using (new StudioIndentScope())
                    {
                        int count = property.propertyType is SerializedPropertyType.Vector2 or SerializedPropertyType.Vector2Int ? 2
                            : property.propertyType == SerializedPropertyType.Vector4 ? 4 : 3;
                        Vector2 range = Bounds(property);
                        for (int index = 0; index < count; index++)
                        {
                            string axis = index switch { 0 => "x", 1 => "y", 2 => "z", _ => "w" };
                            Scalar(property.FindPropertyRelative(axis), new GUIContent(axis.ToUpperInvariant(), label.tooltip), range);
                        }
                    }
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Updates a scalar only after the user edits the number or slider.</summary>
        /// <param name="property">Numeric field or vector axis.</param>
        /// <param name="label">Short caption.</param>
        /// <param name="range">Suggested lower and upper bounds.</param>
        private static void Scalar(SerializedProperty property, GUIContent label, Vector2 range)
        {
            EditorGUI.BeginChangeCheck();
            if (property.propertyType == SerializedPropertyType.Integer)
            {
                int value = StudioFieldGUI.IntField(StudioFieldMenu.Value(property, label), property.intValue, Mathf.CeilToInt(range.x), Mathf.FloorToInt(range.y));
                if (EditorGUI.EndChangeCheck())
                    property.intValue = value;
            }
            else
            {
                float value = StudioFieldGUI.FloatField(StudioFieldMenu.Value(property, label), property.floatValue, range.x, range.y);
                if (EditorGUI.EndChangeCheck())
                    property.floatValue = value;
            }
        }

        /// <summary>Uses declared ranges first and small useful suggestions for unbounded fields.</summary>
        /// <param name="property">Numeric or vector setting.</param>
        /// <returns>Suggested bounds that never clamp existing stored values.</returns>
        private static Vector2 Bounds(SerializedProperty property)
        {
            RangeAttribute declared = StudioFieldGUI.RangeFor(property);
            if (declared != null)
                return new Vector2(declared.min, declared.max);
            string name = property.name.ToLowerInvariant();
            if (name.Contains("rotation") || name.Contains("angle") || name.Contains("yaw") || name.Contains("elevation"))
                return new Vector2(-180f, 180f);
            if (name.Contains("scale"))
                return new Vector2(0.01f, 5f);
            if (property.propertyType is SerializedPropertyType.Vector2 or SerializedPropertyType.Vector3 or SerializedPropertyType.Vector4
                or SerializedPropertyType.Vector2Int or SerializedPropertyType.Vector3Int)
                return new Vector2(-10f, 10f);
            return new Vector2(0f, property.propertyType == SerializedPropertyType.Integer ? 128f : 20f);
        }

        #endregion

        #endregion
    }
}
