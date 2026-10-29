using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Connects prefab Eject settings to exact gameplay-scene animations through persistent identities.</summary>
    internal static class EjectAnimationControls
    {
        #region Methods
        #region Drawing

        /// <summary>Shows connection and completion choices only when Eject requests an animation.</summary>
        /// <param name="settings">Eject proposal or reusable preset settings.</param>
        /// <param name="sections">Retained section expansion state.</param>
        internal static void Draw(SerializedProperty settings, ObjectStudioSections sections)
        {
            if (!sections.Draw("Before Ejection", "Request an exact animation before applying the physical effect.", settings,
                "AnimateBeforeEject", "ExternalAnimation", "AnimationIdentity", "WaitForAnimationReturn", "ForceAutoReturn", "AnimationOverlap", "ReturnOverlap"))
                return;
            using StudioIndentScope indent = new StudioIndentScope();
            HoverControls.Field(settings, "AnimateBeforeEject", "Animate Before Trigger");
            if (!settings.FindPropertyRelative("AnimateBeforeEject").boolValue)
                return;
            HoverControls.Field(settings, "ExternalAnimation");
            if (settings.FindPropertyRelative("ExternalAnimation").boolValue)
                Connection(settings.FindPropertyRelative("AnimationIdentity"));
            HoverControls.Field(settings, "WaitForAnimationReturn");
            bool forceReturnAvailable = !settings.FindPropertyRelative("WaitForAnimationReturn").boolValue;
            if (forceReturnAvailable)
            {
                HoverControls.Field(settings, "ForceAutoReturn");
                forceReturnAvailable = settings.FindPropertyRelative("ForceAutoReturn").boolValue;
            }
            if ((EjectBehavior)settings.FindPropertyRelative("Behavior").enumValueIndex != EjectBehavior.BlackHole)
                return;
            HoverControls.Field(settings, "AnimationOverlap", "Start Overlap (%)");
            if (forceReturnAvailable)
                HoverControls.Field(settings, "ReturnOverlap", "Return Overlap (%)");
        }

        /// <summary>Selects a scene component even while the source prefab is isolated in Prefab Mode.</summary>
        /// <param name="property">Identity stored in detached or reusable Eject settings.</param>
        private static void Connection(SerializedProperty property)
        {
            ObjectTriggerAnimation current = Resolve(property.stringValue);
            Rect rect = EditorGUILayout.GetControlRect();
            StudioFieldMenu.Context(rect, property);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(property));
            rect = EditorGUI.PrefixLabel(rect, new GUIContent("Scene Animation", "Select the exact animation from a loaded gameplay scene; its identity is safe to store in a prefab."));
            string label = current != null ? current.gameObject.scene.name + " / " + current.name + " / " + current.InteractionName
                : string.IsNullOrEmpty(property.stringValue) ? "Select Animation" : "Selected animation is not loaded";
            if (EditorGUI.DropdownButton(rect, new GUIContent(label), FocusType.Keyboard))
                Menu(property, current).DropDown(rect);
            if (current != null && current.gameObject.scene.isDirty)
                EditorGUILayout.LabelField(new GUIContent("Save the scene to retain its animation connection.",
                    "The prefab stores the selected identity; the scene stores its matching animation component."), EditorStyles.miniLabel);
        }

        /// <summary>Builds the scene inventory only when its selection menu is opened.</summary>
        /// <param name="property">Destination identity field.</param>
        /// <param name="current">Currently resolved selection.</param>
        /// <returns>A menu of exact animation components with guarded assignment callbacks.</returns>
        private static GenericMenu Menu(SerializedProperty property, ObjectTriggerAnimation current)
        {
            UnityEngine.Object owner = property.serializedObject.targetObject;
            string path = property.propertyPath;
            Func<bool> guard = StudioFieldMenu.Guard(owner, path);
            GenericMenu menu = new GenericMenu { allowDuplicateNames = true };
            foreach (ObjectTriggerAnimation candidate in Resources.FindObjectsOfTypeAll<ObjectTriggerAnimation>())
            {
                if (!IsSceneAnimation(candidate))
                    continue;
                string hierarchy = AnimationUtility.CalculateTransformPath(candidate.transform, null);
                menu.AddItem(new GUIContent(candidate.gameObject.scene.name + "/" + hierarchy + "/" + candidate.InteractionName), candidate == current, () =>
                {
                    if (!guard() || candidate == null)
                        return;
                    using SerializedObject data = new SerializedObject(owner);
                    data.FindProperty(path).stringValue = Connect(candidate);
                    data.ApplyModifiedProperties();
                    StudioFieldMenu.Notify(owner);
                });
            }
            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("No Trigger Animations in loaded gameplay scenes"));
            return menu;
        }

        #endregion
        #region Identity

        /// <summary>Resolves a saved scene identity in the editor without accepting prefab preview copies.</summary>
        /// <param name="identity">Previously selected animation identity.</param>
        /// <returns>The exact loaded scene component, or null when missing or duplicated.</returns>
        internal static ObjectTriggerAnimation Resolve(string identity)
        {
            if (string.IsNullOrEmpty(identity))
                return null;
            if (GlobalObjectId.TryParse(identity, out GlobalObjectId id)
                && GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) is ObjectTriggerAnimation selected
                && IsSceneAnimation(selected) && selected.SceneIdentity == identity)
                return selected;
            // Unsaved scenes receive opaque identities until their first save; never select an arbitrary duplicate.
            ObjectTriggerAnimation result = null;
            foreach (ObjectTriggerAnimation animation in Resources.FindObjectsOfTypeAll<ObjectTriggerAnimation>())
                if (IsSceneAnimation(animation) && animation.SceneIdentity == identity)
                {
                    if (result != null)
                        return null;
                    result = animation;
                }
            return result;
        }

        /// <summary>Assigns a persistent scene-side endpoint with Undo while leaving the prefab proposal unapplied.</summary>
        /// <param name="animation">Exact gameplay animation selected from the menu.</param>
        /// <returns>Identity to retain in the Eject prefab or preset.</returns>
        internal static string Connect(ObjectTriggerAnimation animation)
        {
            string identity = animation.SceneIdentity;
            if (string.IsNullOrEmpty(identity) || GlobalObjectId.TryParse(identity, out GlobalObjectId prior)
                && GlobalObjectId.GlobalObjectIdentifierToObjectSlow(prior) != animation)
                identity = NewIdentity(animation);
            // Explicit selection repairs copied component identities without changing unrelated connections.
            foreach (ObjectTriggerAnimation other in Resources.FindObjectsOfTypeAll<ObjectTriggerAnimation>())
                if (other != animation && IsSceneAnimation(other) && other.SceneIdentity == identity)
                    Assign(other, NewIdentity(other));
            Assign(animation, identity);
            return identity;
        }

        /// <summary>Creates a distinct endpoint for a new or duplicated scene animation.</summary>
        /// <param name="animation">Scene component receiving its own identity.</param>
        /// <returns>A saved component identity or a persistent opaque key for an unsaved scene.</returns>
        private static string NewIdentity(ObjectTriggerAnimation animation)
        {
            return string.IsNullOrEmpty(animation.gameObject.scene.path) ? Guid.NewGuid().ToString("N")
                : GlobalObjectId.GetGlobalObjectIdSlow(animation).ToString();
        }

        /// <summary>Records an endpoint change as a scene edit that participates in native Undo.</summary>
        /// <param name="animation">Component receiving the exact connection.</param>
        /// <param name="identity">Unique identity assigned by the selector.</param>
        private static void Assign(ObjectTriggerAnimation animation, string identity)
        {
            if (animation.SceneIdentity == identity)
                return;
            Undo.RecordObject(animation, "Connect Scene Trigger Animation");
            using SerializedObject data = new SerializedObject(animation);
            data.FindProperty("sceneIdentity").stringValue = identity;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(animation);
            EditorSceneManager.MarkSceneDirty(animation.gameObject.scene);
        }

        /// <summary>Excludes assets and temporary prefab stages from gameplay-scene selection.</summary>
        /// <param name="animation">Discovered editor component.</param>
        /// <returns>True for an animation in a loaded gameplay scene.</returns>
        private static bool IsSceneAnimation(ObjectTriggerAnimation animation)
        {
            return animation != null && !EditorUtility.IsPersistent(animation) && animation.gameObject.scene.IsValid()
                && animation.gameObject.scene.isLoaded && !EditorSceneManager.IsPreviewScene(animation.gameObject.scene);
        }

        #endregion
        #endregion
    }
}
