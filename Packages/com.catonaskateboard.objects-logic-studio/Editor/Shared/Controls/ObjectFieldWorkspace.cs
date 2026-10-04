using System;
using System.Text.RegularExpressions;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Rebases only a bulk-updated field while preserving unrelated pending Apply/Discard proposals.</summary>
    internal static class ObjectFieldWorkspace
    {
        #region Methods
        #region Synchronization

        /// <summary>Updates the selected feature's baseline after its corresponding saved field changes.</summary>
        /// <param name="workspace">Recoverable object tool session.</param>
        /// <param name="target">Field just updated across prefabs.</param>
        internal static void Rebase(ObjectWorkspace workspace, ObjectFieldTarget target)
        {
            // Inactive sessions still retain baselines and must not report the batch's own edit as an outside conflict.
            Sync(workspace, SingleInteractionSession.Resolve(workspace.Target.Resolve(), workspace.Single.Kind), target, "Single");
            Sync(workspace, workspace.Extended.Resolve(workspace.Target.Resolve()), target, "Extended");
            Sync(workspace, ObjectWorkspaceSession.Resolve(workspace), target, "Hover");
            workspace.Persist();
        }

        /// <summary>Copies one incoming field into its baseline and into any unchanged draft value.</summary>
        /// <param name="workspace">Owner of the retained proposal.</param>
        /// <param name="source">Applied component for this session.</param>
        /// <param name="target">Batch field being reconciled.</param>
        /// <param name="category">Snapshot and session category.</param>
        private static void Sync(ObjectWorkspace workspace, ObjectInteraction source, ObjectFieldTarget target, string category)
        {
            // Missing or differently typed cards do not participate in this update.
            if (source == null || source.GetType() != target.Type)
                return;
            string field = DraftPath(source, target.Path);
            if (field == null)
                return;
            ObjectFieldSnapshot snapshot = ScriptableObject.CreateInstance<ObjectFieldSnapshot>();
            try
            {
                snapshot.Single = source is ObjectSingleInteraction single ? SingleInteractionDraft.Capture(single) : null;
                snapshot.Extended = source is ObjectExtendedInteraction extended ? ExtendedInteractionDraft.Capture(extended) : null;
                snapshot.Hover = source is ObjectHover hover ? ObjectWorkspace.Copy(hover.Configuration) : null;
                snapshot.Binding = source is ObjectHover binding ? HoverBindingDraft.Capture(binding) : null;
                bool hoverBinding = category == "Hover" && !target.Path.StartsWith("configuration.", StringComparison.Ordinal);
                string prefix = category == "Hover" ? hoverBinding ? "Binding." : "Draft." : category + ".Draft.";
                string baseline = category == "Hover" ? hoverBinding ? "OriginalBinding." : "Baseline." : category + ".Baseline.";
                using SerializedObject incoming = new SerializedObject(snapshot);
                using SerializedObject data = new SerializedObject(workspace);
                SerializedProperty current = incoming.FindProperty((hoverBinding ? "Binding" : category) + "." + field);
                SerializedProperty draft = data.FindProperty(prefix + field);
                SerializedProperty original = data.FindProperty(baseline + field);
                if (current == null || draft == null || original == null)
                    return;
                StudioPropertyValue value = new StudioPropertyValue(current);
                if (draft.contentHash == original.contentHash)
                    value.Apply(draft);
                value.Apply(original);
                data.ApplyModifiedProperties();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(snapshot);
            }
        }

        /// <summary>Maps runtime field names back to their retained draft representation.</summary>
        /// <param name="source">Interaction defining the settings block.</param>
        /// <param name="path">Runtime field path.</param>
        /// <returns>The relative draft field, or null when no draft represents it.</returns>
        private static string DraftPath(ObjectInteraction source, string path)
        {
            // Settings roots differ between generic cards and wrappers retaining component IDs.
            int split = path.IndexOf('.');
            string head = split < 0 ? path : path.Substring(0, split);
            string tail = split < 0 ? string.Empty : path.Substring(split);
            string mapped = head switch
            {
                "interactionName" => "Name", "m_Enabled" => "Enabled", "drawGizmos" => "DrawGizmos",
                "action" => source is ObjectSingleInteraction ? "Action" : "StartAction",
                "startAction" => "StartAction", "advanceAction" => "AdvanceAction", "toolRequirement" => "ToolRequirement",
                "flagChange" => "FlagChange", "visualEffect" => "VisualEffect", "physics" => "Release", "trajectory" => "Throw",
                "anchor" => "AnchorPath", "configuration" => string.Empty,
                "settings" => source switch
                {
                    ObjectSingleInteraction single => single.Kind.ToString(),
                    ObjectContactModifier => "Contact", ObjectAmbient => "Ambient", ObjectMakeOrder => "Orders.Settings",
                    ObjectInteractionUnlock => "Unlock.Settings", ObjectAssemblyProduct => "AssemblyProduct.Settings",
                    ObjectExtendedInteraction extended => extended.Kind.ToString(),
                    _ => null
                },
                _ => null
            };
            if (mapped == null)
                return null;
            string result = mapped.Length == 0 ? tail.TrimStart('.') : mapped + tail;
            result = result.Replace("Unlock.Settings.Target", "Unlock.TargetId").Replace("Unlock.Settings.Replacement", "Unlock.ReplacementId");
            result = Regex.Replace(result, @"Orders\.Settings\.Entries\.Array\.data\[(\d+)\]\.Source", "Orders.Sources.Array.data[$1]");
            result = Regex.Replace(result, @"Unlock\.Settings\.Conditions\.Array\.data\[(\d+)\]\.Source", "Unlock.SourceIds.Array.data[$1]");
            return Regex.Replace(result, @"AssemblyProduct\.Settings\.InteractionRules\.Array\.data\[(\d+)\]\.Target", "AssemblyProduct.TargetIds.Array.data[$1]");
        }

        #endregion
        #endregion
    }
}
