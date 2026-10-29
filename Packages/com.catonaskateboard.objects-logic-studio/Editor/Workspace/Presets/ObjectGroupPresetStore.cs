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
            if (preset == null || preset.Group.Key != group.Key || preset.Values.Length != group.Paths.Length)
                throw new InvalidOperationException("Choose a preset for this settings group.");
            using SerializedObject data = new SerializedObject(state);
            Transform root = state.Target.Resolve()?.transform.root;
            for (int index = 0; index < group.Paths.Length; index++)
                preset.Values[index].Apply(data.FindProperty(group.Paths[index]), root, resolve, allowMissing);
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

        #endregion

        #endregion
    }
}
