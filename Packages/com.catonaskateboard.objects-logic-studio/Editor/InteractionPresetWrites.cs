using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares validation and conflict-safe preset updates between buttons and prefab Apply.</summary>
    internal static class InteractionPresetWrites
    {
        #region Methods

        #region Snapshots

        /// <summary>Captures a selected asset for outside-edit detection.</summary>
        /// <param name="preset">Selected preset or null.</param>
        /// <returns>Serialized asset state, or an empty baseline for no selection.</returns>
        internal static string Capture(ScriptableObject preset)
        {
            // Asset identity is stored separately; this snapshot tracks its current saved data.
            return preset != null ? EditorJsonUtility.ToJson(preset) : string.Empty;
        }

        /// <summary>Validates reusable single-action settings independently of per-item input references.</summary>
        /// <param name="session">Proposed single-action data and selected asset.</param>
        /// <param name="warning">Receives invalid settings, read-only assets or conflicts.</param>
        /// <returns>True when Update or Apply may write the selected preset.</returns>
        internal static bool Validate(SingleInteractionSession session, out string warning)
        {
            // Unassigned presets need no asset write.
            warning = string.Empty;
            if (session.Preset == null)
                return true;
            if (session.Preset.Kind != session.Kind || !ValidateAsset(session.Preset, session.PresetBaseline, out warning))
                return false;
            return session.Kind switch
            {
                SingleInteractionKind.Grab => session.Draft.Grab.TryValidate(out warning),
                SingleInteractionKind.Drop => session.Draft.Release.TryValidate(out warning),
                SingleInteractionKind.Throw => session.Draft.Release.TryValidate(out warning) && session.Draft.Throw.TryValidate(out warning),
                _ => false
            };
        }

        /// <summary>Validates transferable extended settings without requiring prefab-local HUD or mesh bindings.</summary>
        /// <param name="session">Proposed extended settings and selected asset.</param>
        /// <param name="warning">Receives invalid settings, read-only assets or conflicts.</param>
        /// <returns>True when Update or Apply may write the selected preset.</returns>
        internal static bool Validate(ExtendedInteractionSession session, out string warning)
        {
            // Unlock rules contain prefab references and deliberately have no transferable preset.
            warning = string.Empty;
            if (session.Preset == null)
                return true;
            if (session.Preset.Kind != session.Kind || !ValidateAsset(session.Preset, session.PresetBaseline, out warning))
                return false;
            return session.Kind switch
            {
                ExtendedInteractionKind.ModifyByContact => session.Draft.Contact.TryValidate(out warning),
                ExtendedInteractionKind.Dialogue => session.Draft.Dialogue.TryValidate(out warning),
                ExtendedInteractionKind.Outline => session.Draft.Outline.TryValidate(out warning),
                ExtendedInteractionKind.AssemblyStation => session.Draft.AssemblyStation.TryValidate(out warning),
                _ => false
            };
        }

        /// <summary>Protects a writable preset against changes made after its selection.</summary>
        /// <param name="preset">Asset receiving the proposed settings.</param>
        /// <param name="baseline">Asset state at selection, import or the latest successful update.</param>
        /// <param name="warning">Receives a write or conflict issue.</param>
        /// <returns>True when the asset remains writable and unchanged externally.</returns>
        private static bool ValidateAsset(ScriptableObject preset, string baseline, out string warning)
        {
            // A selected asset must have a known baseline before a proposed update can overwrite it.
            warning = string.Empty;
            if (!EditorUtility.IsPersistent(preset) || !AssetDatabase.IsOpenForEdit(preset))
                warning = "Select a writable preset asset before updating it.";
            else if (Capture(preset) != baseline)
                warning = "The preset changed outside this session. Import its current values or select the intended preset again before updating it.";
            return warning.Length == 0;
        }

        #endregion

        #region Updates

        /// <summary>Writes the selected single-action preset using the same operation for Update and Apply.</summary>
        /// <param name="state">Workspace owning the retained session.</param>
        internal static void UpdateSingle(ObjectWorkspace state)
        {
            // Only the active feature's configuration enters its dedicated preset.
            SingleInteractionSession session = state.Single;
            if (session.Preset == null)
                return;
            if (!Validate(session, out string warning))
                throw new InvalidOperationException(warning);
            string settings = session.Kind == SingleInteractionKind.Grab ? JsonUtility.ToJson(session.Draft.Grab) : JsonUtility.ToJson(session.Draft.Release);
            string payload = "{\"Settings\":" + settings + (session.Kind == SingleInteractionKind.Throw
                ? ",\"Trajectory\":" + JsonUtility.ToJson(session.Draft.Throw) : string.Empty) + "}";
            Write(state, session.Preset, payload);
            session.PresetBaseline = Capture(session.Preset);
            state.Persist();
        }

        /// <summary>Writes the selected extended preset without changing local interaction references.</summary>
        /// <param name="state">Workspace owning the retained session.</param>
        internal static void UpdateExtended(ObjectWorkspace state)
        {
            // Color, dialogue and contact snapshots remain independent of each prefab's bindings.
            ExtendedInteractionSession session = state.Extended;
            if (session.Preset == null)
                return;
            if (!Validate(session, out string warning))
                throw new InvalidOperationException(warning);
            string settings = session.Kind switch
            {
                ExtendedInteractionKind.ModifyByContact => JsonUtility.ToJson(session.Draft.Contact),
                ExtendedInteractionKind.Dialogue => JsonUtility.ToJson(session.Draft.Dialogue),
                ExtendedInteractionKind.Outline => JsonUtility.ToJson(session.Draft.Outline),
                ExtendedInteractionKind.AssemblyStation => JsonUtility.ToJson(session.Draft.AssemblyStation),
                _ => throw new InvalidOperationException("This configuration has no transferable preset.")
            };
            Write(state, session.Preset, "{\"Settings\":" + settings + "}");
            session.PresetBaseline = Capture(session.Preset);
            state.Persist();
        }

        /// <summary>Commits one validated payload while retaining Undo for the asset and workspace baseline.</summary>
        /// <param name="state">Workspace whose preset baseline will advance.</param>
        /// <param name="preset">Writable selected asset.</param>
        /// <param name="payload">Only the configuration fields to update.</param>
        private static void Write(ObjectWorkspace state, ScriptableObject preset, string payload)
        {
            // Preserve identity, name and unrelated serialized fields on the existing asset.
            Undo.RecordObject(state, "Update interaction preset");
            Undo.RecordObject(preset, "Update interaction preset");
            JsonUtility.FromJsonOverwrite(payload, preset);
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssetIfDirty(preset);
        }

        #endregion

        #endregion
    }
}
