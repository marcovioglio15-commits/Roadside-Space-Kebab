using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Defines a local ambient emitter through an Audio Studio catalog key.</summary>
    [Serializable]
    public sealed class AmbientSettings
    {
        #region Fields

        [Header("Ambient Sound")]
        [Tooltip("Ambient event key selected from the project's Audio Studio catalog.")]
        public string Event = "amb_neonsign";
        [Tooltip("Maximum listening distance in metres. The event follows this object.")]
        public float Radius = 6f;

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks the event key and local attenuation radius.</summary>
        /// <param name="warning">Receives missing or invalid configuration.</param>
        /// <returns>True when a backend can attempt playback.</returns>
        public bool TryValidate(out string warning)
        {
            // Event existence is resolved by the scene's audio adapter after bank loading.
            warning = !string.IsNullOrWhiteSpace(Event) && InteractionValues.Positive(Radius)
                ? string.Empty : "Select an ambient event and a positive finite radius.";
            return warning.Length == 0;
        }

        #endregion
        #endregion
    }
}
