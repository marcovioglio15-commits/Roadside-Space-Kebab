using System;
using System.Collections.Generic;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Routes captured weapon contacts through normal interaction arbitration as isolated button pulses.</summary>
    internal static class InteractionProjectileInput
    {
        #region State

        private static readonly Queue<PlayerToolHit> pending = new Queue<PlayerToolHit>(32);
        private static PlayerToolHit current;
        private static bool active;
        private static int serial;
        private static int sequence;

        #endregion

        #region Properties

        /// <summary>Whether interaction arbitration currently processes an actual bullet contact.</summary>
        internal static bool Active => active;
        /// <summary>Unique pulse identity used by each private input router's consumption state.</summary>
        internal static int Serial => serial;
        /// <summary>Tool captured when the active projectile was fired.</summary>
        internal static PlayerTool Tool => current.Tool;
        /// <summary>Captured incoming velocity, available only during the current matching impact pulse.</summary>
        internal static Vector3 Velocity => active ? current.Velocity : Vector3.zero;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Subscribes after Player Studio clears static listeners for the new Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            // Domain and scene reload options share the same single subscription.
            pending.Clear();
            active = false;
            serial = sequence = 0;
            PlayerTools.ProjectileHit -= Enqueue;
            PlayerTools.ProjectileHit += Enqueue;
        }

        /// <summary>Queues real matching contacts until the observer reaches its normal LateUpdate arbitration.</summary>
        /// <param name="hit">Captured weapon and physical contact.</param>
        private static void Enqueue(PlayerToolHit hit)
        {
            // Misses and unbound scripted shots cannot invent an interaction action.
            if (hit.TriggersInteractions && hit.Shooter != null && hit.Tool != null && hit.Contact.collider != null && hit.ActionId != Guid.Empty)
                pending.Enqueue(hit);
        }

        /// <summary>Returns the next valid contact belonging to this observer's player.</summary>
        /// <param name="observer">Sole active scene observer.</param>
        /// <param name="hit">Receives the next matching shot.</param>
        /// <returns>True when a live collider and the correct shooter still exist.</returns>
        internal static bool TryTake(HoverObserver observer, out PlayerToolHit hit)
        {
            // Discard stale contacts after a respawn or target removal instead of retargeting them.
            while (pending.Count > 0)
            {
                hit = pending.Dequeue();
                if (hit.Shooter != null && hit.Shooter == HoverObserver.ActiveTools && hit.Contact.collider != null
                    && hit.Shooter.transform.IsChildOf(observer.Player))
                    return true;
            }
            hit = default;
            return false;
        }

        #endregion

        #region Eligibility

        /// <summary>Matches the original Use Tool identity against a player's private action copy.</summary>
        /// <param name="action">Interaction action being evaluated.</param>
        /// <returns>True only for the currently dispatched projectile action.</returns>
        internal static bool Matches(InputAction action)
        {
            return active && action != null && action.id == current.ActionId;
        }

        /// <summary>Suppresses direct use presses only while the equipped weapon opts into impact interactions.</summary>
        /// <param name="action">Interaction action from the player's private input asset.</param>
        /// <returns>True when a physical button press must wait for a real bullet impact.</returns>
        internal static bool Blocks(InputAction action)
        {
            // Other actions on the same physical control retain their independent action identities.
            PlayerTools tools = HoverObserver.ActiveTools;
            return !active && action != null && tools != null
                && tools.ActiveTool is { IsWeapon: true, InteractOnBulletImpact: true } && tools.UseActionId == action.id;
        }

        /// <summary>Limits projectile selection to the exact struck hierarchy without considering range or camera aim.</summary>
        /// <param name="root">Interaction or target hierarchy being evaluated.</param>
        /// <returns>True during normal input, or when this hierarchy contains the struck collider.</returns>
        internal static bool Allows(Transform root)
        {
            return !active || root != null && current.Contact.collider != null && current.Contact.collider.transform.IsChildOf(root);
        }

        /// <summary>Requires the input card to belong to the physical hit and accept the original fired tool.</summary>
        /// <param name="feature">Interaction being considered for this pulse.</param>
        /// <returns>True when normal input applies, or the hit owns and permits this feature.</returns>
        internal static bool Allows(ObjectInteraction feature)
        {
            return feature != null && feature.RequestToolAllowed && Allows(feature.transform);
        }

        #endregion

        #endregion

        #region Scope

        /// <summary>Restores the previous routing context after one contact, including nested interaction callbacks.</summary>
        internal readonly struct Scope : IDisposable
        {
            #region State

            private readonly PlayerToolHit previous;
            private readonly bool wasActive;
            private readonly int previousSerial;

            #endregion

            #region Methods

            /// <summary>Starts one synchronous pulse without mutating or enabling Input System actions.</summary>
            /// <param name="hit">Physical contact currently being dispatched.</param>
            internal Scope(PlayerToolHit hit)
            {
                previous = current;
                wasActive = active;
                previousSerial = serial;
                current = hit;
                active = true;
                serial = ++sequence;
            }

            /// <summary>Restores outer routing even if a target disables itself or throws from a callback.</summary>
            public void Dispose()
            {
                current = previous;
                active = wasActive;
                serial = previousSerial;
            }

            #endregion
        }

        #endregion
    }
}
