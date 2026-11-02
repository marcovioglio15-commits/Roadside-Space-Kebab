using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares conditional inventory controls between interaction cards and dedicated presets.</summary>
    internal static class TransferInteractionControls
    {
        #region Methods

        #region Settings

        /// <summary>Draws supply controls with mutually exclusive prefab and linked-storage modes.</summary>
        /// <param name="settings">Detached or preset Dispenser settings.</param>
        /// <param name="sections">Persistent foldout state.</param>
        internal static void Dispenser(SerializedProperty settings, ObjectStudioSections sections)
        {
            // Linked storage restores original instances and has no separate prefab stock.
            Target(settings.FindPropertyRelative("Target"), sections);
            bool scoopOnly = settings.FindPropertyRelative("Mode").enumValueIndex == (int)DispenseMode.ScoopOnly;
            if (sections.Draw("Dispenser Supply", "Choose a supply flag, prefab or linked storage.", settings, "UseContainer", "Prefab", "ScoopFlag", "Unlimited", "Stock", "DestroyWhenEmpty"))
                using (new StudioIndentScope())
                {
                    if (scoopOnly)
                        HoverControls.Field(settings, "ScoopFlag");
                    else
                    {
                        HoverControls.Field(settings, "UseContainer");
                        if (!settings.FindPropertyRelative("UseContainer").boolValue)
                            Prefab(settings.FindPropertyRelative("Prefab"));
                    }
                    HoverControls.Field(settings, "Unlimited");
                    if (!settings.FindPropertyRelative("Unlimited").boolValue)
                    {
                        HoverControls.Field(settings, "Stock");
                        HoverControls.Field(settings, "DestroyWhenEmpty");
                    }
                }
            if (sections.Draw("Dispenser Output", "Choose carried pickup or release objects at independently placed outlets.", settings, "Mode", "Interval", "Outlets", "PickupDuration", "OutputRotation"))
                using (new StudioIndentScope())
                {
                    HoverControls.Field(settings, "Mode", "Output Mode");
                    if ((DispenseMode)settings.FindPropertyRelative("Mode").enumValueIndex == DispenseMode.ToHand)
                    {
                        HoverControls.Field(settings, "PickupDuration");
                        HoverControls.Field(settings, "OutputRotation");
                    }
                    else if ((DispenseMode)settings.FindPropertyRelative("Mode").enumValueIndex != DispenseMode.ScoopOnly)
                    {
                        if ((DispenseMode)settings.FindPropertyRelative("Mode").enumValueIndex == DispenseMode.ContinuousOutput)
                            HoverControls.Field(settings, "Interval");
                        DispenserOutletControls.Draw(settings);
                    }
                }
            if (sections.Draw("Dispense counting", "Choose what advances each step.", settings, "StepCounter"))
                using (new StudioIndentScope())
                    HoverControls.Field(settings, "StepCounter");
            if (!scoopOnly && settings.FindPropertyRelative("UseContainer").boolValue || !settings.FindPropertyRelative("Unlimited").boolValue
                || settings.FindPropertyRelative("StepCounter").enumValueIndex == (int)DispenseStepCounter.Dispensed)
                InventoryFillControls.Draw(settings.FindPropertyRelative("FillSteps"), sections, "Dispense Steps");
        }

        /// <summary>Draws a real allowed-flag list and optional visible storage arrangement.</summary>
        /// <param name="settings">Detached or preset Container settings.</param>
        /// <param name="sections">Persistent foldout state.</param>
        internal static void Container(SerializedProperty settings, ObjectStudioSections sections)
        {
            // Hidden inventory retains the same instance; only presentation controls become irrelevant.
            HoverControls.Field(settings, "Mode", "Container Mode");
            if (settings.FindPropertyRelative("Mode").enumValueIndex != (int)ContainerMode.ScoopFromDispenser)
                Target(settings.FindPropertyRelative("Target"), sections);
            if (sections.Draw("Container Storage", "Choose accepted flags and storage or scoop settings.", settings, "AllowedFlags", "Match", "Unlimited", "Capacity", "LimitToDispenserSpace", "KeepVisible", "Position", "Rotation", "Spacing", "InitialFill", "ScoopAnimation"))
                using (new StudioIndentScope())
                    Storage(settings);
            InventoryFillControls.Draw(settings.FindPropertyRelative("FillSteps"), sections, "Container Steps");
        }

        /// <summary>Draws deposit constraints and the optional display of retained originals.</summary>
        /// <param name="settings">Container settings being edited.</param>
        private static void Storage(SerializedProperty settings)
        {
            // Linked capacity is independent of the Container's own maximum count.
            SerializedProperty flags = settings.FindPropertyRelative("AllowedFlags");
            if (ObjectGroupScope.Allows(flags))
                ObjectFlagSelector.Draw(flags);
            if (flags.arraySize > 1)
                HoverControls.Field(settings, "Match");
            ContainerMode mode = (ContainerMode)settings.FindPropertyRelative("Mode").enumValueIndex;
            if (mode == ContainerMode.RefillDispenser)
                return;
            if (mode == ContainerMode.ScoopFromDispenser)
            {
                HoverControls.Field(settings, "Capacity");
                HoverControls.Field(settings, "InitialFill");
                ScoopAnimationControls.Draw(settings.FindPropertyRelative("ScoopAnimation"));
                return;
            }
            HoverControls.Field(settings, "Unlimited");
            if (!settings.FindPropertyRelative("Unlimited").boolValue)
                HoverControls.Field(settings, "Capacity");
            HoverControls.Field(settings, "LimitToDispenserSpace");
            HoverControls.Field(settings, "KeepVisible");
            if (settings.FindPropertyRelative("KeepVisible").boolValue)
            {
                HoverControls.Field(settings, "Position");
                HoverControls.Field(settings, "Rotation");
                HoverControls.Field(settings, "Spacing");
            }
        }

        /// <summary>Edits common transfer targeting while preserving independent input bindings.</summary>
        /// <param name="target">Serialized targeting configuration.</param>
        /// <param name="sections">Persistent foldout state.</param>
        internal static void Target(SerializedProperty target, ObjectStudioSections sections)
        {
            // Exact cursor mode does not use a screen-centre tolerance.
            if (!sections.Draw("Action Targeting", "Configure reach, aim and solid obstruction layers.", target))
                return;
            using StudioIndentScope indent = new StudioIndentScope();
            HoverControls.Field(target, "Distance");
            HoverControls.Field(target, "Mode", "Targeting Mode");
            Aim(target, "Mode", "Offset");
            HoverControls.Field(target, "ObstacleMask");
            HoverControls.Field(target, "DrawGizmos");
        }

        /// <summary>Shares exact-hit controls across action, Grab and Slice targeting.</summary>
        /// <param name="target">Serialized targeting settings.</param>
        /// <param name="mode">Name of the selected aiming mode field.</param>
        /// <param name="offset">Name of the optional anchor offset field.</param>
        internal static void Aim(SerializedProperty target, string mode, string offset)
        {
            // Solid hits use the actual ray intersection, so centre tolerance and anchor offsets are unused.
            HoverControls.Field(target, "SolidHitOnly");
            if (target.FindPropertyRelative("SolidHitOnly").boolValue)
                return;
            if (target.FindPropertyRelative(mode).enumValueIndex == (int)HoverTargetMode.ViewCenter)
                HoverControls.Field(target, "CenterRadius");
            HoverControls.Field(target, offset);
        }

        /// <summary>Allows only prefab assets in a serialized GameObject field.</summary>
        /// <param name="property">Prefab reference receiving a project asset.</param>
        internal static void Prefab(SerializedProperty property)
        {
            if (!ObjectGroupScope.Allows(property))
                return;
            // Scene instances cannot enter reusable interaction or spawn settings.
            EditorGUI.BeginChangeCheck();
            GameObject selected = (GameObject)StudioGUI.ObjectField(StudioFieldMenu.Value(property, new GUIContent(property.displayName, property.tooltip)),
                property.objectReferenceValue, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
                if (selected == null || PrefabUtility.IsPartOfPrefabAsset(selected) && selected.transform.parent == null)
                    property.objectReferenceValue = selected;
                else
                    Debug.LogWarning("Choose a prefab root from the Project window.");
        }

        #endregion

        #endregion
    }
}
