using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares gravity pulse controls between reusable presets and pending object settings.</summary>
    internal static class GravityControls
    {
        #region Methods
        #region Drawing

        /// <summary>Shows enabled selection groups, timing, force and restoration branches.</summary>
        /// <param name="settings">Serialized gravity configuration.</param>
        /// <param name="sections">Retained window foldout state.</param>
        internal static void Draw(SerializedProperty settings, ObjectStudioSections sections)
        {
            HoverControls.Field(settings, "AffectObjects");
            HoverControls.Field(settings, "AffectPlayer");
            if (settings.FindPropertyRelative("AffectPlayer").boolValue)
            {
                HoverControls.Field(settings, "FilterPlayer");
                if (sections.Draw("Player Suspension", "Simulate an existing CharacterController as a weightless body.", settings, "Player"))
                    Player(settings.FindPropertyRelative("Player"));
            }
            if ((settings.FindPropertyRelative("AffectObjects").boolValue
                || settings.FindPropertyRelative("AffectPlayer").boolValue && settings.FindPropertyRelative("FilterPlayer").boolValue)
                && sections.Draw("Affected Objects", "Combine the enabled layer, identity and interaction groups.", settings, "Filter"))
                Filter(settings.FindPropertyRelative("Filter"));
            RandomScalar(settings, "RandomInterval", "Interval", "IntervalRange");
            HoverControls.Field(settings, "Restore");
            GravityRestoreMode restore = (GravityRestoreMode)settings.FindPropertyRelative("Restore").enumValueIndex;
            if (restore != GravityRestoreMode.Interaction)
                HoverControls.Field(settings, "Duration");
            if (restore != GravityRestoreMode.Timer)
                TransferInteractionControls.Target(settings.FindPropertyRelative("Target"), sections);
            HoverControls.Field(settings, "Push");
            if (!settings.FindPropertyRelative("Push").boolValue)
                return;
            HoverControls.Field(settings, "LocalDirection");
            HoverControls.Field(settings, "RandomDirection");
            if (settings.FindPropertyRelative("RandomDirection").boolValue)
            {
                HoverControls.Field(settings, "DirectionMinimum");
                HoverControls.Field(settings, "DirectionMaximum");
            }
            else
                HoverControls.Field(settings, "Direction");
            RandomScalar(settings, "RandomIntensity", "Intensity", "IntensityRange");
            RandomScalar(settings, "RandomForceDuration", "ForceDuration", "ForceDurationRange");
        }

        /// <summary>Shows only player controls that participate in the chosen suspension simulation.</summary>
        /// <param name="settings">Serialized virtual-body configuration.</param>
        private static void Player(SerializedProperty settings)
        {
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            HoverControls.Field(settings, "Mass");
            HoverControls.Field(settings, "ForceMultiplier");
            HoverControls.Field(settings, "InitialMomentum");
            HoverControls.Field(settings, "Damping");
            HoverControls.Field(settings, "MaximumSpeed");
            HoverControls.Field(settings, "DriftAcceleration");
            HoverControls.Field(settings, "ControlAcceleration");
            if (settings.FindPropertyRelative("ControlAcceleration").floatValue > 0f)
                HoverControls.Field(settings, "FollowCameraPitch");
            HoverControls.Field(settings, "JumpThrust");
            if (settings.FindPropertyRelative("JumpThrust").boolValue)
                HoverControls.Field(settings, "JumpAcceleration");
            HoverControls.Field(settings, "Bounce");
            HoverControls.Field(settings, "ContactFriction");
            HoverControls.Field(settings, "PushSuspendedBodies");
            if (settings.FindPropertyRelative("PushSuspendedBodies").boolValue)
            {
                HoverControls.Field(settings, "SuspendedMassScale");
                HoverControls.Field(settings, "SuspendedPushSpeed");
            }
            HoverControls.Field(settings, "RestoredMomentum");
        }

        /// <summary>Edits optional groups with independent any/all policies.</summary>
        /// <param name="filter">Serialized body selection configuration.</param>
        private static void Filter(SerializedProperty filter)
        {
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            HoverControls.Field(filter, "IncludeSelf");
            HoverControls.Field(filter, "RequireAllGroups");
            HoverControls.Field(filter, "UseLayers");
            if (filter.FindPropertyRelative("UseLayers").boolValue)
                HoverControls.Field(filter, "Layers");
            HoverControls.Field(filter, "UseFlags");
            if (filter.FindPropertyRelative("UseFlags").boolValue)
            {
                ObjectFlagSelector.Draw(filter.FindPropertyRelative("Flags"));
                HoverControls.Field(filter, "FlagMatch");
            }
            HoverControls.Field(filter, "UseInteractions");
            if (!filter.FindPropertyRelative("UseInteractions").boolValue)
                return;
            SerializedProperty types = filter.FindPropertyRelative("Interactions");
            types.intValue = (int)(GravityInteractionFilter)StudioFieldGUI.EnumFlagsField(StudioFieldMenu.Value(types,
                new GUIContent(types.displayName, types.tooltip)), (GravityInteractionFilter)types.intValue);
            HoverControls.Field(filter, "RequireAllInteractions");
        }

        /// <summary>Edits either a fixed scalar or an authored random range.</summary>
        /// <param name="settings">Serialized configuration owning the fields.</param>
        /// <param name="toggle">Randomization flag.</param>
        /// <param name="scalar">Fixed value field.</param>
        /// <param name="range">Minimum and maximum field.</param>
        private static void RandomScalar(SerializedProperty settings, string toggle, string scalar, string range)
        {
            HoverControls.Field(settings, toggle);
            HoverControls.Field(settings, settings.FindPropertyRelative(toggle).boolValue ? range : scalar);
        }

        #endregion
        #endregion
    }
}
