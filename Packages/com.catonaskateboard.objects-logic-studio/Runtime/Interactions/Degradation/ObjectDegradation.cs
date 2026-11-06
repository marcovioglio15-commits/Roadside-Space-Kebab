using CatOnASkateboard.AudioStudio;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Commits degradation only at stage boundaries while respecting carry and reservation policies.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Object Degradation")]
    public sealed class ObjectDegradation : ObjectExtendedInteraction, IPlayerToolImpactReceiver
    {
        #region Fields

        [Header("Degradation")]
        [Tooltip("Time or impact stages, carry policy and optional final fragments.")]
        [SerializeField]
        private DegradationSettings settings = new DegradationSettings();
        private ItemAppearanceChanges[] appearances;
        private ObjectGrab grab;
        private float elapsed;
        private float lastImpact = float.NegativeInfinity;
        private int impacts;
        private bool firstGrab;
        private bool started;
        private bool ready;

        #endregion
        #region Properties

        /// <summary>Locally imported degradation sequence.</summary>
        public DegradationSettings Settings => settings;
        /// <summary>Number of stages already entered by this instance.</summary>
        public int CompletedSteps { get; private set; }
        /// <summary>Passive category used by presets and availability conditions.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.ObjectDegradation;

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Prepares appearance bindings once and resumes retained progress on reactivation.</summary>
        private void OnEnable()
        {
            if (InteractionRuntimeState.IsStaging)
                return;
            if (!Application.isPlaying)
                return;
            ObjectInteraction.Signaled += Observe;
            if (appearances != null)
                return;
            grab = GetComponent<ObjectGrab>();
            firstGrab |= grab != null && grab.IsHeld;
            ready = TryValidate(out string warning);
            if (!ready)
            {
                Debug.LogWarning(warning, this);
                return;
            }
            appearances = new ItemAppearanceChanges[settings.Steps.Length];
            for (int index = 0; index < appearances.Length; index++)
                if (!ItemAppearanceChanges.TryPrepare(Item, settings.Steps[index].Appearance, out appearances[index], out warning))
                {
                    ready = false;
                    Debug.LogWarning(warning, this);
                    return;
                }
        }

        /// <summary>Stops listening while disabled without losing stage progress through storage.</summary>
        private void OnDisable()
        {
            if (InteractionRuntimeState.IsStaging)
                return;
            ObjectInteraction.Signaled -= Observe;
        }

        /// <summary>Restores transient state when scene reload is disabled at Play entry.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            foreach (ObjectDegradation feature in FindObjectsByType<ObjectDegradation>(FindObjectsInactive.Include))
                feature.ResetProgress();
        }

        /// <summary>Explicitly resets a pooled object's degradation and original mesh/material appearance.</summary>
        public void ResetProgress()
        {
            // Restore in reverse order because later stages may replace earlier stage targets.
            OnDisable();
            if (appearances != null)
                for (int index = CompletedSteps - 1; index >= 0; index--)
                    appearances[index]?.Restore();
            appearances = null;
            CompletedSteps = impacts = 0;
            elapsed = 0f;
            lastImpact = float.NegativeInfinity;
            ready = firstGrab = started = false;
            if (isActiveAndEnabled)
                OnEnable();
        }

        #endregion
        #region Progress

        /// <summary>Records a successful Grab rather than merely detecting a nearby player.</summary>
        /// <param name="source">Interaction publishing its successful event.</param>
        /// <param name="moment">Published boundary.</param>
        private void Observe(ObjectInteraction source, InteractionMoment moment)
        {
            if (source == grab && moment == InteractionMoment.Started)
                firstGrab = true;
        }

        /// <summary>Checks progression policies, including the optional exception for a simulated weapon hit.</summary>
        /// <param name="simulatedImpact">Whether this stage would advance from the weapon's simulated impulse.</param>
        /// <returns>True when this instance may advance its next stage.</returns>
        private bool CanProgress(bool simulatedImpact = false)
        {
            // The weapon exception applies to this hit without unlocking physical impacts before Grab.
            return isActiveAndEnabled && ready && CompletedSteps < settings.Steps.Length && Available(InteractionChannels.Passive) && Item != null && !Item.IsReserved
                && (settings.Carry == DegradationCarryPolicy.FromSpawn
                    || settings.Carry == DegradationCarryPolicy.AfterFirstGrab && (firstGrab || simulatedImpact && settings.AllowSimulatedImpactBeforeGrab)
                    || settings.Carry == DegradationCarryPolicy.OnlyWhileCarried && grab != null && grab.IsHeld);
        }

        /// <summary>Accumulates scaled time without rebuilding appearance data each frame.</summary>
        private void Update()
        {
            if (settings.Mode != DegradationMode.Time || Time.deltaTime <= 0f || !CanProgress())
                return;
            elapsed += Time.deltaTime;
            while (CanProgress() && elapsed >= settings.Steps[CompletedSteps].Duration)
            {
                elapsed -= settings.Steps[CompletedSteps].Duration;
                Advance();
            }
        }

        /// <summary>Counts one sufficiently strong physical impact per configured cooldown.</summary>
        /// <param name="collision">Unity collision event carrying its resolved impulse.</param>
        private void OnCollisionEnter(Collision collision)
        {
            RegisterImpact(collision.impulse.magnitude);
        }

        /// <summary>Lets a simulated hit spend its impulse across successive impact stages.</summary>
        /// <param name="impulse">Equivalent impulse supplied by the weapon.</param>
        void IPlayerToolImpactReceiver.ReceiveToolImpact(float impulse) => RegisterImpact(impulse, true);

        /// <summary>Shares impact thresholds between physical collisions, carry sweeps and weapon impulses.</summary>
        /// <param name="impulse">Solver impulse, or the equivalent impulse from a simulated hit.</param>
        /// <param name="simulatedImpact">Whether the hit uses the weapon's impulse budget and carry exception.</param>
        internal void RegisterImpact(float impulse, bool simulatedImpact = false)
        {
            // Compound contacts at the same time count once; sustained pressure is not a fresh carry impact.
            if (settings.Mode != DegradationMode.Impacts || Time.timeScale <= 0f || !CanProgress(simulatedImpact)
                || !float.IsFinite(impulse) || impulse < settings.MinimumImpulse
                || Time.time <= lastImpact || Time.time - lastImpact < settings.ImpactInterval)
                return;
            lastImpact = Time.time;
            // Consume the budget per stage, even when a single hit exceeds an integer impact count.
            double budget = simulatedImpact ? (double)impulse / settings.MinimumImpulse : 1d;
            do
            {
                int remaining = settings.Steps[CompletedSteps].Impacts - impacts;
                if (budget < remaining)
                {
                    impacts += (int)budget;
                    return;
                }
                budget -= remaining;
                impacts = 0;
                if (!Advance())
                    return;
            }
            while (budget >= 1d && CanProgress(simulatedImpact));
        }

        /// <summary>Commits one appearance and identity change before publishing sequence completion.</summary>
        /// <returns>True after the current stage was committed.</returns>
        private bool Advance()
        {
            if (!Item.Acquire(this, InteractionChannels.None))
                return false;
            try
            {
                DegradationStep step = settings.Steps[CompletedSteps];
                appearances[CompletedSteps].Commit();
                if (step.ChangeFlag && !InteractionFlagChange.TryApplyFlag(gameObject, step.Flag, out string warning, step.Operation))
                    Debug.LogWarning(warning, this);
                CompletedSteps++;
                if (!started)
                {
                    started = true;
                    Signal(InteractionMoment.Started);
                }
                if (CompletedSteps != settings.Steps.Length)
                    return true;
                Signal(InteractionMoment.Completed);
                if (!settings.DestroyAtEnd)
                    return true;
                foreach (DegradationFragment fragment in settings.Fragments)
                {
                    GameObject created = ObjectSpawnHistory.Create(fragment.Prefab, transform.TransformPoint(fragment.Position), transform.rotation * Quaternion.Euler(fragment.Rotation));
                    SceneManager.MoveGameObjectToScene(created, gameObject.scene);
                }
                grab?.Cancel();
                if (settings.Mode == DegradationMode.Impacts && settings.BreakSound)
                    StudioAudio.Play(settings.BreakMaterial == DegradationBreakSound.Ceramic ? "sfx_ceramicbreak" : "sfx_glassbreak", transform);
                Item.ConsumeSelf();
                Destroy(gameObject);
                return true;
            }
            finally
            {
                Item.Release(this);
            }
        }

        #endregion
        #region Validation

        /// <summary>Checks this object's applied degradation settings.</summary>
        /// <param name="warning">Receives invalid data or missing geometry.</param>
        /// <returns>True when runtime progression can begin.</returns>
        public override bool TryValidate(out string warning)
        {
            return TryValidate(settings, out warning);
        }

        /// <summary>Checks carry dependencies and appearance targets for a detached proposal.</summary>
        /// <param name="configuration">Proposed sequence.</param>
        /// <param name="warning">Receives the first unavailable dependency.</param>
        /// <returns>True when the sequence can run on this object.</returns>
        public bool TryValidate(DegradationSettings configuration, out string warning)
        {
            warning = "Configure degradation settings.";
            if (configuration == null || !configuration.TryValidate(out warning))
                return false;
            if (configuration.Carry != DegradationCarryPolicy.FromSpawn && GetComponent<ObjectGrab>() == null)
                warning = "Carry-dependent degradation requires Grab on this object.";
            else if (configuration.Mode == DegradationMode.Impacts && configuration.Carry == DegradationCarryPolicy.OnlyWhileCarried
                && GetComponent<ObjectGrab>() is ObjectGrab carrying && !carrying.Settings.WorldCollisions)
                warning = "Enable Grab World Collisions for impact degradation while carried.";
            else if (configuration.Mode == DegradationMode.Impacts && GetComponent<Rigidbody>() == null)
                warning = "Impact degradation requires a Rigidbody on the interaction root.";
            else
                foreach (DegradationStep step in configuration.Steps)
                    if (!step.Appearance.CanBind(Item))
                    {
                        warning = "Rebind degradation appearance targets to this object's hierarchy.";
                        break;
                    }
            return warning.Length == 0;
        }

        #endregion
        #endregion
    }
}
