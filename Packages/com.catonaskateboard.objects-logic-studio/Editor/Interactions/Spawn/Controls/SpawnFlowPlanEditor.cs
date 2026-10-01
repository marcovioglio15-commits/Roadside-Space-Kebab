using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Authors day scenes, prefab completion links and walk paths through explicit selectors.</summary>
    [CustomEditor(typeof(SpawnFlowPlan))]
    public sealed class SpawnFlowPlanEditor : UnityEditor.Editor
    {
        #region Methods
        #region Plan

        /// <summary>Edits the shared plan with Undo and compact, conditional controls.</summary>
        public override void OnInspectorGUI()
        {
            // A dedicated asset editor makes changes visible to every manager referencing the same plan.
            serializedObject.Update();
            Field(serializedObject, "Loop");
            if (!serializedObject.FindProperty("Loop").boolValue)
                Scene(serializedObject.FindProperty("MainMenu"), "Main Menu");
            Scene(serializedObject.FindProperty("PauseScene"), "Pause Scene");
            Field(serializedObject, "FadeOut");
            Field(serializedObject, "BlackDelay");
            Field(serializedObject, "FadeIn");
            Field(serializedObject, "FixedSeed");
            if (serializedObject.FindProperty("FixedSeed").boolValue)
                Field(serializedObject, "Seed");
            Days(serializedObject.FindProperty("Days"));
            serializedObject.ApplyModifiedProperties();
            if (!((SpawnFlowPlan)target).TryValidate(out string warning))
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
            if (StudioButton.Draw(new GUIContent("Register Scenes", "Enable the plan's days, pause scene and final menu in Build Settings without removing existing entries.")))
                RegisterScenes((SpawnFlowPlan)target);
        }

        /// <summary>Links a plan from an interaction card with direct creation and editing commands.</summary>
        /// <param name="property">Shared plan reference.</param>
        internal static void Link(SerializedProperty property)
        {
            // The edit window works independently of Inspector locks or selection state.
            EditorGUILayout.PropertyField(property, new GUIContent("Day Plan", property.tooltip));
            using StudioButton.RowScope row = new StudioButton.RowScope();
            if (StudioButton.Draw(new GUIContent("New", "Create a shared day plan asset.")))
            {
                string path = EditorUtility.SaveFilePanelInProject("Create Day Plan", "DayFlowPlan", "asset", "Choose a shared plan location.");
                if (!string.IsNullOrEmpty(path))
                {
                    SpawnFlowPlan plan = CreateInstance<SpawnFlowPlan>();
                    AssetDatabase.CreateAsset(plan, path);
                    property.objectReferenceValue = plan;
                    SpawnFlowPlanWindow.Open(plan);
                }
            }
            using (new EditorGUI.DisabledScope(property.objectReferenceValue == null))
                if (StudioButton.Draw(new GUIContent("Edit", "Open the linked days and keyframes in a dedicated window.")))
                    SpawnFlowPlanWindow.Open((SpawnFlowPlan)property.objectReferenceValue);
        }

        /// <summary>Draws reorderable day entries with fixed or inclusive random spawn counts.</summary>
        /// <param name="days">Ordered day array.</param>
        private static void Days(SerializedProperty days)
        {
            // Reordering days changes scene progression while preserving each day's independent step list.
            for (int index = 0; index < days.arraySize; index++)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    SerializedProperty day = days.GetArrayElementAtIndex(index);
                    if (Header(days, index, "Day " + (index + 1)))
                        break;
                    if (!day.isExpanded)
                        continue;
                    using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                    HoverControls.Field(day, "Name");
                    Scene(day.FindPropertyRelative("Scene"), "Scene");
                    HoverControls.Field(day, "RandomCount");
                    if (day.FindPropertyRelative("RandomCount").boolValue)
                    {
                        HoverControls.Field(day, "Minimum");
                        HoverControls.Field(day, "Maximum");
                    }
                    else
                        HoverControls.Field(day, "Count");
                    HoverControls.Field(day, "Selection");
                    Steps(day.FindPropertyRelative("Steps"), day.FindPropertyRelative("Selection").enumValueIndex == (int)SpawnSelection.WeightedRandom);
                }
            if (StudioButton.Draw(new GUIContent("+ Add Day", "Append a new day to the scene sequence.")))
            {
                days.arraySize++;
                SerializedProperty added = days.GetArrayElementAtIndex(days.arraySize - 1);
                added.FindPropertyRelative("Name").stringValue = "Day " + days.arraySize;
                added.FindPropertyRelative("Scene").stringValue = string.Empty;
                added.FindPropertyRelative("RandomCount").boolValue = false;
                added.FindPropertyRelative("Count").intValue = 1;
                added.FindPropertyRelative("Minimum").intValue = 1;
                added.FindPropertyRelative("Maximum").intValue = 3;
                added.FindPropertyRelative("Selection").enumValueIndex = (int)SpawnSelection.Sequence;
                added.FindPropertyRelative("Steps").arraySize = 0;
                added.isExpanded = true;
            }
        }

        /// <summary>Draws independent prefab templates and exact completion component selectors.</summary>
        /// <param name="steps">Spawn templates for the current day.</param>
        /// <param name="weighted">Whether weights affect this day's selection.</param>
        private static void Steps(SerializedProperty steps, bool weighted)
        {
            // Each row retains its own prefab and paths when another step is selected or reordered.
            for (int index = 0; index < steps.arraySize; index++)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    SerializedProperty step = steps.GetArrayElementAtIndex(index);
                    if (Header(steps, index, "Spawn " + (index + 1)))
                        break;
                    if (!step.isExpanded)
                        continue;
                    using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                    HoverControls.Field(step, "Name");
                    TransferInteractionControls.Prefab(step.FindPropertyRelative("Prefab"));
                    Completion(step);
                    HoverControls.Field(step, "ArrivalDialogueEnabled", "Walk-in Dialogue");
                    if (step.FindPropertyRelative("ArrivalDialogueEnabled").boolValue)
                    {
                        Completion(step, "ArrivalDialogue", "Dialogue", true);
                        HoverControls.Field(step, "NewCustomerSound", "Arrival Sound");
                    }
                    if (weighted)
                        HoverControls.Field(step, "Weight");
                    HoverControls.Field(step, "Delay");
                    HoverControls.Field(step, "Position");
                    HoverControls.Field(step, "Rotation");
                    Animation(step.FindPropertyRelative("WalkIn"), "Walk In");
                    Animation(step.FindPropertyRelative("WalkOut"), "Walk Out");
                }
            if (StudioButton.Draw(new GUIContent("+ Add Spawn", "Append a prefab template with its own completion interaction.")))
            {
                steps.arraySize++;
                SerializedProperty added = steps.GetArrayElementAtIndex(steps.arraySize - 1);
                added.FindPropertyRelative("Name").stringValue = "Spawn " + steps.arraySize;
                added.FindPropertyRelative("Prefab").objectReferenceValue = null;
                added.FindPropertyRelative("Completion").objectReferenceValue = null;
                added.FindPropertyRelative("ArrivalDialogueEnabled").boolValue = false;
                added.FindPropertyRelative("ArrivalDialogue").objectReferenceValue = null;
                added.FindPropertyRelative("NewCustomerSound").boolValue = false;
                added.FindPropertyRelative("Weight").floatValue = 1f;
                added.FindPropertyRelative("Delay").floatValue = 0f;
                added.FindPropertyRelative("Position").vector3Value = Vector3.zero;
                added.FindPropertyRelative("Rotation").vector3Value = Vector3.zero;
                foreach (string name in new[] { "WalkIn", "WalkOut" })
                {
                    added.FindPropertyRelative(name + ".Enabled").boolValue = false;
                    added.FindPropertyRelative(name + ".Space").enumValueIndex = (int)SpawnFlowSpace.World;
                    added.FindPropertyRelative(name + ".Keyframes").arraySize = 0;
                }
                added.isExpanded = true;
            }
        }

        /// <summary>Selects an existing interaction by component identity within the chosen prefab.</summary>
        /// <param name="step">Spawn row receiving the exact completion source.</param>
        /// <param name="field">Serialized component link.</param>
        /// <param name="title">Selector caption.</param>
        /// <param name="dialogueOnly">Restrict the menu to dialogue components.</param>
        private static void Completion(SerializedProperty step, string field = "Completion", string title = "Complete On", bool dialogueOnly = false)
        {
            // Build the menu only on demand; normal repaints allocate no hierarchy catalog.
            GameObject prefab = step.FindPropertyRelative("Prefab").objectReferenceValue as GameObject;
            SerializedProperty property = step.FindPropertyRelative(field);
            ObjectInteraction current = property.objectReferenceValue as ObjectInteraction;
            Rect rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, new GUIContent(title, property.tooltip));
            if (!GUI.Button(rect, current != null ? current.InteractionName + " (" + current.GetType().Name + ")" : "Select Interaction", EditorStyles.popup))
                return;
            GenericMenu menu = new GenericMenu();
            if (prefab != null)
                foreach (ObjectInteraction source in prefab.GetComponentsInChildren<ObjectInteraction>(true))
                    if (source is not ObjectSpawnManager && (!dialogueOnly || source is ObjectDialogue))
                    {
                        ObjectInteraction selected = source;
                        string path = property.propertyPath;
                        SerializedObject data = property.serializedObject;
                        string label = AnimationUtility.CalculateTransformPath(source.transform, prefab.transform);
                        menu.AddItem(new GUIContent((label.Length > 0 ? label + "/" : string.Empty) + source.InteractionName
                            + " (" + source.GetType().Name + ", " + ObjectWorkspaceTarget.FileId(source) + ")"), current == source, () =>
                        {
                            data.Update();
                            data.FindProperty(path).objectReferenceValue = selected;
                            data.ApplyModifiedProperties();
                        });
                    }
            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("No interactions on this prefab"));
            menu.DropDown(rect);
        }

        /// <summary>Edits explicit transform keyframes with independent delays and rotation directions.</summary>
        /// <param name="animation">Walk-in or walk-out path.</param>
        /// <param name="label">Section label.</param>
        private static void Animation(SerializedProperty animation, string label)
        {
            // Disabled paths keep their saved keys without showing irrelevant pose controls.
            SerializedProperty enabled = animation.FindPropertyRelative("Enabled");
            enabled.boolValue = EditorGUILayout.Toggle(new GUIContent(label, enabled.tooltip), enabled.boolValue);
            if (!enabled.boolValue)
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            HoverControls.Field(animation, "Space", "Key Space");
            SerializedProperty frames = animation.FindPropertyRelative("Keyframes");
            for (int index = 0; index < frames.arraySize; index++)
            {
                SerializedProperty frame = frames.GetArrayElementAtIndex(index);
                if (Header(frames, index, "Keyframe " + (index + 1)))
                    break;
                if (!frame.isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope entry = new EditorGUI.IndentLevelScope();
                SerializedProperty pose = frame.FindPropertyRelative("Pose");
                HoverControls.Field(pose, "Position");
                HoverControls.Field(pose, "Rotation");
                HoverControls.Field(pose, "Scale");
                HoverControls.Field(frame, "Delay");
                HoverControls.Field(frame, "Duration");
                HoverControls.Field(frame, "Direction");
            }
            if (StudioButton.Draw(new GUIContent("+ Add Keyframe", "Append a destination reached after its delay and travel duration.")))
            {
                frames.arraySize++;
                SerializedProperty added = frames.GetArrayElementAtIndex(frames.arraySize - 1);
                added.FindPropertyRelative("Pose.Position").vector3Value = Vector3.zero;
                added.FindPropertyRelative("Pose.Rotation").vector3Value = Vector3.zero;
                added.FindPropertyRelative("Pose.Scale").vector3Value = Vector3.one;
                added.FindPropertyRelative("Delay").floatValue = 0f;
                added.FindPropertyRelative("Duration").floatValue = 1f;
                added.FindPropertyRelative("Direction").enumValueIndex = 1;
                added.isExpanded = true;
            }
        }

        #endregion
        #region Shared Controls

        /// <summary>Provides compact ordering and removal commands for an array entry.</summary>
        /// <param name="array">Ordered entries.</param>
        /// <param name="index">Current entry.</param>
        /// <param name="label">Foldout label.</param>
        /// <returns>True when structural changes require drawing to stop.</returns>
        private static bool Header(SerializedProperty array, int index, string label)
        {
            // Array edits stop the current loop before any stale property handles are reused.
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
            SerializedProperty entry = array.GetArrayElementAtIndex(index);
            entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, label, true);
            using (new EditorGUI.DisabledScope(index == 0))
                if (GUILayout.Button(new GUIContent("↑", "Move earlier."), GUILayout.Width(24f)))
                    return array.MoveArrayElement(index, index - 1);
            using (new EditorGUI.DisabledScope(index == array.arraySize - 1))
                if (GUILayout.Button(new GUIContent("↓", "Move later."), GUILayout.Width(24f)))
                    return array.MoveArrayElement(index, index + 1);
            if (!GUILayout.Button(new GUIContent("−", "Remove this entry."), GUILayout.Width(24f)))
                return false;
            array.DeleteArrayElementAtIndex(index);
            return true;
        }

        /// <summary>Draws a scene asset selector while retaining its runtime-safe path.</summary>
        /// <param name="property">Serialized scene path.</param>
        /// <param name="label">Visible field label.</param>
        private static void Scene(SerializedProperty property, string label)
        {
            // Runtime assemblies never need SceneAsset or editor reflection.
            SceneAsset current = AssetDatabase.LoadAssetAtPath<SceneAsset>(property.stringValue);
            EditorGUI.BeginChangeCheck();
            SceneAsset selected = (SceneAsset)EditorGUILayout.ObjectField(new GUIContent(label, property.tooltip), current, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
                property.stringValue = selected != null ? AssetDatabase.GetAssetPath(selected) : string.Empty;
        }

        /// <summary>Draws a top-level plan field with its authored tooltip.</summary>
        /// <param name="data">Plan serializer.</param>
        /// <param name="name">Serialized field name.</param>
        private static void Field(SerializedObject data, string name)
        {
            // Unity handles native Undo and scalar field types on the shared asset.
            EditorGUILayout.PropertyField(data.FindProperty(name));
        }

        /// <summary>Enables referenced scenes without replacing the project's existing build order.</summary>
        /// <param name="plan">Plan whose scene links should be registered.</param>
        private static void RegisterScenes(SpawnFlowPlan plan)
        {
            // Explicit registration keeps invalid or unassigned paths visible for correction.
            System.Collections.Generic.List<EditorBuildSettingsScene> scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (string path in Paths(plan))
            {
                if (string.IsNullOrEmpty(path))
                    continue;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    Debug.LogWarning("Assign an existing scene: " + path, plan);
                    continue;
                }
                int index = scenes.FindIndex(scene => scene.path == path);
                if (index >= 0)
                    scenes[index].enabled = true;
                else
                    scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Enumerates only scene links used by the current plan options.</summary>
        /// <param name="plan">Authored day plan.</param>
        /// <returns>Day scenes, optional pause overlay and the non-looping destination.</returns>
        private static System.Collections.Generic.IEnumerable<string> Paths(SpawnFlowPlan plan)
        {
            // Optional hidden destinations need no build entry until they participate in the flow.
            foreach (SpawnFlowDay day in plan.Days)
                if (day != null)
                    yield return day.Scene;
            yield return plan.PauseScene;
            if (!plan.Loop)
                yield return plan.MainMenu;
        }

        #endregion
        #endregion
    }
}
