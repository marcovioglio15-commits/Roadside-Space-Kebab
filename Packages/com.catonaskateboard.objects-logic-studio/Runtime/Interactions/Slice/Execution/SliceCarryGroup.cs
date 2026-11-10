using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Carries replacement pieces as one temporary compound body until a throw, drop or cancellation releases them.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class SliceCarryGroup : MonoBehaviour
    {
        #region State

        private SliceCarryPart[] parts;
        private ObjectGrab grab;
        private Rigidbody body;
        private Vector3 carryCenter;
        private int count;
        private bool throwAsShuriken;
        private bool released;

        #endregion

        #region Methods

        #region Construction

        /// <summary>Prepares an inactive group using the held source's carry and release settings and existing input actions.</summary>
        /// <param name="source">Held source replaced by this cut.</param>
        /// <param name="capacity">Exact number of output prefabs.</param>
        /// <param name="throwAsShuriken">Whether Throw releases one piece at a time.</param>
        /// <returns>The inactive group ready to receive newly spawned outputs.</returns>
        internal static SliceCarryGroup Create(ObjectGrab source, int capacity, bool throwAsShuriken)
        {
            GameObject root = new GameObject("Slice Outputs · " + source.name);
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, source.gameObject.scene);
            root.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            SliceCarryGroup group = root.AddComponent<SliceCarryGroup>();
            group.parts = new SliceCarryPart[capacity];
            group.throwAsShuriken = throwAsShuriken && capacity > 1;
            group.body = root.AddComponent<Rigidbody>();
            root.AddComponent<ObjectItem>();
            group.grab = root.AddComponent<ObjectGrab>();
            // Native Unity serialization copies only configuration; no runtime reflection or source mutation is needed.
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source.Settings), group.grab.Settings);
            group.grab.Settings.Instant = true;
            group.grab.Settings.PickupSound = false;
            group.grab.BindAction(source.Action);
            CopyRelease(source.GetComponent<ObjectDrop>(), root);
            CopyRelease(source.GetComponent<ObjectThrow>(), root);
            return group;
        }

        /// <summary>Copies only release configuration and action IDs, leaving flags, VFX and completion bindings independent.</summary>
        /// <param name="source">Optional release action authored on the replaced item.</param>
        /// <param name="root">Inactive temporary carry root.</param>
        private static void CopyRelease(ObjectRelease source, GameObject root)
        {
            if (source == null || !source.enabled)
                return;
            ObjectRelease target = source is ObjectThrow ? root.AddComponent<ObjectThrow>() : root.AddComponent<ObjectDrop>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source.PhysicsSettings), target.PhysicsSettings);
            target.BindAction(source.Action);
            if (source is ObjectThrow original && target is ObjectThrow throwing)
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(original.Trajectory), throwing.Trajectory);
        }

        /// <summary>Suspends a new output once and adds its mass to the shared carrier.</summary>
        /// <param name="created">Supported physical prefab under this inactive root.</param>
        internal void Add(GameObject created)
        {
            // Mass is accumulated only at spawning; ordinary carry frames keep the prepared compound body.
            parts[count] = new SliceCarryPart(created);
            body.mass = count == 0 ? parts[count].Body.mass : body.mass + parts[count].Body.mass;
            count++;
        }

        /// <summary>Activates supported compound geometry and transfers the current player's occupied carry slot.</summary>
        /// <param name="source">Still-held source about to be consumed.</param>
        internal void Take(ObjectGrab source)
        {
            // Activate only after every original body and interaction has been suspended.
            gameObject.SetActive(true);
            SliceCarryLayout.Arrange(parts, count);
            if (throwAsShuriken)
                carryCenter = transform.InverseTransformPoint(SliceCarryLayout.Center(parts, count));
            if (!HoverObserver.ReplaceHeld(source, grab))
                ReleaseMembers(null);
        }

        #endregion

        #region Release

        /// <summary>Recognizes only bodies explicitly suspended by this group during Grab validation.</summary>
        /// <param name="candidate">Nested body encountered while validating the shared carrier.</param>
        /// <returns>True when this group owns that body's dormant state.</returns>
        internal bool Owns(Rigidbody candidate)
        {
            // Foreign nested bodies must still fail the shared Grab ownership check.
            for (int index = 0; index < count; index++)
                if (parts[index].Body == candidate)
                    return true;
            return false;
        }

        /// <summary>Releases the frontmost piece and keeps the other pieces centred around the original carry pose.</summary>
        /// <param name="direction">World-space launch direction.</param>
        /// <param name="profile">Throw body overrides for the released piece.</param>
        /// <param name="piece">Receives the independent Rigidbody ready to launch.</param>
        /// <returns>True when the sequential mode released one piece.</returns>
        internal bool TryReleasePiece(Vector3 direction, ReleaseSettings profile, out Rigidbody piece)
        {
            piece = null;
            if (!throwAsShuriken || released || count == 0)
                return false;

            // Choose the piece farthest along the shot and keep array order for equally placed pieces.
            int front = 0;
            float distance = Vector3.Dot(parts[0].Body.transform.position, direction);
            for (int index = 1; index < count; index++)
            {
                float candidate = Vector3.Dot(parts[index].Body.transform.position, direction);
                if (candidate <= distance)
                    continue;
                distance = candidate;
                front = index;
            }

            // Remove the selected entry before release callbacks can inspect the carrier's ownership.
            SliceCarryPart part = parts[front];
            piece = part.Body;
            count--;
            System.Array.Copy(parts, front + 1, parts, front, count - front);
            parts[count] = null;
            if (count > 0)
                body.mass -= piece.mass;
            part.Release(Vector3.zero, Vector3.zero, piece.position, profile);

            // Keep the same carry slot until the final piece leaves. Rebuild shapes once per throw.
            if (count == 0)
                ReleaseMembers(null);
            else if (!released)
            {
                part.RemoveMirror();
                Vector3 offset = transform.TransformPoint(carryCenter) - SliceCarryLayout.Center(parts, count);
                for (int index = 0; index < count; index++)
                    parts[index].Move(offset);
                grab.RefreshCarryGeometry();
            }

            // Recentring can put the remaining shapes in the launch path before the next physics step.
            Physics.SyncTransforms();
            if (!released)
                ShotBurstCollisions.IgnoreBriefly(part.Colliders, grab.Colliders);
            if (HoverObserver.ActivePlayer is Transform player)
                ShotBurstCollisions.IgnoreBriefly(part.Colliders, player.GetComponentsInChildren<Collider>());
            return true;
        }

        /// <summary>Separates all pieces after Throw has applied its impulse, or immediately when carrying is cancelled.</summary>
        /// <param name="profile">Successful release profile, or null for context loss or external deactivation.</param>
        internal void ReleaseMembers(ReleaseSettings profile)
        {
            if (released)
                return;
            released = true;
            // Capture one rigid-motion frame before removing shapes can change the carrier's centre of mass.
            Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
            Vector3 spin = body != null ? body.angularVelocity : Vector3.zero;
            Vector3 centre = body != null ? body.worldCenterOfMass : transform.position;
            // Releasing children before destroying their temporary parent preserves every actual output prefab.
            for (int index = 0; index < count; index++)
                parts[index].Release(velocity, spin, centre, profile);
            count = 0;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        /// <summary>Prevents disabled or externally destroyed carriers from stranding suspended child bodies.</summary>
        private void OnDisable()
        {
            // External deactivation uses the same idempotent release as deliberate player actions.
            ReleaseMembers(null);
        }

        #endregion

        #endregion
    }
}
