using System;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Identifies a tool across players, input presets and object interaction requirements.</summary>
    [CreateAssetMenu(fileName = "Player Tool", menuName = "Player Studio/Tool")]
    public sealed class PlayerTool : ScriptableObject
    {
        #region Fields

        [Header("Tool")]
        [Tooltip("Custom name displayed in Player Studio. Renaming does not affect input or interaction references.")]
        public string DisplayName = "Tool";
        [Tooltip("Optional animation played after this tool reaches its active slot.")]
        public PlayerToolAnimationPreset SwitchIn;
        [Tooltip("Optional animation played before this tool leaves its active slot.")]
        public PlayerToolAnimationPreset SwitchOut;

        [Header("Use")]
        [Tooltip("Optional transform animation played when the active tool is used. Its starting poses return when playback finishes.")]
        public PlayerToolAnimationPreset UseAnimation;
        [Tooltip("Minimum scaled seconds between accepted uses, in addition to any running use animation.")]
        [Range(0f, 5f)]
        public float UseInterval = 0.15f;

        [Header("Weapon")]
        [Tooltip("Fire the configured bullet prefab when this tool is used.")]
        public bool IsWeapon;
        [Tooltip("Interactions accepting this tool and bound to the player's Use Tool action receive bullet impacts instead of input presses. Their targeting distance is ignored for a hit.")]
        public bool InteractOnBulletImpact;
        [Tooltip("Muzzle, straight projectile, impact and optional cursor aiming settings.")]
        public PlayerToolWeaponSettings Weapon = new PlayerToolWeaponSettings();

        [Header("Reload")]
        [Tooltip("Allow the reload command to play the ordered transform animation combo. No ammunition system is required.")]
        public bool ReloadEnabled;
        [Tooltip("Play the reload combo automatically after each accepted use finishes. Manual reload remains available.")]
        public bool AutoReload;
        [Tooltip("Animation steps played in order. Tracks may target the arm or any child below Hierarchy Root.")]
        public PlayerToolAnimationPreset[] ReloadCombo = Array.Empty<PlayerToolAnimationPreset>();

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks only enabled behaviors without rewriting unused settings.</summary>
        /// <param name="switching">Whether this player allows switch animations.</param>
        /// <param name="warning">Receives the first invalid behavior.</param>
        /// <returns>True when this tool can be bound for the selected player mode.</returns>
        public bool TryValidate(bool switching, out string warning)
        {
            // Hidden options do not prevent single-tool use.
            warning = "Use a tool name and a finite, non-negative use interval.";
            if (string.IsNullOrWhiteSpace(DisplayName) || !float.IsFinite(UseInterval) || UseInterval < 0f)
                return false;
            if (switching && (SwitchIn != null && !SwitchIn.TryValidate(out warning)
                || SwitchOut != null && !SwitchOut.TryValidate(out warning))
                || UseAnimation != null && !UseAnimation.TryValidate(out warning)
                || IsWeapon && (Weapon == null || !Weapon.TryValidate(out warning)))
                return false;
            if (ReloadEnabled)
            {
                warning = "An enabled reload combo requires at least one assigned animation step.";
                if (ReloadCombo == null || ReloadCombo.Length == 0)
                    return false;
                foreach (PlayerToolAnimationPreset step in ReloadCombo)
                    if (step == null || !step.TryValidate(out warning))
                        return false;
            }
            warning = string.Empty;
            return true;
        }

        /// <summary>Reports invalid enabled settings when the asset is edited directly.</summary>
        private void OnValidate()
        {
            // Validation keeps the original values available for Undo and correction.
            if (!TryValidate(false, out string warning))
                Debug.LogWarning(warning, this);
        }

        #endregion

        #endregion
    }
}
