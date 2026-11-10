using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses between availability and an operation that has started.</summary>
    public enum HoverTextCondition { Available, InUse }

    /// <summary>Maps one local interaction state to replacement hover text.</summary>
    [Serializable]
    public sealed class HoverInteractionText
    {
        #region Fields

        [Header("Interaction Text")]
        [Tooltip("Local interaction whose state selects this label.")]
        public LocalInteractionReference Source = new LocalInteractionReference();
        [Tooltip("Available matches enabled, unlocked cards. In Use matches a running operation or a container with contents.")]
        public HoverTextCondition Condition;
        [Tooltip("Text shown when this rule matches. The first matching rule wins.")]
        public string Text = string.Empty;

        #endregion

        #region Methods

        #region Resolution

        /// <summary>Checks a cached component without searching its hierarchy during hover.</summary>
        /// <param name="source">Interaction resolved when the hover was bound.</param>
        /// <returns>True when this rule replaces the fallback label.</returns>
        internal bool Matches(ObjectInteraction source)
        {
            // Ignore channel reservations so an active scoop can still select its label.
            if (source == null || !source.Available(InteractionChannels.None))
                return false;
            if (Condition == HoverTextCondition.Available)
                return true;
            return source switch
            {
                ObjectContainer container => container.IsScooping || container.FillCount > 0,
                ObjectGravityGenerator gravity => gravity.IsSuspending,
                ObjectGrab grab => grab.IsHeld,
                _ => source.VfxDuration > 0f && source.VfxRunning
            };
        }

        /// <summary>Checks local references and label text before applying a hover draft.</summary>
        /// <param name="rules">Rules evaluated in priority order.</param>
        /// <param name="owner">Object whose local cards may be selected.</param>
        /// <param name="warning">Receives missing sources or text.</param>
        /// <returns>True when every rule is usable.</returns>
        internal static bool TryValidate(HoverInteractionText[] rules, GameObject owner, out string warning)
        {
            warning = "Choose another local interaction and enter text for each label rule.";
            if (rules == null)
                return false;
            foreach (HoverInteractionText rule in rules)
                if (rule == null || string.IsNullOrWhiteSpace(rule.Text)
                    || rule.Condition is not (HoverTextCondition.Available or HoverTextCondition.InUse)
                    || rule.Source?.Resolve(owner) is not ObjectInteraction source || source is ObjectHover)
                    return false;
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
