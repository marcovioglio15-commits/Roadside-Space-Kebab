using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Lets the main workspace review each retained proposal without applying unrelated changes.</summary>
    internal sealed class ObjectPendingWindow : EditorWindow
    {
        #region Types

        /// <summary>Executes one row's validated transaction.</summary>
        private delegate bool ApplyScope(out string warning);

        #endregion

        #region State

        private string status = string.Empty;
        private Vector2 scroll;

        #endregion

        #region Methods

        #region Drawing

        /// <summary>Shows the main workspace's retained drafts, structural edits and Scene Setup.</summary>
        internal static void Open()
        {
            GetWindow<ObjectPendingWindow>("Pending Changes").Show();
        }

        /// <summary>Renders one Apply and Discard pair per independent scope.</summary>
        private void OnGUI()
        {
            ObjectWorkspace state = ObjectWorkspace.instance;
            using EditorGUI.DisabledScope locked = new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode);
            using EditorGUILayout.ScrollViewScope page = new EditorGUILayout.ScrollViewScope(scroll);
            scroll = page.scrollPosition;
            if (state.Observer.HasChanges)
                DrawScope("Scene Setup", (out string warning) => ObjectPendingChanges.ApplyScene(state, out warning), () =>
                {
                    state.Observer.Discard();
                    state.Persist();
                });
            HashSet<string> prefabs = new HashSet<string>();
            foreach (ObjectStructureChange change in state.Structure)
            {
                string guid = change.Target.PrefabGuid;
                if (prefabs.Add(guid))
                    DrawScope(change.Target.ObjectName + " · Add / Remove",
                        (out string warning) => ObjectPendingChanges.ApplyStructure(state, guid, out warning),
                        () => ObjectPendingChanges.DiscardStructure(state, guid));
            }
            if (state.PrefabChanged)
                DrawDraft(state, state.DraftKey, state.Target.ObjectName, state.Category, state.Binding, state.Single, state.Extended);
            foreach (ObjectWorkspaceDraft draft in state.SavedDrafts)
                if (draft.Changed && draft.Key != state.DraftKey)
                    DrawDraft(state, draft.Key, draft.Target.ObjectName, draft.Category, draft.Binding, draft.Single, draft.Extended);
            if (status.Length > 0)
                EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>Displays a retained interaction using its existing snapshot without copying the proposal.</summary>
        /// <param name="state">Persistent workspace.</param>
        /// <param name="key">Stable draft identity.</param>
        /// <param name="owner">Object name captured with the proposal.</param>
        /// <param name="category">Active interaction family.</param>
        /// <param name="binding">Hover settings when this is a Hover draft.</param>
        /// <param name="single">Single interaction settings.</param>
        /// <param name="extended">Extended interaction settings.</param>
        private void DrawDraft(ObjectWorkspace state, string key, string owner, ObjectInteractionCategory category,
            HoverBindingDraft binding, SingleInteractionSession single, ExtendedInteractionSession extended)
        {
            string name = category switch
            {
                ObjectInteractionCategory.Hover => binding.Name,
                ObjectInteractionCategory.SingleInteraction => single.Draft.Name,
                _ => extended.Draft.Name
            };
            DrawScope(owner + " · " + name, (out string warning) => ObjectPendingChanges.ApplyDraft(state, key, out warning),
                () => ObjectPendingChanges.DiscardDraft(state, key));
        }

        /// <summary>Shows the actions for one scope and refreshes the workspace after resolving it.</summary>
        /// <param name="label">Object and scope name.</param>
        /// <param name="apply">Validated transaction for this row.</param>
        /// <param name="discard">Reset for this row.</param>
        private void DrawScope(string label, ApplyScope apply, Action discard)
        {
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
            EditorGUILayout.LabelField(label);
            if (GUILayout.Button("Apply", GUILayout.Width(60f)))
            {
                if (apply(out status))
                    status = "Changes applied.";
                ObjectsLogicStudioWindow.RefreshOpen();
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("Discard", GUILayout.Width(60f)))
            {
                discard();
                status = string.Empty;
                ObjectsLogicStudioWindow.RefreshOpen();
                GUIUtility.ExitGUI();
            }
        }

        #endregion

        #endregion
    }
}
