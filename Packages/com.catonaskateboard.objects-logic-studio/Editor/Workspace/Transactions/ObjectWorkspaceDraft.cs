using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Keeps one interaction's values and baseline while another interaction is selected.</summary>
    [Serializable]
    internal sealed class ObjectWorkspaceDraft
    {
        #region Fields

        [Header("Draft")]
        [Tooltip("Stable interaction selection.")]
        public string Key;
        [Tooltip("Object and prefab linked to this draft.")]
        public ObjectWorkspaceTarget Target;
        [Tooltip("Selected interaction.")]
        public long SelectedId;
        [Tooltip("Whether the object has pending settings.")]
        public bool Changed;
        [Tooltip("Selected control family.")]
        public ObjectInteractionCategory Category;
        [Tooltip("Selected hover preset.")]
        public HoverPreset Source;
        [Tooltip("Hover preset before editing.")]
        public HoverPreset OriginalPreset;
        [Tooltip("Preset state before editing.")]
        public string SourceBaseline;
        [Tooltip("Pending hover settings.")]
        public HoverConfiguration Hover;
        [Tooltip("Hover settings before editing.")]
        public HoverConfiguration HoverBaseline;
        [Tooltip("Pending hover binding.")]
        public HoverBindingDraft Binding;
        [Tooltip("Hover binding before editing.")]
        public HoverBindingDraft OriginalBinding;
        [Tooltip("Hierarchy before editing.")]
        public string Hierarchy;
        [Tooltip("Whether this draft contains a hover.")]
        public bool HasBinding;
        [Tooltip("Single-action draft and baseline.")]
        public SingleInteractionSession Single;
        [Tooltip("Extended-action draft and baseline.")]
        public ExtendedInteractionSession Extended;

        #endregion

        #region Methods

        #region Snapshots

        /// <summary>Copies the active proposal without sharing mutable settings with the next selection.</summary>
        /// <param name="state">Workspace to capture.</param>
        /// <returns>A serializable proposal retaining native asset references.</returns>
        internal static ObjectWorkspaceDraft Capture(ObjectWorkspace state)
        {
            return new ObjectWorkspaceDraft
            {
                Key = state.DraftKey, Target = ObjectWorkspace.Copy(state.Target), SelectedId = state.Navigation.SelectedId,
                Changed = state.PrefabChanged, Category = state.Category, Source = state.Source, OriginalPreset = state.OriginalPreset,
                SourceBaseline = state.SourceBaseline, Hover = ObjectWorkspace.Copy(state.Draft), HoverBaseline = ObjectWorkspace.Copy(state.Baseline),
                Binding = ObjectWorkspace.Copy(state.Binding), OriginalBinding = ObjectWorkspace.Copy(state.OriginalBinding),
                Hierarchy = state.Hierarchy, HasBinding = state.HasBinding,
                Single = ObjectWorkspace.Copy(state.Single), Extended = ObjectWorkspace.Copy(state.Extended)
            };
        }

        /// <summary>Restores a proposal without touching the scene setup or navigation preferences.</summary>
        /// <param name="state">Workspace receiving this proposal.</param>
        internal void Restore(ObjectWorkspace state)
        {
            state.DraftKey = Key;
            state.Target = ObjectWorkspace.Copy(Target);
            state.Navigation.SelectedId = SelectedId;
            state.Category = Category;
            state.Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(Target.PrefabGuid));
            state.Source = Source;
            state.OriginalPreset = OriginalPreset;
            state.SourceBaseline = SourceBaseline;
            state.Draft = ObjectWorkspace.Copy(Hover);
            state.Baseline = ObjectWorkspace.Copy(HoverBaseline);
            state.Binding = ObjectWorkspace.Copy(Binding);
            state.OriginalBinding = ObjectWorkspace.Copy(OriginalBinding);
            state.Hierarchy = Hierarchy;
            state.HasBinding = HasBinding;
            state.Single = ObjectWorkspace.Copy(Single);
            state.Extended = ObjectWorkspace.Copy(Extended);
        }

        #endregion

        #endregion
    }
}
