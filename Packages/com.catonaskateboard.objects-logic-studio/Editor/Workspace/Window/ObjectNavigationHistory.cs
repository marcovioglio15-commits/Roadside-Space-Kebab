using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares keyboard search and exact back/forward destinations without changing object settings.</summary>
    internal static class ObjectNavigationHistory
    {
        #region State

        private static bool replaying;

        #endregion

        #region Methods

        #region History

        /// <summary>Records explicit interaction selections and removes obsolete forward destinations.</summary>
        /// <param name="state">Persistent workspace.</param>
        /// <param name="feature">Newly selected interaction.</param>
        internal static void Record(ObjectWorkspace state, ObjectInteraction feature)
        {
            if (replaying)
                return;
            ObjectStudioNavigation navigation = state.Navigation;
            if (navigation.HistoryIndex + 1 < navigation.History.Count)
                navigation.History.RemoveRange(navigation.HistoryIndex + 1, navigation.History.Count - navigation.HistoryIndex - 1);
            navigation.History.Add(new ObjectNavigationStep { Target = ObjectWorkspace.Copy(state.Target), ComponentId = ObjectWorkspaceTarget.FileId(feature), Name = feature.InteractionName });
            if (navigation.History.Count > 30)
                navigation.History.RemoveAt(0);
            navigation.HistoryIndex = navigation.History.Count - 1;
        }

        /// <summary>Reopens one exact history destination while preserving every existing draft.</summary>
        /// <param name="state">Persistent workspace.</param>
        /// <param name="offset">Negative for Back, positive for Forward.</param>
        internal static void Go(ObjectWorkspace state, int offset)
        {
            int index = state.Navigation.HistoryIndex + offset;
            if (index < 0 || index >= state.Navigation.History.Count)
                return;
            ObjectWorkspaceDrafts.Park(state);
            ObjectNavigationStep step = state.Navigation.History[index];
            string path = AssetDatabase.GUIDToAssetPath(step.Target.PrefabGuid);
            if (string.IsNullOrEmpty(path))
                return;
            replaying = true;
            try
            {
                PrefabStageUtility.OpenPrefab(path);
                GameObject target = step.Target.Resolve();
                if (target == null)
                    return;
                foreach (ObjectInteraction feature in target.GetComponents<ObjectInteraction>())
                    if (ObjectWorkspaceTarget.FileId(feature) == step.ComponentId)
                    {
                        ObjectWorkspaceDrafts.Select(state, feature, out _);
                        state.Navigation.HistoryIndex = index;
                        state.Persist();
                        ObjectsLogicStudioWindow.RefreshOpen();
                        break;
                    }
            }
            finally
            {
                replaying = false;
            }
        }

        #endregion

        #region Keyboard

        /// <summary>Handles search and navigation shortcuts only while this workspace receives keyboard events.</summary>
        /// <param name="state">Persistent workspace.</param>
        internal static void HandleKeys(ObjectWorkspace state)
        {
            Event input = Event.current;
            if (input.type != EventType.KeyDown)
                return;
            if ((input.control || input.command) && input.keyCode == KeyCode.F)
            {
                state.Navigation.Views |= ObjectStudioNavigation.Panels.Interactions;
                GUI.FocusControl("ObjectsLogicStudio.Search");
                input.Use();
            }
            else if ((input.control || input.command) && input.keyCode == KeyCode.K)
            {
                ObjectCommandPalette.Open(state);
                input.Use();
            }
            else if (input.alt && input.keyCode is KeyCode.LeftArrow or KeyCode.RightArrow)
            {
                Go(state, input.keyCode == KeyCode.LeftArrow ? -1 : 1);
                input.Use();
            }
        }

        #endregion

        #endregion
    }
}
