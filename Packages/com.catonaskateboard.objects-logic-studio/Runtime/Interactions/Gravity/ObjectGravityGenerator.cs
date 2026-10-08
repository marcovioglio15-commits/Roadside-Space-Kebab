using System.Collections.Generic;
using CatOnASkateboard.PlayerStudio;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Acquires matching bodies at pulse and spawn boundaries, restoring them independently of other generators.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Gravity Generator")]
    public sealed class ObjectGravityGenerator : ObjectRequestedInteraction, IPlayerSuspensionSource
    {
        #region Fields

        [Header("Gravity Generator")]
        [Tooltip("Pulse interval, body filters, restoration policy and physical impulse.")]
        [SerializeField]
        private GravitySettings settings = new GravitySettings();

        /// <summary>Retains sampled forces while temporary motion ownership delays physics.</summary>
        private sealed class AffectedBody
        {
            #region Fields

            internal readonly Rigidbody Body;
            internal readonly Vector3 Force;
            internal readonly float Duration;
            internal readonly ObjectItem Item;
            internal readonly ObjectAssemblyProduct Product;
            internal bool Started;
            internal float ForceUntil;

            #endregion
            #region Methods

            /// <summary>Captures one pulse's immutable force choices.</summary>
            /// <param name="body">Selected active body.</param>
            /// <param name="force">Sampled world-space impulse or acceleration.</param>
            /// <param name="duration">Duration of acceleration, or zero for one impulse.</param>
            /// <param name="item">Optional item whose reservation controls the physical handoff.</param>
            internal AffectedBody(Rigidbody body, Vector3 force, float duration, ObjectItem item)
            {
                Body = body;
                Force = force;
                Duration = duration;
                Item = item;
                Product = body.GetComponent<ObjectAssemblyProduct>();
            }

            #endregion
        }

        private readonly List<AffectedBody> affected = new List<AffectedBody>();
        private readonly List<PlayerCharacterControllerMotor> players = new List<PlayerCharacterControllerMotor>();
        private static readonly HashSet<ObjectGravityGenerator> active = new HashSet<ObjectGravityGenerator>();
        private float nextPulse;
        private float elapsed;
        private bool ready;

        #endregion
        #region Properties

        /// <summary>Locally imported pulse configuration.</summary>
        public GravitySettings Settings => settings;
        /// <summary>Whether this generator currently owns a suspension pulse.</summary>
        public bool IsSuspending { get; private set; }
        /// <summary>Whether restoration accepts a player request.</summary>
        public override bool UsesInput => settings.Restore != GravityRestoreMode.Timer;
        /// <summary>Targeting used by the shared observer for restoration requests.</summary>
        public override TransferTargetSettings Target => settings.Target;
        /// <summary>Passive feature identifier retained by presets.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.GravityGenerator;

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Clears pulse discovery when entering Play without a domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActive()
        {
            ObjectIdentity.Changed -= IdentityChanged;
            active.Clear();
        }

        /// <summary>Validates once and schedules the first pulse when activated.</summary>
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
            base.OnEnable();
            Schedule();
        }

        /// <summary>Restores all owned bodies when disabled, destroyed or stored.</summary>
        protected override void OnDisable()
        {
            Restore(false);
            ready = false;
            base.OnDisable();
        }

        /// <summary>Recovers pulse scheduling when entering Play with retained scene objects.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            foreach (ObjectGravityGenerator generator in FindObjectsByType<ObjectGravityGenerator>())
                if (generator.isActiveAndEnabled)
                {
                    generator.OnDisable();
                    generator.OnEnable();
                }
        }

        /// <summary>Waits without scanning the scene between pulse boundaries.</summary>
        private void Update()
        {
            if (ready && !IsSuspending && Time.timeScale > 0f && Time.time >= nextPulse && Available(InteractionChannels.Passive))
                Begin();
        }

        /// <summary>Applies sustained acceleration only to the cached active body set.</summary>
        private void FixedUpdate()
        {
            if (!IsSuspending || Time.timeScale <= 0f)
                return;
            // Deactivated or destroyed bodies release their lease immediately; no scene search occurs here.
            for (int index = affected.Count - 1; index >= 0; index--)
            {
                AffectedBody entry = affected[index];
                if (entry.Body == null || !entry.Body.gameObject.activeInHierarchy
                    || entry.Item != null && (!entry.Item.isActiveAndEnabled || entry.Item.IsConsumed))
                {
                    GravitySuspension.Release(entry.Body, this);
                    affected.RemoveAt(index);
                    continue;
                }
                Activate(entry);
                if (entry.Started && !entry.Body.isKinematic && elapsed < entry.ForceUntil
                    && (entry.Item == null || !entry.Item.IsReserved && !entry.Item.IsCarried))
                {
                    float remaining = settings.Restore == GravityRestoreMode.Interaction ? entry.ForceUntil - elapsed
                        : Mathf.Min(entry.ForceUntil - elapsed, settings.Duration - elapsed);
                    float fraction = Mathf.Min(Time.fixedDeltaTime, remaining) / Time.fixedDeltaTime;
                    entry.Body.AddForce(entry.Force * fraction, ForceMode.Acceleration);
                }
            }
            for (int index = players.Count - 1; index >= 0; index--)
                if (players[index] == null || !players[index].isActiveAndEnabled)
                {
                    if (players[index] != null)
                        players[index].RestoreGravity(this);
                    players.RemoveAt(index);
                }
            elapsed += Time.fixedDeltaTime;
            // Timer restoration remains available even if a later lock prevents new pulses or player input.
            if (settings.Restore != GravityRestoreMode.Interaction && elapsed >= settings.Duration)
                Restore(true);
        }

        #endregion
        #region Pulses

        /// <summary>Samples the interval once after restoration, never on ordinary frames.</summary>
        private void Schedule()
        {
            nextPulse = Time.time + (settings.RandomInterval ? Random.Range(settings.IntervalRange.x, settings.IntervalRange.y) : settings.Interval);
        }

        /// <summary>Acquires matching dynamic bodies and samples each body's force once.</summary>
        private void Begin()
        {
            elapsed = 0f;
            IsSuspending = true;
            if (active.Count == 0)
                ObjectIdentity.Changed += IdentityChanged;
            active.Add(this);
            if (settings.AffectObjects)
                foreach (Rigidbody body in FindObjectsByType<Rigidbody>())
                    Include(body);
            // Player motors integrate their own cached forces through the existing controller's Move call.
            if (settings.AffectPlayer)
                foreach (PlayerCharacterControllerMotor player in FindObjectsByType<PlayerCharacterControllerMotor>())
                {
                    if (!player.isActiveAndEnabled || settings.FilterPlayer && !settings.Filter.Matches(player, transform))
                        continue;
                    Vector3 force = settings.SampleForce(transform, out float duration);
                    if (settings.Restore != GravityRestoreMode.Interaction)
                        duration = Mathf.Min(duration, settings.Duration);
                    if (player.SuspendGravity(this, settings.Player, force, duration))
                        players.Add(player);
                }
            Signal(InteractionMoment.Started);
        }

        /// <summary>Admits newly active items to ongoing pulses without a per-frame scene scan.</summary>
        /// <param name="root">Newly spawned or restored hierarchy.</param>
        internal static void IncludeSpawn(GameObject root)
        {
            if (!Application.isPlaying || active.Count == 0 || !root.activeInHierarchy)
                return;
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>())
                foreach (ObjectGravityGenerator generator in active)
                    generator.Include(body);
        }

        /// <summary>Reevaluates changed identities only while at least one pulse is active.</summary>
        /// <param name="identity">Object whose permanent or temporary flags just changed.</param>
        private static void IdentityChanged(ObjectIdentity identity)
        {
            IncludeSpawn(identity.gameObject);
        }

        /// <summary>Samples one late participant once, retaining the existing pulse's restoration deadline.</summary>
        /// <param name="body">Dynamic body selected at a pulse or spawn boundary.</param>
        private void Include(Rigidbody body)
        {
            if (!settings.AffectObjects || !body.gameObject.activeInHierarchy
                || GravitySuspension.Contains(body, this) || !settings.Filter.Matches(body, transform))
                return;
            // Exclude attached ingredient bodies and authored kinematic scenery, but retain pending item transitions.
            body.TryGetComponent(out ObjectItem item);
            if (item != null && (!item.isActiveAndEnabled || item.IsConsumed)
                || body.isKinematic && (item == null || !item.IsReserved && !item.IsCarried
                    && !(body.TryGetComponent(out ObjectAssemblyProduct product) && product.Table != null)
                    && !(body.TryGetComponent(out ObjectGrab grab) && grab.Dock != null)))
                return;
            Vector3 force = settings.SampleForce(transform, out float duration);
            GravitySuspension.Acquire(body, this);
            AffectedBody entry = new AffectedBody(body, force, duration, item);
            affected.Add(entry);
            Activate(entry);
        }

        /// <summary>Hands off a retained product only after its current transaction permits physical motion.</summary>
        /// <param name="entry">Participant whose force choices must not be resampled.</param>
        private void Activate(AffectedBody entry)
        {
            // Reservations and carry retain priority over suspension forces until their own release boundary.
            if (entry.Item != null && (entry.Item.IsReserved || entry.Item.IsCarried))
                return;
            if (entry.Body.isKinematic && entry.Product != null)
                entry.Product.ReleaseForSuspension();
            if (entry.Started || entry.Body.isKinematic)
                return;
            entry.Started = true;
            entry.ForceUntil = elapsed + entry.Duration;
            if (entry.Duration <= 0f && settings.Push)
                entry.Body.AddForce(entry.Force, ForceMode.Impulse);
        }

        /// <summary>Exposes shared active-body membership to the player's independent collision simulation.</summary>
        /// <param name="body">Body contacted by the suspended player.</param>
        /// <returns>True when any active generator currently suspends this body.</returns>
        public bool IsBodySuspended(Rigidbody body)
        {
            return GravitySuspension.Contains(body);
        }

        /// <summary>Releases this generator's ownership and schedules its next pulse.</summary>
        /// <param name="completed">Publish completion for a timer or player restoration, not for cancellation.</param>
        private void Restore(bool completed)
        {
            if (!IsSuspending)
                return;
            foreach (AffectedBody entry in affected)
                GravitySuspension.Release(entry.Body, this);
            affected.Clear();
            foreach (PlayerCharacterControllerMotor player in players)
                if (player != null)
                    player.RestoreGravity(this);
            players.Clear();
            IsSuspending = false;
            active.Remove(this);
            if (active.Count == 0)
                ObjectIdentity.Changed -= IdentityChanged;
            Schedule();
            if (completed)
                Signal(InteractionMoment.Completed);
        }

        /// <summary>Offers the restoration button only during an eligible active pulse.</summary>
        /// <returns>True when a player request may restore this generator's bodies.</returns>
        internal override bool CanRequest()
        {
            return ready && UsesInput && IsSuspending && Available(InteractionChannels.Passive);
        }

        /// <summary>Restores gravity after the observer validates range and aim.</summary>
        /// <returns>True when the button completed an active pulse.</returns>
        internal override bool Request()
        {
            if (!CanRequest())
                return false;
            Restore(true);
            return true;
        }

        #endregion
        #region Validation

        /// <summary>Validates the applied pulse and optional input binding.</summary>
        /// <param name="warning">Receives missing or invalid settings.</param>
        /// <returns>True when this generator may schedule pulses.</returns>
        public override bool TryValidate(out string warning)
        {
            return TryValidate(settings, Action, out warning);
        }

        /// <summary>Validates a detached generator proposal before Apply.</summary>
        /// <param name="configuration">Proposed pulse settings.</param>
        /// <param name="button">Proposed restoration action.</param>
        /// <param name="warning">Receives invalid settings or a missing Button.</param>
        /// <returns>True when the proposal is complete.</returns>
        public bool TryValidate(GravitySettings configuration, InputActionReference button, out string warning)
        {
            warning = "Configure gravity generator settings.";
            return configuration != null && configuration.TryValidate(out warning)
                && ValidateInput(configuration.Restore != GravityRestoreMode.Timer, button, out warning);
        }

        #endregion
        #endregion
    }
}
