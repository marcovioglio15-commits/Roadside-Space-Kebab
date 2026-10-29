using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Finds exact saved references only when the user requests a usage check.</summary>
    internal sealed class ObjectPresetUsageWindow : EditorWindow
    {
        #region Fields

        [Header("References")]
        [Tooltip("Saved assets that refer to this preset.")]
        [SerializeField]
        private UnityEngine.Object[] references = Array.Empty<UnityEngine.Object>();
        [Tooltip("Scroll position in the reference list.")]
        [SerializeField]
        private Vector2 scroll;

        #endregion

        #region Methods

        #region Discovery

        /// <summary>Checks serialized object links without treating imported local copies as live preset references.</summary>
        /// <param name="preset">Reusable preset selected for an explicit usage check.</param>
        internal static void Open(UnityEngine.Object preset)
        {
            ObjectPresetUsageWindow window = CreateInstance<ObjectPresetUsageWindow>();
            List<UnityEngine.Object> found = new List<UnityEngine.Object>();
            foreach (string guid in AssetDatabase.FindAssets("t:ObjectMasterPreset t:ObjectInteractionPreset t:Prefab"))
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if (asset != preset && asset is ObjectMasterPreset or ObjectInteractionPreset or GameObject && Uses(asset, preset))
                        found.Add(asset);
            window.references = found.ToArray();
            window.titleContent = new GUIContent(preset.name + " · References");
            window.minSize = new Vector2(380f, 220f);
            window.Show();
        }

        /// <summary>Searches one saved preset or prefab for the exact referenced asset.</summary>
        /// <param name="asset">Saved configuration or prefab.</param>
        /// <param name="preset">Exact main asset or subasset.</param>
        /// <returns>True when a serialized field refers directly to this preset.</returns>
        private static bool Uses(UnityEngine.Object asset, UnityEngine.Object preset)
        {
            if (asset is GameObject prefab)
            {
                foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
                    if (component != null && Uses(component, preset))
                        return true;
                return false;
            }
            using SerializedObject data = new SerializedObject(asset);
            using SerializedProperty field = data.GetIterator();
            while (field.Next(true))
                if (field.propertyType == SerializedPropertyType.ObjectReference && field.objectReferenceValue == preset)
                    return true;
            return false;
        }

        #endregion

        #region Drawing

        /// <summary>Shows saved users of the selected asset with direct Project selection.</summary>
        private void OnGUI()
        {
            EditorGUILayout.LabelField(references.Length + " saved references", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Imported object values are local copies. Updating this preset affects future imports.", EditorStyles.wordWrappedMiniLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (UnityEngine.Object reference in references)
                if (reference != null && GUILayout.Button(new GUIContent(reference.name, "Select this asset in the Project window."), EditorStyles.miniButton))
                {
                    EditorGUIUtility.PingObject(reference);
                    Selection.activeObject = reference;
                }
            EditorGUILayout.EndScrollView();
        }

        #endregion

        #endregion
    }
}
