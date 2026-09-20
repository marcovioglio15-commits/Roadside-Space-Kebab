using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.AudioStudio
{
    /// <summary>Defines a reusable music context without game-specific state detection.</summary>
    [Serializable]
    public sealed class AudioMusic
    {
        #region Fields
        [Header("Identity")]
        [Tooltip("Unique project context, such as Menu, Gameplay or Boss.")]
        public string Context = "";
        [Tooltip("Enable this music context.")]
        public bool Enabled = true;
        [Header("Music")]
        [Tooltip("Music event:/ path from the catalog.")]
        public string EventPath = "";
        [Tooltip("Studio event GUID.")]
        public string EventGuid = "";
        [Tooltip("Bank containing the music event.")]
        public string Bank = "";
        [Tooltip("Music event volume.")]
        public float Volume = 1f;
        [Tooltip("Request playback when the project enters this context.")]
        public bool AutoStart = true;
        [Tooltip("Restart when the selected event changes.")]
        public bool RestartWhenPathChanges = true;
        [Tooltip("Stop this context when disabled.")]
        public bool StopWhenDisabled = true;
        #endregion
    }
}
