using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Routes empty-hand finalization through the same input arbitration and targeting as ordinary pickup.</summary>
    internal sealed class AssemblyPickup
    {
        #region State

        internal static readonly List<AssemblyPickup> Registered = new List<AssemblyPickup>();
        internal readonly ObjectAssemblyProduct Owner;
        internal readonly ObjectGrab Grab;
        private Collider[] colliders;
        internal bool Confirmed;

        #endregion
        #region Properties

        /// <summary>Local Grab binding, or the standalone product's explicitly selected finalization action.</summary>
        internal InputActionReference Action => Owner.Settings.ProlongedAssembly ? Owner.Settings.Charge.Action
            : Grab != null ? Grab.Action : Owner.Settings.FinalizeAction;
        /// <summary>Whether this assembled recipe still needs its empty-hand pickup confirmation.</summary>
        internal bool Waiting => Owner != null && (Owner.Settings.ProlongedAssembly || Owner.Settings.FinalizeOnGrab)
            && Owner.IsComplete && !Owner.IsFinalized && Owner.Item != null && !Owner.Item.IsReserved
            && Owner.Available(InteractionChannels.Assembly);

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Captures the product's optional carry feature without adding runtime components.</summary>
        /// <param name="owner">Product whose existing interactions will be unlocked.</param>
        internal AssemblyPickup(ObjectAssemblyProduct owner)
        {
            Owner = owner;
            Grab = owner.GetComponent<ObjectGrab>();
        }

        /// <summary>Clears only scene discovery when Play retains static data.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Registered.Clear();
        }

        /// <summary>Includes this product in the next shared input binding pass.</summary>
        internal void Register()
        {
            if (!Registered.Contains(this))
                Registered.Add(this);
            SingleInteractionRegistry.Invalidate();
        }

        /// <summary>Withdraws bindings when a product is suspended, pooled or destroyed.</summary>
        internal void Unregister()
        {
            Registered.Remove(this);
            SingleInteractionRegistry.Invalidate();
        }

        /// <summary>Refreshes geometry only when composition changes, not while awaiting input.</summary>
        internal void RefreshGeometry()
        {
            colliders = Owner.GetComponentsInChildren<Collider>();
        }

        #endregion
        #region Pickup

        /// <summary>Tests the product using its Grab targeting or an explicitly authored standalone target.</summary>
        /// <param name="observer">Player and camera providing empty-hand targeting.</param>
        /// <param name="targeting">Shared nonallocating targeting service.</param>
        /// <param name="score">Receives the candidate distance for normal pickup arbitration.</param>
        /// <returns>True when a pending finalization is reachable.</returns>
        internal bool Eligible(HoverObserver observer, InteractionTargeting targeting, out float score)
        {
            score = float.PositiveInfinity;
            if (!Waiting || observer.HeldObject != null)
                return false;
            if (!Owner.Settings.ProlongedAssembly && Grab != null)
            {
                GrabSettings settings = Grab.Settings;
                return targeting.Eligible(observer, Owner.transform, colliders, Grab.WorldTarget, settings.Distance,
                    settings.TargetMode, settings.TargetMode == HoverTargetMode.Cursor ? float.PositiveInfinity : settings.CenterRadius,
                    settings.ObstacleMask, settings.SolidHitOnly, out score);
            }
            TransferTargetSettings target = Owner.Settings.ProlongedAssembly ? Owner.Settings.Charge.Target : Owner.Settings.FinalizeTarget;
            return targeting.Eligible(observer, Owner.transform, colliders, Owner.transform.TransformPoint(target.Offset),
                target.Distance, target.Mode, target.Mode == HoverTargetMode.Cursor ? float.PositiveInfinity : target.CenterRadius,
                target.ObstacleMask, target.SolidHitOnly, out score);
        }

        /// <summary>Finalizes appearance and gates, then starts an available Grab with the same button press.</summary>
        /// <param name="observer">Observer whose carry slot must still be empty.</param>
        /// <param name="carried">Receives the grabbed product when carrying is supported and available.</param>
        /// <param name="allowGrab">Whether this completed input may also execute the product's compatible Grab.</param>
        /// <returns>True when this press finalized the product.</returns>
        internal bool Execute(HoverObserver observer, out ObjectGrab carried, bool allowGrab = true)
        {
            carried = null;
            if (!Waiting || observer.HeldObject != null)
                return false;
            Owner.ConfirmPickup();
            Owner.PublishPending();
            // Completion listeners may consume, disable or move the product before a same-input pickup.
            if (allowGrab && Grab != null && Grab.Begin(observer))
                carried = Grab;
            return true;
        }

        #endregion
        #endregion
    }
}
