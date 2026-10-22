using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Renders cached tool meshes without instantiating gameplay scripts in the muzzle preview.</summary>
    internal sealed class PlayerToolPreviewGeometry : IDisposable
    {
        #region State

        /// <summary>Retains immutable mesh placement and shared material slots.</summary>
        private readonly struct Entry
        {
            internal readonly Mesh Mesh;
            internal readonly Matrix4x4 Matrix;
            internal readonly Material[] Materials;

            /// <summary>Captures one renderer during a geometry refresh.</summary>
            /// <param name="mesh">Shared or privately baked mesh.</param>
            /// <param name="matrix">Placement in the preview root's coordinates.</param>
            /// <param name="materials">Shared renderer materials.</param>
            internal Entry(Mesh mesh, Matrix4x4 matrix, Material[] materials)
            {
                Mesh = mesh;
                Matrix = matrix;
                Materials = materials;
            }
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<Mesh> baked = new List<Mesh>();
        private Bounds bounds;

        #endregion

        #region Properties

        /// <summary>Combined root-relative geometry bounds used by Frame.</summary>
        internal Bounds Bounds => bounds;

        #endregion

        #region Methods

        #region Geometry

        /// <summary>Captures visible static and skinned geometry only when the source changes.</summary>
        /// <param name="root">Sample hierarchy matching Tools Hierarchy Root.</param>
        internal void Refresh(GameObject root)
        {
            Dispose();
            bounds = new Bounds(Vector3.zero, Vector3.one * 0.1f);
            Matrix4x4 basis = root.transform.worldToLocalMatrix;
            // Bake only skinned meshes; ordinary renderers keep shared immutable geometry.
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled)
                    continue;
                Mesh mesh = null;
                switch (renderer)
                {
                    case SkinnedMeshRenderer skinned when skinned.sharedMesh != null:
                        mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                        skinned.BakeMesh(mesh);
                        baked.Add(mesh);
                        break;
                    case MeshRenderer regular when regular.TryGetComponent(out MeshFilter filter):
                        mesh = filter.sharedMesh;
                        break;
                }
                if (mesh == null)
                    continue;
                Matrix4x4 matrix = basis * renderer.transform.localToWorldMatrix;
                entries.Add(new Entry(mesh, matrix, renderer.sharedMaterials));
                // All eight corners preserve correct framing for rotated and scaled children.
                for (int corner = 0; corner < 8; corner++)
                    bounds.Encapsulate(matrix.MultiplyPoint3x4(mesh.bounds.center + Vector3.Scale(mesh.bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f))));
            }
        }

        /// <summary>Queues cached submeshes without changing source renderers or materials.</summary>
        /// <param name="preview">Owned preview renderer.</param>
        internal void Draw(PreviewRenderUtility preview)
        {
            foreach (Entry entry in entries)
                if (entry.Mesh != null && entry.Materials.Length > 0)
                    for (int submesh = 0; submesh < entry.Mesh.subMeshCount; submesh++)
                    {
                        Material material = entry.Materials[Mathf.Min(submesh, entry.Materials.Length - 1)];
                        if (material != null)
                            preview.DrawMesh(entry.Mesh, entry.Matrix, material, submesh);
                    }
        }

        /// <summary>Releases only privately baked preview meshes.</summary>
        public void Dispose()
        {
            foreach (Mesh mesh in baked)
                if (mesh != null)
                    UnityEngine.Object.DestroyImmediate(mesh);
            baked.Clear();
            entries.Clear();
        }

        #endregion

        #endregion
    }
}
