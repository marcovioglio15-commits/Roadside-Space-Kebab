using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits a complete interaction through its existing preset groups and conditional sections.</summary>
    [Serializable]
    internal sealed class ObjectBranchSession : IDisposable
    {
        #region Fields

        [Header("Interaction")]
        [Tooltip("Independent interaction proposal displayed in the nested editor.")]
        public ObjectWorkspace Document;
        [Tooltip("Shared proposal captured when this branch opened or last applied.")]
        [SerializeReference]
        private ObjectWorkspaceDraft opening;

        #endregion
        #region State

        private SerializedObject data;
        private List<ObjectGroupInfo> groups = new List<ObjectGroupInfo>();
        private ObjectInteraction feature;
        private bool pending;

        #endregion
        #region Properties

        /// <summary>Exact component represented by this branch.</summary>
        internal ObjectInteraction Feature => feature;
        /// <summary>One entry per reusable preset group.</summary>
        internal IReadOnlyList<ObjectGroupInfo> Groups => groups;
        /// <summary>Cached serialized document used during tree construction.</summary>
        internal SerializedObject Data => data;
        /// <summary>Whether this branch contains unapplied object settings.</summary>
        internal bool HasChanges => pending;

        #endregion
        #region Methods
        #region Lifetime

        /// <summary>Copies a retained interaction draft, or reads its applied values without changing main-window selection.</summary>
        /// <param name="state">Shared workspace containing active and parked proposals.</param>
        /// <param name="interaction">Exact component to inspect.</param>
        /// <returns>An independent branch owned by the caller.</returns>
        internal static ObjectBranchSession Create(ObjectWorkspace state, ObjectInteraction interaction)
        {
            // A temporary workspace uses the same selection and validation path as the main tool.
            ObjectBranchSession session = new ObjectBranchSession { Document = ScriptableObject.CreateInstance<ObjectWorkspace>() };
            if (!ObjectWorkspaceDrafts.Select(session.Document, interaction, out string warning))
            {
                session.Dispose();
                throw new InvalidOperationException(warning);
            }
            string key = session.Document.DraftKey;
            ObjectWorkspaceDraft retained = state.DraftKey == key ? ObjectWorkspaceDraft.Capture(state)
                : state.SavedDrafts.Find(draft => draft.Key == key);
            if (retained != null)
                retained.Restore(session.Document);
            session.Document.GroupPresets = ObjectWorkspace.Copy(state.GroupPresets);
            session.opening = ObjectWorkspaceDraft.Capture(session.Document);
            session.Refresh();
            // Broad editors start with collapsed sections while keeping every preset group reachable.
            foreach (ObjectGroupInfo group in session.groups)
                foreach (ObjectGroupInfo section in group.Segments)
                {
                    string sectionKey = ObjectFieldFocus.SectionKey(section.Parent, section.Section);
                    if (!session.Document.Navigation.Collapsed.Contains(sectionKey))
                        session.Document.Navigation.Collapsed.Add(sectionKey);
                }
            return session;
        }

        /// <summary>Reconnects cached controls after reload or an explicit external edit.</summary>
        internal void Refresh()
        {
            // Rebuild group metadata only after changes, never for ordinary repaint.
            data ??= new SerializedObject(Document);
            data.Update();
            feature = ObjectWorkspaceDrafts.Selected(Document);
            groups = ObjectInteractionPanel.Groups(Document, data);
            pending = Document.PrefabChanged;
        }

        /// <summary>Releases the temporary serialized wrapper and its independent proposal.</summary>
        public void Dispose()
        {
            // Destroy only this editor's document, never a prefab or the shared workspace.
            ReleaseView();
            if (Document != null)
                UnityEngine.Object.DestroyImmediate(Document);
            Document = null;
        }

        /// <summary>Releases serialized property handles while preserving the proposal through script reload.</summary>
        internal void ReleaseView()
        {
            // Serialized handles are temporary; the document remains available for window recovery.
            data?.Dispose();
            data = null;
        }

        #endregion
        #region Drawing

        /// <summary>Draws preset dropdowns containing the original section dropdowns and fields.</summary>
        internal void Draw()
        {
            // Keep group iteration stable if an edit changes which sections are available.
            EditorGUI.BeginChangeCheck();
            foreach (ObjectGroupInfo group in groups)
            {
                if (!string.IsNullOrEmpty(ObjectGroupAvailability.Requirement(data, group, out _)))
                    continue;
                ObjectGroupPreset preset = ObjectGroupPresetStore.Assignment(Document, group)?.Preset;
                string title = preset != null ? group.Title + " · " + preset.name : group.Title;
                if (!ObjectWorkspaceGUI.Foldout(Document, "Preset." + group.Key, title, group.Tooltip))
                    continue;
                using StudioIndentScope indent = new StudioIndentScope();
                if (GUILayout.Button(new GUIContent("Preset options", "Open this preset group's import, export and comparison controls."), EditorStyles.miniButton))
                {
                    EnsureRetained();
                    ObjectGroupWindow.Open(Document, group, optionsOnly: true);
                }
                using (ObjectGroupScope scope = new ObjectGroupScope(group))
                    ObjectInteractionPanel.Draw(Document, data);
            }
            ObjectInteractionActions.Draw(Document, feature);
            if (EditorGUI.EndChangeCheck())
                Refresh();
        }

        #endregion
        #region Transactions

        /// <summary>Ensures detached child editors can find this interaction without replacing another proposal.</summary>
        internal void EnsureRetained()
        {
            // New unchanged drafts are registered only when a child editor or Apply needs them.
            ObjectWorkspace state = ObjectWorkspace.instance;
            if (state.DraftKey != Document.DraftKey && !state.SavedDrafts.Exists(draft => draft.Key == Document.DraftKey))
            {
                state.SavedDrafts.Add(opening);
                state.Persist();
            }
        }

        /// <summary>Commits this interaction while merging independent edits from other open windows.</summary>
        /// <param name="warning">Receives a conflict or validation warning without discarding the proposal.</param>
        /// <returns>True when the complete branch was saved.</returns>
        internal bool Apply(out string warning)
        {
            // The existing section transaction validates a fresh prefab snapshot and records native Undo.
            warning = string.Empty;
            EnsureRetained();
            ObjectWorkspace combined = ScriptableObject.CreateInstance<ObjectWorkspace>();
            try
            {
                bool applied = false;
                string result = string.Empty;
                if (!ObjectWorkspaceDrafts.Edit(ObjectWorkspace.instance, Document.DraftKey, () =>
                {
                    ObjectWorkspace retained = ObjectWorkspace.instance;
                    ObjectWorkspaceDraft.Capture(retained).Restore(combined);
                    string[] paths = ObjectSectionChanges.Pending(Document);
                    ObjectSectionChanges.Combine(opening, Document, combined, paths);
                    applied = ObjectSectionChanges.Apply(combined, retained, paths, out result);
                    if (applied)
                        ReadOpening(retained);
                }))
                    result = "The original interaction is no longer available.";
                warning = result;
                Refresh();
                ObjectsLogicStudioWindow.RefreshOpen();
                return applied;
            }
            catch (Exception exception)
            {
                warning = exception.Message;
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(combined);
            }
        }

        /// <summary>Reloads the applied interaction while preserving other interactions and Scene Setup.</summary>
        internal void Reload()
        {
            // Discard is explicit and limited to the settings represented by this branch.
            EnsureRetained();
            ObjectWorkspaceDrafts.Edit(ObjectWorkspace.instance, Document.DraftKey, () =>
            {
                ObjectWorkspace retained = ObjectWorkspace.instance;
                ObjectSectionChanges.Reload(retained, ObjectSectionChanges.Pending(Document));
                ReadOpening(retained);
            });
            Refresh();
            ObjectsLogicStudioWindow.RefreshOpen();
        }

        /// <summary>Rebases controls after a successful branch or child-layout transaction.</summary>
        /// <param name="retained">Latest shared interaction draft.</param>
        internal void ReadOpening(ObjectWorkspace retained)
        {
            // Applying this branch ends all of its local edits, so its whole snapshot can be refreshed.
            opening = ObjectWorkspaceDraft.Capture(retained);
            opening.Restore(Document);
            Document.GroupPresets = ObjectWorkspace.Copy(retained.GroupPresets);
        }

        /// <summary>Advances a layout's comparison snapshot while preserving the branch's other local edits.</summary>
        /// <param name="retained">Shared proposal after the child layout was saved.</param>
        internal void Rebase(ObjectWorkspace retained)
        {
            // The layout transaction has already copied its saved fields and baselines into Document.
            opening = ObjectWorkspaceDraft.Capture(retained);
        }

        #endregion
        #endregion
    }
}
