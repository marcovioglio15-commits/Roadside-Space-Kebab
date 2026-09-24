using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares the optional tag-change controls across every interaction category.</summary>
    internal static class InteractionTagControls
    {
        #region Methods

        #region Drawing

        /// <summary>Shows a project tag picker only when the interaction requests a tag change.</summary>
        /// <param name="settings">Serialized per-interaction tag change.</param>
        /// <param name="sections">Retained foldout visibility.</param>
        internal static void Draw(SerializedProperty settings, ObjectStudioSections sections)
        {
            // Tag changes stay with the item binding when reusable settings are imported.
            if (!sections.Draw("Tag Change", "Optionally assign a project tag when this interaction starts or completes."))
                return;
            using EditorGUI.IndentLevelScope sectionIndent = new EditorGUI.IndentLevelScope();
            HoverControls.Field(settings, "Enabled");
            if (!settings.FindPropertyRelative("Enabled").boolValue)
                return;
            SerializedProperty tag = settings.FindPropertyRelative("Tag");
            EditorGUI.BeginChangeCheck();
            string selected = EditorGUILayout.TagField(new GUIContent("Tag", tag.tooltip), tag.stringValue);
            if (EditorGUI.EndChangeCheck())
                tag.stringValue = selected;
            HoverControls.Field(settings, "Moment");
        }

        #endregion

        #endregion
    }
}
