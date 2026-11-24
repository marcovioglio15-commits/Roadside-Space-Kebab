using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
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
            PlanSettings();
            Days(serializedObject.FindProperty("Days"));
            serializedObject.ApplyModifiedProperties();
            if (!((SpawnFlowPlan)target).TryValidate(out string warning))
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
            if (StudioButton.Draw(new GUIContent("Register Scenes", "Enable the plan's days, pause scene and final menu in Build Settings without removing existing entries.")))
                RegisterScenes((SpawnFlowPlan)target);
        }

        /// <summary>Groups plan-wide scene, transition and random settings into themed dropdowns.</summary>
        private void PlanSettings()
        {
            // Independent expansion states keep long plans compact while editing a single visitor.
            if (SpawnFlowKeyframeControls.Section(serializedObject.FindProperty("Loop"), "Flow and Scenes"))
                using (new StudioIndentScope())
                {
                    Field(serializedObject, "Loop");
                    Field(serializedObject, "PreserveScene");
                    if (!serializedObject.FindProperty("Loop").boolValue)
                        Scene(serializedObject.FindProperty("MainMenu"), "Main Menu");
                    Scene(serializedObject.FindProperty("PauseScene"), "Pause Scene");
                }
            if (SpawnFlowKeyframeControls.Section(serializedObject.FindProperty("FadeOut"), "Scene Transitions"))
                using (new StudioIndentScope())
                {
                    Field(serializedObject, "FadeOut");
                    Field(serializedObject, "BlackDelay");
                    Field(serializedObject, "FadeIn");
                }
            if (SpawnFlowKeyframeControls.Section(serializedObject.FindProperty("FixedSeed"), "Randomness"))
                using (new StudioIndentScope())
                {
                    Field(serializedObject, "FixedSeed");
                    if (serializedObject.FindProperty("FixedSeed").boolValue)
                        Integer(serializedObject.FindProperty("Seed"), -10000, 10000);
                }
        }

        /// <summary>Links a plan from an interaction card with direct creation and editing commands.</summary>
        /// <param name="property">Shared plan reference.</param>
        internal static void Link(SerializedProperty property)
        {
            // The edit window works independently of Inspector locks or selection state.
            StudioGUI.PropertyField(property, new GUIContent("Day Plan", property.tooltip));
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
                    if (StudioArrayGUI.Header(days, index, new GUIContent("Day " + (index + 1) + " · " + day.FindPropertyRelative("Name").stringValue, days.tooltip)))
                        break;
                    if (!day.isExpanded)
                        continue;
                    using StudioIndentScope indent = new StudioIndentScope();
                    HoverControls.Field(day, "Name");
                    if (SpawnFlowKeyframeControls.Section(day.FindPropertyRelative("Scene"), "Scene and Events"))
                        using (new StudioIndentScope())
                        {
                            Scene(day.FindPropertyRelative("Scene"), "Scene");
                            SpawnFlowSceneControls.Draw(day);
                        }
                    DayLength(day);
                    Steps(day.FindPropertyRelative("Steps"), day.FindPropertyRelative("Selection").enumValueIndex == (int)SpawnSelection.WeightedRandom);
                }
            if (StudioButton.Draw(new GUIContent("+ Add Day", "Append a new day to the scene sequence.")))
            {
                days.arraySize++;
                SerializedProperty added = days.GetArrayElementAtIndex(days.arraySize - 1);
                added.boxedValue = new SpawnFlowDay { Name = "Day " + days.arraySize };
                added.isExpanded = true;
            }
        }

        /// <summary>Shows only the budget controls used by this day's admission policy.</summary>
        /// <param name="day">Day whose length and selection are being edited.</param>
        private static void DayLength(SerializedProperty day)
        {
            // Timer and count policies expose only their active budget settings.
            if (!SpawnFlowKeyframeControls.Section(day.FindPropertyRelative("LengthMode"), "Duration and Selection"))
                return;
            using StudioIndentScope indent = new StudioIndentScope();
            HoverControls.Field(day, "LengthMode");
            switch ((DayLengthMode)day.FindPropertyRelative("LengthMode").enumValueIndex)
            {
                case DayLengthMode.Timer:
                    HoverControls.Field(day, "DayDuration");
                    break;
                case DayLengthMode.SpawnCount:
                    HoverControls.Field(day, "RandomCount");
                    if (day.FindPropertyRelative("RandomCount").boolValue)
                    {
                        Integer(day.FindPropertyRelative("Minimum"), 1, 100);
                        Integer(day.FindPropertyRelative("Maximum"), 1, 100);
                    }
                    else
                        Integer(day.FindPropertyRelative("Count"), 1, 100);
                    break;
            }
            HoverControls.Field(day, "Selection");
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
                    if (StudioArrayGUI.Header(steps, index, new GUIContent("Spawn " + (index + 1) + " · " + step.FindPropertyRelative("Name").stringValue, steps.tooltip)))
                        break;
                    if (!step.isExpanded)
                        continue;
                    using StudioIndentScope indent = new StudioIndentScope();
                    HoverControls.Field(step, "Name");
                    TransferInteractionControls.Prefab(step.FindPropertyRelative("Prefab"));
                    if (SpawnFlowKeyframeControls.Section(step.FindPropertyRelative("Completions"), "Completion Rules"))
                        using (new StudioIndentScope())
                            Completions(step);
                    if (SpawnFlowKeyframeControls.Section(step.FindPropertyRelative("ArrivalDialogueEnabled"), "Arrival Events"))
                        using (new StudioIndentScope())
                        {
                            HoverControls.Field(step, "ArrivalDialogueEnabled", "Walk-in Dialogue");
                            if (step.FindPropertyRelative("ArrivalDialogueEnabled").boolValue)
                                Completion(step, step.FindPropertyRelative("ArrivalDialogue"), "Dialogue", true);
                            HoverControls.Field(step, "NewCustomerSound", "Arrival Sound");
                        }
                    if (weighted)
                        HoverControls.Field(step, "Weight");
                    HoverControls.Field(step, "Delay");
                    if (SpawnFlowKeyframeControls.Section(step.FindPropertyRelative("Position"), "Spawn Placement"))
                        using (new StudioIndentScope())
                        {
                            StudioVectorControls.Draw(step.FindPropertyRelative("Position"), -20f, 20f);
                            StudioVectorControls.Draw(step.FindPropertyRelative("Rotation"), -360f, 360f);
                        }
                    SpawnFlowKeyframeControls.Path(step.FindPropertyRelative("WalkIn"), "Walk In · Travel Path");
                    SpawnFlowKeyframeControls.Path(step.FindPropertyRelative("WalkOut"), "Walk Out · Travel Path");
                    SpawnFlowAnimatorControls.Draw(step);
                }
            if (StudioButton.Draw(new GUIContent("+ Add Spawn", "Append a prefab template with its own completion interaction.")))
            {
                steps.arraySize++;
                SerializedProperty added = steps.GetArrayElementAtIndex(steps.arraySize - 1);
                added.boxedValue = new SpawnFlowStep
                {
                    Name = "Spawn " + steps.arraySize,
                    WalkIn = new SpawnFlowAnimation { Space = SpawnFlowSpace.World },
                    WalkOut = new SpawnFlowAnimation { Space = SpawnFlowSpace.World }
                };
                added.isExpanded = true;
            }
        }

        /// <summary>Edits the distinct interactions that finish this visitor's step.</summary>
        /// <param name="step">Spawn template containing completion sources.</param>
        private static void Completions(SerializedProperty step)
        {
            // A single selection retains the same departure behavior as an ordinary completion event.
            SerializedProperty sources = step.FindPropertyRelative("Completions");
            SerializedProperty checkpoints = step.FindPropertyRelative("CompletionSteps");
            if (sources.arraySize > 1)
                UnlockInteractionControls.ConditionLogic(step.FindPropertyRelative("RequireAll"));
            for (int index = 0; index < sources.arraySize; index++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    Completion(step, sources.GetArrayElementAtIndex(index), "Complete On " + (index + 1));
                    if (GUILayout.Button(new GUIContent("−", "Remove this completion requirement."), GUILayout.Width(24f)))
                    {
                        sources.GetArrayElementAtIndex(index).objectReferenceValue = null;
                        sources.DeleteArrayElementAtIndex(index);
                        if (index < checkpoints.arraySize)
                            checkpoints.DeleteArrayElementAtIndex(index);
                        break;
                    }
                }
                DialogueStepControls.DrawArray(checkpoints, index, sources.GetArrayElementAtIndex(index).objectReferenceValue as ObjectInteraction);
            }
            if (StudioButton.Draw(new GUIContent("+ Completion", "Select another interaction whose final completion can finish this visit.")))
            {
                sources.arraySize++;
                sources.GetArrayElementAtIndex(sources.arraySize - 1).objectReferenceValue = null;
            }
        }

        /// <summary>Selects an existing interaction by component identity within the chosen prefab.</summary>
        /// <param name="step">Spawn row receiving the exact completion source.</param>
        /// <param name="property">Serialized component link.</param>
        /// <param name="title">Selector caption.</param>
        /// <param name="dialogueOnly">Restrict the menu to dialogue components.</param>
        private static void Completion(SerializedProperty step, SerializedProperty property, string title, bool dialogueOnly = false)
        {
            // Build the menu only on demand; normal repaints allocate no hierarchy catalog.
            GameObject prefab = step.FindPropertyRelative("Prefab").objectReferenceValue as GameObject;
            ObjectInteraction current = property.objectReferenceValue as ObjectInteraction;
            using StudioFieldLayout field = new StudioFieldLayout(new GUIContent(title, property.tooltip));
            Rect rect = field.GetRect(EditorGUIUtility.singleLineHeight);
            rect = field.ValueRect(rect);
            if (!GUI.Button(rect, current != null ? current.InteractionName : "Select Interaction", EditorStyles.popup))
                return;
            GenericMenu menu = new GenericMenu { allowDuplicateNames = true };
            if (prefab != null)
                foreach (ObjectInteraction source in prefab.GetComponentsInChildren<ObjectInteraction>(true))
                    if (source is not ObjectSpawnManager && (!dialogueOnly || source is ObjectDialogue))
                    {
                        ObjectInteraction selected = source;
                        string path = property.propertyPath;
                        SerializedObject data = property.serializedObject;
                        menu.AddItem(new GUIContent(source.InteractionName), current == source, () =>
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

        #endregion
        #region Shared Controls

        /// <summary>Draws a scene asset selector while retaining its runtime-safe path.</summary>
        /// <param name="property">Serialized scene path.</param>
        /// <param name="label">Visible field label.</param>
        private static void Scene(SerializedProperty property, string label)
        {
            // Runtime assemblies never need SceneAsset or editor reflection.
            SceneAsset current = AssetDatabase.LoadAssetAtPath<SceneAsset>(property.stringValue);
            EditorGUI.BeginChangeCheck();
            SceneAsset selected = (SceneAsset)StudioGUI.ObjectField(StudioFieldMenu.Value(property, new GUIContent(label, property.tooltip)), current, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
                property.stringValue = selected != null ? AssetDatabase.GetAssetPath(selected) : string.Empty;
        }

        /// <summary>Draws a top-level plan field with its declared tooltip.</summary>
        /// <param name="data">Plan serializer.</param>
        /// <param name="name">Serialized field name.</param>
        private static void Field(SerializedObject data, string name)
        {
            // Unity handles native Undo and scalar field types on the shared asset.
            StudioGUI.PropertyField(data.FindProperty(name));
        }

        /// <summary>Draws an integer with a slider while preserving saved values on repaint.</summary>
        /// <param name="property">Serialized integer receiving explicit edits.</param>
        /// <param name="minimum">Slider's lower guide.</param>
        /// <param name="maximum">Slider's upper guide.</param>
        private static void Integer(SerializedProperty property, int minimum, int maximum)
        {
            // Numeric entry remains unrestricted; the slider supplies a suggested interval.
            using StudioFieldLayout layout = new StudioFieldLayout(new GUIContent(property.displayName, property.tooltip));
            Rect row = layout.GetRect(EditorGUIUtility.singleLineHeight);
            StudioFieldMenu.Context(row, property);
            using StudioFieldColors colors = new StudioFieldColors(row, StudioFieldColors.Key(property));
            Rect value = layout.ValueRect(row);
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
            Rect number = new Rect(value.x, value.y, Mathf.Min(72f, value.width * 0.45f), value.height);
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUI.IntField(number, property.intValue);
            if (EditorGUI.EndChangeCheck())
                property.intValue = selected;
            if (value.xMax - number.xMax < 34f)
                return;
            EditorGUI.BeginChangeCheck();
            float guided = GUI.HorizontalSlider(new Rect(number.xMax + 6f, value.y, value.xMax - number.xMax - 6f, value.height),
                Mathf.Clamp(property.intValue, minimum, maximum), minimum, maximum);
            if (EditorGUI.EndChangeCheck())
                property.intValue = Mathf.RoundToInt(guided);
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
