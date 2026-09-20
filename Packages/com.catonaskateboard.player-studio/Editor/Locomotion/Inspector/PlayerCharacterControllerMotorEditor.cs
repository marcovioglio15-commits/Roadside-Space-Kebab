using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Displays the difference between requested movement and movement achieved after collisions.</summary>
    [CustomEditor(typeof(PlayerCharacterControllerMotor))]
    internal sealed class PlayerCharacterControllerMotorEditor : UnityEditor.Editor
    {
        #region Labels

        private static readonly GUIContent requestedLabel = new GUIContent("Planar Velocity", "Planar world velocity requested by the movement calculation, before collisions.");
        private static readonly GUIContent actualLabel = new GUIContent("Actual Velocity", "World displacement achieved by the latest motor update, divided by its duration.");

        private static readonly GUIContent verticalLabel = new GUIContent("Vertical Velocity", "Vertical speed: positive while rising, negative while falling; reset by floor or ceiling contact.");
        private static readonly GUIContent groundedLabel = new GUIContent("Grounded", "Contact below from the most recent Move while not ascending. This is not a separate ground probe.");
        private static readonly GUIContent collisionLabel = new GUIContent("Collisions", "Directions blocked during the latest combined movement request.");

        #endregion

        #region Methods

        #region Inspector

        /// <summary>Shows normal setup fields in Edit mode and cached diagnostics during Play.</summary>
        public override void OnInspectorGUI()
        {
            // Runtime reference replacement requires explicit initialization, so keep setup fields locked during Play.
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                DrawDefaultInspector();
            if (!Application.isPlaying)
                return;

            // Read existing values without validating configuration or looking up scene components each frame.
            PlayerCharacterControllerMotor motor = (PlayerCharacterControllerMotor)target;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector3Field(requestedLabel, motor.RequestedVelocity);
                EditorGUILayout.Vector3Field(actualLabel, motor.ActualVelocity);
                EditorGUILayout.FloatField(verticalLabel, motor.VerticalVelocity);
                EditorGUILayout.Toggle(groundedLabel, motor.IsGrounded);
                EditorGUILayout.EnumFlagsField(collisionLabel, motor.Collisions);
            }

            if (motor.InitializationWarning.Length > 0)
                EditorGUILayout.HelpBox(motor.InitializationWarning, MessageType.Warning);
        }

        /// <summary>Refreshes visible diagnostics while the game can move the player.</summary>
        /// <returns>True only during Play, without creating runtime UI.</returns>
        public override bool RequiresConstantRepaint()
        {
            // Only the selected Editor Inspector requests continuous repainting.
            return Application.isPlaying;
        }

        #endregion

        #endregion
    }
}
