using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits floating supply and the sequence of visible counts.</summary>
    internal static class ScoopDisplayControls
    {
        #region Methods

        #region Drawing

        /// <summary>Shows magnet placement and motion only for a Scoop Only dispenser.</summary>
        /// <param name="settings">Detached dispenser settings.</param>
        /// <param name="sections">Shared foldout state.</param>
        internal static void Draw(SerializedProperty settings, ObjectStudioSections sections)
        {
            SerializedProperty display = settings.FindPropertyRelative("ScoopDisplay");
            bool unlimited = settings.FindPropertyRelative("Unlimited").boolValue;
            if (!sections.Draw("Floating Supply", "Place one magnet per visible portion.", settings, "ScoopDisplay"))
                return;
            using StudioIndentScope indent = new StudioIndentScope();
            if (unlimited)
                HoverControls.Field(display, "VisibleCount");
            SerializedProperty drift = display.FindPropertyRelative("Drift");
            ObjectNumericControls.Vector(drift, 0f, 0.25f);
            HoverControls.Field(display, "ScoopLimit");
            HoverControls.Field(display, "RestockForNewContainer");
            if (display.FindPropertyRelative("RestockForNewContainer").boolValue)
            {
                HoverControls.Field(display, "RestockPrefab");
                if (display.FindPropertyRelative("RestockPrefab").objectReferenceValue != null)
                    HoverControls.Field(display, "RestockScale");
            }
            HoverControls.Field(display, "Speed");
            if (drift.vector3Value.y > 0f)
                HoverControls.Field(display, "BobCrestPower");

            // Reuse the isolated outlet viewport without instantiating the source prefab.
            if (settings.serializedObject.targetObject is ObjectWorkspace workspace
                && StudioButton.Draw(new GUIContent("Magnet Layout", "Place floating portions against the dispenser mesh.")))
            {
                settings.serializedObject.ApplyModifiedProperties();
                OutletLayoutWindow.Open(workspace, ObjectPointLayout.Magnets);
            }

            SerializedProperty magnets = display.FindPropertyRelative("Magnets");
            for (int index = 0; index < magnets.arraySize; index++)
            {
                SerializedProperty magnet = magnets.GetArrayElementAtIndex(index);
                string name = "Magnet " + (index + 1);
                if (StudioArrayGUI.Header(magnets, index, new GUIContent(name, magnets.tooltip)))
                    break;
                if (magnet.isExpanded)
                    ObjectNumericControls.Vector(magnet, -2f, 2f, name);
            }
            StudioArrayGUI.Add(magnets, "Add Magnet", () => Vector3.zero);
            DrawSteps(display.FindPropertyRelative("Steps"),
                unlimited ? display.FindPropertyRelative("VisibleCount").intValue : settings.FindPropertyRelative("Stock").intValue);
        }

        /// <summary>Draws the remaining count for each successive scoop.</summary>
        /// <param name="steps">Ordered visible counts.</param>
        /// <param name="stock">Number of portions shown at activation.</param>
        private static void DrawSteps(SerializedProperty steps, int stock)
        {
            steps.isExpanded = StudioArrayGUI.Foldout(steps, new GUIContent("Dispense Steps", steps.tooltip));
            if (!steps.isExpanded)
                return;
            using StudioIndentScope indent = new StudioIndentScope();
            for (int index = 0; index < steps.arraySize; index++)
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(index);
                if (StudioArrayGUI.Header(steps, index, new GUIContent("Step " + (index + 1), steps.tooltip)))
                    break;
                if (step.isExpanded)
                    ObjectNumericControls.Integer(step, 0, Mathf.Max(1, stock - 1));
            }
            StudioArrayGUI.Add(steps, "Add Dispense Step", () => 0);
        }

        #endregion

        #endregion
    }
}
