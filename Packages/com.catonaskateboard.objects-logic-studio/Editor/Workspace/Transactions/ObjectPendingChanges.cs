using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Resolves the main window's retained interaction, structure and scene proposals independently.</summary>
    internal static class ObjectPendingChanges
    {
        #region Methods

        #region Interaction drafts

        /// <summary>Collects pending proposals across every previously selected prefab.</summary>
        /// <param name="state">Persistent workspace.</param>
        /// <returns>Drafts requiring an explicit resolution.</returns>
        internal static List<ObjectWorkspaceDraft> Drafts(ObjectWorkspace state)
        {
            List<ObjectWorkspaceDraft> pending = state.SavedDrafts.FindAll(draft => draft.Key != state.DraftKey && draft.Changed);
            if (state.PrefabChanged)
                pending.Insert(0, ObjectWorkspaceDraft.Capture(state));
            return pending;
        }

        /// <summary>Saves one retained interaction against its current prefab, including after a source-control import.</summary>
        /// <param name="state">Workspace retaining the proposal.</param>
        /// <param name="key">Stable interaction key.</param>
        /// <param name="warning">Receives the first unresolved change.</param>
        /// <returns>True when only this interaction was applied.</returns>
        internal static bool ApplyDraft(ObjectWorkspace state, string key, out string warning)
        {
            string detail = "The interaction draft is no longer available.";
            bool saved = false;
            bool found = ObjectWorkspaceDrafts.Edit(state, key,
                () => saved = ObjectSectionChanges.Apply(state, state, ObjectSectionChanges.Pending(state), out detail));
            warning = detail;
            return found && saved;
        }

        /// <summary>Drops exactly one retained interaction proposal, even if the interaction was deleted.</summary>
        /// <param name="state">Workspace retaining the proposal.</param>
        /// <param name="key">Stable interaction key.</param>
        internal static void DiscardDraft(ObjectWorkspace state, string key)
        {
            if (state.DraftKey == key)
                ObjectWorkspaceSession.Discard(state, includeScene: false);
            state.SavedDrafts.RemoveAll(draft => draft.Key == key);
            state.Persist();
        }

        #endregion

        #region Main window sections

        /// <summary>Checks every source of pending changes before a prefab-wide operation such as master import.</summary>
        /// <param name="state">Persistent workspace.</param>
        /// <param name="prefabGuid">Exact destination prefab.</param>
        /// <returns>True when the prefab still has unresolved object changes.</returns>
        internal static bool HasForPrefab(ObjectWorkspace state, string prefabGuid)
        {
            return state.Structure.Exists(change => change.Target.PrefabGuid == prefabGuid)
                || HasInteractionChanges(state, prefabGuid, 0);
        }

        /// <summary>Checks active, parked and detached edits for the specified source.</summary>
        /// <param name="state">Persistent workspace.</param>
        /// <param name="prefabGuid">Prefab about to be modified.</param>
        /// <param name="componentId">Exact component, or zero for the whole prefab.</param>
        /// <returns>True when any affected editor still owns a proposal.</returns>
        private static bool HasInteractionChanges(ObjectWorkspace state, string prefabGuid, long componentId)
        {
            return state.Target.PrefabGuid == prefabGuid && state.PrefabChanged
                    && (componentId == 0 || state.Navigation.SelectedId == componentId)
                || state.SavedDrafts.Exists(draft => draft.Changed && draft.Key != state.DraftKey
                    && draft.Target.PrefabGuid == prefabGuid && (componentId == 0 || draft.SelectedId == componentId))
                || ObjectGroupWindow.HasPendingFor(prefabGuid, componentId)
                || AssemblyPreviewWindow.HasPendingFor(prefabGuid, componentId)
                || OutletLayoutWindow.HasPendingFor(prefabGuid, componentId);
        }

        /// <summary>Applies the structural changes for one prefab as a dependency-ordered operation.</summary>
        /// <param name="state">Workspace retaining staged additions and removals.</param>
        /// <param name="prefabGuid">Prefab owning those operations.</param>
        /// <param name="warning">Receives a validation or save issue.</param>
        /// <returns>True when that prefab's changes were applied.</returns>
        internal static bool ApplyStructure(ObjectWorkspace state, string prefabGuid, out string warning)
        {
            warning = "Exit Play Mode before applying object structure.";
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return false;
            ObjectWorkspace scope = ScriptableObject.CreateInstance<ObjectWorkspace>();
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            bool writing = false;
            try
            {
                scope.Structure.AddRange(state.Structure.FindAll(change => change.Target.PrefabGuid == prefabGuid));
                warning = string.Empty;
                if (scope.Structure.Count == 0)
                    return true;
                scope.Target = scope.Structure[0].Target;
                // Do not remove a source while a recoverable proposal still belongs to it.
                foreach (ObjectStructureChange change in scope.Structure)
                    if (!change.Add && HasInteractionChanges(state, prefabGuid, change.ComponentId))
                    {
                        warning = "Apply or discard the settings for this interaction before removing it.";
                        return false;
                    }
                if (!ObjectStructureChanges.Validate(scope, out warning))
                    return false;
                ObjectAuthoringSave.CheckSectionStage(scope.Target.Resolve());
                Undo.SetCurrentGroupName("Apply interaction structure");
                writing = true;
                ObjectInteraction added = ObjectStructureChanges.Apply(scope);
                DiscardStructure(state, prefabGuid);
                if (added != null && state.Target.PrefabGuid == prefabGuid)
                    ObjectWorkspaceDrafts.Select(state, added, out _);
                Undo.CollapseUndoOperations(undo);
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undo);
                warning = exception.Message;
                try
                {
                    if (writing && scope.Target.Resolve() is GameObject target)
                        ObjectAuthoringSave.Save(target);
                }
                catch (Exception rollback)
                {
                    warning += " Restoring the prefab also failed: " + rollback.Message;
                }
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scope);
            }
        }

        /// <summary>Discards staged additions and removals for a single prefab.</summary>
        /// <param name="state">Workspace retaining the structure list.</param>
        /// <param name="prefabGuid">Prefab whose staged changes should be removed.</param>
        internal static void DiscardStructure(ObjectWorkspace state, string prefabGuid)
        {
            state.Structure.RemoveAll(change => change.Target.PrefabGuid == prefabGuid);
            state.Persist();
        }

        /// <summary>Applies Scene Setup on its own, preserving all prefab proposals.</summary>
        /// <param name="state">Workspace retaining observer references.</param>
        /// <param name="warning">Receives a validation or save issue.</param>
        /// <returns>True when observer changes were applied.</returns>
        internal static bool ApplyScene(ObjectWorkspace state, out string warning)
        {
            warning = "Exit Play Mode before applying Scene Setup.";
            if (EditorApplication.isPlayingOrWillChangePlaymode || !state.Observer.TryValidate(out warning))
                return false;
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            try
            {
                Undo.SetCurrentGroupName("Apply scene setup");
                Undo.RecordObject(state, "Apply scene setup");
                state.Observer.Apply();
                state.Persist();
                Undo.CollapseUndoOperations(undo);
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undo);
                warning = exception.Message;
                return false;
            }
        }

        /// <summary>Resolves the main window's remaining scopes when the native close dialog requests Apply.</summary>
        /// <param name="state">Workspace owning the changes.</param>
        /// <param name="warning">Receives the first unresolved scope.</param>
        /// <returns>True when all main-window changes were applied.</returns>
        internal static bool ApplyAll(ObjectWorkspace state, out string warning)
        {
            // Individual transactions are retained after success so a later conflict can be resolved separately.
            foreach (ObjectWorkspaceDraft draft in Drafts(state))
                if (!ApplyDraft(state, draft.Key, out warning))
                    return false;
            while (state.Structure.Count > 0)
                if (!ApplyStructure(state, state.Structure[0].Target.PrefabGuid, out warning))
                    return false;
            return ApplyScene(state, out warning);
        }

        #endregion

        #endregion
    }
}
