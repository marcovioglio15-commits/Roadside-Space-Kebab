using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Controls a contact volume that removes objects and returns the player to their initial pose.</summary>
    [Serializable]
    public sealed class DeathboxSettings
    {
        #region Fields

        [Header("Contact")]
        [Tooltip("Collider layers affected by this death volume, including the player layer when player return is wanted.")]
        public LayerMask Layers = ~0;
        [Tooltip("Return a contacted player to the initial scene pose instead of destroying the player.")]
        public bool AffectPlayer = true;
        [Tooltip("Allow objects marked Static to be removed. Disable when the volume can touch permanent level geometry.")]
        public bool AffectStatic;
        [Tooltip("Seconds between overlap checks, including kinematic objects and controllers without collision callbacks.")]
        public float QueryInterval = 0.05f;

        #endregion
        #region Methods
        #region Validation

        /// <summary>Checks contact timing without changing authored settings.</summary>
        /// <param name="warning">Receives invalid timing or an empty layer selection.</param>
        /// <returns>True when the volume can query contacts.</returns>
        public bool TryValidate(out string warning)
        {
            warning = Layers.value == 0 || !InteractionValues.Positive(QueryInterval)
                ? "Deathbox needs affected layers and a positive finite query interval." : string.Empty;
            return warning.Length == 0;
        }

        #endregion
        #endregion
    }
}
