using CatOnASkateboard.StudioColors.Editor;
using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Provides project-wide flag discovery, creation and explicit Unity Tag catalog import.</summary>
    internal sealed class ObjectFlagCatalogWindow : EditorWindow
    {
        #region State

        private string search = string.Empty;
        private Vector2 scroll;
        private string status = string.Empty;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Opens catalog management from a flag selector without changing its selection.</summary>
        internal static void Open()
        {
            // Project flags remain available even when no prefab or interaction is selected.
            GetWindow<ObjectFlagCatalogWindow>("Object Flags").Show();
        }

        #endregion

        #region Drawing

        /// <summary>Lists existing definitions and creates only explicitly requested assets.</summary>
        private void OnGUI()
        {
            // Search spans names and groups while asset identity remains unchanged.
            search = EditorGUILayout.TextField(new GUIContent("Filter", "Search flag names and groups."), search);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (StudioButton.Draw(new GUIContent("+ Create Flag", "Add a new named flag to the shared project catalog.")))
                    ObjectFlagCreateWindow.Open(flag => Selection.activeObject = flag, search);
                if (StudioButton.Draw(new GUIContent("Import Unity Tags", "Create a flag for every project tag that has no same-named definition. Repeating this action creates no duplicates.")))
                    status = ObjectFlagCatalog.ImportProjectTags() + " flags created.";
            }
            if (status.Length > 0)
                EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (ObjectFlag flag in ObjectFlagCatalog.Flags)
                if (search.Length == 0 || (flag.DisplayName?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                    || (flag.Group?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(new GUIContent(flag.DisplayName, flag.Description));
                        EditorGUILayout.LabelField(flag.Group, GUILayout.Width(130f));
                        if (GUILayout.Button(new GUIContent("Edit", "Open this flag to change its name, group or description."), GUILayout.Width(48f)))
                            ObjectFlagEditWindow.Open(flag);
                        if (GUILayout.Button(new GUIContent("Delete", "Delete this definition. Existing references must be reassigned."), GUILayout.Width(52f)))
                        {
                            if (EditorUtility.DisplayDialog("Delete Object Flag", "Delete '" + flag.DisplayName
                                + "'? Objects and presets using it will need their references updated.", "Delete", "Cancel"))
                            {
                                status = AssetDatabase.MoveAssetToTrash(AssetDatabase.GetAssetPath(flag))
                                    ? "Flag deleted." : "The flag could not be deleted. Check asset write access.";
                                ObjectFlagCatalog.Invalidate();
                            }
                            GUIUtility.ExitGUI();
                        }
                    }
            EditorGUILayout.EndScrollView();
        }

        #endregion

        #endregion
    }
}
