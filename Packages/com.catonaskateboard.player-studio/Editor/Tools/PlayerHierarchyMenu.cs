using CatOnASkateboard.StudioColors.Editor;
using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Selects existing hierarchy targets while retaining reusable relative paths in presets.</summary>
    internal static class PlayerHierarchyMenu
    {
        #region Methods

        #region Controls

        /// <summary>Draws a path selector whose choices come from the current hierarchy.</summary>
        /// <param name="property">Stored path in a preset or detached draft.</param>
        /// <param name="root">Hierarchy used to resolve the path.</param>
        /// <param name="label">Compact label and explanatory tooltip.</param>
        /// <param name="allowRoot">Whether the hierarchy root is a valid moving target.</param>
        /// <param name="owner">Window owning draft Undo, or null for a saved asset.</param>
        /// <param name="capture">Captures a detached module after a menu selection.</param>
        internal static void DrawPath(SerializedProperty property, Transform root, GUIContent label, bool allowRoot,
            UnityEngine.Object owner = null, Action capture = null)
        {
            // Keep missing selections visible; repaint never rewrites a stored path.
            SerializedObject data = property.serializedObject;
            string key = property.propertyPath;
            string current = property.stringValue;
            Rect rect = EditorGUILayout.GetControlRect();
            StudioFieldMenu.Context(rect, property);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(property));
            using EditorGUI.PropertyScope scope = new EditorGUI.PropertyScope(rect, label, property);
            rect = EditorGUI.PrefixLabel(rect, label);
            using EditorGUI.DisabledScope disabled = new EditorGUI.DisabledScope(root == null);
            if (!EditorGUI.DropdownButton(rect, new GUIContent(current.Length == 0 ? allowRoot ? "Root" : "Select Child" : current,
                root == null ? "Choose a hierarchy source to browse its children." : current), FocusType.Keyboard))
                return;
            Show(rect, root, PlayerHierarchy.Resolve(root, current), allowRoot, false, selected =>
            {
                if (root == null || selected == null || data.targetObject == null)
                    return;
                if (owner != null)
                    Undo.RecordObject(owner, "Select Tool Hierarchy Target");
                data.Update();
                data.FindProperty(key).stringValue = AnimationUtility.CalculateTransformPath(selected, root);
                if (capture != null)
                    capture();
                else
                    data.ApplyModifiedProperties();
                Notify(owner);
            });
        }

        /// <summary>Draws a scene reference using the same hierarchy navigation as tool targets.</summary>
        /// <param name="label">Reference label and tooltip.</param>
        /// <param name="root">Player hierarchy supplying candidates.</param>
        /// <param name="current">Current optional transform.</param>
        /// <param name="allowRoot">Whether the player root may be selected.</param>
        /// <param name="owner">Window owning the serialized proposal.</param>
        /// <param name="assign">Stores the selected reference without editing the scene.</param>
        internal static void DrawTransform(GUIContent label, Transform root, Transform current, bool allowRoot,
            UnityEngine.Object owner, Action<Transform> assign)
        {
            // Null is explicit for optional camera roles.
            Rect rect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), label);
            using EditorGUI.DisabledScope disabled = new EditorGUI.DisabledScope(root == null);
            if (EditorGUI.DropdownButton(rect, new GUIContent(current != null ? current.name : "None"), FocusType.Keyboard))
                Show(rect, root, current, allowRoot, true, selected =>
                {
                    Undo.RecordObject(owner, "Select Player Hierarchy Target");
                    assign(selected);
                    Notify(owner);
                });
        }

        #endregion

        #region Menu

        /// <summary>Builds hierarchy choices only after the selector is opened.</summary>
        /// <param name="rect">Dropdown anchor.</param>
        /// <param name="root">Hierarchy origin.</param>
        /// <param name="current">Current selection.</param>
        /// <param name="allowRoot">Whether the root itself is offered.</param>
        /// <param name="optional">Whether a None command is offered.</param>
        /// <param name="assign">Receives the chosen transform.</param>
        private static void Show(Rect rect, Transform root, Transform current, bool allowRoot, bool optional, Action<Transform> assign)
        {
            GenericMenu menu = new GenericMenu();
            if (optional)
                menu.AddItem(new GUIContent("None"), current == null, () => assign(null));
            if (allowRoot)
                menu.AddItem(new GUIContent("Root (" + root.name + ")"), current == root, () => assign(root));
            // Ambiguous paths remain unavailable until the hierarchy is named uniquely.
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root)
                    continue;
                string path = AnimationUtility.CalculateTransformPath(child, root);
                GUIContent label = new GUIContent("Hierarchy/" + path + "/Select");
                if (PlayerHierarchy.Resolve(root, path) != child)
                    menu.AddDisabledItem(new GUIContent(label.text + " (ambiguous name)"));
                else
                    menu.AddItem(label, current == child, () => assign(child));
            }
            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("No children"));
            menu.DropDown(rect);
        }

        /// <summary>Refreshes the draft footer after a delayed menu callback.</summary>
        /// <param name="owner">Window retaining the proposal.</param>
        private static void Notify(UnityEngine.Object owner)
        {
            if (owner is PlayerStudioWindow window)
                window.HandleDraftChanged();
            else if (owner is EditorWindow editor)
                editor.Repaint();
        }

        #endregion

        #endregion
    }
}
