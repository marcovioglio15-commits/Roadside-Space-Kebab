using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Assigns reusable entry lists to exact or threshold spawn occurrences.</summary>
    internal static class DialogueSpawnPresetControls
    {
        #region Methods
        #region Drawing
        /// <summary>Shows preset overrides only when spawn-specific content is enabled.</summary>
        /// <param name="settings">Detached interaction settings or a reusable preset.</param>
        internal static void Draw(SerializedProperty settings)
        {
            HoverControls.Field(settings, "UseSpawnPresets");
            if (!settings.FindPropertyRelative("UseSpawnPresets").boolValue)
                return;
            SerializedProperty rules = settings.FindPropertyRelative("SpawnPresets");
            // Explicit element operations keep existing references intact when rules are reordered.
            for (int index = 0; index < rules.arraySize; index++)
            {
                if (StudioArrayGUI.Header(rules, index, new GUIContent("Spawn Rule " + (index + 1), rules.tooltip)))
                    break;
                SerializedProperty rule = rules.GetArrayElementAtIndex(index);
                if (!rule.isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                HoverControls.Field(rule, "Match");
                HoverControls.Field(rule, "Spawn");
                SerializedProperty preset = rule.FindPropertyRelative("Preset");
                StudioFieldGUI.PropertyField(preset);
                using (new StudioButton.RowScope())
                {
                    using (new EditorGUI.DisabledScope(preset.objectReferenceValue == null))
                        if (StudioButton.Draw(new GUIContent("Edit Preset", "Select the shared Dialogue preset in the Inspector. Entries and Selection are read directly at spawn.")))
                            Selection.activeObject = preset.objectReferenceValue;
                    if (StudioButton.Draw(new GUIContent("New Preset", "Create a reusable Dialogue preset for this spawn rule.")))
                        Create(preset);
                }
            }
            StudioArrayGUI.Add(rules, "Add Spawn Preset", () => new DialogueSpawnPreset());
        }

        /// <summary>Creates a shared entry configuration without applying the current interaction draft.</summary>
        /// <param name="reference">Spawn rule receiving the new asset.</param>
        private static void Create(SerializedProperty reference)
        {
            string path = EditorUtility.SaveFilePanelInProject("New Dialogue Preset", "Dialogue Preset", "asset", "Choose the preset location.");
            if (string.IsNullOrEmpty(path))
                return;
            DialoguePreset preset = ScriptableObject.CreateInstance<DialoguePreset>();
            AssetDatabase.CreateAsset(preset, AssetDatabase.GenerateUniqueAssetPath(path));
            Undo.RegisterCreatedObjectUndo(preset, "Create Dialogue Preset");
            reference.objectReferenceValue = preset;
            Selection.activeObject = preset;
        }
        #endregion
        #endregion
    }
}
