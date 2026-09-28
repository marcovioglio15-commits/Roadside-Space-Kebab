using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Creates prefab draws from committed interaction completions, keeping source instances independent.</summary>
    [AddComponentMenu("Objects Logic Studio/Spawn Management")]
    public sealed class ObjectSpawnManager : ObjectExtendedInteraction
    {
        #region Serialized Fields

        [Header("Spawn Management")]
        [Tooltip("Source prefab conditions, per-instance cycles and randomized output prefabs.")]
        [SerializeField]
        private SpawnManagementSettings settings = new SpawnManagementSettings();

        [Tooltip("Inactive child authored by Apply so animated prefabs can be prepared before their first activation.")]
        [SerializeField]
        [HideInInspector]
        private Transform staging;

        #endregion

        #region State

        private readonly Dictionary<Transform, SpawnProgress> progress = new Dictionary<Transform, SpawnProgress>();
        private readonly List<SpawnProgress> pending = new List<SpawnProgress>();
        private readonly List<Transform> expired = new List<Transform>();
        private readonly List<GameObject> spawned = new List<GameObject>();
        private System.Random random;
        private bool[] used;
        private bool ready;
        private int animating;
        private int observedSession = -1;
        private static int session;

        #endregion

        #region Properties

        /// <summary>Arrival duration available to this rule's automatic start effect.</summary>
        internal override float VfxDuration => settings.Animation.Enabled ? settings.Animation.Duration : 0f;
        /// <summary>Whether any generated batch is still completing its arrival animation.</summary>
        internal override bool VfxRunning => animating > 0;

        /// <summary>Rule configuration shared by editor validation and runtime dispatch.</summary>
        public SpawnManagementSettings Settings => settings;
        /// <summary>Identifies the dedicated spawn category card.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.SpawnManagement;
        /// <summary>Number of queued source instances awaiting delay, availability or live capacity.</summary>
        public int PendingCount => pending.Count;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Invalidates transient progress on entering a new Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            // Pooling retains progress within a session, even with domain reload disabled.
            session++;
        }

        /// <summary>Restores completion subscriptions when Play keeps existing scene components alive.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            // One discovery pass replaces recurring scene searches.
            foreach (ObjectSpawnManager manager in FindObjectsByType<ObjectSpawnManager>())
                if (manager.isActiveAndEnabled)
                    manager.OnEnable();
        }

        /// <summary>Validates once and subscribes to committed interaction events.</summary>
        private void OnEnable()
        {
            // Disabled rules miss completions deliberately; their already queued work resumes on reactivation.
            Signaled -= Observe;
            if (observedSession != session)
            {
                observedSession = session;
                progress.Clear();
                pending.Clear();
                spawned.Clear();
                animating = 0;
                random = settings.FixedSeed ? new System.Random(settings.Seed) : new System.Random();
            }
            ready = TryValidate(out string warning);
            if (!ready)
            {
                Debug.LogWarning(warning, this);
                return;
            }
            used = new bool[settings.Choices.Length];
            Signaled += Observe;
        }

        /// <summary>Stops receiving events without resetting per-instance progress or live capacity.</summary>
        private void OnDisable()
        {
            // Re-enabling cannot duplicate an already consumed condition cycle.
            Signaled -= Observe;
        }

        #endregion

        #region Conditions

        /// <summary>Checks reusable settings and editor-prepared source identities.</summary>
        /// <param name="warning">Receives the first missing setting or source binding.</param>
        /// <returns>True when runtime completion events can be matched safely.</returns>
        public override bool TryValidate(out string warning)
        {
            // Only runtime requires prepared tokens; the editor writes them as part of Apply.
            warning = "Spawn Management settings are missing.";
            if (settings == null || !settings.TryValidate(out warning))
                return false;
            if (settings.Animation.Enabled && (staging == null || staging == transform || !staging.IsChildOf(transform) || staging.gameObject.activeSelf))
            {
                warning = "Apply this spawn rule in Objects Logic Studio to prepare its inactive animation staging child.";
                return false;
            }
            if (Application.isPlaying)
                foreach (SpawnCondition condition in settings.Conditions)
                    if (string.IsNullOrEmpty(condition.SourceId) || !condition.Source.TryResolveCompletionRoot(condition.SourceId, out _))
                    {
                        warning = "Reapply Spawn Management in Objects Logic Studio to prepare its source prefab bindings.";
                        return false;
                    }
            return true;
        }

        /// <summary>Records only matching completed interactions under their actual runtime source root.</summary>
        /// <param name="source">Interaction that committed a lifecycle event.</param>
        /// <param name="moment">Successful start or completion.</param>
        private void Observe(ObjectInteraction source, InteractionMoment moment)
        {
            // Starts, cancelled effects and completions while locked never satisfy this rule.
            if (!ready || moment != InteractionMoment.Completed || !Available(InteractionChannels.Spawn)
                || source == null)
                return;
            for (int index = 0; index < settings.Conditions.Length; index++)
            {
                if (!source.TryResolveCompletionRoot(settings.Conditions[index].SourceId, out Transform sourceRoot))
                    continue;
                Prune();
                if (!progress.TryGetValue(sourceRoot, out SpawnProgress state))
                {
                    state = new SpawnProgress(sourceRoot, settings.Conditions.Length);
                    progress.Add(sourceRoot, state);
                }
                if (!state.Pending && state.Counts[index] < settings.Conditions[index].Count)
                    state.Counts[index]++;
                if (!state.Ready(settings))
                    return;
                state.Cycles++;
                Array.Clear(state.Counts, 0, state.Counts.Length);
                if (random.NextDouble() >= settings.Chance)
                    return;
                state.Pending = true;
                state.Due = Time.time + Mathf.Lerp(settings.MinimumDelay, settings.MaximumDelay, (float)random.NextDouble());
                pending.Add(state);
                return;
            }
        }

        /// <summary>Forgets destroyed source roots after their already committed pending draw has finished.</summary>
        private void Prune()
        {
            // Cleanup is event-driven; idle rules do not enumerate source instances or allocate collections.
            expired.Clear();
            foreach (KeyValuePair<Transform, SpawnProgress> pair in progress)
                if (pair.Key == null && !pair.Value.Pending)
                    expired.Add(pair.Key);
            foreach (Transform source in expired)
                progress.Remove(source);
        }

        #endregion

        #region Output

        /// <summary>Processes ready draws outside the source interaction's callback stack.</summary>
        private void Update()
        {
            // Delaying execution prevents recursive spawn chains and respects pause and independent interaction locks.
            if (!ready || pending.Count == 0 || Time.timeScale <= 0f || !Available(InteractionChannels.Spawn))
                return;
            if (settings.LimitAlive)
                for (int index = spawned.Count - 1; index >= 0; index--)
                    if (spawned[index] == null)
                        spawned.RemoveAt(index);
            for (int index = pending.Count - 1; index >= 0; index--)
            {
                SpawnProgress state = pending[index];
                if (Time.time < state.Due || settings.LimitAlive && spawned.Count + settings.Draws > settings.MaximumAlive)
                    continue;
                pending.RemoveAt(index);
                Spawn(state);
                state.Pending = false;
                // Only one cycle is committed per frame; new lifecycle events cannot re-enter this draw.
                break;
            }
        }

        /// <summary>Creates the selected prefabs at this object's configured output without creating runtime UI.</summary>
        /// <param name="state">Source instance whose condition cycle is being committed.</param>
        private void Spawn(SpawnProgress state)
        {
            // Reuse a single selection buffer for every cycle and independently track sequence order per source.
            Array.Clear(used, 0, used.Length);
            SpawnBatch batch = null;
            if (settings.Animation.Enabled)
            {
                animating++;
                batch = new SpawnBatch(this, settings.Draws);
            }
            Signal(InteractionMoment.Started);
            for (int draw = 0; draw < settings.Draws; draw++)
            {
                int selected = SpawnDraw.Select(settings, random, state, used);
                if (selected < 0)
                    break;
                used[selected] = settings.WithoutReplacement;
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Sqrt((float)random.NextDouble()) * settings.Radius;
                Vector3 position = transform.TransformPoint(settings.Position)
                    + transform.rotation * new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Quaternion rotation = transform.rotation * Quaternion.Euler(settings.Rotation)
                    * Quaternion.Euler(0f, settings.RandomYaw ? (float)random.NextDouble() * 360f : 0f, 0f);
                GameObject created;
                if (batch != null)
                {
                    created = Instantiate(settings.Choices[selected].Prefab, staging, false);
                    created.SetActive(false);
                    created.transform.SetParent(null, false);
                    SceneManager.MoveGameObjectToScene(created, gameObject.scene);
                    SpawnArrival.Begin(created, position, rotation, settings.Animation, batch);
                }
                else
                {
                    created = Instantiate(settings.Choices[selected].Prefab, position, rotation);
                    SceneManager.MoveGameObjectToScene(created, gameObject.scene);
                }
                if (settings.LimitAlive)
                    spawned.Add(created);
            }
            if (batch == null)
                Signal(InteractionMoment.Completed);
        }

        /// <summary>Finishes one arrival batch without removing independent locks on the generated objects.</summary>
        /// <param name="completed">Whether every generated object reached its final pose.</param>
        internal void CompleteAnimation(bool completed)
        {
            // Aborted arrivals release their effect lifetime but do not publish a successful completion.
            animating--;
            if (completed)
                Signal(InteractionMoment.Completed);
        }

        #endregion

        #endregion
    }
}
