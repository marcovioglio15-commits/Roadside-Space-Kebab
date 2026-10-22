using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
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
            SerializedObject data = state.Tools.GetEditor(owner);
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
            DrawArm(data.FindProperty("Arm"), root, player, owner, capture);
            StudioGUI.PropertyField(data.FindProperty("SingleTool"));
            bool single = data.FindProperty("SingleTool").boolValue;
            if (!single)
                StudioGUI.PropertyField(data.FindProperty("InitialTool"));
            bool cyclic = data.FindProperty("Layout").enumValueIndex == (int)PlayerToolLayout.Cyclic;
            bool moving = false;
            SerializedProperty entries = data.FindProperty("Tools");
            if (!single || entries.arraySize == 0)
                StudioArrayGUI.Add(entries, "Add Tool", () => new PlayerToolEntry());
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                moving |= !single && entry.FindPropertyRelative("MoveVisual").boolValue;
                PlayerTool tool = entry.FindPropertyRelative("Tool").objectReferenceValue as PlayerTool;
                if (StudioArrayGUI.Header(entries, index, new GUIContent(tool != null ? tool.DisplayName : "Tool " + (index + 1), "Expand this tool's visual binding.")))
                    break;
                if (!entry.isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                StudioGUI.PropertyField(entry.FindPropertyRelative("Tool"));
                if (tool == null && StudioButton.Draw(new GUIContent("Create Tool", "Save a tool identity with a custom name and optional switch animations.")))
                    entry.FindPropertyRelative("Tool").objectReferenceValue = PlayerToolsAuthoring.Create<PlayerTool>("Player Tool");
                if (tool != null && StudioButton.Draw(new GUIContent("Edit Tool", "Open this tool's settings and muzzle preview in a dedicated editor.")))
                    PlayerToolEditor.Open(tool, root, !single);
                if (single)
                    continue;
                StudioGUI.PropertyField(entry.FindPropertyRelative("MoveVisual"), new GUIContent("Use Slots", entry.FindPropertyRelative("MoveVisual").tooltip));
                if (!entry.FindPropertyRelative("MoveVisual").boolValue)
                    continue;
                PlayerHierarchyMenu.DrawPath(entry.FindPropertyRelative("Path"), root,
                    new GUIContent("Target", entry.FindPropertyRelative("Path").tooltip), root != player, owner, capture);
                PlayerToolFields.Pose(entry.FindPropertyRelative("PassivePose"), "Parked Pose");
                Transform target = PlayerHierarchy.Resolve(root, entry.FindPropertyRelative("Path").stringValue);
                using (new EditorGUI.DisabledScope(target == null || target == player))
                    if (StudioButton.Draw(new GUIContent("Read Parked Pose", "Use this target's current local position, rotation and scale as its parked pose.")))
                        entry.FindPropertyRelative("PassivePose").boxedValue = PlayerToolPose.Read(target);
            }
            if (!moving)
                return;
            // Slot geometry is relevant only when at least one visual child participates.
            StudioGUI.PropertyField(data.FindProperty("Layout"));
            StudioGUI.PropertyField(data.FindProperty("SwitchDuration"));
            cyclic = data.FindProperty("Layout").enumValueIndex == (int)PlayerToolLayout.Cyclic;
            if (cyclic)
            {
                SerializedProperty slots = data.FindProperty("Slots");
                StudioArrayGUI.Add(slots, "Add Slot", () => PlayerToolPose.Identity);
                for (int index = 0; index < slots.arraySize; index++)
                {
                    if (StudioArrayGUI.Header(slots, index, new GUIContent(index == 0 ? "Slot 0 (Active)" : "Slot " + index, "Pose relative to the shared parent of all tool targets.")))
                        break;
                    if (slots.GetArrayElementAtIndex(index).isExpanded)
                        PlayerToolFields.Pose(slots.GetArrayElementAtIndex(index));
                }
            }
            else
                PlayerToolFields.Pose(data.FindProperty("ActivePose"));
            if (!cyclic)
            {
                StudioGUI.PropertyField(data.FindProperty("SlotRotation"));
                return;
            }
            StudioGUI.PropertyField(data.FindProperty("SlotMotion"));
            if (data.FindProperty("SlotMotion").enumValueIndex == (int)PlayerToolSlotMotion.AroundPivot)
            {
                PlayerToolFields.Vector(data.FindProperty("Pivot"), -10f, 10f);
                PlayerToolFields.Vector(data.FindProperty("Axis"), -1f, 1f);
                StudioGUI.PropertyField(data.FindProperty("Clockwise"));
            }
            else
                StudioGUI.PropertyField(data.FindProperty("SlotRotation"));
        }

        #endregion

        #region Arm

        /// <summary>Edits only enabled arm bindings and optionally creates editable vertical or horizontal idle keys.</summary>
        /// <param name="settings">Serialized arm settings.</param>
        /// <param name="root">Tools hierarchy root.</param>
        /// <param name="player">Protected player root.</param>
        /// <param name="owner">Window owning draft Undo.</param>
        /// <param name="capture">Captures delayed hierarchy selections.</param>
        private static void DrawArm(SerializedProperty settings, Transform root, Transform player, UnityEngine.Object owner, Action capture)
        {
            StudioGUI.PropertyField(settings.FindPropertyRelative("Enabled"), new GUIContent("Player Arm", settings.FindPropertyRelative("Enabled").tooltip));
            if (!settings.FindPropertyRelative("Enabled").boolValue)
                return;
            PlayerHierarchyMenu.DrawPath(settings.FindPropertyRelative("Path"), root,
                new GUIContent("Arm Target", settings.FindPropertyRelative("Path").tooltip), root != player, owner, capture);
            StudioGUI.PropertyField(settings.FindPropertyRelative("SmoothRotation"));
            if (settings.FindPropertyRelative("SmoothRotation").boolValue)
                StudioFields.Draw(settings, "RotationResponse", "MaximumRotationLag");
            StudioGUI.PropertyField(settings.FindPropertyRelative("IdleEnabled"));
            if (!settings.FindPropertyRelative("IdleEnabled").boolValue)
                return;
            PlayerToolFields.Animation(settings.FindPropertyRelative("Idle"), PlayerHierarchy.Resolve(root, settings.FindPropertyRelative("Path").stringValue));
            if (settings.FindPropertyRelative("Idle").objectReferenceValue != null)
                return;
            using StudioButton.RowScope row = new StudioButton.RowScope();
            if (StudioButton.Draw(new GUIContent("Create Vertical Idle", "Save an editable five-key vertical sway relative to the selected arm.")))
                settings.FindPropertyRelative("Idle").objectReferenceValue = PlayerToolAnimationAuthoring.CreateIdle(false);
            if (StudioButton.Draw(new GUIContent("Create Horizontal Idle", "Save an editable five-key horizontal sway relative to the selected arm.")))
                settings.FindPropertyRelative("Idle").objectReferenceValue = PlayerToolAnimationAuthoring.CreateIdle(true);
        }

        #endregion

        #region Input

        /// <summary>Draws one shared cycling action or independent per-tool action selectors.</summary>
        /// <param name="data">Input preset or detached Input draft.</param>
        /// <param name="switching">Whether selection settings are used by the selected player.</param>
        internal static void DrawInput(SerializedObject data, bool switching = true)
        {
            // Hidden action roles retain their values when switching authoring mode.
            SerializedProperty settings = data.FindProperty("tools");
            StudioInputActionMenu.Draw(data, "tools.Activate", "PlayerStudio.ToolButton", IsButton);
            StudioInputActionMenu.Draw(data, "tools.Reload", "PlayerStudio.ToolButton", IsButton);
            if (!switching)
                return;
            SerializedProperty mode = settings.FindPropertyRelative("Mode");
            mode.enumValueIndex = (int)(PlayerToolInputMode)StudioGUI.EnumPopup(StudioFieldMenu.Value(mode, new GUIContent("Mode", mode.tooltip)),
                (PlayerToolInputMode)mode.enumValueIndex);
            if (settings.FindPropertyRelative("Mode").enumValueIndex == (int)PlayerToolInputMode.SharedCycle)
                StudioInputActionMenu.Draw(data, "tools.UseTool", "PlayerStudio.UseTool", IsButton);
            else
            {
                SerializedProperty bindings = settings.FindPropertyRelative("Bindings");
                StudioArrayGUI.Add(bindings, "Add Tool Action", () => new PlayerToolInputBinding());
                for (int index = 0; index < bindings.arraySize; index++)
                {
                    if (StudioArrayGUI.Header(bindings, index, new GUIContent("Tool Action " + (index + 1), bindings.tooltip)))
                        break;
                    SerializedProperty binding = bindings.GetArrayElementAtIndex(index);
                    if (!binding.isExpanded)
                        continue;
                    StudioGUI.PropertyField(binding.FindPropertyRelative("Tool"));
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
