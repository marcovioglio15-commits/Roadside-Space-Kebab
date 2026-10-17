using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Owns one uninterrupted assembly hold and suppresses its shared press through release.</summary>
    internal sealed class AssemblyChargeRun
    {
        #region State

        private AssemblyPickup active;
        private AssemblyChargeHud hud;
        private InputActionReference blockedAction;
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

        /// <summary>Starts after normal pickup arbitration selects a ready prolonged recipe.</summary>
        /// <param name="pickup">Recipe selected by reach, targeting and input priority.</param>
        /// <param name="button">Private player action currently held.</param>
        /// <param name="observer">Observer supplying the authored shared HUD.</param>
        /// <returns>True when a new hold owns this input.</returns>
        internal bool Begin(AssemblyPickup pickup, InteractionButton button, HoverObserver observer)
        {
            // A released tap or an already running attempt cannot start another charge.
            if (active != null || !pickup.Waiting || button == null || !button.Held)
                return false;
            AssemblyChargeSettings settings = pickup.Owner.Settings.Charge;
            if (settings.ShowHud && (observer.AssemblyHud == null || !observer.AssemblyHud.Ready))
            {
                Debug.LogWarning("Create a valid Scene HUD in the gameplay scene before using prolonged assembly.", observer);
                return false;
            }
            active = pickup;
            ingredientRevision = pickup.Owner.IngredientRevision;
            BlockedButton = button;
            blockedAction = pickup.Action;
            releaseFrame = -1;
            elapsed = 0f;
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
        /// <returns>True when this pass finalized one product.</returns>
        internal bool Tick(HoverObserver observer, InteractionTargeting targeting, InteractionInputRouter input, out ObjectGrab carried)
        {
            carried = null;
            RefreshButton(input);
            if (active == null)
                return false;
            // Lost context, pause, changed ingredients or a missing action cancel instead of completing later.
            if (!input.Usable || Time.timeScale <= 0f || BlockedButton == null || !BlockedButton.Enabled
                || active.Owner == null || active.Owner.IngredientRevision != ingredientRevision || hud != null && !hud.Ready)
            {
                Cancel();
                return false;
            }
            Physics.SyncTransforms();
            if (!active.Eligible(observer, targeting, out _))
            {
                Cancel();
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

        #region Lifetime

        /// <summary>Keeps shared release callbacks suppressed even after a charged product has been grabbed.</summary>
        /// <param name="input">Router resolving the same action after unrelated registry changes.</param>
        private void RefreshButton(InteractionInputRouter input)
        {
            // Rebinding another spawned object must not interrupt an otherwise continuous hold.
            if (blockedAction == null)
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
                blockedAction = null;
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
            Cancel();
            BlockedButton = null;
            blockedAction = null;
            releaseFrame = -1;
        }

        #endregion

        #endregion
    }
}
