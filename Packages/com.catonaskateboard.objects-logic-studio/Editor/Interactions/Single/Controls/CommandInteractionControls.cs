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
            if (sections.Draw("Animation activation", "Choose when the animation starts.", settings, "Activation", "Target", "ProximityDistance", "ExitDistance", "Loop", "OnExit", "ResumeOnReentry", "Source", "Moment"))
            {
                using StudioIndentScope sectionContent1 = new StudioIndentScope();
                HoverControls.Field(settings, "Activation");
                switch ((TriggerAnimationActivation)settings.FindPropertyRelative("Activation").enumValueIndex)
                {
                    case TriggerAnimationActivation.Action:
                        break;
                    case TriggerAnimationActivation.CenterCollider:
                        HoverControls.Field(settings.FindPropertyRelative("Target"), "Distance");
                        HoverControls.Field(settings.FindPropertyRelative("Target"), "ObstacleMask");
                        break;
                    case TriggerAnimationActivation.Proximity:
                        HoverControls.Field(settings, "ProximityDistance");
                        HoverControls.Field(settings, "ExitDistance");
                        HoverControls.Field(settings, "Loop");
                        HoverControls.Field(settings, "OnExit");
                        if (settings.FindPropertyRelative("OnExit").enumValueIndex == (int)TriggerAnimationExit.Pause)
                            HoverControls.Field(settings, "ResumeOnReentry");
                        break;
                    case TriggerAnimationActivation.Interaction:
                        LocalInteractionMenu.Draw(settings.FindPropertyRelative("Source"));
                        HoverControls.Field(settings, "Moment");
                        break;
                }
            }
            if (settings.FindPropertyRelative("Activation").enumValueIndex == (int)TriggerAnimationActivation.Action)
                TransferInteractionControls.Target(settings.FindPropertyRelative("Target"), sections);
            if (!sections.Draw("Transform Animation", "Alternate between two local states on successive activations.", settings, "Path", "StateA", "StateB", "StartAtB", "Duration", "ForwardRotation", "ReturnRotation", "AutoReturn", "ReturnDelay", "AutoReturnDuration", "AutoReturnRotation"))
                return;
            using StudioIndentScope indent = new StudioIndentScope();
            GameObject source = HierarchyPathMenu.Source(settings);
            SerializedProperty path = settings.FindPropertyRelative("Path");
            if (ObjectGroupScope.Allows(path))
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
            bool autoReturn = settings.FindPropertyRelative("AutoReturn").boolValue;
            if (autoReturn)
                HoverControls.Field(settings, "ReturnDelay");
            // Day Flow can request this return independently of the ordinary automatic-return toggle.
            HoverControls.Field(settings, "AutoReturnDuration");
            if (settings.FindPropertyRelative("AutoReturnDuration").floatValue > 0f)
                HoverControls.Field(settings, "AutoReturnRotation");
        }

        /// <summary>Shows one endpoint and reads the current local transform only on an explicit command.</summary>
        /// <param name="pose">Endpoint receiving position, Euler angles and scale.</param>
        /// <param name="target">Resolved sample or destination transform.</param>
        /// <param name="label">Endpoint-specific capture button.</param>
        private static void Pose(SerializedProperty pose, Transform target, string label)
        {
            if (!ObjectGroupScope.Allows(pose))
                return;
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
            EjectAnimationControls.Draw(settings, sections);
            if (sections.Draw("Eject mode", "Push objects away or pull them in.", settings, "Behavior"))
                using (new StudioIndentScope())
                    HoverControls.Field(settings, "Behavior");
            if (settings.FindPropertyRelative("Behavior").enumValueIndex == (int)EjectBehavior.BlackHole)
                BlackHoleControls.Draw(settings, sections);
            else
                Impulse(settings, sections);
            if (sections.Draw("Eject Collisions", "Temporarily ignore selected collision layers on ejected bodies.", settings, "IgnoreCollisions", "IgnoredLayers", "IgnoreDuration"))
                using (new StudioIndentScope())
                {
                    HoverControls.Field(settings, "IgnoreCollisions");
                    if (settings.FindPropertyRelative("IgnoreCollisions").boolValue)
                    {
                        HoverControls.Field(settings, "IgnoredLayers");
                        HoverControls.Field(settings, "IgnoreDuration");
                    }
                }
            if (sections.Draw("Eject Lifetime", "Optionally destroy ejected objects after a scaled-time delay.", settings, "Despawn", "DespawnDelay"))
                using (new StudioIndentScope())
                {
                    HoverControls.Field(settings, "Despawn");
                    if (settings.FindPropertyRelative("Despawn").boolValue)
                        HoverControls.Field(settings, "DespawnDelay");
                }
        }

        /// <summary>Shows existing immediate impulse settings only for the impulse behavior.</summary>
        /// <param name="settings">Detached ejection settings.</param>
        /// <param name="sections">Retained foldout state.</param>
        private static void Impulse(SerializedProperty settings, ObjectStudioSections sections)
        {
            HoverControls.Field(settings, "SelfEject", "Self Eject");
            bool self = settings.FindPropertyRelative("SelfEject").boolValue;
            if (self)
            {
                HoverControls.Field(settings, "TemporaryPhysics");
                if (settings.FindPropertyRelative("TemporaryPhysics").boolValue)
                    SingleInteractionControls.DrawRelease(settings.FindPropertyRelative("Physics"), sections);
            }
            if (sections.Draw("Ejection", "Apply one physical impulse per successful activation.", settings, "IncludeTriggers", "Tolerance", "ReleaseKinematic", "Mode", "SelfImpulse", "UseBulletImpulse", "BulletImpulseMultiplier"))
                using (new StudioIndentScope())
                {
                    if (!self)
                    {
                        HoverControls.Field(settings, "IncludeTriggers");
                        HoverControls.Field(settings, "Tolerance");
                    }
                    if (!self || !settings.FindPropertyRelative("TemporaryPhysics").boolValue)
                        HoverControls.Field(settings, "ReleaseKinematic");
                    HoverControls.Field(settings, "Mode");
                    if (self)
                    {
                        HoverControls.Field(settings, "UseBulletImpulse");
                        if (settings.FindPropertyRelative("UseBulletImpulse").boolValue)
                            HoverControls.Field(settings, "BulletImpulseMultiplier");
                        else
                        {
                            HoverControls.Field(settings.FindPropertyRelative("SelfImpulse"), "Impulse");
                            HoverControls.Field(settings.FindPropertyRelative("SelfImpulse"), "Space");
                        }
                    }
                }
            if (!self && sections.Draw("Eject Impulses", "Rules run in list order. Each body receives the first matching impulse only.", settings, "Rules"))
                using (new StudioIndentScope())
                    Rules(settings.FindPropertyRelative("Rules"));
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
                    rule.isExpanded = StudioArrayGUI.Foldout(rule, new GUIContent("Impulse " + (index + 1),
                        "First matching group wins for each physical body."));
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
                using StudioIndentScope indent = new StudioIndentScope();
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
