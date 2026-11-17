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
            new GUIContent("Update Existing", "Update existing items. Keep fields that are not in the file."),
            new GUIContent("Update and Add", "Update existing items. Add new ones with default values for fields outside the file."),
            new GUIContent("Replace All", "Replace this list with the file's items. Fields outside the file use default values.")
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
                ExcelCollectionPolicy.ReplaceCollection => "Replace items. Fields outside the file use defaults.",
                ExcelCollectionPolicy.Merge => "Update existing items and add missing ones.",
                _ => "Update existing items. Preserve other properties."
            }, EditorStyles.wordWrappedMiniLabel);
            return selected;
        }

        #endregion
    }
}
