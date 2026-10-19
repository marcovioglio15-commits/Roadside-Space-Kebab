using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Shares durable asset drafts, external-edit detection and verified Apply across Studio windows.</summary>
    [Serializable]
    public sealed class StudioAssetSession
    {
        #region Fields

        [Header("Asset Session")]
        [Tooltip("Persistent asset receiving an explicit Apply.")]
        [SerializeField]
        private ScriptableObject source;
        [Tooltip("Temporary editable copy; recovery data survives its destruction.")]
        [SerializeField]
        private ScriptableObject draft;
        [Tooltip("Persistent recovery identity for the exact source or subasset.")]
        [SerializeField]
        private string key = string.Empty;
        [Tooltip("Stable source snapshot used to reject outside edits.")]
        [SerializeField]
        private string baseline = string.Empty;
        [Tooltip("Initial draft snapshot, excluding differences caused by temporary editor ownership.")]
        [SerializeField]
        private string draftBaseline = string.Empty;
        private string retained = string.Empty;
        [Tooltip("Last recovery failure, preserved across script reloads.")]
        [SerializeField]
        private string recoveryWarning = string.Empty;
        [Tooltip("Prevents replacing an unreadable recovery record before an explicit Discard.")]
        [SerializeField]
        private bool recoveryBlocked;
        private const HideFlags draftFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;

        #endregion
        #region Properties

        /// <summary>Persistent asset currently selected by this session.</summary>
        public ScriptableObject Source => source;
        /// <summary>Detached object used by the existing conditional inspector.</summary>
        public ScriptableObject Draft => draft;
        /// <summary>Whether editable data differs from the original proposal.</summary>
        public bool HasChanges => draft != null && EditorJsonUtility.ToJson(draft) != draftBaseline;
        /// <summary>Whether a clean draft should follow a newer asset import or another editor's write.</summary>
        public bool SourceChanged => source != null && EditorJsonUtility.ToJson(source) != baseline;
        /// <summary>Recovery failures remain visible while their original file is preserved.</summary>
        public string RecoveryWarning => recoveryWarning;

        #endregion
        #region Methods
        #region Selection & Recovery

        /// <summary>Recreates a lost temporary object from the per-asset recovery file after an editor restart.</summary>
        public void Restore()
        {
            if (draft != null)
            {
                draft.hideFlags = draftFlags;
                return;
            }
            if (source != null)
                Open(source);
        }

        /// <summary>Opens an asset and recovers unfinished work without overwriting a different pending proposal.</summary>
        /// <param name="asset">Persistent asset selected by the window.</param>
        /// <param name="warning">Receives navigation or recovery errors.</param>
        /// <returns>True when the requested source is selected.</returns>
        public bool Select(ScriptableObject asset, out string warning)
        {
            warning = string.Empty;
            if (source != asset && HasChanges)
            {
                warning = "Apply or Discard before changing assets.";
                return false;
            }
            if (source != asset || draft == null)
                Open(asset);
            else if (!HasChanges && SourceChanged)
                Reload();
            warning = recoveryWarning;
            return true;
        }

        /// <summary>Loads stored editor JSON with stable asset references before a window begins drawing.</summary>
        /// <param name="asset">New persistent source, or null to clear a clean selection.</param>
        private void Open(ScriptableObject asset)
        {
            Release();
            source = asset;
            key = StudioAssetDraftStore.Key(asset);
            recoveryWarning = string.Empty;
            recoveryBlocked = false;
            Reload();
            if (source == null || string.IsNullOrEmpty(key))
                return;
            try
            {
                StudioAssetDraftStore.Snapshot saved = StudioAssetDraftStore.Load(key);
                if (saved == null)
                    return;
                EditorJsonUtility.FromJsonOverwrite(saved.Proposal, draft);
                draft.hideFlags = draftFlags;
                // A completed Apply can outlive a failed recovery-file cleanup.
                if (JsonUtility.ToJson(draft) == JsonUtility.ToJson(source))
                {
                    Reload();
                    ClearRecovery();
                    return;
                }
                baseline = saved.Source;
                draftBaseline = saved.Baseline;
                retained = saved.Proposal;
            }
            catch (Exception exception)
            {
                recoveryBlocked = true;
                recoveryWarning = "Asset draft recovery failed: " + exception.Message;
            }
        }

        /// <summary>Refreshes a clean proposal after asset changes while preserving unfinished work.</summary>
        /// <returns>True when the temporary inspector target was replaced.</returns>
        public bool Refresh()
        {
            if (HasChanges || !SourceChanged || recoveryWarning.Length > 0)
                return false;
            Reload();
            return true;
        }

        /// <summary>Copies the latest applied source without changing project data.</summary>
        private void Reload()
        {
            Release();
            draft = source != null ? UnityEngine.Object.Instantiate(source) : null;
            if (draft != null)
            {
                draft.name = source.name;
                draft.hideFlags = draftFlags;
            }
            baseline = source != null ? EditorJsonUtility.ToJson(source) : string.Empty;
            draftBaseline = draft != null ? EditorJsonUtility.ToJson(draft) : string.Empty;
            retained = string.Empty;
        }

        #endregion
        #region Transactions

        /// <summary>Retains a changed proposal on disk without saving its project asset.</summary>
        /// <returns>True when recovery remains available or no edit needs retention.</returns>
        public bool Retain()
        {
            if (draft == null || EditorApplication.isPlayingOrWillChangePlaymode || recoveryBlocked)
                return !recoveryBlocked;
            string proposal = EditorJsonUtility.ToJson(draft);
            if (proposal == retained || proposal == draftBaseline && retained.Length == 0)
                return true;
            try
            {
                if (proposal == draftBaseline)
                    StudioAssetDraftStore.Delete(key);
                else
                    StudioAssetDraftStore.Save(key, new StudioAssetDraftStore.Snapshot
                    {
                        Source = baseline, Baseline = draftBaseline, Proposal = proposal
                    });
                retained = proposal;
                recoveryWarning = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                recoveryWarning = "The pending asset edit could not be retained: " + exception.Message;
                return false;
            }
        }

        /// <summary>Checks source identity, external edits and file permissions before copying any values.</summary>
        /// <param name="warning">Receives the reason Apply cannot proceed.</param>
        /// <returns>True when a validated proposal can be committed.</returns>
        public bool CanSave(out string warning)
        {
            warning = recoveryWarning;
            if (recoveryBlocked)
                return false;
            if (draft == null || !StudioAssetWrites.CanWrite(source, out warning))
                return false;
            warning = "The saved source changed outside this window. Discard to reload it; the pending proposal remains retained.";
            if (SourceChanged)
                return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Writes a validated proposal and keeps it recoverable if a disk write fails.</summary>
        /// <param name="label">Undo operation shown for this explicit Apply.</param>
        /// <param name="warning">Receives an outside-edit or save failure.</param>
        /// <returns>True only after the source is saved successfully.</returns>
        public bool Save(string label, out string warning)
        {
            Retain();
            if (!CanSave(out warning))
                return false;
            // The shared writer preserves metadata and restores native and disk data on failure.
            try
            {
                StudioAssetWrites.Save(source, label, () => EditorUtility.CopySerialized(draft, source));
            }
            catch (Exception exception)
            {
                warning = "Apply failed; the proposal remains available: " + exception.Message;
                return false;
            }
            // Recovery cleanup must never roll back a source that was already saved.
            Reload();
            ClearRecovery();
            return true;
        }

        /// <summary>Removes an obsolete proposal without reporting a successful asset write as failed.</summary>
        private void ClearRecovery()
        {
            try
            {
                StudioAssetDraftStore.Delete(key);
                recoveryWarning = string.Empty;
            }
            catch (Exception exception)
            {
                recoveryWarning = "The asset is saved, but recovery cleanup failed: " + exception.Message;
            }
        }

        /// <summary>Explicitly abandons recovery data and reloads the latest source.</summary>
        public void Discard()
        {
            StudioAssetDraftStore.Delete(key);
            recoveryWarning = string.Empty;
            recoveryBlocked = false;
            Reload();
        }

        /// <summary>Releases only the transient copy after its proposal has been retained.</summary>
        public void Release()
        {
            if (draft != null && !EditorUtility.IsPersistent(draft))
                UnityEngine.Object.DestroyImmediate(draft);
            draft = null;
        }

        #endregion
        #endregion
    }
}
