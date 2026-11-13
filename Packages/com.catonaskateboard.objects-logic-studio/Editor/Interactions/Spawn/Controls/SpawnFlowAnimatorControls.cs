using System.Collections.Generic;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Offers prefab Animators and exact controller states with their effective clips.</summary>
    internal static class SpawnFlowAnimatorControls
    {
        #region Methods
        #region Controls

        /// <summary>Shows only animation roles supported by this visitor's interactions and paths.</summary>
        /// <param name="step">Serialized visitor template in the shared day plan.</param>
        internal static void Draw(SerializedProperty step)
        {
            // The target selector never accepts an Animator from another prefab or a scene instance.
            GameObject prefab = step.FindPropertyRelative("Prefab").objectReferenceValue as GameObject;
            SerializedProperty settings = step.FindPropertyRelative("CharacterAnimation");
            Target(settings.FindPropertyRelative("Target"), prefab);
            Animator animator = settings.FindPropertyRelative("Target").objectReferenceValue as Animator;
            if (animator == null)
                return;
            using StudioIndentScope indent = new StudioIndentScope();
            using (new EditorGUI.DisabledScope(true))
                StudioGUI.ObjectField(new GUIContent("Controller", "Controller assigned to the selected prefab Animator."),
                    animator.runtimeAnimatorController, typeof(RuntimeAnimatorController), false);
            if (animator.runtimeAnimatorController == null)
                return;
            if (step.FindPropertyRelative("WalkIn.Enabled").boolValue || step.FindPropertyRelative("WalkOut.Enabled").boolValue)
                State(settings.FindPropertyRelative("Walk"), animator, "Walk");
            State(settings.FindPropertyRelative("Idle"), animator, "Idle");
            bool pay = SpawnFlowAnimatorSettings.SupportsPay(prefab);
            bool death = SpawnFlowAnimatorSettings.SupportsDeath(prefab);
            if (pay)
                State(settings.FindPropertyRelative("Pay"), animator, "Pay");
            if (death)
                State(settings.FindPropertyRelative("Death"), animator, "Death");
            HoverControls.Field(settings, "BlendDuration");
            if (settings.FindPropertyRelative("Walk.Clip").objectReferenceValue != null
                && (step.FindPropertyRelative("WalkIn.Enabled").boolValue || step.FindPropertyRelative("WalkOut.Enabled").boolValue))
            {
                HoverControls.Field(settings, "SynchronizeWalk");
                if (settings.FindPropertyRelative("SynchronizeWalk").boolValue)
                    HoverControls.Field(settings, "WalkReferenceSpeed");
            }
            ClearUnavailable(settings, pay, death);
        }

        /// <summary>Clears duplicated animation references only when a new spawn row is explicitly added.</summary>
        /// <param name="step">New row copied by Unity's serialized array expansion.</param>
        internal static void Clear(SerializedProperty step)
        {
            // Existing rows and validation never rewrite saved selections.
            ClearSettings(step.FindPropertyRelative("CharacterAnimation"));
        }

        /// <summary>Clears roles after an explicit None choice or a newly added row.</summary>
        /// <param name="settings">Character animation settings being disabled.</param>
        private static void ClearSettings(SerializedProperty settings)
        {
            // Clearing the Animator also clears dependent roles instead of leaving an invalid row.
            settings.FindPropertyRelative("Target").objectReferenceValue = null;
            settings.FindPropertyRelative("BlendDuration").floatValue = 0f;
            settings.FindPropertyRelative("SynchronizeWalk").boolValue = false;
            settings.FindPropertyRelative("WalkReferenceSpeed").floatValue = 1.3f;
            foreach (string role in new[] { "Walk", "Idle", "Pay", "Death" })
                Assign(settings.FindPropertyRelative(role), string.Empty, null, 0);
        }

        /// <summary>Offers explicit cleanup when interaction changes make a saved role unavailable.</summary>
        /// <param name="settings">Animation roles retained by the spawn row.</param>
        /// <param name="pay">Whether this prefab can collect prices.</param>
        /// <param name="death">Whether this prefab has Self Eject.</param>
        private static void ClearUnavailable(SerializedProperty settings, bool pay, bool death)
        {
            // Hidden invalid choices remain correctable without silently rewriting validation data.
            SerializedProperty payment = settings.FindPropertyRelative("Pay");
            SerializedProperty ejection = settings.FindPropertyRelative("Death");
            bool clearPay = !pay && Selected(payment);
            bool clearDeath = !death && Selected(ejection);
            if ((!clearPay && !clearDeath) || !StudioButton.Draw(new GUIContent("Clear Unavailable Roles",
                "Remove saved Pay or Death selections whose required interactions are no longer configured.")))
                return;
            if (clearPay)
                Assign(payment, string.Empty, null, 0);
            if (clearDeath)
                Assign(ejection, string.Empty, null, 0);
        }

        /// <summary>Detects saved role data even when its clip reference has been removed.</summary>
        /// <param name="property">Role binding inspected by cleanup controls.</param>
        /// <returns>True when an explicit selection remains.</returns>
        private static bool Selected(SerializedProperty property)
        {
            // Missing assets retain their state paths so the tool can report and clear them.
            return property.FindPropertyRelative("Clip").objectReferenceValue != null
                || !string.IsNullOrWhiteSpace(property.FindPropertyRelative("Path").stringValue);
        }

        /// <summary>Selects an existing Animator while keeping invalid previous links visible for correction.</summary>
        /// <param name="property">Prefab Animator reference.</param>
        /// <param name="prefab">Visitor hierarchy supplying candidates.</param>
        private static void Target(SerializedProperty property, GameObject prefab)
        {
            // Build hierarchy menus only when opened, avoiding allocations during tool repaints.
            Animator current = property.objectReferenceValue as Animator;
            using StudioFieldLayout field = new StudioFieldLayout(new GUIContent("Character Animator", property.tooltip));
            Rect rect = field.ValueRect(field.GetRect(EditorGUIUtility.singleLineHeight));
            if (!GUI.Button(rect, current != null ? current.name : "None", EditorStyles.popup))
                return;
            GenericMenu menu = new GenericMenu();
            SerializedObject data = property.serializedObject;
            string path = property.propertyPath;
            menu.AddItem(new GUIContent("None"), current == null, () => SetTarget(data, path, null));
            if (prefab != null)
                foreach (Animator candidate in prefab.GetComponentsInChildren<Animator>(true))
                {
                    Animator selected = candidate;
                    string label = AnimationUtility.CalculateTransformPath(candidate.transform, prefab.transform);
                    menu.AddItem(new GUIContent(string.IsNullOrEmpty(label) ? prefab.name : label), current == selected,
                        () => SetTarget(data, path, selected));
                }
            menu.DropDown(rect);
        }

        /// <summary>Applies one explicit Animator selection with normal Undo support.</summary>
        /// <param name="data">Shared plan serializer.</param>
        /// <param name="path">Exact Animator property path.</param>
        /// <param name="target">Selected prefab Animator, or null.</param>
        private static void SetTarget(SerializedObject data, string path, Animator target)
        {
            // Changing the Animator preserves roles; None explicitly clears their dependent bindings.
            data.Update();
            data.FindProperty(path).objectReferenceValue = target;
            if (target == null)
                ClearSettings(data.FindProperty(path.Substring(0, path.LastIndexOf('.'))));
            data.ApplyModifiedProperties();
        }

        /// <summary>Offers single-clip controller states, including nested machines and clip overrides.</summary>
        /// <param name="property">Role binding receiving state, clip and layer together.</param>
        /// <param name="animator">Prefab Animator providing the controller.</param>
        /// <param name="label">Animation role displayed beside the selector.</param>
        private static void State(SerializedProperty property, Animator animator, string label)
        {
            // Store the exact path so runtime playback needs no controller inspection or reflection.
            AnimationClip current = property.FindPropertyRelative("Clip").objectReferenceValue as AnimationClip;
            using StudioFieldLayout field = new StudioFieldLayout(new GUIContent(label, property.tooltip));
            Rect rect = field.ValueRect(field.GetRect(EditorGUIUtility.singleLineHeight));
            if (!GUI.Button(rect, current != null ? current.name : "None", EditorStyles.popup))
                return;
            GenericMenu menu = new GenericMenu();
            SerializedObject data = property.serializedObject;
            string path = property.propertyPath;
            menu.AddItem(new GUIContent("None"), current == null, () => Select(data, path, string.Empty, null, 0));
            RuntimeAnimatorController runtime = animator.runtimeAnimatorController;
            List<AnimatorOverrideController> overrides = new List<AnimatorOverrideController>();
            while (runtime is AnimatorOverrideController replacement)
            {
                overrides.Add(replacement);
                runtime = replacement.runtimeAnimatorController;
            }
            if (runtime is AnimatorController controller)
                for (int layer = 0; layer < controller.layers.Length; layer++)
                    States(menu, data, path, property, controller.layers[layer].stateMachine,
                        controller.layers[layer].name, layer, overrides, label is "Walk" or "Idle");
            menu.DropDown(rect);
        }

        #endregion
        #region Controller Catalog

        /// <summary>Traverses one controller layer only when its state menu is requested.</summary>
        /// <param name="menu">Menu receiving selectable clips.</param>
        /// <param name="data">Shared plan serializer.</param>
        /// <param name="propertyPath">Role binding path retained by callbacks.</param>
        /// <param name="property">Current binding used for the selected marker.</param>
        /// <param name="machine">Current state machine.</param>
        /// <param name="prefix">Full controller path preceding its states.</param>
        /// <param name="layer">Owning controller layer.</param>
        /// <param name="overrides">Outer-to-inner override controllers.</param>
        /// <param name="looping">Whether this role needs a looping clip.</param>
        private static void States(GenericMenu menu, SerializedObject data, string propertyPath, SerializedProperty property,
            AnimatorStateMachine machine, string prefix, int layer, List<AnimatorOverrideController> overrides, bool looping)
        {
            // Blend trees do not identify one clip and are omitted from these role selectors.
            foreach (ChildAnimatorState child in machine.states)
                if (child.state.motion is AnimationClip clip)
                {
                    for (int index = overrides.Count - 1; index >= 0; index--)
                        clip = overrides[index][clip] ?? clip;
                    if (clip.isLooping != looping)
                        continue;
                    AnimationClip selected = clip;
                    string statePath = prefix + "." + child.state.name;
                    string speedParameter = child.state.speedParameterActive ? child.state.speedParameter : string.Empty;
                    menu.AddItem(new GUIContent(statePath.Replace('.', '/') + " (" + clip.name + ")"),
                        property.FindPropertyRelative("Path").stringValue == statePath,
                        () => Select(data, propertyPath, statePath, selected, layer, speedParameter));
                }
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
                States(menu, data, propertyPath, property, child.stateMachine, prefix + "." + child.stateMachine.name, layer, overrides, looping);
        }

        /// <summary>Commits an explicit state menu choice through Unity serialization.</summary>
        /// <param name="data">Shared plan serializer.</param>
        /// <param name="propertyPath">Exact role binding path.</param>
        /// <param name="statePath">Full controller state path.</param>
        /// <param name="clip">Effective clip, or null to disable the role.</param>
        /// <param name="layer">Controller layer containing the state.</param>
        /// <param name="speedParameter">Optional float multiplier used by this state.</param>
        private static void Select(SerializedObject data, string propertyPath, string statePath, AnimationClip clip, int layer, string speedParameter = "")
        {
            // Menu callbacks resolve fresh property handles after array edits or selection changes.
            data.Update();
            Assign(data.FindProperty(propertyPath), statePath, clip, layer, speedParameter);
            if (clip == null && propertyPath.EndsWith(".Walk", System.StringComparison.Ordinal))
                data.FindProperty(propertyPath.Substring(0, propertyPath.LastIndexOf('.')) + ".SynchronizeWalk").boolValue = false;
            data.ApplyModifiedProperties();
        }

        /// <summary>Writes the three values that identify one exact state selection.</summary>
        /// <param name="property">Role binding being changed.</param>
        /// <param name="statePath">Full controller path, or empty.</param>
        /// <param name="clip">Effective clip, or null.</param>
        /// <param name="layer">Owning controller layer.</param>
        /// <param name="speedParameter">Optional float multiplier captured from the state.</param>
        private static void Assign(SerializedProperty property, string statePath, AnimationClip clip, int layer, string speedParameter = "")
        {
            // A None choice clears stale clip and path data together.
            property.FindPropertyRelative("Path").stringValue = statePath;
            property.FindPropertyRelative("Clip").objectReferenceValue = clip;
            property.FindPropertyRelative("Layer").intValue = layer;
            property.FindPropertyRelative("SpeedParameter").stringValue = speedParameter;
        }

        #endregion
        #endregion
    }
}
