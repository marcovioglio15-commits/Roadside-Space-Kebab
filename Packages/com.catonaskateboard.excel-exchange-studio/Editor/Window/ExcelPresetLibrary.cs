using System;
using System.Collections.Generic;
using System.IO;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Finds configurations for one exact source and remembers the last edited mapping.</summary>
    internal static class ExcelPresetLibrary
    {
        #region Methods

        /// <summary>Loads only configurations linked to the current asset and logical scope.</summary>
        /// <param name="context">Current tool selection.</param>
        /// <returns>Compatible saved configurations sorted by name.</returns>
        internal static ExcelExchangePreset[] Find(StudioPresetContext context)
        {
            List<ExcelExchangePreset> result = new List<ExcelExchangePreset>();
            foreach (string guid in AssetDatabase.FindAssets("t:ExcelExchangePreset"))
            {
                ExcelExchangePreset preset = AssetDatabase.LoadAssetAtPath<ExcelExchangePreset>(AssetDatabase.GUIDToAssetPath(guid));
                if (preset != null && preset.Target == context.Source && preset.Scope == context.Scope)
                    result.Add(preset);
            }
            result.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
            return result.ToArray();
        }

        /// <summary>Reads the last compatible asset without sharing state between projects.</summary>
        /// <param name="context">Exact destination context.</param>
        /// <returns>The remembered configuration or null.</returns>
        internal static ExcelExchangePreset Last(StudioPresetContext context)
        {
            return AssetDatabase.LoadAssetAtPath<ExcelExchangePreset>(AssetDatabase.GUIDToAssetPath(EditorPrefs.GetString(Key(context), string.Empty)));
        }

        /// <summary>Remembers the configuration after an explicit selection or edit.</summary>
        /// <param name="context">Exact destination context.</param>
        /// <param name="preset">Saved configuration identity.</param>
        internal static void Remember(StudioPresetContext context, ExcelExchangePreset preset)
        {
            EditorPrefs.SetString(Key(context), AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(preset)));
        }

        /// <summary>Creates or duplicates a mapping only after choosing its project path.</summary>
        /// <param name="context">Target linked into the new configuration.</param>
        /// <param name="source">Optional configuration to duplicate.</param>
        /// <returns>The newly saved asset or null after cancellation.</returns>
        internal static ExcelExchangePreset Create(StudioPresetContext context, ExcelExchangePreset source = null)
        {
            string path = EditorUtility.SaveFilePanelInProject(source == null ? "New Excel Mapping" : "Duplicate Excel Mapping",
                source == null ? context.Source.name + " Excel" : source.name + " Copy", "asset", "Save the mapping for this preset.");
            if (path.Length == 0)
                return null;
            ExcelExchangePreset created = source == null ? ScriptableObject.CreateInstance<ExcelExchangePreset>() : UnityEngine.Object.Instantiate(source);
            created.Target = context.Source;
            created.Scope = context.Scope;
            created.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(created, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssetIfDirty(created);
            Remember(context, created);
            return created;
        }

        /// <summary>Builds a project-local key stable across source renames and moves.</summary>
        /// <param name="context">Selected source asset and scope.</param>
        /// <returns>The preference key.</returns>
        private static string Key(StudioPresetContext context)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(context.Source, out string guid, out long localId);
            return "ExcelExchange." + Hash128.Compute(Application.dataPath) + "." + guid + "." + localId + "." + context.Scope;
        }

        #endregion
    }
}
