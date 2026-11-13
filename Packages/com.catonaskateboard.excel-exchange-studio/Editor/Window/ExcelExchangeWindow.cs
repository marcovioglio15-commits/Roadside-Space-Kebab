using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Edits contextual spreadsheet mappings and reviews imports before updating a tool.</summary>
    public sealed class ExcelExchangeWindow : EditorWindow
    {
        #region Fields

        [Header("Mapping Session")]
        [Tooltip("Recoverable changes to the exchange configuration, independent of preset values.")]
        [SerializeField]
        private StudioAssetSession session = new StudioAssetSession();
        [Tooltip("Currently visible Layout, Import, Export or Preview page.")]
        [SerializeField]
        private int page;
        private StudioPresetContext context;
        private ExcelExchangePreset[] compatible = Array.Empty<ExcelExchangePreset>();
        private string[] compatibleNames = Array.Empty<string>();
        private List<ExcelField> fields = new List<ExcelField>();
        private readonly ExcelLayoutView layout = new ExcelLayoutView();
        private readonly List<ExcelPreviewRow> exportRows = new List<ExcelPreviewRow>();
        private ExcelImportPreview preview;
        private Vector2 previewScroll;
        private string status = string.Empty;
        private string configurationStamp = string.Empty;
        private string worksheetPath;
        private string[] worksheetNames = Array.Empty<string>();
        private static readonly GUIContent[] pages =
        {
            new GUIContent("Layout", "Choose fields and spreadsheet cells."),
            new GUIContent("Import", "Select a workbook and preview incoming values."),
            new GUIContent("Export", "Choose a template and export current values."),
            new GUIContent("Preview", "Review mapped values and conversion messages.")
        };

        #endregion
        #region Properties

        private ExcelExchangePreset Configuration => session.Draft as ExcelExchangePreset;
        private bool Connected => context != null && context.IsCurrent();

        #endregion
        #region Methods
        #region Lifetime

        /// <summary>Opens the exact preset context supplied by an existing tool button.</summary>
        /// <param name="context">Current tool proposal, snapshot and import callbacks.</param>
        /// <param name="export">Whether the Export page should open first.</param>
        public static void Open(StudioPresetContext context, bool export)
        {
            ExcelExchangeWindow window = GetWindow<ExcelExchangeWindow>("Excel Exchange");
            bool firstOpen = window.context == null;
            if (window.session.HasChanges && window.Configuration != null
                && (window.Configuration.Target != context.Source || window.Configuration.Scope != context.Scope))
            {
                window.status = "Save or discard the current mapping before opening another preset.";
                window.Focus();
                return;
            }
            window.context = context;
            window.page = export ? 2 : 1;
            window.minSize = new Vector2(980f, 570f);
            window.ClearPreview();
            using (Snapshot snapshot = new Snapshot(context.Capture()))
                window.fields = ExcelFieldCatalog.Build(snapshot.Value, context.Paths);
            window.RefreshLibrary();
            if (window.session.Source is not ExcelExchangePreset current || current.Target != context.Source || current.Scope != context.Scope)
            {
                ExcelExchangePreset last = ExcelPresetLibrary.Last(context);
                window.Select(last != null && window.compatible.Contains(last) ? last : window.compatible.FirstOrDefault());
            }
            else
            {
                if (firstOpen)
                    window.layout.Select(window.Configuration);
                window.RefreshConfiguration();
            }
            window.Show();
            if (firstOpen)
            {
                Rect main = EditorGUIUtility.GetMainWindowPosition();
                window.position = new Rect(main.x + 40f, main.y + 60f,
                    Mathf.Max(980f, Mathf.Min(1440f, main.width - 80f)), Mathf.Max(570f, Mathf.Min(820f, main.height - 100f)));
            }
            window.Focus();
        }

        /// <summary>Recovers unfinished mapping edits and observes project changes.</summary>
        private void OnEnable()
        {
            layout.Changed = MappingChanged;
            session.Restore();
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.projectChanged += RefreshLibrary;
        }

        /// <summary>Retains mappings and releases preview copies before closing or recompiling.</summary>
        private void OnDisable()
        {
            session.Retain();
            Undo.undoRedoPerformed -= OnUndo;
            EditorApplication.projectChanged -= RefreshLibrary;
            ClearPreview();
        }

        /// <summary>Releases only the detached configuration copy.</summary>
        private void OnDestroy()
        {
            session.Release();
        }

        /// <summary>Invalidates previous previews after native Undo or Redo.</summary>
        private void OnUndo()
        {
            session.Retain();
            RefreshConfiguration();
            Repaint();
        }

        /// <summary>Refreshes worksheet choices once when returning from an external workbook edit.</summary>
        private void OnFocus()
        {
            worksheetPath = null;
        }

        #endregion
        #region Drawing

        /// <summary>Shows source identity, compatible mappings and the selected workflow.</summary>
        private void OnGUI()
        {
            if (!Connected)
            {
                EditorGUILayout.LabelField("Open Import With Excell or Export With Excell below the preset in its tool.", EditorStyles.wordWrappedLabel);
                if (session.HasChanges)
                    EditorGUILayout.LabelField("Your pending mapping changes are retained.", EditorStyles.wordWrappedMiniLabel);
                return;
            }
            using (new EditorGUI.DisabledScope(true))
                StudioFieldGUI.ObjectField(new GUIContent("Preset", "Exact preset linked to this exchange session."), context.Source, typeof(ScriptableObject), false);
            DrawSelection();
            if (Configuration == null)
            {
                EditorGUILayout.LabelField("Create a mapping for this preset to choose fields and spreadsheet cells.", EditorStyles.wordWrappedLabel);
                DrawStatus();
                return;
            }
            using EditorGUI.DisabledScope locked = new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode);
            if (Event.current.type is EventType.MouseDown or EventType.KeyDown or EventType.ExecuteCommand)
                Undo.RecordObject(Configuration, "Edit Excel Mapping");
            page = GUILayout.Toolbar(page, pages);
            EditorGUI.BeginChangeCheck();
            switch (page)
            {
                case 0:
                    layout.Draw(Configuration, fields, ref status);
                    break;
                case 1:
                    DrawImport();
                    break;
                case 2:
                    DrawExport();
                    break;
                case 3:
                    DrawPreview();
                    break;
            }
            if (EditorGUI.EndChangeCheck())
            {
                MappingChanged();
            }
            DrawStatus();
            hasUnsavedChanges = session.HasChanges;
            saveChangesMessage = "Save or discard the pending Excel mapping changes.";
        }

        /// <summary>Restricts selection to mappings belonging to the current source and scope.</summary>
        private void DrawSelection()
        {
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
            using (new EditorGUI.DisabledScope(session.HasChanges))
            {
                int current = Array.IndexOf(compatible, session.Source as ExcelExchangePreset);
                int selected = StudioFieldGUI.Popup(new GUIContent("Mapping", "Only configurations linked to this preset and settings group."), current, compatibleNames);
                if (selected != current && selected >= 0)
                    Select(compatible[selected]);
                if (StudioButton.Draw(new GUIContent("New", "Create an empty mapping linked to this preset.")))
                    Create(false);
                using (new EditorGUI.DisabledScope(session.Source == null))
                    if (StudioButton.Draw(new GUIContent("Duplicate", "Copy this mapping into an independent configuration.")))
                        Create(true);
            }
            using (new EditorGUI.DisabledScope(!session.HasChanges))
            {
                if (StudioButton.Draw(new GUIContent("Save Mapping", "Save these cell mappings. Preset values are saved from their own tool.")))
                    SaveChanges();
                if (StudioButton.Draw(new GUIContent("Discard Mapping", "Reload the saved cell mappings.")))
                    DiscardChanges();
            }
        }

        /// <summary>Shows workbook selection and the available import operation.</summary>
        private void DrawImport()
        {
            bool changed = PathField("Workbook", "Spreadsheet supplying the imported values.", false);
            DrawWorksheet(changed);
            Configuration.UseFormulaResults = StudioFieldGUI.Toggle(new GUIContent("Formula Results", "Use cached results saved by Excel. Missing or erroneous results block the cell."), Configuration.UseFormulaResults);
            DrawSummary(true);
            GUILayout.FlexibleSpace();
            if (StudioButton.Draw(new GUIContent("Preview Import", "Read the workbook and show changes before updating the preset."), expandWidth: true, minimumHeight: 30f))
                PreviewImport();
        }

        /// <summary>Shows the optional template and explicit export destination flow.</summary>
        private void DrawExport()
        {
            PathField("Template", "Optional workbook whose other cells and formatting will be preserved.", true);
            DrawSummary(false);
            GUILayout.FlexibleSpace();
            using StudioButton.RowScope row = new StudioButton.RowScope();
            if (StudioButton.Draw(new GUIContent("Preview Export", "Show the values written by these mappings."), expandWidth: true, minimumHeight: 30f))
                PreviewExport();
            if (StudioButton.Draw(new GUIContent("Export…", "Choose where to save the workbook using the current preset values."), expandWidth: true, minimumHeight: 30f))
                Export();
        }

        /// <summary>Compares incoming values and exposes import only for a valid current proposal.</summary>
        private void DrawPreview()
        {
            if (preview == null && exportRows.Count == 0)
            {
                EditorGUILayout.LabelField("Run Preview Import or Preview Export to review mapped values.", EditorStyles.wordWrappedLabel);
                GUILayout.FlexibleSpace();
                return;
            }
            if (preview != null)
            {
                EditorGUILayout.LabelField(preview.CanImport ? preview.Changes + " changes ready to review" : "Resolve the highlighted cells before importing.", EditorStyles.boldLabel);
                ExcelPreviewView.Draw(preview.Rows, preview.Warnings, ref previewScroll);
                using StudioButton.RowScope row = new StudioButton.RowScope();
                if (StudioButton.Draw(new GUIContent("Refresh Preview", "Read current workbook and preset values again."), expandWidth: true))
                    PreviewImport();
                using (new EditorGUI.DisabledScope(preview == null || !preview.CanImport))
                    if (StudioButton.Draw(new GUIContent("Import", "Update the values open in the tool. Review them there and use its normal Apply or Discard."), expandWidth: true))
                        Import();
            }
            else
            {
                ExcelPreviewView.Draw(exportRows, Array.Empty<string>(), ref previewScroll, false);
                if (StudioButton.Draw(new GUIContent("Export…", "Choose a destination for these current values."), expandWidth: true))
                    Export();
            }
        }

        /// <summary>Summarizes configured columns and row policies without extra help boxes.</summary>
        /// <param name="import">Whether collection import policies can be edited here.</param>
        private void DrawSummary(bool import)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(Configuration.Mappings.Length + " cell mappings", EditorStyles.boldLabel);
            foreach (ExcelCollectionGroup group in Configuration.Groups)
            {
                ExcelLayoutSections.Horizontal();
                EditorGUILayout.LabelField(new GUIContent(ExcelLayoutSchema.CollectionLabel(group.Path, fields), group.Path), EditorStyles.boldLabel);
                if (import)
                    group.Policy = ExcelCollectionControls.Draw(group.Policy);
            }
            if (Configuration.Mappings.Length == 0)
                EditorGUILayout.LabelField("In Layout, select a field and click its first cell.", EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>Draws one workbook path and an explicit file picker.</summary>
        /// <param name="label">Visible path caption.</param>
        /// <param name="tooltip">Purpose of this path.</param>
        /// <param name="template">Whether to edit the optional export template.</param>
        /// <returns>True after an explicit path change.</returns>
        private bool PathField(string label, string tooltip, bool template)
        {
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
            string current = template ? Configuration.TemplatePath : Configuration.WorkbookPath;
            string value = StudioFieldGUI.TextField(new GUIContent(label, tooltip), current);
            if (StudioButton.Draw(new GUIContent("Browse…", "Choose an existing .xlsx workbook.")))
            {
                string selected = EditorUtility.OpenFilePanel(label, current.Length > 0 ? Path.GetDirectoryName(Path.GetFullPath(current)) : Application.dataPath, "xlsx");
                if (selected.Length > 0)
                {
                    value = selected;
                    if (!template)
                        worksheetPath = null;
                }
            }
            if (template)
                Configuration.TemplatePath = value;
            else
                Configuration.WorkbookPath = value;
            if (value != current)
                GUI.changed = true;
            return value != current;
        }

        /// <summary>Offers only worksheet names read from the selected import workbook.</summary>
        /// <param name="pathChanged">Whether a new file was explicitly chosen.</param>
        private void DrawWorksheet(bool pathChanged)
        {
            if (worksheetPath != Configuration.WorkbookPath || pathChanged)
            {
                worksheetPath = Configuration.WorkbookPath;
                worksheetNames = Array.Empty<string>();
                try
                {
                    if (File.Exists(worksheetPath))
                        worksheetNames = ExcelWorkbookReader.Worksheets(worksheetPath);
                    if (pathChanged && worksheetNames.Length > 0 && !worksheetNames.Contains(Configuration.Worksheet))
                        Configuration.Worksheet = worksheetNames[0];
                }
                catch (Exception exception)
                {
                    status = exception.Message;
                }
            }
            using EditorGUI.DisabledScope disabled = new EditorGUI.DisabledScope(worksheetNames.Length == 0);
            int current = Array.IndexOf(worksheetNames, Configuration.Worksheet);
            int next = StudioFieldGUI.Popup(new GUIContent("Worksheet", "Tab inside the selected Excel file. All mapped coordinates use this tab."), current,
                worksheetNames.Length > 0 ? worksheetNames : new[] { "Choose a valid workbook first" });
            if (next >= 0 && worksheetNames.Length > 0)
                Configuration.Worksheet = worksheetNames[next];
        }

        /// <summary>Shows the current result or mapping recovery issue.</summary>
        private void DrawStatus()
        {
            string message = session.RecoveryWarning.Length > 0 ? session.RecoveryWarning : status;
            if (message.Length > 0)
                EditorGUILayout.LabelField(message, EditorStyles.wordWrappedMiniLabel);
        }

        #endregion
        #region Operations

        /// <summary>Retains direct edits and context-menu structural changes through the same transaction.</summary>
        private void MappingChanged()
        {
            if (Configuration == null || context == null)
                return;
            string stamp = EditorJsonUtility.ToJson(Configuration);
            if (stamp == configurationStamp)
                return;
            configurationStamp = stamp;
            ClearPreview();
            EditorUtility.SetDirty(Configuration);
            session.Retain();
            ExcelPresetLibrary.Remember(context, (ExcelExchangePreset)session.Source);
            Repaint();
        }

        /// <summary>Reads source cells and builds the isolated import proposal.</summary>
        private void PreviewImport()
        {
            ClearPreview();
            try
            {
                preview = ExcelImportService.Preview(context, Configuration);
                page = 3;
                status = preview.CanImport ? "Review the changes, then Import. Apply or Discard remains in the preset tool." : "Preview contains errors. Correct the mapping or workbook and refresh.";
            }
            catch (Exception exception)
            {
                status = exception.Message;
            }
        }

        /// <summary>Builds a read-only export preview from current tool values.</summary>
        private void PreviewExport()
        {
            ClearPreview();
            try
            {
                ExcelWorkbook template = Configuration.TemplatePath.Length > 0 ? ExcelWorkbookReader.Read(Configuration.TemplatePath) : null;
                foreach (ExcelSheet sheet in ExcelExportService.Build(context, Configuration, template).Sheets.Values)
                    foreach (KeyValuePair<(int Row, int Column), ExcelCell> cell in sheet.Cells)
                        exportRows.Add(new ExcelPreviewRow(sheet.Name + "!" + ExcelAddress.Format(cell.Key.Row, cell.Key.Column), string.Empty,
                            string.Empty, cell.Value.Formula.Length > 0 ? "=" + cell.Value.Formula : cell.Value.Text));
                page = 3;
                status = "Export uses the values currently open in the preset tool.";
            }
            catch (Exception exception)
            {
                status = exception.Message;
            }
        }

        /// <summary>Transfers the reviewed proposal without writing the persistent target.</summary>
        private void Import()
        {
            try
            {
                preview.Import();
                status = "Imported. Review the values in the preset tool, then Apply or Discard.";
            }
            catch (Exception exception)
            {
                status = exception.Message;
            }
        }

        /// <summary>Writes a workbook only after choosing its destination.</summary>
        private void Export()
        {
            string path = EditorUtility.SaveFilePanel("Export With Excell", Application.dataPath, context.Source.name + " Export", "xlsx");
            if (path.Length == 0)
                return;
            try
            {
                ExcelExportService.Export(context, Configuration, path);
                status = "Exported: " + path;
            }
            catch (Exception exception)
            {
                status = exception.Message;
            }
        }

        /// <summary>Creates one configuration and selects it without replacing pending edits.</summary>
        /// <param name="duplicate">Whether to copy the currently saved mapping.</param>
        private void Create(bool duplicate)
        {
            ExcelExchangePreset created = ExcelPresetLibrary.Create(context, duplicate ? (ExcelExchangePreset)session.Source : null);
            if (created == null)
                return;
            RefreshLibrary();
            Select(created);
            page = 0;
        }

        /// <summary>Opens an independent mapping draft without reading a workbook.</summary>
        /// <param name="preset">Compatible saved mapping or null.</param>
        private void Select(ExcelExchangePreset preset)
        {
            if (!session.Select(preset, out status))
                return;
            layout.Select(preset);
            if (preset != null)
                ExcelPresetLibrary.Remember(context, preset);
            RefreshConfiguration();
        }

        /// <summary>Refreshes mapping revisions after selection, Undo, Save or Discard.</summary>
        private void RefreshConfiguration()
        {
            configurationStamp = Configuration != null ? EditorJsonUtility.ToJson(Configuration) : string.Empty;
            ClearPreview();
        }

        /// <summary>Refreshes only the small configuration library after project changes.</summary>
        private void RefreshLibrary()
        {
            if (!Connected)
                return;
            compatible = ExcelPresetLibrary.Find(context);
            compatibleNames = Array.ConvertAll(compatible, preset => preset.name);
            Repaint();
        }

        /// <summary>Releases proposals and stale export rows after a mapping or source change.</summary>
        private void ClearPreview()
        {
            preview?.Dispose();
            preview = null;
            exportRows.Clear();
        }

        /// <summary>Saves only the exchange configuration and retains a failed proposal.</summary>
        public override void SaveChanges()
        {
            if (session.Save("Save Excel Mapping", out status))
            {
                RefreshConfiguration();
                base.SaveChanges();
            }
        }

        /// <summary>Discards only mapping edits without touching preset values.</summary>
        public override void DiscardChanges()
        {
            session.Discard();
            RefreshConfiguration();
            base.DiscardChanges();
        }

        #endregion
        #endregion
    }

    /// <summary>Connects the package to contextual Studio buttons without adding a Tools menu.</summary>
    [InitializeOnLoad]
    internal static class ExcelExchangeRegistration
    {
        #region Methods

        /// <summary>Installs the single shared window entry point after an editor reload.</summary>
        static ExcelExchangeRegistration()
        {
            StudioPresetActions.ExchangeRequested += ExcelExchangeWindow.Open;
        }

        #endregion
    }
}
