using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Binds reversible appearance changes once at an interaction or assembly boundary.</summary>
    internal sealed class ItemAppearanceChanges
    {
        #region State

        private readonly ItemMeshChanges meshes;
        private readonly Renderer[] renderers;
        private readonly Material[][] original;
        private readonly ItemMaterialReplacement[] replacements;
        private readonly ItemVisibilityChanges visibility;

        #endregion

        #region Methods

        #region Binding

        /// <summary>Captures original material arrays for later ingredient detachment or recipe rollback.</summary>
        /// <param name="geometry">Prepared mesh changes with their original geometry.</param>
        /// <param name="targets">Resolved destination renderers.</param>
        /// <param name="materials">Validated replacement arrays.</param>
        /// <param name="rendererVisibility">Prepared visibility changes, or null when unused.</param>
        private ItemAppearanceChanges(ItemMeshChanges geometry, Renderer[] targets, ItemMaterialReplacement[] materials,
            ItemVisibilityChanges rendererVisibility)
        {
            // Material arrays are captured once; no material asset is modified or instantiated.
            meshes = geometry;
            renderers = targets;
            replacements = materials;
            visibility = rendererVisibility;
            original = new Material[targets.Length][];
            for (int index = 0; index < targets.Length; index++)
                original[index] = targets[index].sharedMaterials;
        }

        /// <summary>Prepares a grouped configuration without altering the affected item.</summary>
        /// <param name="item">Item owning the existing target hierarchy.</param>
        /// <param name="settings">Appearance settings to bind.</param>
        /// <param name="changes">Receives an independent prepared operation.</param>
        /// <param name="warning">Receives invalid data or missing geometry.</param>
        /// <returns>True when every requested branch is available.</returns>
        internal static bool TryPrepare(ObjectItem item, ItemAppearanceSettings settings, out ItemAppearanceChanges changes, out string warning)
        {
            // A missing serialized group is invalid rather than silently replaced with defaults.
            changes = null;
            warning = "Configure the appearance replacement lists.";
            return settings != null && ItemVisibilityChanges.TryPrepare(item, settings.Visibility, out ItemVisibilityChanges visibility, out warning)
                && TryPrepare(item, settings.Meshes, settings.Materials, out changes, out warning, visibility);
        }

        /// <summary>Resolves all mesh and material branches before any replacement is committed.</summary>
        /// <param name="item">Item owning the existing target hierarchy.</param>
        /// <param name="geometry">Requested mesh replacements.</param>
        /// <param name="materials">Requested material replacements.</param>
        /// <param name="changes">Receives the complete prepared operation.</param>
        /// <param name="warning">Receives an invalid renderer or mesh binding.</param>
        /// <param name="visibility">Optional prepared visibility operation for grouped appearance settings.</param>
        /// <returns>True when the entire appearance change can be applied.</returns>
        internal static bool TryPrepare(ObjectItem item, ContactMeshReplacement[] geometry, ItemMaterialReplacement[] materials,
            out ItemAppearanceChanges changes, out string warning, ItemVisibilityChanges visibility = null)
        {
            // Preparation occurs at transaction boundaries, never while interpolating a frame.
            changes = null;
            if (!ItemAppearanceSettings.TryValidate(geometry, materials, out warning)
                || !ItemMeshChanges.TryPrepare(item, geometry, out ItemMeshChanges meshes, out warning))
                return false;
            Renderer[] targets = new Renderer[materials.Length];
            for (int index = 0; index < targets.Length; index++)
                if (!ItemAppearanceBindings.TryRenderer(item, materials[index], out targets[index], out warning))
                    return false;
            changes = new ItemAppearanceChanges(meshes, targets, materials, visibility);
            return true;
        }

        #endregion

        #region Application

        /// <summary>Applies the complete prepared appearance without changing source assets.</summary>
        internal void Commit()
        {
            // The destination hierarchy was validated before ownership changed.
            meshes.Commit();
            visibility?.Apply(false);
            for (int index = 0; index < renderers.Length; index++)
                if (renderers[index] != null)
                    renderers[index].sharedMaterials = replacements[index].Materials;
        }

        /// <summary>Restores the appearance captured before this operation.</summary>
        internal void Restore()
        {
            // Detachment and recipe rollback restore only components owned by this operation.
            meshes.Restore();
            visibility?.Apply(true);
            for (int index = 0; index < renderers.Length; index++)
                if (renderers[index] != null)
                    renderers[index].sharedMaterials = original[index];
        }

        #endregion

        #endregion
    }
}
