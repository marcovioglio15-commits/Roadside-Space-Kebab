using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Owns source selection, detached proposals, conflict checks and the common Apply/Discard transaction.</summary>
    internal static class ObjectWorkspaceSession
    {
        #region Methods

        #region Selection

        /// <summary>Opens an object's hover without replacing another unfinished draft.</summary>
        /// <param name="state">Durable workspace.</param>
        /// <param name="target">Object selected in a prefab stage or asset.</param>
        /// <param name="index">Hover index on the selected object.</param>
        /// <param name="warning">Receives an unfinished-session warning.</param>
        /// <returns>True when the selection was accepted.</returns>
        internal static bool Select(ObjectWorkspace state, GameObject target, int index, out string warning)
        {
            // Selection is explicit and cannot silently discard a preset, binding or observer proposal.
            warning = string.Empty;
            if (state.HasChanges)
            {
                warning = "Apply or Discard before changing objects or interactions.";
                return false;
            }
            if (target != null && !ObjectAuthoringSave.TryValidate(target, out warning))
                return false;
            state.Target.Capture(target, index);
            state.Prefab = target != null
                ? AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(state.Target.PrefabGuid)) : null;
            state.Single.Read(target);
            state.Extended.ComponentId = 0;
            state.Extended.Read(target);
            ObjectHover hover = Resolve(state);
            state.HasBinding = hover != null;
            state.OriginalPreset = hover != null ? hover.Preset : null;
            state.Binding = HoverBindingDraft.Capture(hover);
            state.OriginalBinding = ObjectWorkspace.Copy(state.Binding);
            state.Hierarchy = hover != null ? HoverHierarchy.Signature(hover.transform) : string.Empty;
            LoadPreset(state, state.OriginalPreset);
            if (hover != null)
                state.Draft = ObjectWorkspace.Copy(hover.Configuration);
            state.Baseline = ObjectWorkspace.Copy(state.Draft);
            state.Persist();
            return true;
        }

        /// <summary>Opens saved preset values while retaining the original component assignment until Apply.</summary>
        /// <param name="state">Workspace receiving the detached configuration.</param>
        /// <param name="preset">Saved configuration selected or created by the user.</param>
        internal static void LoadPreset(ObjectWorkspace state, HoverPreset preset)
        {
            // The caller guards pending changes before redirecting the source.
            state.Source = preset;
            state.Draft = preset != null ? ObjectWorkspace.Copy(preset.Configuration) : new HoverConfiguration();
            state.SourceBaseline = InteractionPresetWrites.Capture(preset);
        }

        /// <summary>Resolves the selected component against the currently available object.</summary>
        /// <param name="state">Workspace containing its durable route.</param>
        /// <returns>The selected hover or null if its object or index is unavailable.</returns>
        internal static ObjectHover Resolve(ObjectWorkspace state)
        {
            // A missing interaction remains missing rather than falling back to the first component.
            GameObject target = state.Target.Resolve();
            ObjectHover[] hovers = target != null ? target.GetComponents<ObjectHover>() : Array.Empty<ObjectHover>();
            return state.Target.InteractionIndex >= 0 && state.Target.InteractionIndex < hovers.Length
                ? hovers[state.Target.InteractionIndex] : null;
        }

        /// <summary>Reloads current applied sources and abandons only the workspace's proposals.</summary>
        /// <param name="state">Workspace whose source objects remain untouched.</param>
        internal static void Discard(ObjectWorkspace state)
        {
            // A missing target retains its identity so reopening its prefab can recover navigation.
            Undo.RecordObject(state, "Discard object interaction draft");
            ObjectHover hover = Resolve(state);
            state.OriginalPreset = hover != null ? hover.Preset : null;
            state.Binding = HoverBindingDraft.Capture(hover);
            state.OriginalBinding = ObjectWorkspace.Copy(state.Binding);
            state.Hierarchy = hover != null ? HoverHierarchy.Signature(hover.transform) : string.Empty;
            state.HasBinding = hover != null;
            LoadPreset(state, state.HasBinding ? state.OriginalPreset : state.Source);
            if (hover != null)
                state.Draft = ObjectWorkspace.Copy(hover.Configuration);
            state.Baseline = ObjectWorkspace.Copy(state.Draft);
            state.Observer.Discard();
            state.Single.Read(state.Target.Resolve());
            state.Extended.Read(state.Target.Resolve());
            state.Persist();
        }

        #endregion

        #region Validation

        /// <summary>Prepares every pending domain before any asset or scene is changed.</summary>
        /// <param name="state">Workspace proposal and original baselines.</param>
        /// <param name="warning">Receives missing references, invalid values or outside-edit conflicts.</param>
        /// <returns>True when the complete transaction may be applied.</returns>
        internal static bool TryValidate(ObjectWorkspace state, out string warning)
        {
            // Observer-only setup does not require a hover preset selection.
            warning = string.Empty;
            if (state.PrefabChanged && !state.Target.IsOpen)
            {
                warning = "Open the selected prefab before applying its retained changes.";
                return false;
            }
            if ((state.Single.HasChanges || state.Extended.HasChanges || state.HasBinding && state.InteractionChanged)
                && !ObjectAuthoringSave.TryValidate(state.Target.Resolve(), out warning))
                return false;
            if (!state.Single.TryValidate(state.Target.Resolve(), out warning))
                return false;
            if (!state.Extended.TryValidate(state.Target.Resolve(), out warning))
                return false;
            if (!state.InteractionChanged)
                return state.Observer.TryValidate(out warning);
            if (!state.HasBinding)
            {
                warning = "Select a Hover interaction before Apply, or use Update or Export to save the preset.";
                return false;
            }
            if (!state.Draft.TryValidate(out warning))
                return false;
            if (warning.Length > 0)
                return false;
            ObjectHover hover = Resolve(state);
            if (state.HasBinding && hover != null && (!state.Binding.ToolRequirement.TryValidate(out warning) || !state.Binding.FlagChange.TryValidate(hover.gameObject, out warning)
                || state.Binding.Enabled && !state.Binding.VisualEffect.TryValidate(false, out warning)))
                return false;
            if (state.HasBinding)
            {
                // Binding and hierarchy checks protect changes made by another inspector or prefab stage.
                if (hover == null || hover.Preset != state.OriginalPreset
                    || JsonUtility.ToJson(hover.Configuration) != JsonUtility.ToJson(state.Baseline)
                    || JsonUtility.ToJson(HoverBindingDraft.Capture(hover)) != JsonUtility.ToJson(state.OriginalBinding)
                    || HoverHierarchy.Signature(hover.transform) != state.Hierarchy)
                    warning = "The selected interaction or its hierarchy changed. Discard to reload it.";
                else if (hover.Label == null || !hover.Label.TryValidate(hover.transform, out warning))
                    warning = warning.Length > 0 ? warning : "Create the dedicated hover label before Apply.";
                else if (state.Binding.AnchorPath != "-" && HoverHierarchy.Resolve(hover.transform, state.Binding.AnchorPath) == null)
                    warning = "The proposed anchor is no longer inside the selected object.";
                else if (state.Draft.Settings.TargetMode != HoverDetectionMode.ViewCenter && hover.GetComponentInChildren<Collider>(true) == null
                    && hover.GetComponent<ObjectAssemblyProduct>() == null)
                    warning = "Collider hover requires an existing 3D collider on the object or its children.";
                else if (EditorUtility.IsPersistent(hover) && !AssetDatabase.IsOpenForEdit(hover))
                    warning = "The selected prefab is not writable.";
            }
            return warning.Length == 0 && state.Observer.TryValidate(out warning);
        }

        #endregion

        #region Confirmation

        /// <summary>Commits validated preset, binding and observer changes in one Undo group.</summary>
        /// <param name="state">Workspace whose proposal should become applied data.</param>
        /// <param name="warning">Receives a validation or save failure.</param>
        /// <returns>True when every pending domain was applied successfully.</returns>
        internal static bool Apply(ObjectWorkspace state, out string warning)
        {
            // Nothing is written until all references and baselines have passed validation.
            if (!TryValidate(state, out warning))
                return false;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply object interaction");
            Undo.RecordObject(state, "Apply object interaction");
            GameObject target = state.Target.Resolve();
            SpawnSourceAuthoring spawnSources = new SpawnSourceAuthoring();
            bool savePrefab = state.Single.HasChanges || state.Extended.HasChanges || state.HasBinding && state.InteractionChanged;
            try
            {
                // Apply writes local snapshots; only the explicit Update buttons modify preset assets.
                ObjectHover hover = state.HasBinding ? Resolve(state) : null;
                if (state.InteractionChanged && hover != null)
                    ApplyBinding(state, hover);

                state.Single.Apply(target);
                state.Extended.Apply(target);
                if (state.Extended.HasChanges && state.Extended.Resolve(target) is ObjectSpawnManager manager && state.Extended.Draft.Enabled)
                    spawnSources.Prepare(manager.Settings);
                if (savePrefab)
                    ObjectAuthoringSave.Save(target);
                state.Observer.Apply();
                spawnSources.Save();
                Undo.FlushUndoRecordObjects();
                Discard(state);
                Undo.CollapseUndoOperations(group);
                return true;
            }
            catch (Exception exception)
            {
                // Restore both in-memory edits and any asset already written before a later save failed.
                Undo.RevertAllDownToGroup(group);
                warning = "Apply failed: " + exception.Message;
                try
                {
                    spawnSources.Save();
                    if (savePrefab && target != null)
                        ObjectAuthoringSave.Save(target);
                }
                catch (Exception rollback)
                {
                    warning += " Restoring saved data also failed: " + rollback.Message;
                }
                return false;
            }
        }

        /// <summary>Writes object-only values and updates the already authored label.</summary>
        /// <param name="state">Validated reusable and per-object proposal.</param>
        /// <param name="hover">Unchanged component receiving the binding.</param>
        private static void ApplyBinding(ObjectWorkspace state, ObjectHover hover)
        {
            // Shared preset assignment never creates another hover component.
            using (SerializedObject data = new SerializedObject(hover))
            {
                data.FindProperty("preset").objectReferenceValue = state.Source;
                data.FindProperty("interactionName").stringValue = state.Binding.Name;
                data.FindProperty("m_Enabled").boolValue = state.Binding.Enabled;
                data.FindProperty("drawGizmos").boolValue = state.Binding.DrawGizmos;
                data.FindProperty("anchor").objectReferenceValue = HoverHierarchy.Resolve(hover.transform, state.Binding.AnchorPath);
                data.ApplyModifiedProperties();
            }
            Undo.RecordObject(hover, "Apply hover flag change");
            JsonUtility.FromJsonOverwrite("{\"configuration\":" + JsonUtility.ToJson(state.Draft)
                + ",\"flagChange\":" + JsonUtility.ToJson(state.Binding.FlagChange)
                + ",\"visualEffect\":" + JsonUtility.ToJson(state.Binding.VisualEffect)
                + ",\"toolRequirement\":" + JsonUtility.ToJson(state.Binding.ToolRequirement) + "}", hover);
            EditorUtility.SetDirty(hover);
            if (!EditorUtility.IsPersistent(hover))
                PrefabUtility.RecordPrefabInstancePropertyModifications(hover);
            HoverLabel label = hover.Label;
            UnityEngine.Object[] graphics = label.Background != null
                ? new UnityEngine.Object[] { label.Text, label.Text.rectTransform, label.Panel, label.Canvas, label.Background }
                : new UnityEngine.Object[] { label.Text, label.Text.rectTransform, label.Panel, label.Canvas };
            Undo.RecordObjects(graphics, "Apply hover appearance");
            state.Draft.Style.Apply(label);
            foreach (UnityEngine.Object graphic in graphics)
            {
                EditorUtility.SetDirty(graphic);
                if (!EditorUtility.IsPersistent(graphic))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(graphic);
            }
        }

        #endregion

        #endregion
    }
}
