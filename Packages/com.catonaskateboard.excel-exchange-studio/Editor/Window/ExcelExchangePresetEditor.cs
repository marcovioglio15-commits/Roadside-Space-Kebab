using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Keeps exchange configurations linked to the preset tool that owns their values.</summary>
    [CustomEditor(typeof(ExcelExchangePreset))]
    internal sealed class ExcelExchangePresetEditor : UnityEditor.Editor
    {
        #region Methods

        /// <summary>Shows target identity and directs mapping edits to the contextual window.</summary>
        public override void OnInspectorGUI()
        {
            ExcelExchangePreset preset = (ExcelExchangePreset)target;
            using (new EditorGUI.DisabledScope(true))
                StudioFieldGUI.ObjectField(new GUIContent("Linked Preset", "Open this preset in its existing tool to edit the spreadsheet mapping."), preset.Target, typeof(ScriptableObject), false);
            EditorGUILayout.LabelField(preset.Mappings.Length + " mappings", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Use Import With Excell or Export With Excell below the preset selector in its tool.", EditorStyles.wordWrappedMiniLabel);
            if (preset.Target != null && StudioButton.Draw(new GUIContent("Locate Preset", "Select the linked preset in the Project window.")))
                EditorGUIUtility.PingObject(preset.Target);
        }

        #endregion
    }
}
