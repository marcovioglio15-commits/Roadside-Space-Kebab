using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Writes managed cells atomically while preserving unrelated template content.</summary>
    public static class ExcelWorkbookWriter
    {
        #region Methods
        #region Transaction

        /// <summary>Writes a new workbook or updates a template copy before replacing the destination.</summary>
        /// <param name="path">Destination XLSX path.</param>
        /// <param name="workbook">Cells explicitly managed by the export plan.</param>
        /// <param name="template">Optional existing workbook to preserve.</param>
        public static void Write(string path, ExcelWorkbook workbook, string template = "")
        {
            if (!string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Choose an .xlsx destination.");
            string destination = Path.GetFullPath(path);
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".xlsx";
            try
            {
                // Work in the same directory so final replacement is a single filesystem operation.
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                if (!string.IsNullOrEmpty(template))
                    File.Copy(template, temporary);
                else
                    Create(temporary);
                using (ZipArchive archive = ZipFile.Open(temporary, ZipArchiveMode.Update))
                {
                    Dictionary<string, string> sheets = ExcelOpenXml.Sheets(archive);
                    foreach (ExcelSheet sheet in workbook.Sheets.Values)
                    {
                        if (!sheets.TryGetValue(sheet.Name, out string sheetPath))
                        {
                            sheetPath = AddSheet(archive, sheet.Name);
                            sheets.Add(sheet.Name, sheetPath);
                        }
                        WriteSheet(archive, sheetPath, sheet);
                    }
                    XDocument book = ExcelOpenXml.Read(archive, "xl/workbook.xml");
                    book.Root.Element(ExcelOpenXml.Main + "calcPr")?.Remove();
                    book.Root.Add(new XElement(ExcelOpenXml.Main + "calcPr", new XAttribute("fullCalcOnLoad", "1")));
                    ExcelOpenXml.Write(archive, "xl/workbook.xml", book);
                }
                // Confirm every written value before the previous workbook can be replaced.
                ExcelWorkbook verified = ExcelWorkbookReader.Read(temporary);
                foreach (ExcelSheet sheet in workbook.Sheets.Values)
                    foreach (KeyValuePair<(int Row, int Column), ExcelCell> cell in sheet.Cells)
                    {
                        ExcelCell actual = verified.Sheets[sheet.Name].Get(cell.Key.Row, cell.Key.Column);
                        if (actual.Text != cell.Value.Text || actual.Formula != cell.Value.Formula)
                            throw new IOException("Workbook verification failed at " + sheet.Name + "!" + ExcelAddress.Format(cell.Key.Row, cell.Key.Column));
                    }
                if (File.Exists(destination))
                    File.Replace(temporary, destination, null);
                else
                    File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        #endregion
        #region Cells

        /// <summary>Updates only explicit output cells, retaining all other worksheet XML.</summary>
        /// <param name="archive">Writable workbook package.</param>
        /// <param name="path">Worksheet part path.</param>
        /// <param name="sheet">Managed sparse output cells.</param>
        private static void WriteSheet(ZipArchive archive, string path, ExcelSheet sheet)
        {
            XDocument document = ExcelOpenXml.Read(archive, path);
            XElement data = document.Root.Element(ExcelOpenXml.Main + "sheetData");
            Dictionary<int, XElement> rows = data.Elements(ExcelOpenXml.Main + "row").ToDictionary(row => (int)row.Attribute("r"));
            foreach (KeyValuePair<(int Row, int Column), ExcelCell> entry in sheet.Cells.OrderBy(cell => cell.Key.Row).ThenBy(cell => cell.Key.Column))
            {
                if (!rows.TryGetValue(entry.Key.Row, out XElement row))
                {
                    row = new XElement(ExcelOpenXml.Main + "row", new XAttribute("r", entry.Key.Row));
                    XElement next = data.Elements(ExcelOpenXml.Main + "row").FirstOrDefault(candidate => (int)candidate.Attribute("r") > entry.Key.Row);
                    if (next != null)
                        next.AddBeforeSelf(row);
                    else
                        data.Add(row);
                    rows.Add(entry.Key.Row, row);
                }
                string address = ExcelAddress.Format(entry.Key.Row, entry.Key.Column);
                XElement cell = row.Elements(ExcelOpenXml.Main + "c").FirstOrDefault(candidate => (string)candidate.Attribute("r") == address);
                if (cell == null)
                {
                    cell = new XElement(ExcelOpenXml.Main + "c", new XAttribute("r", address));
                    XElement next = row.Elements(ExcelOpenXml.Main + "c").FirstOrDefault(candidate => ExcelAddress.Parse((string)candidate.Attribute("r")).Column > entry.Key.Column);
                    if (next != null)
                        next.AddBeforeSelf(cell);
                    else
                        row.Add(cell);
                }
                // Retain the original style; new rows use the anchor's style when supplied.
                if (cell.Attribute("s") == null && entry.Value.Style.Length > 0)
                    cell.SetAttributeValue("s", entry.Value.Style);
                cell.SetAttributeValue("t", null);
                cell.RemoveNodes();
                if (entry.Value.Formula.Length > 0)
                    cell.Add(new XElement(ExcelOpenXml.Main + "f", entry.Value.Formula.TrimStart('=')));
                else
                    switch (entry.Value.Kind)
                    {
                        case ExcelCellKind.Blank:
                            break;
                        case ExcelCellKind.Boolean:
                            cell.SetAttributeValue("t", "b");
                            cell.Add(new XElement(ExcelOpenXml.Main + "v", entry.Value.Text == "true" ? "1" : "0"));
                            break;
                        case ExcelCellKind.Number:
                            cell.Add(new XElement(ExcelOpenXml.Main + "v", entry.Value.Text));
                            break;
                        default:
                            cell.SetAttributeValue("t", "inlineStr");
                            cell.Add(new XElement(ExcelOpenXml.Main + "is", new XElement(ExcelOpenXml.Main + "t",
                                new XAttribute(XNamespace.Xml + "space", "preserve"), entry.Value.Text)));
                            break;
                    }
            }
            // Excel can derive the extent from cells, avoiding stale template dimensions.
            document.Root.Element(ExcelOpenXml.Main + "dimension")?.Remove();
            ExcelOpenXml.Write(archive, path, document);
            foreach (KeyValuePair<string, string> entry in ExcelOpenXml.TableParts(archive, path))
                if (sheet.Tables.TryGetValue(entry.Key, out string range))
                {
                    XDocument table = ExcelOpenXml.Read(archive, entry.Value);
                    table.Root.SetAttributeValue("ref", range);
                    table.Root.Element(ExcelOpenXml.Main + "autoFilter")?.SetAttributeValue("ref", range);
                    ExcelOpenXml.Write(archive, entry.Value, table);
                }
        }

        #endregion
        #region Package

        /// <summary>Creates the minimal public XLSX package parts.</summary>
        /// <param name="path">New temporary workbook path.</param>
        private static void Create(string path)
        {
            using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
            ExcelOpenXml.Write(archive, "[Content_Types].xml", new XDocument(new XElement(ExcelOpenXml.Content + "Types",
                new XElement(ExcelOpenXml.Content + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ExcelOpenXml.Content + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(ExcelOpenXml.Content + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")))));
            ExcelOpenXml.Write(archive, "_rels/.rels", new XDocument(new XElement(ExcelOpenXml.Package + "Relationships",
                new XElement(ExcelOpenXml.Package + "Relationship", new XAttribute("Id", "rId1"),
                    new XAttribute("Type", ExcelOpenXml.Relationships.NamespaceName + "/officeDocument"), new XAttribute("Target", "xl/workbook.xml")))));
            ExcelOpenXml.Write(archive, "xl/workbook.xml", new XDocument(new XElement(ExcelOpenXml.Main + "workbook",
                new XAttribute(XNamespace.Xmlns + "r", ExcelOpenXml.Relationships), new XElement(ExcelOpenXml.Main + "sheets"))));
            ExcelOpenXml.Write(archive, "xl/_rels/workbook.xml.rels", new XDocument(new XElement(ExcelOpenXml.Package + "Relationships")));
        }

        /// <summary>Adds one worksheet and its package relationships.</summary>
        /// <param name="archive">Writable XLSX package.</param>
        /// <param name="name">Requested exact sheet name.</param>
        /// <returns>The new worksheet part path.</returns>
        private static string AddSheet(ZipArchive archive, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 31 || name.IndexOfAny(new[] { '[', ']', ':', '*', '?', '/', '\\' }) >= 0)
                throw new FormatException("Choose a valid Excel sheet name: " + name);
            XDocument workbook = ExcelOpenXml.Read(archive, "xl/workbook.xml");
            XElement sheets = workbook.Root.Element(ExcelOpenXml.Main + "sheets");
            int identity = sheets.Elements().Select(sheet => (int)sheet.Attribute("sheetId")).DefaultIfEmpty(0).Max() + 1;
            string path = "xl/worksheets/exchange" + identity.ToString(CultureInfo.InvariantCulture) + ".xml";
            XDocument relationships = ExcelOpenXml.Read(archive, "xl/_rels/workbook.xml.rels");
            string relation = "exchange" + Guid.NewGuid().ToString("N");
            relationships.Root.Add(new XElement(ExcelOpenXml.Package + "Relationship", new XAttribute("Id", relation),
                new XAttribute("Type", ExcelOpenXml.Relationships.NamespaceName + "/worksheet"), new XAttribute("Target", path.Substring(3))));
            sheets.Add(new XElement(ExcelOpenXml.Main + "sheet", new XAttribute("name", name), new XAttribute("sheetId", identity), new XAttribute(ExcelOpenXml.Relationships + "id", relation)));
            XDocument content = ExcelOpenXml.Read(archive, "[Content_Types].xml");
            content.Root.Add(new XElement(ExcelOpenXml.Content + "Override", new XAttribute("PartName", "/" + path), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
            ExcelOpenXml.Write(archive, "xl/workbook.xml", workbook);
            ExcelOpenXml.Write(archive, "xl/_rels/workbook.xml.rels", relationships);
            ExcelOpenXml.Write(archive, "[Content_Types].xml", content);
            ExcelOpenXml.Write(archive, path, new XDocument(new XElement(ExcelOpenXml.Main + "worksheet", new XElement(ExcelOpenXml.Main + "sheetData"))));
            return path;
        }

        #endregion
        #endregion
    }
}
