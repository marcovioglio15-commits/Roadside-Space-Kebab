using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Caches render-only prefab geometry without instantiating scripts, physics or UI.</summary>
    internal sealed class AssemblyPreviewGeometry
    {
        #region State

        /// <summary>One shared mesh and its material slots in product-local or guide-local space.</summary>
        private readonly struct Entry
        {
            internal readonly Mesh Mesh;
            internal readonly Material[] Materials;
            internal readonly Matrix4x4 Matrix;
            internal readonly int Magnet;

            /// <summary>Captures immutable rendering references during preview refresh.</summary>
            /// <param name="mesh">Shared source mesh.</param>
            /// <param name="materials">Saved renderer material slots.</param>
            /// <param name="matrix">Local mesh placement within its source root.</param>
            /// <param name="magnet">Guide slot index, or minus one for the product itself.</param>
            internal Entry(Mesh mesh, Material[] materials, Matrix4x4 matrix, int magnet)
            {
                // No source renderer is enabled, disabled or modified by this cache.
                Mesh = mesh;
                Materials = materials;
                Matrix = matrix;
                Magnet = magnet;
            }
        }

        private readonly List<Entry> entries = new List<Entry>();

        #endregion

        #region Methods

        #region Geometry

        /// <summary>Captures shared geometry when the prefab, guide assignments or source assets change.</summary>
        /// <param name="product">Current product prefab contents.</param>
        /// <param name="settings">Detached recipe layout with optional guide prefabs.</param>
        internal void Refresh(GameObject product, AssemblyProductSettings settings)
        {
            // Draw shared meshes directly; execute-always components and source cameras never run.
            entries.Clear();
            Add(product, -1, false);
            for (int index = 0; index < settings.Magnets.Length; index++)
                if (settings.Magnets[index].PreviewPrefab != null)
                    Add(settings.Magnets[index].PreviewPrefab, index, true);
        }

        /// <summary>Queues every cached submesh using the current magnet placement.</summary>
        /// <param name="preview">Owned editor preview renderer.</param>
        /// <param name="settings">Current detached magnet transforms.</param>
        internal void Draw(PreviewRenderUtility preview, AssemblyProductSettings settings)
        {
            // Layout edits update matrices immediately without rebuilding source mesh/material caches.
            foreach (Entry entry in entries)
            {
                if (entry.Mesh == null || entry.Magnet >= 0 && (entry.Magnet >= settings.Magnets.Length || !AssemblyValidation.ValidMagnet(settings.Magnets[entry.Magnet])))
                    continue;
                Matrix4x4 matrix = Placement(entry, settings);
                for (int submesh = 0; submesh < entry.Mesh.subMeshCount; submesh++)
                    if (entry.Materials.Length > 0 && entry.Materials[Mathf.Min(submesh, entry.Materials.Length - 1)] != null)
                        preview.DrawMesh(entry.Mesh, matrix, entry.Materials[Mathf.Min(submesh, entry.Materials.Length - 1)], submesh);
            }
        }

        /// <summary>Computes bounds for framing all geometry and empty magnet positions.</summary>
        /// <param name="settings">Current slot layout.</param>
        /// <param name="selected">Optional magnet index to frame only its guide and position.</param>
        /// <returns>Product-local bounds enclosing the requested geometry.</returns>
        internal Bounds Bounds(AssemblyProductSettings settings, int selected = -1)
        {
            // Transform all eight local corners so rotated or scaled meshes are fully framed.
            Bounds result = new Bounds(selected >= 0 ? settings.Magnets[selected].Position : Vector3.zero, Vector3.one * 0.1f);
            foreach (Entry entry in entries)
            {
                if (selected >= 0 && entry.Magnet != selected || entry.Mesh == null || entry.Magnet >= 0 && (entry.Magnet >= settings.Magnets.Length || !AssemblyValidation.ValidMagnet(settings.Magnets[entry.Magnet])))
                    continue;
                Matrix4x4 matrix = Placement(entry, settings);
                Bounds bounds = entry.Mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                    result.Encapsulate(matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f))));
            }
            if (selected < 0)
                foreach (AssemblyMagnet magnet in settings.Magnets)
                    if (AssemblyValidation.ValidMagnet(magnet))
                        result.Encapsulate(magnet.Position);
            return result;
        }

        /// <summary>Captures visible mesh renderers and skinned rest geometry from one source prefab.</summary>
        /// <param name="source">Product or optional ingredient guide.</param>
        /// <param name="magnet">Guide slot, or minus one for product geometry.</param>
        /// <param name="includeRootScale">Include the guide's root scale used by actual ingredient insertion.</param>
        private void Add(GameObject source, int magnet, bool includeRootScale)
        {
            // Inactive renderers are excluded from neutral placement guides.
            Matrix4x4 basis = (includeRootScale ? Matrix4x4.Scale(source.transform.localScale) : Matrix4x4.identity)
                * source.transform.worldToLocalMatrix;
            foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled)
                    continue;
                Mesh mesh = renderer switch
                {
                    SkinnedMeshRenderer skinned => skinned.sharedMesh,
                    MeshRenderer regular when regular.TryGetComponent(out MeshFilter filter) => filter.sharedMesh,
                    _ => null
                };
                if (mesh != null)
                    entries.Add(new Entry(mesh, renderer.sharedMaterials, basis * renderer.transform.localToWorldMatrix, magnet));
            }
        }

        /// <summary>Combines a cached local mesh matrix with its current guide slot.</summary>
        /// <param name="entry">Cached source geometry.</param>
        /// <param name="settings">Current magnet transforms.</param>
        /// <returns>The final product-local mesh matrix.</returns>
        private static Matrix4x4 Placement(Entry entry, AssemblyProductSettings settings)
        {
            // Product geometry has no slot; every guide follows its own magnet only.
            if (entry.Magnet < 0 || entry.Magnet >= settings.Magnets.Length)
                return entry.Matrix;
            AssemblyMagnet magnet = settings.Magnets[entry.Magnet];
            return Matrix4x4.TRS(magnet.Position, Quaternion.Euler(magnet.Rotation), magnet.Scale) * entry.Matrix;
        }

        #endregion

        #endregion
    }
}
