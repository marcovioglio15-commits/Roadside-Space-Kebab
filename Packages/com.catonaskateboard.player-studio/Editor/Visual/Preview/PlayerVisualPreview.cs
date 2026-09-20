using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Draws cached static meshes as an amber proposal without creating scene objects or running model scripts.</summary>
    internal sealed class PlayerVisualPreview : IDisposable
    {
        #region State

        private readonly List<MeshEntry> meshes = new List<MeshEntry>();
        private GameObject source;
        private Material material;
        private bool invalidated = true;
        private string warning = string.Empty;

        #endregion

        #region Properties

        /// <summary>The latest source compatibility warning, computed when the source changes.</summary>
        public string Warning => warning;

        #endregion

        #region Types

        /// <summary>Stores one renderer's mesh and relative pose without retaining a temporary renderer.</summary>
        private readonly struct MeshEntry
        {
            public readonly Mesh Mesh;
            public readonly Matrix4x4 Matrix;

            #region Methods

            /// <summary>Captures the geometry once while the source cache is rebuilt.</summary>
            /// <param name="mesh">Original shared mesh; never duplicated or changed.</param>
            /// <param name="matrix">Pose relative to the source root.</param>
            public MeshEntry(Mesh mesh, Matrix4x4 matrix)
            {
                // The cached entry contains no scene instance or material clone.
                Mesh = mesh;
                Matrix = matrix;
            }

            #endregion
        }

        #endregion

        #region Methods

        #region Cache

        /// <summary>Requests a rebuild after hierarchy or project changes without allocating during that event.</summary>
        public void Invalidate()
        {
            // Several editor notifications collapse into one rebuild on the next preview draw.
            invalidated = true;
        }

        /// <summary>Rebuilds cached supported geometry only when its source or hierarchy changed.</summary>
        /// <param name="model">Static source to represent, or null to clear the preview.</param>
        private void Refresh(GameObject model)
        {
            // Numeric offset edits reuse the same mesh list.
            if (!invalidated && source == model)
                return;

            invalidated = false;
            source = model;
            meshes.Clear();
            warning = string.Empty;
            if (model == null || !PlayerVisualModelValidation.TryValidate(model, out warning))
                return;

            foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                // Disabled renderers or locally inactive ancestors are not part of the proposal.
                if (!renderer.enabled || !IsActive(renderer.transform, model.transform))
                    continue;
                meshes.Add(new MeshEntry(renderer.GetComponent<MeshFilter>().sharedMesh,
                    model.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix));
            }

            // One Editor-only material serves all cached submeshes and is released on window disable.
            if (material == null)
            {
                Shader shader = Shader.Find("Hidden/Player Studio/Visual Draft");
                if (shader == null || !shader.isSupported)
                {
                    warning = "The Visual draft shader is unavailable. Reimport the Player Studio package.";
                    meshes.Clear();
                    return;
                }
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
        }

        /// <summary>Checks activation relative to the model rather than a prefab asset's scene state.</summary>
        /// <param name="current">Renderer transform to inspect.</param>
        /// <param name="root">Model root that ends the walk.</param>
        /// <returns>True when the renderer's entire local branch is active.</returns>
        private static bool IsActive(Transform current, Transform root)
        {
            // Prefab assets do not need to be active in a loaded scene to be previewed.
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                    return false;
                if (current == root)
                    return true;
                current = current.parent;
            }
            return false;
        }

        #endregion

        #region Drawing

        /// <summary>Draws the proposed model in the owning SceneView repaint after the real scene.</summary>
        /// <param name="scene">Selected player's pending binding choices.</param>
        /// <param name="draft">Raw proposed source and offset.</param>
        /// <param name="rootMatrix">Player root pose, including its separate Transform draft.</param>
        public void Draw(PlayerVisualSceneSession scene, PlayerVisualDraft draft, Matrix4x4 rootMatrix)
        {
            // Invalid values remain visible in controls but never reach a render matrix.
            if (!scene.Managed || (draft.Prefab == null && scene.Binding != null && scene.Binding.SourcePrefab != null)
                || !draft.TryGetSettings(out PlayerVisualSettings settings, out _))
                return;

            GameObject model = ResolveModel(scene, draft);
            Refresh(model);
            if (model == null || material == null || meshes.Count == 0)
                return;

            // Source root transforms are preserved; adoption contributes its captured original pose.
            Matrix4x4 authored = PlayerVisualPose.Authored(scene, draft);
            Matrix4x4 matrix = rootMatrix * Matrix4x4.TRS(settings.Position, settings.Rotation, Vector3.one * settings.Scale) * authored;

            if (!PlayerVisualModelValidation.TryValidateMatrix(matrix, out warning))
                return;

            // This callback runs only for the owning viewport; immediate unlit draws create no scene renderers.
            if (!material.SetPass(0))
                return;
            foreach (MeshEntry entry in meshes)
                for (int submesh = 0; submesh < entry.Mesh.subMeshCount; submesh++)
                    Graphics.DrawMeshNow(entry.Mesh, matrix * entry.Matrix, submesh);
        }

        /// <summary>Resolves the same model for drawing and picking, including an unapplied replacement.</summary>
        /// <param name="scene">Current binding proposal.</param>
        /// <param name="draft">Source chosen in the Visual preset draft.</param>
        /// <returns>The model supplying cached meshes, or null when the proposal removes it.</returns>
        private static GameObject ResolveModel(PlayerVisualSceneSession scene, PlayerVisualDraft draft)
        {
            // Clearing a previously assigned prefab proposes removal, not adoption of that old model.
            if (!scene.Managed || (draft.Prefab == null && scene.Binding != null && scene.Binding.SourcePrefab != null))
                return null;
            return scene.Binding != null && scene.Binding.Model != null && draft.Prefab == scene.Binding.SourcePrefab
                ? scene.Binding.Model : draft.Prefab != null ? draft.Prefab : scene.Existing;
        }

        /// <summary>Tests cached mesh bounds at the proposed pose when no real renderer represents that pose yet.</summary>
        /// <param name="scene">Current binding proposal.</param>
        /// <param name="draft">Visual values shown in amber.</param>
        /// <param name="rootMatrix">Player pose shown by this preview.</param>
        /// <param name="ray">Mouse ray in world space.</param>
        /// <returns>True when the ray intersects the proposed model bounds.</returns>
        public bool Pick(PlayerVisualSceneSession scene, PlayerVisualDraft draft, Matrix4x4 rootMatrix, Ray ray)
        {
            // Bounds picking is Editor-only and uses the render cache; no temporary collider is required.
            if (!draft.TryGetSettings(out PlayerVisualSettings settings, out _))
                return false;
            Refresh(ResolveModel(scene, draft));
            Matrix4x4 matrix = rootMatrix * Matrix4x4.TRS(settings.Position, settings.Rotation, Vector3.one * settings.Scale)
                * PlayerVisualPose.Authored(scene, draft);
            if (!PlayerVisualModelValidation.TryValidateMatrix(matrix, out _))
                return false;
            foreach (MeshEntry entry in meshes)
            {
                Matrix4x4 inverse = (matrix * entry.Matrix).inverse;
                if (entry.Mesh.bounds.IntersectRay(new Ray(inverse.MultiplyPoint3x4(ray.origin), inverse.MultiplyVector(ray.direction))))
                    return true;
            }
            return false;
        }

        /// <summary>Includes the proposed model in explicit framing, even when its offset is far from the player.</summary>
        /// <param name="scene">Selected visual binding proposal.</param>
        /// <param name="draft">Source and offset currently shown by the preview.</param>
        /// <param name="rootMatrix">Player's proposed world pose.</param>
        /// <param name="bounds">Receives bounds containing every supported proposed mesh.</param>
        /// <returns>True when valid model geometry contributes to the frame.</returns>
        public bool TryGetBounds(PlayerVisualSceneSession scene, PlayerVisualDraft draft, Matrix4x4 rootMatrix, out Bounds bounds)
        {
            // Framing reads the proposal and never resets a large offset entered by the user.
            bounds = default;
            if (!draft.TryGetSettings(out PlayerVisualSettings settings, out _))
                return false;
            Refresh(ResolveModel(scene, draft));
            Matrix4x4 matrix = rootMatrix * Matrix4x4.TRS(settings.Position, settings.Rotation, Vector3.one * settings.Scale)
                * PlayerVisualPose.Authored(scene, draft);
            if (!PlayerVisualModelValidation.TryValidateMatrix(matrix, out _))
                return false;
            bool initialized = false;
            foreach (MeshEntry entry in meshes)
            {
                Matrix4x4 world = matrix * entry.Matrix;
                Bounds meshBounds = entry.Mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = world.MultiplyPoint3x4(meshBounds.center + Vector3.Scale(meshBounds.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f)));
                    if (initialized)
                        bounds.Encapsulate(point);
                    else
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        initialized = true;
                    }
                }
            }
            return initialized;
        }

        /// <summary>Releases the sole temporary material when the window closes, reloads or enters Play.</summary>
        public void Dispose()
        {
            // Shared meshes and source objects are never owned by this cache.
            if (material != null)
                UnityEngine.Object.DestroyImmediate(material);
            material = null;
            source = null;
            meshes.Clear();
            invalidated = true;
        }

        #endregion

        #endregion
    }
}
