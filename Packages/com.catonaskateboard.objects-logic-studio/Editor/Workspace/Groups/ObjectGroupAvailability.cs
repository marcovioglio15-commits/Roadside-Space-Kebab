using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Explains disabled mode dependencies without silently changing the object's settings.</summary>
    internal static class ObjectGroupAvailability
    {
        #region Methods

        #region Dependencies

        /// <summary>Finds the prerequisite for a group whose controls are currently unused.</summary>
        /// <param name="data">Complete interaction draft.</param>
        /// <param name="group">Group to display or open.</param>
        /// <param name="setting">Receives the controlling field path.</param>
        /// <returns>A brief instruction, or empty when this group is available.</returns>
        internal static string Requirement(SerializedObject data, ObjectGroupInfo group, out string setting)
        {
            if (group.Segments is { Length: > 0 })
            {
                string first = string.Empty;
                setting = string.Empty;
                foreach (ObjectGroupInfo segment in group.Segments)
                {
                    string requirement = Requirement(data, segment, out string dependency);
                    if (string.IsNullOrEmpty(requirement))
                        return string.Empty;
                    if (string.IsNullOrEmpty(first))
                    {
                        first = requirement;
                        setting = dependency;
                    }
                }
                return first;
            }
            if (group.Parent.StartsWith("Single.Draft.Eject.BlackHole", System.StringComparison.Ordinal) || group.Title == "Black Hole Arrival Impulse")
            {
                setting = "Single.Draft.Eject.Behavior";
                if (data.FindProperty(setting).enumValueIndex != (int)EjectBehavior.BlackHole)
                    return "Choose Black Hole in Eject mode to use these settings.";
            }
            if (group.Title == "Radio channels" || group.Parent.StartsWith("Extended.Draft.Ambient.Radio", System.StringComparison.Ordinal))
            {
                setting = "Extended.Draft.Ambient.Mode";
                if (data.FindProperty(setting).enumValueIndex != (int)AmbientMode.Radio)
                    return "Choose Radio in Audio mode to use these settings.";
            }
            if (group.Title == "Day flow")
            {
                setting = "Extended.Draft.SpawnManagement.Mode";
                if (data.FindProperty(setting).enumValueIndex != (int)SpawnManagementMode.DayFlow)
                    return "Choose Day Flow in Spawn mode to use these settings.";
            }
            if (group.Parent.StartsWith("Extended.Draft.SpawnManagement", System.StringComparison.Ordinal) && group.Title is not ("Spawn mode" or "Day flow"))
            {
                setting = "Extended.Draft.SpawnManagement.Mode";
                if (data.FindProperty(setting).enumValueIndex == (int)SpawnManagementMode.DayFlow)
                    return "Choose Completion Rules in Spawn mode to use these settings.";
            }
            if (group.Title == "Ambient sound" && data.FindProperty("Extended.Draft.Ambient.Mode").enumValueIndex == (int)AmbientMode.Radio)
            {
                setting = "Extended.Draft.Ambient.Mode";
                return "Choose Ambient in Audio mode to use these settings.";
            }
            if (group.Title == "Player Suspension" && !data.FindProperty("Extended.Draft.GravityGenerator.AffectPlayer").boolValue)
            {
                setting = "Extended.Draft.GravityGenerator.AffectPlayer";
                return "Turn on Affect Player in General settings to use player suspension.";
            }
            setting = group.Title switch
            {
                "Physical pickup" => group.Parent + ".Instant",
                "Contact rotation" => group.Parent + ".WorldCollisions",
                "Release physics" => group.Parent + ".OverrideBody",
                "Effect lifetime" or "Effect attachment" => group.Parent + ".Enabled",
                "Deformation recovery" => group.Parent + ".Collisions",
                "Ingredient contacts" => group.Parent + ".Trigger",
                _ => string.Empty
            };
            SerializedProperty control = string.IsNullOrEmpty(setting) ? null : data.FindProperty(setting);
            if (control == null)
                return string.Empty;
            return group.Title switch
            {
                "Physical pickup" when control.boolValue => "Turn off Instant in Pickup transition to use physical pickup.",
                "Contact rotation" when !control.boolValue => "Turn on World Collisions in Carry collisions to use contact rotation.",
                "Release physics" when !control.boolValue => "Turn on Override Body in Release weight and drag to change the body settings.",
                "Effect lifetime" or "Effect attachment" when !control.boolValue => "Turn on the effect in Start effect to use these settings.",
                "Deformation recovery" when !control.boolValue => "Turn on Collisions in Deformation contacts to use these settings.",
                "Ingredient contacts" when control.enumValueIndex == (int)AssemblyStationTrigger.InputAction => "Choose a contact trigger in Assembly activation to use these settings.",
                _ => string.Empty
            };
        }

        #endregion

        #endregion
    }
}
