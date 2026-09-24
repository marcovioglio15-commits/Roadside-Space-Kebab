using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Separates observation, direct actions, dialogue and automatic contact effects.</summary>
    internal enum ObjectInteractionCategory { Hover, SingleInteraction, MultipleInteraction, PassiveInteraction, UnlockInteractions, SceneObserver, ObjectAssemble }

    /// <summary>Draws category navigation without changing the retained interaction draft.</summary>
    internal static class ObjectInteractionTabs
    {
        #region Labels

        private static readonly GUIContent[] labels =
        {
            new GUIContent("Hover", "Add and configure hover interactions for the selected object."),
            new GUIContent("Single", "Single Interaction: configure Grab, Drop and Throw actions."),
            new GUIContent("Multiple", "Multiple Interaction: configure prioritized dialogues and explicit text pages."),
            new GUIContent("Passive", "Passive Interaction: configure contact modifications and object outlines."),
            new GUIContent("Unlock Interactions", "Lock existing interactions until their configured conditions are met."),
            new GUIContent("Scene Observer", "Connect a camera and player independently of the edited interaction prefab."),
            new GUIContent("Object Assemble", "Configure assembly tables, product recipes and ingredient magnets.")
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
