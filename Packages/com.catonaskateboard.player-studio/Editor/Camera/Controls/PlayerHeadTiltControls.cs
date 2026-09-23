using UnityEditor;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Shares conditional footstep controls between the Camera preset and the retained tool draft.</summary>
    internal static class PlayerHeadTiltControls
    {
        #region Methods

        #region Drawing

        /// <summary>Exposes only settings used by the active first-person effect.</summary>
        /// <param name="settings">Head Tilt block inside the Camera preset.</param>
        internal static void Draw(SerializedProperty settings)
        {
            // The caller owns the First Person condition and the outer foldout.
            PlayerPresetField.Draw(settings, "Enabled");
            if (!settings.FindPropertyRelative("Enabled").boolValue)
                return;
            PlayerPresetField.Draw(settings, "GroundedOnly");
            PlayerPresetField.Draw(settings, "StrideLength");
            PlayerPresetField.Draw(settings, "ReferenceSpeed");
            PlayerPresetField.Draw(settings, "Height");
            PlayerPresetField.Draw(settings, "SideAmplitude");
            PlayerPresetField.Draw(settings, "Lean");
            if (settings.FindPropertyRelative("Lean").floatValue > 0f)
                PlayerPresetField.Draw(settings, "RollAmplitude");
            PlayerPresetField.Draw(settings, "Response");
            PlayerPresetField.Draw(settings, "Irregular");
            if (!settings.FindPropertyRelative("Irregular").boolValue)
                return;
            PlayerPresetField.Draw(settings, "AmplitudeVariation");
            PlayerPresetField.Draw(settings, "CadenceVariation");
            if (settings.FindPropertyRelative("AmplitudeVariation").floatValue > 0f
                || settings.FindPropertyRelative("CadenceVariation").floatValue > 0f)
            {
                PlayerPresetField.Draw(settings, "VariationRate");
                PlayerPresetField.Draw(settings, "Seed");
            }
        }

        #endregion

        #endregion
    }
}
