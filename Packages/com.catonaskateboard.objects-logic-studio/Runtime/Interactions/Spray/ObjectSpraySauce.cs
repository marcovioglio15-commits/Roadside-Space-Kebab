using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Emits physical sauce while the observer retains a held Button action.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ObjectElasticDeformation))]
    [AddComponentMenu("Objects Logic Studio/Spray Sauce")]
    public sealed class ObjectSpraySauce : ObjectRequestedInteraction
    {
        #region Fields
        [Header("Spray Sauce")]
        [Tooltip("Nozzle pose, physical flow, surface effects and optional capacity.")]
        [SerializeField]
        private SpraySauceSettings settings = new SpraySauceSettings();
        private readonly SprayExposure exposure = new SprayExposure();
        private SurfacePalette palette;
        private ObjectElasticDeformation elastic;
        private Rigidbody body;
        private Collider[] sourceColliders;
        private Collider[] playerColliders;
        private LiquidDroplet latest;
        private uint latestGeneration;
        private float flowSpeed;
        private float flowRadius;
        private Color flowColor;
        private float used;
        private float pending;
        private float nextDeposit;
        private Collider depositReceiver;
        private bool ready;
        private InteractionButton heldInput;
        #endregion
        #region Properties
        /// <summary>Locally imported nozzle and flow configuration.</summary>
        public SpraySauceSettings Settings => settings;
        /// <summary>Continuous feature identifier used by presets.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.SpraySauce;
        /// <summary>Continuous emission always uses the observer's private input asset.</summary>
        public override bool UsesInput => true;
        /// <summary>Detection applied when the bottle need not be carried.</summary>
        public override TransferTargetSettings Target => settings.Target;
        /// <summary>Whether the held request is currently producing liquid.</summary>
        public bool IsEmitting { get; private set; }
        /// <summary>Seconds left, or positive infinity for an unlimited source.</summary>
        public float Remaining => settings.Limited ? Mathf.Max(0f, settings.Capacity - used) : float.PositiveInfinity;
        /// <summary>Cached source geometry excluded from droplet collisions.</summary>
        internal Collider[] SourceColliders => sourceColliders;
        /// <summary>Player geometry excluded while spraying from a carried source.</summary>
        internal Collider[] PlayerColliders => playerColliders;
        /// <summary>World outlet used by both physical samples and the visible column.</summary>
        internal Vector3 Outlet => transform.TransformPoint(elastic != null ? elastic.DeformPoint(settings.Nozzle.Position) : settings.Nozzle.Position);
        /// <summary>Newest live sample, or null after release or pool saturation.</summary>
        internal LiquidDroplet Latest => latest;
        /// <summary>Prepared palette retained by emitted liquid after the source disappears.</summary>
        internal SurfacePalette Palette => palette;
        /// <summary>Stops automatic start effects when the continuous flow finishes.</summary>
        internal override bool VfxRunning => IsEmitting;
        #endregion
        #region Methods
        #region Lifecycle
        /// <summary>Resets the flow and capacity once at Play entry, including retained scene objects.</summary>
        internal void BeginSession()
        {
            IsEmitting = false;
            used = pending = nextDeposit = 0f;
            depositReceiver = null;
            exposure.Clear();
            OnEnable();
        }

        /// <summary>Registers valid flow once without resetting partially used capacity.</summary>
        protected override void OnEnable()
        {
            if (!Application.isPlaying)
                return;
            ready = TryValidate(out string warning);
            if (!ready)
            {
                Debug.LogWarning(warning, this);
                return;
            }
            elastic = GetComponent<ObjectElasticDeformation>();
            body = GetComponent<Rigidbody>();
            RefreshGeometry();
            base.OnEnable();
        }
        /// <summary>Stops emission and withdraws input when disabled or stored.</summary>
        protected override void OnDisable()
        {
            StopEmission();
            ready = false;
            base.OnDisable();
        }
        /// <summary>Rebuilds collision exclusions and colour sources after assembly changes.</summary>
        internal void RefreshGeometry()
        {
            sourceColliders = GetComponentsInChildren<Collider>(true);
            palette ??= new SurfacePalette(Item);
            palette.RefreshGeometry();
            if (IsEmitting)
                palette.Refresh(settings.Trail);
        }
        #endregion
        #region Emission
        /// <summary>Checks live capacity and locks before competing for a held request.</summary>
        /// <returns>True when a new held request may start emission.</returns>
        internal override bool CanRequest()
        {
            return CanContinue() && !IsEmitting;
        }
        /// <summary>Checks non-input conditions while an existing held request continues.</summary>
        /// <returns>True while flow is available and capacity remains.</returns>
        internal bool CanContinue()
        {
            return ready && Remaining > 0f && Available(InteractionChannels.Continuous) && (!settings.HeldOnly || Item.IsCarried);
        }
        /// <summary>Starts physical emission after common input arbitration selects this object.</summary>
        /// <returns>True when this request starts the flow.</returns>
        internal override bool Request()
        {
            if (!CanRequest())
                return false;
            heldInput = null;
            IsEmitting = true;
            pending = 0f;
            latest = null;
            palette.Refresh(settings.Trail);
            // A coherent burst shares a speed, width and colour instead of randomizing its entire shape at every sample.
            flowSpeed = Random.Range(settings.Speed.x, settings.Speed.y);
            flowRadius = Random.Range(settings.Radius.x, settings.Radius.y);
            flowColor = palette.Sample();
            playerColliders = Item.IsCarried && HoverObserver.ActivePlayer != null
                ? HoverObserver.ActivePlayer.GetComponentsInChildren<Collider>(true) : null;
            Emit(0f);
            if (settings.Squeeze && elastic != null && elastic.isActiveAndEnabled)
                elastic.SetPressure(settings.Compression, settings.SqueezeAxis, settings.SqueezeDuration);
            Signal(InteractionMoment.Started);
            return true;
        }
        /// <summary>Retains the private held action so physics cannot emit after a release between observer updates.</summary>
        /// <param name="button">Private player action selected by the observer.</param>
        /// <returns>True when the held request started emission.</returns>
        internal bool Request(InteractionButton button)
        {
            if (button == null || !button.Held || !Request())
                return false;
            heldInput = button;
            return true;
        }
        /// <summary>Releases visual pressure without removing liquid that has already left the nozzle.</summary>
        internal void StopEmission()
        {
            if (!IsEmitting)
                return;
            IsEmitting = false;
            heldInput = null;
            pending = 0f;
            latest = null;
            if (settings.Squeeze && elastic != null)
                elastic.SetPressure(0f, settings.SqueezeAxis, settings.SqueezeDuration);
            Signal(InteractionMoment.Completed);
        }
        /// <summary>Consumes capacity in simulation time and emits only from the bounded liquid pool.</summary>
        private void FixedUpdate()
        {
            if (!IsEmitting)
                return;
            if (!CanContinue() || heldInput != null && !heldInput.Held)
            {
                StopEmission();
                return;
            }
            // Integrate flow and capacity in simulation time, so pausing never drains the source.
            float duration = Mathf.Min(Time.fixedDeltaTime, Remaining);
            used += duration;
            pending += duration * settings.Rate;
            while (pending >= 1f)
            {
                pending -= 1f;
                Emit(pending / settings.Rate);
            }
            if (Remaining <= 0f)
                StopEmission();
        }

        /// <summary>Emits regularly spaced samples with small variations around this burst's pressure.</summary>
        /// <param name="age">Fraction of the current physics step already elapsed since this sample was due.</param>
        private void Emit(float age)
        {
            Vector3 position = Outlet;
            Quaternion rotation = Quaternion.Euler(settings.Nozzle.Rotation);
            float variation = 1f - settings.Cohesion;
            Vector2 spread = Random.insideUnitCircle * Mathf.Tan(settings.Spread * Mathf.Deg2Rad) * variation;
            Vector3 localDirection = rotation * new Vector3(spread.x, spread.y, 1f);
            Vector3 direction = transform.TransformDirection(elastic != null ? elastic.DeformDirection(localDirection) : localDirection).normalized;
            float speed = Mathf.Lerp(flowSpeed, Random.Range(settings.Speed.x, settings.Speed.y), variation) * settings.LaunchPower;
            Vector3 velocity = direction * speed
                + (body != null ? body.GetPointVelocity(position) * settings.InheritVelocity : Vector3.zero);
            float radius = Mathf.Lerp(flowRadius, Random.Range(settings.Radius.x, settings.Radius.y), variation);
            latest = LiquidPool.Emit(this, position, velocity, Color.Lerp(flowColor, palette.Sample(), variation), radius, age,
                latest != null && latest.Generation == latestGeneration && latest.gameObject.activeSelf && latest.Source == this ? latest : null);
            latestGeneration = latest != null ? latest.Generation : 0;
        }
        /// <summary>Applies shared marks and optional exposure changes at a native liquid hit.</summary>
        /// <param name="receiver">Exact collided surface.</param>
        /// <param name="point">World contact point.</param>
        /// <param name="normal">Outward receiver normal.</param>
        /// <param name="velocity">Incoming liquid motion.</param>
        internal void Hit(Collider receiver, Vector3 point, Vector3 normal, Vector3 velocity)
        {
            SpraySurfaceRule rule = settings.ChangeAppearance ? exposure.Hit(this, receiver) : null;
            bool customTrail = rule != null && rule.TrailMode != SprayTrailMode.Normal;
            if ((settings.LeaveTrail || customTrail) && (receiver != depositReceiver || Time.time >= nextDeposit))
            {
                if (!SurfaceMarkPool.Deposit(receiver, point, normal, velocity, settings.Trail, palette,
                    new SurfaceMarkTiming(settings.Trail, customTrail ? rule : null)))
                    return;
                depositReceiver = receiver;
                nextDeposit = Time.time + settings.DepositInterval;
            }
        }
        #endregion
        #region Validation
        /// <summary>Checks applied data and the required input binding.</summary>
        /// <param name="warning">Receives the first invalid setting.</param>
        /// <returns>True when emission can register with the observer.</returns>
        public override bool TryValidate(out string warning)
        {
            return TryValidate(settings, Action, out warning);
        }
        /// <summary>Checks a detached proposal before Apply.</summary>
        /// <param name="proposal">Pending flow configuration.</param>
        /// <param name="button">Pending Button action reference.</param>
        /// <param name="warning">Receives invalid settings or missing input.</param>
        /// <returns>True when the proposal can run.</returns>
        public bool TryValidate(SpraySauceSettings proposal, InputActionReference button, out string warning)
        {
            warning = "Configure Spray Sauce.";
            return proposal != null && proposal.TryValidate(out warning) && ValidateInput(true, button, out warning);
        }
        #endregion
        #endregion
    }
}
