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

        #endregion

        #region Methods

        #region Binding

        /// <summary>Captures original material arrays for later ingredient detachment or recipe rollback.</summary>
        /// <param name="geometry">Prepared mesh changes with their original geometry.</param>
        /// <param name="targets">Resolved destination renderers.</param>
        /// <param name="materials">Validated replacement arrays.</param>
        private ItemAppearanceChanges(ItemMeshChanges geometry, Renderer[] targets, ItemMaterialReplacement[] materials)
        {
            // Material arrays are captured once; no material asset is modified or instantiated.
            meshes = geometry;
            renderers = targets;
            replacements = materials;
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
            return settings != null && TryPrepare(item, settings.Meshes, settings.Materials, out changes, out warning);
        }

        /// <summary>Resolves all mesh and material branches before any replacement is committed.</summary>
        /// <param name="item">Item owning the existing target hierarchy.</param>
        /// <param name="geometry">Requested mesh replacements.</param>
        /// <param name="materials">Requested material replacements.</param>
        /// <param name="changes">Receives the complete prepared operation.</param>
        /// <param name="warning">Receives an invalid renderer or mesh binding.</param>
        /// <returns>True when the entire appearance change can be applied.</returns>
        internal static bool TryPrepare(ObjectItem item, ContactMeshReplacement[] geometry, ItemMaterialReplacement[] materials,
            out ItemAppearanceChanges changes, out string warning)
        {
            // Preparation occurs at transaction boundaries, never while interpolating a frame.
            changes = null;
            if (!ItemAppearanceSettings.TryValidate(geometry, materials, out warning)
                || !ItemMeshChanges.TryPrepare(item, geometry, out ItemMeshChanges meshes, out warning))
                return false;
            Renderer[] targets = new Renderer[materials.Length];
            for (int index = 0; index < targets.Length; index++)
            {
                Transform branch = ItemMeshChanges.Resolve(item.transform, materials[index].Path);
                if (branch == null || !item.Owns(branch) || !branch.TryGetComponent(out targets[index]))
                {
                    warning = "A material target is missing, ambiguous, or has no owned Renderer.";
                    return false;
                }
            }
            changes = new ItemAppearanceChanges(meshes, targets, materials);
            return true;
        }

        #endregion

        #region Application

        /// <summary>Applies the complete prepared appearance without changing source assets.</summary>
        internal void Commit()
        {
            // The destination hierarchy was validated before ownership changed.
            meshes.Commit();
            for (int index = 0; index < renderers.Length; index++)
                if (renderers[index] != null)
                    renderers[index].sharedMaterials = replacements[index].Materials;
        }

        /// <summary>Restores the appearance captured before this operation.</summary>
        internal void Restore()
        {
            // Detachment and recipe rollback restore only components owned by this operation.
            meshes.Restore();
            for (int index = 0; index < renderers.Length; index++)
                if (renderers[index] != null)
                    renderers[index].sharedMaterials = original[index];
        }

        #endregion

        #endregion
    }
}
