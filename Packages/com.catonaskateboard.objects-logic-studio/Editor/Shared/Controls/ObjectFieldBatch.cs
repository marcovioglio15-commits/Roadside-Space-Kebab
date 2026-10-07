using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Applies one field to matching prefab interactions with native Undo and per-target validation.</summary>
    [InitializeOnLoad]
    internal static class ObjectFieldBatch
    {
        #region Methods
        #region Menu

        /// <summary>Registers prefab operations on the common Studio field menu.</summary>
        static ObjectFieldBatch()
        {
            // Other tools keep Copy/Paste even when a field has no interaction mapping.
            StudioFieldMenu.Populate += Populate;
            StudioFieldGroup.Populate += Populate;
        }

        /// <summary>Adds batch commands only for fields that map to an interaction component.</summary>
        /// <param name="menu">Shared field menu.</param>
        /// <param name="property">Clicked draft, preset or component field.</param>
        private static void Populate(GenericMenu menu, SerializedProperty property)
        {
            Populate(menu, new[] { property });
        }

        /// <summary>Adds atomic batch operations for a section or complete interaction.</summary>
        /// <param name="menu">Context menu receiving update commands.</param>
        /// <param name="properties">All fields represented by the clicked header.</param>
        private static void Populate(GenericMenu menu, SerializedProperty[] properties)
        {
            // Resolution happens only when opening the context menu, never during ordinary repaint.
            ObjectFieldTarget target = ObjectFieldTarget.Create(properties);
            if (target == null)
                return;
            Func<bool>[] guards = Array.ConvertAll(properties, property => StudioFieldMenu.Guard(property.serializedObject.targetObject, property.propertyPath));
            Func<bool> guard = () => Array.TrueForAll(guards, check => check());
            bool writable = GUI.enabled && !EditorApplication.isPlayingOrWillChangePlaymode;
            if (writable && target.Preset != null)
                menu.AddItem(new GUIContent("Update Same Preset", "Copy only this field to matching interactions using the selected preset."), false,
                    () => Run(target, true, guard));
            else
                menu.AddDisabledItem(new GUIContent("Update Same Preset", "Assign an interaction preset first."));
            if (writable)
                menu.AddItem(new GUIContent("Update All", "Copy only this field to every compatible prefab interaction of this type."), false,
                    () => Run(target, false, guard));
            else
                menu.AddDisabledItem(new GUIContent("Update All"));
        }

        /// <summary>Runs a delayed field update only while its original source is still selected.</summary>
        /// <param name="target">Captured field and interaction identity.</param>
        /// <param name="samePreset">Whether to restrict destination preset identity.</param>
        /// <param name="guard">Source selection lifetime check.</param>
        private static void Run(ObjectFieldTarget target, bool samePreset, Func<bool> guard)
        {
            // A stale context menu must never redirect writes to a newly selected interaction.
            if (!guard() || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            List<string> skipped = new List<string>();
            int changed = Apply(target, samePreset, AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }), skipped);
            string message = "Updated " + changed + " interaction fields.";
            if (skipped.Count > 0)
                Debug.LogWarning(message + " Skipped " + skipped.Count + " incompatible or unwritable targets:\n" + string.Join("\n", skipped));
            else
                Debug.Log(message);
            StudioFieldMenu.Notify(ObjectWorkspace.instance);
        }

        #endregion
        #region Batch

        /// <summary>Changes matching fields without importing presets or replacing other component settings.</summary>
        /// <param name="target">Source field and target component type.</param>
        /// <param name="samePreset">Whether only the assigned preset may match.</param>
        /// <param name="guids">Prefab asset identities to inspect.</param>
        /// <param name="skipped">Receives precise reasons for rejected targets.</param>
        /// <returns>The number of components whose field changed.</returns>
        internal static int Apply(ObjectFieldTarget target, bool samePreset, string[] guids, List<string> skipped)
        {
            // Persistent prefab objects support native Undo without keeping isolated prefab scenes loaded.
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(samePreset ? "Update Same Preset Field" : "Update All Interaction Fields");
            int changed = 0;
            try
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (root == null || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                        continue;
                    bool dirty = false;
                    foreach (ObjectInteraction feature in root.GetComponentsInChildren<ObjectInteraction>(true))
                    {
                        if (feature.GetType() != target.Type || samePreset && ObjectFieldTarget.PresetOf(feature) != target.Preset)
                            continue;
                        if (!AssetDatabase.IsOpenForEdit(path))
                        {
                            skipped.Add(path + ": prefab is not writable.");
                            break;
                        }
                        if (TryApply(target, feature, out bool modified, out string warning))
                        {
                            dirty |= modified;
                            if (modified)
                                changed++;
                        }
                        else
                            skipped.Add(path + " / " + feature.InteractionName + ": " + warning);
                    }
                    if (!dirty)
                        continue;
                    Undo.FlushUndoRecordObjects();
                    PrefabUtility.SavePrefabAsset(root, out bool saved);
                    if (!saved)
                        throw new InvalidOperationException("Unity could not save " + path + ".");
                    RefreshStage(path, target, samePreset, skipped);
                    if (AssetDatabase.GetAssetPath(ObjectWorkspace.instance.Target.Resolve()) == path)
                        ObjectFieldWorkspace.Rebase(ObjectWorkspace.instance, target);
                }
            }
            finally
            {
                // One native Undo step covers this field update, including any open stage mirrors.
                Undo.CollapseUndoOperations(group);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
            return changed;
        }

        /// <summary>Validates a field proposal before recording its final persistent edit.</summary>
        /// <param name="target">Typed source value and reference context.</param>
        /// <param name="feature">Destination interaction.</param>
        /// <param name="changed">Whether the saved value actually differs.</param>
        /// <param name="warning">Reason the field could not be transferred.</param>
        /// <returns>True when the destination accepted the field.</returns>
        internal static bool TryApply(ObjectFieldTarget target, ObjectInteraction feature, out bool changed, out string warning)
        {
            // Missing array rows remain untouched; mass updates never grow an unrelated recipe implicitly.
            changed = false;
            warning = string.Empty;
            using SerializedObject data = new SerializedObject(feature);
            StudioPropertyValue[] originals = new StudioPropertyValue[target.Fields.Length];
            uint[] hashes = new uint[target.Fields.Length];
            for (int index = 0; index < target.Fields.Length; index++)
            {
                SerializedProperty property = data.FindProperty(target.Fields[index].Path);
                if (!target.Fields[index].Value.Accepts(property))
                {
                    warning = "The corresponding field or array row is missing or has a different type: " + target.Fields[index].Path;
                    return false;
                }
                originals[index] = new StudioPropertyValue(property);
                hashes[index] = property.contentHash;
            }
            try
            {
                // Trial writes are restored even when reference mapping or component validation fails.
                foreach (ObjectFieldTarget field in target.Fields)
                    field.Value.Apply(data.FindProperty(field.Path), reference => MapReference(reference, target.Source, feature));
                data.ApplyModifiedPropertiesWithoutUndo();
                if (!Validate(feature, out warning))
                    return false;
                data.Update();
                for (int index = 0; index < target.Fields.Length; index++)
                    changed |= hashes[index] != data.FindProperty(target.Fields[index].Path).contentHash;
            }
            catch (Exception exception)
            {
                warning = exception.Message;
                return false;
            }
            finally
            {
                data.Update();
                for (int index = 0; index < target.Fields.Length; index++)
                    originals[index].Apply(data.FindProperty(target.Fields[index].Path));
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            if (!changed)
                return true;
            Undo.RecordObject(feature, "Update Interaction Field");
            data.Update();
            foreach (ObjectFieldTarget field in target.Fields)
                field.Value.Apply(data.FindProperty(field.Path), reference => MapReference(reference, target.Source, feature));
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
            if (!EditorUtility.IsPersistent(feature))
                PrefabUtility.RecordPrefabInstancePropertyModifications(feature);
            return true;
        }

        /// <summary>Checks the same feature dependencies used by ordinary Apply.</summary>
        /// <param name="feature">Destination after its trial field update.</param>
        /// <param name="warning">First validation failure.</param>
        /// <returns>True when the interaction remains valid or is explicitly disabled.</returns>
        private static bool Validate(ObjectInteraction feature, out string warning)
        {
            // Disabled unfinished configurations remain editable through the same rules as the tool.
            warning = string.Empty;
            if (!feature.enabled)
                return true;
            return feature switch
            {
                ObjectSingleInteraction single => SingleInteractionDraft.Capture(single).TryValidate(single, out warning),
                ObjectExtendedInteraction extended => ExtendedInteractionDraft.Capture(extended).TryValidate(extended, out warning),
                ObjectHover hover => hover.Configuration.TryValidate(out warning),
                _ => false
            };
        }

        #endregion
        #region Local References

        /// <summary>Remaps references inside the source prefab to the equivalent destination hierarchy.</summary>
        /// <param name="reference">Captured object or asset reference.</param>
        /// <param name="source">Interaction owning local source references.</param>
        /// <param name="destination">Interaction receiving the field.</param>
        /// <returns>The original external asset or an unambiguous local counterpart.</returns>
        internal static UnityEngine.Object MapReference(UnityEngine.Object reference, ObjectInteraction source, ObjectInteraction destination)
        {
            // Asset references such as flags, meshes, materials and output prefabs retain their identity.
            Transform origin = reference switch { GameObject item => item.transform, Component component => component.transform, _ => null };
            if (reference == null || source == null || origin == null || !origin.IsChildOf(source.transform.root))
                return reference;
            if (reference == source)
                return destination;
            string path = AnimationUtility.CalculateTransformPath(origin, source.transform.root);
            Transform mapped = path.Length == 0 ? destination.transform.root : destination.transform.root.Find(path);
            if (mapped == null)
                throw new InvalidOperationException("Missing local reference hierarchy: " + path);
            if (reference is GameObject)
                return mapped.gameObject;
            if (reference is Transform)
                return mapped;
            Component[] matches = mapped.GetComponents(reference.GetType());
            if (reference is ObjectInteraction interaction)
            {
                ObjectInteraction selected = null;
                foreach (Component candidate in matches)
                    if (candidate is ObjectInteraction other && other.InteractionName == interaction.InteractionName)
                    {
                        if (selected != null)
                            throw new InvalidOperationException("Ambiguous local interaction: " + interaction.InteractionName);
                        selected = other;
                    }
                if (selected != null)
                    return selected;
            }
            else if (matches.Length == 1)
                return matches[0];
            throw new InvalidOperationException("No unique local counterpart for " + reference.name + ".");
        }

        /// <summary>Mirrors only the changed field into an open prefab stage while preserving pending workspace proposals.</summary>
        /// <param name="path">Prefab asset just saved.</param>
        /// <param name="target">Transferred field.</param>
        /// <param name="samePreset">Preset matching policy.</param>
        /// <param name="skipped">Receives incompatible stage-only configurations.</param>
        private static void RefreshStage(string path, ObjectFieldTarget target, bool samePreset, List<string> skipped)
        {
            // Stage objects have different instance identities from the saved prefab asset.
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.assetPath != path)
                return;
            foreach (ObjectInteraction feature in stage.prefabContentsRoot.GetComponentsInChildren<ObjectInteraction>(true))
                if (feature.GetType() == target.Type && (!samePreset || ObjectFieldTarget.PresetOf(feature) == target.Preset)
                    && !TryApply(target, feature, out bool changed, out string warning))
                    skipped.Add(path + " (open stage): " + warning);
            ObjectFieldWorkspace.Rebase(ObjectWorkspace.instance, target);
        }

        #endregion
        #endregion
    }
}
