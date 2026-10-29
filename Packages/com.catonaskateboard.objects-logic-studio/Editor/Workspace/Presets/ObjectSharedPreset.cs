using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Saves identity or shared item settings independently of interaction presets.</summary>
    internal sealed class ObjectSharedPreset : ScriptableObject
    {
        #region Fields

        [Header("Shared settings")]
        [Tooltip("Shared components included in this preset.")]
        [HideInInspector]
        public ObjectComponentPreset[] Components = Array.Empty<ObjectComponentPreset>();

        #endregion
    }
}
