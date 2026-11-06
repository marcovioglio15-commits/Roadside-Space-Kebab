using CatOnASkateboard.StudioIdentity;
using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Chooses original storage, local stock refill or direct dispenser scooping.</summary>
    public enum ContainerMode { StoreObjects, RefillDispenser, ScoopFromDispenser }
    /// <summary>Defines accepted flags, storage capacity and the arrangement of retained instances.</summary>
    [Serializable]
    public sealed class ContainerSettings
    {
        #region Fields

        [Header("Targeting")]
        [Tooltip("Player range, aim and obstruction settings.")]
        public TransferTargetSettings Target = new TransferTargetSettings();
        [Header("Storage")]
        [Tooltip("Store original objects, refill local finite stock, or scoop directly while this container is held.")]
        public ContainerMode Mode;
        [Tooltip("Project flags accepted when the carried object is deposited.")]
        public ObjectFlag[] AllowedFlags = Array.Empty<ObjectFlag>();
        [Tooltip("Require any selected flag or every selected flag on the deposited object.")]
        public ObjectFlagMatch Match;
        [Tooltip("Store any number of accepted objects.")]
        public bool Unlimited = true;
        [Tooltip("Maximum scoop portions or stored instances when unlimited storage is disabled.")]
        [Range(1, 1000)]
        public int Capacity = 10;
        [Tooltip("Accept only spaces available in this object's finite Dispenser. Prefab supply starts full; stored-only supply starts empty. Deposited originals become available to that Dispenser.")]
        public bool LimitToDispenserSpace;
        [Tooltip("Keep stored objects visible. Their physics and interactions remain suspended until dispensed.")]
        public bool KeepVisible;
        [Tooltip("Local position of the first stored object.")]
        public Vector3 Position;
        [Tooltip("Local Euler rotation of visible stored objects.")]
        public Vector3 Rotation;
        [Tooltip("Local offset between successive occupied storage slots.")]
        public Vector3 Spacing = new Vector3(0f, 0.2f, 0f);
        [Tooltip("Initial logical portions in Scoop From Dispenser mode.")]
        [Range(0, 1000)]
        public int InitialFill;
        [Tooltip("Optional movement and fill timing used only by Scoop From Dispenser.")]
        public ScoopAnimationSettings ScoopAnimation = new ScoopAnimationSettings();
        [Header("Fill Steps")]
        [Tooltip("Appearance selected by stored count, or remaining Dispenser supply in refill mode. Highest reached count wins; Dispenser steps take precedence on shared targets.")]
        public InventoryFillStep[] FillSteps = Array.Empty<InventoryFillStep>();

        /// <summary>Whether accepted objects replenish this root's finite prefab supply.</summary>
        public bool RefillDispenser => Mode == ContainerMode.RefillDispenser;
        /// <summary>Whether a held container receives logical portions directly from a dispenser.</summary>
        public bool ScoopFromDispenser => Mode == ContainerMode.ScoopFromDispenser;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks storage configuration while leaving invalid values available for correction.</summary>
        /// <param name="warning">Receives a missing flag or invalid capacity or pose.</param>
        /// <returns>True when the container has at least one distinct accepted flag.</returns>
        public bool TryValidate(out string warning)
        {
            // Project flag existence is checked on the destination component at authoring or activation boundaries.
            warning = "Container targeting is missing.";
            if (!ScoopFromDispenser && (Target == null || !Target.TryValidate(out warning)))
                return false;
            if (!ObjectFlagRules.TryValidate(AllowedFlags, false, out warning))
                return false;
            if (!InventoryFillStep.TryValidate(FillSteps, out warning))
                return false;
            if (Mode is not (ContainerMode.StoreObjects or ContainerMode.RefillDispenser or ContainerMode.ScoopFromDispenser))
                warning = "Choose a supported Container mode.";
            else if (Match is not (ObjectFlagMatch.Any or ObjectFlagMatch.All))
                warning = "Choose Any or All for the accepted flags.";
            else if (!RefillDispenser && (ScoopFromDispenser || !Unlimited) && Capacity <= 0)
                warning = "Container Capacity must be a positive whole number.";
            else if (ScoopFromDispenser && (InitialFill < 0 || InitialFill > Capacity))
                warning = "Initial Fill must fit the Scoop Capacity.";
            else if (ScoopFromDispenser && (ScoopAnimation == null || !ScoopAnimation.TryValidate(out warning)))
                return false;
            else if (Mode == ContainerMode.StoreObjects && (!InteractionValues.Finite(Position) || !InteractionValues.Finite(Rotation) || !InteractionValues.Finite(Spacing)))
                warning = "Container placement values must be finite.";
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
