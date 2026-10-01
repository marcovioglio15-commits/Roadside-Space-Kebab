using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Opens flag editing independently of a closed or locked Inspector.</summary>
    internal sealed class ObjectFlagEditWindow : EditorWindow
    {
        #region State

        [Header("Definition")]
        [Tooltip("Flag being edited. Renaming preserves all existing references.")]
        [SerializeField]
        private ObjectFlag flag;
        private UnityEditor.Editor inspector;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Focuses a visible editor on the selected catalog entry.</summary>
        /// <param name="target">Persistent flag definition.</param>
        internal static void Open(ObjectFlag target)
        {
            // The tool must not depend on the main Inspector's selection or lock state.
            ObjectFlagEditWindow window = GetWindow<ObjectFlagEditWindow>("Edit Object Flag");
            if (window.flag != null && window.flag != target)
                AssetDatabase.SaveAssetIfDirty(window.flag);
            window.flag = target;
            window.minSize = new Vector2(320f, 180f);
            window.Show();
            window.Focus();
        }

        /// <summary>Saves pending definition edits and releases the embedded Inspector.</summary>
        private void OnDisable()
        {
            if (flag != null)
                AssetDatabase.SaveAssetIfDirty(flag);
            if (inspector != null)
                DestroyImmediate(inspector);
        }

        #endregion

        #region Drawing

        /// <summary>Reuses the flag Inspector's validation, Undo and catalog invalidation.</summary>
        private void OnGUI()
        {
            // A deleted definition never redirects editing to another flag.
            if (flag == null)
                return;
            UnityEditor.Editor.CreateCachedEditor(flag, null, ref inspector);
            inspector.OnInspectorGUI();
            if (StudioButton.Draw(new GUIContent("Save", "Save this definition without changing its identity.")))
                AssetDatabase.SaveAssetIfDirty(flag);
        }

        #endregion

        #endregion
    }
}
