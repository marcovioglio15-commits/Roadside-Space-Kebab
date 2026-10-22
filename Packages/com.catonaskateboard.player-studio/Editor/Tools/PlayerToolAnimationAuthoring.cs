using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Creates editable keyframe presets for a vertical or horizontal player arm idle.</summary>
    internal static class PlayerToolAnimationAuthoring
    {
        #region Methods

        #region Creation

        /// <summary>Saves a smooth five-key idle with equal endpoints and start-relative coordinates.</summary>
        /// <param name="horizontal">True for horizontal sway, false for vertical sway.</param>
        /// <returns>Saved animation, or null when the save dialog is canceled.</returns>
        internal static PlayerToolAnimationPreset CreateIdle(bool horizontal)
        {
            PlayerToolAnimationPreset animation = PlayerToolsAuthoring.Create<PlayerToolAnimationPreset>(horizontal ? "Arm Horizontal Idle" : "Arm Vertical Idle");
            if (animation == null)
                return null;
            Undo.RecordObject(animation, "Configure Arm Idle");
            animation.Duration = 2f;
            PlayerToolKey[] keys = new PlayerToolKey[5];
            // A full cycle crosses its neutral pose twice and returns without an endpoint jump.
            for (int index = 0; index < keys.Length; index++)
            {
                PlayerToolPose pose = PlayerToolPose.Identity;
                pose.Position = (horizontal ? Vector3.right : Vector3.up) * (index switch { 1 => 0.015f, 3 => -0.015f, _ => 0f });
                keys[index] = new PlayerToolKey { Time = index * 0.5f, Pose = pose };
            }
            animation.Tracks = new[] { new PlayerToolTrack { RelativeToStart = true, Keys = keys } };
            EditorUtility.SetDirty(animation);
            AssetDatabase.SaveAssetIfDirty(animation);
            return animation;
        }

        #endregion

        #endregion
    }
}
