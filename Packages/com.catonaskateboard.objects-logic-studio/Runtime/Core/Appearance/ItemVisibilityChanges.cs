using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Changes only a renderer's visibility, preserving its object's physics and interactions.</summary>
    [Serializable]
    public sealed class ItemVisibilitySettings
    {
        #region Fields

        [Header("Renderer Visibility")]
        [Tooltip("Existing renderer in the item's hierarchy. An empty path selects the root renderer.")]
        public string Path = string.Empty;
        [Tooltip("Show this renderer when the step is active. Disable to hide its entire mesh.")]
        public bool Visible;

        #endregion
    }

    /// <summary>Captures renderer visibility once and restores it when an appearance step is replaced.</summary>
    internal sealed class ItemVisibilityChanges
    {
        #region State

        private readonly Renderer[] renderers;
        private readonly bool[] original;
        private readonly ItemVisibilitySettings[] settings;

        #endregion
        #region Methods
        #region Binding

        /// <summary>Stores the original state before any step alters the renderers.</summary>
        /// <param name="targets">Validated existing renderers.</param>
        /// <param name="configuration">Visibility requested by this step.</param>
        private ItemVisibilityChanges(Renderer[] targets, ItemVisibilitySettings[] configuration)
        {
            renderers = targets;
            settings = configuration;
            original = new bool[targets.Length];
            for (int index = 0; index < targets.Length; index++)
                original[index] = targets[index].enabled;
        }

        /// <summary>Rejects missing and duplicate routes without changing the saved list.</summary>
        /// <param name="configuration">Visibility changes to validate.</param>
        /// <param name="warning">Receives the first invalid route.</param>
        /// <returns>True when every route is unique and explicitly selected.</returns>
        internal static bool TryValidate(ItemVisibilitySettings[] configuration, out string warning)
        {
            warning = "Visibility changes need a complete list with unique renderer paths.";
            if (configuration == null)
                return false;
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemVisibilitySettings entry in configuration)
                if (entry == null || entry.Path == null || !paths.Add(entry.Path))
                    return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Resolves visibility targets before any appearance operation is committed.</summary>
        /// <param name="item">Owner of the renderers.</param>
        /// <param name="configuration">Validated visibility changes.</param>
        /// <param name="changes">Receives the reversible operation.</param>
        /// <param name="warning">Receives a missing renderer or invalid route.</param>
        /// <returns>True when every requested renderer can be changed.</returns>
        internal static bool TryPrepare(ObjectItem item, ItemVisibilitySettings[] configuration,
            out ItemVisibilityChanges changes, out string warning)
        {
            changes = null;
            if (!TryValidate(configuration, out warning))
                return false;
            Renderer[] targets = new Renderer[configuration.Length];
            for (int index = 0; index < targets.Length; index++)
                if (!ItemAppearanceBindings.TryRenderer(item, configuration[index].Path, out targets[index], out warning))
                    return false;
            changes = new ItemVisibilityChanges(targets, configuration);
            return true;
        }

        #endregion
        #region Application

        /// <summary>Applies visibility or restores the captured state without disabling GameObjects.</summary>
        /// <param name="restore">Whether to restore the original renderer state.</param>
        internal void Apply(bool restore)
        {
            // Destroyed hierarchy branches cannot be restored and require no replacement allocation.
            for (int index = 0; index < renderers.Length; index++)
                if (renderers[index] != null)
                    renderers[index].enabled = restore ? original[index] : settings[index].Visible;
        }

        #endregion
        #endregion
    }
}
