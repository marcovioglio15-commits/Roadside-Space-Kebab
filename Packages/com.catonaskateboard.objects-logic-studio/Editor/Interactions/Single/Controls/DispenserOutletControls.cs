using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Authors world-output points with the same independent layout viewport used by Spray Sauce.</summary>
    internal static class DispenserOutletControls
    {
        #region Methods
        #region Drawing

        /// <summary>Shows the layout command and independent pose foldouts only for world output modes.</summary>
        /// <param name="settings">Dispenser proposal or preset settings.</param>
        internal static void Draw(SerializedProperty settings)
        {
            if (!ObjectGroupScope.Allows(settings.FindPropertyRelative("Outlets")))
                return;
            if (settings.serializedObject.targetObject is ObjectWorkspace workspace
                && StudioButton.Draw(new GUIContent("Outlet Layout", "Place output points against the dispenser preview.")))
            {
                settings.serializedObject.ApplyModifiedProperties();
                OutletLayoutWindow.Open(workspace, true);
            }
            SerializedProperty outlets = settings.FindPropertyRelative("Outlets");
            // Each output has independent coordinates and foldout state.
            for (int index = 0; index < outlets.arraySize; index++)
            {
                if (StudioArrayGUI.Header(outlets, index, new GUIContent("Outlet " + (index + 1), outlets.tooltip)))
                    break;
                SerializedProperty outlet = outlets.GetArrayElementAtIndex(index);
                if (!outlet.isExpanded)
                    continue;
                using StudioIndentScope outletContent = new StudioIndentScope();
                HoverControls.Field(outlet, "Position");
                HoverControls.Field(outlet, "Rotation");
            }
            StudioArrayGUI.Add(outlets, "Add Outlet", () => new DispenserOutlet());
        }

        #endregion
        #endregion
    }
}
