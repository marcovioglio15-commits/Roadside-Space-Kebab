using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Replaces one authored mesh at completion while leaving its transform and materials intact.</summary>
    [Serializable]
    public sealed class ContactMeshReplacement
    {
        #region Fields

        [Header("Mesh Replacement")]
        [Tooltip("Child path relative to the affected item, such as Visual/Body. Empty selects its root.")]
        public string Path = string.Empty;
        [Tooltip("Mesh assigned to the existing MeshFilter or SkinnedMeshRenderer at completion.")]
        public Mesh Mesh;
        [Tooltip("Also replace an existing MeshCollider's mesh on this same child. Primitive colliders keep their authored dimensions.")]
        public bool UpdateCollider;

        #endregion
    }

    /// <summary>Effects applied to one participant; mesh changes and consumption commit only at completion.</summary>
    [Serializable]
    public sealed class ContactEffects
    {
        #region Fields

        [Header("Appearance")]
        [Tooltip("Multiply the current material color without changing the shared material asset or texture.")]
        public bool Tint;
        [Tooltip("Multiplicative final color. White preserves the current color.")]
        public Color Multiplier = Color.white;
        [Tooltip("Shader color property receiving the multiplier, usually _BaseColor for URP or _Color for built-in shaders.")]
        public string ColorProperty = "_BaseColor";
        [Tooltip("Renderer branch relative to the affected item. Empty includes all owned renderers.")]
        public string RendererPath = string.Empty;
        [Tooltip("Material slot to tint; -1 includes every material slot with the selected color property.")]
        public int MaterialSlot = -1;
        [Tooltip("Mesh substitutions applied when the modification finishes.")]
        public ContactMeshReplacement[] Meshes = Array.Empty<ContactMeshReplacement>();
        [Header("Consumption")]
        [Tooltip("Deactivate this item at completion. The other participant records its tag once. Only one participant can be consumed.")]
        public bool Consume;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks reusable effect data before binding it to either participant.</summary>
        /// <param name="warning">Receives invalid color or mesh data.</param>
        /// <returns>True when all enabled effects have usable settings.</returns>
        public bool TryValidate(out string warning)
        {
            // Validation reports errors without changing authored values or shared assets.
            warning = string.Empty;
            if (Tint && (string.IsNullOrWhiteSpace(ColorProperty) || MaterialSlot < -1
                || !InteractionValues.Finite(new Vector3(Multiplier.r, Multiplier.g, Multiplier.b)) || !InteractionValues.Finite(Multiplier.a)))
                warning = "Tint needs a shader color property, finite multiplier and a material slot of -1 or greater.";
            else if (Meshes == null)
                warning = "Mesh replacements are missing.";
            else
                foreach (ContactMeshReplacement replacement in Meshes)
                    if (replacement == null || replacement.Mesh == null)
                    {
                        warning = "Assign a mesh to each replacement or remove its unused entry.";
                        break;
                    }
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }

    /// <summary>Configures continuous tag-based contact and independently chosen effects on both participants.</summary>
    [Serializable]
    public sealed class ContactModificationSettings
    {
        #region Fields

        [Header("Contact")]
        [Tooltip("Project tag required on the other Object Item root.")]
        public string Tag = "Untagged";
        [Tooltip("Uninterrupted contact time in seconds before effects begin.")]
        public float ContactDuration = 1f;
        [Tooltip("Maximum separation counted as contact, in metres. Include the carry collision padding when held objects should activate this interaction.")]
        public float ContactTolerance = 0.02f;
        [Tooltip("Seconds between contact queries. Effect animation still updates every frame.")]
        public float QueryInterval = 0.02f;
        [Tooltip("Allow trigger volumes to count as contact.")]
        public bool IncludeTriggers;
        [Tooltip("Allow this interaction while its own item is carried by the player.")]
        public bool AllowCarriedSelf = true;
        [Tooltip("Allow contact with an item currently carried by the player.")]
        public bool AllowCarriedOther = true;
        [Header("Modification")]
        [Tooltip("Seconds for the color transition. Zero commits all effects immediately after the contact delay.")]
        public float Duration = 1f;
        [Tooltip("Finish a started modification after separation. Carried-item eligibility still applies.")]
        public bool CompleteAfterSeparation;
        [Tooltip("Pause at the current appearance and resume with the same participant after contact returns. Temporary restrictions are released while paused; visual ownership is retained. Disabling either item cancels the session.")]
        public bool ResumeAfterInterruption;
        [Tooltip("Allow this pair to run again after a completed interaction and a subsequent separation.")]
        public bool RepeatAfterSeparation;
        [Tooltip("Effects applied to the item owning this interaction.")]
        public ContactEffects Self = new ContactEffects();
        [Tooltip("Effects applied to the matching item in contact.")]
        public ContactEffects Other = new ContactEffects();
        [Header("Contact Item Tag")]
        [Tooltip("Assign a new tag to the contacted item only after this modification completes. Interrupted or cancelled effects do not change its tag.")]
        public bool ChangeContactTag;
        [Tooltip("Project tag assigned to the contacted item's root at completion. Consumption receipts retain the tag recorded before this change.")]
        public string ContactTag = "Untagged";
        [Header("Temporary Restrictions")]
        [Tooltip("Interaction families suspended on this item while effects run. Blocking Grab releases an already held item safely.")]
        public InteractionChannels BlockSelf = InteractionChannels.Grab;
        [Tooltip("Interaction families suspended on the matching item while effects run.")]
        public InteractionChannels BlockOther = InteractionChannels.Grab;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Validates timing and effect ownership before any item is reserved.</summary>
        /// <param name="warning">Receives the first configuration error.</param>
        /// <returns>True when this contact configuration is complete.</returns>
        public bool TryValidate(out string warning)
        {
            // Both items cannot disappear because the surviving item owns the consumption receipt.
            warning = string.Empty;
            if (string.IsNullOrWhiteSpace(Tag) || !InteractionValues.Finite(ContactDuration) || ContactDuration < 0f
                || !InteractionValues.Positive(ContactTolerance) || !InteractionValues.Positive(QueryInterval)
                || !InteractionValues.Finite(Duration) || Duration < 0f)
                warning = "Choose a tag, non-negative finite durations, and positive finite contact tolerance and query interval.";
            else if (Self == null || Other == null)
                warning = "Both participant effect settings are required.";
            else if (!Self.TryValidate(out warning) || !Other.TryValidate(out warning))
                return false;
            else if (Self.Consume && Other.Consume)
                warning = "Consume only one participant so the other can retain its receipt.";
            else if (!ValidChannels(BlockSelf) || !ValidChannels(BlockOther))
                warning = "Choose supported interaction families for temporary restrictions.";
            else if (ChangeContactTag && string.IsNullOrWhiteSpace(ContactTag))
                warning = "Choose a project tag for the contacted item.";
            else if (!ChangeContactTag && !Self.Tint && Self.Meshes.Length == 0 && !Self.Consume && !Other.Tint && Other.Meshes.Length == 0 && !Other.Consume)
                warning = "Enable at least one modification effect.";
            return warning.Length == 0;
        }

        /// <summary>Accepts known restriction flags and Unity's Everything selection without rewriting the mask.</summary>
        /// <param name="channels">Authored participant restriction mask.</param>
        /// <returns>True when the mask represents supported interactions.</returns>
        private static bool ValidChannels(InteractionChannels channels)
        {
            // Enum flag controls may serialize Everything as all bits set, including unused bits.
            return channels == (InteractionChannels)(-1) || (channels & ~(InteractionChannels.Grab | InteractionChannels.Release
                | InteractionChannels.Hover | InteractionChannels.Dialogue | InteractionChannels.Passive
                | InteractionChannels.Assembly | InteractionChannels.Transfer | InteractionChannels.Spawn | InteractionChannels.Slice)) == 0;
        }

        #endregion

        #endregion
    }
}
