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
                return new ObjectFieldTarget(interaction.GetType(), PresetOf(interaction), interaction,
                    property.propertyPath, new StudioPropertyValue(property));
            Type type = property.serializedObject.targetObject switch
            {
                HoverPreset => typeof(ObjectHover),
                SingleInteractionPreset single => SingleType(single.Kind),
                ExtendedInteractionPreset extended => ExtendedType(extended.Kind),
                _ => null
            };
            if (type == null)
                return null;
            string path = property.propertyPath;
            if (path.StartsWith("Settings", StringComparison.Ordinal))
                path = (type == typeof(ObjectDrop) || type == typeof(ObjectThrow) ? "physics" : "settings") + path.Substring(8);
            else if (path.StartsWith("Trajectory", StringComparison.Ordinal))
                path = "trajectory" + path.Substring(10);
            else if (path.StartsWith("ToolRequirement", StringComparison.Ordinal))
                path = "toolRequirement" + path.Substring(15);
            else if (!path.StartsWith("configuration", StringComparison.Ordinal))
                return null;
            return new ObjectFieldTarget(type, property.serializedObject.targetObject, null, path, new StudioPropertyValue(property));
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
                preset = workspace.Single.Preset;
                snapshotPath = "Single." + path.Substring(13);
                runtimePath = RuntimePath(path.Substring(13), source);
            }
            else if (path.StartsWith("Extended.Draft.", StringComparison.Ordinal))
            {
                source = workspace.Extended.Resolve(workspace.Target.Resolve());
                preset = workspace.Extended.Preset;
                snapshotPath = "Extended." + path.Substring(15);
                snapshotPath = ResolveIds(snapshotPath);
                runtimePath = RuntimePath(snapshotPath.Substring(9), source);
            }
            else if (path == "Draft" || path.StartsWith("Draft.", StringComparison.Ordinal) || path.StartsWith("Binding.", StringComparison.Ordinal))
            {
                source = ObjectWorkspaceSession.Resolve(workspace);
                preset = workspace.Source;
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

        /// <summary>Reads the preset assigned to a component without runtime reflection.</summary>
        /// <param name="interaction">Feature being compared.</param>
        /// <returns>Its currently assigned preset, or null.</returns>
        internal static UnityEngine.Object PresetOf(ObjectInteraction interaction)
        {
            // Hover keeps its own preset field; other categories share their base-class binding.
            return interaction switch
            {
                ObjectHover hover => hover.Preset,
                ObjectSingleInteraction single => single.SettingsPreset,
                ObjectExtendedInteraction extended => extended.SettingsPreset,
                _ => null
            };
        }

        /// <summary>Resolves a reusable Single preset's exact component type.</summary>
        /// <param name="kind">Preset category.</param>
        /// <returns>The matching component type.</returns>
        private static Type SingleType(SingleInteractionKind kind)
        {
            // Derived release types remain distinct during mass updates.
            return kind switch
            {
                SingleInteractionKind.Grab => typeof(ObjectGrab),
                SingleInteractionKind.Drop => typeof(ObjectDrop),
                SingleInteractionKind.Throw => typeof(ObjectThrow),
                SingleInteractionKind.Container => typeof(ObjectContainer),
                SingleInteractionKind.Dispenser => typeof(ObjectDispenser),
                SingleInteractionKind.Eject => typeof(ObjectEject),
                SingleInteractionKind.TriggerAnimation => typeof(ObjectTriggerAnimation),
                _ => null
            };
        }

        /// <summary>Resolves a reusable extended preset's component type.</summary>
        /// <param name="kind">Preset category.</param>
        /// <returns>The matching component type.</returns>
        private static Type ExtendedType(ExtendedInteractionKind kind)
        {
            // No name-based type discovery is required by the editor mapping.
            return kind switch
            {
                ExtendedInteractionKind.ModifyByContact => typeof(ObjectContactModifier),
                ExtendedInteractionKind.Dialogue => typeof(ObjectDialogue),
                ExtendedInteractionKind.Outline => typeof(ObjectOutline),
                ExtendedInteractionKind.Unlock => typeof(ObjectInteractionUnlock),
                ExtendedInteractionKind.AssemblyStation => typeof(ObjectAssemblyStation),
                ExtendedInteractionKind.AssemblyProduct => typeof(ObjectAssemblyProduct),
                ExtendedInteractionKind.SpawnManagement => typeof(ObjectSpawnManager),
                ExtendedInteractionKind.Slice => typeof(ObjectSlice),
                ExtendedInteractionKind.PlayAmbient => typeof(ObjectAmbient),
                ExtendedInteractionKind.AvailableOrders => typeof(ObjectAvailableOrders),
                ExtendedInteractionKind.ObjectDegradation => typeof(ObjectDegradation),
                ExtendedInteractionKind.GravityGenerator => typeof(ObjectGravityGenerator),
                _ => null
            };
        }

        #endregion
        #endregion
    }
}
