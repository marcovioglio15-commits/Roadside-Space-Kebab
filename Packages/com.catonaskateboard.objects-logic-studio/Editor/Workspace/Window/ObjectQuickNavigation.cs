using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Offers current-object settings search and navigation without creation commands.</summary>
    internal static class ObjectQuickNavigation
    {
        #region Methods

        #region Menu

        /// <summary>Opens quick navigation only while a browsing object is selected.</summary>
        /// <param name="state">Workspace retaining the current object and history.</param>
        internal static void Show(ObjectWorkspace state)
        {
            if (ObjectWorkspaceContext.Target(state) == null)
                return;
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new UnityEngine.GUIContent("Find settings  Ctrl+K"), false, () => ObjectCommandPalette.Open(state));
            if (state.Navigation.HistoryIndex > 0)
                menu.AddItem(new UnityEngine.GUIContent("Back  Alt+Left"), false, () => ObjectNavigationHistory.Go(state, -1));
            else
                menu.AddDisabledItem(new UnityEngine.GUIContent("Back  Alt+Left"));
            if (state.Navigation.HistoryIndex + 1 < state.Navigation.History.Count)
                menu.AddItem(new UnityEngine.GUIContent("Forward  Alt+Right"), false, () => ObjectNavigationHistory.Go(state, 1));
            else
                menu.AddDisabledItem(new UnityEngine.GUIContent("Forward  Alt+Right"));
            menu.ShowAsContext();
        }

        #endregion

        #endregion
    }
}
