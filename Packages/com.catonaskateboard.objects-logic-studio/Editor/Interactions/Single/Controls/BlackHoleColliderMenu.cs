using System;
using CatOnASkateboard.PlayerStudio;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Lists every destination component, including trigger and solid colliders sharing one object.</summary>
    internal static class BlackHoleColliderMenu
    {
        #region Methods
        #region Selection

        /// <summary>Shows the selected component explicitly instead of relying on Unity's first-component object picker.</summary>
        /// <param name="settings">Black Hole settings in the current detached draft.</param>
        /// <param name="source">Ejector hierarchy supplying the selectable colliders.</param>
        internal static void Draw(SerializedProperty settings, GameObject source)
        {
            // Enumerate only when the menu opens; ordinary repaints resolve one saved branch.
            Collider current = source != null ? ((BlackHoleSettings)settings.boxedValue).Resolve(source.transform) : null;
            Rect rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, new GUIContent("Destination Collider", "Select the exact solid or trigger collider on this ejector or any child. Component numbers follow Inspector order."));
            using EditorGUI.DisabledScope disabled = new EditorGUI.DisabledScope(source == null);
            if (GUI.Button(rect, new GUIContent(current != null ? Label(source.transform, current) : "Select Collider"), EditorStyles.popup))
                Show(settings, source, current);
        }

        /// <summary>Builds distinct entries for all components without excluding trigger geometry.</summary>
        /// <param name="settings">Settings retaining the collider route and component number.</param>
        /// <param name="source">Root defining valid relative paths.</param>
        /// <param name="current">Previously selected collider, if still present.</param>
        private static void Show(SerializedProperty settings, GameObject source, Collider current)
        {
            GenericMenu menu = new GenericMenu();
            UnityEngine.Object owner = settings.serializedObject.targetObject;
            string property = settings.propertyPath;
            // Ambiguous hierarchy names cannot be saved as portable prefab routes.
            foreach (Collider collider in source.GetComponentsInChildren<Collider>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(collider.transform, source.transform);
                GUIContent label = new GUIContent(Label(source.transform, collider));
                if (PlayerHierarchy.Resolve(source.transform, path) != collider.transform)
                    menu.AddDisabledItem(new GUIContent(label.text + " (ambiguous path)"));
                else
                    menu.AddItem(label, collider == current, () => Assign(owner, property, source, collider));
            }
            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("No colliders in this hierarchy"));
            menu.ShowAsContext();
        }

        /// <summary>Saves an exact component using a fresh serialized wrapper so menu callbacks remain Undo-safe.</summary>
        /// <param name="owner">Object retaining the draft.</param>
        /// <param name="property">Path to its Black Hole settings.</param>
        /// <param name="source">Ejector root used for the relative route.</param>
        /// <param name="selected">Exact component chosen from the menu.</param>
        internal static void Assign(UnityEngine.Object owner, string property, GameObject source, Collider selected)
        {
            // Closing a window or deleting its source while the menu is open invalidates the selection.
            if (owner == null || source == null || selected == null || !selected.transform.IsChildOf(source.transform))
                return;
            using SerializedObject data = new SerializedObject(owner);
            SerializedProperty settings = data.FindProperty(property);
            if (settings == null)
                return;
            settings.FindPropertyRelative("ColliderPath").stringValue = AnimationUtility.CalculateTransformPath(selected.transform, source.transform);
            settings.FindPropertyRelative("ColliderIndex").intValue = Array.IndexOf(selected.GetComponents<Collider>(), selected);
            data.ApplyModifiedProperties();
            if (owner is ObjectWorkspace workspace)
                workspace.Persist();
        }

        /// <summary>Names the branch, component number, collider shape and trigger state.</summary>
        /// <param name="root">Ejector root.</param>
        /// <param name="collider">Component represented by this menu entry.</param>
        /// <returns>A distinct label even when one object owns several colliders of the same type.</returns>
        private static string Label(Transform root, Collider collider)
        {
            string path = AnimationUtility.CalculateTransformPath(collider.transform, root);
            string shape = collider switch
            {
                BoxCollider => "Box", SphereCollider => "Sphere", CapsuleCollider => "Capsule",
                MeshCollider => "Mesh", TerrainCollider => "Terrain", _ => "Collider"
            };
            return (path.Length == 0 ? "Root" : path) + " / #" + (Array.IndexOf(collider.GetComponents<Collider>(), collider) + 1)
                + " " + shape + (collider.isTrigger ? " (Trigger)" : " (Solid)") + (!collider.enabled ? " [Disabled]" : string.Empty);
        }

        #endregion
        #endregion
    }
}
