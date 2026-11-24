using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Collects a group name before staging its explicit add or rename operation.</summary>
    internal sealed class ObjectFlagTextWindow : EditorWindow
    {
        #region State

        private string value;
        private string label;
        private Action<string> apply;

        #endregion
        #region Methods
        #region Window

        /// <summary>Shows a focused text editor for one pending operation.</summary>
        /// <param name="title">Operation name.</param>
        /// <param name="initial">Current value.</param>
        /// <param name="onApply">Receives explicitly accepted text.</param>
        internal static void Open(string title, string initial, Action<string> onApply)
        {
            ObjectFlagTextWindow window = CreateInstance<ObjectFlagTextWindow>();
            window.titleContent = new GUIContent(title);
            window.label = title;
            window.value = initial;
            window.apply = onApply;
            window.minSize = new Vector2(360f, 90f);
            window.ShowUtility();
        }

        /// <summary>Applies text only after the user accepts the operation.</summary>
        private void OnGUI()
        {
            value = EditorGUILayout.TextField(new GUIContent(label, "Value applied to the selected group or flags."), value);
            using EditorGUI.DisabledScope empty = new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(value));
            if (GUILayout.Button(new GUIContent("Stage", "Add this change to the main draft.")))
            {
                try
                {
                    apply?.Invoke(value);
                    Close();
                }
                catch (InvalidOperationException exception)
                {
                    ObjectFlagOperations.Report(exception.Message, Array.Empty<string>());
                }
            }
        }

        #endregion
        #endregion
    }
}
