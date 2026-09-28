using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Shares searchable single-value and combined-flag selectors across Studio tools.</summary>
    public static class ObjectFlagSelector
    {
        #region Events

        /// <summary>Notifies editor workspaces after a delayed popup choice has been applied.</summary>
        public static event Action<UnityEngine.Object> SelectionChanged;
        /// <summary>Lets draft-based tools protect a delayed choice against navigation to another object.</summary>
        public static event Action<UnityEngine.Object, string, List<Func<bool>>> CollectSelectionGuards;

        #endregion

        #region Methods

        #region Drawing

        /// <summary>Draws one assigned flag or an array of combinable flags using the same popup.</summary>
        /// <param name="property">Serialized flag reference or flag-reference array.</param>
        /// <param name="label">Optional presentation label; defaults to the field's own label.</param>
        public static void Draw(SerializedProperty property, GUIContent label = null)
        {
            // Explicit rectangles respect the caller's category indentation without duplicate headers.
            Draw(EditorGUILayout.GetControlRect(), property, label ?? new GUIContent(property.displayName, property.tooltip));
        }

        /// <summary>Draws a selector inside a native Inspector property rectangle.</summary>
        /// <param name="rect">Available field rectangle.</param>
        /// <param name="property">Serialized flag or combination.</param>
        /// <param name="label">Field label and tooltip.</param>
        public static void Draw(Rect rect, SerializedProperty property, GUIContent label)
        {
            // The delayed popup retains only the owner and property path, never a stale serialized handle.
            EditorGUI.BeginProperty(rect, label, property);
            Rect field = EditorGUI.PrefixLabel(rect, label);
            Rect create = new Rect(field.xMax - 26f, field.y, 26f, field.height);
            field.width -= 30f;
            UnityEngine.Object owner = property.serializedObject.targetObject;
            string path = property.propertyPath;
            if (EditorGUI.DropdownButton(field, new GUIContent(Label(property), property.tooltip), FocusType.Keyboard))
                PopupWindow.Show(field, new ObjectFlagPopup(owner, path, CaptureGuard(owner, path)));
            if (GUI.Button(create, new GUIContent("+", "Create a new object flag and select it here."), EditorStyles.miniButton))
                Create(owner, path, CaptureGuard(owner, path));
            EditorGUI.EndProperty();
        }

        /// <summary>Builds a compact selection label without exposing serialized array layout.</summary>
        /// <param name="property">Flag reference or array.</param>
        /// <returns>A readable selection summary.</returns>
        private static string Label(SerializedProperty property)
        {
            // Combined flags remain a single consistent row beneath their category.
            if (!property.isArray)
                return property.objectReferenceValue is ObjectFlag flag ? flag.DisplayName : "None";
            if (property.arraySize == 0)
                return "None";
            ObjectFlag first = property.GetArrayElementAtIndex(0).objectReferenceValue as ObjectFlag;
            return (first != null ? first.DisplayName : "Missing Flag")
                + (property.arraySize > 1 ? " + " + (property.arraySize - 1) : string.Empty);
        }

        #endregion

        #region Selection

        /// <summary>Captures optional navigation guards only when a selection menu opens.</summary>
        /// <param name="owner">Object or recoverable workspace containing the field.</param>
        /// <param name="path">Serialized field requested by the control.</param>
        /// <returns>A check that rejects destroyed owners and replaced workspace selections.</returns>
        private static Func<bool> CaptureGuard(UnityEngine.Object owner, string path)
        {
            // A creation form can remain open while another prefab or interaction is selected.
            List<Func<bool>> guards = new List<Func<bool>>();
            CollectSelectionGuards?.Invoke(owner, path, guards);
            return () =>
            {
                if (owner == null)
                    return false;
                foreach (Func<bool> guard in guards)
                    if (!guard())
                        return false;
                return true;
            };
        }

        /// <summary>Creates a flag and assigns it only while the original field still owns the choice.</summary>
        /// <param name="owner">Object containing the requested field.</param>
        /// <param name="path">Original serialized field.</param>
        /// <param name="current">Checks that the original editor context is still selected.</param>
        /// <param name="name">Optional name suggested by the search filter.</param>
        internal static void Create(UnityEngine.Object owner, string path, Func<bool> current, string name = "")
        {
            // The definition remains available even when navigation makes its original assignment obsolete.
            ObjectFlagCreateWindow.Open(flag =>
            {
                if (current())
                    Assign(owner, path, flag, true);
            }, name);
        }

        /// <summary>Reads selected flags for a popup without changing the underlying configuration.</summary>
        /// <param name="owner">Object containing the field.</param>
        /// <param name="path">Serialized flag or array path.</param>
        /// <param name="multiple">Receives whether the field permits a combination.</param>
        /// <returns>The current selection, or an empty list for a removed field.</returns>
        internal static HashSet<ObjectFlag> Read(UnityEngine.Object owner, string path, out bool multiple)
        {
            // Fresh wrappers permit menus to outlive inspector layout passes safely.
            HashSet<ObjectFlag> selected = new HashSet<ObjectFlag>();
            multiple = false;
            if (owner == null)
                return selected;
            using SerializedObject data = new SerializedObject(owner);
            SerializedProperty property = data.FindProperty(path);
            if (property == null)
                return selected;
            multiple = property.isArray;
            if (multiple)
                for (int index = 0; index < property.arraySize; index++)
                {
                    if (property.GetArrayElementAtIndex(index).objectReferenceValue is ObjectFlag flag)
                        selected.Add(flag);
                }
            else if (property.objectReferenceValue is ObjectFlag flag)
                selected.Add(flag);
            return selected;
        }

        /// <summary>Applies a popup choice with native Undo to an existing serialized field.</summary>
        /// <param name="owner">Object containing the field.</param>
        /// <param name="path">Serialized flag or array path.</param>
        /// <param name="flag">Chosen flag, or null to clear the selection.</param>
        /// <param name="selected">Whether this flag should be included.</param>
        internal static void Assign(UnityEngine.Object owner, string path, ObjectFlag flag, bool selected)
        {
            // Scene, prefab and draft fields use Unity's normal serialization and Undo handling.
            if (owner == null)
                return;
            using SerializedObject data = new SerializedObject(owner);
            SerializedProperty property = data.FindProperty(path);
            if (property == null)
                return;
            if (!property.isArray)
                property.objectReferenceValue = flag;
            else if (flag == null)
                property.arraySize = 0;
            else
            {
                for (int index = 0; index < property.arraySize; index++)
                    if (property.GetArrayElementAtIndex(index).objectReferenceValue == flag)
                    {
                        if (selected)
                            return;
                        property.GetArrayElementAtIndex(index).objectReferenceValue = null;
                        property.DeleteArrayElementAtIndex(index);
                        if (data.ApplyModifiedProperties())
                            SelectionChanged?.Invoke(owner);
                        return;
                    }
                if (selected)
                {
                    property.arraySize++;
                    property.GetArrayElementAtIndex(property.arraySize - 1).objectReferenceValue = flag;
                }
            }
            if (data.ApplyModifiedProperties())
                SelectionChanged?.Invoke(owner);
        }

        #endregion

        #endregion
    }

    /// <summary>Provides the same flag picker for nested reference fields drawn by native Inspectors.</summary>
    [CustomPropertyDrawer(typeof(ObjectFlag))]
    internal sealed class ObjectFlagDrawer : PropertyDrawer
    {
        #region Methods

        #region Drawing

        /// <summary>Draws a stable asset reference with search and direct flag creation.</summary>
        /// <param name="position">Unity's property rectangle.</param>
        /// <param name="property">Serialized flag reference.</param>
        /// <param name="label">Inspector label.</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Reuse the same control in dedicated tools and ordinary component inspectors.
            ObjectFlagSelector.Draw(position, property, label);
        }

        #endregion

        #endregion
    }
}
