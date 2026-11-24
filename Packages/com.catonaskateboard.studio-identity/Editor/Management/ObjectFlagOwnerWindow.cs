using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Opens an exact flag field when its package has no dedicated editor window.</summary>
    internal sealed class ObjectFlagOwnerWindow : EditorWindow
    {
        #region Fields

        [Header("Selection")]
        [Tooltip("Asset or scene component containing this selection.")]
        [SerializeField]
        private UnityEngine.Object owner;
        [Tooltip("Exact serialized field opened by the project manager.")]
        [SerializeField]
        private string propertyPath;
        private ObjectFlagField field;

        #endregion
        #region Methods
        #region Window

        /// <summary>Refreshes the visible membership after native Undo or Redo.</summary>
        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnProjectChange;
        }

        /// <summary>Releases the Undo listener when this field editor closes.</summary>
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnProjectChange;
        }

        /// <summary>Shows one selection independently of the main Inspector and its lock state.</summary>
        /// <param name="target">Resolved asset or scene component.</param>
        /// <param name="path">Exact selection property.</param>
        internal static void Open(UnityEngine.Object target, string path)
        {
            ObjectFlagOwnerWindow window = GetWindow<ObjectFlagOwnerWindow>("Flag Selection");
            window.owner = target;
            window.propertyPath = path;
            window.field = null;
            window.minSize = new Vector2(480f, 160f);
            window.Show();
        }

        /// <summary>Releases cached labels after an import changes the owning asset.</summary>
        private void OnProjectChange()
        {
            field = null;
            Repaint();
        }

        /// <summary>Draws the exact field with the shared project selector or a native reference control.</summary>
        private void OnGUI()
        {
            // Deleted owners never redirect this window to another selection.
            if (owner == null)
                return;
            using EditorGUI.DisabledScope locked = new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode);
            using SerializedObject data = new SerializedObject(owner);
            SerializedProperty property = data.FindProperty(propertyPath);
            if (property == null)
            {
                EditorGUILayout.LabelField("This field no longer exists. Refresh the project scan.");
                return;
            }
            bool flag = ObjectFlagStorage.IsFlag(property, out bool portable);
            string path = owner is Component component && !EditorUtility.IsPersistent(component)
                ? component.gameObject.scene.path : AssetDatabase.GetAssetPath(owner);
            field ??= new ObjectFlagField(path, owner, property, portable, flag, true);
            EditorGUILayout.LabelField(field.Root, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(field.Label, EditorStyles.wordWrappedLabel);
            if (flag)
                ObjectFlagSelectionGUI.Draw(field, Repaint);
            else
            {
                EditorGUILayout.PropertyField(property, true);
                if (data.ApplyModifiedProperties())
                    field.Visit((target, selection) => true, true);
            }
        }

        #endregion
        #endregion
    }

    /// <summary>Shares the compact dropdown used by project rows and the fallback owner editor.</summary>
    internal static class ObjectFlagSelectionGUI
    {
        #region Methods
        #region Drawing

        /// <summary>Opens the same guarded flag picker for a stable project selection.</summary>
        /// <param name="field">Native or portable field with current membership.</param>
        /// <param name="changed">Refreshes the caller after a saved selection.</param>
        /// <param name="session">Optional management proposal.</param>
        internal static void Draw(ObjectFlagField field, Action changed, ObjectFlagDraftSession session = null)
        {
            ObjectFlag[] values = session != null ? session.Values(field) : field.Flags;
            string label = values.Length == 0 || values.Length == 1 && values[0] == null ? "None"
                : string.Join(", ", values.Select(flag => flag != null ? (session != null ? session.Definition(flag) : flag).DisplayName : "Missing"));
            Rect rect = GUILayoutUtility.GetRect(new GUIContent(label), EditorStyles.popup, GUILayout.MinWidth(120f), GUILayout.ExpandWidth(true));
            if (EditorGUI.DropdownButton(rect, new GUIContent(label, field.Location + "\nShared assets update every consumer."), FocusType.Keyboard))
                PopupWindow.Show(rect, new ObjectFlagPopup(field, changed, session));
        }

        #endregion
        #endregion
    }
}
