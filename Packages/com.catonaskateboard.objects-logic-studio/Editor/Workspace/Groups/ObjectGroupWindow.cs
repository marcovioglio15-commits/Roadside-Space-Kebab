using System;
using CatOnASkateboard.StudioIdentity.Editor;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits one detached settings group with independent preset and Apply actions.</summary>
    internal sealed class ObjectGroupWindow : EditorWindow
    {
        #region Fields

        [Header("Group")]
        [Tooltip("Interaction draft receiving these changes.")]
        [SerializeField]
        private string sourceKey;
        [Tooltip("Settings group opened in this window.")]
        [SerializeField]
        private ObjectGroupInfo group;
        [Tooltip("Independent working copy for this group.")]
        [SerializeField]
        private ObjectWorkspace document;
        [Tooltip("Group values before this window was opened.")]
        [SerializeField]
        private string baseline;
        [Tooltip("Retained proposal used to merge changes made while this window is open.")]
        [SerializeReference]
        private ObjectWorkspaceDraft opening;
        [Tooltip("Preset linked to this group before editing.")]
        [SerializeField]
        private ObjectGroupPreset originalPreset;
        [Tooltip("Preset selected for this group.")]
        [SerializeField]
        private ObjectGroupPreset preset;
        [Tooltip("Preset values before editing.")]
        [SerializeField]
        private string presetBaseline;
        [Tooltip("Scroll position in this group editor.")]
        [SerializeField]
        private Vector2 scroll;
        [Tooltip("Edit the saved preset directly instead of an object's draft.")]
        [SerializeField]
        private bool assetOnly;
        [Tooltip("Temporary object used to preview this preset.")]
        [SerializeField]
        private GameObject preview;
        [Tooltip("Interaction used to preview this preset's object links.")]
        [SerializeField]
        private ObjectInteraction sample;
        [Tooltip("Whether the preset's object links are available.")]
        [SerializeField]
        private bool canSavePreset = true;
        [Tooltip("Setting selected from the tree preview.")]
        [SerializeField]
        private string focusPath = string.Empty;
        [Tooltip("Caption selected from the settings tree.")]
        [SerializeField]
        private string focusLabel = string.Empty;

        #endregion

        #region State

        private SerializedObject data;
        private string status = string.Empty;
        private readonly ObjectFieldFocus fieldFocus = new ObjectFieldFocus();
        private bool focusPending;
        private bool pendingChanges;
        private Func<StudioPresetContext> exchangeContext;

        #endregion

        #region Properties

        /// <summary>Whether the detached proposal still belongs to an existing interaction or preset preview.</summary>
        internal bool CanEdit => document != null && ObjectWorkspaceDrafts.Selected(document) != null;

        /// <summary>Whether group values or its preset link differ from the last Apply or Discard.</summary>
        internal bool HasChanges => pendingChanges;

        #endregion

        #region Methods

        #region Lifetime

        /// <summary>Reuses an already open group window or creates one independent proposal.</summary>
        /// <param name="state">Persistent workspace supplying the current draft.</param>
        /// <param name="settings">Group to edit.</param>
        /// <param name="field">Optional setting selected from the tree.</param>
        /// <param name="label">Selected field or section caption.</param>
        internal static void Open(ObjectWorkspace state, ObjectGroupInfo settings, string field = null, string label = null)
        {
            foreach (ObjectGroupWindow existing in Resources.FindObjectsOfTypeAll<ObjectGroupWindow>())
                if (existing.sourceKey == state.DraftKey && existing.group?.Key == settings.Key && existing.TryFocus(field, label))
                    return;
            ObjectGroupWindow window = CreateInstance<ObjectGroupWindow>();
            window.sourceKey = state.DraftKey;
            window.group = ObjectWorkspace.Copy(settings);
            window.document = CreateInstance<ObjectWorkspace>();
            window.opening = ObjectWorkspaceDraft.Capture(state);
            window.opening.Restore(window.document);
            window.baseline = ObjectGroupPresetStore.Stamp(state, settings);
            window.preset = ObjectGroupPresetStore.Assignment(state, settings)?.Preset;
            window.originalPreset = window.preset;
            window.presetBaseline = window.preset != null ? ObjectGroupPresetStore.AssetStamp(window.preset) : string.Empty;
            window.data = new SerializedObject(window.document);
            window.titleContent = new GUIContent(ObjectInteractionCatalog.Name(ObjectWorkspaceDrafts.Selected(state)) + " · " + settings.Title);
            window.minSize = new Vector2(370f, 260f);
            window.SetFocus(field, label);
            window.RefreshChanges();
            state.Navigation.Recent.Remove(settings.Key);
            state.Navigation.Recent.Insert(0, settings.Key);
            if (state.Navigation.Recent.Count > 12)
                state.Navigation.Recent.RemoveAt(state.Navigation.Recent.Count - 1);
            state.Persist();
            window.Show();
        }

        /// <summary>Reconnects the serialized wrapper after a script reload.</summary>
        private void OnEnable()
        {
            exchangeContext = CaptureExchangeContext;
            Undo.undoRedoPerformed += RefreshChanges;
            StudioFieldMenu.Changed += FieldChanged;
            ObjectFlagSelector.SelectionChanged += FieldChanged;
            if (document != null)
            {
                data = new SerializedObject(document);
                fieldFocus.Configure(data, focusPath);
                RefreshChanges();
            }
        }

        /// <summary>Releases only the temporary wrapper during a window or script reload.</summary>
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RefreshChanges;
            StudioFieldMenu.Changed -= FieldChanged;
            ObjectFlagSelector.SelectionChanged -= FieldChanged;
            data?.Dispose();
            data = null;
        }

        /// <summary>Releases the detached proposal when the editor is deliberately closed.</summary>
        private void OnDestroy()
        {
            if (assetOnly && ObjectLayoutSource.KeepPreset(document, preview, preset, group, presetBaseline, canSavePreset))
                return;
            if (document != null)
                DestroyImmediate(document);
            if (preview != null)
            {
                ObjectPreviewIdentity.Unregister(preview);
                DestroyImmediate(preview);
            }
        }

        /// <summary>Opens a reusable group preset without requiring a selected gameplay object.</summary>
        /// <param name="preset">Group preset to edit.</param>
        internal static void OpenPreset(ObjectGroupPreset preset)
        {
            foreach (ObjectGroupWindow existing in Resources.FindObjectsOfTypeAll<ObjectGroupWindow>())
                if (existing.assetOnly && existing.preset == preset && existing.TryFocus())
                    return;
            Type type = ObjectStructureChanges.ResolveType(preset.Group.TypeName);
            if (type == null)
                return;
            ObjectGroupWindow window = CreateInstance<ObjectGroupWindow>();
            window.assetOnly = true;
            window.preset = preset;
            window.group = ObjectWorkspace.Copy(preset.Group);
            window.presetBaseline = ObjectGroupPresetStore.AssetStamp(preset);
            window.preview = ObjectPresetCapture.Preview(null);
            ObjectInteraction feature = (ObjectInteraction)window.preview.AddComponent(type);
            feature.enabled = false;
            ObjectPreviewIdentity.Register(window.preview);
            window.document = ObjectPresetCapture.Document(feature);
            window.data = new SerializedObject(window.document);
            try
            {
                ObjectPresetContext.Apply(preset, window.document);
                ObjectGroupPresetStore.Import(preset, window.document, preset.Group);
            }
            catch (Exception exception)
            {
                window.canSavePreset = false;
                window.status = exception.Message + " Open this group from a matching object to edit its links.";
            }
            window.baseline = ObjectGroupPresetStore.Stamp(window.document, window.group);
            window.titleContent = new GUIContent(preset.name);
            window.minSize = new Vector2(370f, 260f);
            window.RefreshChanges();
            window.Show();
        }

        #endregion

        #region Drawing

        /// <summary>Shows one group's conditional controls and simple preset commands.</summary>
        private void OnGUI()
        {
            if (document == null || group == null)
                return;
            EditorGUI.BeginChangeCheck();
            data ??= new SerializedObject(document);
            using ObjectStudioFieldLayout layout = new ObjectStudioFieldLayout(180f);
            using StudioFieldRenderScope render = new StudioFieldRenderScope(ObjectFieldFocus.Draw);
            bool locked = EditorApplication.isPlayingOrWillChangePlaymode;
            EditorGUILayout.LabelField(group.Title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(document.Target.ObjectName + " / " + group.Title, EditorStyles.miniLabel);
            if (assetOnly)
            {
                EditorGUI.BeginChangeCheck();
                ObjectInteraction selected = (ObjectInteraction)StudioFieldGUI.ObjectField(new GUIContent("Sample interaction", "Choose a matching interaction to preview object links."), sample,
                    ObjectStructureChanges.ResolveType(group.TypeName) ?? typeof(ObjectInteraction), true);
                if (EditorGUI.EndChangeCheck() && selected != null)
                {
                    if (ObjectLayoutSource.PendingPreset(document) != null)
                        status = "Apply or discard the open layout before changing the sample.";
                    else
                    {
                        sample = selected;
                        Run(LoadSample);
                    }
                }
            }
            using (new EditorGUI.DisabledScope(locked))
                DrawPreset();
            Rect selection = default;
            if (!string.IsNullOrEmpty(focusLabel))
            {
                selection = EditorGUILayout.GetControlRect(false);
                EditorGUI.LabelField(selection, "Selected · " + focusLabel, EditorStyles.miniLabel);
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            string requirement = ObjectGroupAvailability.Requirement(data, group, out string control);
            if (!string.IsNullOrEmpty(requirement))
            {
                EditorGUILayout.LabelField(requirement, EditorStyles.wordWrappedMiniLabel);
                if (!assetOnly && GUILayout.Button(new GUIContent("Open prerequisite", "Open the group that controls this setting.")))
                    ObjectWorkspaceDrafts.Edit(ObjectWorkspace.instance, sourceKey, () =>
                    {
                        using SerializedObject source = new SerializedObject(ObjectWorkspace.instance);
                        ObjectGroupInfo prerequisite = ObjectInteractionPanel.Groups(ObjectWorkspace.instance, source).Find(candidate => candidate.Contains(control));
                        if (prerequisite != null)
                            Open(ObjectWorkspace.instance, prerequisite);
                    });
            }
            using (new EditorGUI.DisabledScope(locked || !CanEdit))
            using (ObjectFieldFocus.Scope focus = new ObjectFieldFocus.Scope(fieldFocus))
            using (ObjectGroupScope scope = new ObjectGroupScope(group))
                ObjectInteractionPanel.Draw(document, data);
            EditorGUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(focusLabel) && !fieldFocus.Found)
                ObjectFieldFocus.Accent(selection);
            if (focusPending && fieldFocus.Found)
            {
                scroll.y = Mathf.Max(0f, fieldFocus.Rect.y - 30f);
                GUI.FocusControl("ObjectSetting." + focusPath);
                focusPending = false;
                Repaint();
            }
            if (!string.IsNullOrEmpty(status))
                EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel);
            if (EditorGUI.EndChangeCheck())
                RefreshChanges();
            using (new EditorGUI.DisabledScope(locked || !HasChanges))
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!CanEdit))
                    if (GUILayout.Button(new GUIContent("Apply", "Save this settings group now.")))
                        ApplyChanges();
                if (GUILayout.Button(new GUIContent("Discard", "Reload the applied settings for this group.")))
                    Reload();
            }
        }

        /// <summary>Exposes typed group settings instead of portable storage records.</summary>
        /// <returns>A context restricted to this group's existing field paths.</returns>
        private StudioPresetContext CaptureExchangeContext()
        {
            ObjectGroupPreset source = preset;
            string key = group.Key;
            return StudioPresetContext.ForDraft(source, document, this, ExchangeChanged, group.Paths, key,
                () => preset == source && group.Key == key);
        }

        /// <summary>Refreshes cached serialized controls after an external preset action updates the document.</summary>
        private void ExchangeChanged()
        {
            data?.Update();
            RefreshChanges();
        }

        /// <summary>Edits the group preset source without changing another settings group.</summary>
        private void DrawPreset()
        {
            EditorGUI.BeginChangeCheck();
            preset = (ObjectGroupPreset)StudioFieldGUI.ObjectField(new GUIContent("Preset", "Choose a preset for this group."), preset, typeof(ObjectGroupPreset), false);
            if (EditorGUI.EndChangeCheck())
                presetBaseline = preset != null ? ObjectGroupPresetStore.AssetStamp(preset) : string.Empty;
            StudioPresetActions.Draw(preset, exchangeContext);
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
            using (new EditorGUI.DisabledScope(preset == null))
            {
                if (GUILayout.Button(new GUIContent("Import", "Copy this preset into the group.")))
                    Run(() => { ObjectGroupPresetStore.Import(preset, document, group); status = "Preset copied to this group."; });
                if (GUILayout.Button(new GUIContent("Update", "Save this group into the selected preset.")))
                    Run(UpdatePreset);
                if (GUILayout.Button(new GUIContent("Compare", "Check which fields differ from the preset.")))
                    ObjectGroupCompareWindow.Open(document, group, preset);
            }
            using (new EditorGUI.DisabledScope(!canSavePreset))
            if (GUILayout.Button(new GUIContent("Save as", "Save a new preset for this group.")))
            {
                ObjectGroupPreset saved = ObjectGroupPresetStore.SaveAs(document, group);
                if (saved != null)
                {
                    preset = saved;
                    presetBaseline = ObjectGroupPresetStore.AssetStamp(preset);
                    status = "Group preset saved.";
                    RefreshChanges();
                }
            }
        }

        #endregion

        #region Transactions

        /// <summary>Updates buttons after fields, Undo or an independent layout preview change this document.</summary>
        internal void RefreshChanges()
        {
            // Compare values only after an edit; repaint and navigation do not allocate snapshots.
            pendingChanges = document != null && group != null && (assetOnly
                ? ObjectGroupPresetStore.Stamp(document, group) != baseline
                : preset != originalPreset || ObjectSectionChanges.HasPending(document, group.Paths));
            hasUnsavedChanges = pendingChanges;
            saveChangesMessage = "Apply or discard the unsaved changes in " + group?.Title + ".";
            Repaint();
        }

        /// <summary>Refreshes a group changed through a delayed field context menu.</summary>
        /// <param name="edited">Document receiving the edit.</param>
        private void FieldChanged(UnityEngine.Object edited)
        {
            if (edited == document)
                RefreshChanges();
        }

        /// <summary>Checks whether a detached group still has edits for a component being removed.</summary>
        /// <param name="prefabGuid">Prefab containing the component.</param>
        /// <param name="componentId">Exact component, or zero for the whole prefab.</param>
        /// <returns>True when a group would lose its source.</returns>
        internal static bool HasPendingFor(string prefabGuid, long componentId)
        {
            foreach (ObjectGroupWindow window in Resources.FindObjectsOfTypeAll<ObjectGroupWindow>())
                if (!window.assetOnly && window.HasChanges && window.document != null
                    && window.document.Target.PrefabGuid == prefabGuid && (componentId == 0 || window.document.Navigation.SelectedId == componentId))
                    return true;
            return false;
        }

        /// <summary>Reconnects group controls after a layout preview writes into their detached document.</summary>
        /// <param name="changed">Document updated by a layout preview.</param>
        internal static void RefreshDocument(ObjectWorkspace changed)
        {
            foreach (ObjectGroupWindow window in Resources.FindObjectsOfTypeAll<ObjectGroupWindow>())
                if (window.document == changed)
                {
                    window.data?.Update();
                    window.RefreshChanges();
                }
            ObjectsLogicStudioWindow.RefreshOpen();
        }

        /// <summary>Reuses a live proposal or closes a stale window restored without its working copy.</summary>
        /// <param name="field">Optional setting selected from the tree.</param>
        /// <returns>True when an existing proposal received focus.</returns>
        /// <param name="label">Selected field or section caption.</param>
        private bool TryFocus(string field = null, string label = null)
        {
            if (document == null)
            {
                Close();
                return false;
            }
            SetFocus(field, label);
            Focus();
            return true;
        }

        /// <summary>Locates a requested setting without changing its value or enabling any option.</summary>
        /// <param name="path">Field path or empty for normal group editing.</param>
        /// <param name="label">Selected field or section caption.</param>
        private void SetFocus(string path, string label)
        {
            focusPath = path ?? string.Empty;
            focusLabel = label ?? (focusPath.Length > 0 ? ObjectNames.NicifyVariableName(focusPath.Substring(focusPath.LastIndexOf('.') + 1)) : string.Empty);
            focusPending = !string.IsNullOrEmpty(focusPath);
            if (focusPending)
                document.Navigation.Collapsed.RemoveAll(key => key.StartsWith("Group.", StringComparison.Ordinal));
            if (data != null)
                fieldFocus.Configure(data, focusPath);
        }

        /// <summary>Commits this group's values and preset link while retaining every other proposal.</summary>
        private void ApplyChanges()
        {
            if (assetOnly)
            {
                Run(UpdatePreset);
                return;
            }
            Run(() =>
            {
                ObjectWorkspace state = ObjectWorkspace.instance;
                if (!ObjectWorkspaceDrafts.Edit(state, sourceKey, () =>
                {
                    if (ObjectGroupPresetStore.Stamp(state, group) == baseline)
                    {
                        if (!ObjectSectionChanges.Apply(document, state, group.Paths, out string warning))
                            throw new InvalidOperationException(warning);
                    }
                    else
                        ApplyReconciled(state);
                    ObjectGroupPresetStore.Assign(state, group, preset);
                    ReadOpening(state);
                    originalPreset = preset;
                }))
                    throw new InvalidOperationException("The interaction is no longer available.");
                status = "Group saved.";
                ObjectsLogicStudioWindow.RefreshOpen();
            });
        }

        /// <summary>Merges the detached edits with independent changes made to its retained draft.</summary>
        /// <param name="state">Latest retained interaction proposal.</param>
        private void ApplyReconciled(ObjectWorkspace state)
        {
            if (opening == null)
                throw new InvalidOperationException("This group changed in another window. Review its retained draft or discard to reload it.");
            ObjectWorkspace combined = CreateInstance<ObjectWorkspace>();
            try
            {
                ObjectWorkspaceDraft.Capture(state).Restore(combined);
                ObjectSectionChanges.Combine(opening, document, combined, group.Paths);
                if (!ObjectSectionChanges.Apply(combined, state, group.Paths, out string warning))
                    throw new InvalidOperationException(warning);
                ObjectSectionChanges.Synchronize(combined, document, group.Paths);
            }
            finally
            {
                DestroyImmediate(combined);
            }
        }

        /// <summary>Retains the latest shared proposal after applying or explicitly reloading this group.</summary>
        /// <param name="state">Workspace holding the latest retained values.</param>
        private void ReadOpening(ObjectWorkspace state)
        {
            opening = ObjectWorkspaceDraft.Capture(state);
            baseline = ObjectGroupPresetStore.Stamp(state, group);
        }

        /// <summary>Reloads this group from the prefab or preset without discarding other sections.</summary>
        private void Reload()
        {
            if (assetOnly)
            {
                Run(() =>
                {
                    ObjectPresetContext.Apply(preset, document);
                    ObjectGroupPresetStore.Import(preset, document, group);
                    baseline = ObjectGroupPresetStore.Stamp(document, group);
                    presetBaseline = ObjectGroupPresetStore.AssetStamp(preset);
                    status = string.Empty;
                });
                return;
            }
            Run(() =>
            {
                ObjectWorkspace state = ObjectWorkspace.instance;
                if (CanEdit && ObjectWorkspaceDrafts.Edit(state, sourceKey, () =>
                {
                    ObjectSectionChanges.Reload(state, group.Paths);
                    ObjectSectionChanges.Synchronize(state, document, group.Paths);
                    ReadOpening(state);
                    preset = originalPreset = ObjectGroupPresetStore.Assignment(state, group)?.Preset;
                }))
                {
                    status = string.Empty;
                    ObjectsLogicStudioWindow.RefreshOpen();
                    return;
                }
                // A deleted source can still be closed after explicitly discarding its detached changes.
                CloseAfterDiscard();
            });
        }

        /// <summary>Releases a draft whose source was removed after its window opened.</summary>
        private void CloseAfterDiscard()
        {
            data?.Dispose();
            data = null;
            DestroyImmediate(document);
            document = null;
            base.DiscardChanges();
        }

        /// <summary>Handles Unity's close prompt and aborts closing if validation rejects Apply.</summary>
        public override void SaveChanges()
        {
            ApplyChanges();
            if (HasChanges)
                throw new InvalidOperationException(status);
            base.SaveChanges();
        }

        /// <summary>Handles Unity's close prompt without writing the other sections.</summary>
        public override void DiscardChanges()
        {
            Reload();
            if (HasChanges)
                throw new InvalidOperationException(status);
            base.DiscardChanges();
        }

        /// <summary>Updates a group after a child preview saved only its own layout.</summary>
        /// <param name="changed">Document that launched the preview.</param>
        /// <param name="retained">Current proposal in the main workspace.</param>
        internal static void LayoutSaved(ObjectWorkspace changed, ObjectWorkspace retained)
        {
            foreach (ObjectGroupWindow window in Resources.FindObjectsOfTypeAll<ObjectGroupWindow>())
                if (window.document == changed)
                {
                    window.ReadOpening(retained);
                    window.data?.Update();
                    window.RefreshChanges();
                }
        }

        /// <summary>Saves a standalone preset preview’s layout without applying the rest of the preset window.</summary>
        /// <param name="document">Preset working copy which opened the layout.</param>
        /// <param name="proposal">Working copy with the layout change.</param>
        /// <param name="paths">Fields owned by the layout.</param>
        /// <param name="fillThreshold">Optional Actual Item Fill step to update.</param>
        internal static void SavePresetLayout(ObjectWorkspace document, ObjectWorkspace proposal, string[] paths, int fillThreshold)
        {
            foreach (ObjectGroupWindow window in Resources.FindObjectsOfTypeAll<ObjectGroupWindow>())
                if (window.document == document && window.assetOnly)
                {
                    window.presetBaseline = ObjectGroupPresetStore.WriteLayout(window.preset, window.group, document, proposal,
                        paths, fillThreshold, window.presetBaseline, window.canSavePreset, out window.baseline);
                    return;
                }
            throw new InvalidOperationException("Reopen the layout from its preset window.");
        }

        /// <summary>Updates an explicitly selected preset only if its saved values still match the opened version.</summary>
        private void UpdatePreset()
        {
            presetBaseline = ObjectGroupPresetStore.Write(preset, group, document, presetBaseline, canSavePreset);
            if (assetOnly)
                baseline = ObjectGroupPresetStore.Stamp(document, group);
            status = "Group preset updated.";
        }

        /// <summary>Rebuilds an isolated sample without changing the selected interaction or its prefab.</summary>
        private void LoadSample()
        {
            string path = AnimationUtility.CalculateTransformPath(sample.transform, sample.transform.root);
            Type type = sample.GetType();
            int index = Array.IndexOf(sample.GetComponents(type), sample);
            data?.Dispose();
            if (document != null)
                DestroyImmediate(document);
            if (preview != null)
            {
                ObjectPreviewIdentity.Unregister(preview);
                DestroyImmediate(preview);
            }
            preview = ObjectPresetCapture.Preview(sample.transform.root.gameObject);
            Transform node = ObjectPresetHierarchy.Find(preview.transform, path);
            ObjectInteraction feature = node != null && index >= 0 ? node.GetComponents(type)[index] as ObjectInteraction : null;
            if (feature == null)
                throw new InvalidOperationException("Choose an unambiguous sample interaction.");
            document = ObjectPresetCapture.Document(feature);
            data = new SerializedObject(document);
            canSavePreset = false;
            ObjectPresetContext.Apply(preset, document);
            ObjectGroupPresetStore.Import(preset, document, group);
            baseline = ObjectGroupPresetStore.Stamp(document, group);
            canSavePreset = true;
            status = string.Empty;
        }

        /// <summary>Keeps validation errors visible without closing or clearing the group proposal.</summary>
        /// <param name="operation">Explicit edit or preset command.</param>
        private void Run(Action operation)
        {
            try
            {
                // Finish pending serialized edits before commands capture or replace this proposal.
                data?.ApplyModifiedProperties();
                operation();
            }
            catch (Exception exception)
            {
                status = exception.Message;
            }
            finally
            {
                data?.Update();
                RefreshChanges();
            }
        }

        #endregion

        #endregion
    }
}
