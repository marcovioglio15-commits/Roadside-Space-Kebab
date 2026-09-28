using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares mesh and material hierarchy pickers across Slice, contact effects and assembly.</summary>
    internal static class ItemAppearanceControls
    {
        #region Methods

        #region Drawing

        /// <summary>Shows grouped optional appearance changes with one actual hierarchy source.</summary>
        /// <param name="settings">Serialized appearance configuration.</param>
        /// <param name="explicitSource">Whether a counterpart prefab must supply the hierarchy.</param>
        /// <param name="suggested">Existing ingredient preview prefab, when available.</param>
        internal static void Draw(SerializedProperty settings, bool explicitSource = false, GameObject suggested = null)
        {
            // Source selection appears only when these settings cannot use the current object's hierarchy.
            settings.isExpanded = EditorGUILayout.Foldout(settings.isExpanded, new GUIContent(settings.displayName, settings.tooltip), true);
            if (!settings.isExpanded)
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            GameObject source = HierarchyPathMenu.Source(settings, explicitSource, suggested);
            DrawList(settings.FindPropertyRelative("Meshes"), source, true);
            DrawList(settings.FindPropertyRelative("Materials"), source, false);
        }

        /// <summary>Edits replacement rows without allowing free-form hierarchy routes.</summary>
        /// <param name="array">Mesh or material replacement list.</param>
        /// <param name="source">Actual item hierarchy used by the target picker.</param>
        /// <param name="mesh">Whether rows contain meshes and optional collider updates.</param>
        internal static void DrawList(SerializedProperty array, GameObject source, bool mesh)
        {
            // Structural edits restart the next GUI pass before using invalidated array element handles.
            array.isExpanded = EditorGUILayout.Foldout(array.isExpanded, new GUIContent(array.displayName + " (" + array.arraySize + ")", array.tooltip), true);
            if (!array.isExpanded)
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            for (int index = 0; index < array.arraySize; index++)
            {
                SerializedProperty entry = array.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField((mesh ? "Mesh " : "Renderer ") + (index + 1), EditorStyles.boldLabel);
                    if (GUILayout.Button(new GUIContent("−", "Remove this replacement."), GUILayout.Width(26f)))
                    {
                        array.DeleteArrayElementAtIndex(index);
                        break;
                    }
                }
                HierarchyPathMenu.Draw(entry.FindPropertyRelative("Path"), source, mesh);
                HoverControls.Field(entry, mesh ? "Mesh" : "Materials");
                if (mesh)
                    HoverControls.Field(entry, "UpdateCollider");
            }
            if (!GUILayout.Button(new GUIContent(mesh ? "+ Add Mesh Replacement" : "+ Add Material Replacement", "Add one replacement on an existing hierarchy component.")))
                return;
            array.arraySize++;
            SerializedProperty added = array.GetArrayElementAtIndex(array.arraySize - 1);
            added.FindPropertyRelative("Path").stringValue = string.Empty;
            if (mesh)
            {
                added.FindPropertyRelative("Mesh").objectReferenceValue = null;
                added.FindPropertyRelative("UpdateCollider").boolValue = false;
            }
            else
                added.FindPropertyRelative("Materials").arraySize = 0;
        }

        #endregion

        #endregion
    }
}
