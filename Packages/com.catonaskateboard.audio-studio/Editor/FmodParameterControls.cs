using System.Collections.Generic;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.AudioStudio.Editor
{
    /// <summary>Uses loaded FMOD metadata for parameter sliders instead of assuming every event uses normalized values.</summary>
    internal static class FmodParameterControls
    {
        #region Methods
        #region Controls

        /// <summary>Edits parameter entries individually while retaining the binding's existing transaction.</summary>
        /// <param name="binding">Audio event binding with a parameter array.</param>
        /// <param name="catalog">Explicitly refreshed event metadata.</param>
        internal static void Draw(SerializedProperty binding, IReadOnlyList<FmodCatalogEntry> catalog)
        {
            SerializedProperty parameters = binding.FindPropertyRelative("Parameters");
            parameters.isExpanded = StudioArrayGUI.Foldout(parameters, new GUIContent(parameters.displayName, parameters.tooltip));
            if (!parameters.isExpanded)
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            string path = binding.FindPropertyRelative("EventPath").stringValue;

            // Loaded event limits take precedence; unavailable metadata leaves precise entry unrestricted.
            for (int index = 0; index < parameters.arraySize; index++)
            {
                SerializedProperty parameter = parameters.GetArrayElementAtIndex(index);
                string name = parameter.FindPropertyRelative("Name").stringValue;
                if (StudioArrayGUI.Header(parameters, index, new GUIContent(string.IsNullOrEmpty(name) ? "Parameter" : name, parameters.tooltip)))
                    break;
                if (!parameter.isExpanded)
                    continue;
                StudioFields.Draw(parameter, "Name", "Global");
                FmodCatalogParameter metadata = Find(catalog, path, parameter.FindPropertyRelative("Name").stringValue);
                SerializedProperty value = parameter.FindPropertyRelative("Value");
                GUIContent label = new GUIContent("Value", metadata == null
                    ? "FMOD limits are unavailable until the matching catalog is loaded."
                    : "Value supplied to FMOD. Slider endpoints come from the loaded event parameter.");
                value.floatValue = StudioFieldGUI.FloatField(StudioFieldMenu.Value(value, label), value.floatValue,
                    metadata?.Minimum ?? 0f, metadata?.Maximum ?? 1f);
            }
            StudioArrayGUI.Add(parameters, "Add Parameter", () => new AudioParameter());
        }

        /// <summary>Finds a parameter only within the binding's selected event.</summary>
        /// <param name="catalog">Cached event catalog.</param>
        /// <param name="path">Exact event path.</param>
        /// <param name="name">Exact parameter name.</param>
        /// <returns>Matching metadata, or null before it is available.</returns>
        private static FmodCatalogParameter Find(IReadOnlyList<FmodCatalogEntry> catalog, string path, string name)
        {
            foreach (FmodCatalogEntry entry in catalog)
                if (entry.Path == path)
                {
                    foreach (FmodCatalogParameter parameter in entry.Parameters)
                        if (parameter.Name == name)
                            return parameter;
                    break;
                }
            return null;
        }

        #endregion
        #endregion
    }
}
