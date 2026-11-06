using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioInput.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows only the completion and progress settings used by the selected assembly mode.</summary>
    internal static class AssemblyFinalizationControls
    {
        #region Methods

        #region Controls

        /// <summary>Draws independent prolonged input or the existing immediate/pickup completion policy.</summary>
        /// <param name="settings">Detached product configuration or preset.</param>
        /// <param name="sections">Retained foldout states used by the assembly tool.</param>
        internal static void Draw(SerializedProperty settings, ObjectStudioSections sections)
        {
            if (!sections.Draw("Assembly completion", "Set the hold to complete a ready recipe or bind a dismantle command.", settings, "Charge", "Dismantle"))
                return;
            using StudioIndentScope sectionContent1 = new StudioIndentScope();
            GameObject source = HierarchyPathMenu.Source(settings);
            bool hasGrab = source == null || source.GetComponent<ObjectGrab>() != null;
            SerializedProperty charge = settings.FindPropertyRelative("Charge");
            StudioInputActionMenu.Draw(settings.serializedObject, charge.FindPropertyRelative("Action").propertyPath,
                "ObjectsLogicStudio.Assembly.Charge", ExtendedInteractionControls.Button);
            HoverControls.Field(charge, "Duration");
            HoverControls.Field(charge, "CompleteOnRelease");
            if (hasGrab)
                HoverControls.Field(charge, "GrabOnComplete");
            TransferInteractionControls.Target(charge.FindPropertyRelative("Target"), sections);
            DrawHud(charge, sections);
            DrawDismantle(settings.FindPropertyRelative("Dismantle"));
        }

        /// <summary>Shows the dismantle options enabled by its assigned action and switches.</summary>
        /// <param name="settings">Detached dismantle settings.</param>
        private static void DrawDismantle(SerializedProperty settings)
        {
            SerializedProperty action = settings.FindPropertyRelative("Action");
            StudioInputActionMenu.Draw(settings.serializedObject, action.propertyPath,
                "ObjectsLogicStudio.Assembly.Dismantle", ExtendedInteractionControls.Button);
            if (action.objectReferenceValue == null)
                return;
            HoverControls.Field(settings, "Hold");
            if (settings.FindPropertyRelative("Hold").boolValue)
                HoverControls.Field(settings, "Duration");
            HoverControls.Field(settings, "Lift");
            HoverControls.Field(settings, "Spread");
            HoverControls.Field(settings, "CameraImpact");
            if (settings.FindPropertyRelative("CameraImpact").boolValue)
            {
                HoverControls.Field(settings, "CameraDuration");
                HoverControls.Field(settings, "CameraArcHeight");
                HoverControls.Field(settings, "CameraClearance");
            }
        }

        /// <summary>Exposes visual details only while the configured charge HUD is requested.</summary>
        /// <param name="charge">Active prolonged assembly configuration.</param>
        /// <param name="sections">Shared foldout state.</param>
        private static void DrawHud(SerializedProperty charge, ObjectStudioSections sections)
        {
            HoverControls.Field(charge, "ShowHud");
            if (!charge.FindPropertyRelative("ShowHud").boolValue
                || !sections.Draw("Assembly Progress", "Appearance of the observer's existing shared charge indicator.", charge,
                    "Icon", "IconColor", "RingColor", "TrackColor", "Size", "Thickness", "OffsetX", "OffsetY", "Drain", "Clockwise"))
                return;
            using StudioIndentScope indent = new StudioIndentScope();
            HoverControls.Field(charge, "Icon");
            HoverControls.Field(charge, "IconColor");
            HoverControls.Field(charge, "RingColor");
            HoverControls.Field(charge, "TrackColor");
            HoverControls.Field(charge, "Size");
            HoverControls.Field(charge, "Thickness");
            HoverControls.Field(charge, "OffsetX");
            HoverControls.Field(charge, "OffsetY");
            HoverControls.Field(charge, "Drain");
            HoverControls.Field(charge, "Clockwise");
        }

        #endregion

        #endregion
    }
}
