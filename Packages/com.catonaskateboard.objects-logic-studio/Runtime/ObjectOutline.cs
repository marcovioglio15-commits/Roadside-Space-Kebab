using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Enables edge glow on original renderers through a shared depth-tested URP pass.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(300)]
    [AddComponentMenu("Objects Logic Studio/Outline")]
    public sealed class ObjectOutline : ObjectExtendedInteraction
    {
        #region Serialized Fields

        [Header("Edge Glow")]
        [Tooltip("Visible-edge color, pixel width, intensity and crease angle.")]
        [SerializeField]
        private OutlineSettings settings = new OutlineSettings();
        [Tooltip("Original renderers collected by prefab authoring. No duplicate shell geometry is required.")]
        [SerializeField]
        private Renderer[] renderers = Array.Empty<Renderer>();

        #endregion

        #region State

        private OutlineRendererState[] bindings = Array.Empty<OutlineRendererState>();
        private readonly Dictionary<ObjectAssemblyPart, OutlineRendererState[]> assembly = new Dictionary<ObjectAssemblyPart, OutlineRendererState[]>();
        private bool ready;
        private bool visible;

        #endregion

        #region Properties

        /// <summary>Saved glow parameters shown in the passive card.</summary>
        public OutlineSettings Settings => settings;
        /// <summary>Original renderers referenced by this object's authored glow.</summary>
        public Renderer[] Renderers => renderers;
        /// <summary>Identifies this passive feature.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.Outline;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Prepares source registration without creating meshes, renderers or materials.</summary>
        private void OnEnable()
        {
            // Explicit refresh also handles settings changed while the component was disabled.
            Refresh();
        }

        /// <summary>Restores registrations once for retained objects when entering Play.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            // Ordinary frames use cached bindings only.
            foreach (ObjectOutline outline in FindObjectsByType<ObjectOutline>())
                if (outline.isActiveAndEnabled)
                    outline.Refresh();
        }

        /// <summary>Releases only this interaction's rendering membership.</summary>
        private void OnDisable()
        {
            // Original surface rendering remains enabled.
            SetVisible(false);
        }

        /// <summary>Changes rendering membership only when locks or restrictions change availability.</summary>
        private void LateUpdate()
        {
            // URP performs camera culling, LOD selection, skinning and depth testing on the original renderers.
            bool show = ready && settings.Thickness > 0f && settings.Intensity > 0f && settings.Color.a > 0f
                && Available(InteractionChannels.Passive);
            if (visible != show)
                SetVisible(show);
        }

        #endregion

        #region Configuration

        /// <summary>Applies explicit settings changes to cached source renderers.</summary>
        public void Refresh()
        {
            // This boundary allocates binding state once, never while the effect animates.
            SetVisible(false);
            ready = TryValidate(out string warning);
            if (!ready)
            {
                Debug.LogWarning(warning, this);
                return;
            }
            bindings = Bind(renderers);
            foreach (OutlineRendererState[] group in assembly.Values)
                foreach (OutlineRendererState binding in group)
                    binding.Apply(settings);
        }

        /// <summary>Prepares one group of original source renderers.</summary>
        /// <param name="sources">Original static or skinned geometry.</param>
        /// <returns>Cached membership and property state for those renderers.</returns>
        private OutlineRendererState[] Bind(Renderer[] sources)
        {
            // No source mesh access is required, including meshes with Read/Write disabled.
            List<OutlineRendererState> result = new List<OutlineRendererState>(sources.Length);
            foreach (Renderer source in sources)
                if (source is MeshRenderer or SkinnedMeshRenderer && source.GetComponentInParent<InteractionVfxInstance>(true) == null)
                {
                    OutlineRendererState binding = new OutlineRendererState(source);
                    binding.Apply(settings);
                    binding.SetVisible(visible);
                    result.Add(binding);
                }
            return result.ToArray();
        }

        /// <summary>Includes an actual ingredient's visuals after its own interactions are suspended.</summary>
        /// <param name="part">Ingredient joining this product.</param>
        internal void AddPart(ObjectAssemblyPart part)
        {
            // Membership changes once per insertion; the existing renderer keeps its materials and LOD membership.
            if (!assembly.ContainsKey(part))
                assembly.Add(part, Bind(part.GetComponentsInChildren<Renderer>(true)));
            if (!ready)
                Refresh();
        }

        /// <summary>Releases product-owned glow before an ingredient restores standalone interactions.</summary>
        /// <param name="part">Ingredient leaving the product.</param>
        internal void RemovePart(ObjectAssemblyPart part)
        {
            // Detached items can restore their own independent outline settings afterward.
            if (assembly.Remove(part, out OutlineRendererState[] group))
                foreach (OutlineRendererState binding in group)
                    binding.SetVisible(false);
        }

        /// <summary>Validates settings and original source references without generating replacement geometry.</summary>
        /// <param name="warning">Receives a missing renderer or invalid glow setting.</param>
        /// <returns>True when this effect can register its source geometry.</returns>
        public override bool TryValidate(out string warning)
        {
            // Empty product prefabs receive their geometry from inserted ingredients.
            warning = "Collect the original outline renderers in Objects Logic Studio.";
            if (settings == null || !settings.TryValidate(out warning))
                return false;
            if (renderers == null || renderers.Length == 0 && assembly.Count == 0 && GetComponent<ObjectAssemblyProduct>() == null)
            {
                warning = "Collect the original outline renderers in Objects Logic Studio.";
                return false;
            }
            foreach (Renderer source in renderers)
                if (source == null || source is not (MeshRenderer or SkinnedMeshRenderer) || !source.transform.IsChildOf(transform))
                {
                    warning = "An outline source changed. Collect the current renderers in Objects Logic Studio.";
                    return false;
                }
            warning = string.Empty;
            return true;
        }

        /// <summary>Publishes successful activation and updates every owned source's rendering membership.</summary>
        /// <param name="value">Requested glow availability.</param>
        private void SetVisible(bool value)
        {
            // There is no completion on cancellation or loss of availability.
            visible = value;
            foreach (OutlineRendererState binding in bindings)
                binding.SetVisible(value);
            foreach (OutlineRendererState[] group in assembly.Values)
                foreach (OutlineRendererState binding in group)
                    binding.SetVisible(value);
            if (value)
            {
                Signal(InteractionMoment.Started);
                Signal(InteractionMoment.Completed);
            }
        }

        #endregion

        #endregion
    }
}
