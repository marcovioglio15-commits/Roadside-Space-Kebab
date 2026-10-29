using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Remembers which reusable group preset was last imported into an interaction.</summary>
    [Serializable]
    internal sealed class ObjectGroupAssignment
    {
        #region Fields

        [Header("Source")]
        [Tooltip("Prefab, interaction and group linked to this preset.")]
        public string Key;
        [Tooltip("Reusable group preset.")]
        public ObjectGroupPreset Preset;
        [Tooltip("Saved preset state when it was imported.")]
        public string Baseline;

        #endregion
    }
}
