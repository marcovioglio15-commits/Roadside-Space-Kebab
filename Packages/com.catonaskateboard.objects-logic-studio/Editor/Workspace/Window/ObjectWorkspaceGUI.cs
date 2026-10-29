using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares compact foldouts and left-aligned selection rows across workspace columns.</summary>
    internal static class ObjectWorkspaceGUI
    {
        #region Methods

        #region Controls

        /// <summary>Retains a panel's visibility without changing the object's pending configuration.</summary>
        /// <param name="state">Workspace retaining foldout preferences.</param>
        /// <param name="key">Unique panel or bucket identity.</param>
        /// <param name="title">Visible heading.</param>
        /// <param name="tooltip">Short explanation.</param>
        /// <returns>True when the panel is expanded.</returns>
        internal static bool Foldout(ObjectWorkspace state, string key, string title, string tooltip)
        {
            bool previous = !state.Navigation.Collapsed.Contains(key);
            bool changed = GUI.changed;
            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 4f);
            StudioFoldoutGUI.Background(rect, key.StartsWith("View.", System.StringComparison.Ordinal));
            bool open = EditorGUI.Foldout(rect, previous, new GUIContent(title, tooltip), true, EditorStyles.foldoutHeader);
            GUI.changed = changed;
            if (open != previous)
            {
                if (open)
                    state.Navigation.Collapsed.Remove(key);
                else
                    state.Navigation.Collapsed.Add(key);
                state.Persist();
            }
            return open;
        }

        /// <summary>Draws a single-height row with a selected background and a full-label tooltip.</summary>
        /// <param name="label">Row label.</param>
        /// <param name="selected">Whether this row is selected.</param>
        /// <param name="tooltip">Optional explanation.</param>
        /// <returns>True when the user clicks the row.</returns>
        internal static bool Row(string label, bool selected, string tooltip = null)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 3f);
            if (selected)
                EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.22f, 0.36f, 0.49f) : new Color(0.61f, 0.76f, 0.91f));
            return GUI.Button(rect, new GUIContent(label, tooltip ?? label), EditorStyles.label);
        }

        #endregion

        #endregion
    }
}
