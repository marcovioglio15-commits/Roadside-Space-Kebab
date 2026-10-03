using CatOnASkateboard.StudioInput.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows generated mappings without exposing hierarchy paths as manually edited text.</summary>
    internal static class UnlockPresetInspector
    {
        #region Methods

        #region Drawing

        /// <summary>Edits rule values while keeping captured component mappings visible and stable.</summary>
        /// <param name="rule">Serialized portable rule snapshot.</param>
        internal static void Draw(SerializedProperty rule)
        {
            // Structural mapping changes are authored from actual interactions in the workspace.
            HoverControls.Field(rule, "Name");
            HoverControls.Field(rule, "Enabled");
            SerializedProperty settings = rule.FindPropertyRelative("Settings");
            HoverControls.Field(settings, "Operation");
            InteractionAvailabilityChange operation = (InteractionAvailabilityChange)settings.FindPropertyRelative("Operation").enumValueIndex;
            if (operation == InteractionAvailabilityChange.Unlock)
                HoverControls.Field(settings, "InitiallyLocked");
            HoverControls.Field(settings, "Repeat");
            Mapping("Target", rule.FindPropertyRelative("Target"));
            if (operation == InteractionAvailabilityChange.Replace)
                Mapping("Replacement", rule.FindPropertyRelative("Replacement"));
            InteractionToolControls.Draw(rule.FindPropertyRelative("ToolRequirement"));
            SerializedProperty conditions = settings.FindPropertyRelative("Conditions");
            SerializedProperty sources = rule.FindPropertyRelative("Sources");
            if (conditions.arraySize > 1)
                UnlockInteractionControls.ConditionLogic(settings.FindPropertyRelative("RequireAll"));
            for (int index = 0; index < conditions.arraySize; index++)
            {
                SerializedProperty condition = conditions.GetArrayElementAtIndex(index);
                HoverControls.Field(condition, "Count");
                if ((UnlockTrigger)condition.FindPropertyRelative("Trigger").enumValueIndex == UnlockTrigger.Interaction)
                {
                    if (index < sources.arraySize)
                        Mapping("Condition " + (index + 1), sources.GetArrayElementAtIndex(index));
                    HoverControls.Field(condition, "Moment");
                    if (index < sources.arraySize && sources.GetArrayElementAtIndex(index).FindPropertyRelative("Kind").enumValueIndex
                        == (int)InteractionTemplateKind.Contact)
                        UnlockInteractionControls.Consumption(condition);
                }
                else
                {
                    StudioInputActionMenu.Draw(rule.serializedObject, condition.FindPropertyRelative("Action").propertyPath,
                        "ObjectsLogicStudio.RulePreset." + index, ExtendedInteractionControls.Button);
                    HoverControls.Field(condition, "Distance");
                }
            }
            EditorGUILayout.LabelField("Import in Objects Logic Studio to change interaction mappings.", EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>Displays a captured interaction descriptor as a noneditable label.</summary>
        /// <param name="label">Role of this reference.</param>
        /// <param name="reference">Generated portable component descriptor.</param>
        private static void Mapping(string label, SerializedProperty reference)
        {
            // A readable route helps diagnose import mismatches without inviting unsupported path edits.
            string path = reference.FindPropertyRelative("Path").stringValue;
            EditorGUILayout.LabelField(new GUIContent(label, "Automatically mapped to a compatible existing destination interaction during Import."),
                reference.FindPropertyRelative("Assigned").boolValue
                    ? (path.Length == 0 ? "Root" : path) + " / " + reference.FindPropertyRelative("Name").stringValue : "Unassigned");
        }

        #endregion

        #endregion
    }

    /// <summary>Uses portable rule controls for the dedicated single-rule preset.</summary>
    [CustomEditor(typeof(UnlockRulePreset))]
    internal sealed class UnlockRulePresetEditor : UnityEditor.Editor
    {
        #region Methods

        #region Drawing

        /// <summary>Edits rule data and synchronizes the inherited tool requirement.</summary>
        public override void OnInspectorGUI()
        {
            using ObjectStudioFieldLayout layout = new ObjectStudioFieldLayout(205f);
            // Imported object snapshots remain independent from direct asset edits.
            serializedObject.Update();
            UnlockPresetInspector.Draw(serializedObject.FindProperty("Rule"));
            if (serializedObject.ApplyModifiedProperties())
            {
                UnlockRulePreset preset = (UnlockRulePreset)target;
                preset.ToolRequirement = ObjectWorkspace.Copy(preset.Rule.ToolRequirement);
                EditorUtility.SetDirty(preset);
            }
        }

        #endregion

        #endregion
    }

    /// <summary>Shows every captured rule in a complete availability preset.</summary>
    [CustomEditor(typeof(UnlockRuleSetPreset))]
    internal sealed class UnlockRuleSetPresetEditor : UnityEditor.Editor
    {
        #region Methods

        #region Drawing

        /// <summary>Edits existing rule values without replacing their generated mappings.</summary>
        public override void OnInspectorGUI()
        {
            // Set composition is captured from the selected object's actual availability rules.
            serializedObject.Update();
            SerializedProperty rules = serializedObject.FindProperty("Rules");
            for (int index = 0; index < rules.arraySize; index++)
            {
                SerializedProperty rule = rules.GetArrayElementAtIndex(index);
                rule.isExpanded = EditorGUILayout.Foldout(rule.isExpanded, rule.FindPropertyRelative("Name").stringValue, true);
                if (rule.isExpanded)
                    using (new EditorGUI.IndentLevelScope())
                        UnlockPresetInspector.Draw(rule);
            }
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #endregion
    }
}
