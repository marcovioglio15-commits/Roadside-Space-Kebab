using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Replaces the shared material slots on one existing renderer.</summary>
    [Serializable]
    public sealed class ItemMaterialReplacement
    {
        #region Fields

        [Header("Material Replacement")]
        [Tooltip("Renderer selected from the affected item's hierarchy. Empty addresses the root renderer.")]
        public string Path = string.Empty;
        [Tooltip("Shared material assets assigned in slot order without instantiating materials.")]
        public Material[] Materials = Array.Empty<Material>();

        #endregion
    }

    /// <summary>Groups optional mesh and material substitutions for one item.</summary>
    [Serializable]
    public sealed class ItemAppearanceSettings
    {
        #region Fields

        [Header("Appearance Changes")]
        [Tooltip("Mesh replacements on existing branches of the affected item.")]
        public ContactMeshReplacement[] Meshes = Array.Empty<ContactMeshReplacement>();
        [Tooltip("Shared material replacements on existing renderers of the affected item.")]
        public ItemMaterialReplacement[] Materials = Array.Empty<ItemMaterialReplacement>();

        #endregion

        #region Properties

        /// <summary>Whether this configuration requests any visual substitution.</summary>
        public bool HasChanges => Meshes != null && Materials != null && (Meshes.Length > 0 || Materials.Length > 0);

        #endregion

        #region Methods

        #region Validation

        /// <summary>Validates a saved appearance configuration before binding it to an item.</summary>
        /// <param name="warning">Receives invalid or duplicate replacement data.</param>
        /// <returns>True when all configured replacements have valid asset references.</returns>
        public bool TryValidate(out string warning)
        {
            // Empty replacement lists leave the item unchanged.
            return TryValidate(Meshes, Materials, out warning);
        }

        /// <summary>Checks existing destination components without capturing reversible appearance snapshots.</summary>
        /// <param name="item">Item whose hierarchy supplies the selected branches.</param>
        /// <returns>True when every requested mesh and renderer belongs to this item.</returns>
        public bool CanBind(ObjectItem item)
        {
            // Data validation precedes this lightweight eligibility check.
            if (item == null || Meshes == null || Materials == null)
                return false;
            foreach (ContactMeshReplacement replacement in Meshes)
                if (replacement == null || !ItemMeshChanges.CanBind(item, ItemMeshChanges.Resolve(item.transform, replacement.Path), replacement))
                    return false;
            foreach (ItemMaterialReplacement replacement in Materials)
            {
                Transform branch = replacement != null ? ItemMeshChanges.Resolve(item.transform, replacement.Path) : null;
                if (branch == null || !item.Owns(branch) || !branch.TryGetComponent(out Renderer _))
                    return false;
            }
            return true;
        }

        /// <summary>Shares replacement validation with Slice sequences without copying arrays.</summary>
        /// <param name="meshes">Requested mesh changes.</param>
        /// <param name="materials">Requested renderer material arrays.</param>
        /// <param name="warning">Receives the first invalid replacement.</param>
        /// <returns>True when each branch has at most one valid replacement of each type.</returns>
        internal static bool TryValidate(ContactMeshReplacement[] meshes, ItemMaterialReplacement[] materials, out string warning)
        {
            // Duplicate branches would make results depend on array order.
            warning = "Assign complete replacement arrays with unique hierarchy selections and non-null assets.";
            if (meshes == null || materials == null)
                return false;
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (ContactMeshReplacement mesh in meshes)
                if (mesh == null || mesh.Path == null || mesh.Mesh == null || !paths.Add(mesh.Path))
                    return false;
            paths.Clear();
            foreach (ItemMaterialReplacement replacement in materials)
            {
                if (replacement == null || replacement.Path == null || !paths.Add(replacement.Path)
                    || replacement.Materials == null || replacement.Materials.Length == 0)
                    return false;
                foreach (Material material in replacement.Materials)
                    if (material == null)
                        return false;
            }
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
