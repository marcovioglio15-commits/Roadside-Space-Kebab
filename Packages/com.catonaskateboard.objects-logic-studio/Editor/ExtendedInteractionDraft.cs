using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Retains detached contact or dialogue settings independently of the native prefab stage.</summary>
    [Serializable]
    internal sealed class ExtendedInteractionDraft
    {
        #region Fields

        [Header("Binding")]
        [Tooltip("Name identifying this feature card on the selected prefab object.")]
        public string Name = string.Empty;
        [Tooltip("Allow this interaction to run on spawned instances.")]
        public bool Enabled = true;
        [Tooltip("Show selected-object range or contact guides.")]
        public bool DrawGizmos = true;
        [Tooltip("Optional input-triggered dialogue activation action.")]
        public InputActionReference StartAction;
        [Tooltip("Dialogue page-advance action.")]
        public InputActionReference AdvanceAction;
        [Header("Configuration")]
        [Tooltip("Detached contact timing and effect settings.")]
        public ContactModificationSettings Contact = new ContactModificationSettings();
        [Tooltip("Detached dialogue pages, conditions and flow settings.")]
        public DialogueSettings Dialogue = new DialogueSettings();
        [Tooltip("Detached outline shader settings.")]
        public OutlineSettings Outline = new OutlineSettings();
        [Tooltip("Existing target and unlock conditions retained by stable prefab identity.")]
        public UnlockInteractionDraft Unlock = new UnlockInteractionDraft();

        [Tooltip("Product prefab and table placement retained independently of its player action.")]
        public AssemblyStationSettings AssemblyStation = new AssemblyStationSettings();
        [Tooltip("Recipe, magnets and stable references to existing product interactions.")]
        public AssemblyProductDraft AssemblyProduct = new AssemblyProductDraft();

        [Tooltip("Optional item tag change retained independently of reusable settings presets.")]
        public InteractionTagChange TagChange = new InteractionTagChange();

        #endregion

        #region Methods

        #region Snapshots

        /// <summary>Copies the selected component's data without retaining mutable configuration references.</summary>
        /// <param name="feature">Applied component or null for an empty selection.</param>
        /// <returns>A detached proposal containing only the selected feature's meaningful configuration.</returns>
        internal static ExtendedInteractionDraft Capture(ObjectExtendedInteraction feature)
        {
            // Preset import never replaces these per-item name, input and enabled bindings.
            ExtendedInteractionDraft draft = new ExtendedInteractionDraft();
            if (feature == null)
                return draft;
            draft.TagChange = ObjectWorkspace.Copy(feature.TagChange);
            draft.Name = feature.InteractionName;
            draft.Enabled = feature.enabled;
            draft.DrawGizmos = feature.DrawGizmos;
            switch (feature)
            {
                case ObjectAssemblyStation station:
                    draft.AssemblyStation = ObjectWorkspace.Copy(station.Settings);
                    draft.StartAction = station.Action;
                    break;
                case ObjectAssemblyProduct product:
                    draft.AssemblyProduct = AssemblyProductDraft.Capture(product.Settings);
                    break;
                case ObjectContactModifier contact:
                    draft.Contact = ObjectWorkspace.Copy(contact.Settings);
                    break;
                case ObjectOutline outline:
                    draft.Outline = ObjectWorkspace.Copy(outline.Settings);
                    break;
                case ObjectInteractionUnlock unlock:
                    draft.Unlock = UnlockInteractionDraft.Capture(unlock.Settings);
                    break;
                case ObjectDialogue dialogue:
                    draft.Dialogue = ObjectWorkspace.Copy(dialogue.Settings);
                    draft.StartAction = dialogue.StartAction;
                    draft.AdvanceAction = dialogue.AdvanceAction;
                    break;
            }
            return draft;
        }

        /// <summary>Validates a proposal against existing prefab dependencies using runtime validation rules.</summary>
        /// <param name="feature">Component that will receive the proposal.</param>
        /// <param name="warning">Receives the first missing dependency or invalid setting.</param>
        /// <returns>True when Apply can commit this feature.</returns>
        internal bool TryValidate(ObjectExtendedInteraction feature, out string warning)
        {
            // A disabled unfinished feature may be saved without becoming eligible at runtime.
            warning = string.Empty;
            if (!Enabled)
                return true;
            if (!TagChange.TryValidate(feature.gameObject, out warning))
                return false;
            return feature switch
            {
                ObjectAssemblyStation station => station.TryValidate(AssemblyStation, StartAction, out warning),
                ObjectAssemblyProduct product => AssemblyValidation.TryValidate(product.gameObject, AssemblyProduct.Resolve(product.transform), out warning),
                ObjectOutline outline => Outline.TryValidate(out warning),
                ObjectInteractionUnlock unlock => Unlock.Resolve(unlock.transform.root).TryValidate(unlock.transform.root, out warning),
                ObjectContactModifier contact => contact.TryValidate(Contact, out warning),
                ObjectDialogue dialogue => dialogue.TryValidate(Dialogue, StartAction, AdvanceAction, out warning),
                _ => false
            };
        }

        #endregion

        #endregion
    }
}
