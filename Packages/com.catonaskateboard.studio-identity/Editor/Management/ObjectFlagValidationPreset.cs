using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Saves the validation tab's asset scope and cleanup policy.</summary>
    [CreateAssetMenu(menuName = "Studio Identity/Validation Preset", fileName = "Identity Flag Validation")]
    public sealed class ObjectFlagValidationPreset : ScriptableObject
    {
        #region Fields

        [Header("Scope")]
        [Tooltip("Search result paths and property names.")]
        public string Search = string.Empty;
        [Tooltip("Inspect scene files as well as prefabs and other assets.")]
        public bool IncludeScenes = true;
        [Tooltip("Inspect package assets too. Read-only results can be inspected but cannot be cleaned.")]
        public bool IncludePackages = true;
        [Header("Cleanup")]
        [Tooltip("Include empty slots in flag arrays. Optional empty single-flag fields are preserved.")]
        public bool EmptyFlagSlots = true;
        [Tooltip("Expanded result paths retained by this validation preset.")]
        [HideInInspector]
        public System.Collections.Generic.List<string> Expanded = new System.Collections.Generic.List<string>();

        #endregion
    }
}
