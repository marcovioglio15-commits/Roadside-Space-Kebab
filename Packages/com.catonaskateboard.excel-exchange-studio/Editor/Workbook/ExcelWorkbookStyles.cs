using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Reuses styles with wrapping disabled without altering unrelated cells.</summary>
    internal sealed class ExcelWorkbookStyles
    {
        #region Fields

        private readonly ZipArchive archive;
        private readonly string path;
        private readonly XDocument document;
        private readonly XElement formats;
        private readonly Dictionary<int, int> unwrapped = new Dictionary<int, int>();

        #endregion
        #region Methods
        #region Lifetime

        /// <summary>Opens the existing style part or creates the standard minimal style table.</summary>
        /// <param name="archive">Writable XLSX package.</param>
        internal ExcelWorkbookStyles(ZipArchive archive)
        {
            // Resolve the workbook's own style part before creating one for a new file.
            this.archive = archive;
            XDocument relations = ExcelOpenXml.Read(archive, "xl/_rels/workbook.xml.rels");
            XElement relation = relations.Root.Elements().FirstOrDefault(entry => ((string)entry.Attribute("Type"))?.EndsWith("/styles", StringComparison.Ordinal) ?? false);
            path = relation != null ? ExcelOpenXml.Resolve("xl/workbook.xml", (string)relation.Attribute("Target")) : "xl/styles.xml";
            document = archive.GetEntry(path) != null ? ExcelOpenXml.Read(archive, path) : Create();
            formats = document.Root.Element(ExcelOpenXml.Main + "cellXfs");
            if (relation != null)
                return;
            relations.Root.Add(new XElement(ExcelOpenXml.Package + "Relationship", new XAttribute("Id", "styles" + Guid.NewGuid().ToString("N")),
                new XAttribute("Type", ExcelOpenXml.Relationships.NamespaceName + "/styles"), new XAttribute("Target", "styles.xml")));
            ExcelOpenXml.Write(archive, "xl/_rels/workbook.xml.rels", relations);
            XDocument content = ExcelOpenXml.Read(archive, "[Content_Types].xml");
            if (!content.Root.Elements().Any(entry => (string)entry.Attribute("PartName") == "/" + path))
                content.Root.Add(new XElement(ExcelOpenXml.Content + "Override", new XAttribute("PartName", "/" + path),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")));
            ExcelOpenXml.Write(archive, "[Content_Types].xml", content);
        }

        /// <summary>Saves appended styles after every managed cell has been assigned.</summary>
        internal void Save()
        {
            // Keep the style count in sync with appended copies.
            formats.SetAttributeValue("count", formats.Elements().Count());
            ExcelOpenXml.Write(archive, path, document);
        }

        #endregion
        #region Formatting

        /// <summary>Clones each base style at most once to disable wrapping and text shrinking.</summary>
        /// <param name="style">Existing cell style index, or empty for the default style.</param>
        /// <returns>A style index with wrapping disabled and the original font, fill and number format.</returns>
        internal int NoWrap(string style)
        {
            // Reuse the original style when it already leaves text at its natural width.
            int index = string.IsNullOrEmpty(style) ? 0 : int.Parse(style, CultureInfo.InvariantCulture);
            if (unwrapped.TryGetValue(index, out int cached))
                return cached;
            XElement original = formats.Elements().ElementAtOrDefault(index)
                ?? throw new FormatException("The template contains a missing cell style. Save it again in Excel and retry.");
            XElement alignment = original.Element(ExcelOpenXml.Main + "alignment");
            if (!Enabled(alignment?.Attribute("wrapText")) && !Enabled(alignment?.Attribute("shrinkToFit")))
                return unwrapped[index] = index;
            // Copy only the affected style so other template cells keep their formatting.
            XElement copy = new XElement(original);
            alignment = copy.Element(ExcelOpenXml.Main + "alignment");
            if (alignment == null)
            {
                alignment = new XElement(ExcelOpenXml.Main + "alignment");
                XElement next = copy.Elements().FirstOrDefault(element => element.Name.LocalName is "protection" or "extLst");
                if (next != null)
                    next.AddBeforeSelf(alignment);
                else
                    copy.Add(alignment);
            }
            alignment.SetAttributeValue("wrapText", "0");
            alignment.SetAttributeValue("shrinkToFit", "0");
            copy.SetAttributeValue("applyAlignment", "1");
            int result = formats.Elements().Count();
            formats.Add(copy);
            unwrapped[index] = result;
            return result;
        }

        /// <summary>Accepts both valid XML spellings of an enabled formatting option.</summary>
        /// <param name="attribute">Optional boolean attribute.</param>
        /// <returns>True for 1 or true.</returns>
        private static bool Enabled(XAttribute attribute)
        {
            // XML booleans allow both numeric and word forms.
            return (string)attribute == "1" || string.Equals((string)attribute, "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Creates the standard Normal style with a readable default font.</summary>
        /// <returns>A valid style part for a new workbook.</returns>
        private static XDocument Create()
        {
            // Supply the standard Normal style for workbooks created without a template.
            return new XDocument(new XElement(ExcelOpenXml.Main + "styleSheet",
                new XElement(ExcelOpenXml.Main + "fonts", new XAttribute("count", 1), new XElement(ExcelOpenXml.Main + "font",
                    new XElement(ExcelOpenXml.Main + "sz", new XAttribute("val", 11)), new XElement(ExcelOpenXml.Main + "name", new XAttribute("val", "Calibri")))),
                new XElement(ExcelOpenXml.Main + "fills", new XAttribute("count", 2),
                    new XElement(ExcelOpenXml.Main + "fill", new XElement(ExcelOpenXml.Main + "patternFill", new XAttribute("patternType", "none"))),
                    new XElement(ExcelOpenXml.Main + "fill", new XElement(ExcelOpenXml.Main + "patternFill", new XAttribute("patternType", "gray125")))),
                new XElement(ExcelOpenXml.Main + "borders", new XAttribute("count", 1), new XElement(ExcelOpenXml.Main + "border")),
                new XElement(ExcelOpenXml.Main + "cellStyleXfs", new XAttribute("count", 1), new XElement(ExcelOpenXml.Main + "xf")),
                new XElement(ExcelOpenXml.Main + "cellXfs", new XAttribute("count", 1), new XElement(ExcelOpenXml.Main + "xf", new XAttribute("xfId", 0))),
                new XElement(ExcelOpenXml.Main + "cellStyles", new XAttribute("count", 1), new XElement(ExcelOpenXml.Main + "cellStyle",
                    new XAttribute("name", "Normal"), new XAttribute("xfId", 0), new XAttribute("builtinId", 0)))));
        }

        #endregion
        #endregion
    }
}
