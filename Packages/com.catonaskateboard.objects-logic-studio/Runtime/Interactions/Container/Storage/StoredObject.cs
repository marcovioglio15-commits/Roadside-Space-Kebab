using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Keeps one inventory instance and its shared suspension snapshot until the Dispenser retrieves it.</summary>
    internal sealed class StoredObject
    {
        #region State

        private readonly SuspendedItemState state;

        #endregion

        #region Properties

        /// <summary>Original object retained in inventory.</summary>
        internal ObjectGrab Grab { get; }

        #endregion

        #region Methods

        #region Transfer

        /// <summary>Captures original physics and interaction state after carry overrides have been released.</summary>
        /// <param name="grab">Deposited object, including any assembled ingredient hierarchy.</param>
        internal StoredObject(ObjectGrab grab)
        {
            // One snapshot is reused even if a later retrieval attempt fails.
            Grab = grab;
            state = new SuspendedItemState(grab.gameObject);
        }

        /// <summary>Suspends the instance and arranges it inside its owning inventory.</summary>
        /// <param name="container">Container retaining the original object.</param>
        /// <param name="slot">Placement slot for visible storage.</param>
        internal void Suspend(ObjectContainer container, int slot)
        {
            // Hidden and visible inventory use identical interaction and collision restrictions.
            state.Suspend();
            Grab.transform.SetParent(container.transform, true);
            Grab.transform.localPosition = container.Settings.Position + container.Settings.Spacing * slot;
            Grab.transform.localRotation = Quaternion.Euler(container.Settings.Rotation);
            Grab.gameObject.SetActive(container.Settings.KeepVisible);
        }

        /// <summary>Restores the original instance at the dispenser output before attempting its Grab.</summary>
        /// <param name="position">World output position.</param>
        /// <param name="rotation">World output orientation.</param>
        internal void Restore(Vector3 position, Quaternion rotation)
        {
            // Detaching first prevents the container's own body from becoming a competing grab owner.
            Grab.gameObject.SetActive(false);
            Grab.transform.SetParent(null, true);
            Grab.transform.SetPositionAndRotation(position, rotation);
            state.Restore();
        }

        #endregion

        #endregion
    }
}
