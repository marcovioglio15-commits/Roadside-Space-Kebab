using System.Collections.Generic;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Offers the sole ingredient's suspended Grab through normal pickup arbitration.</summary>
    internal sealed class AssemblyIngredientRetrieval
    {
        #region State

        private readonly Dictionary<ObjectGrab, ObjectAssemblyPart> parts = new Dictionary<ObjectGrab, ObjectAssemblyPart>();

        #endregion

        #region Methods

        #region Bindings

        /// <summary>Rebuilds only when product composition or input ownership changes.</summary>
        /// <param name="input">Observer's private action router.</param>
        /// <param name="bindings">Shared pickup candidates receiving suspended ingredient bindings.</param>
        internal void Bind(InteractionInputRouter input, Dictionary<ObjectInteraction, InteractionButton> bindings)
        {
            // Suspended ingredient components cannot register themselves, but keep their authored action.
            parts.Clear();
            foreach (AssemblyPickup pickup in AssemblyPickup.Registered)
            {
                ObjectAssemblyPart part = pickup.Owner != null ? pickup.Owner.RetrievablePart : null;
                if (part == null || part.Grab == null)
                    continue;
                InteractionButton button = input.Bind(part.Grab.Action);
                if (button == null || bindings.ContainsKey(part.Grab))
                    continue;
                parts.Add(part.Grab, part);
                bindings.Add(part.Grab, button);
            }
        }

        /// <summary>Drops retained ingredient references when the observer loses its context.</summary>
        internal void Clear()
        {
            parts.Clear();
        }

        #endregion

        #region Retrieval

        /// <summary>Distinguishes suspended first ingredients from ordinary active Grab components.</summary>
        /// <param name="grab">Candidate in the common pickup pass.</param>
        /// <returns>True when this binding represents ingredient retrieval.</returns>
        internal bool Contains(ObjectGrab grab)
        {
            return parts.ContainsKey(grab);
        }

        /// <summary>Tests original Grab targeting against the ingredient's current collision proxies.</summary>
        /// <param name="grab">Suspended original Grab.</param>
        /// <param name="observer">Current player and gameplay camera.</param>
        /// <param name="targeting">Shared geometry query service.</param>
        /// <param name="score">Receives the usual pickup distance score.</param>
        /// <returns>True when the sole ingredient is reachable and still recoverable.</returns>
        internal bool Eligible(ObjectGrab grab, HoverObserver observer, InteractionTargeting targeting, out float score)
        {
            // Ignore the product's own body while aiming at the ingredient's retained physical surface.
            score = float.PositiveInfinity;
            ObjectAssemblyPart part = parts[grab];
            if (part == null || !part.CanRetrieve)
                return false;
            GrabSettings settings = grab.Settings;
            return targeting.Eligible(observer, part.Product.transform, part.PickupColliders, grab.WorldTarget, settings.Distance,
                settings.TargetMode, settings.TargetMode == HoverTargetMode.Cursor ? float.PositiveInfinity : settings.CenterRadius,
                settings.ObstacleMask, settings.SolidHitOnly, out score);
        }

        /// <summary>Restores a suspended ingredient immediately before ordinary Grab execution.</summary>
        /// <param name="grab">Selected pickup candidate.</param>
        /// <returns>True for ordinary pickups or successfully detached first ingredients.</returns>
        internal bool Release(ObjectGrab grab)
        {
            return !parts.TryGetValue(grab, out ObjectAssemblyPart part) || part != null && part.ReleaseForPickup();
        }

        #endregion

        #endregion
    }
}
