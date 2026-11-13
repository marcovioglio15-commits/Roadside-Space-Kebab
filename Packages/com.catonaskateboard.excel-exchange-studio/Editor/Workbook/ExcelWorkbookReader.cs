using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Reads exact XLSX coordinates, typed values and optional table boundaries.</summary>
    public static class ExcelWorkbookReader
    {
        #region Methods
        #region Reading

        /// <summary>Reads worksheet tab names without loading cell values.</summary>
        /// <param name="path">Existing XLSX workbook.</param>
        /// <returns>Worksheet names in workbook order.</returns>
        public static string[] Worksheets(string path)
        {
            if (!string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Choose an .xlsx workbook.");
            using ZipArchive archive = ZipFile.OpenRead(path);
            return ExcelOpenXml.Sheets(archive).Keys.ToArray();
        }

        /// <summary>Loads a workbook without requiring a generated metadata sheet or dimension record.</summary>
        /// <param name="path">Existing XLSX file.</param>
        /// <returns>Sparse workbook contents for preview or conversion.</returns>
        public static ExcelWorkbook Read(string path)
        {
            if (!string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Choose an .xlsx workbook.");
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
            string[] strings = archive.GetEntry("xl/sharedStrings.xml") == null ? Array.Empty<string>()
                : ExcelOpenXml.Read(archive, "xl/sharedStrings.xml").Root.Elements(ExcelOpenXml.Main + "si")
                    .Select(item => string.Concat(item.Descendants(ExcelOpenXml.Main + "t").Select(text => text.Value))).ToArray();
            ExcelWorkbook workbook = new ExcelWorkbook();
            // Actual stored cells define bounds; formatting alone never creates imported rows.
            foreach (KeyValuePair<string, string> entry in ExcelOpenXml.Sheets(archive))
            {
                ExcelSheet sheet = workbook.Sheet(entry.Key);
                XDocument data = ExcelOpenXml.Read(archive, entry.Value);
                foreach (XElement cell in data.Descendants(ExcelOpenXml.Main + "sheetData").Elements(ExcelOpenXml.Main + "row").Elements(ExcelOpenXml.Main + "c"))
                {
                    (int Row, int Column) position = ExcelAddress.Parse((string)cell.Attribute("r"));
                    sheet.Set(position.Row, position.Column, Value(cell, strings));
                }
                sheet.MergedRanges.AddRange(data.Descendants(ExcelOpenXml.Main + "mergeCell").Select(merge => (string)merge.Attribute("ref")));
                ReadTables(archive, entry.Value, sheet);
            }
            return workbook;
        }

        /// <summary>Decodes inline/shared strings and retains formula state separately from cached results.</summary>
        /// <param name="cell">Stored cell element.</param>
        /// <param name="strings">Workbook shared string table.</param>
        /// <returns>A typed value including its template style.</returns>
        private static ExcelCell Value(XElement cell, string[] strings)
        {
            string text = cell.Element(ExcelOpenXml.Main + "v")?.Value ?? string.Empty;
            XElement formulaNode = cell.Element(ExcelOpenXml.Main + "f");
            string formula = formulaNode == null ? string.Empty : formulaNode.Value.Length > 0 ? formulaNode.Value : "(shared formula)";
            string style = (string)cell.Attribute("s") ?? string.Empty;
            switch ((string)cell.Attribute("t"))
            {
                case "s":
                    return new ExcelCell(strings[int.Parse(text, System.Globalization.CultureInfo.InvariantCulture)], ExcelCellKind.Text, formula, style);
                case "inlineStr":
                    return new ExcelCell(string.Concat(cell.Descendants(ExcelOpenXml.Main + "t").Select(part => part.Value)), ExcelCellKind.Text, formula, style);
                case "b":
                    if (text is not ("0" or "1"))
                        throw new FormatException("Invalid Excel boolean at " + (string)cell.Attribute("r"));
                    return new ExcelCell(text == "1" ? "true" : "false", ExcelCellKind.Boolean, formula, style);
                case "e":
                    return new ExcelCell(text, ExcelCellKind.Error, formula, style);
                case "str":
                    return new ExcelCell(text, ExcelCellKind.Text, formula, style);
                default:
                    return new ExcelCell(text, text.Length == 0 ? ExcelCellKind.Blank : ExcelCellKind.Number, formula, style);
            }
        }

        /// <summary>Reads named table bounds through worksheet relationships.</summary>
        /// <param name="archive">Workbook package.</param>
        /// <param name="sheetPath">Worksheet part.</param>
        /// <param name="sheet">Receives table names and ranges.</param>
        private static void ReadTables(ZipArchive archive, string sheetPath, ExcelSheet sheet)
        {
            foreach (KeyValuePair<string, string> entry in ExcelOpenXml.TableParts(archive, sheetPath))
            {
                XElement table = ExcelOpenXml.Read(archive, entry.Value).Root;
                sheet.Tables.Add(entry.Key, (string)table.Attribute("ref"));
                sheet.TableTotals.Add(entry.Key, (int?)table.Attribute("totalsRowCount") ?? 0);
            }
        }

        #endregion
        #endregion
    }
}
