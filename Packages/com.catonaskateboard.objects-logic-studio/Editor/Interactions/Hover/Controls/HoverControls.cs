using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
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
            if (sections.Draw("Detection", "Choose targeting, player range and visibility checks.", settings, "targetMode", "playerDistance", "centerRadius", "queryInterval", "releaseDelay", "obstacleMask"))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "targetMode");
                    Field(settings, "playerDistance");
                    if ((HoverDetectionMode)settings.FindPropertyRelative("targetMode").enumValueIndex == HoverDetectionMode.ViewCenter)
                        Field(settings, "centerRadius");
                    Field(settings, "queryInterval");
                    Field(settings, "releaseDelay");
                    Field(settings, "obstacleMask");
                }
            if (sections.Draw("Suspension", "Temporarily hide this hover while carried or moving.", settings, "suspendCarried", "suspendMoving", "speedThreshold"))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "suspendCarried");
                    Field(settings, "suspendMoving");
                    if (settings.FindPropertyRelative("suspendMoving").boolValue)
                        Field(settings, "speedThreshold");
                }
            if (sections.Draw("Placement", "Offset the detection anchor and final label.", settings, "anchorOffset", "worldOffset", "screenOffset", "followSmoothing"))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "anchorOffset");
                    Field(settings, "worldOffset");
                    Field(settings, "screenOffset");
                    Field(settings, "followSmoothing");
                }
            if (sections.Draw("Appearance", "Choose instant appearance or an animated pop-up.", settings, "appearance", "duration", "startScale", "popIn", "exitDuration"))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "appearance");
                    if ((HoverAppearance)settings.FindPropertyRelative("appearance").enumValueIndex == HoverAppearance.PopUp)
                    {
                        Field(settings, "duration");
                        Field(settings, "startScale");
                    }
                    Field(settings, "popIn");
                    if (settings.FindPropertyRelative("popIn").boolValue)
                        Field(settings, "exitDuration");
                }
            DrawStyle(configuration.FindPropertyRelative("style"), sections);
        }

        /// <summary>Shows typography, layout and only background options used by the selected style.</summary>
        /// <param name="style">Reusable appearance data.</param>
        /// <param name="sections">Retained dropdown visibility.</param>
        private static void DrawStyle(SerializedProperty style, ObjectStudioSections sections)
        {
            // Appearance has one reusable source instead of independent native-graphic edits.
            if (sections.Draw("Text", "Edit the content, font and text rendering options.", style, "content", "font", "fontSize", "fontStyle", "textColor", "alignment", "richText", "horizontalOverflow", "verticalOverflow"))
                using (new EditorGUI.IndentLevelScope())
                {
                    SerializedProperty content = style.FindPropertyRelative("content");
                    EditorGUILayout.LabelField(new GUIContent("Content", content.tooltip));
                    content.stringValue = StudioGUI.TextArea(StudioFieldMenu.Value(content, content.stringValue), GUILayout.MinHeight(42f));
                    Field(style, "font");
                    Field(style, "fontSize");
                    Field(style, "fontStyle");
                    Field(style, "textColor");
                    Field(style, "alignment");
                    Field(style, "richText");
                    Field(style, "horizontalOverflow");
                    Field(style, "verticalOverflow");
                }
            if (sections.Draw("Layout", "Size and inset the preauthored label in reference pixels.", style, "size", "textSizeOffset", "textOffset", "sortingOrder"))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(style, "size");
                    Field(style, "textSizeOffset");
                    Field(style, "textOffset");
                    Field(style, "sortingOrder");
                }
            if (sections.Draw("Background", "Enable and configure the existing background graphic.", style, "showBackground", "backgroundColor", "backgroundSprite", "backgroundType"))
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
                            type.enumValueIndex = StudioGUI.Popup(StudioFieldMenu.Value(type, new GUIContent(type.displayName, type.tooltip)), type.enumValueIndex, backgroundTypes);
                        }
                    }
                }
        }

        #endregion

        #region Fields

        /// <summary>Draws a scalar with its authored tooltip and without Header spacing inside a dropdown.</summary>
        /// <param name="parent">Owning serialized block.</param>
        /// <param name="name">Serialized field name.</param>
        /// <param name="caption">Optional compact field caption.</param>
        internal static void Field(SerializedProperty parent, string name, string caption = null)
        {
            // Native property fields remain available for asset references and layer masks.
            SerializedProperty property = parent.FindPropertyRelative(name);
            GUIContent label = new GUIContent(caption ?? Label(property), property.tooltip);
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    property.boolValue = StudioGUI.Toggle(StudioFieldMenu.Value(property, label), property.boolValue);
                    break;
                case SerializedPropertyType.Integer:
                    property.intValue = StudioGUI.IntField(StudioFieldMenu.Value(property, label), property.intValue);
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = StudioGUI.FloatField(StudioFieldMenu.Value(property, label), property.floatValue);
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = StudioGUI.TextField(StudioFieldMenu.Value(property, label), property.stringValue);
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = StudioGUI.Popup(StudioFieldMenu.Value(property, label), property.enumValueIndex, property.enumDisplayNames);
                    break;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = StudioGUI.Vector2Field(StudioFieldMenu.Value(property, label), property.vector2Value);
                    break;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = StudioGUI.Vector3Field(StudioFieldMenu.Value(property, label), property.vector3Value);
                    break;
                case SerializedPropertyType.Color:
                    property.colorValue = StudioGUI.ColorField(StudioFieldMenu.Value(property, label), property.colorValue);
                    break;
                default:
                    StudioGUI.PropertyField(property, label, true);
                    break;
            }
        }

        /// <summary>Keeps nested interaction labels concise while tooltips retain the full explanation.</summary>
        /// <param name="property">Setting whose display name may exceed the shared label column.</param>
        /// <returns>A compact context-specific label.</returns>
        internal static string Label(SerializedProperty property)
        {
            // Existing serialized names and preset compatibility remain independent of presentation.
            return property.name switch
            {
                "WhileContact" => "While in Contact",
                "AutoReturnDuration" => "Return Duration",
                "AutoReturnRotation" => "Return Rotation",
                "AcceptReturnedProduct" => "Accept Returns",
                "LimitToDispenserSpace" => "Dispenser Spaces",
                "ForwardRotation" => "Rotation to B",
                "ReturnRotation" => "Rotation to A",
                "CompleteAfterSeparation" => "Finish on Separation",
                "ContactFlagOperation" => "Flag Operation",
                "ContactQueryInterval" => "Contact Interval",
                "RepeatAfterSeparation" => "Repeat on Separation",
                "RequireSightToContinue" => "Sight to Continue",
                "RequireSightToStart" => "Sight to Start",
                "ResumeAfterInterruption" => "Resume Interrupted",
                "HoverOnly" => "Only While Hovered",
                _ => property.displayName
            };
        }

        #endregion

        #endregion
    }
}
