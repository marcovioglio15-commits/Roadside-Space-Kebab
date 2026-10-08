using System.Collections.Generic;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Combines soft impact feedback and optional sustained pressure without scaling gameplay transforms.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Elastic Deformation")]
    public sealed class ObjectElasticDeformation : ObjectExtendedInteraction
    {
        #region Fields

        [Header("Elastic Deformation")]
        [Tooltip("Impact sensitivity, damped rebound and visual geometry selection.")]
        [SerializeField]
        private ElasticSettings settings = new ElasticSettings();
        private readonly List<ElasticMesh> meshes = new List<ElasticMesh>();
        private Rigidbody body;
        private Vector3 impactAxis = Vector3.up;
        private Vector3 squeezeAxis = Vector3.right;
        private float amplitude;
        private float started;
        private float nextImpact;
        private float squeeze;
        private float squeezeTarget;
        private float squeezeSpeed = 1f;
        private bool ready;
        private bool bound;
        private bool posed;

        #endregion
        #region Properties

        /// <summary>Locally imported response configuration.</summary>
        public ElasticSettings Settings => settings;
        /// <summary>Passive category identifier used by snapshots and filters.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.ElasticDeformation;

        #endregion
        #region Methods
        #region Lifecycle
        /// <summary>Restores cached geometry and response clocks once when Play retains scene objects.</summary>
        internal void BeginSession()
        {
            OnDisable();
            RefreshGeometry();
            started = nextImpact = 0f;
            OnEnable();
        }


        /// <summary>Validates once and retains the optional mass source for collision feedback.</summary>
        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;
            ready = TryValidate(out string warning);
            if (!ready)
                Debug.LogWarning(warning, this);
            body = GetComponent<Rigidbody>();
        }

        /// <summary>Restores visual references when stored, assembled into another product or disabled.</summary>
        private void OnDisable()
        {
            RestoreGeometry();
            amplitude = squeeze = squeezeTarget = 0f;
            ready = false;
        }

        /// <summary>Releases only privately allocated visual meshes.</summary>
        private void OnDestroy()
        {
            RefreshGeometry();
        }

        /// <summary>Samples collision impulse once at impact, independently of the contact duration.</summary>
        /// <param name="collision">Native physics impact with another body or static surface.</param>
        private void OnCollisionEnter(Collision collision)
        {
            if (!ready || !settings.Collisions || collision.contactCount == 0 || Time.time < nextImpact
                || (settings.Layers.value & 1 << collision.gameObject.layer) == 0 || !Available(InteractionChannels.Passive))
                return;
            float intensity = collision.impulse.magnitude / (body != null ? body.mass : 1f);
            Impact(collision.GetContact(0).normal, intensity);
        }

        /// <summary>Updates cached visual buffers only while an impact or pressure response is visible.</summary>
        private void LateUpdate()
        {
            if (!ready || Time.timeScale <= 0f || !posed && amplitude == 0f && squeeze == 0f && squeezeTarget == 0f)
                return;
            float time = (Time.time - started) / settings.Duration;
            float compression = time < 1f ? amplitude * Mathf.Exp(-settings.Damping * time)
                * Mathf.Cos(time * settings.Oscillations * Mathf.PI * 2f) * (1f - time) : 0f;
            if (time >= 1f)
                amplitude = 0f;
            squeeze = Mathf.MoveTowards(squeeze, squeezeTarget, squeezeSpeed * Time.deltaTime);
            if (amplitude == 0f && squeeze == 0f)
            {
                if (posed)
                    Signal(InteractionMoment.Completed);
                RestoreGeometry();
                return;
            }
            BindGeometry();
            foreach (ElasticMesh mesh in meshes)
                mesh.Apply(transform, settings.Pivot, impactAxis, compression, settings.Volume, squeezeAxis, squeeze);
            posed = true;
        }

        #endregion
        #region Response

        /// <summary>Starts a bounded response from an externally measured physical impact.</summary>
        /// <param name="worldAxis">Collision normal or incoming force direction.</param>
        /// <param name="intensity">Impulse divided by object mass in metres per second.</param>
        internal void Impact(Vector3 worldAxis, float intensity)
        {
            if (!ready || intensity <= settings.MinimumImpact || !InteractionValues.Finite(worldAxis) || worldAxis.sqrMagnitude < 0.000001f)
                return;
            float requested = settings.Compression * Mathf.InverseLerp(settings.MinimumImpact, settings.FullImpact, intensity);
            if (requested <= 0f)
                return;
            if (settings.StrongestWins && Time.time - started < settings.Duration
                && requested < amplitude * Mathf.Exp(-settings.Damping * (Time.time - started) / settings.Duration))
                return;
            amplitude = requested;
            impactAxis = transform.InverseTransformDirection(worldAxis).normalized;
            started = Time.time;
            nextImpact = Time.time + settings.Cooldown;
            Signal(InteractionMoment.Started);
        }

        /// <summary>Sets pressure from a continuous interaction and smoothly releases it when emission ends.</summary>
        /// <param name="amount">Validated compression fraction, or zero to release.</param>
        /// <param name="axis">Object-local pressure axis.</param>
        /// <param name="seconds">Time to reach the requested pressure from rest.</param>
        internal void SetPressure(float amount, Vector3 axis, float seconds)
        {
            squeezeTarget = amount;
            squeezeAxis = axis.normalized;
            squeezeSpeed = Mathf.Max(amount, squeeze) / seconds;
        }

        #endregion
        #region Geometry

        /// <summary>Collects owned visual geometry once, excluding other items and generated effects.</summary>
        private void BindGeometry()
        {
            if (bound)
                return;
            bound = true;
            Transform root = PlayerHierarchy.Resolve(transform, settings.Path);
            if (root == null)
                return;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (Item.Owns(renderer.transform) && (renderer is SkinnedMeshRenderer || renderer is MeshRenderer && renderer.GetComponent<MeshFilter>() != null))
                    meshes.Add(new ElasticMesh(renderer));
        }

        /// <summary>Exposes original meshes before another appearance operation captures its baseline.</summary>
        internal void RestoreGeometry()
        {
            foreach (ElasticMesh mesh in meshes)
                mesh.Restore();
            posed = false;
        }

        /// <summary>Invalidates geometry after an assembly changes its owned renderer hierarchy.</summary>
        internal void RefreshGeometry()
        {
            foreach (ElasticMesh mesh in meshes)
                mesh.Dispose();
            meshes.Clear();
            bound = false;
        }

        /// <summary>Checks configuration and the optional visual branch without allocating mesh instances.</summary>
        /// <param name="warning">Receives invalid response or hierarchy settings.</param>
        /// <returns>True when the feature can bind its configured branch.</returns>
        public override bool TryValidate(out string warning)
        {
            return TryValidate(settings, out warning);
        }

        /// <summary>Validates an independent editor proposal against this object's hierarchy.</summary>
        /// <param name="proposal">Pending response settings.</param>
        /// <param name="warning">Receives an invalid setting or missing visual branch.</param>
        /// <returns>True when the proposal can be applied.</returns>
        public bool TryValidate(ElasticSettings proposal, out string warning)
        {
            warning = "Configure Elastic Deformation.";
            if (proposal == null || !proposal.TryValidate(out warning))
                return false;
            warning = "Choose an existing visual branch for Elastic Deformation.";
            if (PlayerHierarchy.Resolve(transform, proposal.Path) == null)
                return false;
            warning = string.Empty;
            return true;
        }

        #endregion
        #endregion
    }
}
