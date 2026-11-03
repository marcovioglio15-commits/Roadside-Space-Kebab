using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Owns palette edits through the same detached transactions used by dialogue entries.</summary>
    internal sealed class DialoguePaletteWindow : StudioAssetWindow
    {
        #region Properties

        protected override Type AssetType => typeof(DialoguePalettePreset);

        #endregion

        #region Methods

        #region Window

        /// <summary>Opens a reusable palette without applying any changes to its importing entries.</summary>
        /// <param name="preset">Palette asset to edit.</param>
        internal static void Open(DialoguePalettePreset preset)
        {
            DialoguePaletteWindow window = GetWindow<DialoguePaletteWindow>("Text Colors");
            window.Select(preset);
            window.minSize = new Vector2(380f, 300f);
        }

        /// <summary>Rejects invalid token names before the independent draft is saved.</summary>
        /// <param name="proposal">Detached palette.</param>
        /// <param name="issue">Receives an invalid name or color.</param>
        /// <returns>True when this palette can be saved.</returns>
        protected override bool Validate(ScriptableObject proposal, out string issue)
        {
            return DialogueNamedColors.TryValidate(((DialoguePalettePreset)proposal).Colors, out issue);
        }

        #endregion

        #endregion
    }

    /// <summary>Routes saved palettes to their dedicated window and draws only detached color lists.</summary>
    [CustomEditor(typeof(DialoguePalettePreset))]
    internal sealed class DialoguePaletteEditor : UnityEditor.Editor
    {
        #region Methods

        #region Drawing

        /// <summary>Edits the proposal or opens the saved palette's independent draft.</summary>
        public override void OnInspectorGUI()
        {
            if (EditorUtility.IsPersistent(target))
            {
                if (StudioButton.Draw(new GUIContent("Open Palette", "Edit this palette in an independent Apply/Discard window.")))
                    DialoguePaletteWindow.Open((DialoguePalettePreset)target);
                return;
            }
            serializedObject.Update();
            DialoguePaletteControls.Rows(serializedObject.FindProperty("Colors"));
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #endregion
    }
}
