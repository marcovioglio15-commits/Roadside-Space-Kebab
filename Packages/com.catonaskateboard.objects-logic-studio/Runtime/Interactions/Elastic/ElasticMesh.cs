using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Deforms a private visual mesh while retaining the original asset for colliders and appearance transactions.</summary>
    internal sealed class ElasticMesh : IDisposable
    {
        #region State

        private readonly MeshFilter filter;
        private readonly SkinnedMeshRenderer skinned;
        private readonly Transform target;
        private Mesh source;
        private Mesh instance;
        private Vector3[] positions;
        private Vector3[] normals;
        private Vector3[] output;
        private Vector3[] outputNormals;
        private Vector4[] tangents;
        private Vector4[] outputTangents;
        private Bounds originalSkinBounds;
        private Mesh rejected;

        #endregion
        #region Methods
        #region Ownership

        /// <summary>Captures renderer routing without allocating a mesh before its first response.</summary>
        /// <param name="renderer">Owned static or skinned renderer.</param>
        internal ElasticMesh(Renderer renderer)
        {
            // Only visual mesh references are changed; physics keeps its authored shared mesh.
            target = renderer.transform;
            skinned = renderer as SkinnedMeshRenderer;
            filter = renderer.GetComponent<MeshFilter>();
        }

        /// <summary>Returns visual geometry to the current source without undoing an external replacement.</summary>
        internal void Restore()
        {
            if (target != null && instance != null && Current() == instance)
            {
                Assign(source);
                if (skinned != null)
                    skinned.localBounds = originalSkinBounds;
            }
        }

        /// <summary>Releases private buffers when the feature or its geometry is rebuilt.</summary>
        public void Dispose()
        {
            Restore();
            if (instance != null)
                UnityEngine.Object.Destroy(instance);
            instance = null;
        }

        /// <summary>Resolves the renderer's current mesh without allocating an implicit MeshFilter copy.</summary>
        /// <returns>The visible shared mesh, or null after hierarchy destruction.</returns>
        private Mesh Current() => skinned != null ? skinned.sharedMesh : filter != null ? filter.sharedMesh : null;

        /// <summary>Assigns a visual mesh without touching materials or collider references.</summary>
        /// <param name="mesh">Original or privately deformed mesh.</param>
        private void Assign(Mesh mesh)
        {
            if (skinned != null)
                skinned.sharedMesh = mesh;
            else if (filter != null)
                filter.sharedMesh = mesh;
        }

        /// <summary>Refreshes cached buffers only when an appearance operation selects a different source mesh.</summary>
        /// <returns>True when a readable source is available.</returns>
        private bool Prepare()
        {
            Mesh mesh = Current();
            if (mesh == instance && instance != null || mesh == source && instance != null)
                return true;
            if (mesh == null || !mesh.isReadable)
            {
                if (mesh != null && rejected != mesh)
                    Debug.LogWarning("Elastic Deformation needs Read/Write enabled on mesh '" + mesh.name + "'. Prepare its meshes in Objects Logic Studio.", target);
                rejected = mesh;
                return false;
            }
            Dispose();
            source = mesh;
            instance = UnityEngine.Object.Instantiate(source);
            instance.name = source.name + " Elastic Instance";
            instance.MarkDynamic();
            positions = source.vertices;
            normals = source.normals;
            tangents = source.tangents;
            output = new Vector3[positions.Length];
            outputNormals = normals.Length == positions.Length ? new Vector3[positions.Length] : Array.Empty<Vector3>();
            outputTangents = tangents.Length == positions.Length ? new Vector4[positions.Length] : Array.Empty<Vector4>();
            if (skinned != null)
                originalSkinBounds = skinned.localBounds;
            return true;
        }

        #endregion
        #region Deformation

        /// <summary>Applies impact and sustained compression in owner-local coordinates using cached vertex buffers.</summary>
        /// <param name="owner">Stable gameplay root, never moved by this operation.</param>
        /// <param name="pose">Frame's shared owner-local deformation.</param>
        internal void Apply(Transform owner, ElasticPose pose)
        {
            if (target == null || !Prepare())
                return;
            Matrix4x4 toOwner = owner.worldToLocalMatrix * target.localToWorldMatrix;
            Matrix4x4 fromOwner = toOwner.inverse;
            Matrix4x4 normalToOwner = fromOwner.transpose;
            Matrix4x4 normalFromOwner = toOwner.transpose;
            Vector3 minimum = Vector3.positiveInfinity;
            Vector3 maximum = Vector3.negativeInfinity;
            // Original positions and normals remain immutable, preventing accumulated shape drift.
            for (int index = 0; index < positions.Length; index++)
            {
                output[index] = fromOwner.MultiplyPoint3x4(pose.Point(toOwner.MultiplyPoint3x4(positions[index])));
                minimum = Vector3.Min(minimum, output[index]);
                maximum = Vector3.Max(maximum, output[index]);
                if (outputTangents.Length > 0)
                {
                    Vector3 tangent = toOwner.MultiplyVector(tangents[index]);
                    tangent = fromOwner.MultiplyVector(pose.Vector(tangent)).normalized;
                    outputTangents[index] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[index].w);
                }
                if (outputNormals.Length == 0)
                    continue;
                Vector3 normal = normalToOwner.MultiplyVector(normals[index]);
                outputNormals[index] = normalFromOwner.MultiplyVector(pose.Normal(normal)).normalized;
            }
            instance.SetVertices(output, 0, output.Length, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
            if (outputNormals.Length > 0)
                instance.SetNormals(outputNormals);
            if (outputTangents.Length > 0)
                instance.SetTangents(outputTangents);
            if (output.Length > 0)
            {
                instance.bounds = new Bounds((minimum + maximum) * 0.5f, maximum - minimum);
                if (skinned != null)
                {
                    Bounds bounds = originalSkinBounds;
                    bounds.Encapsulate(instance.bounds);
                    skinned.localBounds = bounds;
                }
            }
            Assign(instance);
        }

        #endregion
        #endregion
    }
}
