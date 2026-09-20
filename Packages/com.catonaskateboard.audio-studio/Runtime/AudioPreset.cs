using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.AudioStudio
{
    /// <summary>Stores project-owned FMOD authoring data; it never dispatches gameplay events.</summary>
    [CreateAssetMenu(menuName = "Studio/Audio Preset", fileName = "AudioPreset")]
    public sealed class AudioPreset : ScriptableObject
    {
        #region Fields
        [Header("Metadata")]
        [Tooltip("Purpose of this audio setup.")]
        public string Description = "";
        [Tooltip("Authoring revision for this preset.")]
        public string Version = "1.0.0";
        [Header("FMOD")]
        [Tooltip("Studio project or built-bank source.")]
        public AudioConnection Connection = new AudioConnection();
        [Tooltip("Default playback values for a future project adapter.")]
        public AudioPlayback Playback = new AudioPlayback();
        [Tooltip("Bus paths and mix defaults.")]
        public AudioRouting Routing = new AudioRouting();
        [Header("Music")]
        [Tooltip("Crossfade duration for future music transitions, in real seconds.")]
        public float MusicCrossfadeSeconds = 1.5f;
        [Tooltip("Named music contexts; add only those your project needs.")]
        public List<AudioMusic> Music = new List<AudioMusic>();
        [Header("Events")]
        [Tooltip("Project-specific event keys; starts empty and has no gameplay connections.")]
        public List<AudioBinding> Events = new List<AudioBinding>();
        #endregion
    }
}
