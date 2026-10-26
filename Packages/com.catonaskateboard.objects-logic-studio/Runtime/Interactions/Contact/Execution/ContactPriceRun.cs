using System;
using CatOnASkateboard.AudioStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Prices one consumption receipt before settling its independent deferred display batch.</summary>
    internal sealed class ContactPriceRun
    {
        #region State

        private ContactPriceSettings settings;
        private ObjectInteraction[] sources = Array.Empty<ObjectInteraction>();
        private bool[] completed = Array.Empty<bool>();
        private decimal pending;
        private decimal receipt;
        private decimal basePrice;
        private int remaining;
        private bool collecting;
        private bool released;
        private int generation = -1;
        private bool closed;
        private bool inReceipt;
        private Transform emitter;
        private Vector3 position;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Binds completion sources once per spawn, preserving progress through temporary deactivation.</summary>
        /// <param name="configuration">Validated collection settings.</param>
        /// <param name="owner">Object containing every selected completion source.</param>
        /// <param name="newSpawn">Whether previous earnings and completion state belong to another lifetime.</param>
        internal void Configure(ContactPriceSettings configuration, GameObject owner, bool newSpawn)
        {
            ObjectInteraction.Signaled -= Observe;
            if (generation != CollectedPrice.Generation)
            {
                generation = CollectedPrice.Generation;
                pending = receipt = 0m;
                inReceipt = false;
                newSpawn = true;
            }
            if (newSpawn)
            {
                EndLifetime();
                sources = Array.Empty<ObjectInteraction>();
                released = closed = collecting = inReceipt = false;
                receipt = basePrice = 0m;
                remaining = 0;
            }
            settings = configuration;
            emitter = owner.transform;
            position = emitter.position;
            if (closed || settings is not { CollectPrice: true, DeferDisplay: true })
                return;
            if (sources.Length != settings.CompletionSources.Length)
            {
                sources = new ObjectInteraction[settings.CompletionSources.Length];
                completed = new bool[sources.Length];
            }
            // Subscription remains through temporary deactivation so an already running local source can finish.
            for (int index = 0; index < sources.Length; index++)
            {
                ObjectInteraction source = settings.CompletionSources[index].Resolve(owner);
                if (sources[index] != source)
                    completed[index] = false;
                sources[index] = source;
            }
            ObjectInteraction.Signaled += Observe;
        }

        /// <summary>Ends one spawn once, applying its unfinished-price policy after any current receipt finishes publishing.</summary>
        internal void EndLifetime()
        {
            // Keep the current receipt's completion callbacks until Commit; later events cannot reopen the visit.
            closed = true;
            if (!inReceipt)
            {
                ObjectInteraction.Signaled -= Observe;
                Settle();
            }
        }

        #endregion

        #region Receipts

        /// <summary>Captures a proposed consumption before deactivation callbacks can end its lifetime.</summary>
        /// <param name="victim">Physical item about to be consumed, retaining any recipe configuration.</param>
        /// <param name="units">Logical units proposed by this transaction, confirmed before publication.</param>
        internal void Begin(ObjectItem victim, int units)
        {
            // No earnings enter the ledger until the actual consumption result has been confirmed.
            receipt = basePrice = 0m;
            inReceipt = true;
            remaining = units;
            collecting = settings is { CollectPrice: true } && units > 0 && victim != null;
            if (!collecting)
                return;
            if (victim.TryGetComponent(out ObjectAssemblyProduct product)
                && product.Settings is { PriceEnabled: true } recipe && CollectedPrice.Valid(recipe.Price))
                basePrice = CollectedPrice.Amount(recipe.Price);
            receipt = basePrice * units;
        }

        /// <summary>Retains prepared pricing only for a real consumption and opens an empty context for other completions.</summary>
        /// <param name="victim">Confirmed consumed item, or null when no item was consumed.</param>
        /// <param name="units">Successfully consumed units; zero cancels the proposed price.</param>
        internal void Confirm(ObjectItem victim, int units)
        {
            // Non-consuming rules still need to finish nested completion callbacks before settling a pending batch.
            if (!inReceipt)
                Begin(victim, units);
            if (units <= 0)
            {
                receipt = 0m;
                collecting = false;
                remaining = 0;
            }
        }

        /// <summary>Replaces one unit's base price only when that unit fulfils an open matching order.</summary>
        /// <param name="order">The same catalog entry selected by normal ticket fulfilment.</param>
        internal void Override(OrderEntry order)
        {
            // Even a ticket without an override consumes its one pricing slot.
            if (!collecting || remaining <= 0)
                return;
            remaining--;
            if (order.OverridePrice && CollectedPrice.Valid(order.Price))
                receipt += CollectedPrice.Amount(order.Price) - basePrice;
        }

        /// <summary>Commits after all order listeners have resolved their overrides, including nested completion events.</summary>
        internal void Commit()
        {
            if (emitter != null)
                position = emitter.position;
            // Clear the active receipt before notifications can start another independent transaction.
            decimal amount = receipt;
            receipt = 0m;
            collecting = false;
            inReceipt = false;
            // Retained objects destroyed during a later Play session cannot affect its new ledger.
            if (generation != CollectedPrice.Generation)
            {
                pending = 0m;
                return;
            }
            if (amount > 0m)
            {
                CollectedPrice.Collect(amount);
                pending += amount;
            }
            // A nested final order completion releases this receipt and every earlier amount in one HUD update.
            if (closed)
            {
                ObjectInteraction.Signaled -= Observe;
                Settle();
            }
            else if (pending > 0m && (settings is not { DeferDisplay: true } || released))
                Flush();
        }

        #endregion

        #region Presentation

        /// <summary>Releases only this collector's balance when its selected completion policy succeeds.</summary>
        /// <param name="source">Exact local interaction publishing the event.</param>
        /// <param name="moment">Successful interaction boundary.</param>
        private void Observe(ObjectInteraction source, InteractionMoment moment)
        {
            // Completed state belongs to this spawn; repeated completion events cannot pay it twice.
            if (closed && !inReceipt || released || moment != InteractionMoment.Completed)
                return;
            int index = Array.IndexOf(sources, source);
            if (index < 0)
                return;
            completed[index] = true;
            released = settings.CompletionMode == PriceCompletionMode.Any || Array.TrueForAll(completed, value => value);
            if (released && !inReceipt)
                Flush();
        }

        /// <summary>Clears the pending amount before notifying HUD listeners to prevent duplicate settlement.</summary>
        private void Flush()
        {
            // Notification callbacks cannot observe or settle the same pending amount again.
            decimal amount = pending;
            pending = 0m;
            Publish(amount);
        }

        /// <summary>Settles only this ending lifetime's remaining balance; zero and repeated closure never change the ledger.</summary>
        private void Settle()
        {
            // Clear ownership before callbacks can destroy, reactivate or close this collector again.
            decimal amount = pending;
            pending = 0m;
            if (amount <= 0m || generation != CollectedPrice.Generation)
                return;
            if (!released && settings is { DeferDisplay: true, OnDespawn: PendingPriceOnDespawn.Discard })
                CollectedPrice.Discard(amount);
            else
                Publish(amount);
        }

        /// <summary>Announces only an actual positive payout; deferred and discarded amounts stay silent.</summary>
        /// <param name="amount">Previously cleared balance now reaching the visible counter.</param>
        private void Publish(decimal amount)
        {
            if (amount <= 0m)
                return;
            CollectedPrice.Publish(amount);
            if (settings.Sound)
                StudioAudio.PlayAt("sfx_cashcollect", emitter != null ? emitter.position : position);
        }

        #endregion

        #endregion
    }
}
