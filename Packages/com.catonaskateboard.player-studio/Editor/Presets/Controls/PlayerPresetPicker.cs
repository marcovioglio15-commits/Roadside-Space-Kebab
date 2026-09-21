using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Adds explicit asset creation beside preset selectors while leaving slot changes in the existing draft.</summary>
    internal static class PlayerPresetPicker
    {
        #region Methods

        #region Controls

        /// <summary>Draws one asset selector with default creation and shallow asset duplication.</summary>
        /// <typeparam name="T">Preset type accepted by this slot.</typeparam>
        /// <param name="label">Slot name and contextual tooltip.</param>
        /// <param name="current">Currently proposed asset.</param>
        /// <returns>The selected or newly created asset; cancellation retains the current value.</returns>
        internal static T Draw<T>(GUIContent label, T current) where T : ScriptableObject
        {
            // Asset creation is explicit; selecting its result still uses the caller's Apply/Discard session.
            using (new EditorGUILayout.HorizontalScope())
            {
                current = (T)EditorGUILayout.ObjectField(label, current, typeof(T), false);
                if (GUILayout.Button(new GUIContent("New", "Create a preset with default values and propose it for this slot."), GUILayout.Width(42f)))
                    current = Create(current, false);
                using (new EditorGUI.DisabledScope(current == null))
                    if (GUILayout.Button(new GUIContent("Duplicate", "Copy the selected saved preset into a new asset. Referenced assets remain shared."), GUILayout.Width(70f)))
                        current = Create(current, true);
            }
            return current;
        }

        #endregion

        #region Assets

        /// <summary>Asks for an asset path without changing the current selection when cancelled.</summary>
        /// <typeparam name="T">Requested preset type.</typeparam>
        /// <param name="current">Source used only by Duplicate.</param>
        /// <param name="duplicate">Whether saved source values should replace class defaults.</param>
        /// <returns>The created asset or the unchanged current selection.</returns>
        private static T Create<T>(T current, bool duplicate) where T : ScriptableObject
        {
            // Unapplied module drafts are deliberately not confused with the selected saved asset.
            string path = EditorUtility.SaveFilePanelInProject(duplicate ? "Duplicate preset" : "New preset",
                duplicate ? current.name + " Copy" : typeof(T).Name, "asset", "Choose where to save the configuration.");
            if (string.IsNullOrEmpty(path))
                return current;
            T created = CreateAtPath<T>(path, duplicate ? current : null);
            GUI.changed = true;
            return created;
        }

        /// <summary>Creates an independent asset at a unique path, using type defaults or saved source values.</summary>
        /// <typeparam name="T">Requested preset type.</typeparam>
        /// <param name="path">Requested project asset path.</param>
        /// <param name="source">Optional asset to duplicate.</param>
        /// <returns>The newly saved preset.</returns>
        internal static T CreateAtPath<T>(string path, T source) where T : ScriptableObject
        {
            // Each module's field initializers define New; Duplicate retains its referenced assets.
            T created = source != null ? Object.Instantiate(source) : ScriptableObject.CreateInstance<T>();
            created.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(created, AssetDatabase.GenerateUniqueAssetPath(path));
            if (source == null && created is PlayerMasterPreset master)
            {
                // A new master needs a valid Body without sharing a mutable default with another player.
                PlayerBodyPreset body = ScriptableObject.CreateInstance<PlayerBodyPreset>();
                body.name = "Body";
                AssetDatabase.AddObjectToAsset(body, master);
                using SerializedObject data = new SerializedObject(master);
                data.FindProperty("bodyPreset").objectReferenceValue = body;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssetIfDirty(created);
            return created;
        }

        #endregion

        #endregion
    }
}
