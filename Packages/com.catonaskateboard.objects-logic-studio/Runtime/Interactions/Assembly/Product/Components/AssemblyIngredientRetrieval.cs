using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Offers attached ingredients' suspended Grab bindings through normal pickup arbitration.</summary>
    internal sealed class AssemblyIngredientRetrieval
    {
        #region State

        private readonly Dictionary<ObjectGrab, ObjectAssemblyPart> parts = new Dictionary<ObjectGrab, ObjectAssemblyPart>();
        private readonly List<ObjectGrab> pending = new List<ObjectGrab>();
        private readonly List<Collider> surfaces = new List<Collider>();
        private readonly HashSet<Collider> ownSurfaces = new HashSet<Collider>();

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
                ObjectAssemblyPart part = pickup.Owner != null ? pickup.Owner.RetrievalPart : null;
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

        /// <summary>Distinguishes a suspended ingredient from ordinary active Grab components.</summary>
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
        /// <returns>True when the ingredient is reachable and still recoverable.</returns>
        internal bool Eligible(ObjectGrab grab, HoverObserver observer, InteractionTargeting targeting, out float score)
        {
            // Aim anywhere on the stack to retrieve the highest-priority original.
            score = float.PositiveInfinity;
            ObjectAssemblyPart part = parts[grab];
            if (part == null || !part.CanRetrieve)
                return false;
            return Aim(grab, observer, targeting, part.Product.transform, part.Product.Pickup.Colliders, null, out score);
        }

        /// <summary>Offers the newest eligible preview while the station is still choosing a recipe.</summary>
        /// <param name="grab">Active Grab on a provisionally placed ingredient.</param>
        /// <param name="observer">Player and view supplying the aim.</param>
        /// <param name="targeting">Shared geometry query service.</param>
        /// <param name="score">Receives the usual pickup distance score.</param>
        /// <returns>True when this preview is first to leave and the stack is reachable.</returns>
        internal bool EligiblePending(ObjectGrab grab, HoverObserver observer, InteractionTargeting targeting, out float score)
        {
            score = float.PositiveInfinity;
            AssemblyIngredientDock dock = grab.Dock;
            pending.Clear();
            AssemblyInteractionRegistry.CollectPending(dock.Station, pending);
            if (grab.Dock != dock)
                return false;

            // Sibling recipe previews share the same aim surface and withdrawal order.
            surfaces.Clear();
            ownSurfaces.Clear();
            foreach (ObjectGrab ingredient in pending)
            {
                if (ingredient.Dock is AssemblyIngredientDock other && other != dock && other.RetrievesBefore(dock))
                    return false;
                foreach (Collider shape in ingredient.Colliders)
                    if (ownSurfaces.Add(shape))
                        surfaces.Add(shape);
            }
            return Aim(grab, observer, targeting, dock.Station.transform, surfaces, ownSurfaces, out score);
        }

        /// <summary>Applies the selected original's Grab settings to a stack's visible surfaces.</summary>
        /// <param name="grab">Ingredient that would leave the stack.</param>
        /// <param name="observer">Current player and camera.</param>
        /// <param name="targeting">Shared reach and visibility checks.</param>
        /// <param name="root">Root excluded from its own sight check.</param>
        /// <param name="colliders">Surfaces available for aiming.</param>
        /// <param name="extra">Preview surfaces outside the root hierarchy, if any.</param>
        /// <param name="score">Receives the usual pickup distance score.</param>
        /// <returns>True when the player is aiming at a reachable stack.</returns>
        private static bool Aim(ObjectGrab grab, HoverObserver observer, InteractionTargeting targeting, Transform root,
            IReadOnlyList<Collider> colliders, ISet<Collider> extra, out float score)
        {
            GrabSettings settings = grab.Settings;
            return targeting.Eligible(observer, root, colliders, grab.WorldTarget, settings.Distance,
                settings.TargetMode, settings.TargetMode == HoverTargetMode.Cursor ? float.PositiveInfinity : settings.CenterRadius,
                settings.ObstacleMask, settings.SolidHitOnly, out score, extra: extra);
        }

        /// <summary>Restores a suspended ingredient immediately before ordinary Grab execution.</summary>
        /// <param name="grab">Selected pickup candidate.</param>
        /// <returns>True for ordinary pickups or successfully detached ingredients.</returns>
        internal bool Release(ObjectGrab grab)
        {
            return !parts.TryGetValue(grab, out ObjectAssemblyPart part) || part != null && part.ReleaseForPickup();
        }

        #endregion

        #endregion
    }
}
