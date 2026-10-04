using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Edits flag labels while reporting ambiguous catalog names without rewriting input.</summary>
    [CustomEditor(typeof(ObjectFlag))]
    internal sealed class ObjectFlagEditor : UnityEditor.Editor
    {
        #region Methods

        #region Drawing

        /// <summary>Shows definition fields and invalidates cached selector labels after an edit.</summary>
        public override void OnInspectorGUI()
        {
            // Renaming changes presentation only; references in identities and recipes stay valid.
            serializedObject.Update();
            StudioGUI.PropertyField(serializedObject.FindProperty("DisplayName"));
            StudioGUI.PropertyField(serializedObject.FindProperty("Group"));
            StudioGUI.PropertyField(serializedObject.FindProperty("Description"));
            if (serializedObject.ApplyModifiedProperties())
                ObjectFlagCatalog.Invalidate();
            ObjectFlag current = (ObjectFlag)target;
            if (!current.TryValidate(out string warning))
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
            foreach (ObjectFlag flag in ObjectFlagCatalog.Flags)
                if (flag != current && string.Equals(flag.DisplayName, current.DisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    EditorGUILayout.LabelField("Another flag has this name. Choose a distinct name to keep selections clear.", EditorStyles.wordWrappedMiniLabel);
                    break;
                }
        }

        #endregion

        #endregion
    }
}
