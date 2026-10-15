using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Shares suspended-body collision response between walking and floating players.</summary>
    public static class PlayerSuspendedBodyPush
    {
        #region Fields

        private static readonly Dictionary<Object, PlayerFloatSettings> sources = new Dictionary<Object, PlayerFloatSettings>();

        #endregion
        #region Methods
        #region Ownership

        /// <summary>Clears pulse ownership before a new Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            sources.Clear();
        }

        /// <summary>Registers collision assistance independently of player gravity.</summary>
        /// <param name="owner">Active pulse implementing body membership.</param>
        /// <param name="settings">Existing mass and push response settings.</param>
        public static void Register(Object owner, PlayerFloatSettings settings)
        {
            if (owner is IPlayerSuspensionSource && settings != null && settings.PushSuspendedBodies && settings.ValidatePush(out _))
                sources[owner] = settings;
        }

        /// <summary>Releases only the ending pulse's collision assistance.</summary>
        /// <param name="owner">Pulse being restored or disabled.</param>
        public static void Release(Object owner)
        {
            sources.Remove(owner);
        }

        #endregion
        #region Collision

        /// <summary>Transfers closing speed once, using the strongest applicable mass advantage.</summary>
        /// <param name="body">Actual contacted body.</param>
        /// <param name="normal">Surface normal pointing toward the player.</param>
        /// <param name="velocity">Requested player velocity, reduced by the resolved resistance.</param>
        /// <returns>True when an active pulse handles this contact.</returns>
        internal static bool TryPush(Rigidbody body, Vector3 normal, ref Vector3 velocity)
        {
            // Authored floating scenery and fixed bodies never qualify merely because gravity is disabled.
            if (body == null || body.isKinematic
                || (body.constraints & RigidbodyConstraints.FreezePosition) == RigidbodyConstraints.FreezePosition)
                return false;
            PlayerFloatSettings policy = null;
            float share = -1f;
            foreach (KeyValuePair<Object, PlayerFloatSettings> source in sources)
                if (source.Key != null && ((IPlayerSuspensionSource)source.Key).IsBodySuspended(body))
                {
                    float candidate = (float)(source.Value.Mass / (source.Value.Mass + (double)body.mass * source.Value.SuspendedMassScale));
                    if (candidate > share)
                    {
                        share = candidate;
                        policy = source.Value;
                    }
                }
            if (policy == null)
                return false;
            float closing = -Vector3.Dot(velocity - body.linearVelocity, normal);
            if (closing > 0f)
            {
                // Compound contacts observe the new velocity rather than stacking identical impulses.
                body.linearVelocity -= normal * (closing * share * policy.SuspendedPushSpeed);
                velocity += normal * (closing * (1f - share));
                body.WakeUp();
            }
            return true;
        }

        #endregion
        #endregion
    }
}
