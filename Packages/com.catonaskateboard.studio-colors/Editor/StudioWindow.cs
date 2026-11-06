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
        [Tooltip("Recoverable proposal and source conflict checks shared by Studio asset editors.")]
        [SerializeField]
        private StudioAssetSession session = new StudioAssetSession();
        [Tooltip("Visible tab index; independent of draft lifetime.")]
        [SerializeField]
        private int tab;
        private SerializedObject serialized;
        private Label status;
        private Button apply;
        private Button discard;
        private ObjectField selector;
        private readonly List<string> warnings = new List<string>();
        #endregion

        #region Properties
        protected abstract Type PresetType { get; }
        protected abstract string[] Tabs { get; }
        protected ScriptableObject Draft => session.Draft;
        protected ScriptableObject Source => session.Source;
        protected int Tab => tab;
        protected bool Pending => session.HasChanges;
        #endregion

        #region Methods
        #region Lifecycle
        /// <summary>Restores draft editing after domain reload and subscribes to Undo.</summary>
        protected virtual void OnEnable()
        {
            // A small minimum width keeps the window useful when docked beside Scene view.
            minSize = new Vector2(360f, 360f);
            Undo.undoRedoPerformed += HandleUndo;
            StudioFieldMenu.Changed += FieldChanged;
            EditorApplication.projectChanged += HandleUndo;
            session.Restore();
            if (session.Draft != null)
                serialized = new SerializedObject(session.Draft);
        }

        /// <summary>Refreshes Apply/Discard after a context-menu edit of the detached preset.</summary>
        /// <param name="edited">Object changed by Paste.</param>
        private void FieldChanged(UnityEngine.Object edited)
        {
            // Delayed menu callbacks occur outside the IMGUI change-check scope.
            if (edited == session.Draft)
                RefreshActions();
        }

        /// <summary>Releases callbacks without destroying a draft during script reload.</summary>
        protected virtual void OnDisable()
        {
            // Serialized draft references survive editor assembly reloads.
            Undo.undoRedoPerformed -= HandleUndo;
            session.Retain();
            EditorApplication.projectChanged -= HandleUndo;
            StudioFieldMenu.Changed -= FieldChanged;
            serialized?.Dispose();
            serialized = null;
        }

        /// <summary>Releases the transient proposal when the actual window is destroyed.</summary>
        protected virtual void OnDestroy()
        {
            // Persistent source assets are never destroyed by workspace cleanup.
            session.Retain();
            session.Release();
        }

        /// <summary>Creates a wrapping toolbar, one scrolling content area and fixed action footer.</summary>
        public void CreateGUI()
        {
            // Compact controls mirror Player Studio while sharing no player-specific code.
            rootVisualElement.Clear();
            Toolbar toolbar = new Toolbar();
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.style.height = StyleKeyword.Auto;
            toolbar.style.flexShrink = 0f;
            selector = new ObjectField { objectType = PresetType, value = session.Source, allowSceneObjects = false, tooltip = "Select the preset to edit." };
            StudioFieldMenu.Attach(selector, value => value == null || PresetType.IsInstanceOfType(value) && EditorUtility.IsPersistent(value));
            selector.style.flexGrow = 1f;
            selector.style.minWidth = 160f;
            selector.RegisterValueChangedCallback(evt => SelectPreset(evt.newValue as ScriptableObject));
            toolbar.Add(selector);
            toolbar.Add(ActionButton("New", "Create an empty project preset.", () => CreatePreset(false)));
            toolbar.Add(ActionButton("Duplicate", "Copy the selected preset to a new asset.", () => CreatePreset(true)));
            rootVisualElement.Add(toolbar);

            if (Tabs.Length > 1)
                DrawTabs();
            ScrollView content = new ScrollView(ScrollViewMode.Vertical);
            content.style.flexGrow = 1f;
            content.style.flexShrink = 1f;
            content.style.flexBasis = 0f;
            content.style.minHeight = 0f;
            content.style.paddingLeft = 6f;
            content.style.paddingRight = 6f;
            content.Add(new IMGUIContainer(() => DrawContent(content.contentViewport.contentRect.width)));
            rootVisualElement.Add(content);

            BuildFooter();
            RefreshActions();
        }

        /// <summary>Adds page navigation when the window has more than one settings page.</summary>
        private void DrawTabs()
        {
            // Keep navigation outside the scrolling fields.
            VisualElement tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.flexWrap = Wrap.Wrap;
            tabs.style.flexShrink = 0f;
            for (int index = 0; index < Tabs.Length; index++)
            {
                int requested = index;
                tabs.Add(ActionButton(Tabs[index], "Show " + Tabs[index] + ".", () => { tab = requested; Repaint(); }));
            }
            rootVisualElement.Add(tabs);
        }

        /// <summary>Reserves a padded action row and keeps long status messages available on hover.</summary>
        private void BuildFooter()
        {
            // Only the form can shrink when its foldouts grow or the window gets shorter.
            VisualElement footer = new VisualElement();
            footer.style.flexShrink = 0f;
            footer.style.paddingLeft = footer.style.paddingRight = 6f;
            footer.style.paddingTop = footer.style.paddingBottom = 5f;
            status = new Label { tooltip = "Current validation or operation result." };
            status.style.height = 18f;
            status.style.flexShrink = 0f;
            status.style.whiteSpace = WhiteSpace.NoWrap;
            status.style.overflow = Overflow.Hidden;
            status.style.textOverflow = TextOverflow.Ellipsis;
            footer.Add(status);

            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexShrink = 0f;
            apply = ActionButton("Apply", "Validate and save this proposal to the selected asset.", SaveChanges);
            discard = ActionButton("Discard", "Reload the source preset and discard pending edits.", DiscardChanges);
            apply.style.flexGrow = discard.style.flexGrow = 1f;
            apply.style.flexBasis = discard.style.flexBasis = 0f;
            apply.style.minHeight = discard.style.minHeight = 26f;
            actions.Add(apply);
            actions.Add(discard);
            footer.Add(actions);
            rootVisualElement.Add(footer);
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
        /// <param name="width">Available width inside the scrolling editor panel.</param>
        private void DrawContent(float width)
        {
            // Empty workspaces create no assets until New is clicked.
            if (session.Draft == null)
            {
                EditorGUILayout.LabelField("Select a preset or create a new one.", EditorStyles.wordWrappedLabel);
                return;
            }
            using StudioFieldLayout.WidthScope pane = new StudioFieldLayout.WidthScope(width);
            if (serialized == null || serialized.targetObject != session.Draft)
            {
                serialized?.Dispose();
                serialized = new SerializedObject(session.Draft);
            }
            serialized.UpdateIfRequiredOrScript();
            if (Tabs.Length > 1)
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
            session.Retain();
            if (session.RecoveryWarning.Length > 0)
                SetStatus(session.RecoveryWarning);
            saveChangesMessage = "Apply the pending preset changes?";
            apply?.SetEnabled(session.Source != null && Pending && !EditorApplication.isPlayingOrWillChangePlaymode);
            discard?.SetEnabled(session.Source != null);
        }

        /// <summary>Refreshes the proposal and actions after Unity Undo changes editor state.</summary>
        private void HandleUndo()
        {
            // A clean proposal follows source Undo; unfinished proposals retain conflict protection.
            if (!Pending && session.SourceChanged)
            {
                session.Refresh();
                RefreshActions();
            }
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
                selector?.SetValueWithoutNotify(session.Source);
                SetStatus("Apply or Discard before changing presets.");
                return;
            }
            serialized?.Dispose();
            serialized = null;
            session.Select(requested, out string warning);
            SetStatus(warning);
            selector?.SetValueWithoutNotify(session.Source);
            RefreshActions();
        }

        /// <summary>Creates or duplicates a preset at a user-selected project location.</summary>
        /// <param name="duplicate">Copy the current source instead of using defaults.</param>
        private void CreatePreset(bool duplicate)
        {
            // New assets never inherit unfinished edits from another preset.
            if (Pending || duplicate && session.Source == null)
            {
                SetStatus("Apply or Discard first; Duplicate also needs a selected preset.");
                return;
            }
            string path = EditorUtility.SaveFilePanelInProject("Save preset", PresetType.Name, "asset", "Choose a project asset path.");
            if (string.IsNullOrEmpty(path))
                return;
            ScriptableObject created = duplicate ? Instantiate(session.Source) : CreateInstance(PresetType);
            created.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(created, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            SelectPreset(created);
            selector?.SetValueWithoutNotify(session.Source);
        }

        /// <summary>Validates and commits the proposal while detecting concurrent source edits.</summary>
        public override void SaveChanges()
        {
            RefreshActions();
            if (!session.CanSave(out string warning))
            {
                SetStatus(warning);
                return;
            }
            warnings.Clear();
            Validate(warnings);
            if (warnings.Count > 0)
            {
                SetStatus(string.Join("\n", warnings));
                return;
            }
            if (session.Save("Apply " + PresetType.Name, out warning))
            {
                base.SaveChanges();
                SetStatus("Preset saved.");
            }
            else
                SetStatus(warning);
            RefreshActions();
        }

        /// <summary>Rebuilds the proposal from the saved source without changing the scene.</summary>
        public override void DiscardChanges()
        {
            // Explicit discard also removes the recovery record for this exact asset.
            session.Discard();
            base.DiscardChanges();
            SetStatus(string.Empty);
            RefreshActions();
            Repaint();
        }
        #endregion
        #endregion
    }
}
