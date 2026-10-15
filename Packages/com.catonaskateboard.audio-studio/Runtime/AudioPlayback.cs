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
        [Tooltip("Enable playback through the scene audio adapter.")]
        public bool Enabled = true;
        [Tooltip("Global event volume multiplier.")]
        [Range(0f, 1f)]
        public float MasterVolume = 1f;
        [Tooltip("Default near attenuation distance.")]
        [Range(0f, 100f)]
        public float MinimumDistance = 8f;
        [Tooltip("Default far attenuation distance.")]
        [Range(1f, 200f)]
        public float MaximumDistance = 45f;
        [Tooltip("Request diagnostics for unresolved event paths.")]
        public bool LogMissingEvents = true;
        #endregion
    }
}
