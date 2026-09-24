using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Shows only camera fields that affect the selected view and follow mode.</summary>
    internal static class PlayerCameraControls
    {
        #region Methods

        #region Fields

        /// <summary>Draws a serialized settings block on an asset Inspector or an isolated draft.</summary>
        /// <param name="serialized">Camera preset properties owned by the caller.</param>
        /// <param name="sections">Persistent visibility of the individual camera groups.</param>
        public static void Draw(SerializedObject serialized, PlayerStudioSections sections)
        {
            // One conditional field list serves both Inspector and Player Studio.
            SerializedProperty settings = serialized.FindProperty("settings");
            PlayerCameraMode mode = (PlayerCameraMode)settings.FindPropertyRelative("mode").enumValueIndex;
            if (sections.Draw("Camera.View", "View"))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "mode");
                    mode = (PlayerCameraMode)settings.FindPropertyRelative("mode").enumValueIndex;
                    if (mode != PlayerCameraMode.Fixed || settings.FindPropertyRelative("fixedTrackTarget").boolValue)
                        Field(settings, "targetOffset");
                    if (mode != PlayerCameraMode.Fixed)
                    {
                        Field(settings, "follow");
                        if ((PlayerCameraFollow)settings.FindPropertyRelative("follow").enumValueIndex == PlayerCameraFollow.Damped)
                        {
                            Field(settings, "followResponse");
                            if (mode == PlayerCameraMode.ThirdPerson)
                                Field(settings, "dampOrbit");
                        }
                        Field(settings, "initialAngles");
                    }
                    else
                    {
                        Field(settings, "fixedPosition");
                        Field(settings, "fixedTrackTarget");
                        if (!settings.FindPropertyRelative("fixedTrackTarget").boolValue)
                            Field(settings, "fixedEuler");
                    }
                    if (mode == PlayerCameraMode.ThirdPerson)
                        Field(settings, "distance");
                }
            if (mode != PlayerCameraMode.Fixed && sections.Draw("Camera.Look", "Look"))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "lookEnabled");
                    if (settings.FindPropertyRelative("lookEnabled").boolValue)
                    {
                        Sensitivity(settings, "deltaSensitivity", new Vector2(0.12f, 0.12f), "Mouse Sensitivity",
                            "Per-axis multiplier: 1 = 0.12 degrees per mouse delta unit. Mouse travel is independent of frame duration and depends on device DPI.");
                        Sensitivity(settings, "rateSensitivity", new Vector2(160f, 120f), "Stick Sensitivity",
                            "Per-axis multiplier: 1 = 160 degrees/second horizontally and 120 vertically at full deflection. The stick value is integrated over time.");
                        Field(settings, "smoothLook");
                        if (settings.FindPropertyRelative("smoothLook").boolValue)
                        {
                            Field(settings, "lookSmoothingTime");
                            Field(settings, "adaptiveLookSmoothing");
                            if (settings.FindPropertyRelative("adaptiveLookSmoothing").boolValue)
                                Field(settings, "lookSmoothingSpeed");
                        }
                        Field(settings, "invertY");
                        Field(settings, "pitchLimits");
                        Field(settings, "limitYaw");
                        if (settings.FindPropertyRelative("limitYaw").boolValue)
                            Field(settings, "yawLimits");
                    }
                }
            if (mode != PlayerCameraMode.Fixed && settings.FindPropertyRelative("lookEnabled").boolValue
                && sections.Draw("Camera.Cursor", "Cursor"))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "lockCursor");
                    if (settings.FindPropertyRelative("lockCursor").boolValue)
                    {
                        Field(settings, "showCenteredCursor");
                        if (settings.FindPropertyRelative("showCenteredCursor").boolValue)
                        {
                            Field(settings, "cursorTexture");
                            Field(settings, "cursorScale");
                        }
                    }
                }
            if (mode == PlayerCameraMode.ThirdPerson && sections.Draw("Camera.Obstacles", "Obstacles"))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "avoidObstacles");
                    if (settings.FindPropertyRelative("avoidObstacles").boolValue)
                    {
                        Field(settings, "obstacleMask");
                        Field(settings, "collisionRadius");
                        Field(settings, "collisionPadding");
                        Field(settings, "obstacleReturnTime");
                    }
                }
            if (mode == PlayerCameraMode.FirstPerson && sections.Draw("Camera.HeadTilt", "Head Tilt"))
                using (new EditorGUI.IndentLevelScope())
                    PlayerHeadTiltControls.Draw(settings.FindPropertyRelative("headTilt"));
            if (sections.Draw("Camera.Lens", "Lens"))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "fieldOfView");
                    Field(settings, "nearClip");
                    Field(settings, "farClip");
                }
            if (sections.Draw("Camera.Presentation", "Player Presentation"))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(settings, "movementFrame");
                    Field(settings, "modelFacing");
                    if ((PlayerModelFacing)settings.FindPropertyRelative("modelFacing").enumValueIndex != PlayerModelFacing.Authored)
                        Field(settings, "turnSpeed");
                    if (mode == PlayerCameraMode.FirstPerson)
                        Field(settings, "hideVisualInFirstPerson");
                }
        }

        /// <summary>Draws a field with its serialized tooltip and a consistent layout.</summary>
        /// <param name="settings">Parent settings property.</param>
        /// <param name="name">Serialized child field name.</param>
        private static void Field(SerializedProperty settings, string name)
        {
            // Tooltips remain defined beside the data instead of being repeated in Editor strings.
            PlayerPresetField.Draw(settings, name);
        }

        /// <summary>Presents device calibration on a common multiplier scale while preserving stored angular units.</summary>
        /// <param name="settings">Serialized camera settings.</param>
        /// <param name="name">Raw angular calibration property.</param>
        /// <param name="calibration">Angular units corresponding to multiplier one.</param>
        /// <param name="label">Device-specific field label.</param>
        /// <param name="tooltip">Explanation of calibration and timing.</param>
        private static void Sensitivity(SerializedProperty settings, string name, Vector2 calibration, string label, string tooltip)
        {
            // Repainting never rewrites an existing value through a floating-point round trip.
            using SerializedProperty property = settings.FindPropertyRelative(name);
            EditorGUI.BeginChangeCheck();
            Vector2 value = EditorGUILayout.Vector2Field(new GUIContent(label, tooltip),
                new Vector2(property.vector2Value.x / calibration.x, property.vector2Value.y / calibration.y));
            if (EditorGUI.EndChangeCheck())
                property.vector2Value = Vector2.Scale(value, calibration);
        }

        #endregion

        #endregion
    }
}
