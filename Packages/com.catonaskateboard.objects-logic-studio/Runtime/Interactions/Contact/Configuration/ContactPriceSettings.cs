using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses whether one or every selected local completion releases collected prices to the HUD.</summary>
    public enum PriceCompletionMode { All, Any }

    /// <summary>Chooses whether an unfinished deferred balance is lost or paid when its owning lifetime ends.</summary>
    public enum PendingPriceOnDespawn { Discard, Collect }

    /// <summary>Configures recipe and order price collection independently of consumption rules.</summary>
    [Serializable]
    public sealed class ContactPriceSettings
    {
        #region Fields

        [Header("Price Collection")]
        [Tooltip("Collect recipe prices and fulfilled Available Orders price overrides. Order overrides also price consumed items without an Assembly Product.")]
        public bool CollectPrice;
        [Tooltip("Play the cash-collection cue when a positive amount is actually released to the HUD, including deferred settlement.")]
        public bool Sound = true;
        [Tooltip("Accumulate this interaction's earnings without updating the HUD until the selected local interactions complete.")]
        public bool DeferDisplay;
        [Tooltip("All waits for every selected interaction; Any releases earnings after the first selected completion.")]
        public PriceCompletionMode CompletionMode;
        [Tooltip("Discard loses only this interaction's still-pending earnings at despawn or the end of its Day Flow visit. Collect pays them anyway. Completed payouts remain untouched.")]
        public PendingPriceOnDespawn OnDespawn;
        [Tooltip("Interactions on this same object whose successful completion releases accumulated earnings before its lifetime ends.")]
        public LocalInteractionReference[] CompletionSources = Array.Empty<LocalInteractionReference>();

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks only enabled collection settings, resolving portable bindings when an owner is supplied.</summary>
        /// <param name="owner">Destination object, or null while editing a reusable preset.</param>
        /// <param name="warning">Receives an incomplete, repeated or missing local selection.</param>
        /// <returns>True when collection can use its selected display policy.</returns>
        public bool TryValidate(GameObject owner, out string warning)
        {
            warning = string.Empty;
            if (!CollectPrice || !DeferDisplay)
                return true;
            warning = "Select local completions and supported completion and despawn policies for deferred prices.";
            if (CompletionSources == null || CompletionSources.Length == 0 || CompletionMode is not (PriceCompletionMode.All or PriceCompletionMode.Any)
                || OnDespawn is not (PendingPriceOnDespawn.Discard or PendingPriceOnDespawn.Collect))
                return false;
            // Value descriptors preserve local selections through prefab duplication and preset transfer.
            for (int index = 0; index < CompletionSources.Length; index++)
            {
                LocalInteractionReference source = CompletionSources[index];
                if (source == null || !source.IsValid || owner != null && source.Resolve(owner) == null)
                    return false;
                for (int previous = 0; previous < index; previous++)
                    if (CompletionSources[previous].Kind == source.Kind && CompletionSources[previous].Name == source.Name
                        && CompletionSources[previous].Occurrence == source.Occurrence)
                    {
                        warning = "Select each price completion interaction only once.";
                        return false;
                    }
            }
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
