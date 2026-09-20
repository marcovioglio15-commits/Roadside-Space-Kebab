using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Retains collapsed module sections without marking their preset values as edited.</summary>
    [Serializable]
    internal sealed class PlayerStudioSections
    {
        #region Serialized State

        [Header("Sections")]
        [Tooltip("Closed sections, identified independently of tab order and visibility.")]
        [SerializeField]
        private List<string> closed = new List<string>();

        #endregion

        #region Methods

        /// <summary>Draws one section heading while keeping GUI change tracking limited to data fields.</summary>
        /// <param name="key">Stable module and section identifier.</param>
        /// <param name="title">Single visible heading for this group.</param>
        /// <returns>True when the section's fields should be drawn.</returns>
        internal bool Draw(string key, string title)
        {
            // Folding changes workspace presentation, never the pending preset.
            bool wasChanged = GUI.changed;
            bool open = EditorGUILayout.Foldout(!closed.Contains(key), title, true, EditorStyles.foldoutHeader);
            GUI.changed = wasChanged;
            if (open)
                closed.Remove(key);
            else if (!closed.Contains(key))
                closed.Add(key);
            return open;
        }

        #endregion
    }
}
