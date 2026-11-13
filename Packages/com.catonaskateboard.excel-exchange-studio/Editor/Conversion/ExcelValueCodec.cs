using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Converts spreadsheet values without changing culture, ranges or asset identities.</summary>
    public static class ExcelValueCodec
    {
        #region Methods
        #region Types

        /// <summary>Recognizes values represented by one spreadsheet cell.</summary>
        /// <param name="type">Declared field or list element type.</param>
        /// <returns>True when a symmetric cell conversion is available.</returns>
        public static bool Supported(Type type)
        {
            return type.IsEnum || type.IsPrimitive && type != typeof(IntPtr) && type != typeof(UIntPtr)
                || type == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(type)
                || type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4)
                || type == typeof(Vector2Int) || type == typeof(Vector3Int) || type == typeof(Color)
                || type == typeof(Color32) || type == typeof(Quaternion) || type == typeof(LayerMask);
        }

        /// <summary>Preserves native numeric and boolean types when exporting scalar values.</summary>
        /// <param name="value">Typed field value.</param>
        /// <param name="type">Declared field type.</param>
        /// <param name="separator">List separator.</param>
        /// <returns>A typed workbook cell.</returns>
        public static ExcelCell Cell(object value, Type type, string separator = ";")
        {
            if (value is float single && !float.IsFinite(single) || value is double number && !double.IsFinite(number))
                throw new FormatException("Use a finite number before exporting " + type.Name + ".");
            return new ExcelCell(Format(value, type, separator), type == typeof(bool) ? ExcelCellKind.Boolean
                : type.IsPrimitive && type != typeof(char) || type == typeof(decimal) ? ExcelCellKind.Number : ExcelCellKind.Text);
        }

        #endregion
        #region Parsing

        /// <summary>Reads a validated typed value, including complete lists stored in one cell.</summary>
        /// <param name="cell">Workbook value and optional formula.</param>
        /// <param name="type">Expected serialized type.</param>
        /// <param name="separator">List separator.</param>
        /// <param name="formulaResults">Whether cached formula results may be consumed.</param>
        /// <returns>The converted typed value.</returns>
        public static object Read(ExcelCell cell, Type type, string separator, bool formulaResults)
        {
            if (cell.Kind == ExcelCellKind.Error)
                throw new FormatException("Excel error: " + cell.Text);
            if (cell.Formula.Length > 0 && (!formulaResults || cell.Kind == ExcelCellKind.Blank))
                throw new FormatException(formulaResults ? "Recalculate and save this formula in Excel first." : "Formula results are disabled for this import.");
            Type element = ExcelPropertyPath.ElementType(type);
            if (element != null)
            {
                List<object> values = new List<object>();
                foreach (string token in Split(cell.Text, separator))
                    values.Add(Parse(token, element));
                return ExcelPropertyPath.Collection(type, values);
            }
            return Parse(cell.Text, type);
        }

        /// <summary>Converts one scalar while rejecting non-finite or ambiguous input.</summary>
        /// <param name="text">Exact cell text or one decoded list item.</param>
        /// <param name="type">Declared scalar type.</param>
        /// <returns>The converted value.</returns>
        private static object Parse(string text, Type type)
        {
            // Text and asset references retain their exact identities before numeric conversion.
            if (type == typeof(string))
                return text;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return ExcelReferenceCache.Read(text.Trim(), type);
            if (type == typeof(bool))
                return text.Trim().ToLowerInvariant() switch
                {
                    "true" or "1" => true,
                    "false" or "0" => false,
                    _ => throw new FormatException("Use an Excel boolean, true/false or 1/0.")
                };
            if (type.IsEnum)
            {
                object value = Enum.Parse(type, text, false);
                if (!type.IsDefined(typeof(FlagsAttribute), false) && !Enum.IsDefined(type, value))
                    throw new FormatException("Unknown " + type.Name + ": " + text);
                return value;
            }
            // Scalar conversions use the workbook's invariant numeric representation.
            if (type.IsPrimitive || type == typeof(decimal))
            {
                object value = Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
                if (value is float single && !float.IsFinite(single) || value is double number && !double.IsFinite(number))
                    throw new FormatException("Use a finite number.");
                return value;
            }
            if (type == typeof(LayerMask))
                return (LayerMask)int.Parse(text, CultureInfo.InvariantCulture);
            if ((type == typeof(Color) || type == typeof(Color32)) && text.StartsWith("#", StringComparison.Ordinal)
                && ColorUtility.TryParseHtmlString(text, out Color html))
                return type == typeof(Color32) ? (object)(Color32)html : html;
            string[] parts = text.Split('|');
            float[] components = Array.ConvertAll(parts, part => float.Parse(part, NumberStyles.Float, CultureInfo.InvariantCulture));
            if (Array.Exists(components, component => !float.IsFinite(component)))
                throw new FormatException("Use finite components separated by |.");
            // Component counts stay explicit; integer vectors never round decimal input.
            return type.Name switch
            {
                nameof(Vector2) when components.Length == 2 => new Vector2(components[0], components[1]),
                nameof(Vector3) when components.Length == 3 => new Vector3(components[0], components[1], components[2]),
                nameof(Vector4) when components.Length == 4 => new Vector4(components[0], components[1], components[2], components[3]),
                nameof(Quaternion) when components.Length == 4 => new Quaternion(components[0], components[1], components[2], components[3]),
                nameof(Color) when components.Length == 4 => new Color(components[0], components[1], components[2], components[3]),
                nameof(Color32) when components.Length == 4 => new Color32(byte.Parse(parts[0], CultureInfo.InvariantCulture), byte.Parse(parts[1], CultureInfo.InvariantCulture),
                    byte.Parse(parts[2], CultureInfo.InvariantCulture), byte.Parse(parts[3], CultureInfo.InvariantCulture)),
                nameof(Vector2Int) when components.Length == 2 => new Vector2Int(int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture)),
                nameof(Vector3Int) when components.Length == 3 => new Vector3Int(int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture), int.Parse(parts[2], CultureInfo.InvariantCulture)),
                _ => throw new FormatException("Use " + type.Name + " components separated by |.")
            };
        }

        #endregion
        #region Formatting

        /// <summary>Formats values for spreadsheets and preview without rounding stored numbers.</summary>
        /// <param name="value">Current typed value.</param>
        /// <param name="type">Declared type.</param>
        /// <param name="separator">List separator.</param>
        /// <returns>A symmetric invariant representation.</returns>
        public static string Format(object value, Type type, string separator = ";")
        {
            if (value == null)
                return string.Empty;
            Type element = ExcelPropertyPath.ElementType(type);
            if (element != null)
            {
                List<string> parts = new List<string>();
                foreach (object item in (IList)value)
                {
                    string text = Format(item, element, separator);
                    parts.Add(text.Length == 0 || text.Contains(separator) || text.Contains('"') || text.Contains('\n')
                        ? "\"" + text.Replace("\"", "\"\"") + "\"" : text);
                }
                return string.Join(separator, parts);
            }
            return value switch
            {
                UnityEngine.Object asset => ExcelReferenceCache.Write(asset),
                bool boolean => boolean ? "true" : "false",
                Vector2 vector => Components(vector.x, vector.y),
                Vector3 vector => Components(vector.x, vector.y, vector.z),
                Vector4 vector => Components(vector.x, vector.y, vector.z, vector.w),
                Vector2Int vector => Components(vector.x, vector.y),
                Vector3Int vector => Components(vector.x, vector.y, vector.z),
                Quaternion rotation => Components(rotation.x, rotation.y, rotation.z, rotation.w),
                Color color => Components(color.r, color.g, color.b, color.a),
                Color32 color => Components(color.r, color.g, color.b, color.a),
                LayerMask mask => mask.value.ToString(CultureInfo.InvariantCulture),
                float number => number.ToString("R", CultureInfo.InvariantCulture),
                double number => number.ToString("R", CultureInfo.InvariantCulture),
                IFormattable formatted => formatted.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
        }

        /// <summary>Formats vector components with an unambiguous separator.</summary>
        /// <param name="values">Ordered numeric components.</param>
        /// <returns>Invariant component text.</returns>
        private static string Components(params float[] values)
        {
            return string.Join("|", Array.ConvertAll(values, value => value.ToString("R", CultureInfo.InvariantCulture)));
        }

        /// <summary>Decodes quoted list cells and retains separators inside quoted values.</summary>
        /// <param name="text">Whole cell content.</param>
        /// <param name="separator">Configured non-empty list delimiter.</param>
        /// <returns>Decoded list items, or no items for an empty cell.</returns>
        private static List<string> Split(string text, string separator)
        {
            if (string.IsNullOrEmpty(separator))
                throw new FormatException("Choose a non-empty list separator.");
            List<string> result = new List<string>();
            if (text.Length == 0)
                return result;
            StringBuilder item = new StringBuilder();
            bool quoted = false;
            for (int index = 0; index < text.Length; index++)
                if (text[index] == '"')
                {
                    if (quoted && index + 1 < text.Length && text[index + 1] == '"')
                    {
                        item.Append('"');
                        index++;
                    }
                    else
                        quoted = !quoted;
                }
                else if (!quoted && index + separator.Length <= text.Length
                    && string.CompareOrdinal(text, index, separator, 0, separator.Length) == 0)
                {
                    result.Add(item.ToString());
                    item.Clear();
                    index += separator.Length - 1;
                }
                else
                    item.Append(text[index]);
            if (quoted)
                throw new FormatException("Close the quoted list value.");
            result.Add(item.ToString());
            return result;
        }

        #endregion
        #endregion
    }
}
