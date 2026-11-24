using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Preserves affected files and loaded owners for Apply recovery and editor Undo.</summary>
    internal sealed class ObjectFlagCommitSnapshot
    {
        #region Types

        /// <summary>Retains one file's exact bytes, including its metadata file.</summary>
        private sealed class FileState
        {
            internal readonly string Path;
            internal readonly byte[] Bytes;

            #region Methods

            /// <summary>Captures file content or records that the file does not exist yet.</summary>
            /// <param name="path">Workspace-relative file path.</param>
            internal FileState(string path)
            {
                Path = path;
                Bytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
            }

            #endregion
        }

        /// <summary>Retains loaded state that may contain unsaved changes outside the asset file.</summary>
        private sealed class OwnerState
        {
            internal readonly string Path;
            internal readonly string Id;
            internal readonly string Json;
            internal readonly bool Dirty;
            internal readonly UnityEngine.Object LiveOwner;
            internal readonly bool StageDirty;
            internal readonly List<SelectionState> Selections = new List<SelectionState>();

            #region Methods

            /// <summary>Captures one loaded owner independently of its disk state.</summary>
            /// <param name="path">Containing asset or scene file.</param>
            /// <param name="owner">Loaded owner being saved.</param>
            /// <param name="live">Whether this owner is the separate open prefab-stage copy.</param>
            internal OwnerState(string path, UnityEngine.Object owner, bool live = false)
            {
                Path = path;
                Id = ObjectFlagAssetAccess.Id(owner);
                Json = EditorJsonUtility.ToJson(owner);
                Dirty = EditorUtility.IsDirty(owner);
                LiveOwner = live ? owner : null;
                StageDirty = live && owner is Component component && component.gameObject.scene.isDirty;
            }

            #endregion
        }

        /// <summary>Retains stable flag identities when a deleted definition is restored with a new native instance.</summary>
        private sealed class SelectionState
        {
            internal readonly ObjectFlagField Field;
            internal readonly string[] References;
            internal readonly EntityId[] Identifiers;

            #region Methods

            /// <summary>Captures valid references while leaving unresolved slots to the original serialized JSON.</summary>
            /// <param name="field">Controlled flag selection.</param>
            /// <param name="property">Current serialized selection.</param>
            internal SelectionState(ObjectFlagField field, SerializedProperty property)
            {
                Field = field;
                References = ObjectFlagStorage.Read(property, field.Portable).Select(flag => flag != null
                    ? GlobalObjectId.GetGlobalObjectIdSlow(flag).ToString() : string.Empty).ToArray();
                Identifiers = new EntityId[References.Length];
                for (int index = 0; index < Identifiers.Length; index++)
                    Identifiers[index] = ObjectFlagStorage.Reference(property, field.Portable, index)?.objectReferenceEntityIdValue ?? default;
            }

            #endregion
        }

        #endregion
        #region State

        private readonly List<FileState> files = new List<FileState>();
        private readonly List<OwnerState> owners = new List<OwnerState>();
        private readonly HashSet<string> selectionPrefabs = new HashSet<string>();

        #endregion
        #region Methods
        #region Capture

        /// <summary>Captures affected files and any corresponding persistent or already loaded scene owners.</summary>
        /// <param name="paths">Affected asset paths, including planned new files.</param>
        /// <param name="fields">Controlled flag selections.</param>
        /// <returns>A recovery snapshot that leaves closed scenes closed.</returns>
        internal static ObjectFlagCommitSnapshot Capture(IEnumerable<string> paths, ObjectFlagField[] fields)
        {
            ObjectFlagCommitSnapshot snapshot = new ObjectFlagCommitSnapshot();
            foreach (string path in paths.Distinct())
            {
                snapshot.files.Add(new FileState(path));
                snapshot.files.Add(new FileState(path + ".meta"));
                ObjectFlagField[] selections = fields.Where(field => field.AssetPath == path).ToArray();
                if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) && selections.Length > 0
                    && selections.All(field => !field.Portable && field.Missing == 0
                        && (!field.Multiple || field.Flags.All(flag => flag != null)))
                    && !AssetDatabase.LoadAllAssetsAtPath(path).Any(asset => asset is ObjectFlag))
                    snapshot.selectionPrefabs.Add(path);
                Scene scene = SceneManager.GetSceneByPath(path);
                if (!File.Exists(path) || path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) && (!scene.IsValid() || !scene.isLoaded))
                    continue;
                ObjectFlagAssetAccess.Visit(path, owner =>
                {
                    OwnerState state = new OwnerState(path, owner);
                    using SerializedObject data = new SerializedObject(owner);
                    foreach (ObjectFlagField field in fields.Where(field => field.AssetPath == path && field.OwnerId == state.Id))
                    {
                        SerializedProperty property = data.FindProperty(field.PropertyPath);
                        if (property != null)
                            state.Selections.Add(new SelectionState(field, property));
                    }
                    snapshot.owners.Add(state);
                    ObjectFlagField[] controlled = fields.Where(field => field.AssetPath == path && field.OwnerId == state.Id).ToArray();
                    if (controlled.Length > 0 && ObjectFlagPrefabStage.Resolve(owner, path) is Component mirror)
                    {
                        OwnerState live = new OwnerState(path, mirror, true);
                        using SerializedObject stage = new SerializedObject(mirror);
                        foreach (ObjectFlagField field in controlled)
                            if (stage.FindProperty(field.PropertyPath) is SerializedProperty property)
                                live.Selections.Add(new SelectionState(field, property));
                        snapshot.owners.Add(live);
                    }
                    return false;
                });
            }
            return snapshot;
        }

        /// <summary>Detects outside disk changes before an Undo could overwrite them.</summary>
        /// <returns>True when affected files still match the expected applied state.</returns>
        internal bool MatchesFiles()
        {
            return files.All(state => selectionPrefabs.Contains(state.Path) ? File.Exists(state.Path)
                : state.Bytes == null ? !File.Exists(state.Path)
                    : File.Exists(state.Path) && File.ReadAllBytes(state.Path).AsSpan().SequenceEqual(state.Bytes))
                && selectionPrefabs.All(MatchesSelections);
        }

        /// <summary>Accepts unrelated prefab edits while protecting the exact flag fields controlled by this history step.</summary>
        /// <param name="path">Prefab whose file can be reserialized by native Prefab Mode.</param>
        /// <returns>True when every controlled owner and flag selection still matches.</returns>
        private bool MatchesSelections(string path)
        {
            Dictionary<string, OwnerState> expected = owners.Where(state => state.Path == path
                && ReferenceEquals(state.LiveOwner, null) && state.Selections.Count > 0).ToDictionary(state => state.Id);
            int resolved = 0;
            bool matches = true;
            ObjectFlagAssetAccess.Visit(path, owner =>
            {
                if (!expected.TryGetValue(ObjectFlagAssetAccess.Id(owner), out OwnerState state))
                    return false;
                using SerializedObject data = new SerializedObject(owner);
                foreach (SelectionState selection in state.Selections)
                {
                    SerializedProperty property = data.FindProperty(selection.Field.PropertyPath);
                    if (property == null || ObjectFlagLabels.Path(property) != selection.Field.Label)
                    {
                        matches = false;
                        continue;
                    }
                    List<ObjectFlag> values = ObjectFlagStorage.Read(property, false);
                    if (values.Count != selection.References.Length)
                    {
                        matches = false;
                        continue;
                    }
                    for (int index = 0; index < values.Count; index++)
                        matches &= values[index] != null
                            ? GlobalObjectId.GetGlobalObjectIdSlow(values[index]).ToString() == selection.References[index]
                            : selection.References[index].Length == 0
                                && ObjectFlagStorage.Reference(property, false, index).objectReferenceEntityIdValue == selection.Identifiers[index];
                }
                resolved++;
                return false;
            });
            return matches && resolved == expected.Count;
        }

        #endregion
        #region Restore

        /// <summary>Restores files before reconnecting loaded owners to their stable asset references.</summary>
        internal void Restore()
        {
            HashSet<string> imported = new HashSet<string>();
            // Metadata is restored with its file so removed definitions keep their GUIDs.
            foreach (FileState state in files)
                if (selectionPrefabs.Contains(state.Path))
                    continue;
                else if (state.Bytes == null)
                {
                    if (File.Exists(state.Path))
                        File.Delete(state.Path);
                    imported.Add(state.Path.EndsWith(".meta", StringComparison.Ordinal) ? state.Path.Substring(0, state.Path.Length - 5) : state.Path);
                }
                else if (!File.Exists(state.Path) || !File.ReadAllBytes(state.Path).AsSpan().SequenceEqual(state.Bytes))
                {
                    File.WriteAllBytes(state.Path, state.Bytes);
                    imported.Add(state.Path.EndsWith(".meta", StringComparison.Ordinal) ? state.Path.Substring(0, state.Path.Length - 5) : state.Path);
                }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in imported.Where(File.Exists))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            foreach (IGrouping<string, OwnerState> asset in owners.Where(state => ReferenceEquals(state.LiveOwner, null)).GroupBy(state => state.Path))
            {
                Scene scene = SceneManager.GetSceneByPath(asset.Key);
                if (asset.Key.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) && (!scene.IsValid() || !scene.isLoaded))
                    continue;
                ObjectFlagAssetAccess.Visit(asset.Key, owner =>
                {
                    OwnerState state = asset.FirstOrDefault(candidate => candidate.Id == ObjectFlagAssetAccess.Id(owner));
                    if (state == null)
                        return false;
                    if (selectionPrefabs.Contains(asset.Key))
                        return RestoreSelections(state, owner);
                    // Import already restores clean prefab owners and retains unresolved GUID metadata.
                    if (!state.Dirty && asset.Key.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                        return false;
                    RestoreOwner(state, owner);
                    return false;
                }, selectionPrefabs.Contains(asset.Key));
            }
            foreach (OwnerState state in owners.Where(state => state.LiveOwner != null))
                if (selectionPrefabs.Contains(state.Path))
                {
                    RestoreSelections(state, state.LiveOwner);
                    RestoreStageState(state, (Component)state.LiveOwner);
                }
                else
                    RestoreOwner(state, state.LiveOwner);
            ObjectFlagCatalog.Invalidate();
            CatOnASkateboard.StudioColors.Editor.StudioSourceChanges.Notify();
        }

        /// <summary>Restores native flag fields without overwriting other prefab settings or open-stage edits.</summary>
        /// <param name="state">Selections recorded for this owner.</param>
        /// <param name="owner">Persistent or open-stage destination.</param>
        /// <returns>True when controlled serialized fields changed.</returns>
        private static bool RestoreSelections(OwnerState state, UnityEngine.Object owner)
        {
            if (state.Selections.Count == 0)
                return false;
            using SerializedObject data = new SerializedObject(owner);
            foreach (SelectionState selection in state.Selections)
            {
                SerializedProperty property = data.FindProperty(selection.Field.PropertyPath);
                if (property == null)
                    throw new InvalidOperationException("An Undo flag field no longer exists: " + selection.Field.Location);
                ObjectFlagStorage.Write(property, false, selection.References.Select(reference =>
                    GlobalObjectId.TryParse(reference, out GlobalObjectId identity)
                        ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(identity) as ObjectFlag : null).ToArray());
                for (int index = 0; index < selection.References.Length; index++)
                    if (selection.References[index].Length == 0)
                        ObjectFlagStorage.Reference(property, false, index).objectReferenceEntityIdValue = selection.Identifiers[index];
            }
            return data.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Restores one persistent or stage owner and reconnects valid flags after definition recreation.</summary>
        /// <param name="state">Captured native state and stable selections.</param>
        /// <param name="owner">Still-loaded destination.</param>
        private static void RestoreOwner(OwnerState state, UnityEngine.Object owner)
        {
            EditorJsonUtility.FromJsonOverwrite(state.Json, owner);
            using SerializedObject data = new SerializedObject(owner);
            foreach (SelectionState selection in state.Selections)
            {
                SerializedProperty property = data.FindProperty(selection.Field.PropertyPath);
                if (property == null)
                    continue;
                for (int index = 0; index < selection.References.Length; index++)
                    if (GlobalObjectId.TryParse(selection.References[index], out GlobalObjectId identity))
                        ObjectFlagStorage.Reference(property, selection.Field.Portable, index).objectReferenceValue
                            = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(identity);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            if (owner is Component component && !EditorUtility.IsPersistent(owner))
                RestoreStageState(state, component);
            else if (state.Dirty)
                EditorUtility.SetDirty(owner);
            else
                EditorUtility.ClearDirty(owner);
        }

        /// <summary>Preserves a stage's prior save state after restoring its controlled membership.</summary>
        /// <param name="state">Original owner and scene state.</param>
        /// <param name="owner">Loaded scene or stage component.</param>
        private static void RestoreStageState(OwnerState state, Component owner)
        {
            if (!ReferenceEquals(state.LiveOwner, null) && !state.StageDirty
                && PrefabStageUtility.GetPrefabStage(owner.gameObject) is PrefabStage stage)
                stage.ClearDirtiness();
            else
                EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
        }

        #endregion
        #endregion
    }
}
