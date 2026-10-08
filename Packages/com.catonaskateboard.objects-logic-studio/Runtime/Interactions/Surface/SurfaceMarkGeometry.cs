using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Builds a small receiver-conforming patch, clipping triangles that would bridge surface edges.</summary>
    internal sealed class SurfaceMarkGeometry
    {
        #region State

        private const int resolution = 8;
        private readonly Vector3[] vertices = new Vector3[resolution * resolution];
        private readonly Vector3[] normals = new Vector3[resolution * resolution];
        private readonly Vector2[] uv = new Vector2[resolution * resolution];
        private readonly bool[] present = new bool[resolution * resolution];
        private readonly List<int> indices = new List<int>((resolution - 1) * (resolution - 1) * 6);

        #endregion
        #region Methods
        #region Projection

        /// <summary>Projects one patch only onto the collider that received the impact.</summary>
        /// <param name="mesh">Reusable output mesh.</param>
        /// <param name="receiver">Exact impacted collider, never neighbouring geometry.</param>
        /// <param name="point">Surface contact in world coordinates.</param>
        /// <param name="normal">Outward contact normal.</param>
        /// <param name="tangent">Distribution direction along the contact surface.</param>
        /// <param name="settings">Patch size, spread and projection limits.</param>
        /// <returns>True when at least one complete surface triangle was found.</returns>
        internal bool Build(Mesh mesh, Collider receiver, Vector3 point, Vector3 normal, Vector3 tangent, SurfaceTrailSettings settings)
        {
            tangent = Vector3.ProjectOnPlane(tangent, normal).normalized;
            if (tangent.sqrMagnitude < 0.001f)
                tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            tangent = Quaternion.AngleAxis(Random.Range(-35f, 35f), normal) * tangent;
            Vector3 side = Vector3.Cross(normal, tangent).normalized;
            float radius = Random.Range(settings.Radius.x, settings.Radius.y);
            float stretch = Random.Range(settings.Stretch.x, settings.Stretch.y);
            Vector2 scatter = Random.insideUnitCircle * settings.Scatter;
            point += tangent * scatter.x + side * scatter.y;
            float threshold = Mathf.Cos(settings.MaximumAngle * Mathf.Deg2Rad);
            Matrix4x4 local = receiver.transform.worldToLocalMatrix;
            Matrix4x4 localNormal = receiver.transform.localToWorldMatrix.transpose;
            // Per-vertex receiver raycasts prevent a mark from floating over an edge or leaking onto another object.
            for (int row = 0; row < resolution; row++)
                for (int column = 0; column < resolution; column++)
                {
                    int index = row * resolution + column;
                    uv[index] = new Vector2(column / (float)(resolution - 1), row / (float)(resolution - 1));
                    Vector3 sample = point + tangent * ((uv[index].x * 2f - 1f) * radius * stretch)
                        + side * ((uv[index].y * 2f - 1f) * radius);
                    present[index] = receiver.Raycast(new Ray(sample + normal * settings.ProjectionDepth, -normal), out RaycastHit hit,
                        settings.ProjectionDepth * 2f) && Vector3.Dot(hit.normal, normal) >= threshold;
                    vertices[index] = present[index] ? local.MultiplyPoint3x4(hit.point + hit.normal * settings.SurfaceOffset) : Vector3.zero;
                    normals[index] = present[index] ? localNormal.MultiplyVector(hit.normal).normalized : Vector3.up;
                }
            indices.Clear();
            for (int row = 0; row < resolution - 1; row++)
                for (int column = 0; column < resolution - 1; column++)
                {
                    int index = row * resolution + column;
                    Triangle(index, index + resolution, index + 1);
                    Triangle(index + 1, index + resolution, index + resolution + 1);
                }
            if (indices.Count == 0)
                return false;
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return true;
        }

        /// <summary>Adds a triangle only when all samples belong to the receiving surface.</summary>
        /// <param name="first">First grid vertex.</param>
        /// <param name="second">Second grid vertex.</param>
        /// <param name="third">Third grid vertex.</param>
        private void Triangle(int first, int second, int third)
        {
            if (!present[first] || !present[second] || !present[third])
                return;
            indices.Add(first);
            indices.Add(second);
            indices.Add(third);
        }

        #endregion
        #endregion
    }
}
