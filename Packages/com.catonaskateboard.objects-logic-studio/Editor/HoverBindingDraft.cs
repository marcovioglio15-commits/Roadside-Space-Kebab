using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Retains per-object binding edits separately from a reusable hover preset.</summary>
    [Serializable]
    internal sealed class HoverBindingDraft
    {
        #region Fields

        [Header("Binding")]
        [Tooltip("Name distinguishing this interaction on its object.")]
        public string Name = "First Person Hover";
        [Tooltip("Whether this interaction is enabled after Apply.")]
        public bool Enabled = true;
        [Tooltip("Show selected-object range and anchor gizmos.")]
        public bool DrawGizmos = true;
        [Tooltip("Prefab-local anchor route retained across closing and reopening the workspace.")]
        public string AnchorPath = "-";

        [Tooltip("Independent optional start effect retained on this exact interaction, including its own prefab and timing.")]
        public InteractionVfxSettings VisualEffect = new InteractionVfxSettings();

        [Tooltip("Optional item tag change retained independently of reusable settings presets.")]
        public InteractionTagChange TagChange = new InteractionTagChange();

        #endregion

        #region Methods

        #region Capture

        /// <summary>Reads the applied binding without changing the component.</summary>
        /// <param name="hover">Interaction currently selected.</param>
        /// <returns>An independent binding snapshot.</returns>
        internal static HoverBindingDraft Capture(ObjectHover hover)
        {
            // Null supports editing a preset before attaching it to an object.
            return hover == null ? new HoverBindingDraft() : new HoverBindingDraft
            {
                TagChange = ObjectWorkspace.Copy(hover.TagChange),
                VisualEffect = ObjectWorkspace.Copy(hover.VisualEffect),
                Name = hover.InteractionName,
                Enabled = hover.enabled,
                DrawGizmos = hover.DrawGizmos,
                AnchorPath = HoverHierarchy.Path(hover.transform, hover.Anchor)
            };
        }

        #endregion

        #endregion
    }
}
