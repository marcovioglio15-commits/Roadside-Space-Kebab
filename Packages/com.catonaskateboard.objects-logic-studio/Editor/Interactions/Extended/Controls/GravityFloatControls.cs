using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits perpetual float motion and opens its orbit center layout.</summary>
    internal static class GravityFloatControls
    {
        #region Methods

        #region Drawing

        /// <summary>Shows speed and layout controls only for the rotation currently used.</summary>
        /// <param name="motion">Continuous floating properties.</param>
        internal static void Draw(SerializedProperty motion)
        {
            // Height zero keeps the body on its floating path without oscillating.
            HoverControls.Field(motion, "Height");
            if (motion.FindPropertyRelative("Height").floatValue > 0f)
                HoverControls.Field(motion, "Frequency");
            HoverControls.Field(motion, "Acceleration");
            HoverControls.Field(motion, "Rotation");
            switch ((GravityFloatRotation)motion.FindPropertyRelative("Rotation").enumValueIndex)
            {
                case GravityFloatRotation.Spin:
                    HoverControls.Field(motion, "SpinSpeed");
                    break;
                case GravityFloatRotation.Orbit:
                    HoverControls.Field(motion, "OrbitSpeed");
                    DrawCenters(motion);
                    break;
            }
        }

        /// <summary>Edits center poses directly or in the shared 3D point preview.</summary>
        /// <param name="motion">Orbit settings containing the center list.</param>
        private static void DrawCenters(SerializedProperty motion)
        {
            if (motion.serializedObject.targetObject is ObjectWorkspace workspace
                && StudioButton.Draw(new GUIContent("Orbit Center Layout", "Position each center and rotate its orbit axis.")))
            {
                motion.serializedObject.ApplyModifiedProperties();
                OutletLayoutWindow.Open(workspace, ObjectPointLayout.OrbitCenters);
            }

            // Keep numeric entry available when no workspace can open the preview.
            SerializedProperty centers = motion.FindPropertyRelative("Centers");
            for (int index = 0; index < centers.arraySize; index++)
            {
                SerializedProperty center = centers.GetArrayElementAtIndex(index);
                if (StudioArrayGUI.Header(centers, index, new GUIContent("Orbit Center " + (index + 1), centers.tooltip)))
                    break;
                if (!center.isExpanded)
                    continue;
                ObjectNumericControls.Vector(center.FindPropertyRelative("Position"), -20f, 20f);
                ObjectNumericControls.Vector(center.FindPropertyRelative("Rotation"), -360f, 360f);
            }
            StudioArrayGUI.Add(centers, "Add Orbit Center", () => new GravityOrbitCenter());
        }

        #endregion

        #endregion
    }
}

