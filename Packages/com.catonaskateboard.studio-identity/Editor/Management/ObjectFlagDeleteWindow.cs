using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Shows every actual flag usage in a scrollable deletion review.</summary>
    internal sealed class ObjectFlagDeleteWindow : EditorWindow
    {
        #region State

        private ObjectFlag[] flags;
        private List<ObjectFlagField> uses;
        private Vector2 scroll;
        private bool confirmed;

        #endregion
        #region Methods
        #region Window

        /// <summary>Requires explicit acceptance after reviewing the affected reference locations.</summary>
        /// <param name="targets">Definitions requested for deletion.</param>
        /// <param name="references">Actual fields using those definitions.</param>
        /// <returns>True only when deletion was explicitly accepted.</returns>
        internal static bool Confirm(IEnumerable<ObjectFlag> targets, List<ObjectFlagField> references)
        {
            // Unused definitions do not display an in-use warning.
            ObjectFlag[] definitions = targets.ToArray();
            if (references.Count == 0)
                return EditorUtility.DisplayDialog("Delete Identity Flags", "Delete " + definitions.Length
                    + " flag(s) on the next Apply? No assigned uses were found.", "Stage Deletion", "Cancel");
            ObjectFlagDeleteWindow window = CreateInstance<ObjectFlagDeleteWindow>();
            window.flags = definitions;
            window.uses = references;
            window.titleContent = new GUIContent("Flags In Use");
            window.minSize = new Vector2(650f, 420f);
            window.position = new Rect(200f, 200f, 760f, 520f);
            window.ShowModalUtility();
            return window.confirmed;
        }

        /// <summary>Lists exact serialized paths without truncating large usage reports.</summary>
        private void OnGUI()
        {
            // The warning is shown only for definitions with actual assigned references.
            EditorGUILayout.LabelField("Warning: selected flags are currently in use.", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Apply will remove these references and definitions. Discard will keep them.", EditorStyles.wordWrappedLabel);
            using (EditorGUILayout.ScrollViewScope view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;
                foreach (ObjectFlagField field in uses)
                {
                    EditorGUILayout.Space(6f);
                    EditorGUILayout.LabelField(string.Join(", ", field.Flags.Where(flag => flags.Contains(flag))
                        .Select(flag => flag.DisplayName).Distinct()), EditorStyles.boldLabel);
                    EditorGUILayout.SelectableLabel(field.Location, EditorStyles.wordWrappedMiniLabel,
                        GUILayout.Height(Mathf.Max(32f, EditorStyles.wordWrappedMiniLabel.CalcHeight(new GUIContent(field.Location), position.width - 40f))));
                }
            }
            using EditorGUILayout.HorizontalScope buttons = new EditorGUILayout.HorizontalScope();
            if (GUILayout.Button(new GUIContent("Cancel", "Keep the definitions and all references.")))
                Close();
            if (GUILayout.Button(new GUIContent("Stage Deletion", "Add this deletion and reference cleanup to the main draft.")))
            {
                confirmed = true;
                Close();
            }
        }

        #endregion
        #endregion
    }
}
