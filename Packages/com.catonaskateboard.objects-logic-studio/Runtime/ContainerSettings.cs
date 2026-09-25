using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Defines accepted tags, storage capacity and the arrangement of retained instances.</summary>
    [Serializable]
    public sealed class ContainerSettings
    {
        #region Fields

        [Header("Targeting")]
        [Tooltip("Player range, aim and obstruction settings.")]
        public TransferTargetSettings Target = new TransferTargetSettings();
        [Header("Storage")]
        [Tooltip("Project tags accepted when the carried object is deposited.")]
        public string[] AllowedTags = Array.Empty<string>();
        [Tooltip("Store any number of accepted objects.")]
        public bool Unlimited = true;
        [Tooltip("Maximum number of stored instances when unlimited storage is disabled.")]
        public int Capacity = 10;
        [Tooltip("Keep stored objects visible. Their physics and interactions remain suspended until dispensed.")]
        public bool KeepVisible;
        [Tooltip("Local position of the first stored object.")]
        public Vector3 Position;
        [Tooltip("Local Euler rotation of visible stored objects.")]
        public Vector3 Rotation;
        [Tooltip("Local offset between successive occupied storage slots.")]
        public Vector3 Spacing = new Vector3(0f, 0.2f, 0f);

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks storage configuration while leaving invalid values available for correction.</summary>
        /// <param name="warning">Receives a missing tag or invalid capacity or pose.</param>
        /// <returns>True when the container has at least one distinct accepted tag.</returns>
        public bool TryValidate(out string warning)
        {
            // Project tag existence is checked on the destination component at authoring or activation boundaries.
            warning = "Container targeting is missing.";
            if (Target == null || !Target.TryValidate(out warning))
                return false;
            if (AllowedTags == null || AllowedTags.Length == 0)
                warning = "Add at least one allowed project tag.";
            else if (!Unlimited && Capacity <= 0)
                warning = "Container Capacity must be a positive whole number.";
            else if (!InteractionValues.Finite(Position) || !InteractionValues.Finite(Rotation) || !InteractionValues.Finite(Spacing))
                warning = "Container placement values must be finite.";
            else
                for (int index = 0; index < AllowedTags.Length; index++)
                    if (string.IsNullOrEmpty(AllowedTags[index]) || Array.IndexOf(AllowedTags, AllowedTags[index]) != index)
                    {
                        warning = "Choose distinct, nonempty allowed tags.";
                        break;
                    }
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
