using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Imports and exports typed contact/dialogue snapshots while preserving per-item bindings.</summary>
    internal static class ExtendedInteractionPresetView
    {
        #region Methods

        #region Controls

        /// <summary>Draws explicit preset operations for the currently expanded feature.</summary>
        /// <param name="state">Persistent workspace receiving imported settings.</param>
        internal static void Draw(ObjectWorkspace state)
        {
            // Export does not Apply; imported values remain a reviewable proposal on the selected prefab.
            ExtendedInteractionPreset selected = (ExtendedInteractionPreset)StudioGUI.ObjectField(
                new GUIContent("Preset", "Import a matching settings snapshot; item name, actions and HUD bindings remain local."),
                state.Extended.Preset, ExtendedPresetTransfer.PresetType(state.Extended.Kind), false);
            if (selected != state.Extended.Preset)
            {
                state.Extended.Preset = selected;
                state.Extended.PresetBaseline = InteractionPresetWrites.Capture(selected);
                state.Persist();
            }
            using (new StudioButton.RowScope())
            {
                using (new EditorGUI.DisabledScope(selected == null))
                    if (StudioButton.Draw(new GUIContent("Import", "Copy the selected preset into this card's pending settings.")))
                        Import(state);
                using (new EditorGUI.DisabledScope(selected == null))
                    if (StudioButton.Draw(new GUIContent("Update", "Write this card's current settings to the selected preset. Apply saves the object independently.")))
                        try
                        {
                            InteractionPresetWrites.UpdateExtended(state);
                        }
                        catch (System.Exception exception)
                        {
                            Debug.LogWarning(exception.Message, selected);
                        }
                if (StudioButton.Draw(new GUIContent("Export", "Save this card's settings as a new typed preset.")))
                    Export(state);
            }
        }

        #endregion

        #region Snapshots

        /// <summary>Copies a matching preset into the detached configuration with Undo support.</summary>
        /// <param name="state">Workspace owning the selected preset and draft.</param>
        internal static void Import(ObjectWorkspace state)
        {
            // Reject mismatched types even if an editor callback outlives its original card selection.
            if (state.Extended.Preset == null || state.Extended.Preset.Kind != state.Extended.Kind)
                return;
            if (!state.Extended.Preset.TryValidate(out string warning))
            {
                Debug.LogWarning(warning, state.Extended.Preset);
                return;
            }
            Undo.RecordObject(state, "Import interaction preset");
            switch (state.Extended.Preset)
            {
                case ElasticDeformationPreset elasticDeformation:
                    state.Extended.Draft.ElasticDeformation = ObjectWorkspace.Copy(elasticDeformation.Settings);
                    break;
                case DirtTrailPreset dirtTrail:
                    state.Extended.Draft.DirtTrail = ObjectWorkspace.Copy(dirtTrail.Settings);
                    break;
                case SpraySaucePreset spraySauce:
                    state.Extended.Draft.SpraySauce = ObjectWorkspace.Copy(spraySauce.Settings);
                    break;
                case DegradationPreset degradation:
                    state.Extended.Draft.ObjectDegradation = ObjectWorkspace.Copy(degradation.Settings);
                    break;
                case GravityGeneratorPreset gravity:
                    state.Extended.Draft.GravityGenerator = ObjectWorkspace.Copy(gravity.Settings);
                    break;
                case AvailableOrdersPreset orders:
                    GameObject owner = state.Target.Resolve();
                    ObjectInteraction completion = UnlockPresetMapping.Resolve(orders.Completion, owner.transform, owner.GetComponents<ObjectInteraction>());
                    OrderSettings proposal = ObjectWorkspace.Copy(orders.Settings);
                    proposal.CompletionSource = completion;
                    if (!proposal.ValidateCompletion(owner, out warning))
                    {
                        Debug.LogWarning(warning, orders);
                        return;
                    }
                    state.Extended.Draft.Orders = ObjectWorkspace.Copy(orders.Settings);
                    state.Extended.Draft.Orders.CompletionSource = null;
                    state.Extended.Draft.OrdersCompletionId = completion != null ? ObjectWorkspaceTarget.FileId(completion) : 0;
                    break;
                case AssemblyProductPreset product:
                    if (!AssemblyProductPresetMapping.TryResolve(product, state.Target.Resolve(), out AssemblyProductDraft draft, out warning))
                    {
                        Debug.LogWarning(warning, product);
                        return;
                    }
                    state.Extended.Draft.AssemblyProduct = draft;
                    break;
                case AmbientPreset ambient:
                    state.Extended.Draft.Ambient = ObjectWorkspace.Copy(ambient.Settings);
                    break;
                case SlicePreset slice:
                    state.Extended.Draft.Slice = ObjectWorkspace.Copy(slice.Settings);
                    break;
                case SpawnManagementPreset spawn:
                    state.Extended.Draft.SpawnManagement = ObjectWorkspace.Copy(spawn.Settings);
                    break;
                case AssemblyStationPreset station:
                    state.Extended.Draft.AssemblyStation = ObjectWorkspace.Copy(station.Settings);
                    break;
                case OutlinePreset outline:
                    state.Extended.Draft.Outline = ObjectWorkspace.Copy(outline.Settings);
                    break;
                case ContactModificationPreset contact:
                    state.Extended.Draft.Contact = ObjectWorkspace.Copy(contact.Settings);
                    break;
                case DialoguePreset dialogue:
                    state.Extended.Draft.Dialogue = ObjectWorkspace.Copy(dialogue.Settings);
                    break;
            }
            state.Extended.Draft.ToolRequirement = ObjectWorkspace.Copy(state.Extended.Preset.ToolRequirement);
            state.Extended.PresetBaseline = InteractionPresetWrites.Capture(state.Extended.Preset);
            state.Persist();
        }

        /// <summary>Creates a new preset asset from the selected card's current settings.</summary>
        /// <param name="state">Workspace providing the pending configuration.</param>
        private static void Export(ObjectWorkspace state)
        {
            // Cancelling the asset picker changes neither the draft nor an existing preset.
            string path = EditorUtility.SaveFilePanelInProject("Export interaction preset", state.Extended.Kind + " Preset", "asset", "Choose where to save these settings.");
            if (string.IsNullOrEmpty(path))
                return;
            ExtendedInteractionPreset preset = ExtendedPresetTransfer.Capture(state);
            if (!preset.TryValidate(out string warning))
            {
                Debug.LogWarning(warning);
                Object.DestroyImmediate(preset);
                return;
            }
            AssetDatabase.CreateAsset(preset, AssetDatabase.GenerateUniqueAssetPath(path));
            Undo.RegisterCreatedObjectUndo(preset, "Export interaction preset");
            AssetDatabase.SaveAssetIfDirty(preset);
            state.Extended.Preset = preset;
            state.Extended.PresetBaseline = InteractionPresetWrites.Capture(preset);
            state.Persist();
        }

        #endregion

        #endregion
    }
}
