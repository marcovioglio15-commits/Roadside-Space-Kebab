using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows field differences between a local group proposal and its reusable preset.</summary>
    internal sealed class ObjectGroupCompareWindow : EditorWindow
    {
        #region Fields

        [Tooltip("Differences between the preset and the local group.")]
        [SerializeField]
        private string[] differences = Array.Empty<string>();
        [Tooltip("Scroll position in the comparison.")]
        [SerializeField]
        private Vector2 scroll;

        #endregion

        #region Methods

        #region Comparison

        /// <summary>Captures a comparison without importing values or changing either source.</summary>
        /// <param name="document">Local group proposal.</param>
        /// <param name="group">Settings group being compared.</param>
        /// <param name="preset">Saved group preset.</param>
        internal static void Open(ObjectWorkspace document, ObjectGroupInfo group, ObjectGroupPreset preset)
        {
            ObjectGroupCompareWindow window = CreateInstance<ObjectGroupCompareWindow>();
            window.titleContent = new GUIContent(group.Title + " · Compare");
            List<string> rows = new List<string>();
            ObjectPresetValue[] local = ObjectGroupPresetStore.Capture(document, group);
            if (preset.Group.Key != group.Key || preset.Values.Length != local.Length)
                rows.Add("This preset belongs to a different group.");
            else
                for (int index = 0; index < local.Length; index++)
                    Compare(ObjectWorkspace.Copy(preset.Values[index]), local[index], string.Empty, rows);
            window.differences = rows.Count == 0 ? new[] { "This group matches the preset." } : rows.ToArray();
            window.minSize = new Vector2(360f, 200f);
            window.Show();
        }

        /// <summary>Shows the applied component beside the isolated result of a master import.</summary>
        /// <param name="current">Existing component, or null for an addition.</param>
        /// <param name="proposed">Validated component from the import preview.</param>
        internal static void Open(Component current, Component proposed)
        {
            ObjectGroupCompareWindow window = CreateInstance<ObjectGroupCompareWindow>();
            window.titleContent = new GUIContent("Configuration changes");
            List<string> rows = new List<string>();
            if (current == null)
                rows.Add("Add " + ObjectInteractionCatalog.Name(proposed as ObjectInteraction));
            else
            {
                ObjectComponentPreset before = ObjectPresetCapture.Component(current, current.transform.root);
                ObjectComponentPreset after = ObjectPresetCapture.Component(proposed, proposed.transform.root);
                foreach (ObjectPresetValue field in after.Fields)
                {
                    ObjectPresetValue original = System.Array.Find(before.Fields, candidate => candidate.Name == field.Name);
                    if (original != null)
                        Compare(original, field, string.Empty, rows);
                }
            }
            window.differences = rows.Count == 0 ? new[] { "This interaction already matches." } : rows.ToArray();
            window.minSize = new Vector2(420f, 240f);
            window.Show();
        }

        /// <summary>Lists changed leaf fields instead of printing serialized implementation data.</summary>
        /// <param name="preset">Saved field value.</param>
        /// <param name="local">Pending local value.</param>
        /// <param name="parent">Readable parent label.</param>
        /// <param name="rows">List receiving differences.</param>
        private static void Compare(ObjectPresetValue preset, ObjectPresetValue local, string parent, List<string> rows)
        {
            // Local references compare by explicit hierarchy slot rather than temporary preview file IDs.
            if (preset.Link is { Local: true } && local.Link is { Local: true })
            {
                preset.Link.SourceGuid = local.Link.SourceGuid = string.Empty;
                preset.Link.FileId = local.Link.FileId = 0;
            }
            if (JsonUtility.ToJson(preset) == JsonUtility.ToJson(local))
                return;
            string label = parent + ObjectNames.NicifyVariableName(local.Name);
            if (preset.Children.Count == local.Children.Count && local.Children.Count > 0)
            {
                for (int index = 0; index < local.Children.Count; index++)
                    Compare(preset.Children[index], local.Children[index], label + " / ", rows);
                return;
            }
            rows.Add(label + "\nBefore: " + Value(preset) + "    After: " + Value(local));
        }

        /// <summary>Formats a short field value for a before-and-after row.</summary>
        /// <param name="value">Saved or local field.</param>
        /// <returns>A brief value label.</returns>
        private static string Value(ObjectPresetValue value)
        {
            if (value.Array)
                return value.Children.Count + " items";
            return value.Kind switch
            {
                SerializedPropertyType.Boolean => value.Integer != 0 ? "On" : "Off",
                SerializedPropertyType.String => value.Text,
                SerializedPropertyType.Float => value.Number.ToString("0.###"),
                SerializedPropertyType.Integer or SerializedPropertyType.Enum or SerializedPropertyType.LayerMask => value.Integer.ToString(),
                SerializedPropertyType.ObjectReference => value.Link?.Asset != null ? value.Link.Asset.name
                    : value.Link is { Local: true } ? value.Link.Path.Length > 0 ? value.Link.Path : "Object root" : "None",
                SerializedPropertyType.Color => value.Color.ToString(),
                SerializedPropertyType.Vector2 or SerializedPropertyType.Vector3 or SerializedPropertyType.Vector4 => value.Vector.ToString(),
                _ => "Changed"
            };
        }

        #endregion

        #region Drawing

        /// <summary>Displays the captured comparison without live writes.</summary>
        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string row in differences)
                EditorGUILayout.LabelField(row, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        #endregion

        #endregion
    }
}
