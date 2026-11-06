using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Prepares preset-based card changes and reverses temporary changes at contact boundaries.</summary>
    internal sealed class InteractionChangeRun
    {
        #region Types

        /// <summary>Retains one exact target instead of resolving names during completion callbacks.</summary>
        private readonly struct Operation
        {
            internal readonly InteractionChangeSettings Settings;
            internal readonly ObjectInteraction Feature;
            internal readonly bool Added;

            #region Methods

            /// <summary>Captures a prepared operation and its staged card.</summary>
            /// <param name="settings">Read-only operation data.</param>
            /// <param name="feature">Existing or staged target card.</param>
            /// <param name="added">Whether the card was created for this batch.</param>
            internal Operation(InteractionChangeSettings settings, ObjectInteraction feature, bool added)
            {
                Settings = settings;
                Feature = feature;
                Added = added;
            }

            #endregion
        }

        #endregion

        #region State

        private static readonly HashSet<InteractionChangeRun> claimed = new HashSet<InteractionChangeRun>();
        private readonly ObjectItem item;
        private readonly List<Operation> operations = new List<Operation>();
        private readonly Dictionary<ObjectInteraction, InteractionRuntimeSnapshot> snapshots = new Dictionary<ObjectInteraction, InteractionRuntimeSnapshot>();
        private readonly HashSet<ObjectInteraction> removed = new HashSet<ObjectInteraction>();
        private readonly HashSet<ObjectInteraction> created = new HashSet<ObjectInteraction>();
        private readonly Dictionary<ObjectInteraction, bool> availability = new Dictionary<ObjectInteraction, bool>();
        private bool applied;
        private bool cancelled;

        #endregion

        #region Methods

        #region Preparation

        /// <summary>Retains the exact affected participant for one batch.</summary>
        /// <param name="item">Reserved contact participant.</param>
        private InteractionChangeRun(ObjectItem item)
        {
            this.item = item;
        }

        /// <summary>Prepares every operation before contact effects start.</summary>
        /// <param name="item">Reserved participant receiving edits.</param>
        /// <param name="settings">Ordered operation blocks.</param>
        /// <param name="run">Receives the prepared batch, or null for an empty list.</param>
        /// <param name="warning">Receives missing cards, presets or dependencies.</param>
        /// <returns>True when the complete batch can be applied.</returns>
        internal static bool TryPrepare(ObjectItem item, InteractionChangeSettings[] settings, out InteractionChangeRun run, out string warning)
        {
            run = null;
            warning = string.Empty;
            if (settings.Length == 0)
                return true;
            InteractionChangeRun proposed = new InteractionChangeRun(item);
            try
            {
                foreach (InteractionChangeSettings change in settings)
                {
                    if (!change.TryValidate(out warning))
                        return proposed.Reject(out run);
                    // Resolve availability before any visual effects or consumption can start.
                    if (change.Operation is InteractionChangeOperation.Enable or InteractionChangeOperation.Disable)
                    {
                        if (!proposed.PrepareAvailability(change))
                            return proposed.Reject(out run);
                        continue;
                    }
                    // Missing removal targets are optional; other edits still validate as a complete batch.
                    if (change.Operation == InteractionChangeOperation.Remove)
                    {
                        proposed.PrepareRemoval(change);
                        continue;
                    }
                    ObjectInteraction feature;
                    bool added = false;
                    if (change.Operation == InteractionChangeOperation.Add)
                    {
                        if (proposed.HasEnabled(change.Kind, null))
                            return proposed.Reject(out run);
                        feature = InteractionRuntimeState.FindRemoved(item.gameObject, change.Kind);
                        foreach (Operation previous in proposed.operations)
                            if (previous.Settings.Operation == InteractionChangeOperation.Remove && previous.Settings.Kind == change.Kind)
                                feature = previous.Feature;
                        if (feature == null)
                        {
                            if (!proposed.CanCreate(change.Kind, out warning))
                                return proposed.Reject(out run);
                            using (InteractionRuntimeState.Stage())
                            {
                                feature = InteractionRuntimeCatalog.Create(change.Kind, item.gameObject);
                                feature.enabled = false;
                            }
                            added = true;
                            proposed.created.Add(feature);
                        }
                    }
                    else
                        feature = change.Target.Resolve(item.gameObject);
                    if (feature == null)
                    {
                        warning = "Select an interaction that exists on the affected object.";
                        return proposed.Reject(out run);
                    }
                    proposed.operations.Add(new Operation(change, feature, added));
                }
                if (proposed.operations.Count == 0)
                    return true;
                bool valid = proposed.StageValues(out warning);
                proposed.Restore(false);
                if (!valid)
                    return proposed.Reject(out run);
                claimed.Add(proposed);
                run = proposed;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidCastException or OverflowException)
            {
                proposed.Restore(false);
                warning = exception.Message;
                return proposed.Reject(out run);
            }
        }

        /// <summary>Collects exact or type-wide removals without rejecting contact items that lack the card.</summary>
        /// <param name="change">Removal selection from the reusable contact preset.</param>
        private void PrepareRemoval(InteractionChangeSettings change)
        {
            // Exact selections retain their occurrence; type selections include every matching local card.
            if (!change.Target.AllOfType)
            {
                ObjectInteraction feature = change.Target.Resolve(item.gameObject);
                if (feature != null)
                    operations.Add(new Operation(change, feature, false));
                return;
            }
            foreach (ObjectInteraction feature in item.GetComponents<ObjectInteraction>())
                if (InteractionRuntimeCatalog.KindOf(feature) == change.Kind && !InteractionRuntimeState.IsRemoved(feature))
                    operations.Add(new Operation(change, feature, false));
        }

        /// <summary>Prepares a state change only when a matching card can actually transition.</summary>
        /// <param name="change">Type or exact card selected by the contact rule.</param>
        /// <returns>True when the requested transition has an eligible target without a conflicting card.</returns>
        private bool PrepareAvailability(InteractionChangeSettings change)
        {
            bool enable = change.Operation == InteractionChangeOperation.Enable;
            if (!change.Target.AllOfType)
            {
                ObjectInteraction feature = change.Target.Resolve(item.gameObject);
                if (feature == null || feature.enabled == enable)
                    return false;
                if (enable && HasEnabled(change.Kind, feature))
                    return false;
                operations.Add(new Operation(change, feature, false));
                return true;
            }

            // Type selection changes the first eligible card in component order.
            if (enable && HasEnabled(change.Kind, null))
                return false;
            int previousCount = operations.Count;
            foreach (ObjectInteraction feature in item.GetComponents<ObjectInteraction>())
                if (InteractionRuntimeCatalog.KindOf(feature) == change.Kind && !InteractionRuntimeState.IsRemoved(feature)
                    && feature.enabled != enable)
                {
                    operations.Add(new Operation(change, feature, false));
                    break;
                }
            return operations.Count > previousCount;
        }

        /// <summary>Checks for a conflict on the participant or among prepared additions.</summary>
        /// <param name="kind">Interaction family that would become enabled.</param>
        /// <param name="except">Exact card receiving the edit, if any.</param>
        /// <returns>True when another card of the same type is enabled or queued to enable.</returns>
        private bool HasEnabled(InteractionRuntimeKind kind, ObjectInteraction except)
        {
            foreach (ObjectInteraction feature in item.GetComponents<ObjectInteraction>())
                if (feature != except && feature.enabled && InteractionRuntimeCatalog.KindOf(feature) == kind)
                    return true;
            if (Claims(kind, except))
                return true;
            // A completed contact can release its item before the modifier flushes queued edits.
            foreach (InteractionChangeRun run in claimed)
                if (run != this && run.item == item && run.Claims(kind, null))
                    return true;
            return false;
        }

        /// <summary>Checks the activation requests retained by this prepared batch.</summary>
        /// <param name="kind">Type being checked for a conflict.</param>
        /// <param name="except">Current operation's card when rechecking the batch.</param>
        /// <returns>True when a different operation will activate this type.</returns>
        private bool Claims(InteractionRuntimeKind kind, ObjectInteraction except)
        {
            foreach (Operation operation in operations)
                if (operation.Feature != except && operation.Settings.Kind == kind
                    && operation.Settings.Operation is InteractionChangeOperation.Add or InteractionChangeOperation.Enable)
                    return true;
            return false;
        }

        /// <summary>Confirms that a transition can still commit after its contact delay.</summary>
        /// <returns>True when no target disappeared or changed availability.</returns>
        internal bool CanCommit()
        {
            if (item == null || item.IsConsumed)
                return false;
            foreach (Operation operation in operations)
            {
                ObjectInteraction feature = operation.Feature;
                if (feature == null)
                    return false;
                switch (operation.Settings.Operation)
                {
                    case InteractionChangeOperation.Enable when feature.enabled:
                    case InteractionChangeOperation.Disable when !feature.enabled:
                        return false;
                    case InteractionChangeOperation.Add:
                    case InteractionChangeOperation.Enable:
                        if (HasEnabled(operation.Settings.Kind, feature))
                            return false;
                        break;
                }
            }
            return true;
        }

        /// <summary>Clears any queued or unfinished availability claims at the Play boundary.</summary>
        internal static void ResetClaims()
        {
            claimed.Clear();
        }

        /// <summary>Checks uniqueness and required physics before invoking Unity's component factory.</summary>
        /// <param name="kind">Requested interaction family.</param>
        /// <param name="warning">Receives a missing dependency or duplicate card.</param>
        /// <returns>True when staged creation has no implicit physics side effects.</returns>
        private bool CanCreate(InteractionRuntimeKind kind, out string warning)
        {
            warning = "This object already has that interaction. Edit it instead of adding another.";
            if (kind is not (InteractionRuntimeKind.Hover or InteractionRuntimeKind.Dialogue or InteractionRuntimeKind.Unlock
                or InteractionRuntimeKind.Dispenser or InteractionRuntimeKind.Container))
                foreach (ObjectInteraction feature in item.GetComponents<ObjectInteraction>())
                    if (InteractionRuntimeCatalog.KindOf(feature) == kind)
                        return false;
            warning = "Grab requires an existing Rigidbody and valid owned colliders.";
            if (kind == InteractionRuntimeKind.Grab && !ObjectGrab.ValidateBody(item.gameObject, out warning))
                return false;
            warning = "Assembly Product requires an existing Rigidbody.";
            if (kind == InteractionRuntimeKind.AssemblyProduct && item.GetComponent<Rigidbody>() == null)
                return false;
            warning = "Spray Sauce requires Elastic Deformation on the affected object.";
            if (kind == InteractionRuntimeKind.SpraySauce && item.GetComponent<ObjectElasticDeformation>() == null)
                return false;
            warning = "Drop and Throw require Grab on the affected object.";
            if ((kind is InteractionRuntimeKind.Drop or InteractionRuntimeKind.Throw) && item.GetComponent<ObjectGrab>() == null)
                return false;
            warning = string.Empty;
            return true;
        }

        /// <summary>Writes copied data only, then validates the final combined configuration before any activation.</summary>
        /// <param name="warning">Receives a failed preset or final card validation.</param>
        /// <returns>True when every operation and remaining dependency is valid.</returns>
        private bool StageValues(out string warning)
        {
            snapshots.Clear();
            removed.Clear();
            availability.Clear();
            warning = "An interaction changed or disappeared before the contact completed.";
            foreach (Operation operation in operations)
            {
                if (operation.Feature == null)
                    return false;
                if (!snapshots.ContainsKey(operation.Feature))
                    snapshots.Add(operation.Feature, new InteractionRuntimeSnapshot(operation.Feature));
                InteractionChangeSettings settings = operation.Settings;
                if (settings.Operation == InteractionChangeOperation.Remove)
                {
                    removed.Add(operation.Feature);
                    continue;
                }
                if (settings.Operation is InteractionChangeOperation.Enable or InteractionChangeOperation.Disable)
                {
                    availability[operation.Feature] = settings.Operation == InteractionChangeOperation.Enable;
                    continue;
                }
                if (settings.Operation == InteractionChangeOperation.Add)
                {
                    availability[operation.Feature] = true;
                    removed.Remove(operation.Feature);
                    operation.Feature.SetRuntimeName(settings.Name);
                    InteractionRuntimeCatalog.BindActions(operation.Feature, settings.Action, settings.AdvanceAction);
                }
                if (!InteractionRuntimeCatalog.Import(operation.Feature, settings.Groups, out warning))
                    return false;
            }
            foreach (ObjectInteraction feature in snapshots.Keys)
                if (!removed.Contains(feature) && (!availability.TryGetValue(feature, out bool enable) || enable)
                    && !InteractionRuntimeCatalog.Validate(feature, out warning))
                    return false;
            warning = string.Empty;
            return true;
        }

        #endregion

        #region Commit And Reversal

        /// <summary>Queues this batch after visual and consumption effects have committed.</summary>
        internal void Commit()
        {
            if (!cancelled && !applied)
                InteractionRuntimeState.Enqueue(this);
        }

        /// <summary>Applies final values outside the modifier's active rule iteration.</summary>
        internal void Apply()
        {
            claimed.Remove(this);
            if (cancelled || applied)
                return;
            if (!CanCommit())
            {
                Cancel();
                return;
            }
            try
            {
                // Session baselines precede every real write; validation probes never enter them.
                foreach (Operation operation in operations)
                    if (!operation.Added)
                        InteractionRuntimeState.Remember(operation.Feature);
                if (!StageValues(out string warning))
                {
                    Restore(false);
                    Debug.LogWarning("Contact interaction edit: " + warning, item);
                    Cancel();
                    return;
                }
                applied = true;
                foreach (ObjectInteraction feature in snapshots.Keys)
                {
                    if (created.Contains(feature))
                        InteractionRuntimeState.Include(feature);
                    InteractionRuntimeState.SetRemoved(feature, removed.Contains(feature));
                    if (removed.Contains(feature))
                        feature.enabled = false;
                    else if (availability.TryGetValue(feature, out bool enable) && feature.enabled != enable)
                        feature.enabled = enable;
                    else
                        InteractionRuntimeCatalog.Refresh(feature);
                }
                InteractionRuntimeRefresh.RebindRoot(item.gameObject);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidCastException or OverflowException)
            {
                Restore(applied);
                applied = false;
                Debug.LogWarning("Contact interaction edit: " + exception.Message, item);
                Cancel();
            }
        }

        /// <summary>Reverses a temporary batch or discards an unfinished prepared batch.</summary>
        internal void Cancel()
        {
            if (cancelled)
                return;
            cancelled = true;
            claimed.Remove(this);
            if (applied)
                Restore(true);
            // New cards remain absent after temporary contact ends; originals keep their component identity.
            foreach (ObjectInteraction feature in created)
                if (feature != null)
                {
                    feature.enabled = false;
                    using (InteractionRuntimeState.Stage())
                        UnityEngine.Object.DestroyImmediate(feature);
                }
            if (item != null)
                InteractionRuntimeRefresh.RebindRoot(item.gameObject);
        }

        /// <summary>Restores settings in reverse operation order without touching presets.</summary>
        /// <param name="refresh">Whether enabled state and cached bindings must be restored.</param>
        private void Restore(bool refresh)
        {
            foreach (InteractionRuntimeSnapshot snapshot in snapshots.Values)
                snapshot.Restore(refresh);
        }

        /// <summary>Discards staged components after preparation fails.</summary>
        /// <param name="run">Receives null for the rejected batch.</param>
        /// <returns>False for direct use by preparation checks.</returns>
        private bool Reject(out InteractionChangeRun run)
        {
            Cancel();
            run = null;
            return false;
        }

        #endregion

        #endregion
    }
}
