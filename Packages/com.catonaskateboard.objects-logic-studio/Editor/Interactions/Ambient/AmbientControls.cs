using System.Collections.Generic;
using CatOnASkateboard.AudioStudio;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Lists authored ambient catalog keys and visualizes each emitter's listening radius.</summary>
    internal static class AmbientControls
    {
        #region Methods
        #region Drawing

        /// <summary>Edits an ambient card without requiring manual FMOD event paths.</summary>
        /// <param name="settings">Detached emitter settings.</param>
        internal static void Draw(SerializedProperty settings)
        {
            // Catalog scanning happens only when the event menu is opened.
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            SerializedProperty selected = settings.FindPropertyRelative("Event");
            Rect rect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), new GUIContent("Event", selected.tooltip));
            if (GUI.Button(rect, selected.stringValue, EditorStyles.popup))
                Menu(selected, rect);
            HoverControls.Field(settings, "Radius");
        }

        /// <summary>Builds a catalog of ambient keys from saved Audio Studio presets.</summary>
        /// <param name="property">Event key receiving the selected entry.</param>
        /// <param name="rect">Popup anchor.</param>
        private static void Menu(SerializedProperty property, Rect rect)
        {
            // Duplicate keys across presets appear once; the runtime scene chooses its preset.
            SortedSet<string> keys = new SortedSet<string> { "amb_neonsign", "amb_traffic" };
            foreach (string guid in AssetDatabase.FindAssets("t:AudioPreset"))
            {
                AudioPreset preset = AssetDatabase.LoadAssetAtPath<AudioPreset>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (AudioBinding binding in preset.Events)
                    if (binding != null && binding.Key.StartsWith("amb_", System.StringComparison.OrdinalIgnoreCase))
                        keys.Add(binding.Key);
            }
            GenericMenu menu = new GenericMenu();
            foreach (string key in keys)
            {
                string path = property.propertyPath;
                SerializedObject data = property.serializedObject;
                menu.AddItem(new GUIContent(key), property.stringValue == key, () =>
                {
                    // Retain Undo through the owning serialized draft or preset.
                    data.Update();
                    data.FindProperty(path).stringValue = key;
                    data.ApplyModifiedProperties();
                });
            }
            menu.DropDown(rect);
        }

        /// <summary>Shows a clean selected-object listening boundary.</summary>
        /// <param name="ambient">Selected emitter.</param>
        /// <param name="type">Unity selection context.</param>
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void Gizmo(ObjectAmbient ambient, GizmoType type)
        {
            // Invalid values remain editable without reaching drawing APIs.
            if (!ambient.DrawGizmos || !ambient.TryValidate(out _))
                return;
            using Handles.DrawingScope scope = new Handles.DrawingScope(new Color(0.3f, 0.8f, 1f, 0.8f));
            Handles.DrawWireDisc(ambient.transform.position, Vector3.up, ambient.Settings.Radius);
            Handles.Label(ambient.transform.position, ambient.Settings.Event + " · " + ambient.Settings.Radius.ToString("0.#") + " m");
        }

        #endregion
        #endregion
    }
}
