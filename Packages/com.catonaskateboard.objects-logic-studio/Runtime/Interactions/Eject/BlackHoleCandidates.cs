using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Collects each physical root once and defers expensive eligibility checks to a bounded admission queue.</summary>
    internal sealed class BlackHoleCandidates
    {
        #region State

        /// <summary>Stores one spatial candidate without repeatedly querying its transform during sorting.</summary>
        private readonly struct Candidate : IComparable<Candidate>
        {
            internal readonly Rigidbody Body;
            private readonly float distance;
            private readonly EntityId identity;

            #region Methods

            /// <summary>Captures nearest-first ordering at the overlap boundary.</summary>
            /// <param name="body">Distinct physical root.</param>
            /// <param name="destination">Collector point used to prioritize the population cap.</param>
            internal Candidate(Rigidbody body, Vector3 destination)
            {
                Body = body;
                distance = (body.worldCenterOfMass - destination).sqrMagnitude;
                identity = body.GetEntityId();
            }

            /// <summary>Uses a stable body identifier when two roots have the same distance.</summary>
            /// <param name="other">Candidate being ordered.</param>
            /// <returns>Nearest-first order followed by instance identity.</returns>
            public int CompareTo(Candidate other)
            {
                int order = distance.CompareTo(other.distance);
                return order != 0 ? order : identity.CompareTo(other.identity);
            }

            #endregion
        }

        private readonly List<Candidate> pending = new List<Candidate>();
        private readonly HashSet<Rigidbody> unique = new HashSet<Rigidbody>();
        private Collider[] hits = new Collider[64];
        private int cursor;

        #endregion
        #region Properties

        /// <summary>Whether the snapshot still contains candidates to inspect.</summary>
        internal bool Pending => cursor < pending.Count;

        #endregion
        #region Methods
        #region Collection

        /// <summary>Collects a complete overlap, deduplicating compound colliders before any filter work.</summary>
        /// <param name="owner">Source defining the optional area.</param>
        /// <param name="point">Collector point for fallback radius and nearest-first priority.</param>
        /// <param name="run">Activation excluding completed and already owned bodies.</param>
        internal void Scan(ObjectEject owner, Vector3 point, BlackHoleRun run)
        {
            Clear();
            BlackHoleSettings settings = owner.Settings.BlackHole;
            int count;
            // A saturated overlap grows retained storage; truncation must not bias the cap towards arbitrary colliders.
            while (true)
            {
                count = settings.Area.Enabled ? settings.Area.Overlap(owner.transform, hits)
                    : Physics.OverlapSphereNonAlloc(point, settings.Radius, hits, ~0, QueryTriggerInteraction.Collide);
                if (count < hits.Length)
                    break;
                Array.Resize(ref hits, checked(hits.Length * 2));
            }
            for (int index = 0; index < count; index++)
            {
                Rigidbody body = hits[index].attachedRigidbody;
                hits[index] = null;
                if (body != null && unique.Add(body) && run.CanConsider(body))
                    pending.Add(new Candidate(body, point));
            }
            pending.Sort();
        }

        /// <summary>Consumes one entry without shifting the remaining queue.</summary>
        /// <returns>Next physical root, possibly destroyed since collection.</returns>
        internal Rigidbody Take()
        {
            return pending[cursor++].Body;
        }

        /// <summary>Releases scene references while retaining allocation capacity for subsequent activations.</summary>
        internal void Clear()
        {
            pending.Clear();
            unique.Clear();
            cursor = 0;
        }

        #endregion
        #endregion
    }
}
