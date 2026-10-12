using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Despawns contacts from solid surfaces and trigger volumes without requiring a dynamic deathbox body.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Deathbox")]
    public sealed class ObjectDeathbox : ObjectExtendedInteraction
    {
        #region Fields

        [Header("Deathbox")]
        [Tooltip("Contact layers, player return and query interval.")]
        [SerializeField]
        private DeathboxSettings settings = new DeathboxSettings();
        private Collider[] shapes;
        private Collider[] contacts = new Collider[64];
        private readonly HashSet<GameObject> visited = new HashSet<GameObject>();
        private float nextQuery;
        private bool ready;

        #endregion
        #region Properties

        /// <summary>Applied contact policy imported through the object tool.</summary>
        public DeathboxSettings Settings => settings;
        /// <summary>Passive preset identifier.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.Deathbox;

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Caches owned geometry once when the volume becomes active.</summary>
        private void OnEnable()
        {
            shapes = GetComponentsInChildren<Collider>(true);
            ready = TryValidate(out string warning);
            nextQuery = 0f;
            if (!ready)
                Debug.LogWarning(warning, this);
        }

        /// <summary>Queries at a bounded interval and deduplicates compound bodies before removal.</summary>
        private void FixedUpdate()
        {
            if (!ready || Time.time < nextQuery || !Available(InteractionChannels.Passive))
                return;
            nextQuery = Time.time + settings.QueryInterval;
            visited.Clear();
            Physics.SyncTransforms();
            foreach (Collider shape in shapes)
            {
                if (!ContactDetection.Usable(shape, true) || !Item.Owns(shape.transform))
                    continue;
                Bounds bounds = shape.bounds;
                float tolerance = shape.contactOffset + Physics.defaultContactOffset;
                bounds.Expand(tolerance * 2f);
                int count;
                // Grow only on saturation, retaining the larger buffer for subsequent checks.
                while ((count = gameObject.scene.GetPhysicsScene().OverlapBox(bounds.center, bounds.extents, contacts,
                    Quaternion.identity, settings.Layers, QueryTriggerInteraction.Collide)) == contacts.Length)
                    contacts = new Collider[contacts.Length * 2];
                for (int index = 0; index < count; index++)
                {
                    Collider other = contacts[index];
                    if (other == null || other.transform.IsChildOf(transform) || transform.IsChildOf(other.transform)
                        || !Touches(shape, other, tolerance))
                        continue;
                    Affect(other);
                }
            }
        }

        /// <summary>Handles solid impacts before the solver can separate a bouncing object from the query volume.</summary>
        /// <param name="collision">New physical contact on this root or its compound body.</param>
        private void OnCollisionEnter(Collision collision) => Affect(collision.collider);

        /// <summary>Includes objects already resting against the deathbox when it becomes available.</summary>
        /// <param name="collision">Retained physical contact.</param>
        private void OnCollisionStay(Collision collision) => Affect(collision.collider);

        /// <summary>Handles fast entries without waiting for the periodic overlap query.</summary>
        /// <param name="other">Collider entering an authored trigger.</param>
        private void OnTriggerEnter(Collider other) => Affect(other);

        /// <summary>Includes trigger occupants when an availability rule unlocks the deathbox.</summary>
        /// <param name="other">Collider remaining inside the trigger.</param>
        private void OnTriggerStay(Collider other) => Affect(other);

        /// <summary>Accepts real overlap or the contact clearance maintained by the physics solver.</summary>
        /// <param name="shape">Owned primitive or convex volume.</param>
        /// <param name="other">Candidate contact shape.</param>
        /// <param name="tolerance">Broad-phase contact clearance.</param>
        /// <returns>True when the shapes overlap or supported surfaces touch within their contact offsets.</returns>
        private static bool Touches(Collider shape, Collider other, float tolerance)
        {
            // ClosestPoint refinement supports static child colliders that do not forward collision callbacks.
            return ContactDetection.Usable(other, true)
                ? ContactDetection.Touches(shape, other, Mathf.Max(tolerance, shape.contactOffset + other.contactOffset))
                : Physics.ComputePenetration(shape, shape.transform.position, shape.transform.rotation,
                    other, other.transform.position, other.transform.rotation, out _, out _);
        }

        /// <summary>Routes player contacts to respawn and other independent roots to ordinary despawn.</summary>
        /// <param name="shape">Collider actually intersecting the death volume.</param>
        private void Affect(Collider shape)
        {
            // Unity can send collision callbacks to disabled components; apply the same gates as polling.
            if (!ready || !Available(InteractionChannels.Passive) || shape == null || !shape.gameObject.activeInHierarchy
                || (settings.Layers.value & (1 << shape.gameObject.layer)) == 0
                || shape.transform.IsChildOf(transform) || transform.IsChildOf(shape.transform))
                return;
            if (PlayerSceneReturn.TryPlayer(shape, out Transform player))
            {
                if (settings.AffectPlayer && visited.Add(player.gameObject))
                    PlayerSceneReturn.Return(player);
                return;
            }
            GameObject target = ObjectDespawn.Root(shape);
            if (target == gameObject || transform.IsChildOf(target.transform) || !settings.AffectStatic && target.isStatic
                || !visited.Add(target))
                return;
            ObjectDespawn.Remove(target);
        }

        #endregion
        #region Validation

        /// <summary>Checks reusable settings and requires an authored volume.</summary>
        /// <param name="warning">Receives invalid timing or missing supported geometry.</param>
        /// <returns>True when this deathbox can run.</returns>
        public override bool TryValidate(out string warning) => TryValidate(settings, out warning);

        /// <summary>Validates detached settings before Apply without constructing runtime colliders.</summary>
        /// <param name="configuration">Pending volume policy.</param>
        /// <param name="warning">Receives an invalid setting or missing shape.</param>
        /// <returns>True when owned primitive or convex geometry exists.</returns>
        public bool TryValidate(DeathboxSettings configuration, out string warning)
        {
            warning = "Configure Deathbox settings.";
            if (configuration == null || !configuration.TryValidate(out warning))
                return false;
            foreach (Collider shape in GetComponentsInChildren<Collider>(true))
                if (shape.enabled && Item.Owns(shape.transform)
                    && shape is BoxCollider or SphereCollider or CapsuleCollider or MeshCollider { convex: true, sharedMesh: not null })
                    return true;
            warning = "Deathbox needs an existing enabled primitive or convex collider on this object.";
            return false;
        }

        #endregion
        #endregion
    }
}
