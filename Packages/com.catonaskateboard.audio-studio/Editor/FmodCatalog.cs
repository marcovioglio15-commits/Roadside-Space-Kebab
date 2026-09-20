using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace CatOnASkateboard.AudioStudio.Editor
{
    /// <summary>Reads Studio metadata without requiring banks, native plugins or gameplay assemblies.</summary>
    internal static class FmodCatalog
    {
        #region Methods
        #region Reading
        /// <summary>Loads the requested Studio project or the installed integration's bank catalog.</summary>
        /// <param name="connection">Explicit source selected by the preset.</param>
        /// <param name="entries">Destination event list.</param>
        /// <returns>Source description or an actionable parsing error.</returns>
        internal static string Load(AudioConnection connection, List<FmodCatalogEntry> entries)
        {
            // Refresh is explicit; filesystem traversal never runs during ordinary GUI repaints.
            entries.Clear();
            try
            {
                if (connection.UseStudioProject)
                    LoadProject(FmodEditorBridge.ResolvePath(connection.ProjectPath), entries);
                else if (!FmodEditorBridge.ReadCatalog(entries))
                    return "Bank catalogs require the official FMOD integration. A .fspro can be browsed directly.";
                entries.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase));
                return entries.Count + " events loaded.";
            }
            catch (Exception exception)
            {
                entries.Clear();
                return "Catalog: " + exception.GetBaseException().Message;
            }
        }

        /// <summary>Builds paths from Studio event, folder and bank relationships.</summary>
        /// <param name="project">Existing .fspro file.</param>
        /// <param name="entries">Destination catalog.</param>
        private static void LoadProject(string project, List<FmodCatalogEntry> entries)
        {
            // Studio XML is input data, not a source of executable code.
            if (!File.Exists(project) || !string.Equals(Path.GetExtension(project), ".fspro", StringComparison.OrdinalIgnoreCase))
                throw new IOException("Select an existing .fspro file.");
            string metadata = Path.Combine(Path.GetDirectoryName(project), "Metadata");
            Dictionary<string, XElement> folders = ReadObjects(Path.Combine(metadata, "EventFolder"), "EventFolder");
            Dictionary<string, XElement> banks = ReadObjects(Path.Combine(metadata, "Bank"), "Bank");
            foreach (XElement item in ReadObjects(Path.Combine(metadata, "Event"), "Event").Values)
            {
                string folder = Relation(item, "folder").FirstOrDefault();
                List<string> segments = new List<string> { Property(item, "name") };
                HashSet<string> visited = new HashSet<string>();
                while (!string.IsNullOrEmpty(folder) && folders.TryGetValue(folder, out XElement parent))
                {
                    if (!visited.Add(folder))
                        throw new InvalidDataException("Cyclic Studio event folder relationship.");
                    segments.Add(Property(parent, "name"));
                    folder = Relation(parent, "folder").FirstOrDefault();
                }
                segments.Reverse();
                FmodCatalogEntry entry = new FmodCatalogEntry { Path = "event:/" + string.Join("/", segments), Guid = (string)item.Attribute("id") };
                foreach (string bank in Relation(item, "banks"))
                    if (banks.TryGetValue(bank, out XElement bankObject))
                        entry.Banks.Add(Property(bankObject, "name"));
                entries.Add(entry);
            }

            // Enrich matching GUIDs with built-bank details without mixing another project's events.
            List<FmodCatalogEntry> built = new List<FmodCatalogEntry>();
            if (!FmodEditorBridge.ReadCatalog(built))
                return;
            foreach (FmodCatalogEntry entry in entries)
            {
                FmodCatalogEntry match = built.Find(candidate => SameGuid(candidate.Guid, entry.Guid));
                if (match == null)
                    continue;
                entry.Source = match.Source;
                entry.Spatial = match.Spatial;
                entry.OneShot = match.OneShot;
                entry.Parameters.AddRange(match.Parameters);
            }
        }

        /// <summary>Reads only the requested Studio object class from a metadata directory.</summary>
        /// <param name="directory">Metadata directory for a single object category.</param>
        /// <param name="className">Studio object class to extract.</param>
        /// <returns>Objects indexed by stable Studio GUID.</returns>
        private static Dictionary<string, XElement> ReadObjects(string directory, string className)
        {
            // Disable external XML resources and bound each metadata file's parsed size.
            Dictionary<string, XElement> result = new Dictionary<string, XElement>();
            if (!Directory.Exists(directory))
                return result;
            XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16000000 };
            foreach (string path in Directory.EnumerateFiles(directory, "*.xml"))
                using (XmlReader reader = XmlReader.Create(path, settings))
                    foreach (XElement item in XDocument.Load(reader).Descendants("object"))
                        if ((string)item.Attribute("class") == className && item.Attribute("id") != null)
                            result[(string)item.Attribute("id")] = item;
            return result;
        }
        #endregion

        #region XML Values
        /// <summary>Reads one named Studio property.</summary>
        /// <param name="item">Studio object element.</param>
        /// <param name="name">Requested property.</param>
        /// <returns>Property text or an empty string.</returns>
        private static string Property(XElement item, string name)
        {
            // Missing optional metadata remains empty rather than fabricating a value.
            return item.Elements("property").FirstOrDefault(property => (string)property.Attribute("name") == name)?.Element("value")?.Value ?? string.Empty;
        }

        /// <summary>Enumerates destinations of one Studio relationship.</summary>
        /// <param name="item">Studio object element.</param>
        /// <param name="name">Relationship name.</param>
        /// <returns>Related object GUIDs.</returns>
        private static IEnumerable<string> Relation(XElement item, string name)
        {
            // Relationship targets can contain more than one assigned bank.
            return item.Elements("relationship").Where(relation => (string)relation.Attribute("name") == name)
                .SelectMany(relation => relation.Elements("destination")).Select(destination => destination.Value);
        }

        /// <summary>Compares metadata and SDK GUID formats without relying on casing or braces.</summary>
        /// <param name="left">SDK or Studio identity.</param>
        /// <param name="right">SDK or Studio identity.</param>
        /// <returns>Whether both values represent the same GUID.</returns>
        private static bool SameGuid(string left, string right)
        {
            // Unparseable values cannot silently attach the wrong bank event.
            return Guid.TryParse(left, out Guid first) && Guid.TryParse(right, out Guid second) && first == second;
        }
        #endregion
        #endregion
    }

    /// <summary>Caches one event's metadata for GUI selection and preview.</summary>
    internal sealed class FmodCatalogEntry
    {
        #region Fields
        internal string Path;
        internal string Guid;
        internal bool Spatial;
        internal bool OneShot;
        internal object Source;
        internal readonly List<string> Banks = new List<string>();
        internal readonly List<FmodCatalogParameter> Parameters = new List<FmodCatalogParameter>();
        #endregion
    }

    /// <summary>Caches the range and scope of one built-bank parameter.</summary>
    internal sealed class FmodCatalogParameter
    {
        #region Fields
        internal string Name;
        internal float Minimum;
        internal float Maximum;
        internal float Default;
        internal bool Global;
        #endregion
    }
}
