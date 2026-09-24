using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Persists one single-interaction proposal inside the common object workspace transaction.</summary>
    [Serializable]
    internal sealed class SingleInteractionSession
    {
        #region Fields

        [Header("Selection")]
        [Tooltip("Single-interaction feature currently selected on the workspace object.")]
        public SingleInteractionKind Kind;
        [Tooltip("Whether the selected feature's settings are expanded.")]
        public bool Expanded = true;
        [Tooltip("Whether this session began with an existing component.")]
        public bool HasBinding;
        [Tooltip("Reusable settings asset selected for explicit import or export on this interaction card.")]
        public SingleInteractionPreset Preset;
        [Tooltip("Preset assigned to the component when this proposal was opened.")]
        public SingleInteractionPreset OriginalPreset;
        [Tooltip("Selected preset state used to protect against outside edits.")]
        public string PresetBaseline = string.Empty;
        [Header("Draft")]
        [Tooltip("Retained proposed settings, applied only through the common footer.")]
        public SingleInteractionDraft Draft = new SingleInteractionDraft();
        [Tooltip("Applied settings captured when the selected feature was opened.")]
        public SingleInteractionDraft Baseline = new SingleInteractionDraft();

        #endregion

        #region Properties

        /// <summary>Whether this proposal differs from its applied baseline.</summary>
        internal bool HasChanges => HasBinding && (Preset != OriginalPreset || JsonUtility.ToJson(Draft) != JsonUtility.ToJson(Baseline));

        #endregion

        #region Methods

        #region Selection

        /// <summary>Resolves one unique feature type on the selected object.</summary>
        /// <param name="target">Workspace object in a scene, stage or asset.</param>
        /// <param name="kind">Requested feature type.</param>
        /// <returns>The matching component, or null when unavailable.</returns>
        internal static ObjectSingleInteraction Resolve(GameObject target, SingleInteractionKind kind)
        {
            // A type-based identity does not shift when another feature card is removed.
            if (target == null)
                return null;
            return kind switch
            {
                SingleInteractionKind.Grab => target.GetComponent<ObjectGrab>(),
                SingleInteractionKind.Drop => target.GetComponent<ObjectDrop>(),
                SingleInteractionKind.Throw => target.GetComponent<ObjectThrow>(),
                _ => null
            };
        }

        /// <summary>Loads the applied component after an explicit selection or Discard.</summary>
        /// <param name="target">Current workspace object.</param>
        internal void Read(GameObject target)
        {
            // An absent feature stays absent rather than changing an unfinished proposal's identity.
            ObjectSingleInteraction feature = Resolve(target, Kind);
            Preset = OriginalPreset = feature != null ? feature.SettingsPreset : null;
            PresetBaseline = InteractionPresetWrites.Capture(Preset);
            HasBinding = feature != null;
            Draft = SingleInteractionDraft.Capture(feature);
            Baseline = ObjectWorkspace.Copy(Draft);
        }

        #endregion

        #region Transaction

        /// <summary>Checks outside edits and complete feature configuration before any workspace writes occur.</summary>
        /// <param name="target">Object captured by the common workspace identity.</param>
        /// <param name="warning">Receives a missing component, conflict or invalid setting.</param>
        /// <returns>True when this session is unchanged or its proposal is valid.</returns>
        internal bool TryValidate(GameObject target, out string warning)
        {
            // A missing or externally changed component must never be silently recreated or overwritten.
            warning = string.Empty;
            if (!HasChanges)
                return true;
            if (!InteractionPresetWrites.Validate(this, out warning))
                return false;
            ObjectSingleInteraction feature = Resolve(target, Kind);
            if (feature == null || feature.SettingsPreset != OriginalPreset || JsonUtility.ToJson(SingleInteractionDraft.Capture(feature)) != JsonUtility.ToJson(Baseline))
                warning = "The selected single interaction changed outside this session. Discard to reload it.";
            else if (EditorUtility.IsPersistent(feature) && !AssetDatabase.IsOpenForEdit(feature))
                warning = "The selected prefab is not writable.";
            return warning.Length == 0 && Draft.TryValidate(feature, out warning);
        }

        /// <summary>Writes one validated feature as part of the existing Apply Undo group.</summary>
        /// <param name="target">Object receiving the pending settings.</param>
        internal void Apply(GameObject target)
        {
            // The caller validates every workspace domain before entering this method.
            if (!HasChanges)
                return;
            ObjectSingleInteraction feature = Resolve(target, Kind);
            Undo.RecordObject(feature, "Apply " + Kind);
            using (SerializedObject data = new SerializedObject(feature))
            {
                data.FindProperty("settingsPreset").objectReferenceValue = Preset;
                data.FindProperty("interactionName").stringValue = Draft.Name;
                data.FindProperty("action").objectReferenceValue = Draft.Action;
                data.FindProperty("m_Enabled").boolValue = Draft.Enabled;
                if (feature is ObjectGrab)
                    data.FindProperty("drawGizmos").boolValue = Draft.DrawGizmos;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            string settings = Kind == SingleInteractionKind.Grab
                ? "\"settings\":" + JsonUtility.ToJson(Draft.Grab) : "\"physics\":" + JsonUtility.ToJson(Draft.Release);
            if (Kind == SingleInteractionKind.Throw)
                settings += ",\"trajectory\":" + JsonUtility.ToJson(Draft.Throw);
            JsonUtility.FromJsonOverwrite("{" + settings + ",\"tagChange\":" + JsonUtility.ToJson(Draft.TagChange) + "}", feature);
            EditorUtility.SetDirty(feature);
            if (!EditorUtility.IsPersistent(feature))
                PrefabUtility.RecordPrefabInstancePropertyModifications(feature);
        }

        #endregion

        #endregion
    }
}
