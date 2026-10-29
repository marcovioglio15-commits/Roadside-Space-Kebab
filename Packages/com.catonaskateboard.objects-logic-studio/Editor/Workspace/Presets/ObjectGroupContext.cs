using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares preview-only mode dependencies between groups captured from one interaction.</summary>
    internal sealed class ObjectGroupContext : ScriptableObject
    {
        #region Fields

        [Header("Preview")]
        [Tooltip("Mode choices used when editing a group on its own.")]
        [HideInInspector]
        public ObjectPresetValue[] Values = Array.Empty<ObjectPresetValue>();

        #endregion
    }
}
