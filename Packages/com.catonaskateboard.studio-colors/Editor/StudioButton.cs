using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Draws compact commands aligned to the current serialized-field indentation.</summary>
    public static class StudioButton
    {
        #region Layout

        /// <summary>Shares one indentation across adjacent command buttons in a horizontal row.</summary>
        public sealed class RowScope : IDisposable
        {
            private readonly EditorGUILayout.HorizontalScope row;
            private readonly EditorGUI.IndentLevelScope indent;

            #region Methods

            /// <summary>Reserves the row's indentation before drawing unindented adjacent commands.</summary>
            public RowScope()
            {
                // Indent the group once rather than adding a gap before every button.
                row = new EditorGUILayout.HorizontalScope();
                GUILayout.Space(EditorGUI.IndentedRect(new Rect()).x);
                indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
            }

            /// <summary>Restores the caller's indentation and closes the command row.</summary>
            public void Dispose()
            {
                // Restore GUI state even if a delayed menu or ExitGUI leaves the calling panel.
                indent.Dispose();
                row.Dispose();
            }

            #endregion
        }

        #endregion

        #region Methods

        #region Drawing

        /// <summary>Reserves the label's natural width and applies the current field indentation.</summary>
        /// <param name="label">Command text and tooltip.</param>
        /// <param name="style">Optional native button style.</param>
        /// <param name="expandWidth">Fill the available row width for persistent footer actions.</param>
        /// <param name="minimumHeight">Optional minimum height for prominent actions.</param>
        /// <returns>True when the command was clicked.</returns>
        public static bool Draw(GUIContent label, GUIStyle style = null, bool expandWidth = false, float minimumHeight = 0f)
        {
            // GUILayout buttons ignore indentLevel; reserve that space explicitly before drawing the hit area.
            style ??= GUI.skin.button;
            Vector2 size = style.CalcSize(label);
            Rect rect = GUILayoutUtility.GetRect(size.x + EditorGUI.IndentedRect(new Rect()).x,
                Mathf.Max(Mathf.Max(EditorGUIUtility.singleLineHeight, size.y), minimumHeight), GUILayout.ExpandWidth(expandWidth));
            return GUI.Button(EditorGUI.IndentedRect(rect), label, style);
        }

        #endregion

        #endregion
    }
}
