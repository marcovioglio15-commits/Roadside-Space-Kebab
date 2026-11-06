using System.Collections.Generic;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;
using Guid = System.Guid;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Owns one uninterrupted assembly hold and suppresses its shared press through release.</summary>
    internal sealed class AssemblyChargeRun
    {
        #region State

        private readonly Dictionary<AssemblyPickup, InteractionButton> dismantleButtons = new Dictionary<AssemblyPickup, InteractionButton>();
        private AssemblyPickup active;
        private ObjectAssemblyProduct pendingImpact;
        private Camera impactView;
        private bool dismantling;
        private AssemblyChargeHud hud;
        private Guid blockedAction;
        private uint ingredientRevision;
        private float elapsed;
        private int releaseFrame = -1;

        #endregion

        #region Properties

        /// <summary>Physical button reserved by charging, including the release after automatic completion.</summary>
        internal InteractionButton BlockedButton { get; private set; }

        #endregion

        #region Methods

        #region Charging

        /// <summary>Resolves the independent dismantle buttons during the normal input rebuild.</summary>
        /// <param name="input">Current player's input router.</param>
        internal void BindDismantle(InteractionInputRouter input)
        {
            dismantleButtons.Clear();
            foreach (AssemblyPickup pickup in AssemblyPickup.Registered)
                if (pickup.Owner != null && pickup.Owner.Settings.Dismantle.Action != null
                    && input.Bind(pickup.Owner.Settings.Dismantle.Action) is InteractionButton button)
                    dismantleButtons.Add(pickup, button);
        }

        /// <summary>Starts after normal pickup arbitration selects a ready prolonged recipe.</summary>
        /// <param name="pickup">Recipe selected by reach, targeting and input priority.</param>
        /// <param name="button">Private player action currently held.</param>
        /// <param name="observer">Observer supplying the authored shared HUD.</param>
        /// <returns>True when a new hold owns this input.</returns>
        internal bool Begin(AssemblyPickup pickup, InteractionButton button, HoverObserver observer)
        {
            // A released tap or an already running attempt cannot start another charge.
            if (active != null || pendingImpact != null || !pickup.Waiting || button == null || !button.Held)
                return false;
            AssemblyChargeSettings settings = pickup.Owner.Settings.Charge;
            if (settings.ShowHud && (observer.AssemblyHud == null || !observer.AssemblyHud.Ready))
            {
                Debug.LogWarning("Create a valid Scene HUD in the gameplay scene before using prolonged assembly.", observer);
                return false;
            }
            Capture(pickup, button, false);
            hud = settings.ShowHud ? observer.AssemblyHud : null;
            if (hud != null)
                hud.Show(settings, observer.View);
            return true;
        }

        /// <summary>Advances a live hold before dialogue and single-action dispatch see the same input.</summary>
        /// <param name="observer">Current player, carry slot and view.</param>
        /// <param name="targeting">Shared nonallocating reach and sight checks.</param>
        /// <param name="input">Current private bindings, including any bindings rebuilt since charging began.</param>
        /// <param name="carried">Receives a product grabbed by the completion policy.</param>
        /// <returns>True when an assembly or camera impact owns this pass.</returns>
        internal bool Tick(HoverObserver observer, InteractionTargeting targeting, InteractionInputRouter input, out ObjectGrab carried)
        {
            carried = null;
            if (!ReferenceEquals(pendingImpact, null))
                return KeepImpact(observer);
            RefreshButton(input);
            if (active == null)
                return TryDismantle(observer, targeting, input);
            // Lost context, pause, changed ingredients or a missing action cancel instead of completing later.
            if (!input.Usable || Time.timeScale <= 0f || BlockedButton == null || !BlockedButton.Enabled
                || active.Owner == null || active.Owner.IngredientRevision != ingredientRevision || hud != null && !hud.Ready)
            {
                Cancel();
                return false;
            }
            Physics.SyncTransforms();
            if (!active.Eligible(observer, targeting, out _) || dismantling && !active.Owner.CanDismantle)
            {
                Cancel();
                return false;
            }
            if (dismantling)
            {
                if (!BlockedButton.Held)
                    Cancel();
                else if ((elapsed += Time.deltaTime) >= active.Owner.Settings.Dismantle.Duration)
                    return FinishDismantle(observer);
                return false;
            }
            AssemblyChargeSettings settings = active.Owner.Settings.Charge;
            if (BlockedButton.Held)
            {
                elapsed = Mathf.Min(settings.Duration, elapsed + Time.deltaTime);
                if (hud != null)
                    hud.SetProgress(elapsed / settings.Duration);
                if (elapsed < settings.Duration || settings.CompleteOnRelease)
                    return false;
            }
            else if (elapsed < settings.Duration)
            {
                Cancel();
                return false;
            }
            // Only the matching runtime action or physical control can share the successful completion.
            AssemblyPickup completed = active;
            bool grab = settings.GrabOnComplete && completed.Grab != null
                && BlockedButton.SharesPress(input.Find(completed.Grab.Action));
            Cancel();
            return completed.Execute(observer, out carried, grab);
        }

        #endregion

        #region Dismantling

        /// <summary>Selects the closest aimed ready recipe when a dismantle press arrives.</summary>
        /// <param name="observer">Player supplying aim and an empty carry slot.</param>
        /// <param name="targeting">Shared reach and obstruction queries.</param>
        /// <param name="input">Private input bindings.</param>
        /// <returns>True when dismantling began or completed.</returns>
        private bool TryDismantle(HoverObserver observer, InteractionTargeting targeting, InteractionInputRouter input)
        {
            if (BlockedButton != null || !input.Usable || Time.timeScale <= 0f || InteractionProjectileInput.Active)
                return false;
            AssemblyPickup selected = null;
            InteractionButton selectedButton = null;
            float distance = float.PositiveInfinity;
            foreach (KeyValuePair<AssemblyPickup, InteractionButton> candidate in dismantleButtons)
            {
                AssemblyPickup pickup = candidate.Key;
                InteractionButton button = candidate.Value;
                if (!button.Enabled || pickup.Owner == null || !pickup.Owner.CanDismantle
                    || (pickup.Owner.Settings.Dismantle.Hold ? !button.Held || !button.Pressed && !button.Pending : !button.Pending)
                    || !pickup.Eligible(observer, targeting, out float score) || score >= distance)
                    continue;
                selected = pickup;
                selectedButton = button;
                distance = score;
            }
            if (selected == null)
                return false;

            // The selected button owns the entire press even if a held dismantle is later cancelled.
            Capture(selected, selectedButton, true);
            return !selected.Owner.Settings.Dismantle.Hold && FinishDismantle(observer);
        }

        /// <summary>Reserves the dish until the optional camera reaches its downward strike.</summary>
        /// <param name="observer">Player whose camera aimed at the dish.</param>
        /// <returns>True when the dish was reserved or dismantled immediately.</returns>
        private bool FinishDismantle(HoverObserver observer)
        {
            ObjectAssemblyProduct product = active.Owner;
            Cancel();
            if (!product.CanDismantle)
                return false;

            // A camera outside Player Studio has no strike timeline to wait for.
            AssemblyDismantleSettings settings = product.Settings.Dismantle;
            Camera view = observer.View;
            if (!settings.CameraImpact || observer.Player == null
                || observer.Player.GetComponentInChildren<PlayerCameraRig>() is not PlayerCameraRig rig || !rig.Drives(view))
                return product.Dismantle();
            if (!product.Item.Acquire(product, InteractionChannels.Assembly))
                return false;

            pendingImpact = product;
            impactView = view;
            if (PlayerCameraImpact.Play(view, product.transform.position, settings.CameraDuration,
                settings.CameraArcHeight, settings.CameraClearance, ResolveImpact))
                return true;
            ResolveImpact(false);
            return product.Dismantle();
        }

        /// <summary>Retains the confirmed dish while the camera is moving toward it.</summary>
        /// <param name="observer">Current player and camera context.</param>
        /// <returns>True while the camera owns the interaction pass.</returns>
        private bool KeepImpact(HoverObserver observer)
        {
            if (pendingImpact != null && pendingImpact.Item.IsOwnedBy(pendingImpact)
                && pendingImpact.IngredientRevision == ingredientRevision && pendingImpact.isActiveAndEnabled
                && impactView != null && impactView.isActiveAndEnabled && observer.View == impactView)
                return true;

            // A lost camera or a changed recipe releases the dish without scattering it.
            StopImpact();
            return false;
        }

        /// <summary>Releases the reservation and scatters the dish on the applied impact frame.</summary>
        /// <param name="struck">Whether the camera actually reached the bottom pose.</param>
        private void ResolveImpact(bool struck)
        {
            ObjectAssemblyProduct product = pendingImpact;
            pendingImpact = null;
            impactView = null;
            if (product == null)
                return;

            bool owned = product.Item.IsOwnedBy(product);
            product.Item.Release(product);
            if (struck && owned && product.IngredientRevision == ingredientRevision)
                product.Dismantle();
        }

        /// <summary>Cancels this pending strike and releases any remaining dish reservation.</summary>
        private void StopImpact()
        {
            PlayerCameraImpact.Stop(impactView);
            if (!ReferenceEquals(pendingImpact, null))
                ResolveImpact(false);
        }

        #endregion

        #region Lifetime

        /// <summary>Captures the input and ingredient revision for either command.</summary>
        /// <param name="pickup">Ready product selected by targeting.</param>
        /// <param name="button">Private runtime button owning this attempt.</param>
        /// <param name="dismantle">Whether this attempt should release the ingredients.</param>
        private void Capture(AssemblyPickup pickup, InteractionButton button, bool dismantle)
        {
            active = pickup;
            dismantling = dismantle;
            ingredientRevision = pickup.Owner.IngredientRevision;
            BlockedButton = button;
            blockedAction = button.Id;
            releaseFrame = -1;
            elapsed = 0f;
        }

        /// <summary>Keeps shared release callbacks suppressed even after a charged product has been grabbed.</summary>
        /// <param name="input">Router resolving the same action after unrelated registry changes.</param>
        private void RefreshButton(InteractionInputRouter input)
        {
            // Rebinding another spawned object must not interrupt an otherwise continuous hold.
            if (blockedAction == Guid.Empty)
                return;
            BlockedButton = input.Find(blockedAction);
            if (BlockedButton == null || !BlockedButton.Enabled)
            {
                Reset();
                return;
            }
            if (active != null || BlockedButton.Held)
                return;
            if (releaseFrame < 0)
                releaseFrame = Time.frameCount;
            else if (Time.frameCount > releaseFrame)
            {
                BlockedButton = null;
                blockedAction = Guid.Empty;
            }
        }

        /// <summary>Hides progress while retaining suppression until this press is fully released.</summary>
        private void Cancel()
        {
            if (hud != null)
                hud.Hide();
            hud = null;
            active = null;
            elapsed = 0f;
        }

        /// <summary>Releases all charging and input ownership when the observer is disabled or replaced.</summary>
        internal void Reset()
        {
            StopImpact();
            Cancel();
            BlockedButton = null;
            blockedAction = Guid.Empty;
            releaseFrame = -1;
        }

        #endregion

        #endregion
    }
}
