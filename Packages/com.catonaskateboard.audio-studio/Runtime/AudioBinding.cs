using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.AudioStudio
{
    /// <summary>Maps one project-defined key to a Studio event and its authored playback options.</summary>
    [Serializable]
    public sealed class AudioBinding
    {
        #region Fields
        [Header("Identity")]
        [Tooltip("Unique project-defined event key; no built-in gameplay IDs.")]
        public string Key = "";
        [Tooltip("Label shown in the event list.")]
        public string DisplayName = "";
        [Tooltip("When this event is intended to be used.")]
        public string Description = "";
        [Header("FMOD Event")]
        [Tooltip("FMOD event:/ path selected from the catalog.")]
        public string EventPath = "";
        [Tooltip("Studio GUID captured during catalog assignment.")]
        public string EventGuid = "";
        [Tooltip("Event volume multiplier.")]
        public float Volume = 1f;
        [Tooltip("Event pitch multiplier; must be positive.")]
        public float Pitch = 1f;
        [Tooltip("Expose 3D position and attenuation options.")]
        public bool Spatialize = true;
        [Tooltip("Use event distances instead of preset defaults.")]
        public bool OverrideDistances = false;
        [Tooltip("Near attenuation distance.")]
        public float MinimumDistance = 8f;
        [Tooltip("Far attenuation distance.")]
        public float MaximumDistance = 45f;
        [Tooltip("Request at most one simultaneous voice for this key.")]
        public bool SingleInstance = false;
        [Header("Rate Limit")]
        [Tooltip("Enable a per-key repetition cap.")]
        public bool LimitRate = false;
        [Tooltip("Maximum plays inside the configured window.")]
        public int MaxPlays = 8;
        [Tooltip("Rate-limit interval in seconds.")]
        public float WindowSeconds = 0.25f;
        [Header("Parameters")]
        [Tooltip("Authored local or global FMOD parameter values.")]
        public List<AudioParameter> Parameters = new List<AudioParameter>();
        #endregion
    }
}
