using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Separates observation, direct actions, dialogue and automatic contact effects.</summary>
    internal enum ObjectInteractionCategory { Hover, SingleInteraction, MultipleInteraction, PassiveInteraction, UnlockInteractions, SceneObserver, ObjectAssemble, SpawnManagement }

    /// <summary>Draws category navigation without changing the retained interaction draft.</summary>
    internal static class ObjectInteractionTabs
    {
        #region Labels

        private static readonly GUIContent[] labels =
        {
            new GUIContent("Hover", "Add and configure hover interactions for the selected object."),
            new GUIContent("Single", "Single Interaction: configure carrying, Dispenser and Container actions."),
            new GUIContent("Multiple", "Multiple Interaction: configure prioritized dialogues and explicit text pages."),
            new GUIContent("Passive", "Passive Interaction: configure contact modifications and object outlines."),
            new GUIContent("Unlock", "Lock existing interactions until their configured conditions are met."),
            new GUIContent("Observer", "Connect a camera and player independently of the edited interaction prefab."),
            new GUIContent("Assembly", "Configure assembly tables, product recipes and ingredient magnets."),
            new GUIContent("Spawning", "Generate prefab outputs when individual source instances complete the configured interactions.")
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
            int columns = Mathf.Clamp(Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 16f) / 105f), 1, labels.Length);
            int selected = GUILayout.SelectionGrid((int)state.Category, labels, columns, EditorStyles.toolbarButton);
            if (selected != (int)state.Category)
            {
                state.Category = (ObjectInteractionCategory)selected;
                state.Persist();
            }
            GUI.changed = changed;
        }

        #endregion

        #endregion
    }
}
