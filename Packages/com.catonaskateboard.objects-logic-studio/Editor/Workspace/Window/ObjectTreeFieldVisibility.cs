using System;
using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Keeps inactive ejection options out of the detailed tree as well as its conditional editors.</summary>
    internal static class ObjectTreeFieldVisibility
    {
        #region Methods
        #region Dependencies

        /// <summary>Checks the same mode dependencies used by the ejection controls.</summary>
        /// <param name="field">Field about to become a tree node.</param>
        /// <returns>True when the setting is relevant to the current proposal.</returns>
        internal static bool IsVisible(SerializedProperty field)
        {
            // Other interactions retain the field paths supplied by their own control sections.
            const string root = "Single.Draft.Eject.";
            if (!field.propertyPath.StartsWith(root, StringComparison.Ordinal))
                return true;
            // Descendants inherit their parent node's visibility.
            string name = field.propertyPath.Substring(root.Length);
            if (name.Contains('.'))
                return !name.StartsWith("SelfImpulse.", StringComparison.Ordinal) || name is "SelfImpulse.Impulse" or "SelfImpulse.Space";
            SerializedProperty settings = field.serializedObject.FindProperty("Single.Draft.Eject");
            bool impulse = settings.FindPropertyRelative("Behavior").enumValueIndex == (int)EjectBehavior.Impulse;
            bool self = impulse && settings.FindPropertyRelative("SelfEject").boolValue;
            bool animate = settings.FindPropertyRelative("AnimateBeforeEject").boolValue;
            bool returning = animate && !settings.FindPropertyRelative("WaitForAnimationReturn").boolValue
                && settings.FindPropertyRelative("ForceAutoReturn").boolValue;
            return name switch
            {
                "BlackHole" => !impulse,
                "SelfEject" => impulse,
                "TeamRocketSpin" or "TemporaryPhysics" or "UseBulletImpulse" => self,
                "SpinSpeed" => self && settings.FindPropertyRelative("TeamRocketSpin").boolValue,
                "Physics" => self && settings.FindPropertyRelative("TemporaryPhysics").boolValue,
                "SelfImpulse" => self && !settings.FindPropertyRelative("UseBulletImpulse").boolValue,
                "BulletImpulseMultiplier" => self && settings.FindPropertyRelative("UseBulletImpulse").boolValue,
                "IncludeTriggers" or "Tolerance" or "Rules" => impulse && !self,
                "ReleaseKinematic" => impulse && (!self || !settings.FindPropertyRelative("TemporaryPhysics").boolValue),
                "IgnoredLayers" or "IgnoreDuration" => settings.FindPropertyRelative("IgnoreCollisions").boolValue,
                "DespawnDelay" => settings.FindPropertyRelative("Despawn").boolValue,
                "ExternalAnimation" or "WaitForAnimationReturn" => animate,
                "AnimationIdentity" => animate && settings.FindPropertyRelative("ExternalAnimation").boolValue,
                "ForceAutoReturn" => animate && !settings.FindPropertyRelative("WaitForAnimationReturn").boolValue,
                "AnimationOverlap" => animate && !impulse,
                "ReturnOverlap" => returning && !impulse,
                _ => true
            };
        }

        #endregion
        #endregion
    }
}
