using System;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Attracts filtered bodies to an authored collider before handing each body to its final impulse.</summary>
    [Serializable]
    public sealed class BlackHoleSettings
    {
        #region Fields

        [Header("Destination")]
        [Tooltip("Hierarchy route to the destination collider. Empty selects this ejector's own object.")]
        public string ColliderPath = string.Empty;
        [Tooltip("Collider component on the selected branch, counted from zero.")]
        [Range(0, 15)]
        public int ColliderIndex;
        [Tooltip("Offset from the collider's center in that collider's local coordinates. Zero attracts to its center.")]
        public Vector3 Offset;
        [Tooltip("Distance in metres from the destination at which the object's center of mass receives the final impulse.")]
        [Range(0.01f, 2f)]
        public float ArrivalRadius = 0.15f;
        [Tooltip("Approach the duct mouth before entering the collector. A clear path for the complete body allows entry without stopping at the approach point.")]
        public bool GuideEntrance;
        [Tooltip("Approach point relative to the destination in the selected collider's local coordinates. Place it outside the duct mouth.")]
        public Vector3 EntranceOffset = new Vector3(0f, 0f, -2f);
        [Header("Attraction")]
        [Tooltip("Radius in metres around the destination admitting matching physical objects.")]
        [Range(0.5f, 100f)]
        public float Radius = 15f;
        [Tooltip("Scaled seconds during which the black hole admits and attracts objects.")]
        [Range(0.1f, 120f)]
        public float Duration = 8f;
        [Tooltip("Acceleration towards the destination, independent of object mass.")]
        [Range(1f, 500f)]
        public float Acceleration = 100f;
        [Tooltip("Maximum attraction speed in metres per second. Braking near the destination prevents overshoot.")]
        [Range(0.1f, 100f)]
        public float MaximumSpeed = 18f;
        [Tooltip("Seconds between overlap queries for new entrants. Existing participants update only in physics ticks.")]
        [Range(0.02f, 1f)]
        public float QueryInterval = 0.1f;
        [Header("Selection")]
        [Tooltip("Optional sphere or oriented box replacing the collector radius. Bodies enter once per activation; by default they continue towards the collector after leaving this area.")]
        public GravityArea Area = new GravityArea { RestoreOutside = false };
        [Tooltip("Population cap, gradual admission and shared route-query budgets.")]
        public BlackHolePerformanceSettings Performance = new BlackHolePerformanceSettings();
        [Tooltip("Combined layer, flag and interaction filters. The ejector and its own hierarchy always remain excluded.")]
        public GravityFilter Filter = new GravityFilter();
        [Tooltip("Release held objects and permit authored kinematic items to be attracted.")]
        public bool ReleaseKinematic = true;
        [Header("Collision Recovery")]
        [Tooltip("Temporarily pass through selected solid layers while attracting, preventing queues and obstacle jams. Original exclusions are restored on arrival or cancellation.")]
        public bool PassThrough;
        [Tooltip("Layers passed through only while attracting. The destination collider itself is always excluded until arrival.")]
        public LayerMask PassageLayers = ~0;
        [Tooltip("Optional sliding, guided rebounds and local detours around solid obstacles during attraction.")]
        public BlackHoleObstacleSettings Obstacles = new BlackHoleObstacleSettings();
        [Tooltip("Let objects captured by this activation pass each other while retaining collisions with walls and furniture. Original pairs return after separation.")]
        public bool IgnoreParticipantCollisions = true;
        [Header("Spiral")]
        [Tooltip("Add tangential motion around the destination while retaining inward acceleration.")]
        public bool Spiral;
        [Tooltip("Local axis of the destination collider around which objects spiral.")]
        public Vector3 SpiralAxis = Vector3.up;
        [Tooltip("Tangential speed in metres per second. It fades near arrival to guarantee convergence.")]
        [Range(0f, 50f)]
        public float SpiralSpeed = 6f;
        [Header("Arrival Impulse")]
        [Tooltip("Impulse applied once when the center of mass reaches the destination. The object cannot be attracted again during this activation.")]
        public Vector3 Impulse = new Vector3(0f, 5f, 0f);
        [Tooltip("Self rotates the impulse with the ejector; World leaves its direction unchanged.")]
        public Space ImpulseSpace = Space.Self;
        [Tooltip("Discard incoming attraction velocity before applying the final impulse.")]
        public bool ReplaceVelocity = true;
        [Header("Camera")]
        [Tooltip("Optional start, sustained and end camera oscillation for players within range.")]
        public PlayerCameraRumbleSettings Rumble = new PlayerCameraRumbleSettings();
        [Header("Air Suction")]
        [Tooltip("Animate an existing child particle system towards the destination. Prepare the effect in the editor before Play.")]
        public bool Air;
        [Tooltip("Hierarchy route to the preauthored ParticleSystem used for inward air streaks.")]
        public string AirPath = string.Empty;
        [Tooltip("Speed of air particles towards the destination in metres per second.")]
        [Range(0.1f, 100f)]
        public float AirSpeed = 12f;

        #endregion
        #region Methods
        #region Geometry

        /// <summary>Resolves the authored collider only at activation or authoring boundaries.</summary>
        /// <param name="root">Ejector transform owning the saved route.</param>
        /// <returns>Selected collider, or null if its route or component index is missing.</returns>
        public Collider Resolve(Transform root)
        {
            Transform target = PlayerHierarchy.Resolve(root, ColliderPath);
            if (target == null)
                return null;
            Collider[] colliders = target.GetComponents<Collider>();
            return ColliderIndex >= 0 && ColliderIndex < colliders.Length ? colliders[ColliderIndex] : null;
        }

        /// <summary>Projects the local collider center and the optional local offset into world space.</summary>
        /// <param name="collider">Resolved destination geometry.</param>
        /// <returns>Current world-space destination, including collider rotation and scale.</returns>
        public Vector3 Destination(Collider collider)
        {
            Vector3 center = collider switch
            {
                BoxCollider box => box.center,
                SphereCollider sphere => sphere.center,
                CapsuleCollider capsule => capsule.center,
                MeshCollider mesh when mesh.sharedMesh != null => mesh.sharedMesh.bounds.center,
                _ => collider.transform.InverseTransformPoint(collider.bounds.center)
            };
            return collider.transform.TransformPoint(center + Offset);
        }

        #endregion
        #region Validation

        /// <summary>Checks active attraction and feedback settings without changing the proposal.</summary>
        /// <param name="warning">Receives an unsupported setting or missing selection filter.</param>
        /// <returns>True when the reusable black hole configuration is valid.</returns>
        public bool TryValidate(out string warning)
        {
            warning = "Configure Black Hole area, work budgets, filters, obstacles and camera feedback.";
            if (Filter == null || Rumble == null || Obstacles == null || Area == null || Performance == null)
                return false;
            warning = "Black Hole needs finite positive range, duration, acceleration, speed, arrival radius and query interval.";
            if (!InteractionValues.Finite(Offset) || ColliderIndex < 0
                || !InteractionValues.Positive(Duration) || !InteractionValues.Positive(Acceleration)
                || !InteractionValues.Positive(MaximumSpeed) || !InteractionValues.Positive(ArrivalRadius)
                || !Area.Enabled && (!InteractionValues.Positive(Radius) || ArrivalRadius > Radius || !InteractionValues.Positive(QueryInterval))
                || !InteractionValues.Finite(Impulse) || Impulse.sqrMagnitude <= 0f
                || ImpulseSpace is not (Space.Self or Space.World))
                return false;
            warning = "Entrance guidance needs a finite nonzero offset outside the collector.";
            if (GuideEntrance && (!InteractionValues.Finite(EntranceOffset) || EntranceOffset.sqrMagnitude <= 0f))
                return false;
            warning = "Spiral needs a finite nonzero axis and nonnegative speed; air suction needs a positive speed.";
            if (Spiral && (!InteractionValues.Finite(SpiralAxis) || SpiralAxis.sqrMagnitude <= 0f || !SpawnFlowPlan.Duration(SpiralSpeed))
                || Air && !InteractionValues.Positive(AirSpeed))
                return false;
            return Filter.TryValidate(out warning) && Rumble.TryValidate(out warning) && Obstacles.TryValidate(out warning)
                && Area.TryValidate(out warning) && Performance.TryValidate(out warning);
        }

        #endregion
        #endregion
    }
}
