using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Owns a persistent draft for one contact or dialogue component, identified by its prefab file ID.</summary>
    [Serializable]
    internal sealed class ExtendedInteractionSession
    {
        #region Fields

        [Header("Selection")]
        [Tooltip("Type of the retained feature card.")]
        public ExtendedInteractionKind Kind;
        [Tooltip("Stable component identity inside the selected prefab object.")]
        public long ComponentId;
        [Tooltip("Whether a selected component belongs to this draft, including when temporarily unavailable.")]
        public bool HasBinding;
        [Tooltip("Whether the selected feature card is expanded.")]
        public bool Expanded = true;
        [Tooltip("Reusable configuration selected for explicit Import or Export.")]
        public ExtendedInteractionPreset Preset;
        [Tooltip("Preset assigned to the component when this proposal was opened.")]
        public ExtendedInteractionPreset OriginalPreset;
        [Tooltip("Selected preset state used to protect against outside edits.")]
        public string PresetBaseline = string.Empty;
        [Tooltip("Hierarchy structure captured before path-based mesh or renderer edits.")]
        public string Hierarchy = string.Empty;
        [Header("Draft")]
        [Tooltip("Detached pending configuration.")]
        public ExtendedInteractionDraft Draft = new ExtendedInteractionDraft();
        [Tooltip("Applied configuration used to detect outside edits.")]
        public ExtendedInteractionDraft Baseline = new ExtendedInteractionDraft();

        #endregion

        #region Properties

        /// <summary>Whether the selected feature has pending data changes.</summary>
        internal bool HasChanges => HasBinding && (Preset != OriginalPreset || JsonUtility.ToJson(Draft) != JsonUtility.ToJson(Baseline));

        #endregion

        #region Methods

        #region Selection

        /// <summary>Finds the same component after stage reopening, preserving identity among duplicate dialogues.</summary>
        /// <param name="target">Current selected prefab branch.</param>
        /// <returns>The retained feature or null if its component no longer exists.</returns>
        internal ObjectExtendedInteraction Resolve(GameObject target)
        {
            // A missing saved ID never falls back to another component with similar conditions or name.
            if (target == null)
                return null;
            foreach (ObjectExtendedInteraction feature in target.GetComponents<ObjectExtendedInteraction>())
                if (feature.Kind == Kind && (ComponentId == 0 || ObjectWorkspaceTarget.FileId(feature) == ComponentId))
                    return feature;
            return null;
        }

        /// <summary>Reads the currently retained component after object selection or Discard.</summary>
        /// <param name="target">Prefab branch owning the retained feature.</param>
        internal void Read(GameObject target)
        {
            // Missing components remain missing until an explicit feature or object selection is made.
            Select(Resolve(target));
        }

        /// <summary>Captures one applied feature when navigation is allowed.</summary>
        /// <param name="feature">Selected component, or null when no matching component exists.</param>
        internal void Select(ObjectExtendedInteraction feature)
        {
            // Keep the last identity on disappearance so a later stage restore can reconnect it.
            HasBinding = feature != null;
            if (feature != null)
            {
                Kind = feature.Kind;
                ComponentId = ObjectWorkspaceTarget.FileId(feature);
            }
            Preset = OriginalPreset = feature != null ? feature.SettingsPreset : null;
            PresetBaseline = InteractionPresetWrites.Capture(Preset);
            Draft = ExtendedInteractionDraft.Capture(feature);
            Baseline = ObjectWorkspace.Copy(Draft);
            Hierarchy = feature != null ? HoverHierarchy.Signature(feature.transform) : string.Empty;
        }

        #endregion

        #region Transaction

        /// <summary>Checks source conflicts and dependencies before any source is written.</summary>
        /// <param name="target">Selected prefab branch.</param>
        /// <param name="warning">Receives missing components, hierarchy changes or invalid settings.</param>
        /// <returns>True when the retained proposal is safe to apply.</returns>
        internal bool TryValidate(GameObject target, out string warning)
        {
            // Collider-only replacements do not alter this hierarchy signature or overwrite collider settings.
            warning = string.Empty;
            if (!HasChanges)
                return true;
            if (!InteractionPresetWrites.Validate(this, out warning))
                return false;
            ObjectExtendedInteraction feature = Resolve(target);
            if (feature == null || feature.SettingsPreset != OriginalPreset || JsonUtility.ToJson(ExtendedInteractionDraft.Capture(feature)) != JsonUtility.ToJson(Baseline)
                || HoverHierarchy.Signature(feature.transform) != Hierarchy)
                warning = "The selected interaction or its hierarchy changed elsewhere. Discard to reload its current values.";
            return warning.Length == 0 && Draft.TryValidate(feature, out warning);
        }

        /// <summary>Writes one validated feature inside the workspace's existing prefab transaction.</summary>
        /// <param name="target">Prefab branch that passed validation.</param>
        internal void Apply(GameObject target)
        {
            // The shared transaction owns Undo, saving and rollback for every interaction category.
            if (!HasChanges)
                return;
            ObjectExtendedInteraction feature = Resolve(target);
            Undo.RecordObject(feature, "Apply " + Kind);
            using (SerializedObject data = new SerializedObject(feature))
            {
                data.FindProperty("settingsPreset").objectReferenceValue = Preset;
                data.FindProperty("interactionName").stringValue = Draft.Name;
                data.FindProperty("m_Enabled").boolValue = Draft.Enabled;
                data.FindProperty("drawGizmos").boolValue = Draft.DrawGizmos;
                if (feature is ObjectAssemblyStation or ObjectSlice)
                    data.FindProperty("action").objectReferenceValue = Draft.StartAction;
                if (feature is ObjectDialogue)
                {
                    data.FindProperty("startAction").objectReferenceValue = Draft.StartAction;
                    data.FindProperty("advanceAction").objectReferenceValue = Draft.AdvanceAction;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            string settings = Kind switch
            {
                ExtendedInteractionKind.Slice => JsonUtility.ToJson(Draft.Slice),
                ExtendedInteractionKind.SpawnManagement => JsonUtility.ToJson(SpawnSourceAuthoring.Resolve(Draft.SpawnManagement)),
                ExtendedInteractionKind.AssemblyStation => JsonUtility.ToJson(Draft.AssemblyStation),
                ExtendedInteractionKind.AssemblyProduct => JsonUtility.ToJson(Draft.AssemblyProduct.Resolve(feature.transform)),
                ExtendedInteractionKind.ModifyByContact => JsonUtility.ToJson(Draft.Contact),
                ExtendedInteractionKind.Dialogue => JsonUtility.ToJson(Draft.Dialogue),
                ExtendedInteractionKind.Outline => JsonUtility.ToJson(Draft.Outline),
                ExtendedInteractionKind.Unlock => JsonUtility.ToJson(Draft.Unlock.Resolve(feature.transform.root)),
                _ => string.Empty
            };
            JsonUtility.FromJsonOverwrite("{\"settings\":" + settings + ",\"tagChange\":" + JsonUtility.ToJson(Draft.TagChange)
                + ",\"visualEffect\":" + JsonUtility.ToJson(Draft.VisualEffect) + "}", feature);
            if (feature is ObjectSpawnManager manager && Draft.SpawnManagement.Animation.Enabled)
                InteractionStagingAuthoring.Prepare(manager, "Spawn Staging");
            if (feature is ObjectOutline outline)
                OutlineAuthoring.Rebuild(outline);
            EditorUtility.SetDirty(feature);
            if (!EditorUtility.IsPersistent(feature))
                PrefabUtility.RecordPrefabInstancePropertyModifications(feature);
        }

        #endregion

        #endregion
    }
}
