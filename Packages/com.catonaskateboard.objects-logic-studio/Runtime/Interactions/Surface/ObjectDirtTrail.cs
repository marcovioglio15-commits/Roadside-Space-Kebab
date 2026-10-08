using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Deposits surface-conforming residue at collision boundaries without creating per-frame trails at rest.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Dirt Trail")]
    public sealed class ObjectDirtTrail : ObjectExtendedInteraction
    {
        #region Fields

        [Header("Dirt Trail")]
        [Tooltip("Receiving surfaces, contact thresholds and fading residue appearance.")]
        [SerializeField]
        private DirtTrailSettings settings = new DirtTrailSettings();
        private SurfacePalette palette;
        private Collider previous;
        private Vector3 lastPoint;
        private float nextDeposit;
        private bool ready;

        #endregion
        #region Properties

        /// <summary>Locally imported trail configuration.</summary>
        public DirtTrailSettings Settings => settings;
        /// <summary>Passive interaction identifier.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.DirtTrail;

        #endregion
        #region Methods
        #region Lifecycle
        /// <summary>Restarts contact clocks at Play entry without creating rendering resources.</summary>
        internal void BeginSession()
        {
            OnEnable();
        }


        /// <summary>Validates contact emission and captures material sources once at activation.</summary>
        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;
            ready = TryValidate(out string warning);
            if (!ready)
                Debug.LogWarning(warning, this);
            palette ??= new SurfacePalette(Item);
            palette.RefreshGeometry();
            previous = null;
            nextDeposit = 0f;
        }

        /// <summary>Emits initial impact residue when relative speed exceeds the configured threshold.</summary>
        /// <param name="collision">Native collision event.</param>
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.relativeVelocity.sqrMagnitude >= settings.MinimumSpeed * settings.MinimumSpeed)
                Deposit(collision, false);
        }

        /// <summary>Extends the trail only after meaningful movement along the receiver.</summary>
        /// <param name="collision">Retained native contact.</param>
        private void OnCollisionStay(Collision collision)
        {
            if (settings.Sliding)
                Deposit(collision, true);
        }

        #endregion
        #region Deposits

        /// <summary>Checks surface ownership and spacing before sampling materials or projecting geometry.</summary>
        /// <param name="collision">Contact data retained only for this callback.</param>
        /// <param name="sliding">Whether to enforce distance from the previous contact.</param>
        private void Deposit(Collision collision, bool sliding)
        {
            if (!ready || Time.timeScale <= 0f || Time.time < nextDeposit || collision.contactCount == 0
                || !Available(InteractionChannels.Passive) || (settings.Layers.value & 1 << collision.gameObject.layer) == 0)
                return;
            Collider receiver = collision.collider;
            if (receiver.transform.IsChildOf(transform) || receiver.GetComponentInParent<LiquidDroplet>() != null)
                return;
            ObjectIdentity identity = settings.FilterFlags ? receiver.GetComponentInParent<ObjectIdentity>() : null;
            if (settings.FilterFlags && (identity == null || !identity.Matches(settings.Flags)))
                return;
            ContactPoint contact = collision.GetContact(0);
            Vector3 local = receiver.transform.InverseTransformPoint(contact.point);
            if (sliding && previous == receiver && (receiver.transform.TransformPoint(lastPoint) - contact.point).sqrMagnitude < settings.Spacing * settings.Spacing)
                return;
            previous = receiver;
            lastPoint = local;
            nextDeposit = Time.time + settings.Interval;
            palette.Refresh(settings.Trail);
            SurfaceMarkPool.Deposit(receiver, contact.point, contact.normal, collision.relativeVelocity, settings.Trail, palette);
            Signal(InteractionMoment.Started);
            Signal(InteractionMoment.Completed);
        }

        /// <summary>Refreshes material sources after an assembly changes the visual hierarchy.</summary>
        internal void RefreshGeometry()
        {
            palette?.RefreshGeometry();
        }

        /// <summary>Checks the locally applied trail configuration without allocating render resources.</summary>
        /// <param name="warning">Receives invalid settings.</param>
        /// <returns>True when the interaction is configured.</returns>
        public override bool TryValidate(out string warning)
        {
            warning = "Configure Dirt Trail settings.";
            return settings != null && settings.TryValidate(out warning);
        }

        #endregion
        #endregion
    }
}
