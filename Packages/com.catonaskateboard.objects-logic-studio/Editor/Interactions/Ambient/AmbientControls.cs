using System.Collections.Generic;
using CatOnASkateboard.AudioStudio;
using CatOnASkateboard.StudioColors.Editor;
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
        /// <param name="sections">Retained targeting foldouts shared by presets and the workspace.</param>
        internal static void Draw(SerializedProperty settings, ObjectStudioSections sections)
        {
            // Catalog scanning happens only when the event menu is opened.
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            HoverControls.Field(settings, "Mode");
            HoverControls.Field(settings, "Radius");
            if (settings.FindPropertyRelative("Mode").enumValueIndex == (int)AmbientMode.Radio)
            {
                Radio(settings.FindPropertyRelative("Radio"), sections);
                return;
            }
            SerializedProperty selected = settings.FindPropertyRelative("Event");
            Rect rect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), new GUIContent("Event", selected.tooltip));
            if (GUI.Button(rect, selected.stringValue, EditorStyles.popup))
                Menu(selected, rect);
        }

        /// <summary>Edits the local parameter cycle only while Radio mode is selected.</summary>
        /// <param name="settings">Radio settings within the current emitter proposal.</param>
        /// <param name="sections">Persistent targeting foldout state.</param>
        private static void Radio(SerializedProperty settings, ObjectStudioSections sections)
        {
            HoverControls.Field(settings, "SeparateBackgroundMusic");
            HoverControls.Field(settings, "Parameter");
            SerializedProperty channels = settings.FindPropertyRelative("Channels");
            SerializedProperty initial = settings.FindPropertyRelative("InitialChannel");
            initial.intValue = StudioFieldGUI.IntField(StudioFieldMenu.Value(initial, new GUIContent("Initial Channel", initial.tooltip)),
                initial.intValue, 0, Mathf.Max(0, channels.arraySize - 1));
            for (int index = 0; index < channels.arraySize; index++)
            {
                SerializedProperty channel = channels.GetArrayElementAtIndex(index);
                if (StudioArrayGUI.Header(channels, index, new GUIContent(channel.FindPropertyRelative("Name").stringValue, channels.tooltip)))
                    break;
                if (!channel.isExpanded)
                    continue;
                using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
                HoverControls.Field(channel, "Name");
                HoverControls.Field(channel, "Value");
            }
            StudioArrayGUI.Add(channels, "Add Radio Channel", () => new RadioChannel());
            TransferInteractionControls.Target(settings.FindPropertyRelative("Target"), sections);
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
            Handles.Label(ambient.transform.position, (ambient.Settings.Mode == AmbientMode.Radio ? "Radio" : ambient.Settings.Event)
                + " · " + ambient.Settings.Radius.ToString("0.#") + " m");
        }

        #endregion
        #endregion
    }
}
