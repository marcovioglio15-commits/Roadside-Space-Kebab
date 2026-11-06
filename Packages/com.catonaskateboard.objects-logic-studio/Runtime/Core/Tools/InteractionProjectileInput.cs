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

        private static readonly Queue<Pulse> pending = new Queue<Pulse>(32);
        private static readonly HashSet<Transform> targets = new HashSet<Transform>();
        private static Pulse current;
        private static bool active;
        private static int serial;
        private static int sequence;
        private static int impactSequence;
        private static int confirmedImpact;

        #endregion

        #region Properties

        /// <summary>Whether interaction arbitration currently processes an actual bullet contact.</summary>
        internal static bool Active => active;
        /// <summary>Whether this pulse represents an exact pointer contact.</summary>
        internal static bool CursorUse => active && current.Hit.IsCursorUse && !current.IsArea;
        /// <summary>Whether the captured shot bypasses the target interaction's reach limit.</summary>
        internal static bool IgnoreRange => active && current.Hit.IgnoreInteractionRange;
        /// <summary>Whether a sweep or blast establishes contact independently of camera aim.</summary>
        internal static bool PhysicalImpact => active && (!current.Hit.IsCursorUse || current.IsArea);
        /// <summary>Whether this pulse came from a nearby collider in the impact area.</summary>
        internal static bool AreaContact => active && current.IsArea;
        /// <summary>Captured collider for the current target.</summary>
        internal static Collider TargetCollider => current.Collider;
        /// <summary>Point on the current target used for range and visibility checks.</summary>
        internal static Vector3 TargetPoint => current.Point;
        /// <summary>Unique pulse identity used by each private input router's consumption state.</summary>
        internal static int Serial => serial;
        /// <summary>Shared impact identity for all output bursts created by the same shot.</summary>
        internal static int Impact => active ? current.Impact : 0;
        /// <summary>Use Tool action captured by this contact, independent of the current loadout.</summary>
        internal static Guid ActionId => current.Hit.ActionId;
        /// <summary>Tool captured when the active projectile was fired.</summary>
        internal static PlayerTool Tool => current.Hit.Tool;
        /// <summary>Captured incoming velocity, available only during the current matching impact pulse.</summary>
        internal static Vector3 Velocity => active ? current.Hit.Velocity : Vector3.zero;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Subscribes after Player Studio clears static listeners for the new Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            // Domain and scene reload options share the same single subscription.
            pending.Clear();
            targets.Clear();
            active = false;
            serial = sequence = 0;
            impactSequence = confirmedImpact = 0;
            PlayerTools.ImpactContacts -= Enqueue;
            PlayerTools.ImpactContacts += Enqueue;
            PlayerTools.CarriedTargetShapes -= HoverObserver.CarriedTargetShapes;
            PlayerTools.CarriedTargetShapes += HoverObserver.CarriedTargetShapes;
        }

        /// <summary>Queues the direct target and each nearby interaction hierarchy once.</summary>
        /// <param name="hit">Captured weapon and primary contact.</param>
        /// <param name="area">Nearby contacts in order of distance from the impact.</param>
        private static void Enqueue(PlayerToolHit hit, IReadOnlyList<PlayerToolAreaContact> area)
        {
            // Misses and unbound scripted shots cannot invent an interaction action.
            if (!hit.TriggersInteractions || hit.Shooter == null || hit.Tool == null || hit.ActionId == Guid.Empty)
                return;
            int impact = ++impactSequence;
            Collider primary = hit.Contact.collider;
            Transform root = area != null && area.Count > 0 ? TargetRoot(primary) : null;
            if (primary != null)
                pending.Enqueue(new Pulse(hit, primary, hit.Contact.point, root, impact, false));
            if (area == null || area.Count == 0)
                return;
            // Keep distinct cards on different items separate even when their shapes share an impact.
            if (root != null)
                targets.Add(root);
            foreach (PlayerToolAreaContact contact in area)
                if (TargetRoot(contact.Collider) is Transform target && targets.Add(target))
                    pending.Enqueue(new Pulse(hit, contact.Collider, contact.Point, target, impact, true));
            targets.Clear();
        }

        /// <summary>Finds the interaction owner shared by an object's compound colliders.</summary>
        /// <param name="collider">Shape reached by the impact.</param>
        /// <returns>Nearest item or standalone interaction root.</returns>
        private static Transform TargetRoot(Collider collider)
        {
            if (collider == null)
                return null;
            return collider.GetComponentInParent<ObjectItem>() is ObjectItem item ? item.transform
                : collider.GetComponentInParent<ObjectInteraction>()?.transform;
        }

        /// <summary>Returns the next valid contact belonging to this observer's player.</summary>
        /// <param name="observer">Sole active scene observer.</param>
        /// <param name="hit">Receives the next matching shot.</param>
        /// <returns>True when a live collider and the correct shooter still exist.</returns>
        internal static bool TryTake(HoverObserver observer, out Pulse hit)
        {
            // Discard stale contacts after a respawn or target removal instead of retargeting them.
            while (pending.Count > 0)
            {
                hit = pending.Dequeue();
                if (Time.timeScale > 0f && hit.Hit.Shooter == HoverObserver.ActiveTools && hit.Hit.Shooter != null
                    && hit.Hit.Shooter.IsReady && hit.Hit.Shooter.isActiveAndEnabled && hit.Collider != null
                    && hit.Collider.enabled && hit.Collider.gameObject.activeInHierarchy && hit.Hit.Shooter.transform.IsChildOf(observer.Player))
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
            return active && action != null && action.id == ActionId;
        }

        /// <summary>Suppresses direct use presses only while the equipped weapon opts into impact interactions.</summary>
        /// <param name="action">Interaction action from the player's private input asset.</param>
        /// <returns>True when a physical button press must wait for a real bullet impact.</returns>
        internal static bool Blocks(InputAction action)
        {
            // Other actions on the same physical control retain their independent action identities.
            PlayerTools tools = HoverObserver.ActiveTools;
            return !active && action != null && tools != null && tools.ActiveTool is { UsesImpactInteractions: true }
                && tools.UseActionId == action.id;
        }

        /// <summary>Limits projectile selection to the exact struck hierarchy without considering range or camera aim.</summary>
        /// <param name="root">Interaction or target hierarchy being evaluated.</param>
        /// <returns>True during normal input, or when this hierarchy contains the struck collider.</returns>
        internal static bool Allows(Transform root)
        {
            return !active || root != null && current.Collider != null && current.Collider.transform.IsChildOf(root)
                && (current.Root == null || root.IsChildOf(current.Root));
        }

        /// <summary>Requires the input card to belong to the physical hit and accept the original fired tool.</summary>
        /// <param name="feature">Interaction being considered for this pulse.</param>
        /// <returns>True when normal input applies, or the hit owns and permits this feature.</returns>
        internal static bool Allows(ObjectInteraction feature)
        {
            return feature != null && feature.RequestToolAllowed && Allows(feature.transform);
        }

        /// <summary>Confirms impact feedback once when a matching interaction reaches a successful lifecycle boundary.</summary>
        /// <param name="feature">Interaction accepting the currently dispatched pointer contact.</param>
        internal static void Confirm(ObjectInteraction feature)
        {
            // Confirmation precedes callbacks that can replace the target or disable its collider.
            if (!active || !current.Hit.IsCursorUse || confirmedImpact == current.Impact || !Allows(feature.transform) || current.Hit.Shooter == null)
                return;
            confirmedImpact = current.Impact;
            current.Hit.Shooter.ConfirmCursorHit(current.Hit);
        }

        #endregion

        #endregion

        #region Scope

        /// <summary>Retains a distinct interaction target and the original shot for feedback and tool filtering.</summary>
        internal readonly struct Pulse
        {
            internal readonly PlayerToolHit Hit;
            internal readonly Collider Collider;
            internal readonly Vector3 Point;
            internal readonly Transform Root;
            internal readonly int Impact;
            internal readonly bool IsArea;

            #region Methods

            /// <summary>Captures one interaction request from a direct hit or its surrounding area.</summary>
            /// <param name="hit">Original confirmed shot.</param>
            /// <param name="collider">Collider belonging to this request.</param>
            /// <param name="point">World point on that collider.</param>
            /// <param name="root">Logical owner when the shot has several targets.</param>
            /// <param name="impact">Shared ID for this shot's impact feedback.</param>
            /// <param name="area">Whether the request came from the surrounding area.</param>
            internal Pulse(PlayerToolHit hit, Collider collider, Vector3 point, Transform root, int impact, bool area)
            {
                Hit = hit;
                Collider = collider;
                Point = point;
                Root = root;
                Impact = impact;
                IsArea = area;
            }

            #endregion
        }

        /// <summary>Restores the previous routing context after one contact, including nested interaction callbacks.</summary>
        internal readonly struct Scope : IDisposable
        {
            #region State

            private readonly Pulse previous;
            private readonly bool wasActive;
            private readonly int previousSerial;

            #endregion

            #region Methods

            /// <summary>Starts one synchronous pulse without mutating or enabling Input System actions.</summary>
            /// <param name="hit">Physical contact currently being dispatched.</param>
            internal Scope(Pulse hit)
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
