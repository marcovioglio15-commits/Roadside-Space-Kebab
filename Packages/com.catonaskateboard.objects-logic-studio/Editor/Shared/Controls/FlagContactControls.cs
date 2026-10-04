using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits direct-contact requirements using the project's object flag catalog.</summary>
    internal static class FlagContactControls
    {
        #region Methods

        #region Drawing

        /// <summary>Shows contact geometry and multiple flag choices only when the requirement is active.</summary>
        /// <param name="settings">Serialized flagged-contact requirement.</param>
        internal static void Draw(SerializedProperty settings)
        {
            // Disabled requirements retain their previous values for later reuse.
            SerializedProperty enabled = settings.FindPropertyRelative("Enabled");
            enabled.boolValue = StudioGUI.Toggle(StudioFieldMenu.Value(enabled, new GUIContent("Require Contact", enabled.tooltip)), enabled.boolValue);
            if (!settings.FindPropertyRelative("Enabled").boolValue)
                return;
            SerializedProperty flags = settings.FindPropertyRelative("Flags");
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            ObjectFlagSelector.Draw(flags, new GUIContent("Contact Flags", flags.tooltip));
            if (flags.arraySize > 1)
                HoverControls.Field(settings, "Match");
            HoverControls.Field(settings, "Tolerance");
            HoverControls.Field(settings, "IncludeTriggers");
        }

        #endregion

        #endregion
    }
}
