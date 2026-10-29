using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Lets one editor surface customize shared field rendering without changing other Studio tools.</summary>
    public sealed class StudioFieldRenderScope : IDisposable
    {
        #region State

        private static Func<SerializedProperty, GUIContent, bool> current;
        private readonly Func<SerializedProperty, GUIContent, bool> previous;

        #endregion

        #region Methods

        #region Lifetime

        /// <summary>Installs a field renderer only for the lifetime of the current editor drawing scope.</summary>
        /// <param name="renderer">Returns true after drawing a supported field.</param>
        public StudioFieldRenderScope(Func<SerializedProperty, GUIContent, bool> renderer)
        {
            previous = current;
            current = renderer;
        }

        /// <summary>Restores the field renderer used by the enclosing editor surface.</summary>
        public void Dispose()
        {
            current = previous;
        }

        /// <summary>Offers the field to the current surface before using the normal shared control.</summary>
        /// <param name="property">Serialized field being drawn.</param>
        /// <param name="label">Caption and tooltip.</param>
        /// <returns>True when the surface already drew the field.</returns>
        internal static bool Draw(SerializedProperty property, GUIContent label)
        {
            return current != null && current(property, label);
        }

        #endregion

        #endregion
    }
}
