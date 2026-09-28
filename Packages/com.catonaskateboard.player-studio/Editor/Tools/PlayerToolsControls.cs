using System;
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

        /// <summary>Edits tool identities, hierarchy targets and interchangeable slots.</summary>
        /// <param name="state">Workspace owning the Tools proposal.</param>
        /// <param name="owner">Window recorded for draft Undo.</param>
        /// <returns>True when module values changed.</returns>
        internal static bool Draw(PlayerStudioState state, UnityEngine.Object owner)
        {
            // All module values follow the common Apply/Discard transaction.
            SerializedObject data = state.Tools.GetEditor();
            if (data == null)
                return false;
            EditorGUI.BeginChangeCheck();
            DrawSettings(data, state.PreviewHost != null ? state.PreviewHost.transform : null, owner, state.Tools.Capture);
            bool changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                Undo.RecordObject(owner, "Edit Player Tools");
                state.Tools.Capture();
            }
            return changed;
        }

        /// <summary>Shows only the slot values used by each selected layout.</summary>
        /// <param name="data">Tools preset or detached module draft.</param>
        /// <param name="player">Selected player hierarchy, or an Inspector sample.</param>
        /// <param name="owner">Optional workspace owning draft Undo.</param>
        /// <param name="capture">Captures delayed menu choices into the detached proposal.</param>
        internal static void DrawSettings(SerializedObject data, Transform player, UnityEngine.Object owner = null, Action capture = null)
        {
            // Empty arrays are valid for players whose tools are assigned later.
            PlayerHierarchyMenu.DrawPath(data.FindProperty("RootPath"), player,
                new GUIContent("Hierarchy Root", data.FindProperty("RootPath").tooltip), true, owner, capture);
            Transform root = PlayerHierarchy.Resolve(player, data.FindProperty("RootPath").stringValue);
            EditorGUILayout.PropertyField(data.FindProperty("InitialTool"));
            bool cyclic = data.FindProperty("Layout").enumValueIndex == (int)PlayerToolLayout.Cyclic;
            bool moving = false;
            SerializedProperty entries = data.FindProperty("Tools");
            DrawCount(entries, "Tools", () => new PlayerToolEntry());
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
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("MoveVisual"), new GUIContent("Use Slots", entry.FindPropertyRelative("MoveVisual").tooltip));
                if (!entry.FindPropertyRelative("MoveVisual").boolValue)
                    continue;
                PlayerHierarchyMenu.DrawPath(entry.FindPropertyRelative("Path"), root,
                    new GUIContent("Target", entry.FindPropertyRelative("Path").tooltip), root != player, owner, capture);
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("PassivePose"), new GUIContent("Parked Pose", entry.FindPropertyRelative("PassivePose").tooltip), true);
                Transform target = PlayerHierarchy.Resolve(root, entry.FindPropertyRelative("Path").stringValue);
                using (new EditorGUI.DisabledScope(target == null || target == player))
                    if (GUILayout.Button(new GUIContent("Read Parked Pose", "Use this target's current local position, rotation and scale as its parked pose.")))
                        entry.FindPropertyRelative("PassivePose").boxedValue = PlayerToolPose.Read(target);
            }
            if (!moving)
                return;
            // Slot geometry is relevant only when at least one visual child participates.
            EditorGUILayout.PropertyField(data.FindProperty("Layout"));
            EditorGUILayout.PropertyField(data.FindProperty("SwitchDuration"));
            cyclic = data.FindProperty("Layout").enumValueIndex == (int)PlayerToolLayout.Cyclic;
            if (cyclic)
            {
                SerializedProperty slots = data.FindProperty("Slots");
                DrawCount(slots, "Slots", () => PlayerToolPose.Identity);
                for (int index = 0; index < slots.arraySize; index++)
                    EditorGUILayout.PropertyField(slots.GetArrayElementAtIndex(index),
                        new GUIContent(index == 0 ? "Slot 0 (Active)" : "Slot " + index, "Pose relative to the shared parent of all tool targets."), true);
            }
            else
                EditorGUILayout.PropertyField(data.FindProperty("ActivePose"), true);
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

        /// <summary>Initializes newly added entries with usable scale instead of Unity's zeroed struct values.</summary>
        /// <param name="array">Serialized tools or slots array.</param>
        /// <param name="label">Compact count label.</param>
        /// <param name="create">Initial value for an explicitly added entry.</param>
        internal static void DrawCount(SerializedProperty array, string label, Func<object> create)
        {
            // Existing entries are never normalized when the count changes.
            int count = EditorGUILayout.IntField(new GUIContent(label, array.tooltip), array.arraySize);
            if (count < 0 || count == array.arraySize)
                return;
            int previous = array.arraySize;
            array.arraySize = count;
            for (int index = previous; index < count; index++)
                array.GetArrayElementAtIndex(index).boxedValue = create();
        }

        #endregion

        #region Input

        /// <summary>Draws one shared cycling action or independent per-tool action selectors.</summary>
        /// <param name="data">Input preset or detached Input draft.</param>
        internal static void DrawInput(SerializedObject data)
        {
            // Hidden action roles retain their values when switching authoring mode.
            SerializedProperty settings = data.FindProperty("tools");
            SerializedProperty mode = settings.FindPropertyRelative("Mode");
            mode.enumValueIndex = (int)(PlayerToolInputMode)EditorGUILayout.EnumPopup(new GUIContent("Mode", mode.tooltip),
                (PlayerToolInputMode)mode.enumValueIndex);
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
