using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Renders all pooled sauce as connected round columns, with free droplets only after breakup.</summary>
    internal sealed class LiquidStreamMesh : IDisposable
    {
        #region State
        private const int sides = 8;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly Vector3[] sphereVertices;
        private readonly int[] sphereTriangles;
        private readonly Vector2[] circle = new Vector2[sides];
        private readonly List<Vector3> vertices;
        private readonly List<Vector3> normals;
        private readonly List<Color> colors;
        private readonly List<Vector2> surfaces;
        private readonly List<int> triangles;
        #endregion
        #region Methods
        #region Lifetime
        /// <summary>Allocates one mesh and fixed-capacity buffers for the entire physical pool.</summary>
        /// <param name="root">World-space pool root, never parented to a moving source.</param>
        /// <param name="material">Shared glossy liquid material.</param>
        /// <param name="sphere">Shared unit sphere used for detached drops and leading tips.</param>
        /// <param name="capacity">Maximum physical samples represented by the mesh.</param>
        internal LiquidStreamMesh(Transform root, Material material, Mesh sphere, int capacity)
        {
            sphereVertices = sphere.vertices;
            sphereTriangles = sphere.triangles;
            int vertexCapacity = capacity * (sphereVertices.Length + sides * 4);
            vertices = new List<Vector3>(vertexCapacity);
            normals = new List<Vector3>(vertexCapacity);
            colors = new List<Color>(vertexCapacity);
            surfaces = new List<Vector2>(vertexCapacity);
            triangles = new List<int>(capacity * (sphereTriangles.Length + sides * 12));
            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2f / sides;
                circle[side] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            GameObject appearance = new GameObject("Sauce Columns");
            appearance.layer = 2;
            appearance.transform.SetParent(root, false);
            mesh = new Mesh { name = "Sauce Columns" };
            mesh.MarkDynamic();
            appearance.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = appearance.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Releases only the owned dynamic mesh; the pool owns its child renderer and material.</summary>
        public void Dispose()
        {
            if (mesh != null)
                UnityEngine.Object.Destroy(mesh);
        }
        #endregion
        #region Geometry
        /// <summary>Follows interpolated physics once per rendered frame without per-sample renderers or buffer allocations.</summary>
        /// <param name="droplets">Bounded physical samples, including short-lived impact endpoints.</param>
        internal void Refresh(LiquidDroplet[] droplets)
        {
            vertices.Clear();
            normals.Clear();
            colors.Clear();
            surfaces.Clear();
            triangles.Clear();
            foreach (LiquidDroplet droplet in droplets)
            {
                if (droplet == null || !droplet.gameObject.activeSelf)
                    continue;
                LiquidDroplet previous = droplet.Previous;
                if (previous != null && !(droplet.Impacted && previous.Impacted))
                    Column(droplet.Position, previous.Position, droplet.Radius, previous.Radius,
                        ColorFor(droplet), ColorFor(previous), droplet.Gloss);
                else if (!droplet.Impacted)
                    Drop(droplet);
                if (droplet.JoinsOutlet)
                    Column(droplet.Source.Outlet, droplet.Position, droplet.Radius, droplet.Radius,
                        ColorFor(droplet), ColorFor(droplet), droplet.Gloss);
            }
            // An idle pool keeps no visible geometry and performs no mesh uploads.
            renderer.enabled = vertices.Count > 0;
            if (vertices.Count == 0)
                return;
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(0, surfaces);
            mesh.SetTriangles(triangles, 0, true);
        }

        /// <summary>Connects two physical points with a shaded round liquid section.</summary>
        /// <param name="start">Younger sample or nozzle.</param>
        /// <param name="end">Older sample or surface endpoint.</param>
        /// <param name="startRadius">Column width at the younger sample.</param>
        /// <param name="endRadius">Column width at the older sample.</param>
        /// <param name="startColor">Younger sample colour and lifetime opacity.</param>
        /// <param name="endColor">Older sample colour and lifetime opacity.</param>
        /// <param name="gloss">Wet-surface highlight strength.</param>
        private void Column(Vector3 start, Vector3 end, float startRadius, float endRadius,
            Color startColor, Color endColor, float gloss)
        {
            Vector3 direction = end - start;
            if (direction.sqrMagnitude < 0.00000001f)
                return;
            direction.Normalize();
            Vector3 right = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
            Vector3 up = Vector3.Cross(right, direction);
            int offset = vertices.Count;
            for (int ring = 0; ring < 2; ring++)
                for (int side = 0; side < sides; side++)
                {
                    Vector3 normal = right * circle[side].x + up * circle[side].y;
                    Vertex((ring == 0 ? start : end) + normal * (ring == 0 ? startRadius : endRadius),
                        normal, ring == 0 ? startColor : endColor, gloss);
                }
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                triangles.Add(offset + side);
                triangles.Add(offset + sides + side);
                triangles.Add(offset + next);
                triangles.Add(offset + next);
                triangles.Add(offset + sides + side);
                triangles.Add(offset + sides + next);
            }
        }

        /// <summary>Draws a compact free drop when age, impact or stretching breaks the column.</summary>
        /// <param name="droplet">Unconnected physical sample.</param>
        private void Drop(LiquidDroplet droplet)
        {
            int offset = vertices.Count;
            Color color = ColorFor(droplet);
            foreach (Vector3 point in sphereVertices)
                Vertex(droplet.Position + point * droplet.Radius, point, color, droplet.Gloss);
            foreach (int index in sphereTriangles)
                triangles.Add(offset + index);
        }

        /// <summary>Stores one vertex in the reusable geometry streams.</summary>
        /// <param name="position">World coordinate inside the identity-transform pool.</param>
        /// <param name="normal">Outward unit surface normal.</param>
        /// <param name="color">Colour including opacity.</param>
        /// <param name="gloss">Wet highlight response.</param>
        private void Vertex(Vector3 position, Vector3 normal, Color color, float gloss)
        {
            vertices.Add(position);
            normals.Add(normal);
            colors.Add(color);
            surfaces.Add(new Vector2(gloss, 0f));
        }

        /// <summary>Combines authored opacity with airborne lifetime fading.</summary>
        /// <param name="droplet">Sample whose colour is retained after its source disappears.</param>
        /// <returns>Final vertex colour.</returns>
        private static Color ColorFor(LiquidDroplet droplet)
        {
            Color color = droplet.Tint;
            color.a *= droplet.Opacity;
            return color;
        }
        #endregion
        #endregion
    }
}
