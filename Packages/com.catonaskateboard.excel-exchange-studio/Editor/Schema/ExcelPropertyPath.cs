using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Resolves typed editor paths and creates independent collection defaults.</summary>
    public static class ExcelPropertyPath
    {
        #region Fields

        private static readonly Dictionary<(Type, string), FieldInfo> fields = new Dictionary<(Type, string), FieldInfo>();
        private static readonly Dictionary<Type, FieldInfo[]> declarations = new Dictionary<Type, FieldInfo[]>();

        #endregion
        #region Methods
        #region Metadata

        /// <summary>Caches serializable field declarations, excluding Unity object ownership.</summary>
        /// <param name="type">Settings or preset type.</param>
        /// <returns>Fields in base-to-derived declaration order.</returns>
        internal static FieldInfo[] Fields(Type type)
        {
            if (declarations.TryGetValue(type, out FieldInfo[] cached))
                return cached;
            List<FieldInfo> result = new List<FieldInfo>();
            if (type.BaseType != null && type.BaseType != typeof(UnityEngine.Object)
                && type.BaseType != typeof(ScriptableObject) && type.BaseType != typeof(MonoBehaviour)
                && type.BaseType != typeof(Behaviour) && type.BaseType != typeof(Component))
                result.AddRange(Fields(type.BaseType));
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (!field.IsStatic && !field.IsInitOnly && !field.IsNotSerialized
                    && (field.IsPublic || field.IsDefined(typeof(SerializeField)) || field.IsDefined(typeof(SerializeReference)))
                    && !field.IsDefined(typeof(HideInInspector)) && !field.Name.StartsWith("m_", StringComparison.Ordinal))
                    result.Add(field);
            return declarations[type] = result.ToArray();
        }

        /// <summary>Finds a serialized member including private fields in a base class.</summary>
        /// <param name="type">Managed owner type.</param>
        /// <param name="name">Declared field name.</param>
        /// <returns>The cached field or an actionable missing-field exception.</returns>
        public static FieldInfo Field(Type type, string name)
        {
            if (fields.TryGetValue((type, name), out FieldInfo cached))
                return cached;
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo found = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (found != null)
                    return fields[(type, name)] = found;
            }
            throw new InvalidOperationException("Field no longer exists: " + type.Name + "." + name);
        }

        /// <summary>Recognizes Unity-serializable arrays and generic lists.</summary>
        /// <param name="type">Declared container type.</param>
        /// <returns>Element type or null for a single value.</returns>
        public static Type ElementType(Type type)
        {
            return type.IsArray ? type.GetElementType() : type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)
                ? type.GetGenericArguments()[0] : null;
        }

        /// <summary>Lists repeated parent paths from outermost to innermost.</summary>
        /// <param name="path">Logical property or collection path.</param>
        /// <returns>Prefixes ending with an empty element token.</returns>
        public static string[] Collections(string path)
        {
            List<string> result = new List<string>();
            for (int index = path.IndexOf("[]", StringComparison.Ordinal); index >= 0;
                index = path.IndexOf("[]", index + 2, StringComparison.Ordinal))
                result.Add(path.Substring(0, index + 2));
            return result.ToArray();
        }

        #endregion
        #region Values

        /// <summary>Reads a field or final collection using explicit parent indices.</summary>
        /// <param name="root">Detached typed document.</param>
        /// <param name="path">Logical path with empty element tokens.</param>
        /// <param name="indices">Indices for repeated parents; a final collection needs only its ancestors.</param>
        /// <returns>The current typed value.</returns>
        public static object Get(object root, string path, params int[] indices)
        {
            int cursor = 0;
            foreach (string segment in path.Split('.'))
            {
                if (root == null)
                    return null;
                bool repeated = segment.EndsWith("[]", StringComparison.Ordinal);
                root = Field(root.GetType(), repeated ? segment.Substring(0, segment.Length - 2) : segment).GetValue(root);
                if (repeated && cursor < indices.Length)
                    root = ((IList)root)[indices[cursor++]];
            }
            return root;
        }

        /// <summary>Writes a field and propagates boxed struct changes back to their parents.</summary>
        /// <param name="root">Detached typed document.</param>
        /// <param name="path">Logical destination path.</param>
        /// <param name="value">Converted value matching the declared type.</param>
        /// <param name="indices">Repeated parent indices.</param>
        public static void Set(object root, string path, object value, params int[] indices)
        {
            SetValue(root, path.Split('.'), 0, indices, 0, value);
        }

        /// <summary>Updates one nested value without losing changes to value-type settings.</summary>
        /// <param name="owner">Current boxed owner.</param>
        /// <param name="segments">Logical path parts.</param>
        /// <param name="depth">Current path position.</param>
        /// <param name="indices">Collection indices.</param>
        /// <param name="cursor">Next collection index.</param>
        /// <param name="value">Converted destination value.</param>
        private static void SetValue(object owner, string[] segments, int depth, int[] indices, int cursor, object value)
        {
            string segment = segments[depth];
            bool repeated = segment.EndsWith("[]", StringComparison.Ordinal);
            FieldInfo field = Field(owner.GetType(), repeated ? segment.Substring(0, segment.Length - 2) : segment);
            if (depth + 1 == segments.Length)
            {
                if (repeated && cursor < indices.Length)
                    ((IList)field.GetValue(owner))[indices[cursor]] = value;
                else
                    field.SetValue(owner, value);
                return;
            }
            object child = field.GetValue(owner);
            if (child == null)
                child = Activator.CreateInstance(field.FieldType);
            if (repeated)
            {
                IList list = (IList)child;
                object element = list[indices[cursor]];
                SetValue(element, segments, depth + 1, indices, cursor + 1, value);
                list[indices[cursor]] = element;
            }
            else
                SetValue(child, segments, depth + 1, indices, cursor, value);
            field.SetValue(owner, child);
        }

        /// <summary>Rebuilds a collection without copying the last serialized element.</summary>
        /// <param name="type">Declared array or list type.</param>
        /// <param name="elements">Independent typed elements.</param>
        /// <returns>A new container preserving the supplied order.</returns>
        public static object Collection(Type type, IList elements)
        {
            IList result = type.IsArray ? Array.CreateInstance(ElementType(type), elements.Count) : (IList)Activator.CreateInstance(type);
            for (int index = 0; index < elements.Count; index++)
                if (type.IsArray)
                    result[index] = elements[index];
                else
                    result.Add(elements[index]);
            return result;
        }

        #endregion
        #endregion
    }
}
