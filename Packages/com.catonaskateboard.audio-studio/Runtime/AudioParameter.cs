using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.AudioStudio
{
    /// <summary>Stores an explicitly authored FMOD parameter value.</summary>
    [Serializable]
    public sealed class AudioParameter
    {
        #region Fields
        [Header("Parameter")]
        [Tooltip("Exact Studio parameter name.")]
        public string Name = "";
        [Tooltip("Value supplied to this parameter.")]
        public float Value = 0f;
        [Tooltip("The parameter affects the Studio system instead of one event instance.")]
        public bool Global = false;
        #endregion
    }
}
