using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Captures one participant's visual state once, then animates existing renderers without material clones.</summary>
    internal sealed class ContactEffectRun
    {
        #region Bindings

        /// <summary>Owns one material-slot override and the snapshot needed for cancellation.</summary>
        private sealed class TintBinding
        {
            internal readonly Renderer Renderer;
            internal readonly int Slot;
            internal readonly Color Color;
            internal readonly MaterialPropertyBlock Original = new MaterialPropertyBlock();
            internal readonly MaterialPropertyBlock Working = new MaterialPropertyBlock();

            /// <summary>Captures an existing property override before the first effect frame.</summary>
            /// <param name="renderer">Authored renderer.</param>
            /// <param name="slot">Material slot with the selected shader property.</param>
            /// <param name="property">Cached shader property ID.</param>
            /// <param name="material">Shared material supplying the fallback color.</param>
            internal TintBinding(Renderer renderer, int slot, int property, Material material)
            {
                // Read existing overrides so repeated modifications multiply the visible result.
                Renderer = renderer;
                Slot = slot;
                renderer.GetPropertyBlock(Original, slot);
                if (Original.isEmpty)
                    renderer.GetPropertyBlock(Working);
                Color = Original.HasColor(property) ? Original.GetColor(property)
                    : Original.isEmpty && Working.HasColor(property) ? Working.GetColor(property) : material.GetColor(property);
            }
        }

        /// <summary>Stores existing mesh components so completion performs no hierarchy discovery.</summary>
        private readonly struct MeshBinding
        {
            internal readonly MeshFilter Filter;
            internal readonly SkinnedMeshRenderer Skin;
            internal readonly MeshCollider Collider;
            internal readonly MeshCollider Proxy;
            internal readonly Mesh Mesh;

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
            }
        }

        #endregion

        #region State

        private readonly List<TintBinding> tints = new List<TintBinding>();
        private readonly List<MeshBinding> meshes = new List<MeshBinding>();
        private readonly Color multiplier;
        private readonly int property;
        private readonly ObjectGrab grab;

        #endregion

        #region Methods

        #region Preparation

        /// <summary>Creates an empty prepared effect after configuration validation.</summary>
        /// <param name="item">Participant owning all affected geometry.</param>
        /// <param name="effects">Validated color and mesh settings.</param>
        private ContactEffectRun(ObjectItem item, ContactEffects effects)
        {
            // Shader names are converted once; shared materials are never instantiated or edited.
            property = Shader.PropertyToID(effects.ColorProperty ?? string.Empty);
            multiplier = effects.Multiplier;
            grab = item.GetComponent<ObjectGrab>();
        }

        /// <summary>Validates every affected branch before either participant is changed.</summary>
        /// <param name="item">Participant receiving these effects.</param>
        /// <param name="effects">Validated reusable effect settings.</param>
        /// <param name="run">Receives prepared bindings when successful.</param>
        /// <param name="warning">Receives missing renderer or mesh dependencies.</param>
        /// <returns>True when this participant is ready for a transaction.</returns>
        internal static bool TryPrepare(ObjectItem item, ContactEffects effects, out ContactEffectRun run, out string warning)
        {
            // Reject ambiguous paths instead of changing an unrelated, similarly named child.
            run = new ContactEffectRun(item, effects);
            warning = string.Empty;
            if (effects.Tint)
            {
                Transform branch = Resolve(item.transform, effects.RendererPath);
                if (branch != null)
                    foreach (Renderer renderer in branch.GetComponentsInChildren<Renderer>(true))
                        if (item.Owns(renderer.transform))
                        {
                            Material[] materials = renderer.sharedMaterials;
                            for (int slot = 0; slot < materials.Length; slot++)
                                if ((effects.MaterialSlot < 0 || effects.MaterialSlot == slot) && materials[slot] != null
                                    && materials[slot].HasColor(run.property))
                                    run.tints.Add(new TintBinding(renderer, slot, run.property, materials[slot]));
                        }
                if (run.tints.Count == 0)
                    warning = "No owned renderer/material slot exposes the selected tint color property.";
            }
            foreach (ContactMeshReplacement replacement in effects.Meshes)
            {
                Transform branch = Resolve(item.transform, replacement.Path);
                if (branch == null || !item.Owns(branch)
                    || branch.GetComponent<MeshFilter>() == null && branch.GetComponent<SkinnedMeshRenderer>() == null
                    || replacement.UpdateCollider && branch.GetComponent<MeshCollider>() == null)
                {
                    warning = "A mesh replacement path is missing, ambiguous, or lacks its requested mesh component.";
                    break;
                }
                run.meshes.Add(new MeshBinding(branch, replacement));
            }
            return warning.Length == 0;
        }

        /// <summary>Resolves a reusable named route only when each segment identifies exactly one child.</summary>
        /// <param name="root">Affected item root.</param>
        /// <param name="path">Slash-separated child names; empty selects the root.</param>
        /// <returns>The unique matching branch, or null.</returns>
        private static Transform Resolve(Transform root, string path)
        {
            // Parsing occurs only when the contact starts; no per-frame path allocations are needed.
            if (string.IsNullOrEmpty(path))
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

        #region Effects

        /// <summary>Updates only the selected color property on existing material slots.</summary>
        /// <param name="progress">Normalized effect progress.</param>
        internal void Animate(float progress)
        {
            // Read the current block to retain unrelated overrides owned by other systems.
            foreach (TintBinding tint in tints)
                if (tint.Renderer != null)
                {
                    tint.Renderer.GetPropertyBlock(tint.Working, tint.Slot);
                    if (tint.Working.isEmpty)
                        tint.Renderer.GetPropertyBlock(tint.Working);
                    tint.Working.SetColor(property, Color.Lerp(tint.Color, tint.Color * multiplier, progress));
                    tint.Renderer.SetPropertyBlock(tint.Working, tint.Slot);
                }
        }

        /// <summary>Commits discrete mesh replacements only after the timed transition finishes.</summary>
        internal void Commit()
        {
            // Reuse authored components; no renderer, collider or UI object is instantiated at runtime.
            foreach (MeshBinding mesh in meshes)
            {
                if (mesh.Filter != null)
                    mesh.Filter.sharedMesh = mesh.Mesh;
                if (mesh.Skin != null)
                    mesh.Skin.sharedMesh = mesh.Mesh;
                if (mesh.Collider != null)
                    mesh.Collider.sharedMesh = mesh.Mesh;
                if (mesh.Proxy != null)
                    mesh.Proxy.sharedMesh = mesh.Mesh;
            }
            if (meshes.Count > 0 && grab != null && grab.IsHeld)
                grab.RefreshCarryGeometry();
        }

        /// <summary>Restores the pre-transition appearance when continuous contact is interrupted.</summary>
        internal void Cancel()
        {
            // Mesh changes have not committed yet, so cancellation only restores property blocks.
            foreach (TintBinding tint in tints)
                if (tint.Renderer != null)
                    tint.Renderer.SetPropertyBlock(tint.Original.isEmpty ? null : tint.Original, tint.Slot);
        }

        #endregion

        #endregion
    }
}
