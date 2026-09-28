using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Defines the rigidbody and contact response used after Drop or Throw.</summary>
    [Serializable]
    public sealed class ReleaseSettings
    {
        #region Fields

        [Header("Body")]
        [Tooltip("Override the body's mass, damping, gravity and motion constraints after release.")]
        public bool OverrideBody = true;
        [Tooltip("Released mass in kilograms. Impulse-based throws accelerate lighter objects more.")]
        public float Mass = 1f;
        [Tooltip("Linear velocity damping after release; higher values increase air resistance.")]
        public float LinearDamping = 0.05f;
        [Tooltip("Angular velocity damping after release; higher values stop spin sooner.")]
        public float AngularDamping = 0.05f;
        [Tooltip("Apply the physics scene's gravity after release.")]
        public bool Gravity = true;
        [Tooltip("Restrict selected world position and rotation axes after release.")]
        public RigidbodyConstraints Constraints;
        [Tooltip("Collision detection after release. Continuous Dynamic is suitable for fast throws.")]
        public CollisionDetectionMode CollisionDetection = CollisionDetectionMode.ContinuousDynamic;
        [Tooltip("Maximum angular speed after release, in radians per second.")]
        public float MaxAngularSpeed = 20f;

        [Header("Surface")]
        [Tooltip("Use this interaction's friction and bounce on every solid collider belonging to the object.")]
        public bool OverrideSurface = true;
        [Tooltip("Friction while the released object rests against another surface; valid range is zero to one.")]
        public float StaticFriction = 0.5f;
        [Tooltip("Friction while the released object slides; valid range is zero to one.")]
        public float DynamicFriction = 0.4f;
        [Tooltip("Restitution against floors and other solid objects; zero absorbs bounce, one preserves it.")]
        public float Bounciness = 0.2f;
        [Tooltip("How this surface's friction combines with the contacted material's friction.")]
        public PhysicsMaterialCombine FrictionCombine = PhysicsMaterialCombine.Average;
        [Tooltip("How bounce combines with the contacted material. Unity uses the higher-priority combine mode of the pair.")]
        public PhysicsMaterialCombine BounceCombine = PhysicsMaterialCombine.Maximum;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Validates only the active body and surface overrides.</summary>
        /// <param name="warning">Receives the first invalid physical quantity.</param>
        /// <returns>True when this profile can be applied without clamping.</returns>
        public bool TryValidate(out string warning)
        {
            // Authored invalid values remain available for correction in the tool.
            warning = string.Empty;
            if (OverrideBody && (!InteractionValues.Positive(Mass) || !InteractionValues.Positive(MaxAngularSpeed)
                || !InteractionValues.Finite(LinearDamping) || LinearDamping < 0f
                || !InteractionValues.Finite(AngularDamping) || AngularDamping < 0f
                || CollisionDetection is < CollisionDetectionMode.Discrete or > CollisionDetectionMode.ContinuousSpeculative
                || (Constraints & ~RigidbodyConstraints.FreezeAll) != 0))
                warning = "Release needs positive mass/angular speed, non-negative finite damping and valid body modes.";
            else if (OverrideSurface && (!Unit(StaticFriction) || !Unit(DynamicFriction) || !Unit(Bounciness)
                || FrictionCombine is < PhysicsMaterialCombine.Average or > PhysicsMaterialCombine.Maximum
                || BounceCombine is < PhysicsMaterialCombine.Average or > PhysicsMaterialCombine.Maximum))
                warning = "Surface friction and bounce must be finite values from zero to one, with valid combine modes.";
            return warning.Length == 0;
        }

        /// <summary>Checks a normalized surface coefficient.</summary>
        /// <param name="value">Authored friction or restitution.</param>
        /// <returns>True when the coefficient is finite and inside zero to one.</returns>
        private static bool Unit(float value)
        {
            // Validation reports unsupported coefficients instead of relying on engine clamping.
            return InteractionValues.Finite(value) && value >= 0f && value <= 1f;
        }

        #endregion

        #region Application

        /// <summary>Writes enabled body overrides after the carry state has been restored.</summary>
        /// <param name="body">Released dynamic rigidbody.</param>
        internal void Apply(Rigidbody body)
        {
            // A disabled override keeps the values captured immediately before pickup.
            if (!OverrideBody)
                return;
            body.mass = Mass;
            body.linearDamping = LinearDamping;
            body.angularDamping = AngularDamping;
            body.useGravity = Gravity;
            body.constraints = Constraints;
            body.collisionDetectionMode = CollisionDetection;
            body.maxAngularVelocity = MaxAngularSpeed;
        }

        /// <summary>Updates one cached runtime material without editing project assets.</summary>
        /// <param name="material">Material owned by the release interaction.</param>
        internal void Apply(PhysicsMaterial material)
        {
            // Materials are allocated once per interaction, never once per throw or physics step.
            material.staticFriction = StaticFriction;
            material.dynamicFriction = DynamicFriction;
            material.bounciness = Bounciness;
            material.frictionCombine = FrictionCombine;
            material.bounceCombine = BounceCombine;
        }

        #endregion

        #endregion
    }
}
