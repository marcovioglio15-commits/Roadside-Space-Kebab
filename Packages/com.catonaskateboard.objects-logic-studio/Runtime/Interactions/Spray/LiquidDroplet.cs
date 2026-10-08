using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains one reusable native liquid body and its source-specific collision exclusions.</summary>
    [AddComponentMenu("")]
    internal sealed class LiquidDroplet : MonoBehaviour
    {
        #region State
        private Collider[] ignored;
        private SphereCollider shape;
        private MeshRenderer visual;
        private Transform geometry;
        private SurfaceTrailSettings trail;
        private SurfacePalette palette;
        private float cohesion;
        private float spacing;
        private float radius;
        private bool leavesTrail;
        private MaterialPropertyBlock properties;
        private static readonly int colorId = Shader.PropertyToID("DepositColor");
        private static readonly int blendId = Shader.PropertyToID("DepositBlend");
        private static readonly int shapeId = Shader.PropertyToID("DepositShape");
        private static readonly int surfaceId = Shader.PropertyToID("DepositSurface");
        private static readonly int timingId = Shader.PropertyToID("DepositTiming");
        #endregion
        #region Properties
        internal Rigidbody Body { get; private set; }
        internal ObjectSpraySauce Source { get; private set; }
        internal float Expires { get; private set; }
        #endregion
        #region Methods
        #region Pooling
        /// <summary>Creates native components only when the shared bounded pool is initialized.</summary>
        /// <param name="mesh">Shared unit-radius sphere geometry.</param>
        /// <param name="material">Shared wet-surface shader.</param>
        internal void Initialize(Mesh mesh, Material material)
        {
            properties = new MaterialPropertyBlock();
            gameObject.layer = 2;
            GameObject appearance = new GameObject("Liquid Geometry");
            appearance.layer = 2;
            geometry = appearance.transform;
            geometry.SetParent(transform, false);
            appearance.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual = appearance.AddComponent<MeshRenderer>();
            visual.sharedMaterial = material;
            visual.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shape = gameObject.AddComponent<SphereCollider>();
            shape.radius = 1f;
            Body = gameObject.AddComponent<Rigidbody>();
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
        }
        /// <summary>Configures one droplet before admitting it to any active gravity pulse.</summary>
        /// <param name="source">Emitter whose filters are inherited during suspension.</param>
        /// <param name="position">Outlet position.</param>
        /// <param name="velocity">Initial world motion.</param>
        /// <param name="color">Sampled sauce colour.</param>
        internal void Launch(ObjectSpraySauce source, Vector3 position, Vector3 velocity, Color color)
        {
            // Configure the inactive body before restoring exclusions and joining any active gravity pulse.
            Source = source;
            SpraySauceSettings settings = source.Settings;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            radius = Random.Range(settings.Radius.x, settings.Radius.y);
            transform.localScale = Vector3.one * radius;
            cohesion = settings.Cohesion;
            spacing = 1f / settings.Rate;
            trail = settings.Trail;
            palette = source.Palette;
            leavesTrail = settings.LeaveTrail;
            Body.position = position;
            Body.rotation = Quaternion.identity;
            Body.mass = settings.Mass;
            Body.linearDamping = settings.Damping;
            Body.angularDamping = settings.Damping;
            Body.useGravity = true;
            Body.excludeLayers = ~settings.Layers.value | 1 << 2;
            Expires = Time.time + settings.Lifetime;
            properties.SetColor(colorId, color);
            properties.SetColor(blendId, color);
            properties.SetVector(shapeId, new Vector4(0f, 0f, 0f, 1f));
            properties.SetVector(surfaceId, new Vector4(settings.Trail.Liquidity, settings.Trail.Gloss, 0f, settings.Trail.Opacity));
            properties.SetVector(timingId, new Vector4(Time.time, settings.Lifetime, Mathf.Min(0.25f, settings.Lifetime), 0f));
            visual.SetPropertyBlock(properties);
            gameObject.SetActive(true);
            ignored = source.SourceColliders;
            foreach (Collider collider in ignored)
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                    Physics.IgnoreCollision(shape, collider);
            Body.linearVelocity = velocity;
            Body.angularVelocity = Vector3.zero;
            UpdateShape();
            ObjectGravityGenerator.IncludeSpawn(gameObject);
        }

        /// <summary>Stretches only visible liquid along its current velocity, including suspended motion.</summary>
        internal void UpdateShape()
        {
            Vector3 velocity = Body.linearVelocity;
            float length = Mathf.Max(radius, velocity.magnitude * spacing * 0.65f);
            geometry.localScale = new Vector3(1f, 1f, Mathf.Lerp(1f, length / radius, cohesion));
            if (velocity.sqrMagnitude > 0.000001f)
                geometry.rotation = Quaternion.LookRotation(velocity);
        }
        /// <summary>Ends all gravity leases before this native body can be reused by another source.</summary>
        internal void Recycle()
        {
            if (!gameObject.activeSelf)
                return;
            ObjectGravityGenerator.Forget(Body);
            if (ignored != null)
                foreach (Collider collider in ignored)
                    if (collider != null)
                        Physics.IgnoreCollision(shape, collider, false);
            ignored = null;
            Source = null;
            trail = null;
            palette = null;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            gameObject.SetActive(false);
        }
        /// <summary>Deposits once at the first solid hit, then returns the body to the shared pool.</summary>
        /// <param name="collision">Native continuous collision contact.</param>
        private void OnCollisionEnter(Collision collision)
        {
            if (!gameObject.activeSelf || collision.contactCount == 0)
                return;
            ContactPoint contact = collision.GetContact(0);
            if (Source != null)
                Source.Hit(collision.collider, contact.point, contact.normal, collision.relativeVelocity);
            else if (leavesTrail && trail != null && palette != null)
                SurfaceMarkPool.Deposit(collision.collider, contact.point, contact.normal, collision.relativeVelocity, trail, palette);
            Recycle();
        }
        #endregion
        #endregion
    }
}
