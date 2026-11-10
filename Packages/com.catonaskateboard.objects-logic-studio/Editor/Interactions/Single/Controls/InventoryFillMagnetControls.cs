using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits visual fill magnets and opens the shared placement preview.</summary>
    internal static class InventoryFillMagnetControls
    {
        #region Methods

        #region Drawing

        /// <summary>Shows the optional model and local fill layout for one threshold.</summary>
        /// <param name="step">Serialized step with Actual Item Fill enabled.</param>
        internal static void Draw(SerializedProperty step)
        {
            SerializedProperty fill = step.FindPropertyRelative("ItemFill");
            HoverControls.Field(fill, "VisualPrefab");
            if (step.serializedObject.targetObject is ObjectWorkspace workspace
                && StudioButton.Draw(new GUIContent("Fill Magnet Layout", "Move, rotate and scale visible units inside the container.")))
            {
                int threshold = step.FindPropertyRelative("Count").intValue;
                step.serializedObject.ApplyModifiedProperties();
                AssemblyPreviewWindow.OpenFill(workspace, threshold);
            }

            // Numeric fields remain available in the Inspector without opening a preview.
            SerializedProperty magnets = fill.FindPropertyRelative("Magnets");
            for (int index = 0; index < magnets.arraySize; index++)
            {
                SerializedProperty magnet = magnets.GetArrayElementAtIndex(index);
                if (StudioArrayGUI.Header(magnets, index, new GUIContent("Fill Unit " + (index + 1), magnets.tooltip)))
                    break;
                if (magnet.isExpanded)
                    Pose(magnet);
            }
            StudioArrayGUI.Add(magnets, "Add Fill Magnet", () => new InventoryFillMagnet());
        }

        /// <summary>Draws the transform fields shared by the list and the 3D preview.</summary>
        /// <param name="magnet">One fill placement or its equivalent preview magnet.</param>
        internal static void Pose(SerializedProperty magnet)
        {
            ObjectNumericControls.Vector(magnet.FindPropertyRelative("Position"), -1f, 1f);
            ObjectNumericControls.Vector(magnet.FindPropertyRelative("Rotation"), -360f, 360f);
            ObjectNumericControls.Vector(magnet.FindPropertyRelative("Scale"), 0.01f, 5f);
        }

        #endregion

        #endregion
    }
}
