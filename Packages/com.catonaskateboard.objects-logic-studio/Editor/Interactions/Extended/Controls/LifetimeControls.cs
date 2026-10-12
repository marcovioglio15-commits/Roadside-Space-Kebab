using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares lifetime controls between interaction drafts and reusable presets.</summary>
    internal static class LifetimeControls
    {
        #region Methods
        #region Configuration

        /// <summary>Edits which contacts remove objects or return the player.</summary>
        /// <param name="settings">Detached volume configuration.</param>
        internal static void Deathbox(SerializedProperty settings)
        {
            // Collider geometry is authored on the object rather than generated during gameplay.
            HoverControls.Field(settings, "Layers");
            HoverControls.Field(settings, "AffectPlayer");
            HoverControls.Field(settings, "AffectStatic");
            HoverControls.Field(settings, "QueryInterval");
        }

        /// <summary>Exposes explicit flag selection only when automatic identity matching is disabled.</summary>
        /// <param name="settings">Detached population policy.</param>
        internal static void PresenceCap(SerializedProperty settings)
        {
            // Multiple selected flags define separate counts, not one combined population.
            HoverControls.Field(settings, "Maximum");
            HoverControls.Field(settings, "IncludeFixedObjects");
            HoverControls.Field(settings, "UseOwnFlags");
            if (!settings.FindPropertyRelative("UseOwnFlags").boolValue)
                ObjectFlagSelector.Draw(settings.FindPropertyRelative("Flags"));
            HoverControls.Field(settings, "OffscreenDelay");
        }

        #endregion
        #endregion
    }
}
