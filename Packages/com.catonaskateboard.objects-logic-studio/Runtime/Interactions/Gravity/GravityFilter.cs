using System;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Identifies supported object features without runtime type discovery.</summary>
    [Flags]
    public enum GravityInteractionFilter
    {
        None = 0, Hover = 1, Grab = 2, Drop = 4, Throw = 8, Container = 16, Dispenser = 32,
        TriggerAnimation = 64, Eject = 128, ModifyByContact = 256, Dialogue = 512, Outline = 1024,
        Availability = 2048, AssemblyStation = 4096, AssemblyProduct = 8192, SpawnManagement = 16384,
        Slice = 32768, PlayAmbient = 65536, AvailableOrders = 131072, ObjectDegradation = 262144, GravityGenerator = 524288,
        ElasticDeformation = 1048576, DirtTrail = 2097152, SpraySauce = 4194304, All = 8388607
    }

    /// <summary>Combines independently enabled layer, identity and interaction filters for a gravity pulse.</summary>
    [Serializable]
    public sealed class GravityFilter
    {
        #region Fields

        [Header("Selection")]
        [Tooltip("Allow this generator's own body and child bodies to participate.")]
        public bool IncludeSelf;
        [Tooltip("Require all enabled filter groups; disable to accept a match in any enabled group.")]
        public bool RequireAllGroups = true;
        [Tooltip("Filter using each Rigidbody object's layer.")]
        public bool UseLayers = true;
        [Tooltip("Layers accepted by the layer filter.")]
        public LayerMask Layers = ~0;
        [Tooltip("Filter using the owning Object Item's identity flags, or the body's nearest identity when no item exists.")]
        public bool UseFlags;
        [Tooltip("Identity flags accepted by the flag filter.")]
        public ObjectFlag[] Flags = Array.Empty<ObjectFlag>();
        [Tooltip("Require any selected flag or all selected flags.")]
        public ObjectFlagMatch FlagMatch;
        [Tooltip("Filter by interactions configured on the body's owning object.")]
        public bool UseInteractions;
        [Tooltip("Object interactions accepted by this filter.")]
        public GravityInteractionFilter Interactions = GravityInteractionFilter.Grab;
        [Tooltip("Require all selected interaction types; disable to accept any one.")]
        public bool RequireAllInteractions;

        #endregion
        #region Methods
        #region Selection

        /// <summary>Evaluates configured groups when a pulse starts or an object joins an active suspension.</summary>
        /// <param name="body">Active dynamic body being considered.</param>
        /// <param name="owner">Generator defining the optional self exclusion.</param>
        /// <returns>True when this body satisfies the selected combination.</returns>
        internal bool Matches(Component body, Transform owner)
        {
            if (body.TryGetComponent(out LiquidDroplet droplet) && droplet.Source != null)
                body = droplet.Source;
            if (!IncludeSelf && body.transform.IsChildOf(owner))
                return false;
            ObjectItem item = body.GetComponentInParent<ObjectItem>();
            Transform root = item != null ? item.transform : body.transform;
            int enabled = 0;
            int matched = 0;
            if (UseLayers)
            {
                enabled++;
                if ((Layers.value & (1 << body.gameObject.layer)) != 0)
                    matched++;
            }
            if (UseFlags)
            {
                enabled++;
                ObjectIdentity identity = item != null ? item.Identity : body.GetComponentInParent<ObjectIdentity>();
                if (identity != null && identity.Matches(Flags, FlagMatch))
                    matched++;
            }
            if (UseInteractions)
            {
                enabled++;
                GravityInteractionFilter found = GravityInteractionFilter.None;
                foreach (ObjectInteraction interaction in root.GetComponents<ObjectInteraction>())
                    found |= Kind(interaction);
                if (RequireAllInteractions ? (found & Interactions) == (Interactions & GravityInteractionFilter.All) : (found & Interactions) != 0)
                    matched++;
            }
            return enabled > 0 && (RequireAllGroups ? matched == enabled : matched > 0);
        }

        /// <summary>Maps known component types explicitly for pulse filtering.</summary>
        /// <param name="interaction">Authored feature, including disabled features.</param>
        /// <returns>The corresponding feature bit.</returns>
        private static GravityInteractionFilter Kind(ObjectInteraction interaction)
        {
            return interaction switch
            {
                ObjectHover => GravityInteractionFilter.Hover,
                ObjectGrab => GravityInteractionFilter.Grab,
                ObjectThrow => GravityInteractionFilter.Throw,
                ObjectDrop => GravityInteractionFilter.Drop,
                ObjectContainer => GravityInteractionFilter.Container,
                ObjectDispenser => GravityInteractionFilter.Dispenser,
                ObjectTriggerAnimation => GravityInteractionFilter.TriggerAnimation,
                ObjectEject => GravityInteractionFilter.Eject,
                ObjectContactModifier => GravityInteractionFilter.ModifyByContact,
                ObjectDialogue => GravityInteractionFilter.Dialogue,
                ObjectOutline => GravityInteractionFilter.Outline,
                ObjectInteractionUnlock => GravityInteractionFilter.Availability,
                ObjectAssemblyStation => GravityInteractionFilter.AssemblyStation,
                ObjectAssemblyProduct => GravityInteractionFilter.AssemblyProduct,
                ObjectSpawnManager => GravityInteractionFilter.SpawnManagement,
                ObjectSlice => GravityInteractionFilter.Slice,
                ObjectAmbient => GravityInteractionFilter.PlayAmbient,
                ObjectAvailableOrders => GravityInteractionFilter.AvailableOrders,
                ObjectDegradation => GravityInteractionFilter.ObjectDegradation,
                ObjectGravityGenerator => GravityInteractionFilter.GravityGenerator,
                ObjectElasticDeformation => GravityInteractionFilter.ElasticDeformation,
                ObjectDirtTrail => GravityInteractionFilter.DirtTrail,
                ObjectSpraySauce => GravityInteractionFilter.SpraySauce,
                _ => GravityInteractionFilter.None
            };
        }

        /// <summary>Checks only enabled selection groups.</summary>
        /// <param name="warning">Receives missing or unsupported filter choices.</param>
        /// <returns>True when the pulse has an explicit usable selection policy.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Enable at least one gravity filter and select its layers, flags or interaction types.";
            if (!UseLayers && !UseFlags && !UseInteractions || UseLayers && Layers.value == 0
                || UseInteractions && (Interactions == GravityInteractionFilter.None
                    || (int)Interactions != -1 && (Interactions & ~GravityInteractionFilter.All) != 0))
                return false;
            if (UseFlags && !ObjectFlagRules.TryValidate(Flags, false, out warning))
                return false;
            warning = "Choose Any or All for the gravity identity filter.";
            if (UseFlags && FlagMatch is not (ObjectFlagMatch.Any or ObjectFlagMatch.All))
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
