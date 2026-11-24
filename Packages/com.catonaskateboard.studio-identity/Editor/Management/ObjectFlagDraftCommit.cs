using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Validates and commits a complete proposal with file recovery and one Undo history step.</summary>
    internal static class ObjectFlagDraftCommit
    {
        #region Methods
        #region Apply

        /// <summary>Applies every changed tab together and retains the draft if validation or saving fails.</summary>
        /// <param name="session">Complete pending proposal.</param>
        /// <param name="warning">Receives a conflict or save diagnostic.</param>
        /// <returns>True after the whole proposal has been saved.</returns>
        internal static bool Apply(ObjectFlagDraftSession session, out string warning)
        {
            warning = string.Empty;
            if (!session.HasChanges)
                return true;
            ObjectFlagCommitSnapshot before = null;
            try
            {
                Validate(session);
                Dictionary<ObjectFlag, string> paths = NewPaths(session);
                string[] affected = AffectedPaths(session).Concat(paths.Values).Distinct().ToArray();
                ObjectFlagField[] fields = session.Selections.Where(entry => entry.Changed).Select(entry => entry.Field).ToArray();
                before = ObjectFlagCommitSnapshot.Capture(affected, fields);
                Dictionary<ObjectFlag, ObjectFlag> created = CreateDefinitions(session, paths);
                WriteDefinitions(session);
                WriteSelections(session, created);
                foreach (ObjectFlagGroupDraft group in session.Groups.Where(entry => entry.Changed))
                {
                    group.Source.Groups = new List<string>(group.Values);
                    EditorUtility.SetDirty(group.Source);
                    ObjectFlagAssetAccess.Save(group.Source);
                }
                DeleteDefinitions(session);
                ObjectFlagCommitSnapshot after = ObjectFlagCommitSnapshot.Capture(affected, fields);
                ObjectFlagCommitHistory.Record(before, after);
                session.Reset();
                ObjectFlagCatalog.Invalidate();
                CatOnASkateboard.StudioColors.Editor.StudioSourceChanges.Notify();
                return true;
            }
            catch (Exception exception)
            {
                warning = "Apply failed. The draft is retained: " + exception.Message;
                if (before != null)
                    try
                    {
                        before.Restore();
                    }
                    catch (Exception recovery)
                    {
                        warning += " Recovery failed: " + recovery.Message;
                    }
                return false;
            }
        }

        /// <summary>Checks uniqueness, write access and exact source baselines before changing project data.</summary>
        /// <param name="session">Pending proposal.</param>
        private static void Validate(ObjectFlagDraftSession session)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before applying flag changes.");
            // Definitions and fields use separate baselines so unrelated navigation remains editable.
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectFlag flag in session.Flags)
                if (!session.Definition(flag).TryValidate(out string issue) || !names.Add(session.Definition(flag).DisplayName))
                    throw new InvalidOperationException(issue.Length > 0 ? issue : "Flag names must be unique.");
            foreach (ObjectFlagDefinitionDraft entry in session.Definitions.Where(entry => entry.Changed && !entry.Created))
                if (entry.Source == null || JsonUtility.ToJson(entry.Source) != entry.Baseline)
                    throw new InvalidOperationException("A flag changed outside this tool. Discard to reload its current values.");
            foreach (ObjectFlagGroupDraft entry in session.Groups.Where(entry => entry.Changed))
                if (entry.Source == null || !entry.Source.Groups.SequenceEqual(entry.Baseline))
                    throw new InvalidOperationException("Group definitions changed outside this tool. Discard to reload them.");
            foreach (string path in AffectedPaths(session))
                if (!ObjectFlagAssetAccess.Writable(path))
                    throw new InvalidOperationException("This asset is read-only: " + path);
            foreach (ObjectFlagSelectionDraft entry in session.Selections.Where(entry => entry.Changed))
                if (!entry.Field.Visit((owner, property) =>
                {
                    if (property.contentHash.ToString() != entry.Baseline || ObjectFlagLabels.Path(property, entry.Field.Portable) != entry.Field.Label)
                        throw new InvalidOperationException("A flag selection changed outside this tool: " + entry.Field.Location);
                    ObjectFlagPrefabStage.Validate(entry.Field, property);
                    return false;
                }))
                    throw new InvalidOperationException("An edited field no longer exists: " + entry.Field.Location);
            HashSet<ObjectFlag> deleted = new HashSet<ObjectFlag>(session.Definitions.Where(entry => entry.Deleted && !entry.Created).Select(entry => entry.Source));
            if (deleted.Count > 0)
            {
                ObjectFlagScan scan = ObjectFlagProject.Scan(true, true, false);
                if (!scan.Complete || scan.Fields.Any(field => session.Values(field).Any(deleted.Contains)))
                    throw new InvalidOperationException("Flag uses changed after deletion was staged. Refresh and review the deletion again.");
            }
            foreach (ObjectFlag flag in deleted)
                if (AssetDatabase.IsMainAsset(flag) && AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(flag))
                    .Any(asset => asset != null && (asset is not ObjectFlag definition || !deleted.Contains(definition))))
                    throw new InvalidOperationException("The flag file contains unselected subassets: " + AssetDatabase.GetAssetPath(flag));
        }

        /// <summary>Lists existing files affected by changed records.</summary>
        /// <param name="session">Pending proposal.</param>
        /// <returns>Distinct project-relative paths.</returns>
        private static IEnumerable<string> AffectedPaths(ObjectFlagDraftSession session)
        {
            return session.Definitions.Where(entry => entry.Changed && !entry.Created).Select(entry => AssetDatabase.GetAssetPath(entry.Source))
                .Concat(session.Selections.Where(entry => entry.Changed).Select(entry => entry.Field.AssetPath))
                .Concat(session.Groups.Where(entry => entry.Changed).Select(entry => AssetDatabase.GetAssetPath(entry.Source))).Distinct();
        }

        /// <summary>Reserves unique paths for pending flags before taking the recovery snapshot.</summary>
        /// <param name="session">Pending proposal.</param>
        /// <returns>Paths indexed by temporary flag identity.</returns>
        private static Dictionary<ObjectFlag, string> NewPaths(ObjectFlagDraftSession session)
        {
            Dictionary<ObjectFlag, string> paths = new Dictionary<ObjectFlag, string>();
            HashSet<string> reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectFlagDefinitionDraft entry in session.Definitions.Where(entry => entry.Created && !entry.Deleted))
            {
                string path = ObjectFlagCatalog.NewAssetPath(entry.Working.DisplayName);
                string candidate = path;
                for (int suffix = 2; !reserved.Add(candidate); suffix++)
                    candidate = AssetDatabase.GenerateUniqueAssetPath(Path.GetDirectoryName(path).Replace('\\', '/') + "/"
                        + Path.GetFileNameWithoutExtension(path) + " " + suffix + ".asset");
                paths.Add(entry.Identity, candidate);
            }
            return paths;
        }

        #endregion
        #region Writing

        /// <summary>Creates persistent copies while keeping the pending originals available for recovery.</summary>
        /// <param name="session">Pending proposal.</param>
        /// <param name="paths">Reserved paths.</param>
        /// <returns>Persistent replacements for temporary flag references.</returns>
        private static Dictionary<ObjectFlag, ObjectFlag> CreateDefinitions(ObjectFlagDraftSession session, Dictionary<ObjectFlag, string> paths)
        {
            Dictionary<ObjectFlag, ObjectFlag> created = new Dictionary<ObjectFlag, ObjectFlag>();
            foreach (ObjectFlagDefinitionDraft entry in session.Definitions.Where(entry => entry.Created && !entry.Deleted))
            {
                ObjectFlag flag = UnityEngine.Object.Instantiate(entry.Working);
                flag.hideFlags = HideFlags.None;
                flag.name = Path.GetFileNameWithoutExtension(paths[entry.Identity]);
                AssetDatabase.CreateAsset(flag, paths[entry.Identity]);
                ObjectFlagAssetAccess.Save(flag);
                created.Add(entry.Identity, flag);
            }
            return created;
        }

        /// <summary>Copies only flag metadata, preserving asset ownership and stable references.</summary>
        /// <param name="session">Pending proposal.</param>
        private static void WriteDefinitions(ObjectFlagDraftSession session)
        {
            foreach (ObjectFlagDefinitionDraft entry in session.Definitions.Where(entry => entry.Changed && !entry.Created && !entry.Deleted))
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(entry.Working), entry.Source);
                EditorUtility.SetDirty(entry.Source);
                ObjectFlagAssetAccess.Save(entry.Source);
            }
        }

        /// <summary>Writes affected owners once per asset and maps newly created flags to persistent references.</summary>
        /// <param name="session">Pending proposal.</param>
        /// <param name="created">Persistent replacements for temporary definitions.</param>
        private static void WriteSelections(ObjectFlagDraftSession session, Dictionary<ObjectFlag, ObjectFlag> created)
        {
            foreach (IGrouping<string, ObjectFlagSelectionDraft> asset in session.Selections.Where(entry => entry.Changed).GroupBy(entry => entry.Field.AssetPath))
            {
                Dictionary<string, ObjectFlagSelectionDraft[]> owners = asset.GroupBy(entry => entry.Field.OwnerId).ToDictionary(group => group.Key, group => group.ToArray());
                int resolved = 0;
                ObjectFlagAssetAccess.Visit(asset.Key, owner =>
                {
                    if (!owners.TryGetValue(ObjectFlagAssetAccess.Id(owner), out ObjectFlagSelectionDraft[] entries))
                        return false;
                    using SerializedObject data = new SerializedObject(owner);
                    foreach (ObjectFlagSelectionDraft entry in entries)
                    {
                        SerializedProperty property = data.FindProperty(entry.Field.PropertyPath);
                        if (property == null)
                            throw new InvalidOperationException("An edited field no longer exists: " + entry.Field.Location);
                        ObjectFlag[] values = entry.Values.Select(flag => flag != null && created.TryGetValue(flag, out ObjectFlag replacement) ? replacement : flag).ToArray();
                        ObjectFlagStorage.Write(property, entry.Field.Portable, values);
                        ObjectFlagPrefabStage.Write(entry.Field, owner, values);
                        resolved++;
                    }
                    return data.ApplyModifiedPropertiesWithoutUndo();
                }, true);
                if (resolved != asset.Count())
                    throw new InvalidOperationException("An edited owner no longer exists: " + asset.Key);
            }
        }

        /// <summary>Removes definitions only after every staged reference update has been saved.</summary>
        /// <param name="session">Pending proposal.</param>
        private static void DeleteDefinitions(ObjectFlagDraftSession session)
        {
            foreach (IGrouping<string, ObjectFlagDefinitionDraft> asset in session.Definitions.Where(entry => entry.Deleted && !entry.Created)
                .GroupBy(entry => AssetDatabase.GetAssetPath(entry.Source)))
                if (asset.Any(entry => AssetDatabase.IsMainAsset(entry.Source)))
                {
                    if (!AssetDatabase.MoveAssetToTrash(asset.Key))
                        throw new IOException("Could not remove flag file: " + asset.Key);
                }
                else
                {
                    foreach (ObjectFlagDefinitionDraft entry in asset)
                    {
                        AssetDatabase.RemoveObjectFromAsset(entry.Source);
                        UnityEngine.Object.DestroyImmediate(entry.Source, true);
                    }
                    UnityEngine.Object main = AssetDatabase.LoadMainAssetAtPath(asset.Key);
                    EditorUtility.SetDirty(main);
                    ObjectFlagAssetAccess.Save(main);
                }
        }

        #endregion
        #endregion
    }
}
