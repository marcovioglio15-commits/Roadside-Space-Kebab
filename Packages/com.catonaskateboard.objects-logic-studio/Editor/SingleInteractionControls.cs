using CatOnASkateboard.StudioInput.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws only settings relevant to the selected single-interaction feature and its active modes.</summary>
    internal static class SingleInteractionControls
    {
        #region Methods

        #region Form

        /// <summary>Edits the retained feature proposal without changing the source component.</summary>
        /// <param name="data">Serialized persistent workspace.</param>
        /// <param name="state">Workspace owning the proposal and foldout sections.</param>
        /// <returns>True when any serialized setting changed during this draw.</returns>
        internal static bool Draw(SerializedObject data, ObjectWorkspace state)
        {
            // The action menu is shared with Player Studio, including map navigation and duplicate-name handling.
            data.Update();
            SerializedProperty draft = data.FindProperty("Single.Draft");
            Field(draft, "Name");
            Field(draft, "Enabled");
            StudioInputActionMenu.Draw(data, "Single.Draft.Action", "ObjectsLogicStudio.Button", Button);
            switch (state.Single.Kind)
            {
                case SingleInteractionKind.Grab:
                    DrawGrab(draft.FindPropertyRelative("Grab"), state.Sections);
                    if (state.Sections.Draw("Grab Debug", "Draw selected-object targeting and carry guides."))
                        using (new EditorGUI.IndentLevelScope())
                            Field(draft, "DrawGizmos");
                    break;
                case SingleInteractionKind.Drop:
                case SingleInteractionKind.Throw:
                    DrawRelease(draft.FindPropertyRelative("Release"), state.Sections);
                    if (state.Single.Kind == SingleInteractionKind.Throw)
                        DrawTrajectory(draft.FindPropertyRelative("Throw"), state.Sections);
                    break;
            }
            InteractionTagControls.Draw(draft.FindPropertyRelative("TagChange"), state.Sections);
            bool changed = data.ApplyModifiedProperties();
            if (changed)
                state.Persist();
            return changed;
        }

        /// <summary>Accepts Button actions regardless of the currently connected devices.</summary>
        /// <param name="action">Imported action inspected during a menu-cache rebuild.</param>
        /// <returns>True for a one-shot button action.</returns>
        private static bool Button(InputAction action)
        {
            // Authored tap, hold and press interactions determine when the button performs.
            return action.type == InputActionType.Button;
        }

        /// <summary>Draws targeting and carry settings with dependent controls hidden.</summary>
        /// <param name="grab">Detached Grab settings.</param>
        /// <param name="sections">Persistent foldout navigation.</param>
        internal static void DrawGrab(SerializedProperty grab, ObjectStudioSections sections)
        {
            // Grab targeting is independent of Hover and remains available on objects without labels.
            if (sections.Draw("Grab Detection", "Choose the object's grab range and view targeting."))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(grab, "Distance");
                    Field(grab, "TargetMode");
                    if (grab.FindPropertyRelative("TargetMode").enumValueIndex == (int)HoverTargetMode.ViewCenter)
                        Field(grab, "CenterRadius");
                    Field(grab, "TargetOffset");
                    Field(grab, "ObstacleMask");
                }
            if (!sections.Draw("Carry", "Configure the held pose, pickup transition, world collisions and hover visibility."))
                return;
            using EditorGUI.IndentLevelScope sectionIndent = new EditorGUI.IndentLevelScope();
            Field(grab, "Space");
            Field(grab, "Offset");
            Field(grab, "Rotation");
            Field(grab, "Instant");
            if (!grab.FindPropertyRelative("Instant").boolValue)
                Field(grab, "TransitionDuration");
            Field(grab, "WorldCollisions");
            if (grab.FindPropertyRelative("WorldCollisions").boolValue)
            {
                Field(grab, "FollowSpeed");
                Field(grab, "RecoveryResponse");
                Field(grab, "RotationSpeed");
                Field(grab, "CollisionPadding");
                Field(grab, "ContactRotation");
                if (grab.FindPropertyRelative("ContactRotation").boolValue)
                {
                    Field(grab, "ContactAngle");
                    Field(grab, "ContactResponse");
                }
            }
            Field(grab, "ShowHover");
        }

        /// <summary>Draws release body and surface settings shared by Drop and Throw.</summary>
        /// <param name="release">Detached release physics configuration.</param>
        /// <param name="sections">Persistent foldout navigation.</param>
        internal static void DrawRelease(SerializedProperty release, ObjectStudioSections sections)
        {
            // Overrides expose their coefficients only when they will actually affect a release.
            if (sections.Draw("Release Body", "Configure mass, air resistance, gravity, constraints and continuous collision detection."))
                using (new EditorGUI.IndentLevelScope())
                {
                    Field(release, "OverrideBody");
                    if (release.FindPropertyRelative("OverrideBody").boolValue)
                    {
                        Field(release, "Mass");
                        Field(release, "LinearDamping");
                        Field(release, "AngularDamping");
                        Field(release, "Gravity");
                        Field(release, "Constraints");
                        Field(release, "CollisionDetection");
                        Field(release, "MaxAngularSpeed");
                    }
                }
            if (!sections.Draw("Release Surface", "Configure friction and bounce against floors and other physical objects."))
                return;
            using EditorGUI.IndentLevelScope sectionIndent = new EditorGUI.IndentLevelScope();
            Field(release, "OverrideSurface");
            if (!release.FindPropertyRelative("OverrideSurface").boolValue)
                return;
            Field(release, "StaticFriction");
            Field(release, "DynamicFriction");
            Field(release, "Bounciness");
            Field(release, "FrictionCombine");
            Field(release, "BounceCombine");
        }

        /// <summary>Shares launch controls between the Throw card and its reusable preset.</summary>
        /// <param name="trajectory">Serialized launch settings.</param>
        /// <param name="sections">Retained foldout visibility.</param>
        internal static void DrawTrajectory(SerializedProperty trajectory, ObjectStudioSections sections)
        {
            // Drop never exposes a trajectory because it releases the body from rest.
            if (!sections.Draw("Trajectory", "Configure launch strength, direction and spin relative to the gameplay camera."))
                return;
            using EditorGUI.IndentLevelScope sectionIndent = new EditorGUI.IndentLevelScope();
            Field(trajectory, "Mode");
            Field(trajectory, "Strength");
            Field(trajectory, "Yaw");
            Field(trajectory, "Elevation");
            Field(trajectory, "Spin");
        }

        /// <summary>Uses native tooltips and full vector controls for one serialized setting.</summary>
        /// <param name="owner">Serialized configuration containing the field.</param>
        /// <param name="name">Exact relative property name.</param>
        private static void Field(SerializedProperty owner, string name)
        {
            // One field renderer keeps labels, tooltips and indentation consistent across cards.
            EditorGUILayout.PropertyField(owner.FindPropertyRelative(name), true);
        }

        #endregion

        #endregion
    }
}
