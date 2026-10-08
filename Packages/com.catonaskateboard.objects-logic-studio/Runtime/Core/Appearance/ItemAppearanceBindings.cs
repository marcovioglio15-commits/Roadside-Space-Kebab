using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Checks appearance targets before editor Apply, recipe activation or a runtime replacement.</summary>
    internal static class ItemAppearanceBindings
    {
        #region Methods

        #region Targets

        /// <summary>Resolves an owned mesh target and reports the exact missing dependency.</summary>
        /// <param name="item">Item receiving the replacement.</param>
        /// <param name="replacement">Requested mesh and collider policy.</param>
        /// <param name="target">Receives the validated hierarchy branch.</param>
        /// <param name="warning">Receives the item, route and failed requirement.</param>
        /// <returns>True when existing components can receive this mesh.</returns>
        internal static bool TryMesh(ObjectItem item, ContactMeshReplacement replacement, out Transform target, out string warning)
        {
            // Missing data never becomes an implicit root selection.
            target = null;
            warning = "Assign a mesh replacement and its mesh asset.";
            if (replacement == null || replacement.Mesh == null
                || !TryTarget(item, replacement.Path, out target, out warning))
                return false;
            if (target.GetComponent<MeshFilter>() == null && target.GetComponent<SkinnedMeshRenderer>() == null)
                warning = Describe(item, replacement.Path) + " needs a MeshFilter or SkinnedMeshRenderer. Select an existing mesh target or add its components in the prefab.";
            else if (replacement.UpdateCollider && target.GetComponent<MeshCollider>() == null)
                warning = Describe(item, replacement.Path) + " needs a MeshCollider while Update Collider is enabled.";
            return warning.Length == 0;
        }

        /// <summary>Resolves an existing renderer without capturing material arrays.</summary>
        /// <param name="item">Item receiving the material slots.</param>
        /// <param name="replacement">Requested renderer route.</param>
        /// <param name="renderer">Receives the owned renderer.</param>
        /// <param name="warning">Receives the exact invalid branch.</param>
        /// <returns>True when the material target belongs to this item.</returns>
        internal static bool TryRenderer(ObjectItem item, ItemMaterialReplacement replacement, out Renderer renderer, out string warning)
        {
            // Binding checks also serve eligibility queries without allocating snapshots.
            renderer = null;
            warning = "Assign a material replacement.";
            return replacement != null && TryRenderer(item, replacement.Path, out renderer, out warning);
        }

        /// <summary>Shares renderer ownership checks for materials and visibility changes.</summary>
        /// <param name="item">Item owning the renderer.</param>
        /// <param name="path">Relative hierarchy route.</param>
        /// <param name="renderer">Receives the existing renderer.</param>
        /// <param name="warning">Receives an invalid route or missing component.</param>
        /// <returns>True when the selected renderer belongs to the item.</returns>
        internal static bool TryRenderer(ObjectItem item, string path, out Renderer renderer, out string warning)
        {
            // Never add renderer components when a configured branch is missing.
            renderer = null;
            if (!TryTarget(item, path, out Transform target, out warning))
                return false;
            if (!target.TryGetComponent(out renderer))
                warning = Describe(item, path) + " needs a Renderer.";
            return warning.Length == 0;
        }

        /// <summary>Rejects missing, ambiguous and independently owned branches consistently.</summary>
        /// <param name="item">Expected owner of the branch.</param>
        /// <param name="path">Relative route; an empty string selects the root.</param>
        /// <param name="target">Receives the unique owned branch.</param>
        /// <param name="warning">Receives the failed ownership or hierarchy requirement.</param>
        /// <returns>True when the route identifies one branch owned by the item.</returns>
        private static bool TryTarget(ObjectItem item, string path, out Transform target, out string warning)
        {
            // Resolve only at validation or transaction boundaries, never during interpolation.
            target = null;
            warning = "Appearance replacements require an Object Item and a selected hierarchy target.";
            if (item == null || path == null)
                return false;
            target = ItemMeshChanges.Resolve(item.transform, path);
            warning = target == null ? Describe(item, path) + " is missing or ambiguous. Select its target again."
                : !item.Owns(target) ? Describe(item, path) + " belongs to a different Object Item." : string.Empty;
            return warning.Length == 0;
        }

        /// <summary>Labels failed bindings with their owning prefab and selected branch.</summary>
        /// <param name="item">Validated non-null owner.</param>
        /// <param name="path">Relative route used by the replacement.</param>
        /// <returns>A compact item and branch description for warnings.</returns>
        private static string Describe(ObjectItem item, string path)
        {
            // Format diagnostics only on failure; valid runtime queries allocate no messages.
            return $"'{item.name}' / {(path.Length == 0 || path == "." ? "Root" : path)}";
        }

        #endregion

        #endregion
    }
}
