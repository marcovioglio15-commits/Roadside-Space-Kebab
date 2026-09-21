using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Shares compact preset selection, draft editing and fixed actions across Studio tools.</summary>
    public abstract class StudioWindow : EditorWindow
    {
        #region Fields
        [Header("Workspace")]
        [Tooltip("Preset currently being edited.")]
        [SerializeField]
        private ScriptableObject source;
        [Tooltip("Detached proposal retained across script reloads.")]
        [SerializeField]
        private ScriptableObject draft;
        [Tooltip("Source snapshot used to detect edits made by another editor.")]
        [SerializeField]
        private string baseline;
        [Tooltip("Visible tab index; independent of draft lifetime.")]
        [SerializeField]
        private int tab;
        private SerializedObject serialized;
        private Label status;
        private Button apply;
        private Button discard;
        private ObjectField selector;
        private readonly List<string> warnings = new List<string>();
        private const HideFlags draftFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        #endregion

        #region Properties
        protected abstract Type PresetType { get; }
        protected abstract string[] Tabs { get; }
        protected ScriptableObject Draft => draft;
        protected ScriptableObject Source => source;
        protected int Tab => tab;
        protected bool Pending => draft != null && JsonUtility.ToJson(draft) != baseline;
        #endregion

        #region Methods
        #region Lifecycle
        /// <summary>Restores draft editing after domain reload and subscribes to Undo.</summary>
        protected virtual void OnEnable()
        {
            // A small minimum width keeps the window useful when docked beside Scene view.
            minSize = new Vector2(360f, 360f);
            Undo.undoRedoPerformed += HandleUndo;
            if (draft != null)
            {
                // HideAndDontSave includes NotEditable, which disables SerializedProperty controls.
                draft.hideFlags = draftFlags;
                serialized = new SerializedObject(draft);
            }
        }

        /// <summary>Releases callbacks without destroying a draft during script reload.</summary>
        protected virtual void OnDisable()
        {
            // Serialized draft references survive editor assembly reloads.
            Undo.undoRedoPerformed -= HandleUndo;
            serialized?.Dispose();
            serialized = null;
        }

        /// <summary>Releases the transient proposal when the actual window is destroyed.</summary>
        protected virtual void OnDestroy()
        {
            // Persistent source assets are never destroyed by workspace cleanup.
            if (draft != null && !AssetDatabase.Contains(draft))
                DestroyImmediate(draft);
        }

        /// <summary>Creates a wrapping toolbar, one scrolling content area and fixed action footer.</summary>
        public void CreateGUI()
        {
            // Compact controls mirror Player Studio while sharing no player-specific code.
            rootVisualElement.Clear();
            Toolbar toolbar = new Toolbar();
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.style.height = StyleKeyword.Auto;
            selector = new ObjectField { objectType = PresetType, value = source, allowSceneObjects = false, tooltip = "Select the preset to edit." };
            selector.style.flexGrow = 1f;
            selector.style.minWidth = 160f;
            selector.RegisterValueChangedCallback(evt => SelectPreset(evt.newValue as ScriptableObject));
            toolbar.Add(selector);
            toolbar.Add(ActionButton("New", "Create an empty project preset.", () => CreatePreset(false)));
            toolbar.Add(ActionButton("Duplicate", "Copy the selected preset to a new asset.", () => CreatePreset(true)));
            rootVisualElement.Add(toolbar);

            VisualElement tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.flexWrap = Wrap.Wrap;
            for (int index = 0; index < Tabs.Length; index++)
            {
                int requested = index;
                tabs.Add(ActionButton(Tabs[index], "Show " + Tabs[index] + ".", () => { tab = requested; Repaint(); }));
            }
            rootVisualElement.Add(tabs);
            ScrollView content = new ScrollView(ScrollViewMode.Vertical);
            content.style.flexGrow = 1f;
            content.style.minHeight = 0f;
            content.style.paddingLeft = 6f;
            content.style.paddingRight = 6f;
            content.Add(new IMGUIContainer(DrawContent));
            rootVisualElement.Add(content);

            // Status wraps above actions; long forms cannot push Apply off screen.
            status = new Label { tooltip = "Current validation or operation result." };
            status.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(status);
            VisualElement footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            apply = ActionButton("Apply", "Validate and save this proposal to the selected asset.", SaveChanges);
            discard = ActionButton("Discard", "Reload the source preset and discard pending edits.", DiscardChanges);
            apply.style.flexGrow = discard.style.flexGrow = 1f;
            footer.Add(apply);
            footer.Add(discard);
            rootVisualElement.Add(footer);
            RefreshActions();
        }
        #endregion

        #region Presentation
        /// <summary>Creates a tooltip-bearing control registered with Studio Colors.</summary>
        /// <param name="label">Visible action name.</param>
        /// <param name="tooltip">Operation explained on hover.</param>
        /// <param name="action">Explicit user action.</param>
        /// <returns>The configured button.</returns>
        protected Button ActionButton(string label, string tooltip, Action action)
        {
            // Stable tool and action keys permit recoloring without editing package code.
            Button button = new Button(action) { text = label, tooltip = tooltip };
            ToolColors.Register(button, GetType().Name + "." + label);
            return button;
        }

        /// <summary>Draws the active tab against a detached serialized draft.</summary>
        private void DrawContent()
        {
            // Empty workspaces create no assets until New is clicked.
            if (draft == null)
            {
                EditorGUILayout.LabelField("Select a preset or create a new one.", EditorStyles.wordWrappedLabel);
                return;
            }
            serialized ??= new SerializedObject(draft);
            serialized.UpdateIfRequiredOrScript();
            EditorGUILayout.LabelField(Tabs[tab], EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                DrawTab(serialized);
            bool guiChanged = EditorGUI.EndChangeCheck();
            if (serialized.ApplyModifiedProperties() || guiChanged)
                RefreshActions();
        }

        /// <summary>Draws the selected domain's editable fields.</summary>
        /// <param name="data">Detached preset serialized object.</param>
        protected abstract void DrawTab(SerializedObject data);

        /// <summary>Collects invalid authored values without changing them.</summary>
        /// <param name="messages">Output diagnostics shown before Apply.</param>
        protected abstract void Validate(List<string> messages);

        /// <summary>Displays a concise operation result in the fixed footer.</summary>
        /// <param name="message">Result or actionable warning.</param>
        protected void SetStatus(string message)
        {
            // The tooltip preserves the same text when the workspace is narrow.
            if (status == null)
                return;
            status.text = message;
            status.tooltip = message;
        }

        /// <summary>Updates action availability after edits, selection and Undo.</summary>
        protected void RefreshActions()
        {
            // Dirty state compares serialized values, so Undo can also clear a proposal.
            hasUnsavedChanges = Pending;
            saveChangesMessage = "Apply the pending preset changes?";
            apply?.SetEnabled(source != null && Pending && !EditorApplication.isPlayingOrWillChangePlaymode);
            discard?.SetEnabled(source != null);
        }

        /// <summary>Refreshes the proposal and actions after Unity Undo changes editor state.</summary>
        private void HandleUndo()
        {
            // A clean proposal follows source Undo; unfinished proposals retain conflict protection.
            if (!Pending && source != null)
                DiscardChanges();
            else
                RefreshActions();
            Repaint();
        }
        #endregion

        #region Presets
        /// <summary>Selects a source only after the previous proposal has been handled.</summary>
        /// <param name="requested">New source asset.</param>
        protected void SelectPreset(ScriptableObject requested)
        {
            // Preserve pending work instead of silently switching its target.
            if (Pending)
            {
                selector?.SetValueWithoutNotify(source);
                SetStatus("Apply or Discard before changing presets.");
                return;
            }
            source = requested;
            DiscardChanges();
        }

        /// <summary>Creates or duplicates a preset at a user-selected project location.</summary>
        /// <param name="duplicate">Copy the current source instead of using defaults.</param>
        private void CreatePreset(bool duplicate)
        {
            // New assets never inherit unfinished edits from another preset.
            if (Pending || duplicate && source == null)
            {
                SetStatus("Apply or Discard first; Duplicate also needs a selected preset.");
                return;
            }
            string path = EditorUtility.SaveFilePanelInProject("Save preset", PresetType.Name, "asset", "Choose a project asset path.");
            if (string.IsNullOrEmpty(path))
                return;
            ScriptableObject created = duplicate ? Instantiate(source) : CreateInstance(PresetType);
            created.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(created, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            SelectPreset(created);
            selector?.SetValueWithoutNotify(source);
        }

        /// <summary>Validates and commits the proposal while detecting concurrent source edits.</summary>
        public override void SaveChanges()
        {
            // Another Inspector must not have its source edits overwritten by a stale proposal.
            if (source == null || draft == null)
                return;
            if (JsonUtility.ToJson(source) != baseline)
            {
                SetStatus("The source changed elsewhere. Discard to reload it before applying.");
                return;
            }
            warnings.Clear();
            Validate(warnings);
            if (warnings.Count > 0)
            {
                SetStatus(string.Join("\n", warnings));
                return;
            }
            Undo.RecordObject(source, "Apply " + PresetType.Name);
            HideFlags flags = source.hideFlags;
            EditorUtility.CopySerialized(draft, source);
            source.hideFlags = flags;
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssetIfDirty(source);
            baseline = JsonUtility.ToJson(source);
            SetStatus("Preset saved.");
            RefreshActions();
        }

        /// <summary>Rebuilds the proposal from the saved source without changing the scene.</summary>
        public override void DiscardChanges()
        {
            // Destroy only our detached copy, then rebuild serialized bindings once.
            serialized?.Dispose();
            serialized = null;
            if (draft != null)
                DestroyImmediate(draft);
            draft = source != null ? Instantiate(source) : null;
            if (draft != null)
            {
                draft.name = source.name;
                draft.hideFlags = draftFlags;
                baseline = JsonUtility.ToJson(source);
                serialized = new SerializedObject(draft);
            }
            SetStatus(string.Empty);
            RefreshActions();
            Repaint();
        }
        #endregion
        #endregion
    }
}
