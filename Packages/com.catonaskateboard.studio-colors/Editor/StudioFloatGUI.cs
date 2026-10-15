using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Shows useful numeric intervals without changing saved values merely by drawing a tool.</summary>
    internal static class StudioFloatGUI
    {
        #region Methods
        #region Drawing

        /// <summary>Preserves a serialized float's section title and spacing without invoking Unity's Range drawer.</summary>
        /// <param name="property">Field whose decorators precede the numeric row.</param>
        internal static void Decorations(SerializedProperty property)
        {
            (string Header, float Space) decorations = StudioPropertySchema.Decorations(property);
            if (decorations.Space > 0f)
                EditorGUILayout.Space(decorations.Space);
            if (string.IsNullOrEmpty(decorations.Header))
                return;
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(decorations.Header, EditorStyles.boldLabel);
        }

        /// <summary>Draws a slider when the declaration supplies a range, otherwise a precise numeric field.</summary>
        /// <param name="rect">Complete field row.</param>
        /// <param name="label">Caption and explanatory tooltip.</param>
        /// <param name="value">Current authored value, possibly outside the suggested interval.</param>
        /// <param name="range">Declared interval, or null for externally defined parameters.</param>
        /// <returns>The unchanged value unless the user edits a control.</returns>
        internal static float Draw(Rect rect, GUIContent label, float value, RangeAttribute range)
        {
            return range == null ? EditorGUI.FloatField(rect, label, value) : Draw(rect, label, value, range.min, range.max);
        }

        /// <summary>Combines visible endpoints, a bounded slider and an unrestricted precise input.</summary>
        /// <param name="rect">Complete field row.</param>
        /// <param name="label">Caption and explanatory tooltip.</param>
        /// <param name="value">Current authored value.</param>
        /// <param name="minimum">Suggested lower endpoint.</param>
        /// <param name="maximum">Suggested upper endpoint.</param>
        /// <returns>The edited value; repaint never clamps an existing value.</returns>
        internal static float Draw(Rect rect, GUIContent label, float value, float minimum, float maximum)
        {
            // Numeric entry remains available outside the slider interval; configuration validation owns warnings.
            string lower = minimum.ToString("G4", CultureInfo.InvariantCulture);
            string upper = maximum.ToString("G4", CultureInfo.InvariantCulture);
            Rect content = EditorGUI.PrefixLabel(rect, label);
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
            float numberWidth = Mathf.Min(72f, content.width * 0.45f);
            Rect number = new Rect(content.x, content.y, numberWidth, content.height);
            Rect interval = new Rect(number.xMax + 6f, content.y, Mathf.Max(0f, content.width - numberWidth - 6f), content.height);
            float lowerWidth = EditorStyles.miniLabel.CalcSize(new GUIContent(lower)).x + 6f;
            float upperWidth = EditorStyles.miniLabel.CalcSize(new GUIContent(upper)).x + 6f;

            // Compact windows retain both endpoints and precise entry, omitting only the slider track.
            if (interval.width < lowerWidth + upperWidth + 70f)
                EditorGUI.LabelField(interval, new GUIContent(lower + " ... " + upper, label.tooltip), EditorStyles.miniLabel);
            else
            {
                EditorGUI.LabelField(new Rect(interval.x, interval.y, lowerWidth, interval.height), lower, EditorStyles.miniLabel);
                EditorGUI.LabelField(new Rect(interval.xMax - upperWidth, interval.y, upperWidth, interval.height), upper, EditorStyles.miniLabel);
                Rect track = new Rect(interval.x + lowerWidth, interval.y, interval.width - lowerWidth - upperWidth, interval.height);
                EditorGUI.BeginChangeCheck();
                float selected = GUI.HorizontalSlider(track, float.IsFinite(value) ? Mathf.Clamp(value, minimum, maximum) : minimum, minimum, maximum);
                if (EditorGUI.EndChangeCheck())
                    value = selected;
            }
            return EditorGUI.FloatField(number, value);
        }

        /// <summary>Preserves prefab overrides, mixed values and Undo when replacing Unity's clamping Range drawer.</summary>
        /// <param name="rect">Complete serialized field row.</param>
        /// <param name="property">Float property being edited.</param>
        /// <param name="label">Caption and tooltip.</param>
        /// <param name="range">Declared numeric interval.</param>
        internal static void Property(Rect rect, SerializedProperty property, GUIContent label, RangeAttribute range)
        {
            using EditorGUI.PropertyScope scope = new EditorGUI.PropertyScope(rect, label, property);
            bool mixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            float value = Draw(rect, scope.content, property.floatValue, range);
            if (EditorGUI.EndChangeCheck())
                property.floatValue = value;
            EditorGUI.showMixedValue = mixed;
        }

        #endregion
        #endregion
    }
}
