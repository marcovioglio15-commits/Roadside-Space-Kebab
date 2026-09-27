using CatOnASkateboard.StudioInput.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Shares conditional Tools and Use Tool controls between the module and asset inspectors.</summary>
    internal static class PlayerToolsControls
    {
        #region Methods

        #region Module

        /// <summary>Edits the retained Tools draft and exposes recording in the existing scene preview.</summary>
        /// <param name="state">Workspace owning the module and recording state.</param>
        /// <param name="owner">Window recorded for draft Undo.</param>
        /// <returns>True when module values changed.</returns>
        internal static bool Draw(PlayerStudioState state, Object owner)
        {
            // All module values follow the common Apply/Discard transaction.
            SerializedObject data = state.Tools.GetEditor();
            if (data == null)
                return false;
            EditorGUI.BeginChangeCheck();
            DrawSettings(data);
            bool changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                Undo.RecordObject(owner, "Edit Player Tools");
                state.Tools.Capture();
            }
            EditorGUI.BeginChangeCheck();
            state.Recording.Draw(state, owner);
            return EditorGUI.EndChangeCheck() || changed;
        }

        /// <summary>Shows only the slot values used by each selected layout.</summary>
        /// <param name="data">Tools preset or detached module draft.</param>
        internal static void DrawSettings(SerializedObject data)
        {
            // Empty arrays are valid for players whose tools are assigned later.
            EditorGUILayout.PropertyField(data.FindProperty("InitialTool"));
            bool cyclic = data.FindProperty("Layout").enumValueIndex == (int)PlayerToolLayout.Cyclic;
            bool moving = false;
            SerializedProperty entries = data.FindProperty("Tools");
            EditorGUILayout.PropertyField(entries.FindPropertyRelative("Array.size"), new GUIContent("Tools", entries.tooltip));
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                moving |= entry.FindPropertyRelative("MoveVisual").boolValue;
                PlayerTool tool = entry.FindPropertyRelative("Tool").objectReferenceValue as PlayerTool;
                entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, new GUIContent(tool != null ? tool.DisplayName : "Tool " + (index + 1), "Expand this tool's visual binding."), true);
                if (!entry.isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("Tool"));
                if (tool == null && GUILayout.Button(new GUIContent("Create Tool", "Save a tool identity with a custom name and optional switch animations.")))
                    entry.FindPropertyRelative("Tool").objectReferenceValue = PlayerToolsAuthoring.Create<PlayerTool>("Player Tool");
                if (tool != null && GUILayout.Button(new GUIContent("Edit Tool Asset", "Select the tool to edit its name and switch-in/switch-out animations.")))
                    Selection.activeObject = tool;
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("MoveVisual"));
                if (!entry.FindPropertyRelative("MoveVisual").boolValue)
                    continue;
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("Path"));
                if (!cyclic || data.FindProperty("InitialTool").objectReferenceValue == null)
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("PassivePose"), true);
            }
            if (!moving)
                return;
            // Slot geometry is relevant only when at least one visual child participates.
            EditorGUILayout.PropertyField(data.FindProperty("Layout"));
            EditorGUILayout.PropertyField(data.FindProperty("SwitchDuration"));
            cyclic = data.FindProperty("Layout").enumValueIndex == (int)PlayerToolLayout.Cyclic;
            EditorGUILayout.PropertyField(data.FindProperty(cyclic ? "Slots" : "ActivePose"), true);
            if (!cyclic)
                return;
            EditorGUILayout.PropertyField(data.FindProperty("SlotMotion"));
            if (data.FindProperty("SlotMotion").enumValueIndex == (int)PlayerToolSlotMotion.AroundPivot)
            {
                EditorGUILayout.PropertyField(data.FindProperty("Pivot"));
                EditorGUILayout.PropertyField(data.FindProperty("Axis"));
                EditorGUILayout.PropertyField(data.FindProperty("Clockwise"));
            }
        }

        #endregion

        #region Input

        /// <summary>Draws one shared cycling action or independent per-tool action selectors.</summary>
        /// <param name="data">Input preset or detached Input draft.</param>
        internal static void DrawInput(SerializedObject data)
        {
            // Hidden action roles retain their values when switching authoring mode.
            SerializedProperty settings = data.FindProperty("tools");
            EditorGUILayout.PropertyField(settings.FindPropertyRelative("Mode"));
            if (settings.FindPropertyRelative("Mode").enumValueIndex == (int)PlayerToolInputMode.SharedCycle)
                StudioInputActionMenu.Draw(data, "tools.UseTool", "PlayerStudio.UseTool", IsButton);
            else
            {
                SerializedProperty bindings = settings.FindPropertyRelative("Bindings");
                EditorGUILayout.PropertyField(bindings.FindPropertyRelative("Array.size"), new GUIContent("Tool Actions", bindings.tooltip));
                for (int index = 0; index < bindings.arraySize; index++)
                {
                    SerializedProperty binding = bindings.GetArrayElementAtIndex(index);
                    EditorGUILayout.PropertyField(binding.FindPropertyRelative("Tool"));
                    StudioInputActionMenu.Draw(data, binding.FindPropertyRelative("Action").propertyPath, "PlayerStudio.UseTool", IsButton);
                }
            }
        }

        /// <summary>Filters Use Tool menus to discrete selection commands.</summary>
        /// <param name="action">Imported action offered by the menu.</param>
        /// <returns>True for a Button action.</returns>
        private static bool IsButton(InputAction action)
        {
            // Device bindings remain owned by the Input Actions asset.
            return action.type == InputActionType.Button;
        }

        #endregion

        #endregion
    }
}
