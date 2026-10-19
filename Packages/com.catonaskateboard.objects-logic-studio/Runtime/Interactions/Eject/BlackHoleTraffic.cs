using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Excludes imminent stream collisions using swept bounds instead of every possible participant pair.</summary>
    internal sealed class BlackHoleTraffic
    {
        #region State

        /// <summary>Caches compound bounds once per step and retains stream ownership after launch.</summary>
        private sealed class Participant
        {
            internal readonly BlackHoleBody Entry;
            internal readonly Collider[] Shapes;
            internal readonly uint Identity;
            internal Bounds Current;
            internal Bounds Swept;
            internal bool Valid;
            internal bool Stream;

            #region Methods

            /// <summary>Collects only solid colliders owned by this physical root.</summary>
            /// <param name="entry">Newly admitted stream participant.</param>
            /// <param name="identity">Unique sequential index within this activation.</param>
            internal Participant(BlackHoleBody entry, uint identity)
            {
                Entry = entry;
                Identity = identity;
                List<Collider> shapes = new List<Collider>();
                foreach (Collider shape in entry.Shapes)
                    if (shape != null && !shape.isTrigger && shape.attachedRigidbody == entry.Body)
                        shapes.Add(shape);
                Shapes = shapes.ToArray();
            }

            /// <summary>Predicts translational and rotational bounds for the upcoming physics step.</summary>
            /// <param name="deltaTime">Scaled physics step.</param>
            internal void Refresh(float deltaTime)
            {
                Valid = false;
                Stream = false;
                if (Entry.Body == null || !Entry.Body.gameObject.activeInHierarchy)
                    return;
                // Cache native bounds once per shape, regardless of how many pairs reference this body.
                foreach (Collider shape in Shapes)
                    if (shape != null && shape.enabled && shape.gameObject.activeInHierarchy)
                    {
                        if (Valid)
                            Current.Encapsulate(shape.bounds);
                        else
                            Current = shape.bounds;
                        Valid = true;
                    }
                Swept = Current;
                Stream = Valid && Entry.InStream;
                if (!Valid || Entry.Body.isKinematic)
                    return;
                Bounds end = Current;
                end.center += Entry.Body.linearVelocity * deltaTime;
                Swept.Encapsulate(end);
                float rotation = Entry.Body.angularVelocity.magnitude * deltaTime;
                float radius = rotation > 0f ? Current.extents.magnitude + (Current.center - Entry.Body.worldCenterOfMass).magnitude : 0f;
                Swept.Expand(0.02f + 2f * radius * Mathf.Min(1f, rotation));
            }

            #endregion
        }

        /// <summary>Retains a collision exclusion actually changed by this effect.</summary>
        private readonly struct Collision
        {
            internal readonly Collider First;
            internal readonly Collider Second;

            #region Methods

            /// <summary>Records a pair after verifying it was not already ignored.</summary>
            /// <param name="first">First participant's shape.</param>
            /// <param name="second">Second participant's shape.</param>
            internal Collision(Collider first, Collider second)
            {
                First = first;
                Second = second;
            }

            #endregion
        }

        /// <summary>Groups owned shape exclusions behind one cached body-level proximity test.</summary>
        private sealed class Pair
        {
            internal readonly Participant First;
            internal readonly Participant Second;
            internal readonly ulong Key;
            internal readonly List<Collision> Collisions = new List<Collision>();

            #region Methods

            /// <summary>Excludes solid compound pairs only once when two participants become neighbors.</summary>
            /// <param name="first">First nearby participant.</param>
            /// <param name="second">Second nearby participant.</param>
            /// <param name="key">Order-independent body identity.</param>
            internal Pair(Participant first, Participant second, ulong key)
            {
                First = first;
                Second = second;
                Key = key;
                foreach (Collider shape in first.Shapes)
                    foreach (Collider other in second.Shapes)
                        if (shape != null && other != null && !Physics.GetIgnoreCollision(shape, other))
                        {
                            Physics.IgnoreCollision(shape, other);
                            Collisions.Add(new Collision(shape, other));
                        }
            }

            /// <summary>Restores this effect's exclusions without altering preexisting ignored pairs.</summary>
            internal void Restore()
            {
                foreach (Collision collision in Collisions)
                    if (collision.First != null && collision.Second != null)
                        Physics.IgnoreCollision(collision.First, collision.Second, false);
            }

            #endregion
        }

        /// <summary>Orders swept bounds along the axis with the largest participant spread.</summary>
        private sealed class AxisComparer : IComparer<Participant>
        {
            internal int Axis;

            #region Methods

            /// <summary>Places invalid entries last so the broad phase can stop at its first inactive entry.</summary>
            /// <param name="first">First cached participant.</param>
            /// <param name="second">Second cached participant.</param>
            /// <returns>Ascending minimum-bound order for valid participants.</returns>
            public int Compare(Participant first, Participant second)
            {
                if (first.Valid != second.Valid)
                    return first.Valid ? -1 : 1;
                return first.Swept.min[Axis].CompareTo(second.Swept.min[Axis]);
            }

            #endregion
        }

        private readonly List<Participant> participants = new List<Participant>();
        private readonly List<Pair> pairs = new List<Pair>();
        private readonly HashSet<ulong> keys = new HashSet<ulong>();
        private readonly AxisComparer comparer = new AxisComparer();

        #endregion
        #region Properties

        /// <summary>Whether exclusions still need safe restoration after the attraction interval.</summary>
        internal bool Pending => pairs.Count > 0;

        #endregion
        #region Methods
        #region Ownership

        /// <summary>Registers geometry without creating exclusions against distant participants.</summary>
        /// <param name="entry">Newly acquired body.</param>
        internal void Add(BlackHoleBody entry)
        {
            participants.Add(new Participant(entry, (uint)participants.Count));
        }

        /// <summary>Restores owned pairs immediately on cancellation and discards all retained participant references.</summary>
        internal void Restore()
        {
            foreach (Pair pair in pairs)
                pair.Restore();
            pairs.Clear();
            keys.Clear();
            participants.Clear();
        }

        #endregion
        #region Proximity

        /// <summary>Refreshes bounds once, restores separated pairs and excludes only imminent stream contacts.</summary>
        /// <param name="deltaTime">Upcoming scaled physics step.</param>
        /// <param name="active">Whether attraction can still produce new stream contacts.</param>
        internal void Tick(float deltaTime, bool active)
        {
            if (participants.Count == 0)
                return;
            Bounds spread = default;
            bool initialized = false;
            foreach (Participant participant in participants)
            {
                participant.Refresh(deltaTime);
                if (!participant.Valid)
                    continue;
                if (initialized)
                    spread.Encapsulate(participant.Current.center);
                else
                    spread = new Bounds(participant.Current.center, Vector3.zero);
                initialized = true;
            }
            // Swap removal avoids shifting a large pair list during cleanup.
            for (int index = pairs.Count - 1; index >= 0; index--)
            {
                Pair pair = pairs[index];
                if (pair.First.Valid && pair.Second.Valid
                    && (pair.First.Current.Intersects(pair.Second.Current)
                        || active && pair.First.Stream && pair.Second.Stream
                        && (pair.First.Entry.Attracting || pair.Second.Entry.Attracting)
                        && pair.First.Swept.Intersects(pair.Second.Swept)))
                    continue;
                pair.Restore();
                keys.Remove(pair.Key);
                pairs[index] = pairs[pairs.Count - 1];
                pairs.RemoveAt(pairs.Count - 1);
            }
            if (!active)
                return;
            comparer.Axis = spread.size.x >= spread.size.y && spread.size.x >= spread.size.z ? 0
                : spread.size.y >= spread.size.z ? 1 : 2;
            participants.Sort(comparer);
            // Sweep-and-prune rejects distant bodies before any collider-pair or native collision calls.
            for (int index = 0; index < participants.Count; index++)
            {
                Participant first = participants[index];
                if (!first.Valid)
                    break;
                if (!first.Stream)
                    continue;
                float limit = first.Swept.max[comparer.Axis];
                for (int other = index + 1; other < participants.Count; other++)
                {
                    Participant second = participants[other];
                    if (!second.Valid || second.Swept.min[comparer.Axis] > limit)
                        break;
                    if (!second.Stream || !first.Entry.Attracting && !second.Entry.Attracting
                        || !first.Swept.Intersects(second.Swept))
                        continue;
                    ulong key = first.Identity < second.Identity ? (ulong)first.Identity << 32 | second.Identity
                        : (ulong)second.Identity << 32 | first.Identity;
                    if (keys.Add(key))
                        pairs.Add(new Pair(first, second, key));
                }
            }
        }

        #endregion
        #endregion
    }
}
