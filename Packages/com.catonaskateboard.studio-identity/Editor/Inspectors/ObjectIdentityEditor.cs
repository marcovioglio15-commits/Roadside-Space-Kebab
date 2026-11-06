using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Edits combined object membership with the shared searchable flag picker.</summary>
    [CustomEditor(typeof(ObjectIdentity))]
    internal sealed class ObjectIdentityEditor : UnityEditor.Editor
    {
        #region Methods

        #region Lifetime

        /// <summary>Listens for live changes while this inspector is open.</summary>
        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += RefreshPlayState;
            if (Application.isPlaying)
                ObjectIdentity.Changed += RefreshFlags;
        }

        /// <summary>Removes inspector listeners when another target is selected.</summary>
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= RefreshPlayState;
            ObjectIdentity.Changed -= RefreshFlags;
        }

        /// <summary>Rebinds after Play resets the identity event and refreshes the displayed labels.</summary>
        /// <param name="state">Current editor Play transition.</param>
        private void RefreshPlayState(PlayModeStateChange state)
        {
            ObjectIdentity.Changed -= RefreshFlags;
            if (state == PlayModeStateChange.EnteredPlayMode)
                ObjectIdentity.Changed += RefreshFlags;
            Repaint();
        }

        /// <summary>Repaints only when this target's gameplay membership changes.</summary>
        /// <param name="identity">Object whose active flags were committed.</param>
        private void RefreshFlags(ObjectIdentity identity)
        {
            if (identity == target)
                Repaint();
        }

        #endregion

        #region Drawing

        /// <summary>Shows saved membership and the live flags during Play.</summary>
        public override void OnInspectorGUI()
        {
            // In Play, show the membership used by gameplay before the saved activation defaults.
            ObjectIdentity identity = (ObjectIdentity)target;
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Active Flags", EditorStyles.boldLabel);
                using (new EditorGUI.IndentLevelScope())
                    foreach (ObjectFlag flag in identity.ActiveFlags)
                        EditorGUILayout.LabelField(flag != null ? flag.DisplayName : "Missing Flag");
                EditorGUILayout.Space();
            }

            serializedObject.Update();
            ObjectFlagSelector.Draw(serializedObject.FindProperty("flags"), Application.isPlaying
                ? new GUIContent("Starting Flags", "Saved flags assigned when a new Play session starts.") : null);
            serializedObject.ApplyModifiedProperties();
            if (!ObjectFlagRules.TryValidate(identity.AuthoredFlags, true, out string warning))
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
        }

        #endregion

        #endregion
    }
}
