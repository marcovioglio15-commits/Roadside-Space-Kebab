using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.AudioStudio
{
    /// <summary>Defines default mix and attenuation settings for project adapters.</summary>
    [Serializable]
    public sealed class AudioPlayback
    {
        #region Fields
        [Header("Playback")]
        [Tooltip("Enable playback in a future project adapter.")]
        public bool Enabled = true;
        [Tooltip("Global event volume multiplier.")]
        public float MasterVolume = 1f;
        [Tooltip("Default near attenuation distance.")]
        public float MinimumDistance = 8f;
        [Tooltip("Default far attenuation distance.")]
        public float MaximumDistance = 45f;
        [Tooltip("Request diagnostics for unresolved event paths.")]
        public bool LogMissingEvents = true;
        #endregion
    }
}
