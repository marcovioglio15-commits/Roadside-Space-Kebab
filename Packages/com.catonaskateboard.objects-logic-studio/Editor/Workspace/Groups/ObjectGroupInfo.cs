using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Identifies the exact fields opened by one settings group.</summary>
    [Serializable]
    internal sealed class ObjectGroupInfo
    {
        #region Fields

        [Header("Group")]
        [Tooltip("Group name.")]
        public string Title;
        [Tooltip("What this group changes.")]
        public string Tooltip;
        [Tooltip("Settings block containing this group.")]
        public string Parent;
        [Tooltip("Controls used by this group.")]
        public string Section;
        [Tooltip("Fields included in this group.")]
        public string[] Paths;
        [Tooltip("Interaction type linked to this group.")]
        public string TypeName;
        [Tooltip("Sections edited together in this settings group.")]
        [SerializeReference]
        public ObjectGroupInfo[] Segments = Array.Empty<ObjectGroupInfo>();

        #endregion

        #region Properties

        /// <summary>Stable group identity shared by presets and search.</summary>
        internal string Key => TypeName + "|" + Parent + "|" + Section + "|" + Title;

        #endregion

        #region Methods

        #region Definition

        /// <summary>Creates a thematic group that combines several existing control sections.</summary>
        /// <param name="title">Visible theme.</param>
        /// <param name="segments">Sections sharing one preset and edit window.</param>
        internal ObjectGroupInfo(string title, ObjectGroupInfo[] segments)
        {
            Title = title;
            Tooltip = "Edit " + title.ToLowerInvariant() + " settings.";
            Parent = string.Empty;
            Section = "Settings";
            TypeName = segments[0].TypeName;
            Segments = segments;
            System.Collections.Generic.List<string> paths = new System.Collections.Generic.List<string>();
            foreach (ObjectGroupInfo segment in segments)
                foreach (string path in segment.Paths)
                    if (!paths.Contains(path))
                        paths.Add(path);
            // A complete block already includes its descendants; retain each value only once.
            paths.RemoveAll(path => paths.Exists(other => path != other && path.StartsWith(other + ".", StringComparison.Ordinal)));
            Paths = paths.ToArray();
        }

        /// <summary>Captures stable serialized paths rather than keeping temporary property handles.</summary>
        /// <param name="parent">Serialized settings block.</param>
        /// <param name="section">Existing control section that draws these fields.</param>
        /// <param name="title">Short group label.</param>
        /// <param name="tooltip">Brief explanation of the group.</param>
        /// <param name="fields">Relative child paths, or empty for the whole block.</param>
        internal ObjectGroupInfo(SerializedProperty parent, string section, string title, string tooltip, params string[] fields)
        {
            Parent = parent.propertyPath;
            Section = section;
            Title = title;
            Tooltip = tooltip;
            Paths = fields.Length == 0 ? new[] { Parent } : Array.ConvertAll(fields, field => Parent + "." + field);
        }

        /// <summary>Checks whether a field belongs to this group, including nested array contents.</summary>
        /// <param name="path">Serialized field path.</param>
        /// <returns>True for a selected field or one of its children.</returns>
        internal bool Contains(string path)
        {
            foreach (string selected in Paths)
                if (path == selected || path.StartsWith(selected + ".", StringComparison.Ordinal))
                    return true;
            return false;
        }

        #endregion

        #endregion
    }
}
