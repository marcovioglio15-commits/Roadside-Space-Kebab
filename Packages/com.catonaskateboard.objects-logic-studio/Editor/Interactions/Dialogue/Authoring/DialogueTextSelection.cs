using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Retains the native text selection while the token menu owns focus.</summary>
    internal sealed class DialogueTextSelection
    {
        #region Types

        /// <summary>Captures an immutable range before a delayed menu callback edits its page.</summary>
        internal readonly struct Range
        {
            internal readonly string Text;
            internal readonly int Start;
            internal readonly int End;

            #region Methods

            /// <summary>Orders native caret endpoints and bounds them to the captured page.</summary>
            /// <param name="text">Page before insertion.</param>
            /// <param name="cursor">Native caret index.</param>
            /// <param name="anchor">Native selection anchor.</param>
            internal Range(string text, int cursor, int anchor)
            {
                // Dragging from right to left reverses the native endpoints.
                Text = text;
                Start = Mathf.Clamp(Math.Min(cursor, anchor), 0, text.Length);
                End = Mathf.Clamp(Math.Max(cursor, anchor), 0, text.Length);
            }

            /// <summary>Wraps selected text or inserts an editable example at the caret.</summary>
            /// <param name="opening">Opening effect token or standalone marker.</param>
            /// <param name="closing">Closing effect token, or empty for a marker.</param>
            /// <param name="example">Editable text used only when a wrapping token has no selection.</param>
            /// <param name="start">Receives the selection inside the inserted tokens.</param>
            /// <param name="end">Receives the end of that selection.</param>
            /// <returns>Page text with the selection and surrounding text preserved.</returns>
            internal string Insert(string opening, string closing, string example, out int start, out int end)
            {
                // Standalone markers precede selected text without deleting it.
                string content = End > Start ? Text.Substring(Start, End - Start) : example;
                start = Start + opening.Length;
                end = start + content.Length;
                return Text.Substring(0, Start) + opening + content + closing + Text.Substring(End);
            }

            #endregion
        }

        #endregion

        #region State

        private UnityEngine.Object owner;
        private string path;
        private Range range;
        private bool restoreFocus;

        #endregion

        #region Methods

        #region Editing

        /// <summary>Draws multiline text and records the native caret before another control takes focus.</summary>
        /// <param name="text">Detached page text.</param>
        internal void Draw(SerializedProperty text)
        {
            // Each page has a stable control name within this entry editor.
            string controlName = "DialogueText." + text.serializedObject.targetObject.GetEntityId() + "." + text.propertyPath;
            EditorGUILayout.LabelField(new GUIContent("Text", text.tooltip));
            Rect rect = EditorGUILayout.GetControlRect(false, Mathf.Clamp(EditorStyles.textArea.CalcHeight(new GUIContent(text.stringValue),
                Mathf.Max(100f, EditorGUIUtility.currentViewWidth - 50f)), 64f, 220f));
            GUI.SetNextControlName(controlName);
            EditorGUI.BeginChangeCheck();
            string value = GUI.TextArea(rect, text.stringValue, EditorStyles.textArea);
            if (EditorGUI.EndChangeCheck())
                text.stringValue = value;
            if (restoreFocus && Matches(text))
            {
                // Menu callbacks request focus here, after the native text control exists again.
                EditorGUI.FocusTextInControl(controlName);
                TextEditor editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                editor.text = range.Text;
                editor.cursorIndex = range.End;
                editor.selectIndex = range.Start;
                restoreFocus = false;
            }
            if (GUI.GetNameOfFocusedControl() != controlName)
                return;
            TextEditor focused = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            owner = text.serializedObject.targetObject;
            path = text.propertyPath;
            range = new Range(text.stringValue, focused.cursorIndex, focused.selectIndex);
        }

        /// <summary>Returns this page's last selection or an insertion point at its end.</summary>
        /// <param name="text">Page opening the token menu.</param>
        /// <returns>A snapshot unaffected by later focus changes.</returns>
        internal Range Capture(SerializedProperty text)
        {
            // A selection retained on another page must not wrap this page's text.
            return Matches(text) ? range : new Range(text.stringValue, text.stringValue.Length, text.stringValue.Length);
        }

        /// <summary>Commits a token only while the menu's captured page is unchanged.</summary>
        /// <param name="text">Current detached destination.</param>
        /// <param name="selection">Selection captured when the menu opened.</param>
        /// <param name="opening">Opening or standalone token.</param>
        /// <param name="closing">Matching closing token.</param>
        /// <param name="example">Text selected for editing when no range was selected.</param>
        internal void Insert(SerializedProperty text, Range selection, string opening, string closing, string example)
        {
            // An outside edit cannot redirect a stale selection into different text.
            if (text.stringValue != selection.Text)
                return;
            text.stringValue = selection.Insert(opening, closing, example, out int start, out int end);
            owner = text.serializedObject.targetObject;
            path = text.propertyPath;
            range = new Range(text.stringValue, start, end);
            restoreFocus = true;
        }

        /// <summary>Checks page identity and text before reusing native caret indices.</summary>
        /// <param name="text">Current page property.</param>
        /// <returns>True when the retained range still belongs to this exact page.</returns>
        private bool Matches(SerializedProperty text)
        {
            // A delayed menu may outlive an edit to its original page.
            return owner == text.serializedObject.targetObject && path == text.propertyPath && range.Text == text.stringValue;
        }

        #endregion

        #endregion
    }
}
