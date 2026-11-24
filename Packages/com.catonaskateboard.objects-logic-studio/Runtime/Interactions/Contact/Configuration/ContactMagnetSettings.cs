using System;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Selects how contact docking handles a fully occupied compatible layout.</summary>
    public enum ContactMagnetOverflow { Reject, ShareNearestCompletion }

    /// <summary>Defines local docking slots shared by this object's contact modifications.</summary>
    [Serializable]
    public sealed class ContactMagnetSettings
    {
        #region Fields

        [Header("Lock Item With Magnets")]
        [Tooltip("Dock compatible contact items in the nearest free magnet. Completion, interruption or pickup releases them.")]
        public bool Enabled;
        [Tooltip("Place items immediately instead of animating attraction.")]
        public bool Instant;
        [Tooltip("Seconds to reach the magnet using a smooth position and rotation transition.")]
        [Range(0.01f, 5f)]
        public float Duration = 0.35f;
        [Tooltip("Reject new items when every compatible slot is occupied, or modify them beside the slot closest to completion.")]
        public ContactMagnetOverflow WhenOccupied;
        [Tooltip("Local docking slots. Each holds one item accepted by a contact modification and its own flag filter.")]
        public ContactMagnet[] Magnets = Array.Empty<ContactMagnet>();
#if UNITY_EDITOR
        [Tooltip("Object shown once as the layout background. Empty uses this interaction's own object.")]
        public GameObject PreviewPrefab;
#endif

        #endregion
        #region Methods
        #region Validation

        /// <summary>Checks enabled slots without changing placements or timing.</summary>
        /// <param name="warning">Receives the first unusable slot or timing value.</param>
        /// <returns>True when docking is disabled or ready to use.</returns>
        public bool TryValidate(out string warning)
        {
            // Disabled docking leaves its inactive placements and timing untouched.
            warning = string.Empty;
            if (!Enabled)
                return true;
            if (WhenOccupied is not (ContactMagnetOverflow.Reject or ContactMagnetOverflow.ShareNearestCompletion))
            {
                warning = "Choose a supported occupied-magnet policy.";
                return false;
            }
            if (!Instant && !InteractionValues.Positive(Duration))
                warning = "Magnet attraction duration must be positive and finite.";
            else if (Magnets == null || Magnets.Length == 0)
                warning = "Add at least one contact magnet.";
            else
                for (int index = 0; index < Magnets.Length; index++)
                {
                    warning = "Remove the empty slot or add a contact magnet.";
                    if (Magnets[index] != null && Magnets[index].TryValidate(out warning))
                        continue;
                    warning = $"Contact magnet {index + 1}: {warning}";
                    return false;
                }
            return warning.Length == 0;
        }

        #endregion
        #endregion
    }

    /// <summary>Stores one owner-local docking pose and its optional identity restriction.</summary>
    [Serializable]
    public sealed class ContactMagnet
    {
        #region Fields

        [Header("Magnet")]
        [Tooltip("Name shown in the layout and scene guides.")]
        public string Name = "Magnet";
        [Tooltip("Accept any item already allowed by a contact modification.")]
        public bool AnyItem = true;
        [Tooltip("Alternative accepted item flags when Any Item is disabled.")]
        public ObjectFlag[] Flags = Array.Empty<ObjectFlag>();
        [Tooltip("Higher priority wins among free compatible magnets. Occupied magnets first prefer the earliest completion, then priority.")]
        [Range(-100, 100)]
        public int Priority;
        [Tooltip("Item pivot position relative to this modifying object.")]
        public Vector3 Position;
        [Tooltip("Item rotation relative to this modifying object, in degrees.")]
        public Vector3 Rotation;
#if UNITY_EDITOR
        [Tooltip("Optional layout guide. It is never instantiated during gameplay.")]
        public GameObject PreviewPrefab;
#endif

        #endregion
        #region Methods
        #region Validation

        /// <summary>Checks the pose and active flag filter before runtime or Apply.</summary>
        /// <param name="warning">Receives the invalid pose or missing flag selection.</param>
        /// <returns>True when this slot can accept an item.</returns>
        public bool TryValidate(out string warning)
        {
            // Only the active filter needs assigned flags.
            warning = "Position and rotation must be finite.";
            if (!InteractionValues.Finite(Position) || !InteractionValues.Finite(Rotation))
                return false;
            warning = "Select at least one existing flag or enable Any Item.";
            if (!AnyItem && (Flags == null || Flags.Length == 0 || Array.Exists(Flags, flag => flag == null)))
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
