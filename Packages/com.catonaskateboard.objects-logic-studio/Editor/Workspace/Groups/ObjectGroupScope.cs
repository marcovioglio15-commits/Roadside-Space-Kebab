using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Reuses the existing conditional controls for group discovery and focused editing.</summary>
    internal sealed class ObjectGroupScope : IDisposable
    {
        #region State

        private static ObjectGroupScope current;
        private readonly ObjectGroupScope previous;
        private readonly StudioIndentScope.Suspension indentation;
        private readonly List<ObjectGroupInfo> collected;
        private readonly ObjectGroupInfo focus;
        private readonly List<string> loose = new List<string>();
        private SerializedObject document;

        #endregion

        #region Properties

        internal static bool Active => current != null;

        #endregion

        #region Methods

        #region Lifetime

        /// <summary>Collects the available groups without drawing their controls.</summary>
        /// <param name="groups">List receiving each visible group.</param>
        internal ObjectGroupScope(List<ObjectGroupInfo> groups)
        {
            previous = current;
            current = this;
            collected = groups;
            indentation = new StudioIndentScope.Suspension(true);
        }

        /// <summary>Opens just one group's controls while keeping their normal dependencies.</summary>
        /// <param name="group">Selected group and field paths.</param>
        internal ObjectGroupScope(ObjectGroupInfo group)
        {
            previous = current;
            current = this;
            focus = group;
        }

        /// <summary>Restores an outer discovery or editing scope.</summary>
        public void Dispose()
        {
            if (collected != null && loose.Count > 0 && document != null)
            {
                string root = loose[0].StartsWith("Draft.", StringComparison.Ordinal) ? "Draft" : loose[0].StartsWith("Binding.", StringComparison.Ordinal) ? "Binding"
                    : loose[0].StartsWith("Single.", StringComparison.Ordinal) ? "Single.Draft" : "Extended.Draft";
                List<string> fields = new List<string>();
                foreach (string path in loose)
                    if (path.StartsWith(root + ".", StringComparison.Ordinal))
                        fields.Add(path.Substring(root.Length + 1));
                collected.Add(new ObjectGroupInfo(document.FindProperty(root), "General settings", "General settings", "Other settings for this interaction.", fields.ToArray()));
            }
            current = previous;
            indentation.Dispose();
        }

        #endregion

        #region Controls

        /// <summary>Registers groups during discovery or selects the matching control section during editing.</summary>
        /// <param name="parent">Serialized block represented by the section.</param>
        /// <param name="title">Existing section identifier.</param>
        /// <param name="tooltip">Short section description.</param>
        /// <param name="fields">Fields included in the section.</param>
        /// <returns>True only when this section contains the focused group.</returns>
        internal static bool Section(SerializedProperty parent, string title, string tooltip, string[] fields)
        {
            if (parent == null)
                return false;
            if (current.focus != null)
            {
                if (current.focus.Segments is { Length: > 0 })
                    foreach (ObjectGroupInfo segment in current.focus.Segments)
                        if (Matches(segment, parent, title))
                            return parent.serializedObject.targetObject is not ObjectWorkspace workspace
                                || ObjectWorkspaceGUI.Foldout(workspace, "Group." + parent.propertyPath + "." + title, title, ObjectFieldText.Short(tooltip));
                if (current.focus.Segments is { Length: > 0 })
                    return false;
                if (current.focus.Parent == parent.propertyPath)
                    return current.focus.Section == title;
                if (parent.propertyPath.StartsWith(current.focus.Parent + ".", StringComparison.Ordinal))
                    foreach (string path in current.focus.Paths)
                        if (path == parent.propertyPath || path.StartsWith(parent.propertyPath + ".", StringComparison.Ordinal)
                            || parent.propertyPath.StartsWith(path + ".", StringComparison.Ordinal))
                            return true;
                return false;
            }
            ObjectGroupLayout.Collect(current.collected, parent, title, tooltip, fields);
            return false;
        }

        /// <summary>Includes a section or its nested controls when editing a combined thematic group.</summary>
        /// <param name="group">One constituent section.</param>
        /// <param name="parent">Current control block.</param>
        /// <param name="title">Current section identifier.</param>
        /// <returns>True when this block belongs to the section.</returns>
        private static bool Matches(ObjectGroupInfo group, SerializedProperty parent, string title)
        {
            if (group.Parent == parent.propertyPath)
                return group.Section == title;
            if (!parent.propertyPath.StartsWith(group.Parent + ".", StringComparison.Ordinal))
                return false;
            foreach (string path in group.Paths)
                if (path == parent.propertyPath || path.StartsWith(parent.propertyPath + ".", StringComparison.Ordinal)
                    || parent.propertyPath.StartsWith(path + ".", StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>Hides fields outside the selected subgroup while retaining existing mode checks.</summary>
        /// <param name="property">Field about to be drawn.</param>
        /// <returns>True outside a scope or for a field included in the focused group.</returns>
        internal static bool Allows(SerializedProperty property)
        {
            if (current?.collected != null && property != null)
            {
                current.document = property.serializedObject;
                if (!current.loose.Contains(property.propertyPath))
                    current.loose.Add(property.propertyPath);
            }
            return current == null || current.focus != null && property != null && current.focus.Contains(property.propertyPath);
        }

        #endregion

        #endregion
    }
}
