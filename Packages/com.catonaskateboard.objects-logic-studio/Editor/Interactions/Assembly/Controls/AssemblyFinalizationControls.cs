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
            // A product with Grab can still choose a completely independent assembly action and target.
            HoverControls.Field(settings, "ProlongedAssembly");
            GameObject source = HierarchyPathMenu.Source(settings, true);
            bool hasGrab = source == null || source.GetComponent<ObjectGrab>() != null;
            if (!settings.FindPropertyRelative("ProlongedAssembly").boolValue)
            {
                HoverControls.Field(settings, "FinalizeOnGrab");
                if (settings.FindPropertyRelative("FinalizeOnGrab").boolValue && (source == null || !hasGrab))
                {
                    StudioInputActionMenu.Draw(settings.serializedObject, settings.FindPropertyRelative("FinalizeAction").propertyPath,
                        "ObjectsLogicStudio.Assembly.Finalize", ExtendedInteractionControls.Button);
                    TransferInteractionControls.Target(settings.FindPropertyRelative("FinalizeTarget"), sections);
                }
                return;
            }
            SerializedProperty charge = settings.FindPropertyRelative("Charge");
            StudioInputActionMenu.Draw(settings.serializedObject, charge.FindPropertyRelative("Action").propertyPath,
                "ObjectsLogicStudio.Assembly.Charge", ExtendedInteractionControls.Button);
            HoverControls.Field(charge, "Duration");
            HoverControls.Field(charge, "CompleteOnRelease");
            if (hasGrab)
                HoverControls.Field(charge, "GrabOnComplete");
            TransferInteractionControls.Target(charge.FindPropertyRelative("Target"), sections);
            DrawHud(charge, sections);
        }

        /// <summary>Exposes visual details only while the authored charge HUD is requested.</summary>
        /// <param name="charge">Active prolonged assembly configuration.</param>
        /// <param name="sections">Shared foldout state.</param>
        private static void DrawHud(SerializedProperty charge, ObjectStudioSections sections)
        {
            HoverControls.Field(charge, "ShowHud");
            if (!charge.FindPropertyRelative("ShowHud").boolValue
                || !sections.Draw("Assembly Progress", "Appearance of the observer's existing shared charge indicator.", charge,
                    "Icon", "IconColor", "RingColor", "TrackColor", "Size", "Thickness", "OffsetX", "OffsetY", "Drain", "Clockwise"))
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
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
