using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Shares Open XML package paths without depending on an Excel installation.</summary>
    internal static class ExcelOpenXml
    {
        #region Fields

        internal static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        internal static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        internal static readonly XNamespace Package = "http://schemas.openxmlformats.org/package/2006/relationships";
        internal static readonly XNamespace Content = "http://schemas.openxmlformats.org/package/2006/content-types";

        #endregion
        #region Methods

        /// <summary>Reads an XML package part without allowing external document entities.</summary>
        /// <param name="archive">Open workbook package.</param>
        /// <param name="path">Package-relative part path.</param>
        /// <returns>The parsed part.</returns>
        internal static XDocument Read(ZipArchive archive, string path)
        {
            using Stream stream = (archive.GetEntry(path) ?? throw new FormatException("Missing workbook part: " + path)).Open();
            using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return XDocument.Load(reader);
        }

        /// <summary>Replaces one XML part while retaining all unrelated ZIP entries.</summary>
        /// <param name="archive">Writable workbook package.</param>
        /// <param name="path">Package-relative part path.</param>
        /// <param name="document">Updated XML content.</param>
        internal static void Write(ZipArchive archive, string path, XDocument document)
        {
            if (archive.Mode == ZipArchiveMode.Update)
                archive.GetEntry(path)?.Delete();
            using Stream stream = archive.CreateEntry(path, CompressionLevel.Optimal).Open();
            document.Save(stream);
        }

        /// <summary>Resolves relative part relationships including rooted targets.</summary>
        /// <param name="owner">Source part path.</param>
        /// <param name="target">Relationship target.</param>
        /// <returns>A normalized ZIP entry path.</returns>
        internal static string Resolve(string owner, string target)
        {
            Uri root = new Uri("https://workbook.local/" + owner);
            return Uri.UnescapeDataString(new Uri(root, target).AbsolutePath.TrimStart('/'));
        }

        /// <summary>Indexes worksheet names to their actual relationship targets.</summary>
        /// <param name="archive">Readable workbook package.</param>
        /// <returns>Exact sheet names and package part paths.</returns>
        internal static Dictionary<string, string> Sheets(ZipArchive archive)
        {
            XDocument workbook = Read(archive, "xl/workbook.xml");
            Dictionary<string, string> targets = Read(archive, "xl/_rels/workbook.xml.rels").Root.Elements(Package + "Relationship")
                .Where(relation => (string)relation.Attribute("TargetMode") != "External")
                .ToDictionary(relation => (string)relation.Attribute("Id"), relation => Resolve("xl/workbook.xml", (string)relation.Attribute("Target")));
            return workbook.Root.Element(Main + "sheets").Elements(Main + "sheet")
                .ToDictionary(sheet => (string)sheet.Attribute("name"), sheet => targets[(string)sheet.Attribute(Relationships + "id")], StringComparer.Ordinal);
        }

        /// <summary>Resolves named table parts belonging to one worksheet.</summary>
        /// <param name="archive">Readable package.</param>
        /// <param name="sheetPath">Worksheet XML part.</param>
        /// <returns>Table names and package part paths.</returns>
        internal static Dictionary<string, string> TableParts(ZipArchive archive, string sheetPath)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            string relationships = sheetPath.Substring(0, sheetPath.LastIndexOf('/') + 1) + "_rels/" + Path.GetFileName(sheetPath) + ".rels";
            if (archive.GetEntry(relationships) == null)
                return result;
            foreach (XElement relation in Read(archive, relationships).Root.Elements(Package + "Relationship"))
                if (((string)relation.Attribute("Type") ?? string.Empty).EndsWith("/table", StringComparison.Ordinal)
                    && (string)relation.Attribute("TargetMode") != "External")
                {
                    string path = Resolve(sheetPath, (string)relation.Attribute("Target"));
                    result.Add((string)Read(archive, path).Root.Attribute("displayName"), path);
                }
            return result;
        }

        #endregion
    }
}
