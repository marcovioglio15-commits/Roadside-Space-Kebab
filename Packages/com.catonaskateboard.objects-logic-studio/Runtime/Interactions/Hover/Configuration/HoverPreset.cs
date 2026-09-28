using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares one hover configuration among independently bound prefab interactions.</summary>
    [CreateAssetMenu(fileName = "Hover Preset", menuName = "Objects Logic Studio/Hover Preset")]
    public sealed class HoverPreset : ScriptableObject
    {
        #region Serialized Fields

        [Header("Configuration")]
        [Tooltip("Reusable detection, animation, text and background values. Object references remain on each interaction.")]
        [SerializeField]
        private HoverConfiguration configuration = new HoverConfiguration();

        #endregion

        #region Tool Requirement

        [Header("Player Tool")]
        [Tooltip("Tool restriction copied into an interaction by Import; existing object snapshots remain independent.")]
        public InteractionToolRequirement ToolRequirement = new InteractionToolRequirement();

        #endregion

        #region Properties

        /// <summary>Saved values read by interactions at activation and edited through a detached workspace draft.</summary>
        public HoverConfiguration Configuration => configuration;

        #endregion
    }
}
