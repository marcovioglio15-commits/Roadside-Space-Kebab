using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Keeps complete serialized settings for a shared component or interaction.</summary>
    [Serializable]
    internal sealed class ObjectComponentPreset
    {
        #region Fields

        [Header("Component")]
        [Tooltip("Component type used by these settings.")]
        public string TypeName;
        [Tooltip("Saved fields, including settings hidden by the current mode.")]
        public ObjectPresetValue[] Fields = Array.Empty<ObjectPresetValue>();

        #endregion
    }
}
