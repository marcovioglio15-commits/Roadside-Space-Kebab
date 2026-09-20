using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.AudioStudio
{
    /// <summary>Identifies the external Studio project or built banks without embedding machine-specific defaults.</summary>
    [Serializable]
    public sealed class AudioConnection
    {
        #region Fields
        [Header("Source")]
        [Tooltip("Connect a Studio project; otherwise use a built-bank folder.")]
        public bool UseStudioProject = true;
        [Tooltip("Absolute path or path relative to the Unity project for a .fspro file.")]
        public string ProjectPath = "";
        [Tooltip("Built-bank directory, relative to the Unity project or absolute.")]
        public string BankPath = "";
        [Tooltip("Bank directory contains platform subdirectories.")]
        public bool BanksHavePlatforms = true;
        [Tooltip("Configure the official integration to load event banks automatically.")]
        public bool AutomaticBankLoading = true;
        #endregion
    }
}
