using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits entry-local named colors and copies reusable palette presets without retaining shared values.</summary>
    internal static class DialoguePaletteControls
    {
        #region Methods

        #region Drawing

        /// <summary>Shows color rows with optional preset import and export controls.</summary>
        /// <param name="colors">Palette inside the current detached entry.</param>
        /// <param name="preset">Editor-only preset picker, independent of the entry's runtime data.</param>
        internal static void Draw(SerializedProperty colors, ref DialoguePalettePreset preset)
        {
            colors.isExpanded = EditorGUILayout.Foldout(colors.isExpanded, new GUIContent("Text Colors", colors.tooltip), true);
            if (!colors.isExpanded)
                return;
            using StudioIndentScope indent = new StudioIndentScope();
            Rows(colors);
            preset = (DialoguePalettePreset)EditorGUILayout.ObjectField(new GUIContent("Palette Preset", "Optional source for replacing this entry's colors."),
                preset, typeof(DialoguePalettePreset), false);
            using StudioButton.RowScope buttons = new StudioButton.RowScope();
            using (new EditorGUI.DisabledScope(preset == null))
            {
                if (StudioButton.Draw(new GUIContent("Import Palette", "Copy the preset's colors into this entry. Later preset edits do not change the entry.")))
                    Write(colors, preset.Colors);
                if (StudioButton.Draw(new GUIContent("Open Palette", "Edit the reusable palette in its own Apply/Discard window.")))
                    DialoguePaletteWindow.Open(preset);
            }
            if (StudioButton.Draw(new GUIContent("Save Palette", "Save these named colors as a separate reusable palette preset.")))
                Save(colors);
        }

        /// <summary>Edits the same compact list in entries and palette preset drafts.</summary>
        /// <param name="colors">Named color list receiving explicit edits.</param>
        internal static void Rows(SerializedProperty colors)
        {
            for (int index = 0; index < colors.arraySize; index++)
            {
                SerializedProperty color = colors.GetArrayElementAtIndex(index);
                string name = color.FindPropertyRelative("Name").stringValue;
                if (StudioArrayGUI.Header(colors, index, new GUIContent(string.IsNullOrEmpty(name) ? "New Color" : name, colors.tooltip)))
                    break;
                if (!color.isExpanded)
                    continue;
                using StudioIndentScope row = new StudioIndentScope();
                HoverControls.Field(color, "Name");
                HoverControls.Field(color, "Color");
            }
            StudioArrayGUI.Add(colors, "Add Color", () => new DialogueNamedColor { Name = NextName(colors), Color = Color.white });
        }

        #endregion

        #region Copy

        /// <summary>Copies value types into the serialized draft so Undo and asset isolation remain intact.</summary>
        /// <param name="colors">Destination draft palette.</param>
        /// <param name="values">Preset values copied by value.</param>
        internal static void Write(SerializedProperty colors, DialogueNamedColor[] values)
        {
            colors.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                SerializedProperty color = colors.GetArrayElementAtIndex(index);
                color.FindPropertyRelative("Name").stringValue = values[index].Name;
                color.FindPropertyRelative("Color").colorValue = values[index].Color;
            }
        }

        /// <summary>Captures a palette without sharing a mutable array with the entry draft.</summary>
        /// <param name="colors">Current serialized palette.</param>
        /// <returns>Independent named colors.</returns>
        internal static DialogueNamedColor[] Read(SerializedProperty colors)
        {
            DialogueNamedColor[] values = new DialogueNamedColor[colors.arraySize];
            for (int index = 0; index < values.Length; index++)
            {
                SerializedProperty color = colors.GetArrayElementAtIndex(index);
                values[index] = new DialogueNamedColor { Name = color.FindPropertyRelative("Name").stringValue,
                    Color = color.FindPropertyRelative("Color").colorValue };
            }
            return values;
        }

        /// <summary>Provides a free initial token name without changing names already entered by the user.</summary>
        /// <param name="colors">Existing palette rows.</param>
        /// <returns>An unused Color name with a numeric suffix.</returns>
        private static string NextName(SerializedProperty colors)
        {
            DialogueNamedColor[] values = Read(colors);
            int index = 1;
            while (Array.Exists(values, value => string.Equals(value.Name, "Color" + index, StringComparison.OrdinalIgnoreCase)))
                index++;
            return "Color" + index;
        }

        /// <summary>Exports only valid named colors and leaves the entry's own draft unchanged.</summary>
        /// <param name="colors">Current palette.</param>
        private static void Save(SerializedProperty colors)
        {
            DialogueNamedColor[] values = Read(colors);
            if (!DialogueNamedColors.TryValidate(values, out string warning))
            {
                Debug.LogWarning(warning, colors.serializedObject.targetObject);
                return;
            }
            string path = EditorUtility.SaveFilePanelInProject("Save Color Palette", "SO_DialogueColors", "asset", "Save these named text colors.");
            if (string.IsNullOrEmpty(path))
                return;
            DialoguePalettePreset preset = ScriptableObject.CreateInstance<DialoguePalettePreset>();
            preset.Colors = values;
            AssetDatabase.CreateAsset(preset, AssetDatabase.GenerateUniqueAssetPath(path));
            Undo.RegisterCreatedObjectUndo(preset, "Save dialogue color palette");
        }

        #endregion

        #endregion
    }
}
