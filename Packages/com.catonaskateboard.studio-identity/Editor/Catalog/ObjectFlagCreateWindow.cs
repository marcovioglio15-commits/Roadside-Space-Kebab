using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
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
        private ObjectFlagDraftSession session;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Opens a nonmodal creation form and retains an optional destination field callback.</summary>
        /// <param name="onCreated">Receives the newly saved flag.</param>
        /// <param name="suggested">Optional name copied from the selector search.</param>
        /// <param name="suggestedGroup">Group selected in catalog management.</param>
        /// <param name="draft">Optional management proposal receiving the new flag.</param>
        internal static void Open(Action<ObjectFlag> onCreated, string suggested = "", string suggestedGroup = "", ObjectFlagDraftSession draft = null)
        {
            // A separate form preserves the original selector's field and its current draft.
            ObjectFlagCreateWindow window = CreateInstance<ObjectFlagCreateWindow>();
            window.titleContent = new GUIContent("Create Object Flag");
            window.flagName = suggested;
            window.group = suggestedGroup;
            window.session = draft;
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
            flagName = StudioGUI.TextField(new GUIContent("Name", "Unique name shown in every object flag selector."), flagName);
            group = StudioGUI.TextField(new GUIContent("Group", "Optional category used by selector filters."), group);
            description = StudioGUI.TextField(new GUIContent("Description", "Short tooltip explaining when to use this flag."), description);
            GUILayout.Space(8f);
            if (warning.Length > 0)
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(flagName)))
                if (StudioButton.Draw(new GUIContent("Create Flag", session != null
                    ? "Add this definition to the main draft. Apply saves its asset."
                    : "Save this definition and assign it to the field that opened this form.")))
                    try
                    {
                        ObjectFlag flag = session != null ? session.Create(flagName, group, description) : ObjectFlagCatalog.Create(flagName, group, description);
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
