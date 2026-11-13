using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Describes one logical field independently of current array lengths.</summary>
    public sealed class ExcelField
    {
        #region Fields

        public readonly string Path;
        public readonly string Label;
        public readonly string Tooltip;
        public readonly Type Type;
        public readonly string Collection;
        public readonly string CollectionLabel;
        public readonly string CollectionTooltip;

        #endregion
        #region Methods

        /// <summary>Captures cached field metadata for the mapping picker.</summary>
        /// <param name="path">Logical path with empty array tokens.</param>
        /// <param name="label">Compact tree caption.</param>
        /// <param name="tooltip">Instructions from the original serialized field.</param>
        /// <param name="type">Value type used by the cell codec.</param>
        /// <param name="collection">Deepest repeated parent, or empty for a single field.</param>
        /// <param name="collectionLabel">Declared collection captions.</param>
        /// <param name="collectionTooltip">Purpose supplied by the collection field.</param>
        internal ExcelField(string path, string label, string tooltip, Type type, string collection, string collectionLabel, string collectionTooltip)
        {
            Path = path;
            Label = label;
            Tooltip = tooltip;
            Type = type;
            Collection = collection;
            CollectionLabel = collectionLabel;
            CollectionTooltip = collectionTooltip;
        }

        #endregion
    }

    /// <summary>Builds a scoped field schema once when a preset context opens.</summary>
    public static class ExcelFieldCatalog
    {
        #region Methods
        #region Schema

        /// <summary>Finds supported serialized fields without depending on existing array elements.</summary>
        /// <param name="root">Typed preset or logical tool document.</param>
        /// <param name="paths">Optional field prefixes belonging to the current group.</param>
        /// <returns>Unique fields in declaration order.</returns>
        public static List<ExcelField> Build(Object root, string[] paths = null)
        {
            List<ExcelField> result = new List<ExcelField>();
            Collect(root.GetType(), string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, paths, result, new HashSet<Type>());
            return result;
        }

        /// <summary>Visits serializable declarations while stopping references and recursive types.</summary>
        /// <param name="type">Current managed settings type.</param>
        /// <param name="prefix">Logical owner path.</param>
        /// <param name="label">Owner captions.</param>
        /// <param name="collection">Current repeated parent.</param>
        /// <param name="collectionLabel">Current collection captions.</param>
        /// <param name="collectionTooltip">Current collection instructions.</param>
        /// <param name="paths">Allowed group fields.</param>
        /// <param name="result">Receives supported leaves.</param>
        /// <param name="ancestors">Types on the current recursion branch.</param>
        private static void Collect(Type type, string prefix, string label, string collection, string collectionLabel, string collectionTooltip, string[] paths,
            List<ExcelField> result, HashSet<Type> ancestors)
        {
            if (!ancestors.Add(type))
                return;
            foreach (FieldInfo field in ExcelPropertyPath.Fields(type))
            {
                string path = prefix + field.Name;
                if (paths != null && !Array.Exists(paths, allowed => path == allowed || path.StartsWith(allowed + ".", StringComparison.Ordinal)
                    || path.StartsWith(allowed + "[]", StringComparison.Ordinal) || allowed.StartsWith(path + ".", StringComparison.Ordinal)))
                    continue;
                Type element = ExcelPropertyPath.ElementType(field.FieldType);
                string caption = label + (field.GetCustomAttribute<InspectorNameAttribute>()?.displayName ?? ObjectNames.NicifyVariableName(field.Name));
                string tooltip = field.GetCustomAttribute<TooltipAttribute>()?.tooltip ?? caption;
                // Scalar lists occupy one cell; complex elements expose reusable child fields.
                if (ExcelValueCodec.Supported(field.FieldType) || element != null && ExcelValueCodec.Supported(element))
                    result.Add(new ExcelField(path, caption, tooltip, field.FieldType, collection, collectionLabel, collectionTooltip));
                else if (element != null)
                    Collect(element, path + "[].", caption + " / ", path + "[]", caption, tooltip, paths, result, ancestors);
                else if (field.FieldType.IsSerializable)
                    Collect(field.FieldType, path + ".", caption + " / ", collection, collectionLabel, collectionTooltip, paths, result, ancestors);
            }
            ancestors.Remove(type);
        }

        #endregion
        #endregion
    }
}
