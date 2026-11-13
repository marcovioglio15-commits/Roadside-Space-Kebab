using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Hosts optional preset actions without depending on their implementing packages.</summary>
    public static class StudioPresetActions
    {
        #region Fields

        public static event Action<StudioPresetContext, bool> ExchangeRequested;
        public static event Action<ScriptableObject, UnityEngine.Object, UnityEngine.Object, List<string>> ProposalReview;
        private static readonly Dictionary<Type, Func<UnityEngine.Object, string>> referenceLabels = new Dictionary<Type, Func<UnityEngine.Object, string>>();
        private static readonly GUIContent importLabel = new GUIContent("Import With Excell", "Read spreadsheet values into this preset's current editing session.");
        private static readonly GUIContent exportLabel = new GUIContent("Export With Excell", "Export this preset's current values to a spreadsheet.");

        #endregion
        #region Methods
        #region Actions

        /// <summary>Lets a tool report references affected by an isolated preset change.</summary>
        /// <param name="source">Persistent preset identity.</param>
        /// <param name="before">Current isolated values.</param>
        /// <param name="after">Proposed isolated values.</param>
        /// <param name="warnings">Receives review notes without changing other assets.</param>
        public static void Review(ScriptableObject source, UnityEngine.Object before, UnityEngine.Object after, List<string> warnings)
        {
            ProposalReview?.Invoke(source, before, after, warnings);
        }

        /// <summary>Places the connected exchange actions immediately below a preset selector.</summary>
        /// <param name="source">Selected persistent preset.</param>
        /// <param name="create">Builds the exact editing context after a button click.</param>
        public static void Draw(ScriptableObject source, Func<StudioPresetContext> create)
        {
            if (ExchangeRequested == null)
                return;
            using EditorGUI.DisabledScope disabled = new EditorGUI.DisabledScope(source == null || !EditorUtility.IsPersistent(source)
                || EditorApplication.isPlayingOrWillChangePlaymode);
            using StudioButton.RowScope row = new StudioButton.RowScope();
            if (StudioButton.Draw(importLabel, expandWidth: true))
                ExchangeRequested.Invoke(create(), false);
            if (StudioButton.Draw(exportLabel, expandWidth: true))
                ExchangeRequested.Invoke(create(), true);
        }

        /// <summary>Registers a tool's existing display label for asset reference conversion.</summary>
        /// <param name="label">Reads the same label displayed by the tool's asset selector.</param>
        /// <typeparam name="T">Referenced asset type.</typeparam>
        public static void RegisterReferenceLabel<T>(Func<T, string> label) where T : UnityEngine.Object
        {
            referenceLabels[typeof(T)] = asset => label((T)asset);
        }

        /// <summary>Resolves the registered label without inspecting runtime fields.</summary>
        /// <param name="asset">Referenced project asset.</param>
        /// <returns>The tool label or asset name.</returns>
        public static string ReferenceLabel(UnityEngine.Object asset)
        {
            return asset == null ? string.Empty : referenceLabels.TryGetValue(asset.GetType(), out Func<UnityEngine.Object, string> label)
                ? label(asset) : asset.name;
        }

        #endregion
        #endregion
    }
}
