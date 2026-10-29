using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Keeps category checks open while the user makes several selections.</summary>
    internal sealed class ObjectCategoryPopup : PopupWindowContent
    {
        #region State

        private readonly ObjectWorkspace state;

        #endregion

        #region Methods

        #region Popup

        /// <summary>Connects the popup to the current workspace without copying object settings.</summary>
        /// <param name="state">Workspace receiving category checks.</param>
        internal ObjectCategoryPopup(ObjectWorkspace state)
        {
            this.state = state;
        }

        /// <summary>Reserves room for seven category checks and bulk controls.</summary>
        /// <returns>The compact popup size.</returns>
        public override Vector2 GetWindowSize()
        {
            return new Vector2(245f, 196f);
        }

        /// <summary>Applies checks immediately and keeps the popup open until focus leaves it.</summary>
        /// <param name="rect">Popup drawing bounds.</param>
        public override void OnGUI(Rect rect)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent("Everything", "Select all categories."), EditorStyles.toolbarButton))
                    Set(ObjectStudioNavigation.Categories.Everything);
                if (GUILayout.Button(new GUIContent("Nothing", "Clear every category."), EditorStyles.toolbarButton))
                    Set(ObjectStudioNavigation.Categories.Nothing);
            }
            foreach (ObjectStudioNavigation.Categories category in Enum.GetValues(typeof(ObjectStudioNavigation.Categories)))
            {
                if (category is ObjectStudioNavigation.Categories.Everything or ObjectStudioNavigation.Categories.Nothing)
                    continue;
                bool selected = (state.Navigation.Filters & category) != 0;
                bool requested = EditorGUILayout.ToggleLeft(new GUIContent(ObjectStudioNavigation.CategoryName(category), "Show interactions in this category."), selected);
                if (requested != selected)
                    Set(state.Navigation.Filters ^ category);
            }
        }

        /// <summary>Saves category checks and refreshes the adjacent columns.</summary>
        /// <param name="categories">Combined checked categories.</param>
        private void Set(ObjectStudioNavigation.Categories categories)
        {
            state.Navigation.Filters = categories;
            state.Persist();
            ObjectsLogicStudioWindow.RefreshOpen();
            editorWindow.Repaint();
        }

        #endregion

        #endregion
    }
}
