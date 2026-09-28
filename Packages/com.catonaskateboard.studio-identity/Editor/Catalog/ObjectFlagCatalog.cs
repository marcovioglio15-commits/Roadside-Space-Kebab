using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Caches the project's extensible object flags and creates explicitly requested definitions.</summary>
    [InitializeOnLoad]
    public static class ObjectFlagCatalog
    {
        #region State

        private const string folder = "Assets/ScriptableObjects/Object Flags";
        private static ObjectFlag[] flags = Array.Empty<ObjectFlag>();
        private static bool dirty = true;

        #endregion

        #region Properties

        /// <summary>Sorted catalog refreshed only after project assets change.</summary>
        public static IReadOnlyList<ObjectFlag> Flags
        {
            get
            {
                // Repaint uses the cached asset list rather than querying the Asset Database repeatedly.
                Refresh();
                return flags;
            }
        }

        #endregion

        #region Methods

        #region Cache

        /// <summary>Connects catalog invalidation to actual project and Undo changes.</summary>
        static ObjectFlagCatalog()
        {
            // Asset creation, deletion and renaming invalidate a single shared catalog.
            EditorApplication.projectChanged += Invalidate;
            Undo.undoRedoPerformed += Invalidate;
        }

        /// <summary>Requests rediscovery on the next visible selector or catalog operation.</summary>
        public static void Invalidate()
        {
            // No project scan is performed inside notifications.
            dirty = true;
        }

        /// <summary>Loads existing definitions and sorts them without changing their stable identities.</summary>
        private static void Refresh()
        {
            // Deleted assets disappear naturally after the next invalidation.
            if (!dirty)
                return;
            dirty = false;
            List<ObjectFlag> found = new List<ObjectFlag>();
            foreach (string guid in AssetDatabase.FindAssets("t:ObjectFlag"))
            {
                ObjectFlag flag = AssetDatabase.LoadAssetAtPath<ObjectFlag>(AssetDatabase.GUIDToAssetPath(guid));
                if (flag != null)
                    found.Add(flag);
            }
            found.Sort((first, second) => StringComparer.OrdinalIgnoreCase.Compare(first.DisplayName, second.DisplayName));
            flags = found.ToArray();
        }

        /// <summary>Finds a definition by display name for creation and one-time tag import.</summary>
        /// <param name="name">Requested display name.</param>
        /// <returns>The matching flag, or null when none exists.</returns>
        public static ObjectFlag Find(string name)
        {
            // Names are compared only by editor catalog operations; gameplay uses asset references.
            foreach (ObjectFlag flag in Flags)
                if (string.Equals(flag.DisplayName, name, StringComparison.OrdinalIgnoreCase))
                    return flag;
            return null;
        }

        #endregion

        #region Creation

        /// <summary>Creates one uniquely named object flag from an explicit authoring action.</summary>
        /// <param name="name">Display name entered in the creation form.</param>
        /// <param name="group">Optional selector group.</param>
        /// <param name="description">Short selection tooltip.</param>
        /// <returns>The new persistent asset.</returns>
        public static ObjectFlag Create(string name, string group, string description)
        {
            // Validate before allocating an asset or creating project folders.
            if (string.IsNullOrWhiteSpace(name) || Find(name.Trim()) != null)
                throw new InvalidOperationException("Choose a nonempty flag name that is not already in the catalog.");
            EnsureFolder(folder);
            string fileName = name.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(invalid, '_');
            ObjectFlag flag = ScriptableObject.CreateInstance<ObjectFlag>();
            flag.DisplayName = name.Trim();
            flag.Group = group ?? string.Empty;
            flag.Description = description ?? string.Empty;
            AssetDatabase.CreateAsset(flag, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + fileName + ".asset"));
            Undo.RegisterCreatedObjectUndo(flag, "Create object flag");
            AssetDatabase.SaveAssetIfDirty(flag);
            Invalidate();
            return flag;
        }

        /// <summary>Creates missing project folders for an explicitly requested flag asset.</summary>
        /// <param name="path">Project-relative folder path.</param>
        private static void EnsureFolder(string path)
        {
            // Existing directories and their metadata remain untouched.
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(parent.Length + 1));
        }

        /// <summary>Creates definitions for project tags that do not already have a same-named flag.</summary>
        /// <returns>The number of newly created definitions.</returns>
        public static int ImportProjectTags()
        {
            // Unity tags are read exclusively by this explicit editor import command.
            int created = 0;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create flags from Unity Tags");
            foreach (string name in UnityEditorInternal.InternalEditorUtility.tags)
                if (Find(name) == null)
                {
                    Create(name, "Imported", "Object identity flag imported from " + name + ".");
                    created++;
                }
            Undo.CollapseUndoOperations(group);
            return created;
        }

        #endregion

        #endregion
    }
}
