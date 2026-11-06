using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Fits a complete caption beside its field, or wraps it above the field in a narrow pane.</summary>
    public readonly struct StudioFieldLayout : IDisposable
    {
        #region State

        private const float panePadding = 26f;
        private static float paneWidth;
        private readonly float previousLabelWidth;
        private readonly float captionHeight;
        private readonly GUIContent caption;

        #endregion

        #region Properties

        /// <summary>Caption supplied to the native field when it fits on the same row.</summary>
        public GUIContent InlineLabel => captionHeight > 0f ? GUIContent.none : caption;

        #endregion

        #region Methods

        #region Layout

        /// <summary>Measures the caption and reserves enough horizontal room for the editable value.</summary>
        /// <param name="label">Complete field caption and tooltip.</param>
        /// <param name="minimumValueWidth">Minimum usable width for the native value widget.</param>
        public StudioFieldLayout(GUIContent label, float minimumValueWidth = 110f)
        {
            previousLabelWidth = EditorGUIUtility.labelWidth;
            caption = label ?? GUIContent.none;
            captionHeight = 0f;
            if (string.IsNullOrEmpty(caption.text))
                return;

            // Measure bold captions as well, since Unity can bold overridden properties.
            float viewWidth = paneWidth > 0f ? paneWidth : EditorGUIUtility.currentViewWidth;
            float width = (float.IsFinite(viewWidth) && viewWidth > panePadding ? viewWidth : 360f) - panePadding;
            float indentation = EditorGUI.IndentedRect(new Rect(0f, 0f, width, 0f)).x;
            float required = CaptionStyle(false).CalcSize(caption).x + indentation + 10f;
            float available = width - minimumValueWidth;
            if (required <= available)
                EditorGUIUtility.labelWidth = Mathf.Clamp(previousLabelWidth, required, available);
            else
                captionHeight = Mathf.Max(EditorGUIUtility.singleLineHeight,
                    CaptionStyle(true).CalcHeight(caption, Mathf.Max(1f, width - indentation)));
        }

        /// <summary>Uses Inspector styles even when a native drawer measures its height outside an IMGUI event.</summary>
        /// <param name="wrapped">Whether the caption occupies a separate wrapping row.</param>
        /// <returns>The style used to measure the complete caption.</returns>
        private static GUIStyle CaptionStyle(bool wrapped)
        {
            if (Event.current != null)
                return wrapped ? EditorStyles.wordWrappedLabel : EditorStyles.boldLabel;
            GUISkin skin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);
            return skin.FindStyle(wrapped ? "WordWrappedLabel" : "BoldLabel") ?? skin.label;
        }

        /// <summary>Reserves one row for the caption and value so both can share context actions.</summary>
        /// <param name="controlHeight">Height required by the native field.</param>
        /// <param name="options">Optional IMGUI layout constraints.</param>
        /// <returns>Complete rectangle occupied by the field.</returns>
        public Rect GetRect(float controlHeight, params GUILayoutOption[] options)
        {
            return EditorGUILayout.GetControlRect(true, Height(controlHeight), options);
        }

        /// <summary>Includes any wrapped caption when a caller owns its own rectangle.</summary>
        /// <param name="controlHeight">Height required by the native field.</param>
        /// <returns>Total height of the caption and field.</returns>
        public float Height(float controlHeight)
        {
            return captionHeight > 0f ? controlHeight + captionHeight + EditorGUIUtility.standardVerticalSpacing : controlHeight;
        }

        /// <summary>Draws a stacked caption and returns the rectangle for the native control.</summary>
        /// <param name="row">Complete rectangle reserved for this field.</param>
        /// <returns>Field rectangle with the stacked caption removed.</returns>
        public Rect Control(Rect row)
        {
            if (captionHeight <= 0f)
                return row;
            Rect label = row;
            label.height = captionHeight;
            EditorGUI.LabelField(label, caption, EditorStyles.wordWrappedLabel);
            row.yMin = label.yMax + EditorGUIUtility.standardVerticalSpacing;
            return row;
        }

        /// <summary>Draws the caption for a manually positioned value, such as a hierarchy dropdown.</summary>
        /// <param name="row">Complete rectangle reserved for this field.</param>
        /// <returns>The rectangle used by the value widget.</returns>
        public Rect ValueRect(Rect row) => EditorGUI.PrefixLabel(Control(row), InlineLabel);

        /// <summary>Restores the caller's native label width after this field is drawn.</summary>
        public void Dispose() => EditorGUIUtility.labelWidth = previousLabelWidth;

        #endregion

        #endregion

        /// <summary>Uses the width of a settings pane that occupies only part of the editor window.</summary>
        public readonly struct WidthScope : IDisposable
        {
            private readonly float previous;

            #region Methods

            /// <summary>Supplies the available width during this pane's IMGUI pass.</summary>
            /// <param name="width">Measured pane width in GUI points.</param>
            public WidthScope(float width)
            {
                previous = paneWidth;
                if (float.IsFinite(width) && width > 0f)
                    paneWidth = width;
            }

            /// <summary>Restores the width of the enclosing editor surface.</summary>
            public void Dispose() => paneWidth = previous;

            #endregion
        }
    }
}
