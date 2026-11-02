using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Maps every supported card and preset explicitly for runtime contact changes.</summary>
    internal static class InteractionRuntimeCatalog
    {
        #region Methods

        #region Mapping
        /// <summary>Finds the compiled family of an existing card.</summary>
        /// <param name="feature">Existing interaction component.</param>
        /// <returns>Supported family, or null.</returns>
        internal static InteractionRuntimeKind? KindOf(ObjectInteraction feature)
        {
            return feature switch
            {
                ObjectHover => InteractionRuntimeKind.Hover,
                ObjectGrab => InteractionRuntimeKind.Grab,
                ObjectDrop => InteractionRuntimeKind.Drop,
                ObjectThrow => InteractionRuntimeKind.Throw,
                ObjectDispenser => InteractionRuntimeKind.Dispenser,
                ObjectContainer => InteractionRuntimeKind.Container,
                ObjectDialogue => InteractionRuntimeKind.Dialogue,
                ObjectSlice => InteractionRuntimeKind.Slice,
                ObjectContactModifier => InteractionRuntimeKind.Contact,
                ObjectOutline => InteractionRuntimeKind.Outline,
                ObjectSpawnManager => InteractionRuntimeKind.Spawn,
                ObjectAssemblyStation => InteractionRuntimeKind.AssemblyStation,
                ObjectAssemblyProduct => InteractionRuntimeKind.AssemblyProduct,
                ObjectAmbient => InteractionRuntimeKind.Ambient,
                ObjectAvailableOrders => InteractionRuntimeKind.AvailableOrders,
                ObjectTriggerAnimation => InteractionRuntimeKind.TriggerAnimation,
                ObjectEject => InteractionRuntimeKind.Eject,
                ObjectDegradation => InteractionRuntimeKind.ObjectDegradation,
                ObjectGravityGenerator => InteractionRuntimeKind.GravityGenerator,
                ObjectElasticDeformation => InteractionRuntimeKind.ElasticDeformation,
                ObjectDirtTrail => InteractionRuntimeKind.DirtTrail,
                ObjectSpraySauce => InteractionRuntimeKind.SpraySauce,
                ObjectDeathbox => InteractionRuntimeKind.Deathbox,
                ObjectPresenceCap => InteractionRuntimeKind.PresenceCap,
                ObjectInteractionUnlock => InteractionRuntimeKind.Unlock,
                _ => null
            };
        }

        /// <summary>Returns the exact component type used by editor selectors.</summary>
        /// <param name="kind">Supported family.</param>
        /// <returns>Compiled component type.</returns>
        internal static Type ComponentType(InteractionRuntimeKind kind)
        {
            return kind switch
            {
                InteractionRuntimeKind.Hover => typeof(ObjectHover),
                InteractionRuntimeKind.Grab => typeof(ObjectGrab),
                InteractionRuntimeKind.Drop => typeof(ObjectDrop),
                InteractionRuntimeKind.Throw => typeof(ObjectThrow),
                InteractionRuntimeKind.Dispenser => typeof(ObjectDispenser),
                InteractionRuntimeKind.Container => typeof(ObjectContainer),
                InteractionRuntimeKind.Dialogue => typeof(ObjectDialogue),
                InteractionRuntimeKind.Slice => typeof(ObjectSlice),
                InteractionRuntimeKind.Contact => typeof(ObjectContactModifier),
                InteractionRuntimeKind.Outline => typeof(ObjectOutline),
                InteractionRuntimeKind.Spawn => typeof(ObjectSpawnManager),
                InteractionRuntimeKind.AssemblyStation => typeof(ObjectAssemblyStation),
                InteractionRuntimeKind.AssemblyProduct => typeof(ObjectAssemblyProduct),
                InteractionRuntimeKind.Ambient => typeof(ObjectAmbient),
                InteractionRuntimeKind.AvailableOrders => typeof(ObjectAvailableOrders),
                InteractionRuntimeKind.TriggerAnimation => typeof(ObjectTriggerAnimation),
                InteractionRuntimeKind.Eject => typeof(ObjectEject),
                InteractionRuntimeKind.ObjectDegradation => typeof(ObjectDegradation),
                InteractionRuntimeKind.GravityGenerator => typeof(ObjectGravityGenerator),
                InteractionRuntimeKind.ElasticDeformation => typeof(ObjectElasticDeformation),
                InteractionRuntimeKind.DirtTrail => typeof(ObjectDirtTrail),
                InteractionRuntimeKind.SpraySauce => typeof(ObjectSpraySauce),
                InteractionRuntimeKind.Deathbox => typeof(ObjectDeathbox),
                InteractionRuntimeKind.PresenceCap => typeof(ObjectPresenceCap),
                InteractionRuntimeKind.Unlock => typeof(ObjectInteractionUnlock),
                _ => null
            };
        }

        /// <summary>Returns the preset type accepted by a family.</summary>
        /// <param name="kind">Supported family.</param>
        /// <returns>Dedicated preset type.</returns>
        internal static Type PresetType(InteractionRuntimeKind kind)
        {
            return kind switch
            {
                InteractionRuntimeKind.Hover => typeof(HoverPreset),
                InteractionRuntimeKind.Grab => typeof(GrabPreset),
                InteractionRuntimeKind.Drop => typeof(DropPreset),
                InteractionRuntimeKind.Throw => typeof(ThrowPreset),
                InteractionRuntimeKind.Dispenser => typeof(DispenserPreset),
                InteractionRuntimeKind.Container => typeof(ContainerPreset),
                InteractionRuntimeKind.Dialogue => typeof(DialoguePreset),
                InteractionRuntimeKind.Slice => typeof(SlicePreset),
                InteractionRuntimeKind.Contact => typeof(ContactModificationPreset),
                InteractionRuntimeKind.Outline => typeof(OutlinePreset),
                InteractionRuntimeKind.Spawn => typeof(SpawnManagementPreset),
                InteractionRuntimeKind.AssemblyStation => typeof(AssemblyStationPreset),
                InteractionRuntimeKind.AssemblyProduct => typeof(AssemblyProductPreset),
                InteractionRuntimeKind.Ambient => typeof(AmbientPreset),
                InteractionRuntimeKind.AvailableOrders => typeof(AvailableOrdersPreset),
                InteractionRuntimeKind.TriggerAnimation => typeof(TriggerAnimationPreset),
                InteractionRuntimeKind.Eject => typeof(EjectPreset),
                InteractionRuntimeKind.ObjectDegradation => typeof(DegradationPreset),
                InteractionRuntimeKind.GravityGenerator => typeof(GravityGeneratorPreset),
                InteractionRuntimeKind.ElasticDeformation => typeof(ElasticDeformationPreset),
                InteractionRuntimeKind.DirtTrail => typeof(DirtTrailPreset),
                InteractionRuntimeKind.SpraySauce => typeof(SpraySaucePreset),
                InteractionRuntimeKind.Deathbox => typeof(DeathboxPreset),
                InteractionRuntimeKind.PresenceCap => typeof(PresenceCapPreset),
                InteractionRuntimeKind.Unlock => typeof(UnlockRulePreset),
                _ => null
            };
        }

        /// <summary>Checks a preset through compiled type patterns.</summary>
        /// <param name="kind">Destination family.</param>
        /// <param name="preset">Proposed reusable preset.</param>
        /// <returns>True for a matching preset.</returns>
        internal static bool Accepts(InteractionRuntimeKind kind, ScriptableObject preset)
        {
            return kind switch
            {
                InteractionRuntimeKind.Hover => preset is HoverPreset,
                InteractionRuntimeKind.Grab => preset is GrabPreset,
                InteractionRuntimeKind.Drop => preset is DropPreset,
                InteractionRuntimeKind.Throw => preset is ThrowPreset,
                InteractionRuntimeKind.Dispenser => preset is DispenserPreset,
                InteractionRuntimeKind.Container => preset is ContainerPreset,
                InteractionRuntimeKind.Dialogue => preset is DialoguePreset,
                InteractionRuntimeKind.Slice => preset is SlicePreset,
                InteractionRuntimeKind.Contact => preset is ContactModificationPreset,
                InteractionRuntimeKind.Outline => preset is OutlinePreset,
                InteractionRuntimeKind.Spawn => preset is SpawnManagementPreset,
                InteractionRuntimeKind.AssemblyStation => preset is AssemblyStationPreset,
                InteractionRuntimeKind.AssemblyProduct => preset is AssemblyProductPreset,
                InteractionRuntimeKind.Ambient => preset is AmbientPreset,
                InteractionRuntimeKind.AvailableOrders => preset is AvailableOrdersPreset,
                InteractionRuntimeKind.TriggerAnimation => preset is TriggerAnimationPreset,
                InteractionRuntimeKind.Eject => preset is EjectPreset,
                InteractionRuntimeKind.ObjectDegradation => preset is DegradationPreset,
                InteractionRuntimeKind.GravityGenerator => preset is GravityGeneratorPreset,
                InteractionRuntimeKind.ElasticDeformation => preset is ElasticDeformationPreset,
                InteractionRuntimeKind.DirtTrail => preset is DirtTrailPreset,
                InteractionRuntimeKind.SpraySauce => preset is SpraySaucePreset,
                InteractionRuntimeKind.Deathbox => preset is DeathboxPreset,
                InteractionRuntimeKind.PresenceCap => preset is PresenceCapPreset,
                InteractionRuntimeKind.Unlock => preset is UnlockRulePreset,
                _ => false
            };
        }

        /// <summary>Creates one card while its initialization is staged.</summary>
        /// <param name="kind">Requested family.</param>
        /// <param name="root">Existing affected object.</param>
        /// <returns>New uninitialized interaction.</returns>
        internal static ObjectInteraction Create(InteractionRuntimeKind kind, GameObject root)
        {
            return kind switch
            {
                InteractionRuntimeKind.Hover => root.AddComponent<ObjectHover>(),
                InteractionRuntimeKind.Grab => root.AddComponent<ObjectGrab>(),
                InteractionRuntimeKind.Drop => root.AddComponent<ObjectDrop>(),
                InteractionRuntimeKind.Throw => root.AddComponent<ObjectThrow>(),
                InteractionRuntimeKind.Dispenser => root.AddComponent<ObjectDispenser>(),
                InteractionRuntimeKind.Container => root.AddComponent<ObjectContainer>(),
                InteractionRuntimeKind.Dialogue => root.AddComponent<ObjectDialogue>(),
                InteractionRuntimeKind.Slice => root.AddComponent<ObjectSlice>(),
                InteractionRuntimeKind.Contact => root.AddComponent<ObjectContactModifier>(),
                InteractionRuntimeKind.Outline => root.AddComponent<ObjectOutline>(),
                InteractionRuntimeKind.Spawn => root.AddComponent<ObjectSpawnManager>(),
                InteractionRuntimeKind.AssemblyStation => root.AddComponent<ObjectAssemblyStation>(),
                InteractionRuntimeKind.AssemblyProduct => root.AddComponent<ObjectAssemblyProduct>(),
                InteractionRuntimeKind.Ambient => root.AddComponent<ObjectAmbient>(),
                InteractionRuntimeKind.AvailableOrders => root.AddComponent<ObjectAvailableOrders>(),
                InteractionRuntimeKind.TriggerAnimation => root.AddComponent<ObjectTriggerAnimation>(),
                InteractionRuntimeKind.Eject => root.AddComponent<ObjectEject>(),
                InteractionRuntimeKind.ObjectDegradation => root.AddComponent<ObjectDegradation>(),
                InteractionRuntimeKind.GravityGenerator => root.AddComponent<ObjectGravityGenerator>(),
                InteractionRuntimeKind.ElasticDeformation => root.AddComponent<ObjectElasticDeformation>(),
                InteractionRuntimeKind.DirtTrail => root.AddComponent<ObjectDirtTrail>(),
                InteractionRuntimeKind.SpraySauce => root.AddComponent<ObjectSpraySauce>(),
                InteractionRuntimeKind.Deathbox => root.AddComponent<ObjectDeathbox>(),
                InteractionRuntimeKind.PresenceCap => root.AddComponent<ObjectPresenceCap>(),
                InteractionRuntimeKind.Unlock => root.AddComponent<ObjectInteractionUnlock>(),
                _ => null
            };
        }

        /// <summary>Returns instance-owned settings objects for native serialization snapshots.</summary>
        /// <param name="feature">Existing interaction.</param>
        /// <returns>Settings sections without any component state.</returns>
        internal static object[] Sections(ObjectInteraction feature)
        {
            return feature switch
            {
                ObjectHover value => new object[] { value.Configuration },
                ObjectGrab value => new object[] { value.Settings },
                ObjectDrop value => new object[] { value.PhysicsSettings },
                ObjectThrow value => new object[] { value.PhysicsSettings, value.Trajectory },
                ObjectDispenser value => new object[] { value.Settings },
                ObjectContainer value => new object[] { value.Settings },
                ObjectDialogue value => new object[] { value.Settings },
                ObjectSlice value => new object[] { value.Settings },
                ObjectContactModifier value => new object[] { value.Settings },
                ObjectOutline value => new object[] { value.Settings },
                ObjectSpawnManager value => new object[] { value.Settings },
                ObjectAssemblyStation value => new object[] { value.Settings },
                ObjectAssemblyProduct value => new object[] { value.Settings },
                ObjectAmbient value => new object[] { value.Settings },
                ObjectAvailableOrders value => new object[] { value.Settings },
                ObjectTriggerAnimation value => new object[] { value.Settings },
                ObjectEject value => new object[] { value.Settings },
                ObjectDegradation value => new object[] { value.Settings },
                ObjectGravityGenerator value => new object[] { value.Settings },
                ObjectElasticDeformation value => new object[] { value.Settings },
                ObjectDirtTrail value => new object[] { value.Settings },
                ObjectSpraySauce value => new object[] { value.Settings },
                ObjectDeathbox value => new object[] { value.Settings },
                ObjectPresenceCap value => new object[] { value.Settings },
                ObjectInteractionUnlock value => new object[] { value.Settings },
                _ => Array.Empty<object>()
            };
        }

        #endregion

        #region Configuration
        /// <summary>Imports native settings copies and resolves portable component mappings.</summary>
        /// <param name="feature">Destination runtime card.</param>
        /// <param name="preset">Matching immutable preset.</param>
        /// <param name="warning">Receives an unresolved mapping.</param>
        /// <returns>True when all settings were copied.</returns>
        internal static bool Import(ObjectInteraction feature, ScriptableObject preset, out string warning)
        {
            warning = "Choose the matching interaction preset.";
            if (!KindOf(feature).HasValue || !Accepts(KindOf(feature).Value, preset))
                return false;
            switch (feature)
            {
                case ObjectHover value:
                    InteractionRuntimeCopy.Into(((HoverPreset)preset).Configuration, value.Configuration);
                    break;
                case ObjectGrab value:
                    InteractionRuntimeCopy.Into(((GrabPreset)preset).Settings, value.Settings);
                    break;
                case ObjectDrop value:
                    InteractionRuntimeCopy.Into(((DropPreset)preset).Settings, value.PhysicsSettings);
                    break;
                case ObjectThrow value:
                    InteractionRuntimeCopy.Into(((ThrowPreset)preset).Settings, value.PhysicsSettings);
                    InteractionRuntimeCopy.Into(((ThrowPreset)preset).Trajectory, value.Trajectory);
                    break;
                case ObjectDispenser value:
                    InteractionRuntimeCopy.Into(((DispenserPreset)preset).Settings, value.Settings);
                    break;
                case ObjectContainer value:
                    InteractionRuntimeCopy.Into(((ContainerPreset)preset).Settings, value.Settings);
                    break;
                case ObjectDialogue value:
                    InteractionRuntimeCopy.Into(((DialoguePreset)preset).Settings, value.Settings);
                    break;
                case ObjectSlice value:
                    InteractionRuntimeCopy.Into(((SlicePreset)preset).Settings, value.Settings);
                    break;
                case ObjectContactModifier value:
                    InteractionRuntimeCopy.Into(((ContactModificationPreset)preset).Settings, value.Settings);
                    break;
                case ObjectOutline value:
                    InteractionRuntimeCopy.Into(((OutlinePreset)preset).Settings, value.Settings);
                    break;
                case ObjectSpawnManager value:
                    InteractionRuntimeCopy.Into(((SpawnManagementPreset)preset).Settings, value.Settings);
                    break;
                case ObjectAssemblyStation value:
                    InteractionRuntimeCopy.Into(((AssemblyStationPreset)preset).Settings, value.Settings);
                    break;
                case ObjectAssemblyProduct value:
                    InteractionRuntimeCopy.Into(((AssemblyProductPreset)preset).Settings, value.Settings);
                    break;
                case ObjectAmbient value:
                    InteractionRuntimeCopy.Into(((AmbientPreset)preset).Settings, value.Settings);
                    break;
                case ObjectAvailableOrders value:
                    InteractionRuntimeCopy.Into(((AvailableOrdersPreset)preset).Settings, value.Settings);
                    break;
                case ObjectTriggerAnimation value:
                    InteractionRuntimeCopy.Into(((TriggerAnimationPreset)preset).Settings, value.Settings);
                    break;
                case ObjectEject value:
                    InteractionRuntimeCopy.Into(((EjectPreset)preset).Settings, value.Settings);
                    break;
                case ObjectDegradation value:
                    InteractionRuntimeCopy.Into(((DegradationPreset)preset).Settings, value.Settings);
                    break;
                case ObjectGravityGenerator value:
                    InteractionRuntimeCopy.Into(((GravityGeneratorPreset)preset).Settings, value.Settings);
                    break;
                case ObjectElasticDeformation value:
                    InteractionRuntimeCopy.Into(((ElasticDeformationPreset)preset).Settings, value.Settings);
                    break;
                case ObjectDirtTrail value:
                    InteractionRuntimeCopy.Into(((DirtTrailPreset)preset).Settings, value.Settings);
                    break;
                case ObjectSpraySauce value:
                    InteractionRuntimeCopy.Into(((SpraySaucePreset)preset).Settings, value.Settings);
                    break;
                case ObjectDeathbox value:
                    InteractionRuntimeCopy.Into(((DeathboxPreset)preset).Settings, value.Settings);
                    break;
                case ObjectPresenceCap value:
                    InteractionRuntimeCopy.Into(((PresenceCapPreset)preset).Settings, value.Settings);
                    break;
                case ObjectInteractionUnlock value:
                    InteractionRuntimeCopy.Into(((UnlockRulePreset)preset).Rule.Settings, value.Settings);
                    break;
            }
            InteractionToolRequirement tools = preset switch
            {
                SingleInteractionPreset single => single.ToolRequirement,
                ExtendedInteractionPreset extended => extended.ToolRequirement,
                HoverPreset hover => hover.ToolRequirement,
                _ => null
            };
            if (tools != null)
                InteractionRuntimeCopy.Into(tools, feature.ToolRequirement);
            return InteractionRuntimeMappings.Apply(feature, preset, out warning);
        }

        /// <summary>Assigns saved action identifiers before activating a new card.</summary>
        /// <param name="feature">Prepared interaction.</param>
        /// <param name="action">Default activation Button.</param>
        /// <param name="advance">Optional dialogue advance Button.</param>
        internal static void BindActions(ObjectInteraction feature, InputActionReference action, InputActionReference advance)
        {
            switch (feature)
            {
                case ObjectSingleInteraction single:
                    single.BindAction(action);
                    break;
                case ObjectRequestedInteraction request:
                    request.BindAction(action);
                    break;
                case ObjectDialogue dialogue:
                    dialogue.BindActions(action, advance);
                    break;
            }
        }

        /// <summary>Validates destination bindings after a complete batch has been staged.</summary>
        /// <param name="feature">Proposed destination card.</param>
        /// <param name="warning">Receives invalid settings or dependencies.</param>
        /// <returns>True when the card can run.</returns>
        internal static bool Validate(ObjectInteraction feature, out string warning)
        {
            warning = "Interaction settings, tool requirement or flag change is invalid.";
            if (feature == null || feature.ToolRequirement == null || feature.FlagChange == null
                || !feature.ToolRequirement.TryValidate(out warning) || !feature.FlagChange.TryValidate(feature.gameObject, out warning))
                return false;
            return feature switch
            {
                ObjectSingleInteraction single => single.TryValidate(out warning),
                ObjectExtendedInteraction extended => extended.TryValidate(out warning),
                ObjectHover hover => hover.TryValidate(out warning),
                _ => false
            };
        }

        /// <summary>Refreshes prepared caches after committed runtime data changes.</summary>
        /// <param name="feature">Changed card.</param>
        internal static void Refresh(ObjectInteraction feature)
        {
            InteractionRuntimeRefresh.Apply(feature);
        }

        #endregion

        #endregion
    }
}
