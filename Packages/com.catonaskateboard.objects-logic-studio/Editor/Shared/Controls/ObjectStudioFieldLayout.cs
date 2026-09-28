using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Reserves readable labels across nested interaction cards and restores the caller's layout.</summary>
    internal readonly struct ObjectStudioFieldLayout : IDisposable
    {
        #region State

        private readonly float labelWidth;

        #endregion

        #region Methods

        #region Layout

        /// <summary>Allows nested labels more space while retaining room for field values.</summary>
        /// <param name="minimum">Preferred minimum label width before nested indentation.</param>
        internal ObjectStudioFieldLayout(float minimum)
        {
            // The width follows the current editor surface, including narrow docked Inspectors.
            labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Min(Mathf.Clamp(EditorGUIUtility.currentViewWidth * 0.35f, minimum, 290f),
                Mathf.Max(100f, EditorGUIUtility.currentViewWidth - 135f));
        }

        /// <summary>Restores the previous label width for the rest of Unity's controls.</summary>
        public void Dispose()
        {
            EditorGUIUtility.labelWidth = labelWidth;
        }

        #endregion

        #endregion
    }
}
