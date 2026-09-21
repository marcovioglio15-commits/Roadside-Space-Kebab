using System;
using UnityEditor;
using UnityEditor.SceneManagement;
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
        /// <param name="target">Object selected in a scene, prefab stage or asset.</param>
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
            state.Target.Capture(target, index);
            state.Single.Read(target);
            ObjectHover hover = Resolve(state);
            state.HasBinding = hover != null;
            state.OriginalPreset = hover != null ? hover.Preset : null;
            state.Binding = HoverBindingDraft.Capture(hover);
            state.OriginalBinding = ObjectWorkspace.Copy(state.Binding);
            state.Hierarchy = hover != null ? HoverHierarchy.Signature(hover.transform) : string.Empty;
            LoadPreset(state, state.OriginalPreset);
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
            state.Baseline = ObjectWorkspace.Copy(state.Draft);
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
            // A missing target retains its identity so reopening its scene or prefab can recover navigation.
            Undo.RecordObject(state, "Discard object interaction draft");
            ObjectHover hover = Resolve(state);
            state.OriginalPreset = hover != null ? hover.Preset : null;
            state.Binding = HoverBindingDraft.Capture(hover);
            state.OriginalBinding = ObjectWorkspace.Copy(state.Binding);
            state.Hierarchy = hover != null ? HoverHierarchy.Signature(hover.transform) : string.Empty;
            LoadPreset(state, state.HasBinding ? state.OriginalPreset : state.Source);
            state.Observer.Discard();
            state.Single.Read(state.Target.Resolve());
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
            if (!state.Single.TryValidate(state.Target.Resolve(), out warning))
                return false;
            if (!state.InteractionChanged)
                return state.Observer.TryValidate(out warning);
            if (state.Source == null || !EditorUtility.IsPersistent(state.Source) || !AssetDatabase.IsOpenForEdit(state.Source))
                warning = "Select or create a writable Hover Preset before Apply.";
            else if (JsonUtility.ToJson(state.Source.Configuration) != JsonUtility.ToJson(state.Baseline))
                warning = "The preset changed outside this session. Discard to reload its current values.";
            else if (!state.Draft.TryValidate(out warning))
                return false;
            if (warning.Length > 0)
                return false;
            ObjectHover hover = Resolve(state);
            if (state.HasBinding)
            {
                // Binding and hierarchy checks protect changes made by another inspector or prefab stage.
                if (hover == null || hover.Preset != state.OriginalPreset
                    || JsonUtility.ToJson(HoverBindingDraft.Capture(hover)) != JsonUtility.ToJson(state.OriginalBinding)
                    || HoverHierarchy.Signature(hover.transform) != state.Hierarchy)
                    warning = "The selected interaction or its hierarchy changed. Discard to reload it.";
                else if (hover.Label == null || !hover.Label.TryValidate(hover.transform, out warning))
                    warning = warning.Length > 0 ? warning : "Create the dedicated hover label before Apply.";
                else if (state.Binding.AnchorPath != "-" && HoverHierarchy.Resolve(hover.transform, state.Binding.AnchorPath) == null)
                    warning = "The proposed anchor is no longer inside the selected object.";
                else if (state.Draft.Settings.TargetMode == HoverTargetMode.Cursor && hover.GetComponentInChildren<Collider>(true) == null)
                    warning = "Cursor hover requires an existing 3D collider on the object or its children.";
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
            try
            {
                // The preset remains the single source of reusable settings and appearance.
                if (state.Source != null && state.PresetChanged)
                {
                    Undo.RecordObject(state.Source, "Apply hover preset");
                    JsonUtility.FromJsonOverwrite("{\"configuration\":" + JsonUtility.ToJson(state.Draft) + "}", state.Source);
                    EditorUtility.SetDirty(state.Source);
                }
                ObjectHover hover = state.HasBinding ? Resolve(state) : null;
                if (state.InteractionChanged && hover != null && state.Source != null)
                    ApplyBinding(state, hover);
                state.Observer.Apply();
                state.Single.Apply(state.Target.Resolve());
                if (state.Single.HasChanges || state.InteractionChanged && hover != null)
                    ObjectAuthoringSave.Save(state.Target.Resolve());
                if (state.Source != null)
                    AssetDatabase.SaveAssetIfDirty(state.Source);
                Undo.FlushUndoRecordObjects();
                Discard(state);
                Undo.CollapseUndoOperations(group);
                return true;
            }
            catch (Exception exception)
            {
                // Roll back component and preset writes before reporting a save failure.
                Undo.RevertAllDownToGroup(group);
                if (state.Source != null)
                    AssetDatabase.SaveAssetIfDirty(state.Source);
                warning = "Apply failed: " + exception.Message;
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
