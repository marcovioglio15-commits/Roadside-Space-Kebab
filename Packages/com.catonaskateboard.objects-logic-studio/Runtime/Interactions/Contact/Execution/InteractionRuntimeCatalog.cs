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
        /// <summary>Applies matching settings groups in their selected order.</summary>
        /// <param name="feature">Destination runtime card.</param>
        /// <param name="groups">Prepared values from the normal settings-group presets.</param>
        /// <param name="warning">Receives an incompatible group or unresolved object slot.</param>
        /// <returns>True when all selected group patches were applied.</returns>
        internal static bool Import(ObjectInteraction feature, InteractionGroupPatch[] groups, out string warning)
        {
            warning = "Select matching settings-group presets.";
            if (groups == null || groups.Length == 0)
                return false;
            // A later group intentionally overrides fields shared with an earlier selection.
            foreach (InteractionGroupPatch group in groups)
                if (group == null || !group.Apply(feature, out warning))
                    return false;
            warning = string.Empty;
            return true;
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
