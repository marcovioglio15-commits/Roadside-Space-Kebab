using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses a passive ambience or an interactive spatial radio.</summary>
    public enum AmbientMode { Ambient, Radio }

    /// <summary>Defines a local ambient emitter through an Audio Studio catalog key.</summary>
    [Serializable]
    public sealed class AmbientSettings
    {
        #region Fields

        [Header("Ambient Sound")]
        [Tooltip("Ambient plays a catalog event automatically. Radio plays mus_radio and cycles local channels through a bound Button.")]
        public AmbientMode Mode;
        [Tooltip("Ambient event key selected from the project's Audio Studio catalog.")]
        public string Event = "amb_neonsign";
        [Tooltip("Maximum listening distance in metres. The event follows this object.")]
        [Range(0.1f, 50f)]
        public float Radius = 6f;
        [Tooltip("Channel values, initial station and targeting used only in Radio mode.")]
        public RadioSettings Radio = new RadioSettings();

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks the event key and local attenuation radius.</summary>
        /// <param name="warning">Receives missing or invalid configuration.</param>
        /// <returns>True when a backend can attempt playback.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Choose a supported ambient mode and a positive finite listening radius.";
            if (Mode is not (AmbientMode.Ambient or AmbientMode.Radio) || !InteractionValues.Positive(Radius))
                return false;
            if (Mode == AmbientMode.Radio)
            {
                warning = "Configure radio channels.";
                return Radio != null && Radio.TryValidate(out warning);
            }
            // Event existence is resolved by the scene's audio adapter after bank loading.
            warning = !string.IsNullOrWhiteSpace(Event) && InteractionValues.Positive(Radius)
                ? string.Empty : "Select an ambient event and a positive finite radius.";
            return warning.Length == 0;
        }

        #endregion
        #endregion
    }
}
