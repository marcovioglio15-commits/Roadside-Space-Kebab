using System;
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

        #endregion

        #region State

        private SerializedObject data;
        private string status = string.Empty;
        private readonly ObjectFieldFocus fieldFocus = new ObjectFieldFocus();
        private bool focusPending;
        private bool pendingChanges;

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
        internal static void Open(ObjectWorkspace state, ObjectGroupInfo settings, string field = null)
        {
            foreach (ObjectGroupWindow existing in Resources.FindObjectsOfTypeAll<ObjectGroupWindow>())
                if (existing.sourceKey == state.DraftKey && existing.group?.Key == settings.Key && existing.TryFocus(field))
                    return;
            ObjectGroupWindow window = CreateInstance<ObjectGroupWindow>();
            window.sourceKey = state.DraftKey;
            window.group = ObjectWorkspace.Copy(settings);
            window.document = CreateInstance<ObjectWorkspace>();
            window.document.hideFlags = HideFlags.HideAndDontSave;
            ObjectWorkspaceDraft.Capture(state).Restore(window.document);
            window.baseline = ObjectGroupPresetStore.Stamp(state, settings);
            window.preset = ObjectGroupPresetStore.Assignment(state, settings)?.Preset;
            window.originalPreset = window.preset;
            window.presetBaseline = window.preset != null ? ObjectGroupPresetStore.AssetStamp(window.preset) : string.Empty;
            window.data = new SerializedObject(window.document);
            window.titleContent = new GUIContent(ObjectInteractionCatalog.Name(ObjectWorkspaceDrafts.Selected(state)) + " · " + settings.Title);
            window.minSize = new Vector2(370f, 260f);
            window.SetFocus(field);
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
            Undo.undoRedoPerformed += RefreshChanges;
            StudioFieldMenu.Changed += FieldChanged;
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
            data?.Dispose();
            data = null;
        }

        /// <summary>Releases the detached proposal when the editor is deliberately closed.</summary>
        private void OnDestroy()
        {
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
                sample = (ObjectInteraction)EditorGUILayout.ObjectField(new GUIContent("Sample interaction", "Choose a matching interaction to preview object links."), sample,
                    ObjectStructureChanges.ResolveType(group.TypeName) ?? typeof(ObjectInteraction), true);
                if (EditorGUI.EndChangeCheck() && sample != null)
                    Run(LoadSample);
            }
            using (new EditorGUI.DisabledScope(locked))
                DrawPreset();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (!string.IsNullOrEmpty(focusPath))
                EditorGUILayout.LabelField("Selected: " + ObjectNames.NicifyVariableName(focusPath.Substring(focusPath.LastIndexOf('.') + 1)), EditorStyles.miniLabel);
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
            using (new EditorGUI.DisabledScope(locked || !CanEdit || !HasChanges))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent(assetOnly ? "Save preset" : "Apply to draft", assetOnly ? "Save this settings group." : "Keep this group's changes. Apply in the main window saves the object.")))
                {
                    if (assetOnly)
                        Run(UpdatePreset);
                    else
                        Apply();
                }
                if (GUILayout.Button(new GUIContent("Discard", "Reload this group from the object's draft.")))
                    Reload();
            }
        }

        /// <summary>Edits the group preset source without changing another settings group.</summary>
        private void DrawPreset()
        {
            EditorGUI.BeginChangeCheck();
            preset = (ObjectGroupPreset)EditorGUILayout.ObjectField(new GUIContent("Preset", "Choose a preset for this group."), preset, typeof(ObjectGroupPreset), false);
            if (EditorGUI.EndChangeCheck())
                presetBaseline = preset != null ? ObjectGroupPresetStore.AssetStamp(preset) : string.Empty;
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
            pendingChanges = document != null && group != null
                && (ObjectGroupPresetStore.Stamp(document, group) != baseline || !assetOnly && preset != originalPreset);
            Repaint();
        }

        /// <summary>Refreshes a group changed through a delayed field context menu.</summary>
        /// <param name="edited">Document receiving the edit.</param>
        private void FieldChanged(UnityEngine.Object edited)
        {
            if (edited == document)
                RefreshChanges();
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
        private bool TryFocus(string field = null)
        {
            if (document == null)
            {
                Close();
                return false;
            }
            SetFocus(field);
            Focus();
            return true;
        }

        /// <summary>Locates a requested setting without changing its value or enabling any option.</summary>
        /// <param name="path">Field path or empty for normal group editing.</param>
        private void SetFocus(string path)
        {
            focusPath = path ?? string.Empty;
            focusPending = !string.IsNullOrEmpty(focusPath);
            if (focusPending)
                document.Navigation.Collapsed.RemoveAll(key => key.StartsWith("Group.", StringComparison.Ordinal));
            if (data != null)
                fieldFocus.Configure(data, focusPath);
        }

        /// <summary>Commits only this group after checking that its source values have not changed elsewhere.</summary>
        private void Apply()
        {
            Run(() =>
            {
                bool applied = false;
                ObjectWorkspace state = ObjectWorkspace.instance;
                if (!ObjectWorkspaceDrafts.Edit(state, sourceKey, () =>
                {
                    if (ObjectGroupPresetStore.Stamp(state, group) != baseline)
                        throw new InvalidOperationException("This group changed in another window. Discard to reload it.");
                    using SerializedObject destination = new SerializedObject(state);
                    data.Update();
                    Undo.RecordObject(state, "Apply " + group.Title);
                    foreach (string path in group.Paths)
                        new StudioPropertyValue(data.FindProperty(path)).Apply(destination.FindProperty(path));
                    destination.ApplyModifiedProperties();
                    ObjectGroupPresetStore.Assign(state, group, preset);
                    baseline = ObjectGroupPresetStore.Stamp(state, group);
                    originalPreset = preset;
                    applied = true;
                }) || !applied)
                    throw new InvalidOperationException("The interaction is no longer available. Reopen it from the object.");
                status = "Group added to the object draft.";
                ObjectsLogicStudioWindow.RefreshOpen();
            });
        }

        /// <summary>Restores the latest group values without discarding any other object proposal.</summary>
        private void Reload()
        {
            if (assetOnly)
            {
                Run(() =>
                {
                    ObjectPresetContext.Apply(preset, document);
                    ObjectGroupPresetStore.Import(preset, document, group);
                    baseline = ObjectGroupPresetStore.Stamp(document, group);
                });
                return;
            }
            Run(() =>
            {
                ObjectWorkspace state = ObjectWorkspace.instance;
                if (!ObjectWorkspaceDrafts.Edit(state, sourceKey, () =>
                {
                    ObjectPresetContext.Apply(ObjectPresetContext.Capture(state), document);
                    using SerializedObject source = new SerializedObject(state);
                    data.Update();
                    foreach (string path in group.Paths)
                        new StudioPropertyValue(source.FindProperty(path)).Apply(data.FindProperty(path));
                    data.ApplyModifiedProperties();
                    baseline = ObjectGroupPresetStore.Stamp(state, group);
                    preset = originalPreset = ObjectGroupPresetStore.Assignment(state, group)?.Preset;
                }))
                    throw new InvalidOperationException("The interaction is no longer available.");
                status = string.Empty;
            });
        }

        /// <summary>Updates an explicitly selected preset only if its saved values still match the opened version.</summary>
        private void UpdatePreset()
        {
            if (!canSavePreset)
                throw new InvalidOperationException("Choose a sample interaction to resolve this preset's object links first.");
            if (preset.Group.Key != group.Key)
                throw new InvalidOperationException("Choose a preset for this group.");
            if (ObjectGroupPresetStore.AssetStamp(preset) != presetBaseline)
                throw new InvalidOperationException("This preset changed elsewhere. Select it again before updating.");
            Undo.RecordObject(preset, "Update group preset");
            preset.Values = ObjectGroupPresetStore.Capture(document, group);
            ObjectPresetContext.Update(preset, document);
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssetIfDirty(preset);
            presetBaseline = ObjectGroupPresetStore.AssetStamp(preset);
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
