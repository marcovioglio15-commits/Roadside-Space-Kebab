using System;
using CatOnASkateboard.MenuStudio;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Runs one persistent day sequence and matches completion to the exact generated component.</summary>
    internal sealed class SpawnFlowRun
    {
        #region State

        private enum Phase { Delay, Arrival, Waiting, Departure, Transition, Stopped }
        private static SpawnFlowRun active;
        private readonly ObjectSpawnManager owner;
        private readonly SpawnFlowPlan plan;
        private readonly Transform staging;
        private readonly MenuSceneTransition transition;
        private readonly System.Random random;
        private GameObject instance;
        private ObjectInteraction completion;
        private ObjectDialogue arrivalDialogue;
        private SpawnFlowStep step;
        private SpawnFlowMotion motion;
        private SuspendedItemState suspended;
        private Phase phase = Phase.Stopped;
        private int day;
        private int completed;
        private int total;
        private float remaining;
        private bool signaled;

        #endregion

        #region Properties

        /// <summary>One-based current day for runtime inspection.</summary>
        internal int Day => phase == Phase.Stopped ? 0 : day + 1;
        /// <summary>Number of completed steps in the current day.</summary>
        internal int Completed => completed;
        /// <summary>Spawn budget drawn once at the start of the current day.</summary>
        internal int Total => phase == Phase.Stopped ? 0 : total;

        #endregion

        #region Methods
        #region Lifecycle

        /// <summary>Clears persistent flow ownership before a new Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            // Scene reload settings do not carry yesterday's run into a new Play session.
            active = null;
        }

        /// <summary>Captures validated scene, animation and deterministic random configuration.</summary>
        /// <param name="owner">Standalone persistent Spawn Management root.</param>
        /// <param name="staging">Authored inactive child used before clone activation.</param>
        /// <param name="transition">Authored black overlay used between scenes.</param>
        internal SpawnFlowRun(ObjectSpawnManager owner, Transform staging, MenuSceneTransition transition)
        {
            // The plan remains an authored shared asset; runtime progress belongs only to this run.
            this.owner = owner;
            this.staging = staging;
            this.transition = transition;
            plan = owner.Settings.Flow;
            random = plan.FixedSeed ? new System.Random(plan.Seed) : new System.Random();
        }

        /// <summary>Claims the sole game-flow role and prepares the first day's scene.</summary>
        /// <returns>True when this manager owns the active run.</returns>
        internal bool Begin()
        {
            // Managers authored in later day scenes cannot restart or duplicate the persistent run.
            if (active != null && active != this)
                return false;
            active = this;
            Object.DontDestroyOnLoad(owner.gameObject);
            ObjectInteraction.Signaled += Observe;
            SceneManager.activeSceneChanged += SceneChanged;
            if (SceneManager.GetActiveScene().path == plan.Days[0].Scene)
                StartDay();
            else
                Load(plan.Days[0].Scene, StartDay);
            return true;
        }

        /// <summary>Withdraws callbacks and removes only the current generated visitor.</summary>
        internal void Stop()
        {
            // Disabling or destroying the manager cannot strand input, suspended visitors or an active fade.
            phase = Phase.Stopped;
            ObjectInteraction.Signaled -= Observe;
            SceneManager.activeSceneChanged -= SceneChanged;
            RemoveInstance();
            if (active != this)
                return;
            if (transition != null)
                transition.enabled = false;
            active = null;
        }

        /// <summary>Restarts the same day after an external scene reload or exits when returning to a menu.</summary>
        /// <param name="previous">Scene that was active before the change.</param>
        /// <param name="next">New active gameplay or menu scene.</param>
        private void SceneChanged(Scene previous, Scene next)
        {
            // Flow-owned loads already have a completion callback; additive pause scenes never replace the active scene.
            if (phase == Phase.Transition || phase == Phase.Stopped)
                return;
            RemoveInstance();
            if (next.path == plan.Days[day].Scene)
                StartDay();
            else
            {
                Stop();
                Object.Destroy(owner.gameObject);
            }
        }

        #endregion
        #region Sequence

        /// <summary>Draws a fresh daily budget and prepares the first visitor.</summary>
        private void StartDay()
        {
            // Random counts include both endpoints and are drawn once, including when the plan loops.
            SpawnFlowDay settings = plan.Days[day];
            completed = 0;
            total = settings.RandomCount ? random.Next(settings.Minimum, settings.Maximum + 1) : settings.Count;
            MenuSceneOverlay.Ensure(plan.PauseScene);
            PrepareStep();
            owner.Signal(InteractionMoment.Started);
        }

        /// <summary>Selects a template and schedules its pre-spawn delay.</summary>
        private void PrepareStep()
        {
            // Repeating the ordered templates makes the spawn count independent of list length.
            SpawnFlowDay settings = plan.Days[day];
            int index = completed % settings.Steps.Length;
            switch (settings.Selection)
            {
                case SpawnSelection.UniformRandom:
                    index = random.Next(settings.Steps.Length);
                    break;
                case SpawnSelection.WeightedRandom:
                    double weight = 0d;
                    foreach (SpawnFlowStep candidate in settings.Steps)
                        weight += candidate.Weight;
                    double draw = random.NextDouble() * weight;
                    for (int candidate = 0; candidate < settings.Steps.Length; candidate++)
                    {
                        draw -= settings.Steps[candidate].Weight;
                        if (draw < 0d)
                        {
                            index = candidate;
                            break;
                        }
                    }
                    break;
            }
            step = settings.Steps[index];
            remaining = step.Delay;
            signaled = false;
            phase = Phase.Delay;
        }

        /// <summary>Advances one visitor outside interaction callback stacks and honors gameplay pause.</summary>
        internal void Tick()
        {
            // Completed events are retained while the manager is temporarily locked.
            if (phase is Phase.Transition or Phase.Stopped || Time.timeScale <= 0f || !owner.Available(InteractionChannels.Spawn))
                return;
            if (phase is Phase.Arrival or Phase.Waiting or Phase.Departure && instance == null)
            {
                Fail("The current day-flow instance was destroyed before departure completed. The sequence has stopped.");
                return;
            }
            switch (phase)
            {
                case Phase.Delay:
                    remaining -= Time.deltaTime;
                    if (remaining <= 0f)
                        Spawn();
                    break;
                case Phase.Arrival:
                    if (motion.Tick(Time.deltaTime))
                    {
                        phase = Phase.Waiting;
                        suspended.Restore();
                        arrivalDialogue?.RequestArrival(step.NewCustomerSound);
                    }
                    break;
                case Phase.Waiting:
                    if (!signaled)
                        return;
                    suspended = new SuspendedItemState(instance, true);
                    suspended.Suspend();
                    motion.Begin(step.WalkOut);
                    suspended.Reveal();
                    phase = Phase.Departure;
                    break;
                case Phase.Departure:
                    if (motion.Tick(Time.deltaTime))
                        FinishStep();
                    break;
            }
        }

        /// <summary>Creates an inactive clone and resolves its exact completion component before activation.</summary>
        private void Spawn()
        {
            // Component order is captured before any OnEnable callback can alter the clone's hierarchy.
            instance = Object.Instantiate(step.Prefab, staging, false);
            instance.SetActive(false);
            instance.transform.SetParent(null, false);
            SceneManager.MoveGameObjectToScene(instance, SceneManager.GetActiveScene());
            instance.transform.SetPositionAndRotation(owner.transform.TransformPoint(step.Position), owner.transform.rotation * Quaternion.Euler(step.Rotation));
            ObjectInteraction[] original = step.Prefab.GetComponentsInChildren<ObjectInteraction>(true);
            ObjectInteraction[] generated = instance.GetComponentsInChildren<ObjectInteraction>(true);
            int index = Array.IndexOf(original, step.Completion);
            if (index < 0 || index >= generated.Length)
            {
                Fail("The selected completion interaction no longer belongs to the spawned prefab. Select it again in the day plan.");
                return;
            }
            completion = generated[index];
            // Match the independent arrival component before any activation callback can run.
            if (step.ArrivalDialogueEnabled)
            {
                index = Array.IndexOf(original, step.ArrivalDialogue);
                arrivalDialogue = index >= 0 && index < generated.Length ? generated[index] as ObjectDialogue : null;
                arrivalDialogue?.ConfigureArrival();
            }
            motion = new SpawnFlowMotion(instance.transform);
            if (step.WalkIn.Enabled)
            {
                suspended = new SuspendedItemState(instance, true);
                suspended.Suspend();
                motion.Begin(step.WalkIn);
                phase = Phase.Arrival;
            }
            else
                phase = Phase.Waiting;
            if (suspended != null)
                suspended.Reveal();
            else
                instance.SetActive(true);
            if (!step.WalkIn.Enabled)
                arrivalDialogue?.RequestArrival(step.NewCustomerSound);
        }

        /// <summary>Records only the selected component's successful completion on the current clone.</summary>
        /// <param name="source">Interaction that emitted the event.</param>
        /// <param name="moment">Successful lifecycle boundary.</param>
        private void Observe(ObjectInteraction source, InteractionMoment moment)
        {
            // Dialogue starts, page advances, interruptions and completions from other clones do not advance the day.
            if (phase == Phase.Waiting && source == completion && moment == InteractionMoment.Completed)
                signaled = true;
        }

        /// <summary>Despawns a departed visitor and advances the day only after its final step.</summary>
        private void FinishStep()
        {
            // Disable immediately so the next spawn cannot overlap a still-active outgoing visitor.
            RemoveInstance();
            completed++;
            if (completed < total)
            {
                PrepareStep();
                return;
            }
            phase = Phase.Transition;
            owner.Signal(InteractionMoment.Completed);
            if (owner == null || !owner.isActiveAndEnabled)
                return;
            day++;
            if (day < plan.Days.Length || plan.Loop)
            {
                day %= plan.Days.Length;
                Load(plan.Days[day].Scene, StartDay);
            }
            else
                Load(plan.MainMenu, FinishRun);
        }

        /// <summary>Releases the standalone persistent manager after the final menu fade.</summary>
        private void FinishRun()
        {
            // A later Play command starts a new manager from the gameplay scene's authored prefab.
            Stop();
            Object.Destroy(owner.gameObject);
        }

        /// <summary>Starts the authored black transition or reports a scene setup failure.</summary>
        /// <param name="scene">Destination path from the plan.</param>
        /// <param name="callback">Next stage after the reveal finishes.</param>
        private void Load(string scene, Action callback)
        {
            // All transition state is established before Unity can activate the destination scene.
            phase = Phase.Transition;
            if (!transition.Begin(scene, plan.FadeOut, plan.BlackDelay, plan.FadeIn, callback))
                Fail("Cannot start the day transition. Check the authored black overlay and enabled Build Settings scenes: " + scene);
        }

        /// <summary>Stops an invalid run once rather than silently skipping incomplete gameplay.</summary>
        /// <param name="warning">Concrete dependency or lifecycle failure.</param>
        private void Fail(string warning)
        {
            // No per-frame retry can instantiate duplicate visitors or flood the console.
            Debug.LogWarning(warning, owner);
            Stop();
        }

        /// <summary>Removes only this run's active visitor and clears its completion identity.</summary>
        private void RemoveInstance()
        {
            // Spawned products unrelated to the visitor remain in their own scene until normal scene unload.
            if (instance != null)
            {
                instance.SetActive(false);
                Object.Destroy(instance);
            }
            instance = null;
            completion = null;
            arrivalDialogue = null;
            suspended = null;
            motion = null;
        }

        #endregion
        #endregion
    }
}
