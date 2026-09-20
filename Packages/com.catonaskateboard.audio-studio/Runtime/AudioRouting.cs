using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.AudioStudio
{
    /// <summary>Keeps independent bus assignments and default mixer levels.</summary>
    [Serializable]
    public sealed class AudioRouting
    {
        #region Fields
        [Header("Buses")]
        [Tooltip("Master FMOD bus path.")]
        public string MasterBus = "bus:/";
        [Tooltip("Sound-effects bus path.")]
        public string SfxBus = "bus:/SFX";
        [Tooltip("Music bus path.")]
        public string MusicBus = "bus:/Music";
        [Tooltip("Default sound-effects level.")]
        public float SfxVolume = 1f;
        [Tooltip("Default music level.")]
        public float MusicVolume = 1f;
        #endregion
    }
}
