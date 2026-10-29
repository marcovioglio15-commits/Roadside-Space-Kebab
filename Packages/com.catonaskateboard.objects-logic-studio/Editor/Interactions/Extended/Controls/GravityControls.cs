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
            if (sections.Draw("Area of Effect", "Optional spatial selection shared by objects and the player.", settings, "Area"))
                using (new StudioIndentScope())
                    Area(settings.FindPropertyRelative("Area"));
            HoverControls.Field(settings, "AffectObjects");
            HoverControls.Field(settings, "AffectPlayer");
            SerializedProperty player = settings.FindPropertyRelative("Player");
            HoverControls.Field(player, "PushSuspendedBodies");
            if (player.FindPropertyRelative("PushSuspendedBodies").boolValue)
            {
                if (!settings.FindPropertyRelative("AffectPlayer").boolValue)
                    HoverControls.Field(player, "Mass");
                HoverControls.Field(player, "SuspendedMassScale");
                HoverControls.Field(player, "SuspendedPushSpeed");
            }
            if (settings.FindPropertyRelative("AffectPlayer").boolValue)
            {
                HoverControls.Field(settings, "FilterPlayer");
                if (sections.Draw("Player Suspension", "Simulate an existing CharacterController as a weightless body.", settings, "Player"))
                    using (new StudioIndentScope())
                        Player(settings.FindPropertyRelative("Player"));
            }
            if ((settings.FindPropertyRelative("AffectObjects").boolValue
                || settings.FindPropertyRelative("AffectPlayer").boolValue && settings.FindPropertyRelative("FilterPlayer").boolValue)
                && sections.Draw("Affected Objects", "Combine the enabled layer, identity and interaction groups.", settings, "Filter"))
                using (new StudioIndentScope())
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

        /// <summary>Shows dimensions and movement policies only for an enabled spatial restriction.</summary>
        /// <param name="area">Serialized sphere or oriented box configuration.</param>
        private static void Area(SerializedProperty area)
        {
            using StudioIndentScope indent = new StudioIndentScope();
            HoverControls.Field(area, "Enabled");
            if (!area.FindPropertyRelative("Enabled").boolValue)
                return;
            HoverControls.Field(area, "Shape");
            HoverControls.Field(area, "Offset");
            if (area.FindPropertyRelative("Shape").enumValueIndex == (int)GravityAreaShape.Sphere)
                HoverControls.Field(area, "Radius");
            else
            {
                HoverControls.Field(area, "Size");
                HoverControls.Field(area, "Rotation");
            }
            HoverControls.Field(area, "TrackEntries");
            if (area.FindPropertyRelative("TrackEntries").boolValue)
                HoverControls.Field(area, "QueryInterval");
            HoverControls.Field(area, "RestoreOutside");
        }

        /// <summary>Shows only player controls that participate in the chosen suspension simulation.</summary>
        /// <param name="settings">Serialized virtual-body configuration.</param>
        private static void Player(SerializedProperty settings)
        {
            using StudioIndentScope indent = new StudioIndentScope();
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
            HoverControls.Field(settings, "RestoredMomentum");
        }

        /// <summary>Edits optional groups with independent any/all policies.</summary>
        /// <param name="filter">Serialized body selection configuration.</param>
        /// <param name="includeSelf">Whether this effect supports including its own hierarchy.</param>
        internal static void Filter(SerializedProperty filter, bool includeSelf = true)
        {
            using StudioIndentScope indent = new StudioIndentScope();
            if (includeSelf)
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
