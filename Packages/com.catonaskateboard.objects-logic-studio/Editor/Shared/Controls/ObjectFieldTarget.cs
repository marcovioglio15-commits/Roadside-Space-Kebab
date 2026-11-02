using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Maps a clicked draft field to its corresponding serialized interaction field on prefabs.</summary>
    internal sealed class ObjectFieldTarget
    {
        #region State

        internal readonly Type Type;
        internal readonly UnityEngine.Object Preset;
        internal readonly ObjectInteraction Source;
        internal readonly string Path;
        internal readonly StudioPropertyValue Value;
        internal readonly ObjectFieldTarget[] Fields;

        #endregion

        #region Methods
        #region Mapping

        /// <summary>Captures a resolved interaction field for a delayed batch update.</summary>
        /// <param name="type">Exact interaction component type.</param>
        /// <param name="preset">Selected reusable asset, if assigned.</param>
        /// <param name="source">Source interaction used to remap local references.</param>
        /// <param name="path">Serialized component field path.</param>
        /// <param name="value">Clicked field's resolved typed value.</param>
        private ObjectFieldTarget(Type type, UnityEngine.Object preset, ObjectInteraction source, string path, StudioPropertyValue value)
        {
            // Batch callbacks do not retain a transient SerializedObject.
            Type = type;
            Preset = preset;
            Source = source;
            Path = path;
            Value = value;
            Fields = new[] { this };
        }

        /// <summary>Combines fields of the same interaction for atomic section updates.</summary>
        /// <param name="fields">Resolved fields sharing source, component type and preset.</param>
        private ObjectFieldTarget(ObjectFieldTarget[] fields)
        {
            Fields = fields;
            Type = fields[0].Type;
            Preset = fields[0].Preset;
            Source = fields[0].Source;
            Path = fields[0].Path;
            Value = fields[0].Value;
        }

        /// <summary>Resolves a complete section without allowing fields from different interactions.</summary>
        /// <param name="properties">Fields represented by the selected header.</param>
        /// <returns>A complete transfer or null when any field cannot be mapped.</returns>
        internal static ObjectFieldTarget Create(SerializedProperty[] properties)
        {
            if (properties.Length == 0)
                return null;
            List<ObjectFieldTarget> mapped = new List<ObjectFieldTarget>();
            foreach (SerializedProperty property in properties)
            {
                // Parallel editor identities are already resolved inside the corresponding settings block.
                if (property.name is "TargetIds" or "SourceIds")
                    continue;
                mapped.Add(Create(property));
            }
            ObjectFieldTarget[] fields = mapped.ToArray();
            if (fields.Length == 0)
                return null;
            foreach (ObjectFieldTarget field in fields)
                if (field == null || fields[0] == null || field.Type != fields[0].Type || field.Source != fields[0].Source || field.Preset != fields[0].Preset)
                    return null;
            return new ObjectFieldTarget(fields);
        }

        /// <summary>Maps object workspace proposals, native interaction inspectors and reusable presets.</summary>
        /// <param name="property">Clicked field.</param>
        /// <returns>A supported interaction field, or null for window navigation and unrelated settings.</returns>
        internal static ObjectFieldTarget Create(SerializedProperty property)
        {
            if (property == null)
                return null;
            // A workspace keeps several inactive proposals; the clicked prefix determines its actual owner.
            if (property.serializedObject.targetObject is ObjectWorkspace workspace)
                return FromWorkspace(workspace, property);
            if (property.serializedObject.targetObject is ObjectInteraction interaction)
                return new ObjectFieldTarget(interaction.GetType(), PresetOf(interaction, property.propertyPath), interaction,
                    property.propertyPath, new StudioPropertyValue(property));
            return null;
        }

        /// <summary>Resolves editor-only IDs before capturing values for a component on another prefab.</summary>
        /// <param name="workspace">Current object tool proposal.</param>
        /// <param name="property">Clicked field inside that proposal.</param>
        /// <returns>A runtime field mapping or null for an unrelated workspace field.</returns>
        private static ObjectFieldTarget FromWorkspace(ObjectWorkspace workspace, SerializedProperty property)
        {
            // Resolve component identity without changing the selected card or committing pending edits.
            string path = property.propertyPath;
            ObjectInteraction source;
            UnityEngine.Object preset;
            string snapshotPath;
            string runtimePath;
            if (path.StartsWith("Single.Draft.", StringComparison.Ordinal))
            {
                source = SingleInteractionSession.Resolve(workspace.Target.Resolve(), workspace.Single.Kind);
                preset = GroupSource(workspace, property.propertyPath);
                snapshotPath = "Single." + path.Substring(13);
                runtimePath = RuntimePath(path.Substring(13), source);
            }
            else if (path.StartsWith("Extended.Draft.", StringComparison.Ordinal))
            {
                source = workspace.Extended.Resolve(workspace.Target.Resolve());
                preset = GroupSource(workspace, property.propertyPath);
                snapshotPath = "Extended." + path.Substring(15);
                snapshotPath = ResolveIds(snapshotPath);
                runtimePath = RuntimePath(snapshotPath.Substring(9), source);
            }
            else if (path == "Draft" || path.StartsWith("Draft.", StringComparison.Ordinal) || path.StartsWith("Binding.", StringComparison.Ordinal))
            {
                source = ObjectWorkspaceSession.Resolve(workspace);
                preset = GroupSource(workspace, property.propertyPath);
                snapshotPath = path == "Draft" ? "Hover" : path.StartsWith("Draft.", StringComparison.Ordinal) ? "Hover." + path.Substring(6) : path;
                runtimePath = path == "Draft" ? "configuration" : path.StartsWith("Draft.", StringComparison.Ordinal) ? "configuration." + path.Substring(6) : RuntimePath(path.Substring(8), source);
                if (path == "Binding.AnchorPath")
                {
                    snapshotPath = "Anchor";
                    runtimePath = "anchor";
                }
            }
            else
                return null;
            if (source == null || string.IsNullOrEmpty(runtimePath))
                return null;
            ObjectFieldSnapshot snapshot = ScriptableObject.CreateInstance<ObjectFieldSnapshot>();
            try
            {
                // Local source links become actual components before the typed field snapshot is created.
                snapshot.hideFlags = HideFlags.HideAndDontSave;
                snapshot.Read(workspace, source);
                using SerializedObject data = new SerializedObject(snapshot);
                SerializedProperty field = data.FindProperty(snapshotPath);
                ObjectInteraction persistent = ObjectFieldClipboard.Persistent(source);
                return field != null ? new ObjectFieldTarget(source.GetType(), preset, persistent, runtimePath,
                    new StudioPropertyValue(field, reference => ObjectFieldBatch.MapReference(reference, source, persistent))) : null;
            }
            catch (InvalidOperationException exception)
            {
                // Unsaved local components cannot become durable references in a delayed transfer.
                Debug.LogWarning("This selection cannot be transferred yet: " + exception.Message);
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(snapshot);
            }
        }

        /// <summary>Converts stable-ID selector routes into their resolved reference fields.</summary>
        /// <param name="path">Path inside the detached snapshot.</param>
        /// <returns>The equivalent path containing runtime references.</returns>
        private static string ResolveIds(string path)
        {
            // Array indices remain unchanged; no neighbouring order or rule is substituted.
            if (path is "Extended.Unlock" or "Extended.AssemblyProduct")
                return path + ".Settings";
            path = path.Replace("Unlock.TargetId", "Unlock.Settings.Target").Replace("Unlock.ReplacementId", "Unlock.Settings.Replacement");
            path = path.Replace("OrdersCompletionId", "Orders.CompletionSource");
            path = Regex.Replace(path, @"Unlock\.SourceIds\.Array\.data\[(\d+)\]", "Unlock.Settings.Conditions.Array.data[$1].Source");
            return Regex.Replace(path, @"AssemblyProduct\.TargetIds\.Array\.data\[(\d+)\]", "AssemblyProduct.Settings.InteractionRules.Array.data[$1].Target");
        }

        /// <summary>Translates common binding names and settings roots to the component's serialization.</summary>
        /// <param name="path">Field path relative to one interaction draft.</param>
        /// <param name="source">Interaction controlling action-field naming.</param>
        /// <returns>The runtime field path.</returns>
        private static string RuntimePath(string path, ObjectInteraction source)
        {
            // Every mapping changes only the leading field; nested arrays retain their exact authored index.
            int split = path.IndexOf('.');
            string head = split < 0 ? path : path.Substring(0, split);
            string tail = split < 0 ? string.Empty : path.Substring(split);
            string mapped = head switch
            {
                "Name" => "interactionName",
                "Enabled" => "m_Enabled",
                "DrawGizmos" => "drawGizmos",
                "Action" => "action",
                "StartAction" => source is ObjectDialogue ? "startAction" : "action",
                "AdvanceAction" => "advanceAction",
                "ToolRequirement" => "toolRequirement",
                "FlagChange" => "flagChange",
                "VisualEffect" => "visualEffect",
                "Release" => "physics",
                "Throw" => "trajectory",
                _ => "settings"
            };
            if (head is "Unlock" or "AssemblyProduct" && tail.StartsWith(".Settings", StringComparison.Ordinal))
                tail = tail.Substring(9);
            return mapped + tail;
        }

        /// <summary>Finds a saved group source in the active object workspaces.</summary>
        /// <param name="interaction">Feature whose group assignment is requested.</param>
        /// <param name="path">Optional component field used to choose its specific settings group.</param>
        /// <param name="expected">Optional group identity to find among all assignments.</param>
        /// <returns>A matching group asset, or null for local settings.</returns>
        internal static UnityEngine.Object PresetOf(ObjectInteraction interaction, string path = null, UnityEngine.Object expected = null)
        {
            // Group assignments belong to editor workspaces, not to runtime component fields.
            if (InteractionRuntimeCatalog.KindOf(interaction) is not InteractionRuntimeKind kind)
                return null;
            string key = ObjectGroupPresetStore.InteractionKey(interaction);
            foreach (ObjectWorkspace workspace in Resources.FindObjectsOfTypeAll<ObjectWorkspace>())
                foreach (ObjectGroupAssignment assignment in workspace.GroupPresets)
                    if (assignment.Key.StartsWith(key, StringComparison.Ordinal) && assignment.Preset?.Group != null
                        && assignment.Preset.Group.TypeName == interaction.GetType().FullName && (expected == null || assignment.Preset == expected))
                        foreach (string field in assignment.Preset.Group.Paths)
                        {
                            string mapped = ObjectPresetPaths.Component(field, kind);
                            if (path == null || path == mapped || path.StartsWith(mapped + ".", StringComparison.Ordinal))
                                return assignment.Preset;
                        }
            return null;
        }

        /// <summary>Finds the specific group that owns a clicked workspace field.</summary>
        /// <param name="workspace">Current component proposal.</param>
        /// <param name="path">Clicked serialized field.</param>
        /// <returns>The group's source asset, or null for local settings.</returns>
        private static UnityEngine.Object GroupSource(ObjectWorkspace workspace, string path)
        {
            foreach (ObjectGroupAssignment assignment in workspace.GroupPresets)
                if (assignment.Key.StartsWith(workspace.DraftKey + "|", StringComparison.Ordinal) && assignment.Preset?.Group != null
                    && assignment.Preset.Group.Contains(path))
                    return assignment.Preset;
            return null;
        }
        #endregion
        #endregion
    }
}
