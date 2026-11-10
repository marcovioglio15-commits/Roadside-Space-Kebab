using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Commits selected serialized settings against the latest prefab while retaining unrelated drafts.</summary>
    internal static class ObjectSectionChanges
    {
        #region Methods

        #region Transactions

        /// <summary>Saves the selected paths using a fresh copy of the component for validation and Undo.</summary>
        /// <param name="proposal">Edited settings and their original applied baselines.</param>
        /// <param name="retained">Workspace retaining this interaction's previous proposal.</param>
        /// <param name="paths">Exact fields owned by this edit.</param>
        /// <param name="warning">Receives invalid settings or a conflicting field.</param>
        /// <returns>True when the requested changes are applied or already present.</returns>
        /// <param name="fillThreshold">Optional container threshold edited by a layout preview.</param>
        internal static bool Apply(ObjectWorkspace proposal, ObjectWorkspace retained, string[] paths, out string warning, int fillThreshold = -1)
        {
            ObjectWorkspace current = ScriptableObject.CreateInstance<ObjectWorkspace>();
            try
            {
                // Build a transaction without the other draft domains or Scene Setup.
                Read(current, retained);
                ObjectAuthoringSave.CheckSectionStage(current.Target.Resolve());
                using SerializedObject edits = new SerializedObject(proposal);
                using SerializedObject live = new SerializedObject(current);
                foreach (string path in paths)
                    Merge(edits.FindProperty(BaselinePath(path)), edits.FindProperty(path), live.FindProperty(path));
                if (fillThreshold >= 0)
                    ObjectFillLayoutSection.Merge(edits, live, fillThreshold);
                live.ApplyModifiedPropertiesWithoutUndo();
                if (!ObjectWorkspaceSession.Apply(current, out warning))
                    return false;
                Synchronize(current, retained, paths, fillThreshold);
                if (proposal != retained)
                    Synchronize(current, proposal, paths, fillThreshold);
                return true;
            }
            catch (Exception exception)
            {
                warning = exception.Message;
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(current);
            }
        }

        /// <summary>Combines a window's edits with a retained draft that changed while the window was open.</summary>
        /// <param name="opening">Retained proposal at the time the editor opened.</param>
        /// <param name="proposal">Local group edits.</param>
        /// <param name="result">Working copy initialized from the latest retained proposal.</param>
        /// <param name="paths">Fields owned by this section.</param>
        internal static void Combine(ObjectWorkspaceDraft opening, ObjectWorkspace proposal, ObjectWorkspace result, string[] paths)
        {
            ObjectWorkspace original = ScriptableObject.CreateInstance<ObjectWorkspace>();
            try
            {
                opening.Restore(original);
                using SerializedObject before = new SerializedObject(original);
                using SerializedObject edits = new SerializedObject(proposal);
                using SerializedObject combined = new SerializedObject(result);
                foreach (string path in paths)
                    Merge(before.FindProperty(path), edits.FindProperty(path), combined.FindProperty(path));
                combined.ApplyModifiedPropertiesWithoutUndo();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(original);
            }
        }

        /// <summary>Reloads specific fields from the actual prefab without changing any other draft values.</summary>
        /// <param name="retained">Workspace holding the interaction and its pending settings.</param>
        /// <param name="paths">Fields to discard.</param>
        internal static void Reload(ObjectWorkspace retained, string[] paths)
        {
            ObjectWorkspace current = ScriptableObject.CreateInstance<ObjectWorkspace>();
            try
            {
                Read(current, retained);
                Synchronize(current, retained, paths);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(current);
            }
        }

        /// <summary>Finds pending paths for a recovered interaction using its persisted original baseline.</summary>
        /// <param name="workspace">Retained interaction draft.</param>
        /// <returns>Changed top-level settings blocks, split into leaves at Apply.</returns>
        internal static string[] Pending(ObjectWorkspace workspace)
        {
            List<string> paths = new List<string>();
            using SerializedObject data = new SerializedObject(workspace);
            // Older sessions may contain pending settings in more than one domain.
            if (workspace.InteractionChanged)
            {
                AddChanged(data, "Draft", paths);
                AddChanged(data, "Binding", paths);
            }
            if (workspace.Single.HasChanges)
                AddChanged(data, "Single.Draft", paths);
            if (workspace.Extended.HasChanges)
                AddChanged(data, "Extended.Draft", paths);
            return paths.ToArray();
        }

        /// <summary>Checks whether one settings window owns pending values from the retained proposal.</summary>
        /// <param name="workspace">Window proposal with applied baselines.</param>
        /// <param name="paths">Fields in the current group.</param>
        /// <returns>True when at least one owned field differs from the applied value.</returns>
        internal static bool HasPending(ObjectWorkspace workspace, string[] paths)
        {
            using SerializedObject data = new SerializedObject(workspace);
            foreach (string path in paths)
            {
                using SerializedProperty edited = data.FindProperty(path);
                using SerializedProperty original = data.FindProperty(BaselinePath(path));
                if (edited != null && original != null && edited.contentHash != original.contentHash)
                    return true;
            }
            return false;
        }

        #endregion

        #region Serialization

        /// <summary>Captures the current values of the exact component identified by the retained draft.</summary>
        /// <param name="destination">New isolated transaction document.</param>
        /// <param name="source">Retained target and component ID.</param>
        internal static void Read(ObjectWorkspace destination, ObjectWorkspace source)
        {
            ObjectInteraction feature = ObjectWorkspaceDrafts.Selected(source);
            if (feature == null)
                throw new InvalidOperationException("The original interaction is no longer available. Restore it or discard these changes.");
            int hoverIndex = feature is ObjectHover hover ? Array.IndexOf(feature.GetComponents<ObjectHover>(), hover) : 0;
            if (!ObjectWorkspaceSession.Select(destination, feature.gameObject, hoverIndex, out string warning))
                throw new InvalidOperationException(warning);
            destination.Category = source.Category;
            destination.DraftKey = source.DraftKey;
            destination.Navigation.SelectedId = source.Navigation.SelectedId;
            switch (feature)
            {
                case ObjectSingleInteraction single:
                    destination.Single.Select(single);
                    break;
                case ObjectExtendedInteraction extended:
                    destination.Extended.Select(extended);
                    break;
            }
        }

        /// <summary>Copies selected applied values into a draft and its baseline.</summary>
        /// <param name="applied">Document with current applied values.</param>
        /// <param name="destination">Retained or detached draft to rebase.</param>
        /// <param name="paths">Committed or explicitly discarded fields.</param>
        /// <param name="fillThreshold">Optional container threshold to update without replacing other steps.</param>
        /// <param name="updateBaseline">Whether to update the underlying prefab baseline too.</param>
        internal static void Synchronize(ObjectWorkspace applied, ObjectWorkspace destination, string[] paths, int fillThreshold = -1, bool updateBaseline = true)
        {
            using SerializedObject source = new SerializedObject(applied);
            using SerializedObject target = new SerializedObject(destination);
            foreach (string path in paths)
            {
                StudioPropertyValue value = new StudioPropertyValue(source.FindProperty(path));
                value.Apply(target.FindProperty(path));
                if (updateBaseline)
                    value.Apply(target.FindProperty(BaselinePath(path)));
            }
            if (fillThreshold >= 0)
                ObjectFillLayoutSection.Transfer(source, target, fillThreshold, updateBaseline);
            target.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Applies disjoint changes recursively and rejects competing changes to the same value.</summary>
        /// <param name="original">Value in the applied baseline when editing began.</param>
        /// <param name="edited">Value proposed by this section.</param>
        /// <param name="current">Latest serialized prefab value, receiving the merge.</param>
        internal static void Merge(SerializedProperty original, SerializedProperty edited, SerializedProperty current)
        {
            if (original == null || edited == null || current == null)
                throw new InvalidOperationException("A selected setting moved or was removed. Reload the section before applying.");
            if (edited.contentHash == original.contentHash || edited.contentHash == current.contentHash)
                return;
            if (current.contentHash == original.contentHash)
            {
                new StudioPropertyValue(edited).Apply(current);
                return;
            }
            // Array length and order carry meaning. Competing changes to an array require review.
            if (current.propertyType != SerializedPropertyType.Generic || current.isArray)
                throw new InvalidOperationException("This setting changed elsewhere: " + current.propertyPath + ". Reload its section before applying.");
            using SerializedProperty child = edited.Copy();
            using SerializedProperty end = edited.GetEndProperty();
            if (!child.Next(true))
                return;
            while (child.depth > edited.depth && !SerializedProperty.EqualContents(child, end))
            {
                Merge(original.FindPropertyRelative(child.name), child, current.FindPropertyRelative(child.name));
                if (!child.Next(false))
                    break;
            }
        }

        /// <summary>Collects dirty children without transferring unused settings from another interaction.</summary>
        /// <param name="data">Retained serialized workspace.</param>
        /// <param name="root">Proposal block to compare.</param>
        /// <param name="paths">List receiving changed child paths.</param>
        private static void AddChanged(SerializedObject data, string root, List<string> paths)
        {
            using SerializedProperty parent = data.FindProperty(root);
            using SerializedProperty child = parent.Copy();
            using SerializedProperty end = parent.GetEndProperty();
            if (!child.Next(true))
                return;
            while (child.depth > parent.depth && !SerializedProperty.EqualContents(child, end))
            {
                using SerializedProperty original = data.FindProperty(BaselinePath(child.propertyPath));
                if (original != null && child.contentHash != original.contentHash)
                    paths.Add(child.propertyPath);
                if (!child.Next(false))
                    break;
            }
        }

        /// <summary>Maps a proposal path to its existing serialized baseline.</summary>
        /// <param name="path">Absolute workspace proposal path.</param>
        /// <returns>Matching original field path.</returns>
        private static string BaselinePath(string path)
        {
            if (path.StartsWith("Binding.", StringComparison.Ordinal))
                return "Original" + path;
            return path.StartsWith("Draft.", StringComparison.Ordinal)
                ? "Baseline" + path.Substring("Draft".Length)
                : path.Replace(".Draft.", ".Baseline.");
        }

        #endregion

        #endregion
    }
}
