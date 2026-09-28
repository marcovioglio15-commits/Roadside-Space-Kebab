using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Creates a named custom flag from the same small form used by every selector.</summary>
    internal sealed class ObjectFlagCreateWindow : EditorWindow
    {
        #region State

        private string flagName = string.Empty;
        private string group = string.Empty;
        private string description = string.Empty;
        private string warning = string.Empty;
        private Action<ObjectFlag> created;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Opens a nonmodal creation form and retains an optional destination field callback.</summary>
        /// <param name="onCreated">Receives the newly saved flag.</param>
        /// <param name="suggested">Optional name copied from the selector search.</param>
        internal static void Open(Action<ObjectFlag> onCreated, string suggested = "")
        {
            // A separate form preserves the original selector's field and its current draft.
            ObjectFlagCreateWindow window = CreateInstance<ObjectFlagCreateWindow>();
            window.titleContent = new GUIContent("Create Object Flag");
            window.flagName = suggested;
            window.created = onCreated;
            window.minSize = new Vector2(360f, 190f);
            window.ShowUtility();
        }

        #endregion

        #region Drawing

        /// <summary>Validates the entered name before creating any persistent asset.</summary>
        private void OnGUI()
        {
            // Creation is explicit; closing the form leaves the destination unchanged.
            flagName = EditorGUILayout.TextField(new GUIContent("Name", "Unique name shown in every object flag selector."), flagName);
            group = EditorGUILayout.TextField(new GUIContent("Group", "Optional category used by selector filters."), group);
            description = EditorGUILayout.TextField(new GUIContent("Description", "Short tooltip explaining when to use this flag."), description);
            GUILayout.Space(8f);
            if (warning.Length > 0)
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(flagName)))
                if (GUILayout.Button(new GUIContent("Create Flag", "Save this definition and assign it to the field that opened this form.")))
                    try
                    {
                        ObjectFlag flag = ObjectFlagCatalog.Create(flagName, group, description);
                        created?.Invoke(flag);
                        Close();
                    }
                    catch (InvalidOperationException exception)
                    {
                        warning = exception.Message;
                    }
        }

        #endregion

        #endregion
    }
}
