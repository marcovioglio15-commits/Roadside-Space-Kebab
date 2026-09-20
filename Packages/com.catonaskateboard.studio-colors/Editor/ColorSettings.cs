using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Persists project colors independently of package installation paths.</summary>
    [FilePath("ProjectSettings/StudioColors.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class ColorSettings : ScriptableSingleton<ColorSettings>
    {
        #region Fields
        [Header("Folder Rules")]
        [Tooltip("Folder GUIDs retain their colors after moves and renames.")]
        [SerializeField]
        private List<FolderRule> folders = new List<FolderRule>();
        [Header("Tool Rules")]
        [Tooltip("Stable element keys shared by registered editor tools.")]
        [SerializeField]
        private List<ElementRule> elements = new List<ElementRule>();
        public static event Action Changed;
        #endregion

        #region Properties
        public IReadOnlyList<FolderRule> Folders => folders;
        public IReadOnlyList<ElementRule> Elements => elements;
        #endregion

        #region Methods
        #region Persistence
        /// <summary>Saves one deliberate edit and refreshes registered views.</summary>
        public void Commit()
        {
            // ScriptableSingleton saves outside Assets without importing generated files.
            Save(true);
            Changed?.Invoke();
            EditorApplication.RepaintProjectWindow();
        }
        #endregion

        #region Folders
        /// <summary>Resolves the nearest applicable folder rule, including explicit exclusions.</summary>
        /// <param name="path">Unity asset folder path.</param>
        /// <returns>Effective rule or null when this folder uses Unity's colors.</returns>
        public FolderRule ResolveFolder(string path)
        {
            // Walk ancestors on cache misses only; inheritance remains dynamic for new folders.
            string current = path;
            while (!string.IsNullOrEmpty(current))
            {
                string guid = AssetDatabase.AssetPathToGUID(current);
                FolderRule rule = folders.Find(candidate => candidate.Guid == guid);
                if (rule != null && (current == path || rule.Inherit))
                    return rule.Enabled ? rule : null;
                int separator = current.LastIndexOf('/');
                current = separator < 0 ? string.Empty : current.Substring(0, separator);
            }
            return null;
        }

        /// <summary>Sets one explicit rule without touching descendant overrides.</summary>
        /// <param name="path">Existing Assets or Packages folder.</param>
        /// <param name="color">Requested tint.</param>
        /// <param name="mode">Icon, text or both.</param>
        /// <param name="inherit">Allow descendants to inherit this rule.</param>
        /// <param name="enabled">False creates an explicit no-color boundary.</param>
        public void SetFolder(string path, Color color, FolderColorMode mode, bool inherit, bool enabled = true)
        {
            // Reject files and external filesystem paths before changing persistent state.
            if (!AssetDatabase.IsValidFolder(path))
                return;
            string guid = AssetDatabase.AssetPathToGUID(path);
            Undo.RecordObject(this, "Set folder color");
            folders.RemoveAll(rule => rule.Guid == guid);
            folders.Add(new FolderRule { Guid = guid, Color = color, Mode = mode, Inherit = inherit, Enabled = enabled });
            Commit();
        }

        /// <summary>Removes an override so the folder can inherit its parent's rule again.</summary>
        /// <param name="guid">Stable folder identity.</param>
        public void RemoveFolder(string guid)
        {
            // Keep reset behavior undoable and persist the removal immediately.
            Undo.RecordObject(this, "Reset folder color");
            folders.RemoveAll(rule => rule.Guid == guid);
            Commit();
        }
        #endregion

        #region Elements
        /// <summary>Finds the persisted appearance for an editor control.</summary>
        /// <param name="key">Stable tool and element key.</param>
        /// <returns>Authored rule or null.</returns>
        public ElementRule FindElement(string key)
        {
            // Keys belong to tools rather than translated display labels.
            return elements.Find(rule => rule.Key == key);
        }

        /// <summary>Changes one control's text and background overrides.</summary>
        /// <param name="rule">Complete replacement appearance.</param>
        public void SetElement(ElementRule rule)
        {
            // Store a single record per key instead of parallel arrays.
            Undo.RecordObject(this, "Set tool colors");
            elements.RemoveAll(entry => entry.Key == rule.Key);
            elements.Add(rule);
            Commit();
        }

        /// <summary>Restores the original style of one registered control.</summary>
        /// <param name="key">Stable control key.</param>
        public void RemoveElement(string key)
        {
            // Removal triggers live refresh for every registered instance.
            Undo.RecordObject(this, "Reset tool colors");
            elements.RemoveAll(entry => entry.Key == key);
            Commit();
        }
        #endregion
        #endregion
    }

    /// <summary>Chooses the Project window surfaces affected by a folder rule.</summary>
    public enum FolderColorMode { Icon, Text, IconAndText }

    /// <summary>Keeps all folder appearance data in one atomic record.</summary>
    [Serializable]
    public sealed class FolderRule
    {
        #region Fields
        [Tooltip("Folder asset identity, stable across moves.")]
        public string Guid;
        [Tooltip("Tint used by the Project window.")]
        public Color Color;
        [Tooltip("Surfaces receiving the tint.")]
        public FolderColorMode Mode;
        [Tooltip("Pass this rule to descendant folders lacking an override.")]
        public bool Inherit = true;
        [Tooltip("Disable color here and, when inherited, below this folder.")]
        public bool Enabled = true;
        #endregion
    }

    /// <summary>Stores optional colors for one registered editor element.</summary>
    [Serializable]
    public sealed class ElementRule
    {
        #region Fields
        [Tooltip("Stable key supplied by the owning tool.")]
        public string Key;
        [Tooltip("Override label and button text.")]
        public bool OverrideText;
        [Tooltip("Text color applied when enabled.")]
        public Color Text = Color.white;
        [Tooltip("Override the element background.")]
        public bool OverrideBackground;
        [Tooltip("Background color applied when enabled.")]
        public Color Background = new Color(0.18f, 0.24f, 0.3f);
        #endregion
    }
}
