using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.PlayerStudio;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws transform toggles and flag-specific impulses with the shared Single input and preset workflow.</summary>
    internal static class CommandInteractionControls
    {
        #region Methods

        #region Animation

        /// <summary>Edits two local states with hierarchy selection and explicit pose capture buttons.</summary>
        /// <param name="settings">Detached or preset transform animation settings.</param>
        /// <param name="sections">Retained foldout state.</param>
        internal static void Animation(SerializedProperty settings, ObjectStudioSections sections)
        {
            // Pose capture reads the selected target without moving the source object.
            TransferInteractionControls.Target(settings.FindPropertyRelative("Target"), sections);
            if (!sections.Draw("Transform Animation", "Alternate between two local states on successive input presses."))
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            GameObject source = HierarchyPathMenu.Source(settings);
            SerializedProperty path = settings.FindPropertyRelative("Path");
            HierarchyPathMenu.Draw(path, source, false, transforms: true);
            Transform target = source != null ? PlayerHierarchy.Resolve(source.transform, path.stringValue) : null;
            Pose(settings.FindPropertyRelative("StateA"), target, "Read State A");
            Pose(settings.FindPropertyRelative("StateB"), target, "Read State B");
            HoverControls.Field(settings, "StartAtB");
            HoverControls.Field(settings, "Duration");
            if (settings.FindPropertyRelative("Duration").floatValue > 0f)
            {
                HoverControls.Field(settings, "ForwardRotation");
                HoverControls.Field(settings, "ReturnRotation");
            }
            HoverControls.Field(settings, "AutoReturn");
            if (settings.FindPropertyRelative("AutoReturn").boolValue)
            {
                HoverControls.Field(settings, "ReturnDelay");
                HoverControls.Field(settings, "AutoReturnDuration");
                if (settings.FindPropertyRelative("AutoReturnDuration").floatValue > 0f)
                    HoverControls.Field(settings, "AutoReturnRotation");
            }
        }

        /// <summary>Shows one endpoint and reads the current local transform only on an explicit command.</summary>
        /// <param name="pose">Endpoint receiving position, Euler angles and scale.</param>
        /// <param name="target">Resolved sample or destination transform.</param>
        /// <param name="label">Endpoint-specific capture button.</param>
        private static void Pose(SerializedProperty pose, Transform target, string label)
        {
            // Numerical fields remain available when a standalone preset has no sample hierarchy yet.
            StudioGUI.PropertyField(pose, true);
            using (new EditorGUI.DisabledScope(target == null))
                if (StudioButton.Draw(new GUIContent(label, "Read the selected transform's current local pose into this endpoint.")))
                    pose.boxedValue = PlayerToolPose.Read(target);
        }

        #endregion

        #region Ejection

        /// <summary>Edits contact policy, impulse groups and optional delayed destruction.</summary>
        /// <param name="settings">Detached or preset ejection settings.</param>
        /// <param name="sections">Retained foldout state.</param>
        internal static void Eject(SerializedProperty settings, ObjectStudioSections sections)
        {
            // Only input-time contact queries use these settings; no passive scans are introduced.
            TransferInteractionControls.Target(settings.FindPropertyRelative("Target"), sections);
            HoverControls.Field(settings, "SelfEject", "Self Eject");
            bool self = settings.FindPropertyRelative("SelfEject").boolValue;
            if (sections.Draw("Ejection", "Apply one physical impulse per successful activation."))
                using (new EditorGUI.IndentLevelScope())
                {
                    if (!self)
                    {
                        HoverControls.Field(settings, "IncludeTriggers");
                        HoverControls.Field(settings, "Tolerance");
                    }
                    HoverControls.Field(settings, "ReleaseKinematic");
                    HoverControls.Field(settings, "Mode");
                    if (self)
                    {
                        HoverControls.Field(settings.FindPropertyRelative("SelfImpulse"), "Impulse");
                        HoverControls.Field(settings.FindPropertyRelative("SelfImpulse"), "Space");
                    }
                }
            if (!self && sections.Draw("Eject Impulses", "Rules run in list order. Each body receives the first matching impulse only."))
                using (new EditorGUI.IndentLevelScope())
                    Rules(settings.FindPropertyRelative("Rules"));
            if (sections.Draw("Eject Collisions", "Temporarily ignore selected collision layers on ejected bodies."))
                using (new EditorGUI.IndentLevelScope())
                {
                    HoverControls.Field(settings, "IgnoreCollisions");
                    if (settings.FindPropertyRelative("IgnoreCollisions").boolValue)
                    {
                        HoverControls.Field(settings, "IgnoredLayers");
                        HoverControls.Field(settings, "IgnoreDuration");
                    }
                }
            if (sections.Draw("Eject Lifetime", "Optionally destroy ejected objects after a scaled-time delay."))
                using (new EditorGUI.IndentLevelScope())
                {
                    HoverControls.Field(settings, "Despawn");
                    if (settings.FindPropertyRelative("Despawn").boolValue)
                        HoverControls.Field(settings, "DespawnDelay");
                }
        }

        /// <summary>Edits independent flag groups with searchable selectors and deterministic priority.</summary>
        /// <param name="rules">Serialized ordered impulse rules.</param>
        private static void Rules(SerializedProperty rules)
        {
            // Native array insertion is explicitly initialized instead of duplicating the previous rule's flags.
            for (int index = 0; index < rules.arraySize; index++)
            {
                SerializedProperty rule = rules.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.HorizontalScope())
                {
                    rule.isExpanded = EditorGUILayout.Foldout(rule.isExpanded, new GUIContent("Impulse " + (index + 1),
                        "First matching group wins for each physical body."), true);
                    using (new EditorGUI.DisabledScope(index == 0))
                        if (GUILayout.Button(new GUIContent("↑", "Give this rule higher priority."), GUILayout.Width(26f)))
                        {
                            rules.MoveArrayElement(index, index - 1);
                            break;
                        }
                    if (GUILayout.Button(new GUIContent("−", "Remove this impulse group."), GUILayout.Width(26f)))
                    {
                        rules.DeleteArrayElementAtIndex(index);
                        break;
                    }
                }
                if (!rule.isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                ObjectFlagSelector.Draw(rule.FindPropertyRelative("Flags"));
                if (rule.FindPropertyRelative("Flags").arraySize > 1)
                    HoverControls.Field(rule, "Match");
                HoverControls.Field(rule, "Impulse");
                HoverControls.Field(rule, "Space");
            }
            if (StudioButton.Draw(new GUIContent("+ Add Impulse", "Add a flag group with its own impulse vector.")))
            {
                rules.arraySize++;
                rules.GetArrayElementAtIndex(rules.arraySize - 1).boxedValue = new EjectRule();
                rules.GetArrayElementAtIndex(rules.arraySize - 1).isExpanded = true;
            }
        }

        #endregion

        #endregion
    }
}
