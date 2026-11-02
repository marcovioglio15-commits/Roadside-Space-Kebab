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

        private readonly ObjectItem item;
        private readonly List<Operation> operations = new List<Operation>();
        private readonly Dictionary<ObjectInteraction, InteractionRuntimeSnapshot> snapshots = new Dictionary<ObjectInteraction, InteractionRuntimeSnapshot>();
        private readonly HashSet<ObjectInteraction> removed = new HashSet<ObjectInteraction>();
        private readonly HashSet<ObjectInteraction> created = new HashSet<ObjectInteraction>();
        private readonly HashSet<ObjectInteraction> additions = new HashSet<ObjectInteraction>();
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
                    ObjectInteraction feature;
                    bool added = false;
                    if (change.Operation == InteractionChangeOperation.Add)
                    {
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
                bool valid = proposed.StageValues(out warning);
                proposed.Restore(false);
                if (!valid)
                    return proposed.Reject(out run);
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

        /// <summary>Checks uniqueness and required physics before invoking Unity's component factory.</summary>
        /// <param name="kind">Requested interaction family.</param>
        /// <param name="warning">Receives a missing dependency or duplicate card.</param>
        /// <returns>True when staged creation has no implicit physics side effects.</returns>
        private bool CanCreate(InteractionRuntimeKind kind, out string warning)
        {
            warning = "This object already has that interaction. Edit it instead of adding another.";
            if (kind is not (InteractionRuntimeKind.Hover or InteractionRuntimeKind.Dialogue or InteractionRuntimeKind.Unlock))
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
            additions.Clear();
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
                if (settings.Operation == InteractionChangeOperation.Add)
                {
                    additions.Add(operation.Feature);
                    removed.Remove(operation.Feature);
                    operation.Feature.SetRuntimeName(settings.Name);
                    InteractionRuntimeCatalog.BindActions(operation.Feature, settings.Action, settings.AdvanceAction);
                }
                if (settings.Preset != null && !InteractionRuntimeCatalog.Import(operation.Feature, settings.Preset, out warning))
                    return false;
            }
            foreach (ObjectInteraction feature in snapshots.Keys)
                if (!removed.Contains(feature) && !InteractionRuntimeCatalog.Validate(feature, out warning))
                    return false;
            ObjectGrab grab = item.GetComponent<ObjectGrab>();
            if (grab != null && removed.Contains(grab))
                foreach (ObjectInteraction feature in item.GetComponents<ObjectInteraction>())
                    if (feature is ObjectRelease && !removed.Contains(feature))
                    {
                        warning = "Remove Drop and Throw together with their required Grab interaction.";
                        return false;
                    }
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
            if (cancelled || applied || item == null || item.IsConsumed)
                return;
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
                    else if (additions.Contains(feature) && !feature.enabled)
                        feature.enabled = true;
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
