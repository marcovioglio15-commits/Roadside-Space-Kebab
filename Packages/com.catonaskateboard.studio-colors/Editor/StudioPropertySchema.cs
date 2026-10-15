using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Resolves exact editor field types where Unity reports only generic names such as Enum.</summary>
    internal static class StudioPropertySchema
    {
        #region State

        private static readonly Dictionary<(Type Owner, string Path), (Type Type, RangeAttribute Range, string Header, float Space)> fields =
            new Dictionary<(Type Owner, string Path), (Type Type, RangeAttribute Range, string Header, float Space)>();

        #endregion
        #region Methods
        #region Resolution

        /// <summary>Resolves and caches the declared type of a serialized field or array element.</summary>
        /// <param name="property">Field being copied or checked for Paste.</param>
        /// <returns>Its declared type, or null for native fields without a managed declaration.</returns>
        internal static Type Resolve(SerializedProperty property)
        {
            return Schema(property).Type;
        }

        /// <summary>Reads the declared slider interval without running reflection on every repaint.</summary>
        /// <param name="property">Bound field, including nested settings and array elements.</param>
        /// <returns>The field's range, or null for unbounded and native fields.</returns>
        internal static RangeAttribute Range(SerializedProperty property)
        {
            return property == null ? null : Schema(property).Range;
        }

        /// <summary>Shares declared ranges with transient drafts that do not expose a SerializedProperty.</summary>
        /// <param name="owner">Settings class declaring the field.</param>
        /// <param name="path">Field path within that class.</param>
        /// <returns>The cached interval, or null when no range is declared.</returns>
        internal static RangeAttribute Range(Type owner, string path)
        {
            return Schema(owner, path).Range;
        }

        /// <summary>Retains native headers and spacing when the numeric drawer is replaced.</summary>
        /// <param name="property">Serialized scalar being drawn.</param>
        /// <returns>The optional header and extra vertical spacing.</returns>
        internal static (string Header, float Space) Decorations(SerializedProperty property)
        {
            (Type Type, RangeAttribute Range, string Header, float Space) schema = Schema(property);
            return (schema.Header, schema.Space);
        }

        /// <summary>Caches field metadata exclusively in the editor; runtime settings remain reflection-free.</summary>
        /// <param name="property">Serialized path whose declaration is needed.</param>
        /// <returns>The managed type and optional slider interval.</returns>
        private static (Type Type, RangeAttribute Range, string Header, float Space) Schema(SerializedProperty property)
        {
            return Schema(property.serializedObject.targetObject.GetType(), property.propertyPath);
        }

        /// <summary>Resolves a managed path shared by serialized and transient controls.</summary>
        /// <param name="owner">Type at the root of the path.</param>
        /// <param name="path">Nested field and optional array-element path.</param>
        /// <returns>The cached field type and range.</returns>
        private static (Type Type, RangeAttribute Range, string Header, float Space) Schema(Type owner, string path)
        {
            // Resolve each owner/path once, including fields inherited from a base class.
            if (fields.TryGetValue((owner, path), out (Type Type, RangeAttribute Range, string Header, float Space) cached))
                return cached;
            Type current = owner;
            FieldInfo field = null;
            string[] segments = path.Split('.');
            for (int index = 0; index < segments.Length && current != null; index++)
            {
                if (segments[index] == "Array" && index + 1 < segments.Length && typeof(IList).IsAssignableFrom(current))
                {
                    index++;
                    current = segments[index] == "size" ? typeof(int) : current.IsArray ? current.GetElementType() : current.GetGenericArguments()[0];
                    continue;
                }
                field = null;
                for (Type declaring = current; declaring != null && field == null; declaring = declaring.BaseType)
                    field = declaring.GetField(segments[index], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                current = field?.FieldType;
            }
            return fields[(owner, path)] = (current, field?.GetCustomAttribute<RangeAttribute>(),
                field?.GetCustomAttribute<HeaderAttribute>()?.header, field?.GetCustomAttribute<SpaceAttribute>()?.height ?? 0f);
        }

        #endregion
        #endregion
    }
}
