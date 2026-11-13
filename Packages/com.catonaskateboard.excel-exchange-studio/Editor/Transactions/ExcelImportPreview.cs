using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using Object = UnityEngine.Object;

namespace CatOnASkateboard.ExcelExchangeStudio.Editor
{
    /// <summary>Stores a reviewable field change or a cell-specific diagnostic.</summary>
    public readonly struct ExcelPreviewRow
    {
        #region Fields

        public readonly string Cell;
        public readonly string Field;
        public readonly string Before;
        public readonly string After;
        public readonly string Message;
        public readonly bool Error;

        #endregion
        #region Methods

        /// <summary>Captures one compact preview row.</summary>
        /// <param name="cell">Workbook address or collection name.</param>
        /// <param name="field">Logical destination.</param>
        /// <param name="before">Current value.</param>
        /// <param name="after">Incoming value.</param>
        /// <param name="message">Optional diagnostic or reset explanation.</param>
        /// <param name="error">Whether this row blocks import.</param>
        public ExcelPreviewRow(string cell, string field, string before, string after, string message = "", bool error = false)
        {
            Cell = cell;
            Field = field;
            Before = before;
            After = after;
            Message = message;
            Error = error;
        }

        #endregion
    }

    /// <summary>Owns an isolated import proposal and the revisions required before transfer.</summary>
    public sealed class ExcelImportPreview : IDisposable
    {
        #region Fields

        public readonly List<ExcelPreviewRow> Rows = new List<ExcelPreviewRow>();
        public readonly List<string> Warnings = new List<string>();
        public readonly StudioPresetContext Context;
        public readonly ExcelExchangePreset Preset;
        public readonly string Path;
        public readonly string WorkbookHash;
        public readonly string PresetStamp;
        public readonly string SourceStamp;
        public readonly string AssetStamp;
        public readonly Object Before;
        public readonly Object Proposal;
        private bool imported;

        #endregion
        #region Properties

        public bool CanImport => !imported && !Rows.Exists(row => row.Error);
        public int Changes
        {
            get
            {
                int count = 0;
                foreach (ExcelPreviewRow row in Rows)
                    if (!row.Error && row.Before != row.After)
                        count++;
                return count;
            }
        }

        #endregion
        #region Methods
        #region Lifetime

        /// <summary>Captures current values and all conflict stamps before reading cells.</summary>
        /// <param name="context">Tool editing session.</param>
        /// <param name="preset">Exact linked configuration.</param>
        /// <param name="path">Workbook used for this preview.</param>
        internal ExcelImportPreview(StudioPresetContext context, ExcelExchangePreset preset, string path)
        {
            Context = context;
            Preset = preset;
            Path = System.IO.Path.GetFullPath(path);
            WorkbookHash = Hash(Path);
            PresetStamp = EditorJsonUtility.ToJson(preset);
            AssetStamp = JsonUtility(context.Source);
            Before = context.Capture();
            SourceStamp = JsonUtility(Before);
            Proposal = StudioPresetContext.Clone(Before);
        }

        /// <summary>Releases both isolated snapshots without changing the real preset.</summary>
        public void Dispose()
        {
            StudioPresetContext.Release(Proposal);
            StudioPresetContext.Release(Before);
        }

        #endregion
        #region Transfer

        /// <summary>Revalidates the preview and transfers it as one Undo operation.</summary>
        public void Import()
        {
            if (!CanImport || !Context.IsCurrent() || Context.Source == null)
                throw new InvalidOperationException("Resolve the preview errors and keep the same preset open before importing.");
            using Snapshot current = new Snapshot(Context.Capture());
            if (Hash(Path) != WorkbookHash || EditorJsonUtility.ToJson(Preset) != PresetStamp
                || JsonUtility(current.Value) != SourceStamp || JsonUtility(Context.Source) != AssetStamp)
                throw new InvalidOperationException("The workbook, mapping or preset changed. Run Preview again.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Import With Excell");
            Undo.RegisterCompleteObjectUndo(Context.UndoOwner, "Import With Excell");
            try
            {
                Context.Import(Proposal);
                Undo.CollapseUndoOperations(group);
                imported = true;
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                Context.Import(Before);
                throw;
            }
        }

        /// <summary>Hashes exact workbook bytes so edits after preview cannot pass unnoticed.</summary>
        /// <param name="path">Workbook file.</param>
        /// <returns>A stable byte fingerprint.</returns>
        internal static string Hash(string path)
        {
            using SHA256 algorithm = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return Convert.ToBase64String(algorithm.ComputeHash(stream));
        }

        /// <summary>Captures settings without temporary Unity ownership fields.</summary>
        /// <param name="value">Typed snapshot or saved asset.</param>
        /// <returns>The serialized user fields.</returns>
        private static string JsonUtility(Object value)
        {
            return UnityEngine.JsonUtility.ToJson(value);
        }

        #endregion
        #endregion
    }

    /// <summary>Releases a temporary typed snapshot at a transaction boundary.</summary>
    internal readonly struct Snapshot : IDisposable
    {
        #region Fields

        internal readonly Object Value;

        #endregion
        #region Methods

        /// <summary>Takes ownership of one temporary snapshot.</summary>
        /// <param name="value">Owned copy.</param>
        internal Snapshot(Object value)
        {
            Value = value;
        }

        /// <summary>Releases only the owned snapshot.</summary>
        public void Dispose()
        {
            StudioPresetContext.Release(Value);
        }

        #endregion
    }
}
