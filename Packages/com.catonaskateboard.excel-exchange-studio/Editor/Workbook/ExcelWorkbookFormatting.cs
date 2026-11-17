using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Fits exported columns without wrapping values while preserving unrelated template formatting.</summary>
    internal static class ExcelWorkbookFormatting
    {
        #region Methods
        #region Sheet Formatting

        /// <summary>Sizes managed columns using the longest text, subject to the configured outlier limit.</summary>
        /// <param name="archive">Writable XLSX package.</param>
        /// <param name="document">Worksheet currently being written.</param>
        /// <param name="sheet">Managed output cells.</param>
        /// <param name="widthRatio">Longest-to-shortest text ratio limiting width.</param>
        internal static void Apply(ZipArchive archive, XDocument document, ExcelSheet sheet, double widthRatio)
        {
            // Reuse template styles and existing column records.
            ExcelWorkbookStyles styles = new ExcelWorkbookStyles(archive);
            XElement data = document.Root.Element(ExcelOpenXml.Main + "sheetData");
            XElement columns = document.Root.Element(ExcelOpenXml.Main + "cols");
            if (columns == null)
            {
                columns = new XElement(ExcelOpenXml.Main + "cols");
                data.AddBeforeSelf(columns);
            }
            Dictionary<int, XElement> rows = data.Elements().ToDictionary(row => (int)row.Attribute("r"));
            // Ignore blank cells and limit only columns containing unusually long text.
            foreach (IGrouping<int, KeyValuePair<(int Row, int Column), ExcelCell>> group in sheet.Cells.GroupBy(cell => cell.Key.Column))
            {
                double[] lengths = group.Where(cell => cell.Value.Text.Length > 0).Select(cell => LongestLine(cell.Value.Text)).ToArray();
                if (lengths.Length == 0)
                    continue;
                FitColumn(columns, group.Key, Math.Min(255d, Math.Min(lengths.Max(), lengths.Min() * widthRatio) + 3d));
                foreach (KeyValuePair<(int Row, int Column), ExcelCell> entry in group)
                {
                    if (entry.Value.Kind == ExcelCellKind.Blank || !rows.TryGetValue(entry.Key.Row, out XElement row))
                        continue;
                    XElement cell = row.Elements().First(candidate => (string)candidate.Attribute("r") == ExcelAddress.Format(entry.Key.Row, entry.Key.Column));
                    int style = styles.NoWrap((string)cell.Attribute("s"));
                    cell.SetAttributeValue("s", style);
                }
            }
            XElement[] ordered = columns.Elements().OrderBy(column => (int)column.Attribute("min")).ToArray();
            columns.ReplaceNodes(ordered);
            styles.Save();
        }

        /// <summary>Splits template column ranges so unrelated widths and styles stay intact.</summary>
        /// <param name="columns">Worksheet column records.</param>
        /// <param name="index">Managed column.</param>
        /// <param name="width">Width needed by the exported values.</param>
        private static void FitColumn(XElement columns, int index, double width)
        {
            // Preserve the left and right parts of a shared template column range.
            XElement original = columns.Elements().FirstOrDefault(column => (int)column.Attribute("min") <= index && (int)column.Attribute("max") >= index);
            XElement fitted = original != null ? new XElement(original) : new XElement(ExcelOpenXml.Main + "col");
            if (original != null)
            {
                int first = (int)original.Attribute("min");
                int last = (int)original.Attribute("max");
                if (first < index)
                {
                    XElement left = new XElement(original);
                    left.SetAttributeValue("max", index - 1);
                    columns.Add(left);
                }
                if (last > index)
                {
                    XElement right = new XElement(original);
                    right.SetAttributeValue("min", index + 1);
                    columns.Add(right);
                }
                original.Remove();
            }
            fitted.SetAttributeValue("min", index);
            fitted.SetAttributeValue("max", index);
            fitted.SetAttributeValue("width", width);
            fitted.SetAttributeValue("customWidth", "1");
            columns.Add(fitted);
        }

        #endregion
        #region Text Measurement

        /// <summary>Measures the widest explicit line with extra room for wide characters.</summary>
        /// <param name="text">Exported text.</param>
        /// <returns>Approximate Excel character units.</returns>
        private static double LongestLine(string text)
        {
            // Use Excel character units with extra space for wide glyphs.
            return text.Replace("\r", string.Empty).Split('\n').Select(line => line.Sum(character => character > 255 ? 2d : "MW@#".IndexOf(character) >= 0 ? 1.4d : 1d)).DefaultIfEmpty(0d).Max();
        }

        #endregion
        #endregion
    }
}
