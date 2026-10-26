using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Maps one player-selectable station to a local FMOD channel value.</summary>
    [Serializable]
    public sealed class RadioChannel
    {
        #region Fields

        [Tooltip("Name identifying this entry in the radio's channel cycle.")]
        public string Name = "Channel";
        [Tooltip("Local FMOD channel value. Zero stops this radio; values above zero select its authored channel regions.")]
        [Range(0f, 3f)]
        public float Value;

        #endregion
    }

    /// <summary>Configures a spatial radio's local parameter, initial station and player targeting.</summary>
    [Serializable]
    public sealed class RadioSettings
    {
        #region Fields

        [Header("Radio")]
        [Tooltip("Keep the main background music independent of this radio, including its OFF channel. Disable to mute it while this radio is available.")]
        public bool SeparateBackgroundMusic = true;
        [Tooltip("Local parameter controlling mus_radio. The current banks expose Channels from zero to three.")]
        public string Parameter = "Channels";
        [Tooltip("Entry selected when gameplay begins. Ordinary disable and enable preserves the selected channel.")]
        [Range(0, 16)]
        public int InitialChannel = 1;
        [Tooltip("Ordered entries cycled by the bound Button action. Their values define the splits between authored FMOD channel regions.")]
        public RadioChannel[] Channels =
        {
            new RadioChannel { Name = "OFF", Value = 0f },
            new RadioChannel { Name = "ONE", Value = 1f },
            new RadioChannel { Name = "TWO", Value = 2f },
            new RadioChannel { Name = "THREE", Value = 3f }
        };
        [Tooltip("Reach, aiming and obstruction requirements for changing this radio's channel.")]
        public TransferTargetSettings Target = new TransferTargetSettings();

        #endregion

        #region Methods
        #region Validation

        /// <summary>Checks active channel entries and targeting without altering their authored values.</summary>
        /// <param name="warning">Receives an invalid channel, initial index or target setting.</param>
        /// <returns>True when the radio can start and cycle its configured channels.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Choose a local radio parameter, one to seventeen named channels, and an initial entry from that list.";
            if (string.IsNullOrWhiteSpace(Parameter) || Channels == null || Channels.Length < 1 || Channels.Length > 17
                || InitialChannel < 0 || InitialChannel >= Channels.Length)
                return false;
            foreach (RadioChannel channel in Channels)
                if (channel == null || string.IsNullOrWhiteSpace(channel.Name) || !float.IsFinite(channel.Value) || channel.Value < 0f || channel.Value > 3f)
                    return false;
            warning = "Configure radio interaction targeting.";
            return Target != null && Target.TryValidate(out warning);
        }

        #endregion
        #endregion
    }
}
