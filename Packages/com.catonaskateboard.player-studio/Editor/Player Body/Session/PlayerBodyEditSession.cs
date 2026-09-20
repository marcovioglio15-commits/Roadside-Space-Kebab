using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>
    /// Holds a Body draft separately from its asset and checks for conflicting edits before Apply.
    /// Serialized values let the owning window keep its draft across script reloads.
    /// </summary>
    [Serializable]
    internal sealed class PlayerBodyEditSession
    {
        #region Serialized State

        [Header("Source")]
        [Tooltip("Persistent Body asset selected for this session.")]
        [SerializeField]
        private PlayerBodyPreset source;

        [Header("Baseline")]
        [Tooltip("Radius read when the session last opened, applied or discarded.")]
        [SerializeField]
        private float originalRadius;

        [Tooltip("Height read when the session last opened, applied or discarded.")]
        [SerializeField]
        private float originalHeight;

        [Header("Draft")]
        [Tooltip("Unsaved radius in metres; editing it does not change the Body asset.")]
        [SerializeField]
        private float radius;

        [Tooltip("Unsaved total height in metres, including both rounded ends.")]
        [SerializeField]
        private float height;

        #endregion

        #region Properties

        /// <summary>The asset that Apply may update.</summary>
        public PlayerBodyPreset Source => source;

        /// <summary>The unsaved radius shown in the window.</summary>
        public float Radius => radius;

        /// <summary>The unsaved total height shown in the window.</summary>
        public float Height => height;

        /// <summary>Compares values exactly, treating unchanged NaN values as unchanged.</summary>
        public bool HasChanges => !radius.Equals(originalRadius) || !height.Equals(originalHeight);

        #endregion

        #region Methods

        #region Draft

        /// <summary>Opens an asset only when no unsaved draft would be replaced.</summary>
        /// <param name="asset">Body asset to edit, or null to clear a clean session.</param>
        /// <param name="warning">Receives the reason opening was refused.</param>
        /// <returns>True when the requested source has been loaded.</returns>
        public bool TryOpen(PlayerBodyPreset asset, out string warning)
        {
            // A source switch must never silently discard pending edits.
            warning = string.Empty;
            if (HasChanges)
            {
                warning = "Apply or discard the current draft before changing the Body preset.";
                return false;
            }

            // Scene-only or temporary objects cannot be the destination of Apply.
            if (asset != null && !EditorUtility.IsPersistent(asset))
            {
                warning = "Select a Body preset saved in the Project window.";
                return false;
            }

            // Read even invalid dimensions so the session can repair an invalid asset.
            source = asset;
            Discard();
            return true;
        }

        /// <summary>Stores exactly what was entered without writing or correcting the asset.</summary>
        /// <param name="newRadius">Requested draft radius in metres.</param>
        /// <param name="newHeight">Requested draft total height in metres.</param>
        public void SetDraft(float newRadius, float newHeight)
        {
            // Keep invalid input available for an explicit correction.
            radius = newRadius;
            height = newHeight;
        }

        /// <summary>Uses the same dimension rules as a saved Body preset.</summary>
        /// <param name="settings">Receives valid preview dimensions, or default on failure.</param>
        /// <param name="warning">Receives the validation warning when the draft is invalid.</param>
        /// <returns>True when the draft can be previewed and applied.</returns>
        public bool TryGetSettings(out PlayerBodySettings settings, out string warning)
        {
            // Delegate geometry instead of maintaining a second set of rules.
            return PlayerBodySettings.TryCreate(radius, height, out settings, out warning);
        }

        /// <summary>Reloads current asset values, including edits made outside the session.</summary>
        public void Discard()
        {
            // A deleted or cleared source leaves an empty, clean session.
            if (source == null)
            {
                originalRadius = 0f;
                originalHeight = 0f;
            }
            else
            {
                // The field paths are confined to the session's serialization boundary.
                using SerializedObject serialized = new SerializedObject(source);
                ReadDimensions(serialized, out originalRadius, out originalHeight);
            }

            // Reset only the draft; Discard never writes to the source.
            SetDraft(originalRadius, originalHeight);
        }

        #endregion

        #region Apply

        /// <summary>Prepares candidate properties after checking the draft and its original asset values.</summary>
        /// <param name="changes">Receives pending properties owned and disposed by the confirmation coordinator.</param>
        /// <param name="warning">Receives the reason Apply was refused or could not finish.</param>
        /// <returns>True when the candidate is ready without writing the asset.</returns>
        public bool TryPrepareApply(out SerializedObject changes, out string warning)
        {
            // Applying in Play would make asset edits look like runtime tuning.
            warning = string.Empty;
            changes = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode || source == null)
            {
                warning = "Apply requires Edit mode and an existing Body preset.";
                return false;
            }

            // Invalid drafts remain intact; an unchanged draft has nothing to write.
            if (!TryGetSettings(out _, out warning))
                return false;

            if (!HasChanges)
                return true;

            // Compare live values at the moment of Apply, not only when opening the window.
            SerializedObject serialized = new SerializedObject(source);
            ReadDimensions(serialized, out float currentRadius, out float currentHeight);
            if (!currentRadius.Equals(originalRadius) || !currentHeight.Equals(originalHeight))
            {
                warning = "The Body changed outside this session. Discard to reload its current values.";
                serialized.Dispose();
                return false;
            }

            // The coordinator owns this separate view until the complete session is confirmed.
            changes = serialized;
            changes.FindProperty("radius").floatValue = radius;
            changes.FindProperty("height").floatValue = height;
            return true;
        }

        #endregion

        #region Serialization

        /// <summary>Reads raw dimensions so invalid assets can still be opened for correction.</summary>
        /// <param name="serialized">Fresh serialized view of the source Body.</param>
        /// <param name="readRadius">Receives the stored radius without correction.</param>
        /// <param name="readHeight">Receives the stored total height without correction.</param>
        private static void ReadDimensions(SerializedObject serialized, out float readRadius, out float readHeight)
        {
            // Read the same private fields edited by the Body's standard Inspector.
            readRadius = serialized.FindProperty("radius").floatValue;
            readHeight = serialized.FindProperty("height").floatValue;
        }

        #endregion

        #endregion
    }
}
