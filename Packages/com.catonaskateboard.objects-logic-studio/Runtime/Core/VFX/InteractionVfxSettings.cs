using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Configures an independent start effect for one interaction component.</summary>
    [Serializable]
    public sealed class InteractionVfxSettings
    {
        #region Fields

        [Header("Start VFX")]
        [Tooltip("Spawn this interaction's own visual effect after a successful start.")]
        public bool Enabled;
        [Tooltip("Separate visual-effect prefab. Use a cosmetic prefab without object interactions, UI canvases or physics bodies/colliders.")]
        public GameObject Prefab;
        [Tooltip("Use the interaction's defined duration. Paused contact modifications also pause this effect; cancellation ends it.")]
        public bool AutoTiming;
        [Tooltip("Effect lifetime in game-time seconds when automatic timing is disabled.")]
        public float Duration = 1f;
        [Tooltip("Move the effect with its owning interaction. Otherwise it remains at the initial world pose.")]
        public bool FollowObject = true;
        [Tooltip("Effect position relative to this interaction's transform.")]
        public Vector3 Position;
        [Tooltip("Effect Euler rotation relative to this interaction's transform.")]
        public Vector3 Rotation;
        [Tooltip("Positive scale multiplier applied to the visual-effect prefab.")]
        public Vector3 Scale = Vector3.one;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks the optional effect without modifying its prefab or timing settings.</summary>
        /// <param name="timed">Whether this interaction currently has a positive predefined duration.</param>
        /// <param name="warning">Receives a missing prefab or invalid timing or pose.</param>
        /// <returns>True when the effect is disabled or fully configured.</returns>
        public bool TryValidate(bool timed, out string warning)
        {
            // Interactions and UI cannot be cloned as cosmetic start effects.
            warning = string.Empty;
            if (!Enabled)
                return true;
            if (Prefab == null || Prefab.scene.IsValid() || Prefab.transform.parent != null || !Prefab.activeSelf
                || Prefab.GetComponentsInChildren<ObjectInteraction>(true).Length > 0 || Prefab.GetComponentsInChildren<Canvas>(true).Length > 0
                || Prefab.GetComponentsInChildren<Collider>(true).Length > 0 || Prefab.GetComponentsInChildren<Rigidbody>(true).Length > 0)
                warning = "Choose an active VFX prefab root without interactions, UI canvases or physics bodies/colliders.";
            else if (AutoTiming && !timed || !AutoTiming && !InteractionValues.Positive(Duration))
                warning = "Choose a positive manual VFX duration, or enable automatic timing on an interaction with a defined duration.";
            else if (!InteractionValues.Finite(Position) || !InteractionValues.Finite(Rotation) || !InteractionValues.Finite(Scale)
                || Scale.x <= 0f || Scale.y <= 0f || Scale.z <= 0f)
                warning = "VFX position and rotation must be finite; every scale multiplier must be positive.";
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
