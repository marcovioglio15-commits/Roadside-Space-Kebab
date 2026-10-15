using System;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Selects whether an exposed target keeps the ordinary trail or receives a dedicated deposit lifetime.</summary>
    public enum SprayTrailMode { Normal, Timed, Permanent }

    /// <summary>Applies reusable surface effects after continuous wetting of a flag-matched object.</summary>
    [Serializable]
    public sealed class SpraySurfaceRule
    {
        #region Fields
        [Header("Surface Rule")]
        [Tooltip("Identity flags required on the receiving object.")]
        public ObjectFlag[] Flags = Array.Empty<ObjectFlag>();
        [Tooltip("Accept any selected flag or require every selected flag.")]
        public ObjectFlagMatch Match;
        [Tooltip("Seconds of continuous wetting before the surface effects become active.")]
        [Range(0.01f, 10f)]
        public float Exposure = 0.5f;
        [Tooltip("Maximum gap between hits before unfinished exposure restarts.")]
        [Range(0.01f, 2f)]
        public float ContactGap = 0.2f;
        [Tooltip("Mesh, material and visibility changes on the receiving item's existing hierarchy. Leave empty to use only tint or deposits.")]
        public ItemAppearanceSettings Appearance = new ItemAppearanceSettings();
        [Tooltip("Optional color multiplication using the same material property handling as Modify By Contact. Applied once per receiving object, not once per droplet.")]
        public ContactEffects Tint = new ContactEffects();
        [Tooltip("Normal uses the general trail toggle and lifetime. Timed or Permanent deposits the same dirt after exposure, even when ordinary trails are disabled.")]
        public SprayTrailMode TrailMode;
        [Tooltip("Lifetime in seconds for Timed deposits on matching objects.")]
        [Range(0.1f, 300f)]
        public float TrailLifetime = 30f;
        [Tooltip("Final part of the custom lifetime spent fading to transparent.")]
        [Range(0.1f, 120f)]
        public float TrailFadeDuration = 6f;
        #endregion
        #region Methods
        #region Validation
        /// <summary>Checks only selected effects and reports invalid authoring without changing values.</summary>
        /// <param name="warning">Receives the first invalid filter, duration or effect.</param>
        /// <returns>True when this rule can respond to a matching surface.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Use positive exposure and contact gap, and choose Any or All for the flag filter.";
            if (!InteractionValues.Positive(Exposure) || !InteractionValues.Positive(ContactGap)
                || Match is not (ObjectFlagMatch.Any or ObjectFlagMatch.All))
                return false;
            if (!ObjectFlagRules.TryValidate(Flags, false, out warning))
                return false;
            warning = "Configure appearance, tint or a dedicated trail lifetime for this surface rule.";
            if (Appearance == null || Tint == null || !Appearance.HasChanges && !Tint.Tint && TrailMode == SprayTrailMode.Normal)
                return false;
            if (!Appearance.TryValidate(out warning) || !Tint.TryValidate(out warning))
                return false;
            warning = "Surface tint cannot consume an item or replace meshes; use Appearance for replacements.";
            if (Tint.Consume || Tint.Meshes.Length > 0)
                return false;
            warning = "Choose a trail mode and use positive custom lifetime and fade, with fade no longer than lifetime.";
            if (TrailMode is not (SprayTrailMode.Normal or SprayTrailMode.Timed or SprayTrailMode.Permanent)
                || TrailMode == SprayTrailMode.Timed && (!InteractionValues.Positive(TrailLifetime)
                    || !InteractionValues.Positive(TrailFadeDuration) || TrailFadeDuration > TrailLifetime))
                return false;
            warning = string.Empty;
            return true;
        }
        #endregion
        #endregion
    }
}
