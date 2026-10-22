using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Suspends one cut output while a temporary group carries its mirrored collision geometry.</summary>
    internal sealed class SliceCarryPart
    {
        #region State

        private readonly GameObject output;
        private readonly ObjectInteraction[] features;
        private readonly bool[] enabledFeatures;
        private readonly ObjectItem[] items;
        private readonly bool[] enabledItems;
        private readonly Collider[] colliders;
        private readonly bool[] enabledColliders;
        private readonly CarryBodyState original;
        private readonly GameObject geometry;
        private readonly Collider[] carryShapes;

        #endregion

        #region Properties

        /// <summary>Original body retained for exact release, never replaced or destroyed.</summary>
        internal Rigidbody Body { get; }

        #endregion

        #region Methods

        #region Ownership

        /// <summary>Captures authored activation and builds compound shapes through the existing assembly utility.</summary>
        /// <param name="created">New output already parented under the inactive temporary group.</param>
        internal SliceCarryPart(GameObject created)
        {
            output = created;
            Body = created.GetComponent<Rigidbody>();
            original = new CarryBodyState(Body);
            features = created.GetComponentsInChildren<ObjectInteraction>(true);
            items = created.GetComponentsInChildren<ObjectItem>(true);
            colliders = created.GetComponentsInChildren<Collider>(true);
            enabledFeatures = new bool[features.Length];
            enabledItems = new bool[items.Length];
            enabledColliders = new bool[colliders.Length];
            // Actual output scripts and shapes remain dormant while their group owns carry and collision.
            for (int index = 0; index < features.Length; index++)
            {
                enabledFeatures[index] = features[index].enabled;
                features[index].enabled = false;
            }
            for (int index = 0; index < items.Length; index++)
            {
                enabledItems[index] = items[index].enabled;
                items[index].enabled = false;
            }
            geometry = AssemblyGeometry.Create(created.transform, colliders, new Dictionary<Collider, Collider>());
            carryShapes = geometry.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                enabledColliders[index] = colliders[index].enabled;
                colliders[index].enabled = false;
            }
            Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            Body.isKinematic = true;
            Body.detectCollisions = false;
            Body.useGravity = false;
            Body.interpolation = RigidbodyInterpolation.None;
            geometry.SetActive(true);
        }

        /// <summary>Restores one independent piece at its current world pose with the group's deliberate release velocity.</summary>
        /// <param name="velocity">Carrier linear velocity captured before detaching any pieces.</param>
        /// <param name="spin">Carrier angular velocity captured in the same rigid-motion frame.</param>
        /// <param name="centre">Carrier world centre of mass before its collision shapes change.</param>
        /// <param name="profile">Drop or Throw body overrides, or null on carry cancellation.</param>
        internal void Release(Vector3 velocity, Vector3 spin, Vector3 centre, ReleaseSettings profile)
        {
            if (output == null)
                return;
            geometry.SetActive(false);
            output.transform.SetParent(null, true);
            original.Restore(Body, true);
            profile?.Apply(Body);
            // Restore items before their interaction components recache ownership and register their inputs.
            for (int index = 0; index < colliders.Length; index++)
                if (colliders[index] != null)
                    colliders[index].enabled = enabledColliders[index];
            for (int index = 0; index < items.Length; index++)
                if (items[index] != null)
                    items[index].enabled = enabledItems[index];
            for (int index = 0; index < features.Length; index++)
                if (features[index] != null)
                    features[index].enabled = enabledFeatures[index];
            ObjectGravityGenerator.IncludeSpawn(output);
            Body.linearVelocity = velocity + Vector3.Cross(spin, Body.worldCenterOfMass - centre);
            Body.angularVelocity = spin;
            Body.WakeUp();
        }

        /// <summary>Finds the deepest overlap between live mirrored shapes while original output colliders remain suspended.</summary>
        /// <param name="other">Previously placed output whose pose must remain stable.</param>
        /// <param name="correction">Receives the minimum world translation for the deepest intersecting shape pair.</param>
        /// <returns>True when this output needs separation from the previous piece.</returns>
        internal bool TrySeparation(SliceCarryPart other, out Vector3 correction)
        {
            correction = Vector3.zero;
            float deepest = 0f;
            // Fresh inactive prefab colliders have no native geometry yet; use the activated carrier's shapes.
            for (int index = 0; index < carryShapes.Length; index++)
            {
                Collider shape = carryShapes[index];
                if (shape == null || !shape.enabled || !shape.gameObject.activeInHierarchy || shape.isTrigger)
                    continue;
                for (int previous = 0; previous < other.carryShapes.Length; previous++)
                {
                    Collider obstacle = other.carryShapes[previous];
                    if (obstacle == null || !obstacle.enabled || !obstacle.gameObject.activeInHierarchy || obstacle.isTrigger
                        || !Physics.ComputePenetration(shape, shape.transform.position, shape.transform.rotation,
                            obstacle, obstacle.transform.position, obstacle.transform.rotation, out Vector3 direction, out float distance)
                        || distance <= deepest)
                        continue;
                    deepest = distance;
                    correction = direction * (distance + 0.001f);
                }
            }
            return deepest > 0f;
        }

        /// <summary>Moves an output and its mirrored carrier colliders together before ordinary carrying begins.</summary>
        /// <param name="correction">World translation computed from actual intersecting output geometry.</param>
        internal void Move(Vector3 correction)
        {
            output.transform.position += correction;
            geometry.transform.position += correction;
            // Keep dormant nested bodies aligned with their newly separated visual pose before release.
            Body.position = output.transform.position;
        }

        #endregion

        #endregion
    }
}
