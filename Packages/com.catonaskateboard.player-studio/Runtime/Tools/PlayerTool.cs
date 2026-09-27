using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Identifies a tool across players, input presets and object interaction requirements.</summary>
    [CreateAssetMenu(fileName = "Player Tool", menuName = "Player Studio/Tool")]
    public sealed class PlayerTool : ScriptableObject
    {
        #region Fields

        [Header("Tool")]
        [Tooltip("Custom name displayed in Player Studio. Renaming does not affect input or interaction references.")]
        public string DisplayName = "Tool";
        [Tooltip("Optional animation played after this tool reaches its active slot.")]
        public PlayerToolAnimationPreset SwitchIn;
        [Tooltip("Optional animation played before this tool leaves its active slot.")]
        public PlayerToolAnimationPreset SwitchOut;

        #endregion
    }
}
