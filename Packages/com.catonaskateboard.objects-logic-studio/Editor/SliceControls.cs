using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits ordered slice steps through the same controls for prefab drafts and reusable presets.</summary>
    internal static class SliceControls
    {
        #region Methods

        #region Drawing

        /// <summary>Shows targeting and explicit sequence rows without exposing unrelated interaction settings.</summary>
        /// <param name="settings">Serialized Slice configuration.</param>
        /// <param name="sections">Persisted foldout state.</param>
        internal static void Draw(SerializedProperty settings, ObjectStudioSections sections)
        {
            // Exact cursor targeting has no centre-radius setting.
            if (sections.Draw("Slice Targeting", "Choose reach, aiming and priority for the next cut."))
                using (new EditorGUI.IndentLevelScope())
                {
                    SerializedProperty target = settings.FindPropertyRelative("Target");
                    HoverControls.Field(target, "Distance");
                    HoverControls.Field(target, "Mode");
                    if (target.FindPropertyRelative("Mode").enumValueIndex == (int)HoverTargetMode.ViewCenter)
                        HoverControls.Field(target, "CenterRadius");
                    HoverControls.Field(target, "Offset");
                    HoverControls.Field(target, "ObstacleMask");
                    HoverControls.Field(settings, "Priority");
                }
            if (!sections.Draw("Slice Sequence", "Each performed press commits the next step. The last step completes this interaction."))
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            HoverControls.Field(settings, "Interval");
            SerializedProperty steps = settings.FindPropertyRelative("Steps");
            for (int index = 0; index < steps.arraySize; index++)
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    if (Header(steps, step, index))
                        break;
                    if (!step.isExpanded)
                        continue;
                    using (new EditorGUI.IndentLevelScope())
                    {
                        HoverControls.Field(step, "Name");
                        HoverControls.Field(step, "Meshes");
                        HoverControls.Field(step, "Materials");
                        DrawSpawns(step.FindPropertyRelative("Spawns"));
                    }
                }
            }
            if (GUILayout.Button(new GUIContent("+ Add Slice Step", "Append an independent cut to the end of this sequence.")))
            {
                steps.arraySize++;
                SerializedProperty added = steps.GetArrayElementAtIndex(steps.arraySize - 1);
                added.FindPropertyRelative("Name").stringValue = "Slice " + steps.arraySize;
                added.FindPropertyRelative("Meshes").arraySize = 0;
                added.FindPropertyRelative("Materials").arraySize = 0;
                added.FindPropertyRelative("Spawns").arraySize = 0;
                added.isExpanded = true;
            }
        }

        /// <summary>Provides a foldout and explicit reordering controls for a single cut.</summary>
        /// <param name="steps">Ordered sequence receiving structural edits.</param>
        /// <param name="step">Current step.</param>
        /// <param name="index">Current index in the sequence.</param>
        /// <returns>True when the array changed and drawing must restart.</returns>
        private static bool Header(SerializedProperty steps, SerializedProperty step, int index)
        {
            // Stop the current loop after a structural change to avoid drawing stale property handles.
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
            step.isExpanded = EditorGUILayout.Foldout(step.isExpanded,
                new GUIContent((index + 1) + ". " + step.FindPropertyRelative("Name").stringValue, "Edit this cut's appearance and outputs."), true);
            using (new EditorGUI.DisabledScope(index == 0))
                if (GUILayout.Button(new GUIContent("↑", "Move this step earlier."), GUILayout.Width(26f)))
                    return steps.MoveArrayElement(index, index - 1);
            using (new EditorGUI.DisabledScope(index == steps.arraySize - 1))
                if (GUILayout.Button(new GUIContent("↓", "Move this step later."), GUILayout.Width(26f)))
                    return steps.MoveArrayElement(index, index + 1);
            if (!GUILayout.Button(new GUIContent("−", "Remove this step."), GUILayout.Width(26f)))
                return false;
            steps.DeleteArrayElementAtIndex(index);
            return true;
        }

        /// <summary>Edits output prefab rows with local placement and asset-only selection.</summary>
        /// <param name="spawns">Output array for the current step.</param>
        private static void DrawSpawns(SerializedProperty spawns)
        {
            // Scene instances cannot become persistent spawn dependencies.
            spawns.isExpanded = EditorGUILayout.Foldout(spawns.isExpanded,
                new GUIContent("Prefab Outputs (" + spawns.arraySize + ")", spawns.tooltip), true);
            if (!spawns.isExpanded)
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            for (int index = 0; index < spawns.arraySize; index++)
            {
                SerializedProperty spawn = spawns.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.HorizontalScope())
                {
                    TransferInteractionControls.Prefab(spawn.FindPropertyRelative("Prefab"));
                    if (GUILayout.Button(new GUIContent("−", "Remove this prefab output."), GUILayout.Width(26f)))
                    {
                        spawns.DeleteArrayElementAtIndex(index);
                        break;
                    }
                }
                HoverControls.Field(spawn, "Position");
                HoverControls.Field(spawn, "Rotation");
            }
            if (!GUILayout.Button(new GUIContent("+ Add Prefab Output", "Generate another prefab at this cut's local output pose.")))
                return;
            spawns.arraySize++;
            SerializedProperty added = spawns.GetArrayElementAtIndex(spawns.arraySize - 1);
            added.FindPropertyRelative("Prefab").objectReferenceValue = null;
            added.FindPropertyRelative("Position").vector3Value = Vector3.zero;
            added.FindPropertyRelative("Rotation").vector3Value = Vector3.zero;
        }

        #endregion

        #endregion
    }
}
