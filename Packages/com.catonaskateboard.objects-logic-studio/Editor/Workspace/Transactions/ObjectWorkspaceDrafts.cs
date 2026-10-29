using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Preserves interaction drafts during navigation and applies one prefab's changes together.</summary>
    internal static class ObjectWorkspaceDrafts
    {
        #region Methods

        #region Navigation

        /// <summary>Saves the current proposal before changing interaction or object.</summary>
        /// <param name="state">Workspace whose current selection is being left.</param>
        internal static void Park(ObjectWorkspace state)
        {
            if (string.IsNullOrEmpty(state.DraftKey))
            {
                if (!state.PrefabChanged)
                    return;
                state.DraftKey = state.Target.PrefabGuid + ":" + state.Target.ObjectPath + ":" + state.Navigation.SelectedId;
            }
            state.SavedDrafts.RemoveAll(draft => draft.Key == state.DraftKey);
            state.SavedDrafts.Add(ObjectWorkspaceDraft.Capture(state));
        }

        /// <summary>Opens an interaction while retaining pending values from the previous selection.</summary>
        /// <param name="state">Persistent workspace.</param>
        /// <param name="feature">Existing component to open.</param>
        /// <param name="warning">Receives an unavailable-target message.</param>
        /// <returns>True when the interaction was selected.</returns>
        internal static bool Select(ObjectWorkspace state, ObjectInteraction feature, out string warning)
        {
            warning = string.Empty;
            if (feature == null || !ObjectAuthoringSave.TryValidate(feature.gameObject, out warning))
                return false;
            long id = ObjectWorkspaceTarget.FileId(feature);
            ObjectWorkspaceTarget target = new ObjectWorkspaceTarget();
            int index = feature is ObjectHover hover ? Array.IndexOf(feature.GetComponents<ObjectHover>(), hover) : 0;
            target.Capture(feature.gameObject, index);
            GameObject browsing = state.BrowseTarget.Resolve();
            if (browsing == null || state.BrowseTarget.PrefabGuid != target.PrefabGuid || !feature.transform.IsChildOf(browsing.transform))
                state.BrowseTarget.Capture(feature.gameObject, 0);
            string key = target.PrefabGuid + ":" + target.ObjectPath + ":" + id;
            bool navigating = state.DraftKey != key;
            if (!navigating && state.PrefabChanged)
                return true;
            Park(state);
            ObjectWorkspaceDraft saved = state.SavedDrafts.Find(draft => draft.Key == key);
            if (saved != null && !saved.Changed)
            {
                state.SavedDrafts.Remove(saved);
                saved = null;
            }
            if (saved != null)
            {
                saved.Restore(state);
                state.SavedDrafts.Remove(saved);
            }
            else
            {
                // The previous proposal is already parked; selection can safely read fresh applied values.
                ClearActive(state);
                ObjectWorkspaceSession.Select(state, feature.gameObject, index, out warning);
                switch (feature)
                {
                    case ObjectHover:
                        state.Category = ObjectInteractionCategory.Hover;
                        break;
                    case ObjectSingleInteraction single:
                        state.Category = ObjectInteractionCategory.SingleInteraction;
                        state.Single.Kind = single.Kind;
                        state.Single.Read(single.gameObject);
                        break;
                    case ObjectExtendedInteraction extended:
                        state.Category = ObjectInteractionCategory.MultipleInteraction;
                        state.Extended.Select(extended);
                        break;
                }
                state.DraftKey = key;
                state.Navigation.SelectedId = id;
            }
            if (navigating)
                ObjectNavigationHistory.Record(state, feature);
            state.Persist();
            return true;
        }

        /// <summary>Opens an object without asking to discard other interaction drafts.</summary>
        /// <param name="state">Persistent workspace.</param>
        /// <param name="target">Prefab branch to browse.</param>
        /// <param name="warning">Receives an unavailable-target message.</param>
        /// <returns>True when the object was selected.</returns>
        internal static bool SelectObject(ObjectWorkspace state, GameObject target, out string warning)
        {
            warning = string.Empty;
            if (target != null && !ObjectAuthoringSave.TryValidate(target, out warning))
                return false;
            state.BrowseTarget.Capture(target, 0);
            if (target != null && target.GetComponent<ObjectInteraction>() is ObjectInteraction first)
                return Select(state, first, out warning);
            Park(state);
            ClearActive(state);
            state.DraftKey = string.Empty;
            state.Navigation.SelectedId = 0;
            return ObjectWorkspaceSession.Select(state, target, 0, out warning);
        }

        /// <summary>Temporarily opens a parked proposal for a group-window operation, then restores the visible selection.</summary>
        /// <param name="state">Persistent workspace.</param>
        /// <param name="key">Interaction receiving the operation.</param>
        /// <param name="operation">Operation on that interaction's draft.</param>
        /// <returns>True when the target draft still exists.</returns>
        internal static bool Edit(ObjectWorkspace state, string key, Action operation)
        {
            if (state.DraftKey == key)
            {
                operation();
                state.Persist();
                return true;
            }
            ObjectWorkspaceDraft target = state.SavedDrafts.Find(draft => draft.Key == key);
            if (target == null)
                return false;
            ObjectWorkspaceDraft selected = ObjectWorkspaceDraft.Capture(state);
            target.Restore(state);
            try
            {
                operation();
                Park(state);
            }
            finally
            {
                selected.Restore(state);
                state.Persist();
            }
            return true;
        }

        /// <summary>Resolves the exact selected interaction without guessing among duplicate names.</summary>
        /// <param name="state">Workspace holding a stable component ID.</param>
        /// <returns>The selected component or null.</returns>
        internal static ObjectInteraction Selected(ObjectWorkspace state)
        {
            GameObject target = state.Target.Resolve();
            if (target != null && state.Navigation.SelectedId == 0)
            {
                ObjectInteraction initial = state.Category switch
                {
                    ObjectInteractionCategory.Hover => ObjectWorkspaceSession.Resolve(state),
                    ObjectInteractionCategory.SingleInteraction => SingleInteractionSession.Resolve(target, state.Single.Kind),
                    _ => state.Extended.Resolve(target)
                };
                if (initial != null)
                {
                    state.Navigation.SelectedId = ObjectWorkspaceTarget.FileId(initial);
                    state.DraftKey = state.Target.PrefabGuid + ":" + state.Target.ObjectPath + ":" + state.Navigation.SelectedId;
                    return initial;
                }
            }
            if (target != null)
                foreach (ObjectInteraction feature in target.GetComponents<ObjectInteraction>())
                    if (ObjectWorkspaceTarget.FileId(feature) == state.Navigation.SelectedId)
                        return feature;
            return null;
        }

        /// <summary>Releases only the active proposal's ownership after its values have been parked.</summary>
        /// <param name="state">Workspace preparing a fresh selection.</param>
        private static void ClearActive(ObjectWorkspace state)
        {
            state.HasBinding = false;
            state.Single = new SingleInteractionSession();
            state.Extended = new ExtendedInteractionSession();
            state.Draft = new HoverConfiguration();
            state.Baseline = ObjectWorkspace.Copy(state.Draft);
            state.Source = state.OriginalPreset = null;
        }

        #endregion

        #region Transactions

        /// <summary>Counts pending interaction drafts belonging to the currently selected prefab.</summary>
        /// <param name="state">Workspace containing active and parked proposals.</param>
        /// <returns>The number of interactions with pending edits.</returns>
        internal static int Count(ObjectWorkspace state)
        {
            int count = state.PrefabChanged ? 1 : 0;
            foreach (ObjectWorkspaceDraft draft in state.SavedDrafts)
                if (draft.Changed && draft.Key != state.DraftKey && draft.Target.PrefabGuid == state.Target.PrefabGuid)
                    count++;
            return count;
        }

        /// <summary>Validates all pending interaction proposals before saving the current prefab in one Undo group.</summary>
        /// <param name="state">Workspace containing the prefab's proposals.</param>
        /// <param name="warning">Receives a validation or save failure.</param>
        /// <returns>True when every selected-prefab proposal and scene setup change was saved.</returns>
        internal static bool Apply(ObjectWorkspace state, out string warning)
        {
            Park(state);
            ObjectWorkspaceDraft selected = ObjectWorkspaceDraft.Capture(state);
            List<ObjectWorkspaceDraft> pending = state.SavedDrafts.FindAll(draft => draft.Changed && draft.Target.PrefabGuid == selected.Target.PrefabGuid);
            warning = string.Empty;
            if (!ObjectStructureChanges.Validate(state, out warning))
                return false;
            foreach (ObjectWorkspaceDraft draft in pending)
            {
                draft.Restore(state);
                if (!ObjectWorkspaceSession.TryValidate(state, out warning))
                {
                    selected.Restore(state);
                    return false;
                }
            }
            selected.Restore(state);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply object configuration");
            Undo.RecordObject(state, "Apply object configuration");
            try
            {
                if (pending.Count == 0 && !ObjectWorkspaceSession.Apply(state, out warning))
                    return false;
                foreach (ObjectWorkspaceDraft draft in pending)
                {
                    draft.Restore(state);
                    if (!ObjectWorkspaceSession.Apply(state, out warning))
                    {
                        Undo.RevertAllDownToGroup(group);
                        selected.Restore(state);
                        GameObject target = state.Target.Resolve();
                        if (target != null)
                            ObjectAuthoringSave.Save(target);
                        return false;
                    }
                }
                selected.Restore(state);
                ObjectInteraction added = ObjectStructureChanges.Apply(state);
                state.SavedDrafts.RemoveAll(draft => draft.Target.PrefabGuid == selected.Target.PrefabGuid);
                state.Structure.RemoveAll(change => change.Target.PrefabGuid == selected.Target.PrefabGuid);
                selected.Restore(state);
                ObjectWorkspaceSession.Discard(state);
                if (added != null)
                    Select(state, added, out _);
                state.Persist();
                Undo.CollapseUndoOperations(group);
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                selected.Restore(state);
                GameObject target = state.Target.Resolve();
                if (target != null)
                    ObjectAuthoringSave.Save(target);
                warning = "The object could not be saved: " + exception.Message;
                return false;
            }
        }

        /// <summary>Abandons the selected prefab's proposals while keeping drafts for other prefabs.</summary>
        /// <param name="state">Workspace whose current prefab is being reset.</param>
        /// <param name="includeScene">Whether pending Scene Setup should be discarded too.</param>
        internal static void Discard(ObjectWorkspace state, bool includeScene = true)
        {
            Undo.RecordObject(state, "Discard object configuration");
            state.SavedDrafts.RemoveAll(draft => draft.Target.PrefabGuid == state.Target.PrefabGuid);
            state.Structure.RemoveAll(change => change.Target.PrefabGuid == state.Target.PrefabGuid);
            ObjectWorkspaceSession.Discard(state, includeScene);
        }

        #endregion

        #endregion
    }
}
