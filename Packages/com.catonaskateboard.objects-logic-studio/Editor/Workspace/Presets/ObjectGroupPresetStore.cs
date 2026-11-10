using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Captures and imports only the fields belonging to a selected settings group.</summary>
    internal static class ObjectGroupPresetStore
    {
        #region Methods

        #region Values

        /// <summary>Includes preview dependencies when checking for outside edits to a group preset.</summary>
        /// <param name="preset">Group asset and its optional shared preview context.</param>
        /// <returns>A complete editor conflict stamp.</returns>
        internal static string AssetStamp(ObjectGroupPreset preset)
        {
            return preset == null ? string.Empty : EditorJsonUtility.ToJson(preset) + (preset.Context != null ? EditorJsonUtility.ToJson(preset.Context) : string.Empty);
        }

        /// <summary>Captures a group's pending values without modifying its source object.</summary>
        /// <param name="state">Workspace or group document.</param>
        /// <param name="group">Exact group paths.</param>
        /// <param name="localRoot">Optional capture root for a whole-object master.</param>
        /// <returns>Portable serialized values in the same order as the group paths.</returns>
        internal static ObjectPresetValue[] Capture(ObjectWorkspace state, ObjectGroupInfo group, Transform localRoot = null)
        {
            using SerializedObject data = new SerializedObject(state);
            Transform root = localRoot != null ? localRoot : state.Target.Resolve()?.transform.root;
            return Array.ConvertAll(group.Paths, path => ObjectPresetValue.Capture(data.FindProperty(path), root));
        }

        /// <summary>Imports one group after checking its type and field paths.</summary>
        /// <param name="preset">Reusable group preset.</param>
        /// <param name="state">Destination draft.</param>
        /// <param name="group">Group opened in the destination.</param>
        /// <param name="resolve">Optional portable import-slot resolver.</param>
        /// <param name="allowMissing">Allows an isolated preview to collect missing links.</param>
        internal static void Import(ObjectGroupPreset preset, ObjectWorkspace state, ObjectGroupInfo group,
            Func<ObjectPresetLink, UnityEngine.Object> resolve = null, bool allowMissing = false)
        {
            if (preset == null || preset.Group.Key != group.Key || preset.Values.Length != preset.Group.Paths.Length)
                throw new InvalidOperationException("Choose a preset for this settings group.");
            using SerializedObject data = new SerializedObject(state);
            Transform root = state.Target.Resolve()?.transform.root;
            // Match by path so older presets can be imported after a group gains another option.
            for (int index = 0; index < preset.Group.Paths.Length; index++)
            {
                string path = preset.Group.Paths[index];
                if (Array.IndexOf(group.Paths, path) >= 0)
                    preset.Values[index].Apply(data.FindProperty(path), root, resolve, allowMissing);
            }
            data.ApplyModifiedProperties();
        }

        /// <summary>Returns a stable group-value stamp for conflict and preset comparison.</summary>
        /// <param name="state">Workspace or group document.</param>
        /// <param name="group">Group to compare.</param>
        /// <returns>The saved representation of the group's current values.</returns>
        internal static string Stamp(ObjectWorkspace state, ObjectGroupInfo group)
        {
            ObjectGroupPreset temporary = ScriptableObject.CreateInstance<ObjectGroupPreset>();
            try
            {
                temporary.Values = Capture(state, group);
                return EditorJsonUtility.ToJson(temporary);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        #endregion

        #region Assignments

        /// <summary>Finds group assignments for an exact interaction across saved workspace selections.</summary>
        /// <param name="feature">Prefab component or its open-stage counterpart.</param>
        /// <returns>The assignment-key prefix for this component.</returns>
        internal static string InteractionKey(ObjectInteraction feature)
        {
            ObjectInteraction persistent = ObjectFieldClipboard.Persistent(feature);
            ObjectWorkspaceTarget target = new ObjectWorkspaceTarget();
            target.Capture(persistent.gameObject, 0);
            return target.PrefabGuid + ":" + target.ObjectPath + ":" + ObjectWorkspaceTarget.FileId(persistent) + "|";
        }

        /// <summary>Finds the selected interaction's imported preset for this group.</summary>
        /// <param name="state">Persistent workspace.</param>
        /// <param name="group">Selected settings group.</param>
        /// <returns>The saved assignment or null for local settings.</returns>
        internal static ObjectGroupAssignment Assignment(ObjectWorkspace state, ObjectGroupInfo group)
        {
            string key = state.DraftKey + "|" + group.Key;
            return state.GroupPresets.Find(assignment => assignment.Key == key);
        }

        /// <summary>Remembers the preset source without changing the prefab's settings.</summary>
        /// <param name="state">Persistent workspace.</param>
        /// <param name="group">Selected group.</param>
        /// <param name="preset">Imported or newly saved preset.</param>
        internal static void Assign(ObjectWorkspace state, ObjectGroupInfo group, ObjectGroupPreset preset)
        {
            string key = state.DraftKey + "|" + group.Key;
            state.GroupPresets.RemoveAll(assignment => assignment.Key == key);
            if (preset != null)
                state.GroupPresets.Add(new ObjectGroupAssignment { Key = key, Preset = preset, Baseline = AssetStamp(preset) });
            state.Persist();
        }

        /// <summary>Shows the imported source without scanning assets during repaint.</summary>
        /// <param name="state">Persistent workspace.</param>
        /// <param name="group">Group shown in the overview.</param>
        /// <returns>A short source label.</returns>
        internal static string Summary(ObjectWorkspace state, ObjectGroupInfo group)
        {
            ObjectGroupAssignment assignment = Assignment(state, group);
            return assignment?.Preset != null ? assignment.Preset.name : "Local settings";
        }

        #endregion

        #region Assets

        /// <summary>Creates a separate reusable asset from the current group proposal.</summary>
        /// <param name="state">Group document supplying current values.</param>
        /// <param name="group">Selected group.</param>
        /// <returns>The saved preset or null when the file picker is cancelled.</returns>
        internal static ObjectGroupPreset SaveAs(ObjectWorkspace state, ObjectGroupInfo group)
        {
            string path = EditorUtility.SaveFilePanelInProject("Save group preset", group.Title, "asset", "Choose where to save this group.");
            if (string.IsNullOrEmpty(path))
                return null;
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            ObjectGroupPreset preset = ScriptableObject.CreateInstance<ObjectGroupPreset>();
            preset.Group = ObjectWorkspace.Copy(group);
            preset.Values = Capture(state, group);
            AssetDatabase.CreateAsset(preset, path);
            preset.Context = ObjectPresetContext.Create(state, preset);
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssetIfDirty(preset);
            return preset;
        }

        /// <summary>Writes the intended group values and preserves the existing preview context checks.</summary>
        /// <param name="preset">Preset receiving the values.</param>
        /// <param name="group">Expected settings group.</param>
        /// <param name="valuesSource">Document holding the values to save.</param>
        /// <param name="expected">Preset stamp captured before editing.</param>
        /// <param name="canSave">Whether local links were resolved on a sample.</param>
        /// <returns>The stamp of the newly saved preset.</returns>
        internal static string Write(ObjectGroupPreset preset, ObjectGroupInfo group, ObjectWorkspace valuesSource, string expected, bool canSave)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before applying the preset.");
            if (!canSave)
                throw new InvalidOperationException("Choose a sample interaction to resolve this preset's object links first.");
            if (preset == null || preset.Group.Key != group.Key)
                throw new InvalidOperationException("Choose a preset for this group.");
            if (AssetStamp(preset) != expected)
                throw new InvalidOperationException("This preset changed elsewhere. Discard to reload it.");
            ObjectPresetValue[] values = Capture(valuesSource, group);
            StudioAssetWrites.Save(preset, "Update group preset", () =>
            {
                preset.Group = ObjectWorkspace.Copy(group);
                preset.Values = values;
                ObjectPresetContext.Update(preset, valuesSource);
            });
            return AssetStamp(preset);
        }

        /// <summary>Saves a layout within a preset without applying the other window's pending values.</summary>
        /// <param name="preset">Preset receiving the selected layout.</param>
        /// <param name="group">Expected settings group.</param>
        /// <param name="document">Local settings copy that opened the layout.</param>
        /// <param name="proposal">Local copy including this preview’s edits.</param>
        /// <param name="paths">Exact layout fields to transfer.</param>
        /// <param name="fillThreshold">Optional Actual Item Fill threshold.</param>
        /// <param name="expected">Preset stamp captured before editing.</param>
        /// <param name="canSave">Whether local links were resolved on a sample.</param>
        /// <param name="baseline">Receives the applied group values for the settings window.</param>
        /// <returns>The stamp of the newly saved preset.</returns>
        internal static string WriteLayout(ObjectGroupPreset preset, ObjectGroupInfo group, ObjectWorkspace document, ObjectWorkspace proposal,
            string[] paths, int fillThreshold, string expected, bool canSave, out string baseline)
        {
            ObjectWorkspace saved = ScriptableObject.CreateInstance<ObjectWorkspace>();
            try
            {
                // Import the applied group before overlaying only the placements edited in this preview.
                ObjectWorkspaceDraft.Capture(document).Restore(saved);
                ObjectPresetContext.Apply(preset, saved);
                Import(preset, saved, group);
                ObjectSectionChanges.Synchronize(proposal, saved, paths, fillThreshold, updateBaseline: false);
                string stamp = Write(preset, group, saved, expected, canSave);
                baseline = Stamp(saved, group);
                ObjectSectionChanges.Synchronize(saved, document, paths, fillThreshold, updateBaseline: false);
                return stamp;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(saved);
            }
        }

        #endregion

        #endregion
    }
}
