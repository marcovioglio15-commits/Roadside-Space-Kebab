using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Explains collection replacement consistently in Layout and Import.</summary>
    internal static class ExcelCollectionControls
    {
        #region Fields

        private static readonly GUIContent[] modes =
        {
            new GUIContent("Update Existing", "Update existing items. Preserve their unmapped properties."),
            new GUIContent("Update and Add", "Update existing items and add missing ones using declared defaults."),
            new GUIContent("Replace All", "Rebuild the involved collections. Reset every unmapped property to its declared default.")
        };

        #endregion
        #region Methods

        /// <summary>Shows replacement behavior beside the collection it affects.</summary>
        /// <param name="mode">Current import policy.</param>
        /// <returns>The explicitly selected policy.</returns>
        internal static ExcelCollectionPolicy Draw(ExcelCollectionPolicy mode)
        {
            EditorGUILayout.LabelField(new GUIContent("Import Mode", "Choose whether existing items are updated, added or completely replaced."), EditorStyles.boldLabel);
            ExcelCollectionPolicy selected = (ExcelCollectionPolicy)StudioFieldGUI.Popup(GUIContent.none, (int)mode, modes);
            EditorGUILayout.LabelField(selected switch
            {
                ExcelCollectionPolicy.ReplaceCollection => "Rebuild items. Unmapped properties use defaults.",
                ExcelCollectionPolicy.Merge => "Update existing items and add missing ones.",
                _ => "Update existing items. Preserve other properties."
            }, EditorStyles.wordWrappedMiniLabel);
            return selected;
        }

        #endregion
    }
}
