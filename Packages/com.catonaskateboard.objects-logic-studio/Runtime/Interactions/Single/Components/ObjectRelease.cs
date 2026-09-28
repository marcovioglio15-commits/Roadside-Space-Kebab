using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares release physics and material ownership between Drop and Throw.</summary>
    public abstract class ObjectRelease : ObjectSingleInteraction
    {
        #region Serialized Fields

        [Header("Release")]
        [Tooltip("Body and surface settings applied when releasing the held object.")]
        [SerializeField]
        private ReleaseSettings physics = new ReleaseSettings();

        #endregion

        #region State

        private PhysicsMaterial surface;
        private ObjectGrab grab;

        #endregion

        #region Properties

        /// <summary>Body and contact response applied by this release action.</summary>
        public ReleaseSettings PhysicsSettings => physics;
        /// <summary>Grab on the same object; release never affects another object's carry slot.</summary>
        internal ObjectGrab Grab => grab;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Caches the same-object Grab required by this feature.</summary>
        protected override void OnEnable()
        {
            // No dependency lookup occurs during normal input or carry updates.
            grab = GetComponent<ObjectGrab>();
            base.OnEnable();
        }

        /// <summary>Restores collider assets before disposing the one cached runtime material.</summary>
        protected override void OnDisable()
        {
            // Deleting a release component must not leave its material assigned to the object.
            if (surface != null)
            {
                if (grab != null)
                    grab.RestoreSurface(surface);
                Destroy(surface);
                surface = null;
            }
            base.OnDisable();
        }

        #endregion

        #region Configuration

        /// <summary>Validates the required Grab and shared release settings.</summary>
        /// <param name="warning">Receives the first dependency or physics issue.</param>
        /// <returns>True when the release can be configured independently of its input binding.</returns>
        protected override bool TryValidateSettings(out string warning)
        {
            // A disabled Grab is not a usable dependency even if the component still exists.
            warning = "Drop and Throw require an enabled Grab on the same object.";
            if (GetComponent<ObjectGrab>() is not ObjectGrab owner || !owner.enabled)
                return false;
            warning = "Release physics settings are missing.";
            return physics != null && physics.TryValidate(out warning);
        }

        #endregion

        #region Release

        /// <summary>Applies common release physics once before a possible throw impulse.</summary>
        /// <param name="view">Camera defining the aim direction for a Throw.</param>
        internal virtual void Execute(Transform view)
        {
            // Lazy creation avoids materials on features that never release an object.
            if (physics.OverrideSurface)
            {
                if (surface == null)
                    surface = new PhysicsMaterial("Objects Logic Studio " + Kind) { hideFlags = HideFlags.HideAndDontSave };
                physics.Apply(surface);
            }
            grab.Release(physics, physics.OverrideSurface ? surface : null);
        }

        #endregion

        #endregion
    }
}
