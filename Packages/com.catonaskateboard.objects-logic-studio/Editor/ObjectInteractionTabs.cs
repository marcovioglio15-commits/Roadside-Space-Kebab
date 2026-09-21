using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Separates hover, single-action features and the future multiple-interaction category.</summary>
    internal enum ObjectInteractionCategory { Hover, SingleInteraction, MultipleInteraction }

    /// <summary>Draws category navigation without changing the retained interaction draft.</summary>
    internal static class ObjectInteractionTabs
    {
        #region Labels

        private static readonly GUIContent[] labels =
        {
            new GUIContent("Hover", "Add and configure hover interactions for the selected object."),
            new GUIContent("Single Interaction", "Add and configure Grab, Drop and Throw actions."),
            new GUIContent("Multiple Interaction", "Reserved for future multiple interactions.")
        };

        #endregion

        #region Methods

        #region Drawing

        /// <summary>Shows exactly one category while keeping pending values when switching tabs.</summary>
        /// <param name="state">Workspace retaining the selected category across window reloads.</param>
        internal static void Draw(ObjectWorkspace state)
        {
            // Category navigation never participates in Apply or Discard.
            bool changed = GUI.changed;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                for (int index = 0; index < labels.Length; index++)
                    if (GUILayout.Toggle((int)state.Category == index, labels[index], EditorStyles.toolbarButton)
                        && (int)state.Category != index)
                    {
                        state.Category = (ObjectInteractionCategory)index;
                        state.Persist();
                    }
            GUI.changed = changed;
        }

        #endregion

        #endregion
    }
}
