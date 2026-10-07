using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Adds Copy/Paste to transient tool fields while returning edits through their normal GUI transaction.</summary>
    internal static class StudioValueMenu
    {
        #region State

        private static object pending;
        private static bool hasPending;
        private static int pendingId;
        private static EditorWindow pendingWindow;

        #endregion

        #region Methods
        #region Editing

        /// <summary>Opens a typed menu and delivers delayed paste back to the originating control.</summary>
        /// <typeparam name="T">Native value type returned by the field.</typeparam>
        /// <param name="rect">Field rectangle including its label.</param>
        /// <param name="label">Label used to keep control identity stable.</param>
        /// <param name="value">Current field value.</param>
        /// <param name="accepted">Optional validation for reference types or popup ranges.</param>
        /// <returns>The current value or a compatible pending paste.</returns>
        internal static T Edit<T>(Rect rect, GUIContent label, T value, Func<T, bool> accepted = null)
        {
            // Serialized controls use the shared property clipboard and prefab operations.
            SerializedProperty property = StudioFieldMenu.TakeNext();
            int id = GUIUtility.GetControlID(label, FocusType.Passive, rect);
            EditorWindow window = EditorWindow.mouseOverWindow ?? EditorWindow.focusedWindow;
            if (property != null)
            {
                StudioFieldMenu.Context(rect, property);
                return value;
            }
            if (hasPending && pendingWindow == window && pendingId == id && (pending is T || pending == null && !typeof(T).IsValueType))
            {
                T result = (T)pending;
                hasPending = false;
                pendingWindow = null;
                pending = null;
                if (GUI.enabled && (accepted == null || accepted(result)))
                {
                    GUI.changed = true;
                    value = result;
                }
            }
            if (Event.current.type != EventType.ContextClick || !rect.Contains(Event.current.mousePosition))
                return value;
            GenericMenu menu = new GenericMenu();
            T copied = value;
            menu.AddItem(new GUIContent("Copy"), false, () =>
            {
                StudioFieldMenu.Clipboard = new StudioPropertyValue(copied, copied is Enum enumeration ? enumeration.GetType() : typeof(T));
            });
            if (GUI.enabled && !EditorApplication.isPlayingOrWillChangePlaymode && StudioFieldMenu.Clipboard != null
                && StudioFieldMenu.Clipboard.TryRead(out T pastedValue) && (accepted == null || accepted(pastedValue)))
            {
                object pasted = pastedValue;
                menu.AddItem(new GUIContent("Paste"), false, () =>
                {
                    pending = pasted;
                    hasPending = true;
                    pendingId = id;
                    pendingWindow = window;
                    if (window != null)
                        window.Repaint();
                });
            }
            else
                menu.AddDisabledItem(new GUIContent("Paste"));
            StudioFieldMenu.Unavailable(menu);
            StudioFieldColors.Menu(menu, StudioFieldColors.Key(null, label.text));
            menu.ShowAsContext();
            Event.current.Use();
            return value;
        }

        #endregion
        #endregion
    }
}
