using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Presents preauthored outline geometry while this passive interaction is available.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(300)]
    [AddComponentMenu("Objects Logic Studio/Outline")]
    public sealed class ObjectOutline : ObjectExtendedInteraction
    {
        #region Serialized Fields

        [Header("Outline")]
        [Tooltip("Color, width and depth behavior of the outline shells.")]
        [SerializeField]
        private OutlineSettings settings = new OutlineSettings();
        [Tooltip("Prefab-local shell bindings prepared by Objects Logic Studio before Play.")]
        [SerializeField]
        private OutlineBinding[] bindings = Array.Empty<OutlineBinding>();
        [Tooltip("Shared authored material for depth-tested outlines; no runtime material clone is required.")]
        [SerializeField]
        private Material occludedMaterial;
        [Tooltip("Shared authored material for outlines visible through other geometry.")]
        [SerializeField]
        private Material throughWallsMaterial;

        #endregion

        #region State

        private static readonly int colorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int thicknessId = Shader.PropertyToID("_OutlineThickness");
        private MaterialPropertyBlock properties;
        private bool ready;
        private bool visible;
        private readonly Dictionary<ObjectAssemblyPart, OutlineBinding[]> assembly = new Dictionary<ObjectAssemblyPart, OutlineBinding[]>();

        #endregion

        #region Properties

        /// <summary>Saved shader parameters exposed by the passive card.</summary>
        public OutlineSettings Settings => settings;
        /// <summary>Authored geometry used by editor rebuild and cleanup operations.</summary>
        public OutlineBinding[] Bindings => bindings;
        /// <summary>Identifies this passive feature.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.Outline;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Initializes shader properties once when the component becomes active.</summary>
        private void OnEnable()
        {
            // Materials, renderers and meshes are authored before Play.
            Refresh();
        }

        /// <summary>Refreshes retained shader state when entering Play without reloading scene objects.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            // Scene discovery occurs once per Play session, never per rendered frame.
            foreach (ObjectOutline outline in FindObjectsByType<ObjectOutline>())
                if (outline.isActiveAndEnabled)
                    outline.Refresh();
        }

        /// <summary>Hides only this interaction's dedicated shells.</summary>
        private void OnDisable()
        {
            // The surface renderer remains independent of the outline's lifetime.
            SetVisible(false);
        }

        /// <summary>Follows availability, changed meshes and animated blend shapes after object movement.</summary>
        private void LateUpdate()
        {
            // Disabled outlines avoid all geometry synchronization.
            bool show = ready && settings.Thickness > 0f && settings.Color.a > 0f && Available(InteractionChannels.Passive);
            if (visible != show)
                SetVisible(show);
            if (!visible)
                return;
            foreach (OutlineBinding binding in bindings)
                binding.Sync(true, settings.ThroughWalls ? throughWallsMaterial : occludedMaterial);
            foreach (OutlineBinding[] group in assembly.Values)
                foreach (OutlineBinding binding in group)
                    binding.Sync(true, settings.ThroughWalls ? throughWallsMaterial : occludedMaterial);
        }

        #endregion

        #region Configuration

        /// <summary>Applies explicitly changed shader values to cached shells without material instantiation.</summary>
        public void Refresh()
        {
            // A caller may request this once after changing settings at runtime.
            SetVisible(false);
            ready = TryValidate(out string warning);
            if (!ready)
            {
                Debug.LogWarning(warning, this);
                return;
            }
            properties ??= new MaterialPropertyBlock();
            properties.SetColor(colorId, settings.Color);
            properties.SetFloat(thicknessId, settings.Thickness / 250f);
            foreach (OutlineBinding binding in bindings)
                binding.Shell.SetPropertyBlock(properties);
            foreach (OutlineBinding[] group in assembly.Values)
                foreach (OutlineBinding binding in group)
                    if (binding.Shell != null)
                        binding.Shell.SetPropertyBlock(properties);
        }

        /// <summary>Adds outline geometry once for an ingredient joining this composite product.</summary>
        /// <param name="part">Newly attached ingredient whose original interactions are suspended.</param>
        internal void AddPart(ObjectAssemblyPart part)
        {
            // Existing ingredient shells stay untouched so detachment can restore their own settings.
            if (assembly.ContainsKey(part))
                return;
            HashSet<Renderer> excluded = new HashSet<Renderer>();
            foreach (ObjectOutline outline in part.GetComponentsInChildren<ObjectOutline>(true))
                foreach (OutlineBinding binding in outline.Bindings)
                    if (binding != null)
                        excluded.Add(binding.Shell);
            List<OutlineBinding> added = new List<OutlineBinding>();
            foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>(true))
                if (!excluded.Contains(renderer))
                {
                    OutlineBinding binding = OutlineGeometry.Create(renderer, settings.ThroughWalls ? throughWallsMaterial : occludedMaterial);
                    if (binding != null)
                        added.Add(binding);
                }
            assembly.Add(part, added.ToArray());
            OutlineGeometry.UpdateLods(part.gameObject, added, true);
            if (!ready)
                Refresh();
            else
                foreach (OutlineBinding binding in added)
                    binding.Shell.SetPropertyBlock(properties);
        }

        /// <summary>Removes only this product's shells before an ingredient returns to standalone behavior.</summary>
        /// <param name="part">Ingredient leaving the product.</param>
        internal void RemovePart(ObjectAssemblyPart part)
        {
            // Deferred destruction is safe because each shell is hidden immediately.
            if (!assembly.Remove(part, out OutlineBinding[] group))
                return;
            OutlineGeometry.UpdateLods(part.gameObject, group, false);
            foreach (OutlineBinding binding in group)
                if (binding.Shell != null)
                {
                    binding.Shell.enabled = false;
                    Destroy(binding.Shell.gameObject);
                }
        }

        /// <summary>Checks saved parameters and required prefab-local geometry.</summary>
        /// <param name="warning">Receives missing geometry or invalid shader settings.</param>
        /// <returns>True when preauthored shells can be rendered.</returns>
        public override bool TryValidate(out string warning)
        {
            // Rebuilding shell geometry is an explicit editor operation.
            warning = "Prepare outline geometry in Objects Logic Studio.";
            if (settings == null || !settings.TryValidate(out warning))
                return false;
            if (bindings == null || bindings.Length == 0 && assembly.Count == 0 && GetComponent<ObjectAssemblyProduct>() == null
                || occludedMaterial == null || throughWallsMaterial == null)
            {
                warning = "Prepare outline geometry in Objects Logic Studio.";
                return false;
            }
            foreach (OutlineBinding binding in bindings)
                if (binding == null || binding.Source == null || binding.Shell == null
                    || !binding.Source.transform.IsChildOf(transform) || !binding.Shell.transform.IsChildOf(binding.Source.transform))
                {
                    warning = "An outline renderer changed or was removed. Rebuild its geometry in Objects Logic Studio.";
                    return false;
                }
            warning = string.Empty;
            return true;
        }

        /// <summary>Changes shell availability at activation, restriction and disable boundaries.</summary>
        /// <param name="value">Whether the outline should be visible.</param>
        private void SetVisible(bool value)
        {
            // Null entries can exist temporarily during prefab editing.
            visible = value;
            if (value)
            {
                Signal(InteractionMoment.Started);
                Signal(InteractionMoment.Completed);
            }
            if (bindings == null)
                return;
            foreach (OutlineBinding binding in bindings)
                binding?.Sync(value, settings.ThroughWalls ? throughWallsMaterial : occludedMaterial);
            foreach (OutlineBinding[] group in assembly.Values)
                foreach (OutlineBinding binding in group)
                    binding.Sync(value, settings.ThroughWalls ? throughWallsMaterial : occludedMaterial);
        }

        #endregion

        #endregion
    }
}
