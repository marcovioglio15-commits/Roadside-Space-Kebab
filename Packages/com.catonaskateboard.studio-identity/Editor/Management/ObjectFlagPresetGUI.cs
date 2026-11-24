using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Shares preset selection and serialized filter controls across the three tabs.</summary>
    internal static class ObjectFlagPresetGUI
    {
        #region Methods
        #region Presets

        /// <summary>Restores the last selected preset or loads an existing preset for this tab.</summary>
        /// <typeparam name="T">Dedicated tab configuration type.</typeparam>
        /// <param name="preset">Current tab configuration.</param>
        internal static void Draw<T>(ref T preset) where T : ScriptableObject
        {
            // Opening the tool never creates assets implicitly.
            if (preset == null)
            {
                string saved = EditorPrefs.GetString("StudioIdentity." + typeof(T).Name, string.Empty);
                preset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(saved));
                string[] found = preset == null ? AssetDatabase.FindAssets("t:" + typeof(T).Name) : System.Array.Empty<string>();
                if (found.Length > 0)
                    preset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(found[0]));
            }
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
            T chosen = (T)EditorGUILayout.ObjectField(new GUIContent("Tab Preset", "Settings saved independently for this tab."), preset, typeof(T), false);
            if (chosen != preset)
            {
                preset = chosen;
                EditorPrefs.SetString("StudioIdentity." + typeof(T).Name, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(preset)));
            }
            if (GUILayout.Button(new GUIContent("New", "Create another preset for this tab."), GUILayout.Width(48f)))
            {
                string path = EditorUtility.SaveFilePanelInProject("Create Tab Preset", typeof(T).Name, "asset", "Choose the preset location.");
                if (path.Length == 0)
                    return;
                preset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(preset, path);
                AssetDatabase.SaveAssetIfDirty(preset);
                EditorPrefs.SetString("StudioIdentity." + typeof(T).Name, AssetDatabase.AssetPathToGUID(path));
            }
        }

        /// <summary>Draws configuration fields with their stored tooltips and Undo.</summary>
        /// <param name="preset">Configuration receiving explicit edits.</param>
        /// <param name="fields">Serialized fields appropriate to this tab.</param>
        /// <returns>True when the configuration changed.</returns>
        internal static bool Fields(ScriptableObject preset, params string[] fields)
        {
            using SerializedObject data = new SerializedObject(preset);
            foreach (string field in fields)
                EditorGUILayout.PropertyField(data.FindProperty(field));
            if (!data.ApplyModifiedProperties())
                return false;
            return true;
        }

        #endregion
        #endregion
    }
}
