using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws preset fields inside persistent dropdowns without repeating serialized headers.</summary>
    internal static class HoverControls
    {
        #region State

        private static readonly string[] backgroundTypes = { "Simple", "Sliced" };

        #endregion

        #region Methods

        #region Configuration

        /// <summary>Edits a detached workspace proposal or a preset inspector through the same controls.</summary>
        /// <param name="configuration">Configuration property owned by the caller.</param>
        /// <param name="sections">Retained dropdown visibility.</param>
        internal static void Draw(SerializedProperty configuration, ObjectStudioSections sections)
        {
            // Closing a section changes only navigation; hidden values remain in the draft.
            SerializedProperty settings = configuration.FindPropertyRelative("settings");
            if (sections.Draw("Detection", "Choose targeting, player range and visibility checks."))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "targetMode");
                    Field(settings, "playerDistance");
                    if ((HoverTargetMode)settings.FindPropertyRelative("targetMode").enumValueIndex == HoverTargetMode.ViewCenter)
                        Field(settings, "centerRadius");
                    Field(settings, "queryInterval");
                    Field(settings, "obstacleMask");
                }
            if (sections.Draw("Placement", "Offset the detection anchor and final label."))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "anchorOffset");
                    Field(settings, "worldOffset");
                    Field(settings, "screenOffset");
                }
            if (sections.Draw("Appearance", "Choose instant appearance or an animated pop-up."))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "appearance");
                    if ((HoverAppearance)settings.FindPropertyRelative("appearance").enumValueIndex == HoverAppearance.PopUp)
                    {
                        Field(settings, "duration");
                        Field(settings, "startScale");
                    }
                }
            DrawStyle(configuration.FindPropertyRelative("style"), sections);
        }

        /// <summary>Shows typography, layout and only background options used by the selected style.</summary>
        /// <param name="style">Reusable appearance data.</param>
        /// <param name="sections">Retained dropdown visibility.</param>
        private static void DrawStyle(SerializedProperty style, ObjectStudioSections sections)
        {
            // Appearance has one reusable source instead of independent native-graphic edits.
            if (sections.Draw("Text", "Edit the content, font and text rendering options."))
                using (new EditorGUI.IndentLevelScope())
                {
                    SerializedProperty content = style.FindPropertyRelative("content");
                    EditorGUILayout.LabelField(new GUIContent("Content", content.tooltip));
                    content.stringValue = EditorGUILayout.TextArea(content.stringValue, GUILayout.MinHeight(42f));
                    Field(style, "font");
                    Field(style, "fontSize");
                    Field(style, "fontStyle");
                    Field(style, "textColor");
                    Field(style, "alignment");
                    Field(style, "richText");
                    Field(style, "horizontalOverflow");
                    Field(style, "verticalOverflow");
                }
            if (sections.Draw("Layout", "Size and inset the preauthored label in reference pixels."))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(style, "size");
                    Field(style, "textSizeOffset");
                    Field(style, "textOffset");
                    Field(style, "sortingOrder");
                }
            if (sections.Draw("Background", "Enable and configure the existing background graphic."))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(style, "showBackground");
                    if (style.FindPropertyRelative("showBackground").boolValue)
                    {
                        Field(style, "backgroundColor");
                        Field(style, "backgroundSprite");
                        if (style.FindPropertyRelative("backgroundSprite").objectReferenceValue != null)
                        {
                            SerializedProperty type = style.FindPropertyRelative("backgroundType");
                            type.enumValueIndex = EditorGUILayout.Popup(new GUIContent(type.displayName, type.tooltip), type.enumValueIndex, backgroundTypes);
                        }
                    }
                }
        }

        #endregion

        #region Fields

        /// <summary>Draws a scalar with its authored tooltip and without Header spacing inside a dropdown.</summary>
        /// <param name="parent">Owning serialized block.</param>
        /// <param name="name">Serialized field name.</param>
        internal static void Field(SerializedProperty parent, string name)
        {
            // Native property fields remain available for asset references and layer masks.
            SerializedProperty property = parent.FindPropertyRelative(name);
            GUIContent label = new GUIContent(property.displayName, property.tooltip);
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    property.boolValue = EditorGUILayout.Toggle(label, property.boolValue);
                    break;
                case SerializedPropertyType.Integer:
                    property.intValue = EditorGUILayout.IntField(label, property.intValue);
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = EditorGUILayout.FloatField(label, property.floatValue);
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = EditorGUILayout.TextField(label, property.stringValue);
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
                case SerializedPropertyType.Color:
                    property.colorValue = EditorGUILayout.ColorField(label, property.colorValue);
                    break;
                default:
                    EditorGUILayout.PropertyField(property, label, true);
                    break;
            }
        }

        #endregion

        #endregion
    }
}
