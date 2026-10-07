using CatOnASkateboard.StudioIdentity.Editor;
using CatOnASkateboard.StudioIdentity;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares the optional flag-change controls across every interaction category.</summary>
    internal static class InteractionFlagControls
    {
        #region Methods

        #region Drawing

        /// <summary>Shows a project flag picker only when the interaction requests a flag change.</summary>
        /// <param name="settings">Serialized per-interaction flag change.</param>
        /// <param name="sections">Retained foldout visibility.</param>
        internal static void Draw(SerializedProperty settings, ObjectStudioSections sections)
        {
            // Flag changes stay with the item binding when reusable settings are imported.
            if (!sections.Draw("Identity Change", "Optionally change object flags when this interaction starts or completes.", settings))
                return;
            using EditorGUI.IndentLevelScope sectionIndent = new EditorGUI.IndentLevelScope();
            HoverControls.Field(settings, "Enabled");
            if (!settings.FindPropertyRelative("Enabled").boolValue)
                return;
            HoverControls.Field(settings, "Operation");
            if (settings.FindPropertyRelative("Operation").enumValueIndex != (int)ObjectFlagOperation.Clear)
                ObjectFlagSelector.Draw(settings.FindPropertyRelative("Flag"));
            HoverControls.Field(settings, "Moment");
        }

        #endregion

        #endregion
    }
}
