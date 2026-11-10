using System;
using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Owns a recoverable asset proposal with conflict detection and an independent Apply/Discard footer.</summary>
    public abstract class StudioAssetWindow : EditorWindow
    {
        #region Fields

        [Header("Editing")]
        [Tooltip("Detached asset session, retained independently of the inspector and editor process.")]
        [SerializeField]
        private StudioAssetSession session = new StudioAssetSession();
        private UnityEditor.Editor inspector;
        private Vector2 scroll;
        private string warning = string.Empty;
        private readonly ObjectFieldFocus fieldFocus = new ObjectFieldFocus();
        private string focusPath = string.Empty;
        private string focusLabel = string.Empty;
        private bool focusPending;
        private bool focusConfigured;

        #endregion
        #region Properties

        /// <summary>Accepted asset type for the source picker.</summary>
        protected abstract System.Type AssetType { get; }
        /// <summary>Whether the shared inspector needs outer scrolling; graph editors provide their own viewport.</summary>
        protected virtual bool ScrollInspector => true;

        #endregion
        #region Methods
        #region Window

        /// <summary>Recovers unfinished edits and observes source imports, Undo and delayed field menus.</summary>
        protected virtual void OnEnable()
        {
            session.Restore();
            EditorApplication.projectChanged += RefreshSource;
            Undo.undoRedoPerformed += RefreshSource;
            StudioFieldMenu.Changed += FieldChanged;
            UpdatePending();
        }

        /// <summary>Opens the selected source while preserving unfinished work on another asset.</summary>
        /// <param name="asset">Shared source to edit without changing saved data.</param>
        protected void Select(ScriptableObject asset)
        {
            ReleaseInspector();
            session.Select(asset, out warning);
            focusPath = string.Empty;
            focusLabel = string.Empty;
            focusPending = false;
            focusConfigured = false;
            UpdatePending();
            minSize = new Vector2(480f, 360f);
            Show();
        }

        /// <summary>Shares the asset inspector with the dedicated scrollable window.</summary>
        protected virtual void OnGUI()
        {
            // The source picker cannot redirect a pending transaction.
            using (new EditorGUI.DisabledScope(session.HasChanges))
            {
                EditorGUI.BeginChangeCheck();
                ScriptableObject selected = (ScriptableObject)StudioGUI.ObjectField(new GUIContent("Asset", "Shared asset receiving Apply."),
                    session.Source, AssetType, false);
                if (EditorGUI.EndChangeCheck())
                    Select(selected);
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                Rect selection = default;
                if (focusLabel.Length > 0)
                {
                    selection = EditorGUILayout.GetControlRect(false);
                    EditorGUI.LabelField(selection, "Selected · " + focusLabel, EditorStyles.miniLabel);
                }
                if (ScrollInspector)
                    using (EditorGUILayout.ScrollViewScope view = new EditorGUILayout.ScrollViewScope(scroll))
                    {
                        scroll = view.scrollPosition;
                        DrawContent();
                    }
                else
                    DrawContent();
                if (focusLabel.Length > 0 && !fieldFocus.Found)
                    ObjectFieldFocus.Accent(selection);
                UpdatePending();
                string message = session.RecoveryWarning.Length > 0 ? session.RecoveryWarning : warning;
                DrawWarning(message);
                using (new StudioButton.RowScope())
                {
                    using (new EditorGUI.DisabledScope(!session.HasChanges))
                        if (StudioButton.Draw(new GUIContent("Apply", "Validate and save this shared asset to its project file."), expandWidth: true, minimumHeight: 26f))
                            ApplyChanges();
                    using (new EditorGUI.DisabledScope(session.Source == null || !session.HasChanges))
                        if (StudioButton.Draw(new GUIContent("Discard", "Abandon the retained proposal and reload the latest source."), expandWidth: true, minimumHeight: 26f))
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

        /// <summary>Places validation or conflict messages in the current window's presentation.</summary>
        /// <param name="message">Current issue, or empty after it has been resolved.</param>
        protected virtual void DrawWarning(string message)
        {
            if (message.Length > 0)
                EditorGUILayout.LabelField(message, EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>Shares cached inspector ownership between scrollable forms and fixed graph viewports.</summary>
        private void DrawContent()
        {
            if (session.Draft == null)
                return;
            UnityEditor.Editor.CreateCachedEditor(session.Draft, null, ref inspector);
            if (focusPending && !focusConfigured)
            {
                fieldFocus.Configure(inspector.serializedObject, focusPath);
                focusConfigured = true;
            }
            fieldFocus.RevealSections = focusPending;
            using (new StudioFieldRenderScope(ObjectFieldFocus.Draw))
            using (new ObjectFieldFocus.Scope(fieldFocus))
                DrawInspector(inspector);
            if (!focusPending || !fieldFocus.Found)
                return;
            scroll.y = Mathf.Max(0f, fieldFocus.Rect.y - 30f);
            GUI.FocusControl("ObjectSetting." + focusPath);
            focusPending = false;
            Repaint();
        }

        /// <summary>Reveals a tree-selected option in its already open asset editing session.</summary>
        /// <param name="asset">Source asset opened by its specialized editor.</param>
        /// <param name="path">Exact serialized option path.</param>
        /// <param name="label">Selected tree caption.</param>
        internal static void FocusSetting(ScriptableObject asset, string path, string label)
        {
            foreach (StudioAssetWindow window in Resources.FindObjectsOfTypeAll<StudioAssetWindow>())
                if (window.session.Source == asset)
                {
                    window.focusPath = path;
                    window.focusLabel = label;
                    window.focusPending = !string.IsNullOrEmpty(path);
                    window.focusConfigured = false;
                    window.Focus();
                    window.Repaint();
                    return;
                }
        }

        /// <summary>Retains proposals before an editor reload or window close releases native references.</summary>
        protected virtual void OnDisable()
        {
            session.Retain();
            EditorApplication.projectChanged -= RefreshSource;
            Undo.undoRedoPerformed -= RefreshSource;
            StudioFieldMenu.Changed -= FieldChanged;
            ReleaseInspector();
        }

        /// <summary>Releases the temporary copy after retaining any unresolved close-time proposal.</summary>
        protected virtual void OnDestroy()
        {
            session.Retain();
            session.Release();
        }

        /// <summary>Destroys the cached inspector before replacing its detached target.</summary>
        private void ReleaseInspector()
        {
            if (inspector != null)
                DestroyImmediate(inspector);
            inspector = null;
        }

        /// <summary>Refreshes clean copies when another editor or a source-control import changes their asset.</summary>
        private void RefreshSource()
        {
            session.Refresh();
            UpdatePending();
            Repaint();
        }

        /// <summary>Captures delayed menu edits before a window can be closed without another GUI pass.</summary>
        /// <param name="edited">Object written by a field or group menu.</param>
        private void FieldChanged(Object edited)
        {
            if (edited != session.Draft)
                return;
            UpdatePending();
            Repaint();
        }

        /// <summary>Updates close prompts and writes recovery data only when the proposal actually changes.</summary>
        private void UpdatePending()
        {
            hasUnsavedChanges = session.HasChanges;
            saveChangesMessage = "Apply or discard the pending " + titleContent.text + " changes.";
            session.Retain();
        }

        #endregion
        #region Transaction

        /// <summary>Checks the detached proposal before it can replace its source.</summary>
        /// <param name="proposal">Detached asset containing pending changes.</param>
        /// <param name="issue">Receives a validation warning without modifying the proposal.</param>
        /// <returns>True when the proposal may be saved.</returns>
        protected abstract bool Validate(ScriptableObject proposal, out string issue);

        /// <summary>Validates and saves the exact shared asset while retaining any rejected proposal.</summary>
        public override void SaveChanges()
        {
            if (!ApplyChanges())
                throw new InvalidOperationException(warning);
        }

        /// <summary>Saves this asset without clearing validation errors or the retained proposal on failure.</summary>
        /// <returns>True when the current proposal was saved.</returns>
        private bool ApplyChanges()
        {
            UpdatePending();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                warning = "Exit Play Mode before applying the shared asset.";
                return false;
            }
            bool saved = session.CanSave(out warning) && Validate(session.Draft, out warning)
                && session.Save("Apply " + titleContent.text, out warning);
            if (saved)
                base.SaveChanges();
            UpdatePending();
            Repaint();
            return saved;
        }

        /// <summary>Explicitly abandons this proposal and reloads the latest shared source.</summary>
        public override void DiscardChanges()
        {
            session.Discard();
            warning = string.Empty;
            base.DiscardChanges();
            UpdatePending();
        }

        #endregion
        #endregion
    }
}
