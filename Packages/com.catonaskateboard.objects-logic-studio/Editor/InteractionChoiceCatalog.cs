using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares named existing-interaction selection without storing names as identity.</summary>
    internal sealed class InteractionChoiceCatalog
    {
        #region State

        private long[] identities = Array.Empty<long>();
        private GUIContent[] labels = Array.Empty<GUIContent>();

        #endregion

        #region Methods

        #region Catalog

        /// <summary>Caches names and stable prefab IDs at hierarchy refresh boundaries.</summary>
        /// <param name="root">Prefab branch containing selectable interactions.</param>
        /// <param name="product">Exclude product configuration itself when selecting ingredient-gated features.</param>
        internal void Refresh(GameObject root, bool product)
        {
            // Duplicate names include a path, type and component index so every choice remains identifiable.
            List<ObjectInteraction> found = new List<ObjectInteraction>();
            if (root != null)
                foreach (ObjectInteraction interaction in root.GetComponentsInChildren<ObjectInteraction>(true))
                    if (interaction is not ObjectInteractionUnlock && (!product || interaction is not ObjectAssemblyProduct))
                        found.Add(interaction);
            identities = new long[found.Count];
            labels = new GUIContent[found.Count + 1];
            labels[0] = new GUIContent("Select an existing interaction", "Configure the interaction in its own category first.");
            for (int index = 0; index < found.Count; index++)
            {
                identities[index] = ObjectWorkspaceTarget.FileId(found[index]);
                string path = AnimationUtility.CalculateTransformPath(found[index].transform, root.transform);
                labels[index + 1] = new GUIContent(found[index].InteractionName + "  ("
                    + found[index].GetType().Name.Replace("Object", string.Empty) + ", "
                    + (path.Length > 0 ? path : "Root") + ", " + (index + 1) + ")", "Stable reference to this existing component.");
            }
        }

        /// <summary>Edits an existing component reference through its cached display name.</summary>
        /// <param name="property">Serialized stable component identity.</param>
        /// <param name="label">Role of the selected feature.</param>
        /// <param name="excluded">Identity forbidden as a self-referencing source, or zero.</param>
        internal void Draw(SerializedProperty property, string label, long excluded = 0)
        {
            // Missing references stay missing until a replacement is selected explicitly.
            int current = Array.IndexOf(identities, property.longValue) + 1;
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup(new GUIContent(label, property.tooltip), current, labels);
            if (EditorGUI.EndChangeCheck())
            {
                long identity = selected > 0 ? identities[selected - 1] : 0;
                if (identity != 0 && identity == excluded)
                    Debug.LogWarning("An interaction cannot unlock itself. Choose a different source.");
                else
                    property.longValue = identity;
            }
            if (current == 0 && property.longValue != 0)
                EditorGUILayout.LabelField("The selected interaction was removed. Choose an existing replacement.", EditorStyles.miniLabel);
        }

        #endregion

        #endregion
    }
}
