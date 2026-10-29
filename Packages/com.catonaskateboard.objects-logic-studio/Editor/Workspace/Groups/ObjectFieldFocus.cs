using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Highlights a tree-selected field and exposes its position for one-time scrolling.</summary>
    internal sealed class ObjectFieldFocus
    {
        #region Types

        /// <summary>Limits focus tracking to one group editor's controls.</summary>
        internal readonly struct Scope : IDisposable
        {
            private readonly ObjectFieldFocus previous;

            #region Methods

            /// <summary>Installs the current editor's focus without allocating another tracking object.</summary>
            /// <param name="owner">Editor focus state.</param>
            internal Scope(ObjectFieldFocus owner)
            {
                previous = current;
                current = owner;
                owner.Found = false;
            }

            /// <summary>Restores an enclosing editor's focus tracking.</summary>
            public void Dispose()
            {
                current = previous;
            }

            #endregion
        }

        /// <summary>Records one field after its normal conditional control has finished drawing.</summary>
        internal readonly struct Marker : IDisposable
        {
            private readonly ObjectFieldFocus owner;

            #region Methods

            /// <summary>Names only the requested control; unrelated fields keep their normal behavior.</summary>
            /// <param name="property">Control about to be drawn.</param>
            internal Marker(SerializedProperty property)
            {
                owner = current != null && property != null && current.path == property.propertyPath ? current : null;
                if (owner != null)
                    GUI.SetNextControlName("ObjectSetting." + owner.path);
            }

            /// <summary>Captures the field rectangle and draws a small navigation accent.</summary>
            public void Dispose()
            {
                if (owner == null || Event.current.type != EventType.Repaint)
                    return;
                owner.Rect = GUILayoutUtility.GetLastRect();
                owner.Found = true;
                EditorGUI.DrawRect(new Rect(owner.Rect.x - 3f, owner.Rect.y, 2f, owner.Rect.height), new Color(0.3f, 0.65f, 0.95f));
            }

            #endregion
        }

        #endregion

        #region State

        private static ObjectFieldFocus current;
        private string path = string.Empty;
        internal bool Found;
        internal Rect Rect;
        internal bool RevealSections;

        #endregion

        #region Methods

        #region Navigation

        /// <summary>Expands parent properties so a block can navigate to a nested setting.</summary>
        /// <param name="data">Group document.</param>
        /// <param name="setting">Exact selected field.</param>
        internal void Configure(SerializedObject data, string setting)
        {
            path = setting ?? string.Empty;
            string parent = path;
            while (!string.IsNullOrEmpty(parent))
            {
                using SerializedProperty property = data.FindProperty(parent);
                if (property != null)
                    property.isExpanded = true;
                int separator = parent.LastIndexOf('.');
                parent = separator > 0 ? parent.Substring(0, separator) : string.Empty;
            }
        }

        /// <summary>Preserves slider rendering while tracking ordinary and nested property controls.</summary>
        /// <param name="property">Serialized setting.</param>
        /// <param name="label">Visible caption and tooltip.</param>
        /// <returns>True when the field was drawn.</returns>
        internal static bool Draw(SerializedProperty property, GUIContent label)
        {
            using Marker marker = new Marker(property);
            if (ObjectNumericFields.Draw(property, label))
                return true;
            if (current == null || current.path != property.propertyPath)
                return false;
            using StudioFieldRenderScope normal = new StudioFieldRenderScope(null);
            StudioFieldGUI.PropertyField(property, label, true);
            return true;
        }

        /// <summary>Reveals a catalog section containing the option selected from the tree.</summary>
        /// <param name="owner">Serialized settings block.</param>
        /// <param name="fields">Relative fields controlled by the heading.</param>
        /// <returns>True only during navigation to a field in this section.</returns>
        internal static bool ExpandSection(SerializedProperty owner, string[] fields)
        {
            if (current == null || !current.RevealSections)
                return false;
            foreach (string field in fields)
            {
                string candidate = owner.propertyPath + "." + field;
                if (current.path == candidate || current.path.StartsWith(candidate + ".", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        #endregion

        #endregion
    }
}
