using System;
using System.Collections.Generic;
using System.Globalization;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Retains the native spreadsheet value family, including formula errors.</summary>
    public enum ExcelCellKind { Blank, Text, Number, Boolean, Error }

    /// <summary>Stores one sparse worksheet cell without loading an Excel application.</summary>
    public readonly struct ExcelCell
    {
        #region Fields

        public readonly string Text;
        public readonly ExcelCellKind Kind;
        public readonly string Formula;
        public readonly string Style;

        #endregion
        #region Methods

        /// <summary>Captures a value together with template styling and optional formula.</summary>
        /// <param name="text">Invariant value text.</param>
        /// <param name="kind">Native cell type.</param>
        /// <param name="formula">Formula without the leading equals sign.</param>
        /// <param name="style">Template style index.</param>
        public ExcelCell(string text, ExcelCellKind kind = ExcelCellKind.Text, string formula = "", string style = "")
        {
            Text = text ?? string.Empty;
            Kind = kind;
            Formula = formula ?? string.Empty;
            Style = style ?? string.Empty;
        }

        #endregion
    }

    /// <summary>Stores only existing cells and declared table boundaries.</summary>
    public sealed class ExcelSheet
    {
        #region Fields

        public readonly string Name;
        public readonly Dictionary<(int Row, int Column), ExcelCell> Cells = new Dictionary<(int, int), ExcelCell>();
        public readonly Dictionary<string, string> Tables = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> TableTotals = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly List<string> MergedRanges = new List<string>();
        public int LastRow;
        public int LastColumn;

        #endregion
        #region Methods

        /// <summary>Names one sparse worksheet.</summary>
        /// <param name="name">Exact sheet name.</param>
        public ExcelSheet(string name)
        {
            Name = name;
        }

        /// <summary>Reads a cell without treating missing values as numeric zero.</summary>
        /// <param name="row">One-based row.</param>
        /// <param name="column">One-based column.</param>
        /// <returns>The stored value or an explicit blank.</returns>
        public ExcelCell Get(int row, int column)
        {
            return Cells.TryGetValue((row, column), out ExcelCell cell) ? cell : new ExcelCell(string.Empty, ExcelCellKind.Blank);
        }

        /// <summary>Detects merged cells before a mapping can read or overwrite an ambiguous value.</summary>
        /// <param name="row">One-based row.</param>
        /// <param name="column">One-based column.</param>
        /// <returns>True when a merge covers this cell.</returns>
        public bool IsMerged(int row, int column)
        {
            foreach (string range in MergedRanges)
            {
                string[] corners = range.Split(':');
                (int Row, int Column) first = ExcelAddress.Parse(corners[0]);
                (int Row, int Column) last = ExcelAddress.Parse(corners[corners.Length - 1]);
                if (row >= first.Row && row <= last.Row && column >= first.Column && column <= last.Column)
                    return true;
            }
            return false;
        }

        /// <summary>Stores an output value or an explicit instruction to clear managed contents.</summary>
        /// <param name="row">One-based row.</param>
        /// <param name="column">One-based column.</param>
        /// <param name="cell">Typed output value.</param>
        public void Set(int row, int column, ExcelCell cell)
        {
            Cells[(row, column)] = cell;
            LastRow = Math.Max(LastRow, row);
            LastColumn = Math.Max(LastColumn, column);
        }

        #endregion
    }

    /// <summary>Shares sparse sheets between import, export and layout preview.</summary>
    public sealed class ExcelWorkbook
    {
        #region Fields

        public readonly Dictionary<string, ExcelSheet> Sheets = new Dictionary<string, ExcelSheet>(StringComparer.Ordinal);

        #endregion
        #region Methods

        /// <summary>Finds or creates a sheet in declaration order.</summary>
        /// <param name="name">Exact worksheet name.</param>
        /// <returns>The sparse worksheet.</returns>
        public ExcelSheet Sheet(string name)
        {
            if (!Sheets.TryGetValue(name, out ExcelSheet sheet))
                Sheets.Add(name, sheet = new ExcelSheet(name));
            return sheet;
        }

        #endregion
    }

    /// <summary>Converts one-based grid coordinates to validated Excel addresses.</summary>
    public static class ExcelAddress
    {
        #region Methods

        /// <summary>Checks an in-progress address without interrupting the mapping editor.</summary>
        /// <param name="address">Editable cell address.</param>
        /// <param name="position">Receives a valid one-based coordinate.</param>
        /// <returns>True when the address is usable.</returns>
        public static bool TryParse(string address, out (int Row, int Column) position)
        {
            try
            {
                position = Parse(address);
                return true;
            }
            catch (Exception exception) when (exception is FormatException or OverflowException)
            {
                position = default;
                return false;
            }
        }

        /// <summary>Reads an A1 address within Excel's worksheet limits.</summary>
        /// <param name="address">One cell address without a sheet prefix.</param>
        /// <returns>One-based row and column.</returns>
        public static (int Row, int Column) Parse(string address)
        {
            string value = (address ?? string.Empty).Replace("$", string.Empty).ToUpperInvariant();
            int column = 0;
            int index = 0;
            while (index < value.Length && value[index] >= 'A' && value[index] <= 'Z')
                column = checked(column * 26 + value[index++] - 'A' + 1);
            if (column < 1 || column > 16384 || !int.TryParse(value.Substring(index), NumberStyles.None, CultureInfo.InvariantCulture, out int row)
                || row < 1 || row > 1048576)
                throw new FormatException("Choose a cell between A1 and XFD1048576: " + address);
            return (row, column);
        }

        /// <summary>Formats a one-based grid coordinate.</summary>
        /// <param name="row">Worksheet row.</param>
        /// <param name="column">Worksheet column.</param>
        /// <returns>The corresponding A1 address.</returns>
        public static string Format(int row, int column)
        {
            string letters = string.Empty;
            for (int value = column; value > 0; value = (value - 1) / 26)
                letters = (char)('A' + (value - 1) % 26) + letters;
            return letters + row.ToString(CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
