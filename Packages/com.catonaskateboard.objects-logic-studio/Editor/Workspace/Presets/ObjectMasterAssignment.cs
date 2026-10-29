using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Remembers which master was selected for an individual object in the workspace.</summary>
    [Serializable]
    internal sealed class ObjectMasterAssignment
    {
        [Header("Selection")]
        [Tooltip("Object linked to this master selection.")]
        public string Key = string.Empty;
        [Tooltip("Master selected for this object.")]
        public ObjectMasterPreset Preset;
    }
}
