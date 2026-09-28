using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Edits combined object membership with the shared searchable flag picker.</summary>
    [CustomEditor(typeof(ObjectIdentity))]
    internal sealed class ObjectIdentityEditor : UnityEditor.Editor
    {
        #region Methods

        #region Drawing

        /// <summary>Shows authored membership and optional live flags during Play.</summary>
        public override void OnInspectorGUI()
        {
            // Live interaction changes are separate from the saved activation defaults.
            serializedObject.Update();
            ObjectFlagSelector.Draw(serializedObject.FindProperty("flags"));
            serializedObject.ApplyModifiedProperties();
            ObjectIdentity identity = (ObjectIdentity)target;
            if (!ObjectFlagRules.TryValidate(identity.AuthoredFlags, true, out string warning))
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
            if (!Application.isPlaying)
                return;
            EditorGUILayout.LabelField("Active Flags", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
                foreach (ObjectFlag flag in identity.ActiveFlags)
                    EditorGUILayout.LabelField(flag != null ? flag.DisplayName : "Missing Flag");
        }

        #endregion

        #endregion
    }
}
