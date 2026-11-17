using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Opens an object or interaction as nested interaction, preset and section dropdowns.</summary>
    internal sealed class ObjectBranchWindow : EditorWindow
    {
        #region Fields

        [Header("Branch")]
        [Tooltip("Object represented by this nested editor.")]
        [SerializeField]
        private ObjectWorkspaceTarget target = new ObjectWorkspaceTarget();
        [Tooltip("Exact interaction, or zero for the entire object.")]
        [SerializeField]
        private long interactionId;
        [Tooltip("Independent proposals for the interactions in this branch.")]
        [SerializeField]
        private List<ObjectBranchSession> sessions = new List<ObjectBranchSession>();
        [Tooltip("Scroll position retained through script reloads.")]
        [SerializeField]
        private Vector2 scroll;

        #endregion
        #region State

        private string status = string.Empty;

        #endregion
        #region Methods
        #region Lifetime

        /// <summary>Reuses an exact object branch or creates independent proposals for all its interactions.</summary>
        /// <param name="state">Workspace supplying retained drafts.</param>
        /// <param name="owner">Object at the root of this branch.</param>
        /// <param name="interaction">Optional single interaction; null opens the entire object.</param>
        internal static void Open(ObjectWorkspace state, GameObject owner, ObjectInteraction interaction = null)
        {
            // Stable component IDs distinguish repeated interactions on the same object.
            ObjectWorkspaceTarget selection = new ObjectWorkspaceTarget();
            selection.Capture(owner, 0);
            long id = interaction != null ? ObjectWorkspaceTarget.FileId(interaction) : 0;
            foreach (ObjectBranchWindow existing in Resources.FindObjectsOfTypeAll<ObjectBranchWindow>())
                if (existing.target.PrefabGuid == selection.PrefabGuid && existing.target.ObjectPath == selection.ObjectPath && existing.interactionId == id)
                {
                    existing.Focus();
                    return;
                }
            ObjectBranchWindow window = CreateInstance<ObjectBranchWindow>();
            window.target = selection;
            window.interactionId = id;
            ObjectInteraction[] features = interaction != null ? new[] { interaction }
                : state.Navigation.IncludeChildren ? owner.GetComponentsInChildren<ObjectInteraction>(true) : owner.GetComponents<ObjectInteraction>();
            foreach (ObjectInteraction feature in features)
            {
                ObjectBranchSession session = ObjectBranchSession.Create(state, feature);
                session.EnsureRetained();
                window.sessions.Add(session);
            }
            window.titleContent = new GUIContent(interaction != null ? interaction.InteractionName : owner.name);
            window.minSize = new Vector2(440f, 320f);
            window.RefreshChanges();
            window.Show();
        }

        /// <summary>Reconnects editor documents and delayed field callbacks after reload.</summary>
        private void OnEnable()
        {
            // Native selectors may finish an edit after the immediate GUI event has ended.
            Undo.undoRedoPerformed += RefreshChanges;
            StudioFieldMenu.Changed += FieldChanged;
            ObjectFlagSelector.SelectionChanged += FieldChanged;
            foreach (ObjectBranchSession session in sessions)
                if (session.Document != null)
                    session.Refresh();
            UpdatePending();
        }

        /// <summary>Detaches callbacks while retaining proposals through script reload.</summary>
        private void OnDisable()
        {
            // Keep independent proposals, but release all editor callbacks and property handles.
            Undo.undoRedoPerformed -= RefreshChanges;
            StudioFieldMenu.Changed -= FieldChanged;
            ObjectFlagSelector.SelectionChanged -= FieldChanged;
            foreach (ObjectBranchSession session in sessions)
                session.ReleaseView();
        }

        /// <summary>Releases all independent proposals after the native close prompt resolves.</summary>
        private void OnDestroy()
        {
            // The native close prompt resolves pending changes before these documents are destroyed.
            foreach (ObjectBranchSession session in sessions)
                session.Dispose();
        }

        #endregion
        #region Drawing

        /// <summary>Draws complete branches through the same conditional controls used by focused preset editors.</summary>
        private void OnGUI()
        {
            using ObjectStudioFieldLayout layout = new ObjectStudioFieldLayout(180f);
            using StudioFieldRenderScope render = new StudioFieldRenderScope(ObjectNumericFields.Draw);
            using (EditorGUILayout.ScrollViewScope page = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = page.scrollPosition;
                using EditorGUI.DisabledScope disabled = new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode);
                EditorGUILayout.LabelField(target.ObjectName, EditorStyles.boldLabel);
                GameObject owner = target.Resolve();
                if (interactionId == 0 && owner != null)
                    using (new EditorGUI.DisabledScope(!target.IsOpen || EditorUtility.IsPersistent(owner)))
                        ObjectIdentityControls.Draw(owner, sessions.Exists(session => session.HasChanges));
                foreach (ObjectBranchSession session in sessions)
                {
                    if (session.Document == null)
                        continue;
                    ObjectInteraction feature = session.Feature;
                    if (feature == null)
                    {
                        EditorGUILayout.LabelField("The original interaction is no longer available.", EditorStyles.miniLabel);
                        continue;
                    }
                    if (!ObjectWorkspaceGUI.Foldout(session.Document, "Interaction." + session.Document.DraftKey,
                        feature.InteractionName + " · " + ObjectInteractionCatalog.Name(feature), "All preset groups for this interaction."))
                        continue;
                    using StudioIndentScope indent = new StudioIndentScope();
                    session.Draw();
                    using EditorGUI.DisabledScope unchanged = new EditorGUI.DisabledScope(!session.HasChanges);
                    using EditorGUILayout.HorizontalScope actions = new EditorGUILayout.HorizontalScope();
                    if (GUILayout.Button(new GUIContent("Apply interaction", "Validate and save this interaction's settings.")))
                        session.Apply(out status);
                    if (GUILayout.Button(new GUIContent("Discard interaction", "Reload this interaction's applied values.")))
                        session.Reload();
                }
            }
            if (!string.IsNullOrEmpty(status))
                EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel);
            UpdatePending();
        }

        #endregion
        #region Coordination

        /// <summary>Refreshes independent proposals only at explicit edit boundaries.</summary>
        private void RefreshChanges()
        {
            // Undo and delayed selectors can change documents outside their immediate GUI event.
            foreach (ObjectBranchSession session in sessions)
                if (session.Document != null)
                    session.Refresh();
            UpdatePending();
            Repaint();
        }

        /// <summary>Updates the native close prompt without serializing snapshots on repaint.</summary>
        private void UpdatePending()
        {
            // Session flags are cached at edit boundaries, so this check creates no snapshots.
            hasUnsavedChanges = sessions.Exists(session => session.HasChanges);
            saveChangesMessage = "Apply or discard the unsaved interaction settings in this branch.";
        }

        /// <summary>Refreshes a document edited by a delayed picker.</summary>
        /// <param name="edited">Temporary proposal receiving the edit.</param>
        private void FieldChanged(UnityEngine.Object edited)
        {
            // Ignore edits belonging to another independent window.
            if (sessions.Exists(session => session.Document == edited))
                RefreshChanges();
        }

        /// <summary>Updates a branch after an existing layout editor changes its document.</summary>
        /// <param name="document">Document receiving external edits.</param>
        internal static void RefreshDocument(ObjectWorkspace document)
        {
            // Notify only the window that owns this layout proposal.
            foreach (ObjectBranchWindow window in Resources.FindObjectsOfTypeAll<ObjectBranchWindow>())
                if (window.sessions.Exists(session => session.Document == document))
                    window.RefreshChanges();
        }

        /// <summary>Rebases the opening snapshot after a child layout saves only its own fields.</summary>
        /// <param name="document">Branch document already synchronized by the layout transaction.</param>
        /// <param name="retained">Latest shared interaction proposal.</param>
        internal static void LayoutSaved(ObjectWorkspace document, ObjectWorkspace retained)
        {
            // Keep unrelated local edits in the document while advancing their comparison snapshot.
            foreach (ObjectBranchWindow window in Resources.FindObjectsOfTypeAll<ObjectBranchWindow>())
                foreach (ObjectBranchSession session in window.sessions)
                    if (session.Document == document)
                        session.Rebase(retained);
        }

        /// <summary>Protects pending branch editors from component removal.</summary>
        /// <param name="prefabGuid">Prefab about to change.</param>
        /// <param name="componentId">Exact interaction, or zero for the entire prefab.</param>
        /// <returns>True when a pending proposal would lose its source.</returns>
        internal static bool HasPendingFor(string prefabGuid, long componentId)
        {
            // Component removal must account for every independently edited branch.
            foreach (ObjectBranchWindow window in Resources.FindObjectsOfTypeAll<ObjectBranchWindow>())
                if (window.sessions.Exists(session => session.HasChanges && session.Document != null
                    && session.Document.Target.PrefabGuid == prefabGuid
                    && (componentId == 0 || session.Document.Navigation.SelectedId == componentId)))
                    return true;
            return false;
        }

        /// <summary>Applies every changed interaction when Unity resolves this window's close prompt.</summary>
        public override void SaveChanges()
        {
            // A failed transaction leaves the window and its remaining proposals open.
            foreach (ObjectBranchSession session in sessions)
                if (session.HasChanges && !session.Apply(out status))
                    throw new InvalidOperationException(status);
            UpdatePending();
            base.SaveChanges();
        }

        /// <summary>Reloads changed interactions when the native close prompt requests Discard.</summary>
        public override void DiscardChanges()
        {
            // Explicitly reload each changed interaction before releasing its local document.
            foreach (ObjectBranchSession session in sessions)
                if (session.HasChanges)
                    session.Reload();
            UpdatePending();
            base.DiscardChanges();
        }

        #endregion
        #endregion
    }
}
