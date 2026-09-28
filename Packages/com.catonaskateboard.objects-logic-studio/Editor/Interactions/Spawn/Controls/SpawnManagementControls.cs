using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Draws prefab-linked completion requirements and conditional random output controls.</summary>
    internal static class SpawnManagementControls
    {
        #region State

        private static GameObject cachedPrefab;
        private static ObjectInteraction[] sources = Array.Empty<ObjectInteraction>();
        private static GUIContent[] names = Array.Empty<GUIContent>();
        private static bool dirty = true;

        #endregion

        #region Methods

        #region Catalog

        /// <summary>Invalidates the source-name cache after asset changes.</summary>
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // Repaint reuses existing labels; project changes rebuild them on the next relevant draw.
            EditorApplication.projectChanged += Invalidate;
        }

        /// <summary>Requests a source catalog refresh without searching during unrelated editor events.</summary>
        private static void Invalidate()
        {
            // Deleted or renamed source interactions appear correctly on the next draw.
            dirty = true;
        }

        /// <summary>Captures existing source names while retaining real component references as identity.</summary>
        /// <param name="prefab">Linked persistent source root.</param>
        private static void Refresh(GameObject prefab)
        {
            // A source prefab change invalidates cached names even when no project event was emitted yet.
            if (!dirty && cachedPrefab == prefab)
                return;
            dirty = false;
            cachedPrefab = prefab;
            List<ObjectInteraction> found = new List<ObjectInteraction>();
            if (prefab != null)
                foreach (ObjectInteraction source in prefab.GetComponentsInChildren<ObjectInteraction>(true))
                    if (source is not ObjectSpawnManager)
                        found.Add(source);
            sources = found.ToArray();
            names = new GUIContent[sources.Length + 1];
            names[0] = new GUIContent("Select an existing interaction", "Choose a configured interaction on the linked prefab.");
            for (int index = 0; index < sources.Length; index++)
                names[index + 1] = new GUIContent(sources[index].InteractionName + " (" + sources[index].GetType().Name.Replace("Object", string.Empty)
                    + ", " + AnimationUtility.CalculateTransformPath(sources[index].transform, prefab.transform) + ", " + (index + 1) + ")",
                    "Successful completions of this exact component; starts and interruptions do not count.");
        }

        #endregion

        #region Drawing

        /// <summary>Edits completion conditions and only the settings used by the chosen output mode.</summary>
        /// <param name="settings">Detached card or reusable preset settings.</param>
        /// <param name="sections">Retained section visibility.</param>
        internal static void Draw(SerializedProperty settings, ObjectStudioSections sections)
        {
            // Each rule observes one source prefab; multiple rules can observe different prefabs independently.
            if (sections.Draw("Spawn Conditions", "Each instance of the linked prefab must meet its own completion quantities."))
                using (new EditorGUI.IndentLevelScope())
                {
                    TransferInteractionControls.Prefab(settings.FindPropertyRelative("SourcePrefab"));
                    Refresh((GameObject)settings.FindPropertyRelative("SourcePrefab").objectReferenceValue);
                    Conditions(settings.FindPropertyRelative("Conditions"));
                    if (settings.FindPropertyRelative("Conditions").arraySize > 1)
                        HoverControls.Field(settings, "RequireAll");
                    HoverControls.Field(settings, "Repeat");
                    if (settings.FindPropertyRelative("Repeat").boolValue)
                        HoverControls.Field(settings, "CyclesPerInstance");
                }
            if (sections.Draw("Spawn Draw", "Choose weighted probability, equal probability or a sequence of prefab outputs."))
                using (new EditorGUI.IndentLevelScope())
                {
                    HoverControls.Field(settings, "Selection");
                    Choices(settings.FindPropertyRelative("Choices"), settings.FindPropertyRelative("Selection").enumValueIndex == (int)SpawnSelection.WeightedRandom);
                    HoverControls.Field(settings, "Draws");
                    if (settings.FindPropertyRelative("Draws").intValue > 1)
                        HoverControls.Field(settings, "WithoutReplacement");
                    HoverControls.Field(settings, "Chance");
                    HoverControls.Field(settings, "FixedSeed");
                    if (settings.FindPropertyRelative("FixedSeed").boolValue)
                        HoverControls.Field(settings, "Seed");
                }
            Animation(settings.FindPropertyRelative("Animation"), sections);
            if (sections.Draw("Spawn Placement", "Configure local output placement, delayed execution and surviving object capacity."))
                using (new EditorGUI.IndentLevelScope())
                {
                    HoverControls.Field(settings, "MinimumDelay");
                    HoverControls.Field(settings, "MaximumDelay");
                    HoverControls.Field(settings, "Position");
                    HoverControls.Field(settings, "Rotation");
                    HoverControls.Field(settings, "Radius");
                    HoverControls.Field(settings, "RandomYaw");
                    HoverControls.Field(settings, "LimitAlive");
                    if (settings.FindPropertyRelative("LimitAlive").boolValue)
                        HoverControls.Field(settings, "MaximumAlive");
                }
        }

        /// <summary>Shows transform arrival controls only when the object should animate before interaction.</summary>
        /// <param name="animation">Detached arrival settings.</param>
        /// <param name="sections">Retained foldout state.</param>
        private static void Animation(SerializedProperty animation, ObjectStudioSections sections)
        {
            // Finishing the animation restores original enabled states and leaves all independent locks in place.
            if (!sections.Draw("Spawn Animation", "Animate the generated object before any of its interactions or collisions become available."))
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            HoverControls.Field(animation, "Enabled");
            if (!animation.FindPropertyRelative("Enabled").boolValue)
                return;
            HoverControls.Field(animation, "Duration");
            HoverControls.Field(animation, "Offset");
            HoverControls.Field(animation, "Rotation");
            HoverControls.Field(animation, "Scale");
            HoverControls.Field(animation, "Progress");
        }

        /// <summary>Draws explicit completion rows with named source selectors and integer quantities.</summary>
        /// <param name="conditions">Serialized condition array.</param>
        private static void Conditions(SerializedProperty conditions)
        {
            // Missing references stay visible until explicitly replaced instead of selecting a different component.
            for (int index = 0; index < conditions.arraySize; index++)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    SerializedProperty row = conditions.GetArrayElementAtIndex(index);
                    SerializedProperty source = row.FindPropertyRelative("Source");
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        int current = Array.IndexOf(sources, source.objectReferenceValue as ObjectInteraction) + 1;
                        EditorGUI.BeginChangeCheck();
                        int selected = EditorGUILayout.Popup(new GUIContent("Interaction", source.tooltip), current, names);
                        if (EditorGUI.EndChangeCheck())
                        {
                            source.objectReferenceValue = selected > 0 ? sources[selected - 1] : null;
                            row.FindPropertyRelative("SourceId").stringValue = string.Empty;
                        }
                        if (GUILayout.Button(new GUIContent("−", "Remove this completion condition."), GUILayout.Width(28f)))
                        {
                            conditions.DeleteArrayElementAtIndex(index);
                            break;
                        }
                    }
                    HoverControls.Field(row, "Count");
                    if (source.objectReferenceValue != null && Array.IndexOf(sources, source.objectReferenceValue as ObjectInteraction) < 0)
                        EditorGUILayout.LabelField("Choose an interaction from the current source prefab.", EditorStyles.miniLabel);
                }
            if (GUILayout.Button(new GUIContent("+ Add Completion Condition", "Require an existing interaction to complete on the same source instance.")))
            {
                conditions.arraySize++;
                SerializedProperty added = conditions.GetArrayElementAtIndex(conditions.arraySize - 1);
                added.FindPropertyRelative("Source").objectReferenceValue = null;
                added.FindPropertyRelative("SourceId").stringValue = string.Empty;
                added.FindPropertyRelative("Count").intValue = 1;
            }
        }

        /// <summary>Draws prefab-only output rows with weights limited to the weighted random mode.</summary>
        /// <param name="choices">Serialized output choice array.</param>
        /// <param name="weighted">Whether relative weights affect the selected mode.</param>
        private static void Choices(SerializedProperty choices, bool weighted)
        {
            // Each output is a prefab reference; scene instances never enter the saved recipe.
            for (int index = 0; index < choices.arraySize; index++)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    SerializedProperty row = choices.GetArrayElementAtIndex(index);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        TransferInteractionControls.Prefab(row.FindPropertyRelative("Prefab"));
                        if (GUILayout.Button(new GUIContent("−", "Remove this output choice."), GUILayout.Width(28f)))
                        {
                            choices.DeleteArrayElementAtIndex(index);
                            break;
                        }
                    }
                    if (weighted)
                        HoverControls.Field(row, "Weight");
                }
            if (GUILayout.Button(new GUIContent("+ Add Output Prefab", "Add another eligible prefab to this rule's output choices.")))
            {
                choices.arraySize++;
                SerializedProperty added = choices.GetArrayElementAtIndex(choices.arraySize - 1);
                added.FindPropertyRelative("Prefab").objectReferenceValue = null;
                added.FindPropertyRelative("Weight").floatValue = 1f;
            }
        }

        #endregion

        #endregion
    }
}
