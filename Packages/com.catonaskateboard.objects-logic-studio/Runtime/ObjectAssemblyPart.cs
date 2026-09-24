using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains an inserted ingredient and restores its original ownership if explicitly detached.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ObjectAssemblyPart : MonoBehaviour
    {
        #region State

        private ObjectAssemblyProduct product;
        private ObjectInteraction[] features;
        private bool[] enabledFeatures;
        private Collider[] colliders;
        private bool[] enabledColliders;
        private ObjectItem[] items;
        private bool[] enabledItems;
        private Rigidbody body;
        private CarryBodyState original;
        private GameObject geometry;
        private Vector3 originalScale;
        private string originalName;
        private bool attached;
        private readonly Dictionary<Collider, Collider> shapes = new Dictionary<Collider, Collider>();

        #endregion

        #region Properties

        /// <summary>Product currently owning the ingredient, or null after detachment.</summary>
        public ObjectAssemblyProduct Product => attached ? product : null;
        /// <summary>Recipe tag recorded at insertion, independent of later product tag changes.</summary>
        public string IngredientTag { get; private set; }
        /// <summary>Occupied slot in the product's recipe layout.</summary>
        public int MagnetIndex { get; private set; }
        /// <summary>Mass contributed to the assembled body's total.</summary>
        public float Mass { get; private set; }

        #endregion

        #region Methods

        #region Ownership

        /// <summary>Moves an accepted held ingredient into a slot and transfers collision ownership to its product.</summary>
        /// <param name="owner">Validated product accepting the ingredient.</param>
        /// <param name="grab">Currently held ingredient with supported geometry.</param>
        /// <param name="index">Empty compatible magnet selected by the recipe.</param>
        internal void Attach(ObjectAssemblyProduct owner, ObjectGrab grab, int index)
        {
            // Capture state after ending carry, so later detachment restores the ingredient's own body policy.
            grab.Cancel();
            product = owner;
            IngredientTag = gameObject.tag;
            MagnetIndex = index;
            body = grab.Body;
            original = new CarryBodyState(body);
            Mass = body.mass;
            originalScale = transform.localScale;
            originalName = gameObject.name;
            features = GetComponentsInChildren<ObjectInteraction>(true);
            items = GetComponentsInChildren<ObjectItem>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            enabledFeatures = new bool[features.Length];
            enabledItems = new bool[items.Length];
            enabledColliders = new bool[colliders.Length];
            for (int entry = 0; entry < features.Length; entry++)
            {
                enabledFeatures[entry] = features[entry].enabled;
                features[entry].enabled = false;
            }
            for (int entry = 0; entry < items.Length; entry++)
            {
                enabledItems[entry] = items[entry].enabled;
                items[entry].enabled = false;
            }

            // Actual ingredients remain children; separate shape transforms bypass their dormant rigidbody.
            AssemblyMagnet magnet = owner.Settings.Magnets[index];
            gameObject.name = magnet.Name;
            transform.SetParent(owner.transform, false);
            transform.SetLocalPositionAndRotation(magnet.Position, Quaternion.Euler(magnet.Rotation));
            transform.localScale = Vector3.Scale(originalScale, magnet.Scale);
            geometry = AssemblyGeometry.Create(transform, colliders, shapes);
            for (int entry = 0; entry < colliders.Length; entry++)
            {
                enabledColliders[entry] = colliders[entry].enabled;
                colliders[entry].enabled = false;
            }
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            body.detectCollisions = false;
            body.interpolation = RigidbodyInterpolation.None;
            geometry.SetActive(true);
            attached = true;
        }

        /// <summary>Restores standalone behavior when an external operation removes this ingredient from its product.</summary>
        private void OnTransformParentChanged()
        {
            // Pooling the complete product does not detach its ingredients or erase recipe progress.
            if (attached && product != null && transform.parent != product.transform)
                Detach();
        }

        /// <summary>Keeps proxy geometry inactive while its visible ingredient is disabled.</summary>
        private void OnDisable()
        {
            // Pooling the entire product naturally disables both branches without changing recipe counts.
            if (geometry != null)
                geometry.SetActive(false);
        }

        /// <summary>Restores existing proxy geometry after the ingredient becomes visible again.</summary>
        private void OnEnable()
        {
            // No shapes are recreated on activation.
            if (geometry != null && attached)
                geometry.SetActive(true);
        }

        /// <summary>Finds the product-owned counterpart of an original ingredient collider.</summary>
        /// <param name="source">Original shape retained on the ingredient.</param>
        /// <returns>The active proxy shape, or null after detachment.</returns>
        internal Collider Proxy(Collider source)
        {
            // Contact mesh replacements resolve this association once when preparing their effects.
            return attached && source != null && shapes.TryGetValue(source, out Collider proxy) ? proxy : null;
        }

        /// <summary>Updates the surviving product when an ingredient is explicitly destroyed.</summary>
        private void OnDestroy()
        {
            // A destroyed product ignores callbacks from its own children.
            if (!attached)
                return;
            attached = false;
            RemoveGeometry();
            if (product != null)
                product.Remove(this);
        }

        /// <summary>Releases the product's collision branch and restores each captured component state.</summary>
        private void Detach()
        {
            // Disable proxy collisions before original colliders become active again.
            attached = false;
            RemoveGeometry();
            product.Remove(this);
            product = null;
            transform.localScale = originalScale;
            gameObject.name = originalName;
            if (body != null)
                original.Restore(body, false);
            for (int entry = 0; entry < colliders.Length; entry++)
                if (colliders[entry] != null)
                    colliders[entry].enabled = enabledColliders[entry];
            for (int entry = 0; entry < items.Length; entry++)
                if (items[entry] != null)
                    items[entry].enabled = enabledItems[entry];
            for (int entry = 0; entry < features.Length; entry++)
                if (features[entry] != null)
                    features[entry].enabled = enabledFeatures[entry];
        }

        /// <summary>Removes only the collision branch owned by this insertion.</summary>
        private void RemoveGeometry()
        {
            // Deferred destruction cannot leave active duplicate shapes during this frame.
            if (geometry == null)
                return;
            geometry.SetActive(false);
            Destroy(geometry);
            geometry = null;
            shapes.Clear();
        }

        #endregion

        #endregion
    }
}
