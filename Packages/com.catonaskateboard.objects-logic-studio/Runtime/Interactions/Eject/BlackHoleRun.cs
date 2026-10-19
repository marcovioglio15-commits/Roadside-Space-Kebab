using System.Collections.Generic;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Owns a bounded attraction interval and each participant's one-way handoff to its launch impulse.</summary>
    internal sealed class BlackHoleRun
    {
        #region State

        /// <summary>Prioritizes cached remaining distances without querying physics during sorting.</summary>
        private sealed class DistanceComparer : IComparer<BlackHoleBody>
        {
            #region Methods

            /// <summary>Orders nearer participants first and retains stable ties.</summary>
            /// <param name="first">First participant.</param>
            /// <param name="second">Second participant.</param>
            /// <returns>Ascending remaining distance, then body identity.</returns>
            public int Compare(BlackHoleBody first, BlackHoleBody second)
            {
                int distance = first.Distance.CompareTo(second.Distance);
                return distance != 0 ? distance : first.Identity.CompareTo(second.Identity);
            }

            #endregion
        }

        private static readonly Dictionary<Rigidbody, BlackHoleRun> ownership = new Dictionary<Rigidbody, BlackHoleRun>();
        private readonly List<BlackHoleBody> bodies = new List<BlackHoleBody>();
        private readonly BlackHoleAir air = new BlackHoleAir();
        private readonly BlackHoleTraffic traffic = new BlackHoleTraffic();
        private readonly BlackHoleCandidates candidates = new BlackHoleCandidates();
        private readonly DistanceComparer priority = new DistanceComparer();
        private readonly Dictionary<Rigidbody, BlackHoleBody> participants = new Dictionary<Rigidbody, BlackHoleBody>();
        private readonly List<ObjectInteraction> interactions = new List<ObjectInteraction>();
        private bool scanned;
        private ObjectEject owner;
        private Collider destination;
        private PhysicsMaterial surface;
        private float remaining;
        private float nextQuery;
        /// <summary>Whether the timed attraction interval is still open.</summary>
        internal bool Active { get; private set; }
        /// <summary>Whether physics or post-launch collision restoration still needs a tick.</summary>
        internal bool Pending => Active || bodies.Count > 0 || traffic.Pending;

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Starts an effect even when its area is initially empty, allowing later entrants.</summary>
        /// <param name="source">Validated black hole interaction.</param>
        internal void Begin(ObjectEject source)
        {
            Cancel();
            owner = source;
            destination = source.Settings.BlackHole.Resolve(source.transform);
            remaining = source.Settings.BlackHole.Duration;
            nextQuery = 0f;
            scanned = false;
            Active = true;
            if (source.Settings.BlackHole.Obstacles.Enabled)
                surface = new PhysicsMaterial("Black Hole Sliding Surface")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    dynamicFriction = source.Settings.BlackHole.Obstacles.Friction,
                    staticFriction = source.Settings.BlackHole.Obstacles.Friction,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounciness = 0f,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            Vector3 point = source.Settings.BlackHole.Destination(destination);
            PlayerCameraRumble.Begin(source, point, source.Settings.BlackHole.Rumble, remaining);
            air.Begin(source.transform, source.Settings.BlackHole);
        }

        /// <summary>Releases active bodies and pending collision pairs on cancellation or pooling.</summary>
        internal void Cancel()
        {
            foreach (BlackHoleBody body in bodies)
            {
                if (!body.Launched)
                    body.Release(false);
                body.RestoreCollisions(true);
                ownership.Remove(body.Body);
            }
            bodies.Clear();
            traffic.Restore();
            participants.Clear();
            candidates.Clear();
            interactions.Clear();
            if (surface != null)
                UnityEngine.Object.Destroy(surface);
            surface = null;
            air.Stop();
            if (owner != null)
                PlayerCameraRumble.Cancel(owner);
            owner = null;
            destination = null;
            Active = false;
        }

        /// <summary>Clears retained ownership before a new Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            // Source components restore their bodies at the following scene initialization boundary.
            ownership.Clear();
        }

        /// <summary>Allows gravity generators to retain suspension without competing with attraction steering.</summary>
        /// <param name="body">Physical root being driven by a generator.</param>
        /// <returns>True while a black hole owns this body's velocity.</returns>
        internal static bool Contains(Rigidbody body)
        {
            return body != null && ownership.ContainsKey(body);
        }

        /// <summary>Hands an attracted body back before a player grab captures its normal physics policy.</summary>
        /// <param name="body">Body selected for an explicit player pickup.</param>
        internal static void ReleaseForGrab(Rigidbody body)
        {
            // An explicit pickup also prevents this activation from taking the same object back.
            if (body == null || !ownership.TryGetValue(body, out BlackHoleRun run))
                return;
            for (int index = run.bodies.Count - 1; index >= 0; index--)
                if (run.bodies[index].Body == body && !run.bodies[index].Launched)
                {
                    run.bodies[index].Release(false);
                    run.bodies.RemoveAt(index);
                    break;
                }
            ownership.Remove(body);
        }

        #endregion
        #region Simulation

        /// <summary>Queries new entrants at the configured interval and steers cached participants each physics tick.</summary>
        /// <param name="deltaTime">Positive scaled physics step.</param>
        /// <returns>True on the single normal completion boundary.</returns>
        internal bool Tick(float deltaTime)
        {
            if (owner == null || Active && (destination == null || !destination.enabled || !destination.gameObject.activeInHierarchy))
            {
                Cancel();
                return false;
            }
            BlackHoleSettings settings = owner.Settings.BlackHole;
            bool completed = Active && remaining <= deltaTime;
            if (Active)
            {
                remaining -= deltaTime;
                nextQuery -= deltaTime;
                if (!completed && participants.Count < settings.Performance.MaximumParticipants)
                {
                    if (!candidates.Pending && nextQuery <= 0f && (!scanned || !settings.Area.Enabled || settings.Area.TrackEntries))
                    {
                        candidates.Scan(owner, settings.Destination(destination), this);
                        nextQuery = settings.Area.Enabled ? settings.Area.QueryInterval : settings.QueryInterval;
                        scanned = true;
                    }
                    Admit();
                }
            }
            Vector3 point = Active ? owner.Settings.BlackHole.Destination(destination) : Vector3.zero;
            Vector3 axis = Active ? destination.transform.TransformDirection(owner.Settings.BlackHole.SpiralAxis).normalized : Vector3.up;
            int searches = settings.Performance.RouteSearchesPerStep;
            if (Active)
                Prioritize(point, settings.GuideEntrance
                    ? point + destination.transform.TransformVector(settings.EntranceOffset) : point, settings.GuideEntrance);
            // Nearer participants receive route searches first; removal preserves the remaining priority order.
            for (int index = 0; index < bodies.Count; index++)
            {
                BlackHoleBody entry = bodies[index];
                if (entry.Launched)
                {
                    if (entry.RestoreCollisions(false))
                        bodies.RemoveAt(index--);
                    continue;
                }
                if (entry.Body == null || !entry.Body.gameObject.activeInHierarchy || entry.Body.isKinematic
                    || entry.Item != null && (entry.Item.IsReserved || entry.Item.IsCarried || entry.Item.IsConsumed) || completed
                    || settings.Area.Enabled && settings.Area.RestoreOutside && !settings.Area.Contains(owner.transform, entry.Body.worldCenterOfMass))
                {
                    entry.Release(false);
                    ownership.Remove(entry.Body);
                    bodies.RemoveAt(index--);
                    continue;
                }
                if (!entry.Advance(point, axis, owner.Settings.BlackHole, deltaTime, ref searches))
                    continue;
                ownership.Remove(entry.Body);
                entry.Launch();
            }
            if (Active)
                air.Tick(point, owner.Settings.BlackHole.AirSpeed);
            if (completed)
            {
                Active = false;
                air.Stop();
            }
            traffic.Tick(deltaTime, Active);
            return completed;
        }

        /// <summary>Spreads filter checks and physical acquisition across a bounded number of candidates per step.</summary>
        private void Admit()
        {
            BlackHolePerformanceSettings budget = owner.Settings.BlackHole.Performance;
            int admitted = 0;
            // The cap counts distinct bodies for the entire activation, including those already launched.
            for (int checkedCount = 0; candidates.Pending && checkedCount < budget.CandidatesPerStep
                && admitted < budget.AdmissionsPerStep && participants.Count < budget.MaximumParticipants; checkedCount++)
                if (Include(candidates.Take()))
                    admitted++;
            if (participants.Count >= budget.MaximumParticipants)
                candidates.Clear();
        }

        /// <summary>Gives the closest remaining approach first access to the shared path-search budget.</summary>
        /// <param name="point">Collector position for this step.</param>
        /// <param name="approach">Entrance position, or the collector without guidance.</param>
        /// <param name="guided">Whether participants first approach the duct mouth.</param>
        private void Prioritize(Vector3 point, Vector3 approach, bool guided)
        {
            // Inactive cleanup entries sort last; active bodies cache their progress once before steering.
            foreach (BlackHoleBody body in bodies)
            {
                body.Distance = float.PositiveInfinity;
                if (body.Attracting && body.Body != null && body.Body.gameObject.activeInHierarchy && !body.Body.isKinematic)
                    body.Refresh(point, approach, guided);
            }
            bodies.Sort(priority);
        }

        /// <summary>Rejects already completed or currently owned bodies before sorting an overlap snapshot.</summary>
        /// <param name="body">Potential physical root.</param>
        /// <returns>True when detailed admission checks may still accept this root.</returns>
        internal bool CanConsider(Rigidbody body)
        {
            return body != null && !ownership.ContainsKey(body) && !participants.ContainsKey(body)
                && !body.gameObject.isStatic && body.gameObject.activeInHierarchy;
        }

        /// <summary>Lets route sweeps ignore stream peers before their imminent collision pairs are excluded.</summary>
        /// <param name="body">Potential obstacle body.</param>
        /// <returns>True for an active or launched participant of this same stream.</returns>
        internal bool IsStreamBody(Rigidbody body)
        {
            return owner.Settings.BlackHole.IgnoreParticipantCollisions && body != null
                && participants.TryGetValue(body, out BlackHoleBody entry) && entry.InStream;
        }

        /// <summary>Hands a free eligible physical body to the attraction without interrupting object transactions.</summary>
        /// <param name="body">Rigidbody discovered in the attraction area.</param>
        /// <returns>True when this step acquired the body.</returns>
        private bool Include(Rigidbody body)
        {
            BlackHoleSettings settings = owner.Settings.BlackHole;
            if (!CanConsider(body) || body.transform.IsChildOf(owner.transform) || owner.transform.IsChildOf(body.transform)
                || !settings.ReleaseKinematic && body.isKinematic || !settings.Area.Contains(owner.transform, body.worldCenterOfMass)
                || !settings.Filter.Matches(body, owner.transform, interactions))
                return false;
            ObjectItem item = body.GetComponentInParent<ObjectItem>();
            if (item != null && (!item.isActiveAndEnabled || item.IsReserved || item.IsConsumed || item.IsBlocked(InteractionChannels.Eject)))
                return false;
            if (body.TryGetComponent(out ObjectGrab grab))
            {
                if (grab.IsHeld && !settings.ReleaseKinematic)
                    return false;
                grab.Cancel();
                grab.Dock?.Release();
                grab.RequiresAssemblyPlacement = true;
            }
            if (body.TryGetComponent(out ObjectAssemblyProduct product))
                product.ReleaseForSuspension();
            ownership.Add(body, this);
            BlackHoleBody entry = new BlackHoleBody(body, item, owner, destination, surface, this);
            participants[body] = entry;
            if (settings.IgnoreParticipantCollisions)
                traffic.Add(entry);
            bodies.Add(entry);
            return true;
        }

        #endregion
        #endregion
    }
}
