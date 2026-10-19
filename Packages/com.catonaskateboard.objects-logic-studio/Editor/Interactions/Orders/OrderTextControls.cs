using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares conditional paragraph controls between base orders and recipe variants.</summary>
    internal static class OrderTextControls
    {
        #region Methods
        #region Drawing

        /// <summary>Exposes typography only when this paragraph requests an independent appearance.</summary>
        /// <param name="style">Detached paragraph settings inside a catalog entry or variant.</param>
        internal static void Draw(SerializedProperty style)
        {
            HoverControls.Field(style, "Enabled", "Customize Board Text");
            if (!style.FindPropertyRelative("Enabled").boolValue)
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            if (OrderSections.Draw(style, "Font & Size", "Typeface, emphasis and fixed or automatic size.",
                "Font", "Material", "Style", "Weight", "AutoSize", "MinimumSize", "MaximumSize", "Size"))
                using (new EditorGUI.IndentLevelScope())
                {
                    SurfaceInteractionControls.Fields(style, "Font", "Material", "Style", "Weight", "AutoSize");
                    if (style.FindPropertyRelative("AutoSize").boolValue)
                        SurfaceInteractionControls.Fields(style, "MinimumSize", "MaximumSize");
                    else
                        HoverControls.Field(style, "Size");
                }
            if (OrderSections.Draw(style, "Paragraph Layout", "Alignment, wrapping and text overflow.", "Alignment", "Wrapping", "Overflow", "RichText"))
                using (new EditorGUI.IndentLevelScope())
                    SurfaceInteractionControls.Fields(style, "Alignment", "Wrapping", "Overflow", "RichText");
            if (OrderSections.Draw(style, "Spacing & Margins", "Character and paragraph spacing, indentation and margins.",
                "CharacterSpacing", "WordSpacing", "LineSpacing", "ParagraphSpacing", "Indent", "SpaceBefore", "SpaceAfter", "Margins"))
                using (new EditorGUI.IndentLevelScope())
                {
                    SurfaceInteractionControls.Fields(style, "CharacterSpacing", "WordSpacing", "LineSpacing", "ParagraphSpacing", "Indent", "SpaceBefore", "SpaceAfter");
                    Margins(style.FindPropertyRelative("Margins"));
                }
            if (OrderSections.Draw(style, "Color & Gradient", "Text color and optional independent corner colors.",
                "Color", "UseGradient", "TopLeft", "TopRight", "BottomLeft", "BottomRight"))
                using (new EditorGUI.IndentLevelScope())
                {
                    SurfaceInteractionControls.Fields(style, "Color", "UseGradient");
                    if (style.FindPropertyRelative("UseGradient").boolValue)
                        SurfaceInteractionControls.Fields(style, "TopLeft", "TopRight", "BottomLeft", "BottomRight");
                }
        }

        /// <summary>Edits the four paragraph margins without exposing an unbounded vector control.</summary>
        /// <param name="margins">TMP margin vector in left, top, right, bottom order.</param>
        private static void Margins(SerializedProperty margins)
        {
            // Four explicit sliders retain the order used by TMP without exposing an unbounded vector field.
            Vector4 value = margins.vector4Value;
            EditorGUI.BeginChangeCheck();
            value.x = StudioFieldGUI.Slider(new GUIContent("Left Margin", margins.tooltip), value.x, 0f, 10f);
            value.y = StudioFieldGUI.Slider(new GUIContent("Top Margin", margins.tooltip), value.y, 0f, 10f);
            value.z = StudioFieldGUI.Slider(new GUIContent("Right Margin", margins.tooltip), value.z, 0f, 10f);
            value.w = StudioFieldGUI.Slider(new GUIContent("Bottom Margin", margins.tooltip), value.w, 0f, 10f);
            if (EditorGUI.EndChangeCheck())
                margins.vector4Value = value;
        }

        #endregion
        #endregion
    }
}
