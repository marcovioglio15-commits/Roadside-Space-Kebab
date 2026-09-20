using UnityEditor;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Edits a saved locomotion preset directly with the same conditional gravity fields as the session.</summary>
    [CustomEditor(typeof(PlayerLocomotionPreset))]
    internal sealed class PlayerLocomotionPresetEditor : UnityEditor.Editor
    {
        #region Methods

        #region Drawing

        /// <summary>Keeps inactive gravity numbers hidden and records normal asset Undo through serialized properties.</summary>
        public override void OnInspectorGUI()
        {
            // Inspector edits are immediate asset edits; Player Studio owns the separate Apply/Discard draft.
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "useGravity", "gravityAcceleration", "terminalSpeed", "groundSpeed",
                "useJump", "jumpHeight", "jumpBufferTime", "coyoteTime");
            bool enabled = serializedObject.FindProperty("useGravity").boolValue;
            float acceleration = serializedObject.FindProperty("gravityAcceleration").floatValue;
            float terminalSpeed = serializedObject.FindProperty("terminalSpeed").floatValue;
            float groundSpeed = serializedObject.FindProperty("groundSpeed").floatValue;
            PlayerGravityControls.Draw(ref enabled, ref acceleration, ref terminalSpeed, ref groundSpeed);

            bool useJump = serializedObject.FindProperty("useJump").boolValue;
            float height = serializedObject.FindProperty("jumpHeight").floatValue;
            float bufferTime = serializedObject.FindProperty("jumpBufferTime").floatValue;
            float coyoteTime = serializedObject.FindProperty("coyoteTime").floatValue;
            PlayerJumpControls.Draw(enabled, ref useJump, ref height, ref bufferTime, ref coyoteTime);

            // Applying serialized properties supplies Unity's regular Inspector Undo without a second history.
            serializedObject.FindProperty("useGravity").boolValue = enabled;
            serializedObject.FindProperty("gravityAcceleration").floatValue = acceleration;
            serializedObject.FindProperty("terminalSpeed").floatValue = terminalSpeed;
            serializedObject.FindProperty("groundSpeed").floatValue = groundSpeed;
            serializedObject.FindProperty("useJump").boolValue = useJump;
            serializedObject.FindProperty("jumpHeight").floatValue = height;
            serializedObject.FindProperty("jumpBufferTime").floatValue = bufferTime;
            serializedObject.FindProperty("coyoteTime").floatValue = coyoteTime;
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #endregion
    }
}
