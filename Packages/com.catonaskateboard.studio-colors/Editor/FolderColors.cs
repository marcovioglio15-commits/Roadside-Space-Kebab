using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Draws cached folder tints in both Project window layouts.</summary>
    [InitializeOnLoad]
    internal static class FolderColors
    {
        #region Fields
        private static readonly Dictionary<string, FolderPresentation> cache = new Dictionary<string, FolderPresentation>();
        private static readonly HashSet<string> selection = new HashSet<string>();
        private static GUIStyle rowStyle;
        private static GUIStyle listStyle;
        private static GUIStyle gridStyle;
        private static bool darkSkin;
        #endregion

        #region Methods
        #region Lifecycle
        /// <summary>Registers repaint callbacks once per editor domain.</summary>
        static FolderColors()
        {
            // Invalidate outside repaint so ordinary item drawing only performs cached lookups.
            EditorApplication.projectWindowItemOnGUI += Draw;
            EditorApplication.projectChanged += Invalidate;
            ColorSettings.Changed += Invalidate;
            Undo.undoRedoPerformed += HandleUndo;
            Selection.selectionChanged += RefreshSelection;
            RefreshSelection();
        }

        /// <summary>Persists Undo changes and refreshes folder and tool presentation.</summary>
        private static void HandleUndo()
        {
            // Undo must update the project settings file as well as memory.
            ColorSettings.instance.Commit();
        }

        /// <summary>Invalidates colors after rules or asset paths change.</summary>
        private static void Invalidate()
        {
            // Do not keep inherited rules after moves between differently colored parents.
            cache.Clear();
            EditorApplication.RepaintProjectWindow();
        }

        /// <summary>Caches selected folders so repaint does not allocate selection arrays per item.</summary>
        private static void RefreshSelection()
        {
            // Native selected labels preserve readable contrast and inline rename feedback.
            selection.Clear();
            foreach (string guid in Selection.assetGUIDs)
                selection.Add(guid);
            EditorApplication.RepaintProjectWindow();
        }

        /// <summary>Resolves folder metadata once until assets or color rules change.</summary>
        /// <param name="guid">Identity received from the Project window callback.</param>
        /// <returns>Cached folder appearance, or null when no color applies.</returns>
        private static FolderPresentation Resolve(string guid)
        {
            // Negative entries also avoid repeated AssetDatabase queries during repaint.
            if (cache.TryGetValue(guid, out FolderPresentation folder))
                return folder;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            FolderRule rule = AssetDatabase.IsValidFolder(path) ? ColorSettings.instance.ResolveFolder(path) : null;
            if (rule != null)
            {
                // Only a package root uses displayName; its child folders keep their own names.
                PackageInfo package = path.StartsWith("Packages/", System.StringComparison.Ordinal)
                    && path.IndexOf('/', 9) < 0 ? PackageInfo.FindForAssetPath(path) : null;
                folder = new FolderPresentation(rule,
                    package != null && !string.IsNullOrEmpty(package.displayName) ? package.displayName : Path.GetFileName(path),
                    AssetDatabase.GetCachedIcon(path), path == "Assets");
            }
            cache[guid] = folder;
            return folder;
        }
        #endregion

        #region Drawing
        /// <summary>Tints unselected folder labels using the Project window's native typography.</summary>
        /// <param name="guid">Asset identity supplied by Unity.</param>
        /// <param name="rect">Visible row or grid item bounds.</param>
        private static void Draw(string guid, Rect rect)
        {
            // Resolve folders only once per project or rule change, including negative results.
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(guid))
                return;
            FolderPresentation folder = Resolve(guid);
            if (folder == null)
                return;
            PrepareStyles();

            // Use the cached native icon, including package and empty-folder variants.
            bool grid = rect.height > 22f;
            // List items start in the 14-pixel margin; tree callbacks start at 16 pixels or deeper.
            bool list = !grid && rect.x < 16f;
            float size = grid ? Mathf.Min(rect.width, rect.height - gridStyle.fixedHeight) : 16f;
            Rect icon = new Rect(grid ? rect.center.x - size * 0.5f : rect.x, rect.y, size, size);
            if (list)
                icon.x += listStyle.margin.left;
            if (folder.Rule.Mode != FolderColorMode.Text && folder.Icon != null)
            {
                Color previous = GUI.color;
                GUI.color = folder.Rule.Color * previous;
                GUI.DrawTexture(icon, folder.Icon, ScaleMode.ScaleToFit);
                GUI.color = previous;
            }

            // Selected and renamed labels remain native; never paint over their highlights or caret.
            if (folder.Rule.Mode == FolderColorMode.Icon || selection.Contains(guid))
                return;
            // List rows start in the outer margin; tree callbacks already include their indentation.
            GUIStyle style = grid ? gridStyle : list ? listStyle : rowStyle;
            Rect label = grid ? new Rect(rect.x, rect.yMax - style.fixedHeight + 1f, rect.width, style.fixedHeight)
                : new Rect(icon.x, rect.y, rect.xMax - icon.x, rect.height);
            if (!grid && !list)
                label.xMin += 18f;
            // Match the native glyph positions without repainting row backgrounds or selection accents.
            style.normal.textColor = folder.Rule.Color;
            style.fontStyle = folder.Root && !grid && !list ? FontStyle.Bold : FontStyle.Normal;
            style.Draw(label, folder.Label, false, false, false, false);
        }

        /// <summary>Creates private native-style copies only on first use or a skin change.</summary>
        private static void PrepareStyles()
        {
            // Native padding and grid font size keep the replacement aligned at editor DPI scaling.
            if (rowStyle != null && darkSkin == EditorGUIUtility.isProSkin)
                return;
            darkSkin = EditorGUIUtility.isProSkin;
            rowStyle = new GUIStyle("TV Line");
            listStyle = new GUIStyle("OL ResultLabel");
            listStyle.padding.left = 18;
            gridStyle = new GUIStyle("ProjectBrowserGridLabel")
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Ellipsis
            };
        }
        #endregion
        #endregion
    }

    /// <summary>Keeps the resolved folder rule, native icon and displayed name together between repaints.</summary>
    internal sealed class FolderPresentation
    {
        #region Properties

        internal FolderRule Rule { get; }
        internal GUIContent Label { get; }
        internal Texture Icon { get; }
        internal bool Root { get; }

        #endregion

        #region Methods

        #region Construction

        /// <summary>Captures asset metadata when the folder first enters the drawing cache.</summary>
        /// <param name="rule">Effective folder tint, including inherited rules.</param>
        /// <param name="label">Folder name or package display name shown by Unity.</param>
        /// <param name="icon">Unity's cached folder icon.</param>
        /// <param name="root">Whether this is the bold Assets root in the folder tree.</param>
        internal FolderPresentation(FolderRule rule, string label, Texture icon, bool root)
        {
            // Cache GUIContent along with asset lookups to keep the repaint path allocation-free.
            Rule = rule;
            Label = new GUIContent(label);
            Icon = icon;
            Root = root;
        }

        #endregion

        #endregion
    }
}
