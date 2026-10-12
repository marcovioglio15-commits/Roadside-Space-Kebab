using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Applies temporary self-ejection physics and restores authored body and surfaces on disable.</summary>
    internal sealed class EjectBodyState
    {
        #region State

        private Rigidbody body;
        private CarryBodyState original;
        private float mass;
        private Collider[] shapes;
        private PhysicsMaterial[] surfaces;
        private PhysicsMaterial material;

        #endregion
        #region Properties

        /// <summary>Whether the ejected body is still using this temporary profile.</summary>
        internal bool Active => body != null;

        #endregion
        #region Methods
        #region Lifetime

        /// <summary>Captures the pre-launch state and enables real collision-driven motion until despawn.</summary>
        /// <param name="target">Authored Rigidbody, which may normally be kinematic.</param>
        /// <param name="settings">Reusable body and contact overrides.</param>
        internal void Begin(Rigidbody target, ReleaseSettings settings)
        {
            // A new launch cannot overwrite the original snapshot of an active one.
            if (Active)
                return;
            body = target;
            original = new CarryBodyState(body);
            mass = body.mass;
            body.isKinematic = false;
            settings.Apply(body);
            if (!settings.OverrideSurface)
                return;
            shapes = body.GetComponentsInChildren<Collider>();
            surfaces = new PhysicsMaterial[shapes.Length];
            material = new PhysicsMaterial("Temporary Eject Surface");
            settings.Apply(material);
            // Only this physical root's solid shapes receive the temporary material.
            for (int index = 0; index < shapes.Length; index++)
                if (!shapes[index].isTrigger && shapes[index].attachedRigidbody == body)
                {
                    surfaces[index] = shapes[index].sharedMaterial;
                    shapes[index].sharedMaterial = material;
                }
        }

        /// <summary>Restores pooled objects and releases the one material owned by this launch.</summary>
        internal void Restore()
        {
            if (body != null)
            {
                original.Restore(body, false);
                body.mass = mass;
                body = null;
            }
            if (material == null)
                return;
            for (int index = 0; index < shapes.Length; index++)
                if (shapes[index] != null && shapes[index].sharedMaterial == material)
                    shapes[index].sharedMaterial = surfaces[index];
            Object.Destroy(material);
            material = null;
            shapes = null;
            surfaces = null;
        }

        #endregion
        #endregion
    }
}
