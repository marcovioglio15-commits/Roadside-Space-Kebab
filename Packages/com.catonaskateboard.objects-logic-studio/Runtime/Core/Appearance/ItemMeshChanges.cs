using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Prepares and commits mesh substitutions shared by contact effects and slicing.</summary>
    internal sealed class ItemMeshChanges
    {
        #region Bindings

        /// <summary>Stores existing mesh components so completion performs no hierarchy discovery.</summary>
        private readonly struct MeshBinding
        {
            internal readonly MeshFilter Filter;
            internal readonly SkinnedMeshRenderer Skin;
            internal readonly MeshCollider Collider;
            internal readonly MeshCollider Proxy;
            internal readonly Mesh Mesh;
            internal readonly Mesh OriginalFilter;
            internal readonly Mesh OriginalSkin;
            internal readonly Mesh OriginalCollider;
            internal readonly Mesh OriginalProxy;

            /// <summary>Binds a validated replacement to its existing components.</summary>
            /// <param name="target">Validated mesh branch.</param>
            /// <param name="replacement">Replacement and collider policy.</param>
            internal MeshBinding(Transform target, ContactMeshReplacement replacement)
            {
                // Component discovery belongs to contact activation, never color-animation frames.
                Filter = target.GetComponent<MeshFilter>();
                Skin = target.GetComponent<SkinnedMeshRenderer>();
                Collider = replacement.UpdateCollider ? target.GetComponent<MeshCollider>() : null;
                ObjectAssemblyPart part = Collider != null ? target.GetComponentInParent<ObjectAssemblyPart>() : null;
                Proxy = part != null ? part.Proxy(Collider) as MeshCollider : null;
                Mesh = replacement.Mesh;
                OriginalFilter = Filter != null ? Filter.sharedMesh : null;
                OriginalSkin = Skin != null ? Skin.sharedMesh : null;
                OriginalCollider = Collider != null ? Collider.sharedMesh : null;
                OriginalProxy = Proxy != null ? Proxy.sharedMesh : null;
            }
        }

        #endregion

        #region State

        private readonly MeshBinding[] meshes;
        private readonly ObjectGrab grab;

        #endregion

        #region Methods

        #region Preparation

        /// <summary>Retains prepared geometry without touching its source assets.</summary>
        /// <param name="item">Item owning the mesh branches.</param>
        /// <param name="bindings">Validated substitutions for this transaction.</param>
        private ItemMeshChanges(ObjectItem item, MeshBinding[] bindings)
        {
            // Geometry refresh happens only after all substitutions are committed.
            grab = item.GetComponent<ObjectGrab>();
            meshes = bindings;
        }

        /// <summary>Resolves every substitution before any participant or step changes its appearance.</summary>
        /// <param name="item">Item owning the affected hierarchy.</param>
        /// <param name="replacements">Mesh references and collider policies.</param>
        /// <param name="changes">Receives a complete prepared transaction.</param>
        /// <param name="warning">Receives a missing or ambiguous component binding.</param>
        /// <returns>True when every requested component is available.</returns>
        internal static bool TryPrepare(ObjectItem item, ContactMeshReplacement[] replacements, out ItemMeshChanges changes, out string warning)
        {
            // Preparation is a step boundary; ordinary frames never search child paths.
            changes = null;
            warning = "Mesh replacements require an Object Item and a complete list.";
            if (item == null || replacements == null)
                return false;
            if (replacements.Length > 0 && item.TryGetComponent(out ObjectElasticDeformation elastic))
                elastic.RestoreGeometry();
            MeshBinding[] bindings = new MeshBinding[replacements.Length];
            for (int index = 0; index < replacements.Length; index++)
            {
                ContactMeshReplacement replacement = replacements[index];
                if (!ItemAppearanceBindings.TryMesh(item, replacement, out Transform branch, out warning))
                    return false;
                bindings[index] = new MeshBinding(branch, replacement);
            }
            changes = new ItemMeshChanges(item, bindings);
            warning = string.Empty;
            return true;
        }

        /// <summary>Resolves a reusable named route only when each segment identifies exactly one child.</summary>
        /// <param name="root">Affected item root.</param>
        /// <param name="path">Slash-separated child names; empty selects the root.</param>
        /// <returns>The unique matching branch, or null.</returns>
        internal static Transform Resolve(Transform root, string path)
        {
            // Parsing occurs only when the contact starts; no per-frame path allocations are needed.
            if (string.IsNullOrEmpty(path) || path == ".")
                return root;
            foreach (string segment in path.Split('/'))
            {
                Transform found = null;
                foreach (Transform child in root)
                    if (child.name == segment)
                    {
                        if (found != null)
                            return null;
                        found = child;
                    }
                if (found == null)
                    return null;
                root = found;
            }
            return root;
        }

        #endregion

        #region Commit

        /// <summary>Updates existing render and physics geometry once the transaction has succeeded.</summary>
        internal void Commit()
        {
            // Commit and rollback share the same geometry-refresh boundary.
            Apply(false);
        }

        /// <summary>Restores the captured mesh references after a reversible appearance change.</summary>
        internal void Restore()
        {
            // Restore existing references without rebuilding their source assets.
            Apply(true);
        }

        /// <summary>Updates bound geometry and refreshes carry caches once per transaction.</summary>
        /// <param name="restore">Whether to restore the captured geometry instead of the replacement.</param>
        private void Apply(bool restore)
        {
            // Shared asset references change on components; their source mesh assets remain untouched.
            foreach (MeshBinding mesh in meshes)
            {
                if (mesh.Filter != null)
                    mesh.Filter.sharedMesh = restore ? mesh.OriginalFilter : mesh.Mesh;
                if (mesh.Skin != null)
                    mesh.Skin.sharedMesh = restore ? mesh.OriginalSkin : mesh.Mesh;
                if (mesh.Collider != null)
                    mesh.Collider.sharedMesh = restore ? mesh.OriginalCollider : mesh.Mesh;
                if (mesh.Proxy != null)
                    mesh.Proxy.sharedMesh = restore ? mesh.OriginalProxy : mesh.Mesh;
            }
            if (meshes.Length > 0 && grab != null)
                grab.RefreshCarryGeometry();
        }

        #endregion

        #endregion
    }
}
