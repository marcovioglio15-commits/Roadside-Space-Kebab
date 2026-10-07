using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Draws nested serialized structures without exposing Unity's destructive array-size controls.</summary>
    public static class StudioStructureGUI
    {
        #region State

        private static readonly Dictionary<Type, Action<SerializedProperty>> drawers = new Dictionary<Type, Action<SerializedProperty>>();

        #endregion

        #region Methods
        #region Controls

        /// <summary>Preserves a tool's conditional controls when its settings appear inside a generic structure.</summary>
        /// <typeparam name="T">Serialized configuration type.</typeparam>
        /// <param name="draw">Existing conditional controls for this type.</param>
        public static void Register<T>(Action<SerializedProperty> draw)
        {
            drawers[typeof(T)] = draw;
        }

        /// <summary>Draws immediate children recursively while retaining field menus and safe array operations.</summary>
        /// <param name="property">Structured field whose children will be displayed.</param>
        /// <param name="label">Foldout caption and tooltip.</param>
        /// <returns>Whether this structure is expanded.</returns>
        public static bool Draw(SerializedProperty property, GUIContent label)
        {
            // Registered controls preserve conditional settings for specialized structures.
            Type type = StudioPropertySchema.Resolve(property);
            if (type != null && drawers.TryGetValue(type, out Action<SerializedProperty> draw))
            {
                draw(property);
                return property.isExpanded;
            }
            property.isExpanded = StudioArrayGUI.Foldout(property, label);
            if (!property.isExpanded)
                return false;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            using SerializedProperty child = property.Copy();
            using SerializedProperty end = property.GetEndProperty();
            if (child.NextVisible(true))
                do
                {
                    if (child.depth <= property.depth || SerializedProperty.EqualContents(child, end))
                        break;
                    StudioFieldGUI.PropertyField(child, new GUIContent(child.displayName, child.tooltip), true);
                }
                while (child.NextVisible(false));
            return true;
        }

        #endregion
        #endregion
    }
}
