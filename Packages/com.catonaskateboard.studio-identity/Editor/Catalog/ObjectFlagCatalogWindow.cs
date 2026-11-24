using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Hosts one shared flag proposal across group, validation and assignment tabs.</summary>
    internal sealed class ObjectFlagCatalogWindow : EditorWindow
    {
        #region Fields

        [Header("Draft")]
        [Tooltip("Pending flag changes shared by all tabs.")]
        [SerializeField]
        private ObjectFlagDraftSession session;
        [Tooltip("Currently visible management tab.")]
        [SerializeField]
        private int tab;
        [Header("Assignments")]
        [Tooltip("Assignment filters and navigation retained by this window.")]
        [SerializeField]
        private ObjectFlagAssignmentSettings assignmentSettings = new ObjectFlagAssignmentSettings();

        #endregion
        #region State

        private readonly ObjectFlagGroupsView groups = new ObjectFlagGroupsView();
        private readonly ObjectFlagReferencesView validation = new ObjectFlagReferencesView(true);
        private readonly ObjectFlagReferencesView assignments = new ObjectFlagReferencesView(false);
        private bool undoQueued;
        private string warning = string.Empty;
        private static readonly GUIContent[] tabs =
        {
            new GUIContent("Groups", "Edit flag definitions and groups in the shared draft."),
            new GUIContent("Validation", "Find and stage cleanup of missing flag references."),
            new GUIContent("Assignments", "Edit project flag selections in the shared draft.")
        };

        #endregion
        #region Properties

        internal ObjectFlagAssignmentSettings AssignmentSettings => assignmentSettings;

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Opens the shared catalog from Tools or an existing flag selector.</summary>
        [MenuItem("Tools/Identity Flags")]
        internal static void Open()
        {
            GetWindow<ObjectFlagCatalogWindow>("Identity Flags").Show();
        }

        /// <summary>Restores the proposal and connects native Undo and external source notifications.</summary>
        private void OnEnable()
        {
            minSize = new Vector2(680f, 420f);
            wantsMouseMove = true;
            if (session == null)
            {
                session = CreateInstance<ObjectFlagDraftSession>();
                session.hideFlags = HideFlags.HideAndDontSave;
            }
            session.Restore();
            session.Changed += DraftChanged;
            EditorApplication.projectChanged += ProjectChanged;
            CatOnASkateboard.StudioColors.Editor.StudioSourceChanges.Changed += ProjectChanged;
            EditorApplication.update += UpdateSearch;
            Undo.undoRedoPerformed += QueueUndo;
            saveChangesMessage = "Apply or Discard the pending identity flag changes.";
            DraftChanged();
        }

        /// <summary>Releases listeners while retaining serialized working objects during assembly reload.</summary>
        private void OnDisable()
        {
            if (session != null)
                session.Changed -= DraftChanged;
            EditorApplication.projectChanged -= ProjectChanged;
            CatOnASkateboard.StudioColors.Editor.StudioSourceChanges.Changed -= ProjectChanged;
            EditorApplication.update -= UpdateSearch;
            Undo.undoRedoPerformed -= QueueUndo;
            EditorApplication.delayCall -= HandleUndo;
            groups.SaveNavigation();
            validation.SaveNavigation();
            assignments.SaveNavigation();
        }

        /// <summary>Releases only this window's isolated proposal after close confirmation.</summary>
        private void OnDestroy()
        {
            if (session == null)
                return;
            session.Release();
            DestroyImmediate(session);
        }

        /// <summary>Refreshes lightweight views after a proposal changes.</summary>
        private void DraftChanged()
        {
            hasUnsavedChanges = session != null && session.HasChanges;
            groups.Invalidate();
            validation.DraftChanged();
            assignments.DraftChanged();
            Repaint();
        }

        /// <summary>Marks scans stale after external writes without discarding a pending proposal.</summary>
        private void ProjectChanged()
        {
            session.RefreshSources();
            validation.Invalidate();
            if (session.HasChanges)
                assignments.Invalidate();
            else
                assignments.Applied();
        }

        /// <summary>Defers draft refresh until file-backed Apply Undo has restored its snapshots.</summary>
        private void QueueUndo()
        {
            if (undoQueued)
                return;
            undoQueued = true;
            EditorApplication.delayCall += HandleUndo;
        }

        /// <summary>Rebuilds serialized lookup tables after native Undo or Redo completes.</summary>
        private void HandleUndo()
        {
            undoQueued = false;
            if (session == null)
                return;
            session.Restore();
            if (!session.HasChanges)
                session.RefreshSources();
            DraftChanged();
        }

        /// <summary>Runs debounced text filtering only when a view has a pending search.</summary>
        private void UpdateSearch()
        {
            if (validation.UpdateSearch() | assignments.UpdateSearch())
                Repaint();
        }

        #endregion
        #region Drawing and Commit

        /// <summary>Draws one active view above the shared full-width Apply and Discard buttons.</summary>
        private void OnGUI()
        {
            if (session == null)
                return;
            if (Event.current.type == EventType.MouseMove)
                Repaint();
            tab = GUILayout.Toolbar(tab, tabs);
            using EditorGUI.DisabledScope locked = new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode);
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
                switch (tab)
                {
                    case 0:
                        groups.Draw(session);
                        break;
                    case 1:
                        validation.Draw(session);
                        break;
                    case 2:
                        assignments.Draw(session, this);
                        break;
                }
            if (warning.Length > 0)
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
            using EditorGUILayout.HorizontalScope footer = new EditorGUILayout.HorizontalScope();
            using EditorGUI.DisabledScope clean = new EditorGUI.DisabledScope(!session.HasChanges);
            if (GUILayout.Button(new GUIContent("Apply", "Save all pending flag changes across every tab."), GUILayout.Height(30f), GUILayout.ExpandWidth(true)))
                SaveChanges();
            if (GUILayout.Button(new GUIContent("Discard", "Reload saved data and abandon all pending flag changes."), GUILayout.Height(30f), GUILayout.ExpandWidth(true)))
                DiscardChanges();
        }

        /// <summary>Commits the entire proposal before marking the native close prompt as handled.</summary>
        public override void SaveChanges()
        {
            if (!ObjectFlagDraftCommit.Apply(session, out warning))
                return;
            validation.Invalidate();
            assignments.Applied();
            base.SaveChanges();
        }

        /// <summary>Discards all staged operations without changing project assets.</summary>
        public override void DiscardChanges()
        {
            session.Discard();
            groups.DiscardNavigation();
            warning = string.Empty;
            base.DiscardChanges();
        }

        #endregion
        #endregion
    }
}
