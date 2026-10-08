using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Owns a detached asset proposal with conflict detection and an independent Apply/Discard footer.</summary>
    public abstract class StudioAssetWindow : EditorWindow
    {
        #region Fields

        [Tooltip("Shared asset currently open in this window.")]
        [SerializeField]
        private ScriptableObject source;
        [Tooltip("Independent proposal retained through editor reloads.")]
        [SerializeField]
        private ScriptableObject draft;
        [Tooltip("Saved source state used to detect changes from another editor.")]
        [SerializeField]
        private string baseline = string.Empty;
        private UnityEditor.Editor inspector;
        private Vector2 scroll;
        private string warning = string.Empty;

        #endregion

        #region Properties

        /// <summary>Accepted asset type for the source picker.</summary>
        protected abstract System.Type AssetType { get; }

        #endregion

        #region Methods
        #region Window

        /// <summary>Keeps retained proposals editable after the editor reloads their native object state.</summary>
        protected virtual void OnEnable()
        {
            // Native property scopes respect NotEditable even when ordinary text fields do not.
            if (draft != null)
                draft.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        }

        /// <summary>Opens the selected source in a focused authoring window.</summary>
        /// <param name="asset">Shared source to edit without changing its saved data.</param>
        protected void Select(ScriptableObject asset)
        {
            // A retained proposal must be resolved before selecting another source.
            if (source != asset && hasUnsavedChanges)
                warning = "Apply or Discard before changing assets.";
            else if (source != asset || draft == null)
            {
                source = asset;
                Reload();
            }
            minSize = new Vector2(480f, 360f);
            Show();
        }

        /// <summary>Shares the asset inspector with the dedicated scrollable window.</summary>
        protected virtual void OnGUI()
        {
            // Selection remains locked while this window owns pending changes.
            using (new EditorGUI.DisabledScope(hasUnsavedChanges))
            {
                EditorGUI.BeginChangeCheck();
                source = (ScriptableObject)StudioGUI.ObjectField(new GUIContent("Asset", "Shared asset receiving Apply."), source, AssetType, false);
                if (EditorGUI.EndChangeCheck())
                    Reload();
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                using (EditorGUILayout.ScrollViewScope view = new EditorGUILayout.ScrollViewScope(scroll))
                {
                    scroll = view.scrollPosition;
                    if (draft != null)
                    {
                        UnityEditor.Editor.CreateCachedEditor(draft, null, ref inspector);
                        DrawInspector(inspector);
                    }
                }
                hasUnsavedChanges = draft != null && JsonUtility.ToJson(draft) != baseline;
                saveChangesMessage = "Apply or discard the pending " + titleContent.text + " changes.";
                if (warning.Length > 0)
                    EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
                using (new StudioButton.RowScope())
                using (new EditorGUI.DisabledScope(!hasUnsavedChanges))
                {
                    if (StudioButton.Draw(new GUIContent("Apply", "Validate and save this window's source changes."), expandWidth: true, minimumHeight: 26f))
                        SaveChanges();
                    if (StudioButton.Draw(new GUIContent("Discard", "Reload the saved source and abandon this proposal."), expandWidth: true, minimumHeight: 26f))
                        DiscardChanges();
                }
            }
        }

        /// <summary>Draws the detached inspector, allowing specialized windows to supply non-persistent context.</summary>
        /// <param name="editor">Cached editor targeting the current proposal.</param>
        protected virtual void DrawInspector(UnityEditor.Editor editor)
        {
            editor.OnInspectorGUI();
        }

        /// <summary>Releases the cached editor when the window closes or reloads.</summary>
        protected virtual void OnDisable()
        {
            // Shared source assets remain intact; only the transient inspector is released.
            if (inspector != null)
                DestroyImmediate(inspector);
        }

        /// <summary>Releases the temporary source after the window has resolved its close prompt.</summary>
        protected virtual void OnDestroy()
        {
            if (draft != null)
                DestroyImmediate(draft);
        }

        #endregion
        #region Transaction

        /// <summary>Checks the detached proposal before it can replace its source.</summary>
        /// <param name="proposal">Detached asset containing pending changes.</param>
        /// <param name="issue">Receives a validation warning without modifying the proposal.</param>
        /// <returns>True when the proposal may be saved.</returns>
        protected abstract bool Validate(ScriptableObject proposal, out string issue);


        /// <summary>Validates the proposal and rejects external source edits before saving.</summary>
        public override void SaveChanges()
        {
            // Validation reports invalid data without rewriting the proposal.
            if (source == null || draft == null || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (JsonUtility.ToJson(source) != baseline)
                warning = "The saved source changed outside this window. Discard to reload it.";
            else if (!AssetDatabase.IsOpenForEdit(source))
                warning = "The selected source is not writable.";
            else if (Validate(draft, out warning))
            {
                Undo.RecordObject(source, "Apply " + titleContent.text);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(draft), source);
                EditorUtility.SetDirty(source);
                AssetDatabase.SaveAssetIfDirty(source);
                Reload();
                base.SaveChanges();
            }
            Repaint();
        }

        /// <summary>Abandons only this window's proposal and reloads its source.</summary>
        public override void DiscardChanges()
        {
            Reload();
            base.DiscardChanges();
        }

        /// <summary>Creates a detached source while retaining native asset references.</summary>
        private void Reload()
        {
            // Destroy the old inspector before replacing its target.
            if (inspector != null)
                DestroyImmediate(inspector);
            if (draft != null)
                DestroyImmediate(draft);
            draft = source != null ? Instantiate(source) : null;
            if (draft != null)
            {
                draft.name = source.name;
                draft.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            }
            baseline = source != null ? JsonUtility.ToJson(source) : string.Empty;
            warning = string.Empty;
            hasUnsavedChanges = false;
        }

        #endregion
        #endregion
    }
}
