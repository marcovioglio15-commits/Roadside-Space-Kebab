using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows composition and explicit edit commands without exposing serialized preset payloads.</summary>
    internal abstract class ObjectPresetInspector : UnityEditor.Editor
    {
        #region Methods

        #region Inspector

        /// <summary>Draws clear preset references and opens the corresponding focused editor.</summary>
        public override void OnInspectorGUI()
        {
            using EditorGUI.DisabledScope locked = new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode);
            serializedObject.Update();
            SerializedProperty description = serializedObject.FindProperty("Description");
            if (description != null)
                EditorGUILayout.PropertyField(description, new GUIContent("Description", "A short note about this preset."));
            switch (target)
            {
                case ObjectGroupPreset group:
                    EditorGUILayout.LabelField(group.Group?.Title ?? "Settings group", EditorStyles.boldLabel);
                    if (GUILayout.Button(new GUIContent("Edit group preset", "Open just this settings group.")))
                        ObjectGroupWindow.OpenPreset(group);
                    break;
                case ObjectInteractionPreset interaction:
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("SourcePreset"), new GUIContent("Main preset", "Choose the original interaction preset, if needed."));
                    for (int index = 0; index < interaction.Groups.Length; index++)
                        Group(interaction, index);
                    break;
                case ObjectMasterPreset master:
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("SourcePrefab"), new GUIContent("Source prefab", "Original object. Include it when exporting, if needed."));
                    for (int index = 0; index < master.Shared.Length; index++)
                    {
                        ObjectMasterPreset.SharedEntry shared = master.Shared[index];
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("Shared").GetArrayElementAtIndex(index).FindPropertyRelative("Preset"),
                            new GUIContent(string.IsNullOrEmpty(shared.Path) ? "Root shared settings" : shared.Path, "Choose the flags and shared settings for this object."));
                        if (shared.Preset != null && GUILayout.Button(new GUIContent("Edit shared settings", "Edit these reusable flags and switches.")))
                            ObjectSharedPresetWindow.Open(shared.Preset);
                    }
                    for (int entryIndex = 0; entryIndex < master.Interactions.Length; entryIndex++)
                    {
                        ObjectMasterPreset.InteractionEntry entry = master.Interactions[entryIndex];
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("Interactions").GetArrayElementAtIndex(entryIndex).FindPropertyRelative("Preset"),
                            new GUIContent(entry.Name, "Choose the main preset for this interaction."));
                        if (entry.Preset != null)
                        {
                            if (GUILayout.Button(new GUIContent("Make local copy", "Save a separate main preset and use it in this master.")))
                            {
                                ObjectInteractionPreset copy = ObjectPresetCopy.Save(entry.Preset);
                                if (copy != null)
                                {
                                    Undo.RecordObject(master, "Copy main preset");
                                    entry.Preset = copy;
                                    EditorUtility.SetDirty(master);
                                    AssetDatabase.SaveAssetIfDirty(master);
                                    serializedObject.Update();
                                }
                            }
                            for (int index = 0; index < entry.Preset.Groups.Length; index++)
                                Group(entry.Preset, index);
                        }
                    }
                    if (GUILayout.Button(new GUIContent("Export configuration", "Review the assets before saving a Unity package.")))
                        ObjectMasterExportWindow.Open(master);
                    break;
                case ObjectSharedPreset shared:
                    EditorGUILayout.LabelField(shared.Components.Length + " shared components", EditorStyles.miniLabel);
                    if (GUILayout.Button(new GUIContent("Edit shared settings", "Edit these reusable flags and switches.")))
                        ObjectSharedPresetWindow.Open(shared);
                    break;
            }
            if (GUILayout.Button(new GUIContent("Find references", "See which saved assets use this preset.")))
                ObjectPresetUsageWindow.Open(target);
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Shows one granular preset reference and its independent edit command.</summary>
        /// <param name="owner">Main preset containing the group references.</param>
        /// <param name="index">Group reference to edit.</param>
        private static void Group(ObjectInteractionPreset owner, int index)
        {
            ObjectGroupPreset preset = owner.Groups[index];
            using SerializedObject data = new SerializedObject(owner);
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
            EditorGUILayout.PropertyField(data.FindProperty("Groups").GetArrayElementAtIndex(index), new GUIContent(preset?.Group?.Title ?? "Missing group", "Choose a reusable preset for this group."));
            data.ApplyModifiedProperties();
            using (new EditorGUI.DisabledScope(preset == null))
            {
                if (GUILayout.Button(new GUIContent("Edit", "Edit this group preset."), GUILayout.Width(45f)))
                    ObjectGroupWindow.OpenPreset(preset);
                if (GUILayout.Button(new GUIContent("Copy", "Save a separate group preset and use it here."), GUILayout.Width(45f)))
                {
                    ObjectGroupPreset copy = ObjectPresetCopy.Save(preset);
                    if (copy != null)
                    {
                        Undo.RecordObject(owner, "Copy group preset");
                        owner.Groups[index] = copy;
                        EditorUtility.SetDirty(owner);
                        AssetDatabase.SaveAssetIfDirty(owner);
                    }
                }
            }
        }

        #endregion

        #endregion
    }

    /// <summary>Connects object master assets to their composition inspector.</summary>
    [CustomEditor(typeof(ObjectMasterPreset))]
    internal sealed class ObjectMasterPresetEditor : ObjectPresetInspector { }

    /// <summary>Connects main interaction assets to their group-reference inspector.</summary>
    [CustomEditor(typeof(ObjectInteractionPreset))]
    internal sealed class ObjectInteractionPresetEditor : ObjectPresetInspector { }

    /// <summary>Connects granular group assets to their focused editor.</summary>
    [CustomEditor(typeof(ObjectGroupPreset))]
    internal sealed class ObjectGroupPresetEditor : ObjectPresetInspector { }

    /// <summary>Connects shared settings assets to their master import instructions.</summary>
    [CustomEditor(typeof(ObjectSharedPreset))]
    internal sealed class ObjectSharedPresetEditor : ObjectPresetInspector { }
}
