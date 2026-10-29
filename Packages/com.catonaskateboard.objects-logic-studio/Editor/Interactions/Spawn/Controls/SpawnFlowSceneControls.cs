using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Authors scene links as portable hierarchy and card descriptors in shared day plans.</summary>
    internal static class SpawnFlowSceneControls
    {
        #region Methods
        #region Drawing

        /// <summary>Shows only the gate and reset selections used by this day.</summary>
        /// <param name="day">Day proposal being edited.</param>
        internal static void Draw(SerializedProperty day)
        {
            string scene = day.FindPropertyRelative("Scene").stringValue;
            HoverControls.Field(day, "WaitBeforeFirstSpawn");
            if (day.FindPropertyRelative("WaitBeforeFirstSpawn").boolValue)
                Selection(day.FindPropertyRelative("StartInteraction"), scene, false, "Start After");
            HoverControls.Field(day, "ResetAnimations");
            if ((DayAnimationReset)day.FindPropertyRelative("ResetAnimations").enumValueIndex != DayAnimationReset.Selected)
                return;
            SerializedProperty animations = day.FindPropertyRelative("AnimationsToReset");
            // Each independent foldout edits one exact scene animation.
            for (int index = 0; index < animations.arraySize; index++)
            {
                if (StudioArrayGUI.Header(animations, index, new GUIContent("Reset " + (index + 1), animations.tooltip)))
                    break;
                if (animations.GetArrayElementAtIndex(index).isExpanded)
                    using (new StudioIndentScope())
                        Selection(animations.GetArrayElementAtIndex(index), scene, true, "Scene Animation");
            }
            StudioArrayGUI.Add(animations, "Add Animation", () => new SceneInteractionReference());
        }

        /// <summary>Lists only saved gameplay objects in the day's loaded scene, excluding preview and prefab-stage copies.</summary>
        /// <param name="property">Portable selection stored in the plan.</param>
        /// <param name="scene">Day's authored scene path.</param>
        /// <param name="animationsOnly">Whether only Trigger Animations are offered.</param>
        /// <param name="label">Concise control label.</param>
        private static void Selection(SerializedProperty property, string scene, bool animationsOnly, string label)
        {
            SceneInteractionReference current = (SceneInteractionReference)property.boxedValue;
            Rect rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, new GUIContent(label, property.tooltip));
            string caption = current is { IsValid: true } ? current.Path + " / " + current.Interaction.Kind + " · " + current.Interaction.Name : "Select Interaction";
            if (!EditorGUI.DropdownButton(rect, new GUIContent(caption), FocusType.Keyboard))
                return;
            UnityEngine.Object owner = property.serializedObject.targetObject;
            string path = property.propertyPath;
            System.Func<bool> guard = StudioFieldMenu.Guard(owner, path);
            GenericMenu menu = new GenericMenu { allowDuplicateNames = true };
            // Scene discovery happens only when the selector opens.
            foreach (ObjectInteraction candidate in Resources.FindObjectsOfTypeAll<ObjectInteraction>())
            {
                if (EditorUtility.IsPersistent(candidate) || !candidate.gameObject.scene.IsValid() || candidate.gameObject.scene.path != scene
                    || EditorSceneManager.IsPreviewScene(candidate.gameObject.scene) || PrefabStageUtility.GetPrefabStage(candidate.gameObject) != null
                    || animationsOnly && candidate is not ObjectTriggerAnimation || candidate is ObjectSpawnManager)
                    continue;
                SceneInteractionReference selection = new SceneInteractionReference
                {
                    Path = AnimationUtility.CalculateTransformPath(candidate.transform, null),
                    Interaction = LocalInteractionReference.Capture(candidate)
                };
                if (!selection.IsValid || selection.Resolve(candidate.gameObject.scene) != candidate)
                    continue;
                menu.AddItem(new GUIContent(selection.Path + "/" + selection.Interaction.Kind + "/" + candidate.InteractionName + " [" + (selection.Interaction.Occurrence + 1) + "]"),
                    JsonUtility.ToJson(current) == JsonUtility.ToJson(selection), () =>
                    {
                        if (!guard())
                            return;
                        using SerializedObject data = new SerializedObject(owner);
                        data.FindProperty(path).boxedValue = selection;
                        data.ApplyModifiedProperties();
                        StudioFieldMenu.Notify(owner);
                    });
            }
            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("Open the day's scene to select an interaction with a unique hierarchy path"));
            menu.DropDown(rect);
        }

        #endregion
        #endregion
    }
}
