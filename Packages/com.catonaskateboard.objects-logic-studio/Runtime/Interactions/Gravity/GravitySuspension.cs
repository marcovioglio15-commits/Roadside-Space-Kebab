using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Composes overlapping generator ownership with carry and release gravity policies.</summary>
    internal static class GravitySuspension
    {
        #region State

        /// <summary>Retains the underlying gravity policy until the last generator releases a body.</summary>
        private sealed class Lease
        {
            internal bool Gravity;
            internal readonly HashSet<ObjectGravityGenerator> Owners = new HashSet<ObjectGravityGenerator>();
        }

        private static readonly Dictionary<Rigidbody, Lease> bodies = new Dictionary<Rigidbody, Lease>();

        #endregion
        #region Methods
        #region Ownership

        /// <summary>Disables gravity once while retaining independent ownership for overlapping pulses.</summary>
        /// <param name="body">Active dynamic body selected by a pulse.</param>
        /// <param name="owner">Generator acquiring the suspension.</param>
        internal static void Acquire(Rigidbody body, ObjectGravityGenerator owner)
        {
            if (!bodies.TryGetValue(body, out Lease lease))
            {
                lease = new Lease { Gravity = body.useGravity };
                bodies.Add(body, lease);
            }
            lease.Owners.Add(owner);
            body.useGravity = false;
        }

        /// <summary>Restores gravity only when no other generator still owns this body.</summary>
        /// <param name="body">Body retained by this pulse, possibly destroyed since acquisition.</param>
        /// <param name="owner">Generator relinquishing ownership.</param>
        internal static void Release(Rigidbody body, ObjectGravityGenerator owner)
        {
            if (ReferenceEquals(body, null) || !bodies.TryGetValue(body, out Lease lease))
                return;
            lease.Owners.Remove(owner);
            if (lease.Owners.Count > 0)
                return;
            if (body != null)
                body.useGravity = lease.Gravity;
            bodies.Remove(body);
        }

        /// <summary>Reads the underlying policy before carrying temporarily disables physics.</summary>
        /// <param name="body">Body whose carry snapshot is being captured.</param>
        /// <returns>Gravity policy to restore after all temporary owners release it.</returns>
        internal static bool Original(Rigidbody body)
        {
            return bodies.TryGetValue(body, out Lease lease) ? lease.Gravity : body.useGravity;
        }

        /// <summary>Updates the underlying release policy without overriding an active generator.</summary>
        /// <param name="body">Body restored by carrying or an explicit release profile.</param>
        /// <param name="gravity">Desired policy after suspension ends.</param>
        internal static void Set(Rigidbody body, bool gravity)
        {
            if (bodies.TryGetValue(body, out Lease lease))
            {
                lease.Gravity = gravity;
                body.useGravity = false;
            }
            else
                body.useGravity = gravity;
        }

        /// <summary>Restores retained bodies before a new Play session clears generator state.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            foreach (KeyValuePair<Rigidbody, Lease> pair in bodies)
                if (pair.Key != null)
                    pair.Key.useGravity = pair.Value.Gravity;
            bodies.Clear();
        }

        #endregion
        #endregion
    }
}
