using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Tracks actual floating portions independently of their own interactions.</summary>
    internal sealed class ScoopDisplay
    {
        #region Types

        /// <summary>Retains one floating body's standalone physics and independent motion phase.</summary>
        private readonly struct Portion
        {
            internal readonly ObjectItem Item;
            internal readonly Rigidbody Body;
            internal readonly CarryBodyState Original;
            internal readonly Vector3 Phase;
            internal readonly float Speed;
            internal readonly Collider[] Colliders;

            /// <summary>Captures the body before the magnet controls its pose.</summary>
            /// <param name="item">Active portion on this magnet.</param>
            /// <param name="body">Optional body on the portion root.</param>
            internal Portion(ObjectItem item, Rigidbody body)
            {
                Item = item;
                Body = body;
                Original = body != null ? new CarryBodyState(body) : default;
                Colliders = item.GetComponentsInChildren<Collider>(true);
                Phase = new Vector3(Random.value, Random.value, Random.value) * (Mathf.PI * 2f);
                Speed = Random.Range(0.78f, 1.22f);
            }
        }

        #endregion

        #region State

        private readonly ObjectDispenser owner;
        private readonly ScoopDisplaySettings settings;
        private readonly Portion[] portions;
        private readonly Quaternion rotation;
        private readonly GameObject restockPrefab;
        private readonly ObjectIdentity restockIdentity;
        private GameObject currentPrefab;
        private bool usingRestockPrefab;
        private readonly Vector3[] lastPositions;
        private readonly List<int> batch = new List<int>();
        private readonly HashSet<Collider> supplyColliders = new HashSet<Collider>();
        private Collider[] combinedTargets;
        private bool active = true;
        private int count;

        #endregion

        #region Properties

        /// <summary>Number of originals currently occupying magnets.</summary>
        internal int Count => count;
        /// <summary>Logical scoops and original departures since this display was activated.</summary>
        internal int WithdrawnCount { get; private set; }
        /// <summary>Portion shapes that count as part of the source for aiming.</summary>
        internal ISet<Collider> SupplyColliders => supplyColliders;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Creates the initial supply once at the activation boundary.</summary>
        /// <param name="owner">Dispenser owning this supply cycle.</param>
        internal ScoopDisplay(ObjectDispenser owner)
        {
            this.owner = owner;
            settings = owner.Settings.ScoopDisplay;
            portions = new Portion[settings.Magnets.Length];
            lastPositions = new Vector3[portions.Length];
            currentPrefab = owner.Settings.Prefab;
            rotation = currentPrefab.transform.rotation;
            restockPrefab = settings.RestockPrefab != null ? settings.RestockPrefab : currentPrefab;
            if (settings.RestockForNewContainer)
                restockIdentity = restockPrefab.GetComponent<ObjectIdentity>();
            // Instances remain independent roots so contact and carry ownership stay separate.
            for (int index = 0; index < portions.Length; index++)
                Spawn(index);
        }

        /// <summary>Updates local motion and prunes items consumed by other interactions.</summary>
        internal void Tick()
        {
            if (count == 0)
                return;
            float time = Time.time * settings.Speed * (Mathf.PI * 2f);
            Matrix4x4 frame = owner.transform.localToWorldMatrix;
            Quaternion orientation = owner.transform.rotation * rotation;
            for (int index = 0; index < portions.Length; index++)
            {
                Portion portion = portions[index];
                if (ReferenceEquals(portion.Item, null))
                    continue;
                // External movement releases the magnet instead of pulling an escaped original back.
                if (portion.Item == null || !portion.Item.isActiveAndEnabled || portion.Item.IsConsumed || portion.Item.IsCarried
                    || portion.Body != null && !portion.Body.isKinematic
                    || (portion.Item.transform.position - lastPositions[index]).sqrMagnitude > 0.0004f)
                {
                    Remove(index, true);
                    continue;
                }
                // Slow eddies carry each portion sideways while a separate rhythm makes it bob.
                float localTime = time * portion.Speed;
                float orbit = localTime + portion.Phase.x;
                float bob = (Mathf.Sin(localTime * 1.32f + portion.Phase.y) + 1f) * 0.5f;
                if (settings.BobCrestPower > 1f)
                    bob = Mathf.Pow(bob, settings.BobCrestPower);
                Vector3 drift = Vector3.Scale(settings.Drift, new Vector3(
                    0.72f * Mathf.Sin(orbit) + 0.28f * Mathf.Sin(localTime * 0.58f + portion.Phase.z),
                    bob * 2f - 1f,
                    0.72f * Mathf.Cos(orbit) + 0.28f * Mathf.Sin(localTime * 0.86f + portion.Phase.y)));
                lastPositions[index] = frame.MultiplyPoint3x4(settings.Magnets[index] + drift);
                portion.Item.transform.SetPositionAndRotation(lastPositions[index], orientation);
            }
        }

        /// <summary>Disposes displayed stock while leaving any items already withdrawn in the world.</summary>
        internal void Stop()
        {
            active = false;
            // Re-enabling starts a fresh cycle with the prefab's initial flags and appearance.
            for (int index = 0; index < portions.Length; index++)
                if (portions[index].Item != null)
                {
                    ObjectItem item = portions[index].Item;
                    Remove(index);
                    item.gameObject.SetActive(false);
                    Object.Destroy(item.gameObject);
                }
        }

        #endregion

        #region Stock

        /// <summary>Returns combined supply geometry, rebuilding only after a magnet changes occupants.</summary>
        /// <returns>Basket geometry and the currently displayed portions.</returns>
        internal Collider[] Targets()
        {
            if (combinedTargets == null)
            {
                Collider[] baseTargets = owner.Colliders;
                combinedTargets = new Collider[baseTargets.Length + supplyColliders.Count];
                baseTargets.CopyTo(combinedTargets, 0);
                supplyColliders.CopyTo(combinedTargets, baseTargets.Length);
            }
            return combinedTargets;
        }

        /// <summary>Ignores the existing portions during the container's immersion.</summary>
        /// <param name="pairs">Collision set owned by this scoop.</param>
        /// <param name="colliders">Container's owned colliders.</param>
        internal void Ignore(CarryCollisions pairs, Collider[] colliders)
        {
            foreach (Portion portion in portions)
                if (portion.Item != null)
                    pairs.Add(colliders, portion.Colliders);
        }

        /// <summary>Lets one independent Grab lift a portion clear of the basket and its neighbours.</summary>
        /// <param name="pairs">Temporary collision exclusions owned by Grab.</param>
        /// <param name="colliders">Colliders on the withdrawn portion.</param>
        internal void IgnorePickup(CarryCollisions pairs, Collider[] colliders)
        {
            pairs.Ignore(colliders, owner.transform);
            Ignore(pairs, colliders);
        }

        /// <summary>Finds a live compatible portion using its current flags.</summary>
        /// <param name="receiver">Container supplying the accepted flag filter.</param>
        /// <param name="available">Require the portion to be free for reservation.</param>
        /// <returns>The first matching original, if present.</returns>
        internal ObjectItem Find(ObjectContainer receiver, bool available)
        {
            // Cooking and other modifications can change flags while an item stays on its magnet.
            foreach (Portion portion in portions)
                if (portion.Item != null && (!available || !portion.Item.IsReserved) && Matches(portion.Item, receiver))
                    return portion.Item;
            return null;
        }

        /// <summary>Checks whether a fresh compatible container can begin a new stock cycle.</summary>
        /// <param name="receiver">Container attempting to scoop from the empty display.</param>
        /// <returns>True when this display can provide compatible new stock.</returns>
        internal bool CanRestock(ObjectContainer receiver)
        {
            return active && count == 0 && receiver.FillCount == 0 && restockIdentity != null
                && restockIdentity.Matches(receiver.Settings.AllowedFlags, receiver.Settings.Match);
        }

        /// <summary>Refills all magnets after the winning container reserves the empty dispenser.</summary>
        internal void Restock()
        {
            // The new cycle uses the same magnet positions and stock limits.
            currentPrefab = restockPrefab;
            usingRestockPrefab = settings.RestockPrefab != null;
            for (int index = 0; index < portions.Length; index++)
                Spawn(index);
        }

        /// <summary>Releases a portion before Grab captures its standalone body state.</summary>
        /// <param name="item">Original selected by the player.</param>
        internal void Withdraw(ObjectItem item)
        {
            int index = IndexOf(item);
            if (index >= 0)
                Remove(index, true);
        }

        /// <summary>Reinstates a released original when it hits the dispenser and a magnet is free.</summary>
        /// <param name="grab">Former portion that completed Drop or Throw.</param>
        /// <param name="contact">Collider struck by the returning portion.</param>
        /// <param name="entered">True for a new contact, including an object moved by external physics.</param>
        internal void Return(ObjectGrab grab, Collider contact, bool entered = false)
        {
            // Empty stock stays receptive. An availability rule can still prevent this supply from accepting a return.
            if (!active || owner == null || !owner.isActiveAndEnabled || owner.IsLocked || count >= portions.Length
                || grab.IsHeld || grab.Item.IsReserved || grab.Item.IsConsumed || grab.Item.IsFloatingSupply
                || !entered && grab.ReleaseRevision == grab.ScoopReleaseRevision || !contact.transform.IsChildOf(owner.transform))
                return;
            // A returning object keeps its current identity and appearance.
            for (int index = 0; index < portions.Length; index++)
                if (ReferenceEquals(portions[index].Item, null))
                {
                    Attach(index, grab.Item);
                    return;
                }
        }

        /// <summary>Consumes compatible originals in one scoop and advances the visible stock step once.</summary>
        /// <param name="output">First original held by the animation reservation.</param>
        /// <param name="receiver">Container providing the acceptance filter.</param>
        /// <param name="limit">Maximum number of logical portions this scoop can transfer.</param>
        /// <returns>Number of compatible originals transferred before the display advances.</returns>
        internal int Take(ObjectItem output, ObjectContainer receiver, int limit)
        {
            int first = IndexOf(output);
            if (!active || first < 0 || limit <= 0 || !output.IsOwnedBy(receiver) || !Matches(output, receiver))
                return 0;

            // Select distinct available originals before changing any magnet or respawning unlimited stock.
            batch.Clear();
            batch.Add(first);
            for (int index = 0; index < portions.Length && batch.Count < limit; index++)
                if (index != first && portions[index].Item is ObjectItem item && item != null && !item.IsReserved && Matches(item, receiver))
                    batch.Add(index);

            // A display step may clear extra decorative stock. Reserved portions always stay in place.
            int target = count - batch.Count;
            foreach (int step in settings.Steps)
                if (step < count)
                {
                    target = Mathf.Min(target, step);
                    break;
                }
            int removable = batch.Count;
            for (int index = 0; index < portions.Length && removable < count - target; index++)
                if (!batch.Contains(index) && portions[index].Item is ObjectItem item && item != null && !item.IsReserved)
                    removable++;
            if (removable < count - target)
                return 0;

            foreach (int index in batch)
            {
                receiver.RecordScoopPortion(portions[index].Item.gameObject);
                Consume(index);
            }
            for (int index = portions.Length - 1; count > target && index >= 0; index--)
                if (portions[index].Item is ObjectItem item && item != null && !item.IsReserved)
                    Consume(index);
            if (active && owner.Settings.Unlimited && settings.Steps.Length == 0)
                foreach (int index in batch)
                    if (ReferenceEquals(portions[index].Item, null))
                        Spawn(index);
            WithdrawnCount += batch.Count;
            return batch.Count;
        }

        /// <summary>Checks the live original against the container's current ingredient filter.</summary>
        /// <param name="item">Portion currently on a magnet.</param>
        /// <param name="receiver">Container that will receive the portion.</param>
        /// <returns>True while the original is still usable and compatible.</returns>
        private static bool Matches(ObjectItem item, ObjectContainer receiver)
        {
            // Contact effects may change the flags between scoop start and the fill keyframe.
            return item.isActiveAndEnabled && !item.IsConsumed && !item.IsCarried
                && item.Identity.Matches(receiver.Settings.AllowedFlags, receiver.Settings.Match);
        }

        #endregion

        #region Magnets

        /// <summary>Removes a spent clone after releasing any effects that should finish independently.</summary>
        /// <param name="index">Occupied magnet being cleared.</param>
        private void Consume(int index)
        {
            // Release physics and stock ownership before consumption notifies other interactions.
            ObjectItem item = portions[index].Item;
            Remove(index);
            item.ConsumeSelf();
            Object.Destroy(item.gameObject);
        }

        /// <summary>Creates a pristine portion at the specified local magnet.</summary>
        /// <param name="index">Vacant magnet index.</param>
        private void Spawn(int index)
        {
            GameObject portion = Object.Instantiate(currentPrefab,
                owner.transform.TransformPoint(settings.Magnets[index]), owner.transform.rotation * rotation);
            // Instantiation without a parent uses the active scene, which may differ from the dispenser's scene.
            if (portion.scene != owner.gameObject.scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(portion, owner.gameObject.scene);
            if (usingRestockPrefab)
                portion.transform.localScale *= settings.RestockScale;
            Attach(index, portion.GetComponent<ObjectItem>());
        }

        /// <summary>Occupies a magnet and suspends only motion driven by physics.</summary>
        /// <param name="index">Vacant magnet index.</param>
        /// <param name="item">Active original entering the display.</param>
        private void Attach(int index, ObjectItem item)
        {
            // Clear an existing gravity pulse before capturing the original Rigidbody policy.
            item.TryGetComponent(out Rigidbody body);
            if (body != null)
                ObjectGravityGenerator.Forget(body);
            Portion portion = new Portion(item, body);
            portions[index] = portion;
            foreach (Collider collider in portion.Colliders)
                supplyColliders.Add(collider);
            combinedTargets = null;
            count++;
            item.IsFloatingSupply = true;
            lastPositions[index] = owner.transform.TransformPoint(settings.Magnets[index]);
            item.transform.SetPositionAndRotation(lastPositions[index], owner.transform.rotation * rotation);
            if (body != null)
            {
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.isKinematic = true;
                body.useGravity = false;
            }
            if (item.TryGetComponent(out ObjectGrab grab))
            {
                grab.ScoopSupply = this;
                grab.ScoopReleaseRevision = grab.ReleaseRevision;
            }
        }

        /// <summary>Frees one magnet and restores the original's physics before another interaction uses it.</summary>
        /// <param name="index">Occupied magnet index.</param>
        /// <param name="departed">Whether an original left independently of a scoop or teardown.</param>
        private void Remove(int index, bool departed = false)
        {
            Portion portion = portions[index];
            portions[index] = default;
            foreach (Collider collider in portion.Colliders)
                supplyColliders.Remove(collider);
            combinedTargets = null;
            count--;
            if (departed)
                WithdrawnCount++;
            if (portion.Item != null)
                portion.Item.IsFloatingSupply = false;
            if (portion.Body != null)
                portion.Original.Restore(portion.Body, false);
            if (departed && count == 0)
                owner.NotifyScoopEmpty();
        }

        /// <summary>Looks up the exact original instead of relying on flags or world position.</summary>
        /// <param name="item">Original instance to locate.</param>
        /// <returns>Occupied slot index, or minus one when detached.</returns>
        private int IndexOf(ObjectItem item)
        {
            if (item != null)
                for (int index = 0; index < portions.Length; index++)
                    if (portions[index].Item == item)
                        return index;
            return -1;
        }

        #endregion

        #endregion
    }
}
