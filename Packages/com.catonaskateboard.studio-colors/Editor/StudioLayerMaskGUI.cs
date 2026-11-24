using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Commits delayed layer selections through the same Undo and draft notifications as other Studio menus.</summary>
    internal static class StudioLayerMaskGUI
    {
        #region Methods
        #region Drawing

        /// <summary>Draws an atomic layer selector without retaining a serialized property across GUI passes.</summary>
        /// <param name="property">Layer mask stored in the active draft.</param>
        /// <param name="label">Field caption and tooltip.</param>
        internal static void Draw(SerializedProperty property, GUIContent label)
        {
            // Menus outlive OnGUI; capture the owner and selection guard instead of the property iterator.
            using StudioFieldLayout layout = new StudioFieldLayout(label);
            Rect row = layout.GetRect(EditorGUIUtility.singleLineHeight);
            using StudioFieldColors colors = new StudioFieldColors(row, StudioFieldColors.Key(property));
            StudioFieldMenu.Context(row, property);
            if (!EditorGUI.DropdownButton(EditorGUI.PrefixLabel(layout.Control(row), layout.InlineLabel),
                new GUIContent(Caption(property.intValue), label.tooltip), FocusType.Keyboard))
                return;
            property.serializedObject.ApplyModifiedProperties();
            UnityEngine.Object owner = property.serializedObject.targetObject;
            string path = property.propertyPath;
            Func<bool> guard = StudioFieldMenu.Guard(owner, path);
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Nothing"), property.intValue == 0, () => Write(owner, path, 0, false, guard));
            menu.AddItem(new GUIContent("Everything"), property.intValue == -1, () => Write(owner, path, -1, false, guard));
            menu.AddSeparator(string.Empty);
            for (int index = 0; index < 32; index++)
            {
                string name = LayerMask.LayerToName(index);
                if (name.Length == 0)
                    continue;
                int bit = 1 << index;
                menu.AddItem(new GUIContent(name), (property.intValue & bit) != 0, () => Write(owner, path, bit, true, guard));
            }
            menu.DropDown(row);
        }

        /// <summary>Updates the current mask after a menu action and preserves unrelated pending edits.</summary>
        /// <param name="owner">Draft that opened the menu.</param>
        /// <param name="path">Original serialized field path.</param>
        /// <param name="value">Complete selection or one bit to toggle.</param>
        /// <param name="toggle">Whether only one layer changes.</param>
        /// <param name="guard">Checks that the draft still represents the original interaction.</param>
        private static void Write(UnityEngine.Object owner, string path, int value, bool toggle, Func<bool> guard)
        {
            // A different card or destroyed window must not receive an old dropdown selection.
            if (!guard())
                return;
            using SerializedObject data = new SerializedObject(owner);
            SerializedProperty property = data.FindProperty(path);
            if (property == null || property.propertyType != SerializedPropertyType.LayerMask)
                return;
            property.intValue = toggle ? property.intValue ^ value : value;
            if (data.ApplyModifiedProperties())
                StudioFieldMenu.Notify(owner);
        }

        /// <summary>Summarizes the saved mask without changing unnamed layer bits.</summary>
        /// <param name="mask">Full 32-bit selection.</param>
        /// <returns>Compact dropdown caption.</returns>
        private static string Caption(int mask)
        {
            if (mask == 0)
                return "Nothing";
            if (mask == -1)
                return "Everything";
            if ((mask & (mask - 1)) != 0)
                return "Mixed";
            for (int index = 0; index < 32; index++)
                if ((mask & (1 << index)) != 0)
                    return LayerMask.LayerToName(index) is string name && name.Length > 0 ? name : "Layer " + index;
            return "Mixed";
        }

        #endregion
        #endregion
    }
}
