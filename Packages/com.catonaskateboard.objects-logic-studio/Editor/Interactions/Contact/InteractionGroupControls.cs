using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Uses the same group assets as the interaction tool for contact additions and imports.</summary>
    internal static class InteractionGroupControls
    {
        #region Methods

        #region Drawing

        /// <summary>Edits an ordered group list and prepares runtime data only after an explicit edit.</summary>
        /// <param name="change">Contact operation inside its detached rule.</param>
        /// <param name="kind">Interaction family accepted by the group picker.</param>
        internal static void Draw(SerializedProperty change, InteractionRuntimeKind kind)
        {
            SerializedProperty groups = change.FindPropertyRelative("GroupPresets");
            EditorGUILayout.LabelField(new GUIContent("Settings Groups", groups.tooltip), EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            // Group assets stay shared; only their prepared values are imported during Play.
            for (int index = 0; index < groups.arraySize; index++)
                using (new EditorGUILayout.HorizontalScope())
                {
                    SerializedProperty field = groups.GetArrayElementAtIndex(index);
                    field.objectReferenceValue = StudioFieldGUI.ObjectField(new GUIContent("Group " + (index + 1), groups.tooltip),
                        field.objectReferenceValue, typeof(ObjectGroupPreset), false);
                    using (new EditorGUI.DisabledScope(field.objectReferenceValue == null))
                        if (GUILayout.Button(new GUIContent("Open", "Edit this same group in its dedicated window."), GUILayout.Width(48f)))
                            ObjectGroupWindow.OpenPreset((ObjectGroupPreset)field.objectReferenceValue);
                    if (GUILayout.Button(new GUIContent("−", "Remove this group from the operation."), GUILayout.Width(24f)))
                    {
                        field.objectReferenceValue = null;
                        groups.DeleteArrayElementAtIndex(index);
                        break;
                    }
                }
            if (StudioButton.Draw(new GUIContent("Add Settings Group", "Apply another group of the same interaction type after the preceding groups.")))
            {
                groups.arraySize++;
                groups.GetArrayElementAtIndex(groups.arraySize - 1).objectReferenceValue = null;
            }
            if (EditorGUI.EndChangeCheck() || NeedsCompilation(change, kind))
            {
                InteractionChangeSettings value = (InteractionChangeSettings)change.boxedValue;
                InteractionGroupCompiler.Compile(value);
                change.boxedValue = value;
            }
            string warning = change.FindPropertyRelative("GroupWarning").stringValue;
            if (string.IsNullOrEmpty(warning))
                return;
            Color previous = GUI.color;
            GUI.color = new Color(1f, 0.82f, 0.25f);
            EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
            GUI.color = previous;
        }

        /// <summary>Invalidates prepared values when the operation switches interaction families.</summary>
        /// <param name="change">Current serialized operation.</param>
        /// <param name="kind">Selected destination family.</param>
        /// <returns>True when an existing prepared group targets another family.</returns>
        private static bool NeedsCompilation(SerializedProperty change, InteractionRuntimeKind kind)
        {
            // Empty selections retain their warning until the group list changes.
            return change.FindPropertyRelative("CompiledKind").enumValueIndex != (int)kind;
        }

        #endregion

        #endregion
    }
}
