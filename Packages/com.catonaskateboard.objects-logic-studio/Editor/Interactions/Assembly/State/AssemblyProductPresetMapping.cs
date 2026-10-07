using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Transfers assembly recipes without retaining references to the source prefab hierarchy.</summary>
    internal static class AssemblyProductPresetMapping
    {
        #region Methods
        #region Transfer

        /// <summary>Captures resolved draft targets as portable descriptors.</summary>
        /// <param name="preset">Transient or writable preset receiving the snapshot.</param>
        /// <param name="draft">Pending product configuration.</param>
        /// <param name="owner">Source product containing the referenced interactions.</param>
        internal static void Capture(AssemblyProductPreset preset, AssemblyProductDraft draft, GameObject owner)
        {
            // Resolve stable draft IDs before discarding local component references.
            preset.Settings = draft.Resolve(owner.transform);
            preset.Targets = new InteractionTemplateReference[preset.Settings.InteractionRules.Length];
            for (int index = 0; index < preset.Targets.Length; index++)
            {
                preset.Targets[index] = UnlockPresetMapping.Capture(owner.transform, preset.Settings.InteractionRules[index].Target);
                preset.Settings.InteractionRules[index].Target = null;
            }
        }

        /// <summary>Resolves every target before replacing any pending destination data.</summary>
        /// <param name="preset">Portable source snapshot.</param>
        /// <param name="owner">Destination product containing its existing interactions.</param>
        /// <param name="draft">Receives a detached draft with destination-local stable IDs.</param>
        /// <param name="warning">Receives missing mappings or destination geometry issues.</param>
        /// <returns>True when the entire product can be imported atomically.</returns>
        internal static bool TryResolve(AssemblyProductPreset preset, GameObject owner, out AssemblyProductDraft draft, out string warning)
        {
            draft = null;
            warning = "Select a destination product before importing its preset.";
            if (owner == null || !preset.TryValidate(out warning))
                return false;
            AssemblyProductSettings settings = ObjectWorkspace.Copy(preset.Settings);
            ObjectInteraction[] candidates = owner.GetComponentsInChildren<ObjectInteraction>(true);
            for (int index = 0; index < preset.Targets.Length; index++)
                settings.InteractionRules[index].Target = UnlockPresetMapping.Resolve(preset.Targets[index], owner.transform, candidates);
            if (!AssemblyValidation.TryValidate(owner, settings, out warning))
                return false;
            draft = AssemblyProductDraft.Capture(settings);
            return true;
        }

        #endregion
        #endregion
    }
}
