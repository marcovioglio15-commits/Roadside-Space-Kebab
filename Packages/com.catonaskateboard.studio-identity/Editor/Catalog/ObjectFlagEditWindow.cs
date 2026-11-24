using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Edits one detached definition in the management window's shared proposal.</summary>
    internal sealed class ObjectFlagEditWindow : EditorWindow
    {
        #region Fields

        [Header("Definition")]
        [Tooltip("Original flag identity being edited.")]
        [SerializeField]
        private ObjectFlag flag;
        [Tooltip("Management proposal receiving these edits.")]
        [SerializeField]
        private ObjectFlagDraftSession session;

        #endregion
        #region Methods
        #region Window

        /// <summary>Opens current proposed metadata without writing the source asset.</summary>
        /// <param name="target">Selected flag identity.</param>
        /// <param name="draft">Shared management proposal.</param>
        internal static void Open(ObjectFlag target, ObjectFlagDraftSession draft)
        {
            try
            {
                draft.Editable(target);
                ObjectFlagEditWindow window = GetWindow<ObjectFlagEditWindow>("Edit Object Flag");
                window.flag = target;
                window.session = draft;
                window.minSize = new Vector2(380f, 240f);
                window.Show();
                window.Focus();
            }
            catch (InvalidOperationException exception)
            {
                ObjectFlagOperations.Report(exception.Message, Array.Empty<string>());
            }
        }

        /// <summary>Draws the working copy with native property Undo and a multiline description.</summary>
        private void OnGUI()
        {
            // The parent owns the proposal; closing this editor never commits it.
            if (session == null || session.Released || flag == null || session.IsDeleted(flag))
                return;
            using SerializedObject data = new SerializedObject(session.Editable(flag));
            EditorGUILayout.PropertyField(data.FindProperty("DisplayName"));
            EditorGUILayout.PropertyField(data.FindProperty("Group"));
            SerializedProperty description = data.FindProperty("Description");
            EditorGUILayout.LabelField(new GUIContent("Description", description.tooltip));
            description.stringValue = EditorGUILayout.TextArea(description.stringValue, GUILayout.MinHeight(90f), GUILayout.ExpandHeight(true));
            if (data.ApplyModifiedProperties())
                session.Notify();
            EditorGUILayout.LabelField("Changes remain in the main draft.", EditorStyles.miniLabel);
            if (GUILayout.Button(new GUIContent("Done", "Close this editor and keep the pending changes.")))
                Close();
        }

        #endregion
        #endregion
    }
}
