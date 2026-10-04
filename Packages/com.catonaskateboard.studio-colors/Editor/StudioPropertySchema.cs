using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Resolves exact editor field types where Unity reports only generic names such as Enum.</summary>
    internal static class StudioPropertySchema
    {
        #region State

        private static readonly Dictionary<(Type Owner, string Path), Type> types = new Dictionary<(Type Owner, string Path), Type>();

        #endregion
        #region Methods
        #region Resolution

        /// <summary>Resolves and caches the declared type of a serialized field or array element.</summary>
        /// <param name="property">Field being copied or checked for Paste.</param>
        /// <returns>Its declared type, or null for native fields without a managed declaration.</returns>
        internal static Type Resolve(SerializedProperty property)
        {
            // This editor-only lookup runs on menu operations, never during gameplay or ordinary repaint.
            Type owner = property.serializedObject.targetObject.GetType();
            string path = property.propertyPath;
            if (types.TryGetValue((owner, path), out Type cached))
                return cached;
            Type current = owner;
            string[] segments = path.Split('.');
            for (int index = 0; index < segments.Length && current != null; index++)
            {
                if (segments[index] == "Array" && index + 1 < segments.Length && typeof(IList).IsAssignableFrom(current))
                {
                    index++;
                    current = segments[index] == "size" ? typeof(int) : current.IsArray ? current.GetElementType() : current.GetGenericArguments()[0];
                    continue;
                }
                FieldInfo field = null;
                for (Type declaring = current; declaring != null && field == null; declaring = declaring.BaseType)
                    field = declaring.GetField(segments[index], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                current = field?.FieldType;
            }
            types[(owner, path)] = current;
            return current;
        }

        #endregion
        #endregion
    }
}
