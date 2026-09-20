using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Draws cached folder tints in both Project window layouts.</summary>
    [InitializeOnLoad]
    internal static class FolderColors
    {
        #region Fields
        private static readonly Dictionary<string, FolderRule> cache = new Dictionary<string, FolderRule>();
        private static GUIStyle labelStyle;
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
        #endregion

        #region Drawing
        /// <summary>Adds icon tint and actual colored text without covering selection backgrounds.</summary>
        /// <param name="guid">Asset identity supplied by Unity.</param>
        /// <param name="rect">Visible row or grid item bounds.</param>
        private static void Draw(string guid, Rect rect)
        {
            // Resolve folders only once per project or rule change, including negative results.
            if (Event.current.type != EventType.Repaint)
                return;
            if (!cache.TryGetValue(guid, out FolderRule rule))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                rule = AssetDatabase.IsValidFolder(path) ? ColorSettings.instance.ResolveFolder(path) : null;
                cache[guid] = rule;
            }
            if (rule == null)
                return;

            bool grid = rect.height > 22f;
            float size = grid ? Mathf.Min(rect.width, rect.height - 18f) : 16f;
            Rect icon = new Rect(grid ? rect.center.x - size * 0.5f : rect.x, rect.y, size, size);
            if (rule.Mode != FolderColorMode.Text)
            {
                Color previous = GUI.color;
                GUI.color = rule.Color;
                GUI.DrawTexture(icon, EditorGUIUtility.IconContent("Folder Icon").image, ScaleMode.ScaleToFit);
                GUI.color = previous;
            }

            // Draw the label itself; the original tool only laid a rectangle over the text.
            if (rule.Mode == FolderColorMode.Icon)
                return;
            labelStyle ??= new GUIStyle(EditorStyles.label);
            labelStyle.normal.textColor = rule.Color;
            labelStyle.alignment = grid ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            labelStyle.clipping = TextClipping.Clip;
            Rect label = grid ? new Rect(rect.x, rect.yMax - 18f, rect.width, 18f)
                : new Rect(rect.x + 18f, rect.y, rect.width - 18f, rect.height);
            GUI.Label(label, Path.GetFileName(AssetDatabase.GUIDToAssetPath(guid)), labelStyle);
        }
        #endregion
        #endregion
    }
}
