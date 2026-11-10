using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Reads and commits validated object interaction snapshots.</summary>
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
            if (state.PrefabChanged)
            {
                warning = "Apply or Discard before changing objects or interactions.";
                return false;
            }
            if (target != null && !ObjectAuthoringSave.TryValidate(target, out warning))
                return false;
            state.Target.Capture(target, index);
            state.Prefab = target != null
                ? AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(state.Target.PrefabGuid)) : null;
            state.Single.ComponentId = 0;
            state.Single.Read(target);
            state.Extended.ComponentId = 0;
            state.Extended.Read(target);
            ObjectHover hover = Resolve(state);
            state.HasBinding = hover != null;
            state.Binding = HoverBindingDraft.Capture(hover);
            state.OriginalBinding = ObjectWorkspace.Copy(state.Binding);
            state.Hierarchy = hover != null ? HoverHierarchy.Signature(hover.transform) : string.Empty;
            if (hover != null)
                state.Draft = ObjectWorkspace.Copy(hover.Configuration);
            state.Baseline = ObjectWorkspace.Copy(state.Draft);
            state.Persist();
            return true;
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
        /// <param name="includeScene">Whether to also drop pending Scene Setup changes.</param>
        /// <param name="recordUndo">Whether a retained workspace is being reset by the user.</param>
        internal static void Discard(ObjectWorkspace state, bool includeScene = true, bool recordUndo = true)
        {
            // A missing target retains its identity so reopening its prefab can recover navigation.
            if (recordUndo)
                Undo.RecordObject(state, "Discard object interaction draft");
            ObjectHover hover = Resolve(state);
            state.Binding = HoverBindingDraft.Capture(hover);
            state.OriginalBinding = ObjectWorkspace.Copy(state.Binding);
            state.Hierarchy = hover != null ? HoverHierarchy.Signature(hover.transform) : string.Empty;
            state.HasBinding = hover != null;
            if (hover != null)
                state.Draft = ObjectWorkspace.Copy(hover.Configuration);
            state.Baseline = ObjectWorkspace.Copy(state.Draft);
            if (includeScene)
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
            // Validate the detached object snapshot before making any prefab changes.
            warning = string.Empty;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                warning = "Exit Play Mode before applying object settings.";
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
                return true;
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
                || state.Binding.Enabled && (!state.Binding.VisualEffect.TryValidate(false, out warning)
                    || !HoverInteractionText.TryValidate(state.Binding.InteractionText, hover.gameObject, out warning))))
                return false;
            if (state.HasBinding)
            {
                // Binding and hierarchy checks protect changes made by another inspector or prefab stage.
                if (hover == null
                    || JsonUtility.ToJson(hover.Configuration) != JsonUtility.ToJson(state.Baseline)
                    || JsonUtility.ToJson(HoverBindingDraft.Capture(hover)) != JsonUtility.ToJson(state.OriginalBinding)
                    || HoverHierarchy.Signature(hover.transform) != state.Hierarchy)
                    warning = "The selected interaction or its hierarchy changed. Discard to reload it.";
                else if (state.Binding.AnchorPath != "-" && HoverHierarchy.Resolve(hover.transform, state.Binding.AnchorPath) == null)
                    warning = "The proposed anchor is no longer inside the selected object.";
                else if (state.Draft.Settings.TargetMode != HoverDetectionMode.ViewCenter && hover.GetComponentInChildren<Collider>(true) == null
                    && hover.GetComponent<ObjectAssemblyProduct>() == null)
                    warning = "Collider hover requires an existing 3D collider on the object or its children.";
                else if (EditorUtility.IsPersistent(hover) && !AssetDatabase.IsOpenForEdit(hover))
                    warning = "The selected prefab is not writable.";
            }
            return warning.Length == 0;
        }

        #endregion

        #region Confirmation

        /// <summary>Commits a validated interaction in one Undo group.</summary>
        /// <param name="state">Workspace whose proposal should become applied data.</param>
        /// <param name="warning">Receives a validation or save failure.</param>
        /// <returns>True when every pending domain was applied successfully.</returns>
        internal static bool Apply(ObjectWorkspace state, out string warning)
        {
            // Nothing is written until all references and baselines have passed validation.
            if (!TryValidate(state, out warning))
                return false;
            bool prepareSpawn = state.Extended.HasChanges && state.Extended.Kind == ExtendedInteractionKind.SpawnManagement
                && state.Extended.Draft.Enabled;
            // A source stage outside this transaction cannot receive completion bindings that would remain unsaved.
            if (prepareSpawn && !state.Target.IsOpen && state.Extended.Draft.SpawnManagement.SourcePrefab is GameObject source
                && PrefabStageUtility.GetCurrentPrefabStage() is PrefabStage stage && stage.assetPath == AssetDatabase.GetAssetPath(source))
            {
                warning = "Open the spawn manager's prefab before applying changes to its source bindings.";
                return false;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply object interaction");
            GameObject target = state.Target.Resolve();
            SpawnSourceAuthoring spawnSources = new SpawnSourceAuthoring();
            bool savePrefab = state.Single.HasChanges || state.Extended.HasChanges || state.HasBinding && state.InteractionChanged;
            try
            {
                // Apply writes local snapshots; only the explicit Update buttons modify preset assets.
                ObjectHover hover = state.HasBinding ? Resolve(state) : null;
                if (state.InteractionChanged && hover != null)
                    ApplyHover(state, hover);

                state.Single.Apply(target);
                state.Extended.Apply(target);
                if (prepareSpawn && state.Extended.Resolve(target) is ObjectSpawnManager manager)
                    spawnSources.Prepare(manager.Settings);
                if (savePrefab)
                    ObjectAuthoringSave.Save(target);
                spawnSources.Save();
                Undo.FlushUndoRecordObjects();
                Discard(state, includeScene: false, recordUndo: false);
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

        /// <summary>Writes object-only values and updates the already configured label.</summary>
        /// <param name="state">Validated reusable and per-object proposal.</param>
        /// <param name="hover">Unchanged component receiving the binding.</param>
        /// <param name="recordUndo">Whether this is a real-object write rather than an isolated preview.</param>
        internal static void ApplyHover(ObjectWorkspace state, ObjectHover hover, bool recordUndo = true)
        {
            // Shared preset assignment never creates another hover component.
            using (SerializedObject data = new SerializedObject(hover))
            {
                data.FindProperty("interactionName").stringValue = state.Binding.Name;
                data.FindProperty("m_Enabled").boolValue = state.Binding.Enabled;
                data.FindProperty("drawGizmos").boolValue = state.Binding.DrawGizmos;
                data.FindProperty("anchor").objectReferenceValue = HoverHierarchy.Resolve(hover.transform, state.Binding.AnchorPath);
                SerializedProperty labels = data.FindProperty("interactionText");
                labels.arraySize = state.Binding.InteractionText.Length;
                for (int index = 0; index < labels.arraySize; index++)
                    labels.GetArrayElementAtIndex(index).boxedValue = state.Binding.InteractionText[index];
                if (recordUndo)
                    data.ApplyModifiedProperties();
                else
                    data.ApplyModifiedPropertiesWithoutUndo();
            }
            if (recordUndo)
                Undo.RecordObject(hover, "Apply hover flag change");
            JsonUtility.FromJsonOverwrite("{\"configuration\":" + JsonUtility.ToJson(state.Draft)
                + ",\"flagChange\":" + JsonUtility.ToJson(state.Binding.FlagChange)
                + ",\"visualEffect\":" + JsonUtility.ToJson(state.Binding.VisualEffect)
                + ",\"toolRequirement\":" + JsonUtility.ToJson(state.Binding.ToolRequirement) + "}", hover);
            EditorUtility.SetDirty(hover);
            if (!EditorUtility.IsPersistent(hover))
                PrefabUtility.RecordPrefabInstancePropertyModifications(hover);

        }

        #endregion

        #endregion
    }
}
