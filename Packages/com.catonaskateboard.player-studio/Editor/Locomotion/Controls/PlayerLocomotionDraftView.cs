using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Edits movement and optional gravity on the window's draft, keeping the shared asset unchanged.</summary>
    internal static class PlayerLocomotionDraftView
    {
        #region Labels

        private static readonly GUIContent speedLabel = new GUIContent("Speed (m/s)", "Maximum speed at full directional input; zero prevents movement.");
        private static readonly GUIContent accelerationLabel = new GUIContent("Acceleration (m/s²)", "Velocity response rate while a direction is requested, including turns.");
        private static readonly GUIContent decelerationLabel = new GUIContent("Deceleration (m/s²)", "Braking rate when directional input is released.");

        #endregion

        #region Methods

        #region Drawing

        /// <summary>Records one numeric edit on the owning window for the native Undo command.</summary>
        /// <param name="session">Draft opened through the current master's Locomotion slot.</param>
        /// <param name="owner">Window whose serialized fields are recorded before the edit.</param>
        /// <param name="sections">Retained visibility of movement, gravity and jump groups.</param>
        /// <returns>True when a movement value changed in this GUI event.</returns>
        public static bool Draw(PlayerLocomotionEditSession session, Object owner, PlayerStudioSections sections)
        {
            // All fields are independent values; none is corrected by the controls.
            EditorGUI.BeginChangeCheck();
            float speed = session.Speed;
            float acceleration = session.Acceleration;
            float deceleration = session.Deceleration;
            if (sections.Draw("Locomotion.Movement", "Movement"))
                using (new EditorGUI.IndentLevelScope())
                {
                    speed = EditorGUILayout.FloatField(speedLabel, speed);
                    acceleration = EditorGUILayout.FloatField(accelerationLabel, acceleration);
                    deceleration = EditorGUILayout.FloatField(decelerationLabel, deceleration);
                }
            bool useGravity = session.UseGravity;
            float gravityAcceleration = session.GravityAcceleration;
            float terminalSpeed = session.TerminalSpeed;
            float groundSpeed = session.GroundSpeed;
            if (sections.Draw("Locomotion.Gravity", "Gravity"))
                using (new EditorGUI.IndentLevelScope())
                    PlayerGravityControls.Draw(ref useGravity, ref gravityAcceleration, ref terminalSpeed, ref groundSpeed);
            bool useJump = session.UseJump;
            float height = session.JumpHeight;
            float bufferTime = session.JumpBufferTime;
            float coyoteTime = session.CoyoteTime;
            if ((useGravity || useJump) && sections.Draw("Locomotion.Jump", "Jump"))
                using (new EditorGUI.IndentLevelScope())
                    PlayerJumpControls.Draw(useGravity, ref useJump, ref height, ref bufferTime, ref coyoteTime);
            if (!EditorGUI.EndChangeCheck())
                return false;

            Undo.RecordObject(owner, "Edit Locomotion Draft");
            session.SetDraft(speed, acceleration, deceleration);
            session.SetGravityDraft(useGravity, gravityAcceleration, terminalSpeed, groundSpeed);
            session.SetJumpDraft(useJump, height, bufferTime, coyoteTime);
            return true;
        }

        #endregion

        #endregion
    }
}
