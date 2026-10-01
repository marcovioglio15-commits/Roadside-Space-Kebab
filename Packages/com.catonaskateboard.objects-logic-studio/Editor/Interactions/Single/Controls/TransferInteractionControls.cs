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
            if (sections.Draw("Dispenser Supply", "Choose prefab supply or recover the last object deposited in this object's Container."))
                using (new EditorGUI.IndentLevelScope())
                {
                    HoverControls.Field(settings, "UseContainer");
                    if (!settings.FindPropertyRelative("UseContainer").boolValue)
                        Prefab(settings.FindPropertyRelative("Prefab"));
                    HoverControls.Field(settings, "Unlimited");
                    if (!settings.FindPropertyRelative("Unlimited").boolValue)
                        HoverControls.Field(settings, "Stock");
                }
            if (sections.Draw("Dispenser Output", "Animate from the dispenser pivot to the item carry pose while ignoring the dispenser colliders."))
                using (new EditorGUI.IndentLevelScope())
                {
                    HoverControls.Field(settings, "PickupDuration");
                    HoverControls.Field(settings, "OutputRotation");
                }
            if (settings.FindPropertyRelative("UseContainer").boolValue || !settings.FindPropertyRelative("Unlimited").boolValue)
                InventoryFillControls.Draw(settings.FindPropertyRelative("FillSteps"), sections, "Dispenser Fill");
        }

        /// <summary>Draws a real allowed-flag list and optional visible storage arrangement.</summary>
        /// <param name="settings">Detached or preset Container settings.</param>
        /// <param name="sections">Persistent foldout state.</param>
        internal static void Container(SerializedProperty settings, ObjectStudioSections sections)
        {
            // Hidden inventory retains the same instance; only presentation controls become irrelevant.
            Target(settings.FindPropertyRelative("Target"), sections);
            if (sections.Draw("Container Storage", "Store carried objects with matching flags. Recovery requires a linked Dispenser."))
                using (new EditorGUI.IndentLevelScope())
                    Storage(settings);
            InventoryFillControls.Draw(settings.FindPropertyRelative("FillSteps"), sections, "Container Fill");
        }

        /// <summary>Draws deposit constraints and the optional display of retained originals.</summary>
        /// <param name="settings">Container settings being edited.</param>
        private static void Storage(SerializedProperty settings)
        {
            // Linked capacity is independent of the Container's own maximum count.
            SerializedProperty flags = settings.FindPropertyRelative("AllowedFlags");
            ObjectFlagSelector.Draw(flags);
            if (flags.arraySize > 1)
                HoverControls.Field(settings, "Match");
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
            if (!sections.Draw("Action Targeting", "Configure reach, aim and solid obstruction layers."))
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            HoverControls.Field(target, "Distance");
            HoverControls.Field(target, "Mode");
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
            // Scene instances cannot enter reusable interaction or spawn settings.
            EditorGUI.BeginChangeCheck();
            GameObject selected = (GameObject)EditorGUILayout.ObjectField(new GUIContent(property.displayName, property.tooltip),
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
