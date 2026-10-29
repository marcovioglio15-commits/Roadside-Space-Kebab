using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Saves one settings group with portable asset and object links.</summary>
    internal sealed class ObjectGroupPreset : ScriptableObject
    {
        #region Fields

        [Header("Preset")]
        [Tooltip("What this preset is for.")]
        public string Description = string.Empty;
        [Tooltip("Interaction type and fields used by this preset.")]
        [HideInInspector]
        public ObjectGroupInfo Group;
        [Tooltip("Saved values for this group.")]
        [HideInInspector]
        public ObjectPresetValue[] Values = System.Array.Empty<ObjectPresetValue>();
        [Tooltip("Mode choices used only when previewing this group on its own.")]
        [HideInInspector]
        public ObjectGroupContext Context;

        #endregion
    }
}
