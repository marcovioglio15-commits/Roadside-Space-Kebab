using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Keeps unapplied asset proposals outside project assets, using persistent Unity references.</summary>
    internal static class StudioAssetDraftStore
    {
        #region Storage

        private const string directory = "UserSettings/StudioAssetDrafts";

        /// <summary>Stores stable editor JSON rather than process-specific object identifiers.</summary>
        [Serializable]
        internal sealed class Snapshot
        {
            [Tooltip("Storage schema used to reject unsupported recovery files.")]
            public int Version = 1;
            [Tooltip("Source content captured before editing.")]
            public string Source;
            [Tooltip("Initial detached content used to detect pending edits.")]
            public string Baseline;
            [Tooltip("Unapplied content with persistent asset GUID references.")]
            public string Proposal;
        }

        #endregion
        #region Methods
        #region Identity

        /// <summary>Identifies the exact main asset or subasset independently of its current file name.</summary>
        /// <param name="source">Persistent asset opened by a tool.</param>
        /// <returns>Safe per-asset recovery key, or an empty key for an unsaved object.</returns>
        internal static string Key(ScriptableObject source)
        {
            return source != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long local)
                ? guid + "_" + local.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        /// <summary>Rejects invalid keys before constructing a recovery path.</summary>
        /// <param name="key">GUID and local file identifier created by this store.</param>
        /// <returns>Project-local recovery path.</returns>
        private static string PathFor(string key)
        {
            if (string.IsNullOrEmpty(key) || key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || key.Contains(".."))
                throw new IOException("The asset has no valid recovery identity.");
            return Path.Combine(directory, key + ".json");
        }

        #endregion
        #region Persistence

        /// <summary>Reads a retained proposal without replacing malformed or newer data.</summary>
        /// <param name="key">Exact source identity.</param>
        /// <returns>Stored proposal, or null when this asset has no unfinished edit.</returns>
        internal static Snapshot Load(string key)
        {
            string path = PathFor(key);
            if (!File.Exists(path))
                return null;
            Snapshot snapshot = JsonUtility.FromJson<Snapshot>(File.ReadAllText(path));
            if (snapshot == null || snapshot.Version != 1 || string.IsNullOrEmpty(snapshot.Source)
                || string.IsNullOrEmpty(snapshot.Baseline) || string.IsNullOrEmpty(snapshot.Proposal))
                throw new IOException("The retained asset proposal is invalid or uses an unsupported version.");
            return snapshot;
        }

        /// <summary>Replaces a recovery record atomically after a real proposal edit.</summary>
        /// <param name="key">Exact source identity.</param>
        /// <param name="snapshot">Complete proposal and its conflict baselines.</param>
        internal static void Save(string key, Snapshot snapshot)
        {
            // An interrupted write leaves the previous complete record available.
            string path = PathFor(key);
            Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(snapshot));
            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }

        /// <summary>Removes recovery data only after an explicit discard or a successful source write.</summary>
        /// <param name="key">Exact source identity.</param>
        internal static void Delete(string key)
        {
            if (!string.IsNullOrEmpty(key))
                File.Delete(PathFor(key));
        }

        #endregion
        #endregion
    }
}
