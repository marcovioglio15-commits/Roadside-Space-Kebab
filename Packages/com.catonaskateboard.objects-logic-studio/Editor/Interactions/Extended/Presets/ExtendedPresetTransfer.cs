using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Builds the same portable snapshot for Export and Update.</summary>
    internal static class ExtendedPresetTransfer
    {
        #region Methods
        #region Capture

        /// <summary>Copies the active proposal into a temporary typed preset owned by the caller.</summary>
        /// <param name="state">Workspace containing the selected interaction and its pending settings.</param>
        /// <returns>A temporary preset to validate, save or destroy.</returns>
        internal static ExtendedInteractionPreset Capture(ObjectWorkspace state)
        {
            // Only the active configuration is transferred; local input bindings remain on the object.
            ExtendedInteractionPreset preset = (ExtendedInteractionPreset)ScriptableObject.CreateInstance(PresetType(state.Extended.Kind));
            ExtendedInteractionDraft draft = state.Extended.Draft;
            switch (preset)
            {
                case ElasticDeformationPreset elasticDeformation:
                    elasticDeformation.Settings = ObjectWorkspace.Copy(draft.ElasticDeformation);
                    break;
                case DirtTrailPreset dirtTrail:
                    dirtTrail.Settings = ObjectWorkspace.Copy(draft.DirtTrail);
                    break;
                case SpraySaucePreset spraySauce:
                    spraySauce.Settings = ObjectWorkspace.Copy(draft.SpraySauce);
                    break;
                case DegradationPreset degradation:
                    degradation.Settings = ObjectWorkspace.Copy(draft.ObjectDegradation);
                    break;
                case GravityGeneratorPreset gravity:
                    gravity.Settings = ObjectWorkspace.Copy(draft.GravityGenerator);
                    break;
                case AmbientPreset ambient:
                    ambient.Settings = ObjectWorkspace.Copy(draft.Ambient);
                    break;
                case SlicePreset slice:
                    slice.Settings = ObjectWorkspace.Copy(draft.Slice);
                    break;
                case SpawnManagementPreset spawn:
                    spawn.Settings = SpawnSourceAuthoring.Resolve(draft.SpawnManagement);
                    break;
                case AssemblyStationPreset station:
                    station.Settings = ObjectWorkspace.Copy(draft.AssemblyStation);
                    break;
                case AssemblyProductPreset product:
                    AssemblyProductPresetMapping.Capture(product, draft.AssemblyProduct, state.Target.Resolve());
                    break;
                case AvailableOrdersPreset orders:
                    orders.Settings = draft.ResolveOrders(state.Target.Resolve());
                    orders.Completion = UnlockPresetMapping.Capture(state.Target.Resolve().transform, orders.Settings.CompletionSource);
                    orders.Settings.CompletionSource = null;
                    break;
                case OutlinePreset outline:
                    outline.Settings = ObjectWorkspace.Copy(draft.Outline);
                    break;
                case ContactModificationPreset contact:
                    contact.Settings = ObjectWorkspace.Copy(draft.Contact);
                    break;
                case DialoguePreset dialogue:
                    dialogue.Settings = ObjectWorkspace.Copy(draft.Dialogue);
                    break;
            }
            preset.ToolRequirement = ObjectWorkspace.Copy(draft.ToolRequirement);
            return preset;
        }

        /// <summary>Maps every ordinary extended interaction to its dedicated reusable asset.</summary>
        /// <param name="kind">Feature represented by the selected interaction.</param>
        /// <returns>The matching preset type; unlock rules use their separate mapping workflow.</returns>
        internal static Type PresetType(ExtendedInteractionKind kind)
        {
            return kind switch
            {
                ExtendedInteractionKind.ObjectDegradation => typeof(DegradationPreset),
                ExtendedInteractionKind.GravityGenerator => typeof(GravityGeneratorPreset),
                ExtendedInteractionKind.ElasticDeformation => typeof(ElasticDeformationPreset),
                ExtendedInteractionKind.DirtTrail => typeof(DirtTrailPreset),
                ExtendedInteractionKind.SpraySauce => typeof(SpraySaucePreset),
                ExtendedInteractionKind.PlayAmbient => typeof(AmbientPreset),
                ExtendedInteractionKind.Slice => typeof(SlicePreset),
                ExtendedInteractionKind.ModifyByContact => typeof(ContactModificationPreset),
                ExtendedInteractionKind.Outline => typeof(OutlinePreset),
                ExtendedInteractionKind.SpawnManagement => typeof(SpawnManagementPreset),
                ExtendedInteractionKind.AssemblyStation => typeof(AssemblyStationPreset),
                ExtendedInteractionKind.AssemblyProduct => typeof(AssemblyProductPreset),
                ExtendedInteractionKind.AvailableOrders => typeof(AvailableOrdersPreset),
                ExtendedInteractionKind.Dialogue => typeof(DialoguePreset),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        #endregion
        #endregion
    }
}
