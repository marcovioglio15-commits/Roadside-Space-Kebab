using System;
using System.Collections.Generic;
using System.Globalization;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Indexes project references once per type and rejects ambiguous labels.</summary>
    [InitializeOnLoad]
    public static class ExcelReferenceCache
    {
        #region Fields

        private static readonly Dictionary<Type, Dictionary<string, List<Object>>> labels = new Dictionary<Type, Dictionary<string, List<Object>>>();

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Invalidates reference labels only when project assets change.</summary>
        static ExcelReferenceCache()
        {
            EditorApplication.projectChanged += labels.Clear;
        }

        #endregion
        #region Conversion

        /// <summary>Resolves an exact GUID, asset path or unique tool label.</summary>
        /// <param name="text">Spreadsheet reference text.</param>
        /// <param name="type">Expected project asset type.</param>
        /// <returns>The matching asset or an actionable conversion exception.</returns>
        public static Object Read(string text, Type type)
        {
            if (text.Length == 0)
                return null;
            if (text.StartsWith("guid:", StringComparison.Ordinal))
            {
                string[] parts = text.Split(':');
                if (parts.Length != 3 || !long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out long identity))
                    throw new FormatException("Use guid:asset-guid:local-id for a stable reference.");
                foreach (Object candidate in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(parts[1])))
                    if (type.IsInstanceOfType(candidate) && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out string _, out long localId) && localId == identity)
                        return candidate;
                throw new FormatException("Referenced asset no longer exists: " + text);
            }
            if (text.StartsWith("Assets/", StringComparison.Ordinal) || text.StartsWith("Packages/", StringComparison.Ordinal))
                return AssetDatabase.LoadAssetAtPath(text, type) ?? throw new FormatException("No matching asset at " + text);
            Dictionary<string, List<Object>> index = Index(type);
            if (!index.TryGetValue(text, out List<Object> found))
                throw new FormatException("Unknown " + type.Name + ": " + text);
            if (found.Count != 1)
                throw new FormatException("Ambiguous " + type.Name + ": " + text + ". Use an asset path or GUID.");
            return found[0];
        }

        /// <summary>Uses an unambiguous tool label or a stable GUID for duplicate labels.</summary>
        /// <param name="asset">Referenced project asset.</param>
        /// <returns>Spreadsheet text that resolves to the same asset.</returns>
        public static string Write(Object asset)
        {
            if (asset == null)
                return string.Empty;
            if (!EditorUtility.IsPersistent(asset))
                throw new FormatException("Scene object references cannot be exported as project assets.");
            string label = StudioPresetActions.ReferenceLabel(asset);
            if (Index(asset.GetType()).TryGetValue(label, out List<Object> matches) && matches.Count == 1)
                return label;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId);
            return "guid:" + guid + ":" + localId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Loads compatible asset labels once, including subassets.</summary>
        /// <param name="type">Expected reference type.</param>
        /// <returns>An exact-label index retaining duplicates for diagnostics.</returns>
        private static Dictionary<string, List<Object>> Index(Type type)
        {
            if (labels.TryGetValue(type, out Dictionary<string, List<Object>> cached))
                return cached;
            Dictionary<string, List<Object>> result = new Dictionary<string, List<Object>>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:" + type.Name))
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if (asset != null && type.IsInstanceOfType(asset))
                    {
                        string label = StudioPresetActions.ReferenceLabel(asset);
                        if (!result.TryGetValue(label, out List<Object> matches))
                            result.Add(label, matches = new List<Object>());
                        matches.Add(asset);
                    }
            return labels[type] = result;
        }

        #endregion
        #endregion
    }
}
