using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Edits folder rules and registered tool colors in a small project-scoped window.</summary>
    public sealed class StudioColorsWindow : EditorWindow
    {
        #region Fields
        private Color color = new Color(0.3f, 0.8f, 1f);
        private FolderColorMode mode = FolderColorMode.IconAndText;
        private bool inherit = true;
        private Vector2 scroll;
        private string elementKey = string.Empty;
        private ElementRule element = new ElementRule();
        #endregion

        #region Methods
        #region Opening
        /// <summary>Opens the project color workspace.</summary>
        [MenuItem("Tools/Studio Colors")]
        public static void Open()
        {
            // Selection remains in the Project window for multi-folder operations.
            GetWindow<StudioColorsWindow>("Studio Colors").minSize = new Vector2(320f, 300f);
        }

        /// <summary>Opens the same workspace from a folder context menu.</summary>
        [MenuItem("Assets/Set Folder Color...", false, 2000)]
        private static void OpenFolders()
        {
            // The selected folder list is read when the operation is committed.
            Open();
        }

        /// <summary>Restricts the context action to selected folders.</summary>
        /// <returns>Whether at least one selected object is a folder.</returns>
        [MenuItem("Assets/Set Folder Color...", true)]
        private static bool CanOpenFolders()
        {
            // AssetDatabase distinguishes folders from files and scene objects.
            foreach (Object selected in Selection.objects)
                if (AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(selected)))
                    return true;
            return false;
        }

        /// <summary>Loads an element rule from its context menu.</summary>
        /// <param name="key">Stable key of the clicked control.</param>
        public static void EditElement(string key)
        {
            // Edit a detached copy until Apply is pressed.
            Open();
            StudioColorsWindow window = GetWindow<StudioColorsWindow>();
            window.elementKey = key;
            ElementRule stored = ColorSettings.instance.FindElement(key);
            window.element = stored != null ? JsonUtility.FromJson<ElementRule>(JsonUtility.ToJson(stored)) : new ElementRule { Key = key };
        }
        #endregion

        #region Controls
        /// <summary>Draws only controls applicable to the selected color target.</summary>
        private void OnGUI()
        {
            // The list scrolls independently of the operating system's window size.
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Selected folders", EditorStyles.boldLabel);
            color = EditorGUILayout.ColorField(new GUIContent("Color", "Folder icon and label tint."), color);
            mode = (FolderColorMode)EditorGUILayout.EnumPopup(new GUIContent("Apply to", "Color the icon, text, or both."), mode);
            inherit = EditorGUILayout.Toggle(new GUIContent("Inherit to children", "New and existing children inherit unless they have an override."), inherit);
            using (new EditorGUI.DisabledScope(!CanOpenFolders()))
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Apply", "Color all selected folders.")))
                        ApplySelection(true);
                    if (GUILayout.Button(new GUIContent("No color", "Block inherited color for these folders.")))
                        ApplySelection(false);
                }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Folder overrides", EditorStyles.boldLabel);
            foreach (FolderRule rule in ColorSettings.instance.Folders)
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(AssetDatabase.GUIDToAssetPath(rule.Guid), GUILayout.MinWidth(120f));
                    if (GUILayout.Button(new GUIContent("Reset", "Remove this override and resume parent inheritance."), GUILayout.Width(55f)))
                    {
                        ColorSettings.instance.RemoveFolder(rule.Guid);
                        GUIUtility.ExitGUI();
                    }
                }

            // Right-click any registered label or button to bring its key here.
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Tool element", EditorStyles.boldLabel);
            elementKey = EditorGUILayout.TextField(new GUIContent("Key", "Stable control key from a tool's Colors/Edit context action."), elementKey);
            element.OverrideText = EditorGUILayout.Toggle(new GUIContent("Text override", "Use a custom text color."), element.OverrideText);
            if (element.OverrideText)
                element.Text = EditorGUILayout.ColorField(new GUIContent("Text", "Color for this element's text."), element.Text);
            element.OverrideBackground = EditorGUILayout.Toggle(new GUIContent("Background override", "Use a custom background color."), element.OverrideBackground);
            if (element.OverrideBackground)
                element.Background = EditorGUILayout.ColorField(new GUIContent("Background", "Color for the control's background."), element.Background);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(elementKey)))
                if (GUILayout.Button(new GUIContent("Apply tool colors", "Save and refresh this key in every registered tool.")))
                {
                    element.Key = elementKey;
                    ColorSettings.instance.SetElement(JsonUtility.FromJson<ElementRule>(JsonUtility.ToJson(element)));
                }
            foreach (ElementRule rule in ColorSettings.instance.Elements)
                if (GUILayout.Button(new GUIContent(rule.Key, "Edit this saved tool color.")))
                    EditElement(rule.Key);
            EditorGUILayout.EndScrollView();
        }

        /// <summary>Commits the same explicit rule to each selected folder.</summary>
        /// <param name="enabled">Whether the rule colors or excludes the folders.</param>
        private void ApplySelection(bool enabled)
        {
            // Selection can include files; SetFolder ignores them safely.
            foreach (Object selected in Selection.objects)
                ColorSettings.instance.SetFolder(AssetDatabase.GetAssetPath(selected), color, mode, inherit, enabled);
        }
        #endregion
        #endregion
    }
}
