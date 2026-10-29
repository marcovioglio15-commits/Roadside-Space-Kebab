using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Keeps visibility, category checks and simple list preferences separate from object settings.</summary>
    [Serializable]
    internal sealed class ObjectStudioNavigation
    {
        #region Types

        /// <summary>Independent workspace panels and the optional settings map.</summary>
        [Flags]
        internal enum Panels { Object = 1, Interactions = 2, Presets = 4, SceneSetup = 8, SettingsTree = 16 }
        /// <summary>Interaction categories combined through persistent checkboxes.</summary>
        [Flags]
        internal enum Categories { Nothing = 0, Inventory = 1, Physics = 2, Processes = 4, Dialogue = 8, Assembly = 16, Spawning = 32, Feedback = 64, Everything = 127 }
        /// <summary>Whether the list includes enabled or disabled interactions.</summary>
        internal enum Status { Everything, Enabled, Disabled }
        /// <summary>List grouping by interaction type or the initial letter of its name.</summary>
        internal enum Order { Type, Name }

        #endregion

        #region Fields

        [Header("Views")]
        [Tooltip("Choose the panels to show.")]
        public Panels Views = Panels.Object | Panels.Interactions;
        [Tooltip("Choose which interaction categories to show.")]
        public Categories Filters = Categories.Everything;
        [Tooltip("Find an interaction, group or setting.")]
        public string Search = string.Empty;
        [Tooltip("Show enabled interactions, disabled interactions, or both.")]
        public Status State;
        [Tooltip("Group the list by type or name.")]
        public Order Sort;
        [Tooltip("Also list interactions on child objects.")]
        public bool IncludeChildren;
        [Tooltip("Keep the selected interaction when its name changes.")]
        public long SelectedId;
        [Tooltip("Recently opened settings groups.")]
        public List<string> Recent = new List<string>();
        [Tooltip("Previously selected interactions.")]
        public List<ObjectNavigationStep> History = new List<ObjectNavigationStep>();
        [Tooltip("Current position in the navigation history.")]
        public int HistoryIndex = -1;
        [Tooltip("Collapsed panels and list groups.")]
        public List<string> Collapsed = new List<string>();
        [Tooltip("Scroll position in the interaction list.")]
        public Vector2 ListScroll;
        [Tooltip("Scroll position in the selected interaction.")]
        public Vector2 SettingsScroll;
        [Tooltip("Scroll position in the preset library.")]
        public Vector2 PresetsScroll;

        #endregion

        #region Methods

        #region Toolbars

        /// <summary>Draws only workspace-wide navigation above the object picker.</summary>
        /// <param name="state">Workspace retaining these preferences.</param>
        internal void Draw(ObjectWorkspace state)
        {
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar);
            if (GUILayout.Button(new GUIContent("Views", "Choose what to show."), EditorStyles.toolbarDropDown, GUILayout.Width(65f)))
                ViewsMenu(state);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(ObjectWorkspaceContext.Target(state) == null))
                if (GUILayout.Button(new GUIContent("Quick access", "Find settings or revisit an interaction."), EditorStyles.toolbarDropDown, GUILayout.Width(105f)))
                    ObjectQuickNavigation.Show(state);
        }

        /// <summary>Places interaction filters directly above the catalog, settings and preset columns.</summary>
        /// <param name="state">Workspace retaining category and sort choices.</param>
        internal void DrawFilters(ObjectWorkspace state)
        {
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar);
            if (GUILayout.Button(new GUIContent("Categories", "Choose the interaction categories."), EditorStyles.toolbarDropDown, GUILayout.Width(95f)))
                PopupWindow.Show(GUILayoutUtility.GetLastRect(), new ObjectCategoryPopup(state));
            if (GUILayout.Button(new GUIContent("Filters", "Filter enabled state, tools and input."), EditorStyles.toolbarDropDown, GUILayout.Width(65f)))
                StatusMenu(state);
            if (GUILayout.Button(new GUIContent("Sort: " + (Sort == Order.Name ? "Name" : "Type"), "Choose how to group the list."), EditorStyles.toolbarDropDown, GUILayout.Width(95f)))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Type"), Sort != Order.Name, () => SetSort(state, Order.Type));
                menu.AddItem(new GUIContent("Name"), Sort == Order.Name, () => SetSort(state, Order.Name));
                menu.ShowAsContext();
            }
            GUILayout.FlexibleSpace();
        }

        #endregion

        #region Menus

        /// <summary>Offers independent view checks, including the optional settings tree.</summary>
        /// <param name="state">Workspace retaining visibility.</param>
        private void ViewsMenu(ObjectWorkspace state)
        {
            GenericMenu menu = new GenericMenu();
            foreach (Panels panel in Enum.GetValues(typeof(Panels)))
            {
                Panels choice = panel;
                menu.AddItem(new GUIContent(panel switch { Panels.SceneSetup => "Scene Setup", Panels.SettingsTree => "Settings tree", _ => panel.ToString() }), (Views & panel) != 0,
                    () => { Views ^= choice; state.Persist(); ObjectsLogicStudioWindow.RefreshOpen(); });
            }
            menu.ShowAsContext();
        }

        /// <summary>Offers the remaining state, reference-name and child-object filters.</summary>
        /// <param name="state">Workspace retaining list preferences.</param>
        private void StatusMenu(ObjectWorkspace state)
        {
            GenericMenu menu = new GenericMenu();
            foreach (Status status in Enum.GetValues(typeof(Status)))
            {
                Status choice = status;
                menu.AddItem(new GUIContent(status.ToString()), State == status,
                    () => { State = choice; state.Persist(); ObjectsLogicStudioWindow.RefreshOpen(); });
            }
            menu.AddSeparator(string.Empty);
            ObjectSettingsSearch.Filters(menu, state);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Include children"), IncludeChildren,
                () => { IncludeChildren = !IncludeChildren; state.Persist(); ObjectsLogicStudioWindow.RefreshOpen(); });
            menu.ShowAsContext();
        }

        /// <summary>Changes only the list grouping and refreshes cached rows.</summary>
        /// <param name="state">Persistent workspace.</param>
        /// <param name="order">Requested grouping.</param>
        private void SetSort(ObjectWorkspace state, Order order)
        {
            Sort = order;
            state.Persist();
            ObjectsLogicStudioWindow.RefreshOpen();
        }

        /// <summary>Returns the short category label used by lists, menus and the settings finder.</summary>
        /// <param name="category">Functional category.</param>
        /// <returns>The English category label.</returns>
        internal static string CategoryName(Categories category)
        {
            return category switch
            {
                Categories.Nothing => "Nothing",
                Categories.Inventory => "Pick up & inventory",
                Categories.Physics => "Transform & physics",
                Categories.Processes => "Process & surfaces",
                Categories.Dialogue => "Dialogue & orders",
                Categories.Assembly => "Assembly",
                Categories.Spawning => "Spawn & lifetime",
                Categories.Feedback => "Feedback & availability",
                _ => "Everything"
            };
        }

        #endregion

        #endregion
    }
}
