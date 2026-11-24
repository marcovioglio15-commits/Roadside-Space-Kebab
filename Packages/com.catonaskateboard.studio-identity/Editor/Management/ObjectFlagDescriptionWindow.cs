using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Shows current descriptions for every selected flag and stages individual or shared text.</summary>
    internal sealed class ObjectFlagDescriptionWindow : EditorWindow
    {
        #region Fields

        [Header("Selection")]
        [Tooltip("Stable flag selection captured when this editor opened.")]
        [SerializeField]
        private ObjectFlag[] flags = Array.Empty<ObjectFlag>();
        [Tooltip("Shared management proposal receiving description edits.")]
        [SerializeField]
        private ObjectFlagDraftSession session;

        #endregion
        #region State

        private string bulkText = string.Empty;
        private bool mixed;
        private bool bulkEdited;
        private Vector2 scroll;

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Opens all current descriptions and initializes shared text only when the values agree.</summary>
        /// <param name="targets">Flags selected in the groups tab.</param>
        /// <param name="draft">Shared management proposal.</param>
        internal static void Open(ObjectFlag[] targets, ObjectFlagDraftSession draft)
        {
            ObjectFlagDescriptionWindow window = GetWindow<ObjectFlagDescriptionWindow>("Flag Descriptions");
            if (window.session != null)
                window.session.Changed -= window.Repaint;
            window.flags = targets.Where(flag => flag != null).Distinct().ToArray();
            window.session = draft;
            window.session.Changed += window.Repaint;
            string[] descriptions = window.flags.Select(flag => draft.Definition(flag).Description ?? string.Empty).Distinct().ToArray();
            window.mixed = descriptions.Length > 1;
            window.bulkText = descriptions.Length == 1 ? descriptions[0] : string.Empty;
            window.bulkEdited = false;
            window.minSize = new Vector2(460f, 340f);
            window.Show();
            window.Focus();
        }

        /// <summary>Refreshes descriptions after native Undo restores the working copies.</summary>
        private void OnEnable()
        {
            Undo.undoRedoPerformed += Repaint;
        }

        /// <summary>Releases shared proposal and Undo listeners when this editor closes.</summary>
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
            if (session != null)
                session.Changed -= Repaint;
        }

        #endregion
        #region Drawing

        /// <summary>Draws separate current values plus an explicit common-description command.</summary>
        private void OnGUI()
        {
            if (session == null || session.Released)
                return;
            EditorGUILayout.LabelField(flags.Length + " selected flags", EditorStyles.boldLabel);
            if (flags.Length > 1)
            {
                EditorGUILayout.LabelField(new GUIContent("Description for All", "Set one description on every selected flag."));
                EditorGUI.showMixedValue = mixed && !bulkEdited;
                EditorGUI.BeginChangeCheck();
                bulkText = EditorGUILayout.TextArea(bulkText, GUILayout.MinHeight(55f));
                if (EditorGUI.EndChangeCheck())
                    bulkEdited = true;
                EditorGUI.showMixedValue = false;
                using (new EditorGUI.DisabledScope(mixed && !bulkEdited))
                    if (GUILayout.Button(new GUIContent("Set for All Selected", "Stage this text for every selected flag. Save it with the main Apply button.")))
                        Edit(flags, bulkText);
            }
            // Every selected description stays visible, including mixed existing values.
            using (EditorGUILayout.ScrollViewScope view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;
                foreach (ObjectFlag flag in flags)
                {
                    if (flag == null || session.IsDeleted(flag))
                        continue;
                    ObjectFlag definition = session.Definition(flag);
                    EditorGUILayout.LabelField(new GUIContent(definition.DisplayName, definition.Description), EditorStyles.boldLabel);
                    EditorGUI.BeginChangeCheck();
                    string text = EditorGUILayout.TextArea(definition.Description ?? string.Empty, GUILayout.MinHeight(65f));
                    if (EditorGUI.EndChangeCheck())
                        Edit(new[] { flag }, text);
                    GUILayout.Space(6f);
                }
            }
            EditorGUILayout.LabelField("Changes remain in the main draft.", EditorStyles.miniLabel);
            if (GUILayout.Button(new GUIContent("Done", "Close this editor and keep the pending descriptions.")))
                Close();
        }

        /// <summary>Stages description changes with a single native Undo operation.</summary>
        /// <param name="targets">Flags receiving this text.</param>
        /// <param name="text">Explicit description, including an intentional empty value.</param>
        private void Edit(ObjectFlag[] targets, string text)
        {
            try
            {
                session.Edit(targets, flag => flag.Description = text, "Edit flag descriptions");
            }
            catch (InvalidOperationException exception)
            {
                ObjectFlagOperations.Report(exception.Message, Array.Empty<string>());
            }
        }

        #endregion
        #endregion
    }
}
