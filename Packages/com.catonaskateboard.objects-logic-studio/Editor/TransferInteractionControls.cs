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
                    {
                        Prefab(settings.FindPropertyRelative("Prefab"));
                        HoverControls.Field(settings, "Unlimited");
                        if (!settings.FindPropertyRelative("Unlimited").boolValue)
                            HoverControls.Field(settings, "Stock");
                    }
                }
            if (sections.Draw("Dispenser Output", "Animate from the dispenser pivot to the item carry pose while ignoring the dispenser colliders."))
                using (new EditorGUI.IndentLevelScope())
                {
                    HoverControls.Field(settings, "PickupDuration");
                    HoverControls.Field(settings, "OutputRotation");
                }
        }

        /// <summary>Draws a real allowed-tag list and optional visible storage arrangement.</summary>
        /// <param name="settings">Detached or preset Container settings.</param>
        /// <param name="sections">Persistent foldout state.</param>
        internal static void Container(SerializedProperty settings, ObjectStudioSections sections)
        {
            // Hidden inventory retains the same instance; only presentation controls become irrelevant.
            Target(settings.FindPropertyRelative("Target"), sections);
            if (!sections.Draw("Container Storage", "Store carried objects with matching tags. Recovery requires a linked Dispenser."))
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            SerializedProperty tags = settings.FindPropertyRelative("AllowedTags");
            for (int index = 0; index < tags.arraySize; index++)
                using (new EditorGUILayout.HorizontalScope())
                {
                    ExtendedInteractionControls.Tag(tags.GetArrayElementAtIndex(index));
                    if (GUILayout.Button(new GUIContent("−", "Remove this allowed tag."), GUILayout.Width(28f)))
                    {
                        tags.DeleteArrayElementAtIndex(index);
                        break;
                    }
                }
            if (GUILayout.Button(new GUIContent("+ Add Allowed Tag", "Accept another tag from this project's catalog.")))
            {
                tags.arraySize++;
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = "Untagged";
            }
            HoverControls.Field(settings, "Unlimited");
            if (!settings.FindPropertyRelative("Unlimited").boolValue)
                HoverControls.Field(settings, "Capacity");
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
        private static void Target(SerializedProperty target, ObjectStudioSections sections)
        {
            // Exact cursor mode does not use a screen-centre tolerance.
            if (!sections.Draw("Transfer Targeting", "Configure reach, aim and solid obstruction layers."))
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            HoverControls.Field(target, "Distance");
            HoverControls.Field(target, "Mode");
            if (target.FindPropertyRelative("Mode").enumValueIndex == (int)HoverTargetMode.ViewCenter)
                HoverControls.Field(target, "CenterRadius");
            HoverControls.Field(target, "Offset");
            HoverControls.Field(target, "ObstacleMask");
            HoverControls.Field(target, "DrawGizmos");
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
