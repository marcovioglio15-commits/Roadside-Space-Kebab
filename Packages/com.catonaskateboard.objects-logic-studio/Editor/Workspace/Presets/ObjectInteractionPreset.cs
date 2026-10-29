using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Composes the reusable groups for one interaction while preserving its complete local settings.</summary>
    internal sealed class ObjectInteractionPreset : ScriptableObject
    {
        #region Fields

        [Header("Interaction")]
        [Tooltip("What this interaction preset is for.")]
        public string Description = string.Empty;
        [Tooltip("Original main preset, when one was assigned.")]
        public ScriptableObject SourcePreset;
        [Tooltip("Presets for this interaction's settings groups.")]
        public ObjectGroupPreset[] Groups = Array.Empty<ObjectGroupPreset>();
        [Tooltip("Complete settings captured from the interaction.")]
        [HideInInspector]
        public ObjectComponentPreset Configuration = new ObjectComponentPreset();

        #endregion
    }
}
