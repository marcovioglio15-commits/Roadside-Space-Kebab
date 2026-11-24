using System;
using System.Collections.Generic;
using System.Reflection;
using CatOnASkateboard.StudioIdentity;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Resolves portable vector arrays against editor schemas without creating preview components.</summary>
    internal static class ObjectFlagPortableArrays
    {
        #region State

        private static readonly Dictionary<string, Type> types = new Dictionary<string, Type>();
        private static readonly Dictionary<(Type Owner, string Name), Type> fields = new Dictionary<(Type, string), Type>();

        #endregion
        #region Methods
        #region Resolution

        /// <summary>Checks the original field type when a saved vector has no elements carrying type metadata.</summary>
        /// <param name="property">Portable array stored in a group or component preset.</param>
        /// <returns>True when the original field is an ObjectFlag array or list.</returns>
        internal static bool IsFlagArray(SerializedProperty property)
        {
            // Build the original field chain while ignoring portable storage containers.
            List<SerializedProperty> chain = new List<SerializedProperty>();
            string prefix = string.Empty;
            foreach (string segment in property.propertyPath.Split('.'))
            {
                prefix = prefix.Length == 0 ? segment : prefix + "." + segment;
                SerializedProperty value = property.serializedObject.FindProperty(prefix);
                if (ObjectFlagProject.IsPortableValue(value))
                    chain.Add(value);
            }
            if (chain.Count == 0)
                return false;
            Type type = RootType(property, chain[0], out string path);
            type = PathType(type, path);
            for (int index = 1; index < chain.Count && type != null; index++)
            {
                string name = chain[index].FindPropertyRelative("Name").stringValue;
                type = name.StartsWith("data[", StringComparison.Ordinal) ? ElementType(type) : PathType(type, name);
            }
            return ElementType(type) == typeof(ObjectFlag);
        }

        /// <summary>Locates component metadata or the workspace path saved with a group value.</summary>
        /// <param name="property">Final portable array.</param>
        /// <param name="root">First portable value in its ancestor chain.</param>
        /// <param name="path">Receives the original root field path.</param>
        /// <returns>The original component, workspace or owner type.</returns>
        private static Type RootType(SerializedProperty property, SerializedProperty root, out string path)
        {
            path = root.FindPropertyRelative("Name").stringValue;
            if (property.serializedObject.targetObject is ObjectGroupPreset group)
            {
                int start = root.propertyPath.IndexOf("data[", StringComparison.Ordinal) + 5;
                int end = root.propertyPath.IndexOf(']', start);
                if (group.Group?.Paths != null && start >= 5 && end > start && int.TryParse(root.propertyPath.Substring(start, end - start), out int index)
                    && index < group.Group.Paths.Length)
                    path = group.Group.Paths[index];
                return typeof(ObjectWorkspace);
            }
            int component = root.propertyPath.IndexOf(".Fields.Array.data[", StringComparison.Ordinal);
            if (component >= 0)
            {
                string name = property.serializedObject.FindProperty(root.propertyPath.Substring(0, component) + ".TypeName")?.stringValue;
                return ResolveType(name);
            }
            return property.serializedObject.targetObject.GetType();
        }

        /// <summary>Resolves field paths through inherited serialized fields and collection elements.</summary>
        /// <param name="type">Original root type.</param>
        /// <param name="path">Original serialized field path.</param>
        /// <returns>The declared destination type, or null for an unavailable field.</returns>
        private static Type PathType(Type type, string path)
        {
            foreach (string segment in path.Split('.'))
            {
                if (type == null)
                    return null;
                if (segment == "Array")
                    continue;
                if (segment.StartsWith("data[", StringComparison.Ordinal))
                {
                    type = ElementType(type);
                    continue;
                }
                if (!fields.TryGetValue((type, segment), out Type field))
                {
                    // Reflection is confined to editor schema discovery and cached per declared field.
                    for (Type current = type; current != null && field == null; current = current.BaseType)
                        field = current.GetField(segment, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)?.FieldType;
                    fields.Add((type, segment), field);
                }
                type = field;
            }
            return type;
        }

        /// <summary>Resolves a saved component type once per editor assembly reload.</summary>
        /// <param name="name">Saved assembly-qualified component name.</param>
        /// <returns>The available component type, or null.</returns>
        private static Type ResolveType(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            if (!types.TryGetValue(name, out Type type))
            {
                type = Type.GetType(name, false);
                types.Add(name, type);
            }
            return type;
        }

        /// <summary>Reads the declared element type of an array or generic list.</summary>
        /// <param name="type">Potential collection type.</param>
        /// <returns>The collection element type, or null for scalar fields.</returns>
        private static Type ElementType(Type type)
        {
            if (type == null)
                return null;
            if (type.IsArray)
                return type.GetElementType();
            return type.IsGenericType && type.GetGenericArguments().Length == 1 ? type.GetGenericArguments()[0] : null;
        }

        #endregion
        #endregion
    }
}
