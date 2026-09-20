using System;
using System.IO;
using UnityEditor;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Retains original generated defaults so deleted assets can return with their original GUIDs and file IDs.</summary>
    internal static class PlayerDefaultRecovery
    {
        #region Paths

        private const string backupRoot = PlayerDefaultAssets.ContentRoot + "/Recovery~";

        #endregion

        #region Methods

        /// <summary>Restores only absent default files, preserving existing personalized assets and references.</summary>
        internal static void RestoreMissing()
        {
            // Unity ignores package folders ending in a tilde; backup GUIDs are never imported twice.
            if (!Directory.Exists(backupRoot))
                return;
            bool restored = false;
            foreach (string source in Directory.GetFiles(backupRoot, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(backupRoot, source);
                string destination = Path.Combine(PlayerDefaultAssets.DefaultsRoot, relative);
                if (File.Exists(destination))
                    continue;
                if (destination.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                if (File.Exists(destination + ".meta") && File.Exists(source + ".meta")
                    && File.ReadAllText(destination + ".meta") != File.ReadAllText(source + ".meta"))
                    throw new IOException("A missing default has different metadata: " + destination);
                File.Copy(source, destination);
                if (!File.Exists(destination + ".meta") && File.Exists(source + ".meta"))
                    File.Copy(source + ".meta", destination + ".meta");
                restored = true;
            }
            if (restored)
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>Records each generated asset once, including metadata and prefab-local reference identities.</summary>
        internal static void CaptureMissing()
        {
            // Existing backups remain original defaults even after the live assets are personalized.
            foreach (string source in Directory.GetFiles(PlayerDefaultAssets.DefaultsRoot, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(backupRoot, Path.GetRelativePath(PlayerDefaultAssets.DefaultsRoot, source));
                if (File.Exists(destination))
                    continue;
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source, destination);
            }
        }

        #endregion
    }
}
