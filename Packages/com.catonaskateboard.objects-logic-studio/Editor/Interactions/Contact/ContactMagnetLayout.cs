using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Retains the complete independent contact layout, including filters, priorities and background geometry.</summary>
    [Serializable]
    internal sealed class ContactMagnetLayout
    {
        #region Fields

        [Header("Layout")]
        [Tooltip("Independent contact layout edited in the placement preview.")]
        public ContactMagnetSettings Settings = new ContactMagnetSettings();
        [Tooltip("Opening values used to detect concurrent layout changes.")]
        [SerializeField]
        private string baseline = string.Empty;

        #endregion
        #region Properties

        /// <summary>Whether placement, priority, filtering or background geometry changed.</summary>
        internal bool HasChanges => Capture(Settings) != baseline;

        #endregion
        #region Methods
        #region Transactions

        /// <summary>Copies the source layout without sharing mutable flag selections.</summary>
        /// <param name="settings">Current source settings, including pending workspace edits.</param>
        internal void Read(ContactMagnetSettings settings)
        {
            // Timing remains in the source form; this draft owns only layout fields.
            Settings = ObjectWorkspace.Copy(settings);
            baseline = Capture(settings);
        }

        /// <summary>Commits layout fields after checking their source baseline and configuration.</summary>
        /// <param name="current">Current workspace docking settings.</param>
        internal void Apply(ContactMagnetSettings current)
        {
            // Another window may edit timing without conflicting with these placements.
            if (Capture(current) != baseline)
                throw new InvalidOperationException("This contact layout changed in another window. Discard to reload it.");
            ContactMagnetSettings proposal = ObjectWorkspace.Copy(current);
            proposal.Magnets = ObjectWorkspace.Copy(Settings).Magnets;
            proposal.PreviewPrefab = Settings.PreviewPrefab;
            if (!proposal.TryValidate(out string warning))
                throw new InvalidOperationException(warning);
            current.Magnets = proposal.Magnets;
            current.PreviewPrefab = proposal.PreviewPrefab;
        }

        /// <summary>Compares only fields owned by this preview.</summary>
        /// <param name="settings">Contact layout to compare.</param>
        /// <returns>Stable serialized layout fields.</returns>
        private static string Capture(ContactMagnetSettings settings)
        {
            return JsonUtility.ToJson(new ContactMagnetSettings { Magnets = settings.Magnets, PreviewPrefab = settings.PreviewPrefab });
        }

        #endregion
        #endregion
    }
}
