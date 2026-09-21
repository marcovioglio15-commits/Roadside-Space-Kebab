using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Restores only collision pairs changed by a pickup, after safe separation on release.</summary>
    internal sealed class CarryCollisions
    {
        #region State

        private readonly List<Pair> pairs = new List<Pair>();

        /// <summary>Records a previously enabled object/player collision pair.</summary>
        private readonly struct Pair
        {
            internal readonly Collider Object;
            internal readonly Collider Player;

            /// <summary>Keeps both colliders for exact restoration without hierarchy searches.</summary>
            /// <param name="owned">Collider attached to the carried body.</param>
            /// <param name="player">Player collider to temporarily ignore.</param>
            internal Pair(Collider owned, Collider player)
            {
                // Pairs already ignored by another system are never captured.
                Object = owned;
                Player = player;
            }
        }

        #endregion

        #region Properties

        /// <summary>Whether collision pairs still wait for separation after release.</summary>
        internal bool Pending => pairs.Count > 0;

        #endregion

        #region Methods

        #region Collision Ownership

        /// <summary>Ignores the player's colliders without changing layer collision rules.</summary>
        /// <param name="owned">Cached colliders attached to the grabbed body.</param>
        /// <param name="player">Tagged player root resolved by the observer.</param>
        internal void Ignore(Collider[] owned, Transform player)
        {
            // Pickup is an allocation boundary; ordinary carry frames reuse the captured pairs.
            Restore(true);
            Collider[] players = player.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in owned)
                foreach (Collider other in players)
                    if (collider != null && other != null && collider != other && !Physics.GetIgnoreCollision(collider, other))
                    {
                        pairs.Add(new Pair(collider, other));
                        Physics.IgnoreCollision(collider, other);
                    }
        }

        /// <summary>Reasserts owned pairs if pooling re-enabled a collider during carrying.</summary>
        internal void Maintain()
        {
            // Unity may clear ignored pairs when a collider is deactivated.
            foreach (Pair pair in pairs)
                if (pair.Object != null && pair.Player != null && !Physics.GetIgnoreCollision(pair.Object, pair.Player))
                    Physics.IgnoreCollision(pair.Object, pair.Player);
        }

        /// <summary>Restores captured pairs immediately or once their bounds no longer overlap.</summary>
        /// <param name="immediate">Restore during teardown even if the objects still overlap.</param>
        internal void Restore(bool immediate)
        {
            // Conservative bounds separation also works for CharacterController and arbitrary player shapes.
            for (int index = pairs.Count - 1; index >= 0; index--)
            {
                Pair pair = pairs[index];
                if (!immediate && pair.Object != null && pair.Player != null && pair.Object.enabled && pair.Player.enabled
                    && pair.Object.gameObject.activeInHierarchy && pair.Player.gameObject.activeInHierarchy
                    && pair.Object.bounds.Intersects(pair.Player.bounds))
                    continue;
                if (pair.Object != null && pair.Player != null)
                    Physics.IgnoreCollision(pair.Object, pair.Player, false);
                pairs.RemoveAt(index);
            }
        }

        #endregion

        #endregion
    }
}
