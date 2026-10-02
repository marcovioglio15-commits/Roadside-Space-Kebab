using System.IO;
using System.Xml;

namespace CatOnASkateboard.AudioStudio.Editor
{
    /// <summary>Resolves the bank destination authored in an FMOD Studio project.</summary>
    public static class FmodBankPaths
    {
        #region Methods

        #region Project Paths

        /// <summary>Reads the project's custom export directory, falling back to FMOD's default Build directory.</summary>
        /// <param name="project">Absolute path of an existing fspro project.</param>
        /// <returns>Absolute bank root before the platform subdirectory.</returns>
        public static string FromProject(string project)
        {
            // Relative export paths are based on the FMOD project, not on Unity's Assets directory.
            string directory = Path.GetDirectoryName(project);
            string workspace = Path.Combine(directory, "Metadata", "Workspace.xml");
            string output = null;
            if (File.Exists(workspace))
            {
                XmlDocument document = new XmlDocument { XmlResolver = null };
                using (XmlReader reader = XmlReader.Create(workspace, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    document.Load(reader);
                output = document.SelectSingleNode("/objects/object[@class='Workspace']/property[@name='builtBanksOutputDirectory']/value")?.InnerText;
            }
            return Path.GetFullPath(Path.Combine(directory, string.IsNullOrWhiteSpace(output) ? "Build" : output));
        }

        #endregion

        #endregion
    }
}
