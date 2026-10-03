using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Previews one deferred ingredient at its future magnet without transferring its interactions or recipe ownership.</summary>
    internal sealed class AssemblyIngredientDock
    {
        #region State

        private ObjectAssemblyProduct product;
        private Rigidbody body;
        private CarryBodyState originalBody;
        private Transform parent;
        private Vector3 originalScale;
        private Vector3 snappedScale;
        private Pose pose;

        #endregion

        #region Properties

        /// <summary>Station holding the visual preview, independently of the ingredient's original assembly table.</summary>
        internal ObjectAssemblyStation Station { get; private set; }
        /// <summary>Usable ingredient retained even when its magnet lies beyond the station's contact surface.</summary>
        internal ObjectGrab Ingredient { get; private set; }

        #endregion

        #region Methods

        #region Eligibility

        /// <summary>Prevents automatic contact from stealing a held product or another station's preview.</summary>
        /// <param name="station">Station considering contact insertion.</param>
        /// <param name="ingredient">Physical object proposed for this recipe.</param>
        /// <returns>True when the ingredient is free for this station's automatic contact handling.</returns>
        internal static bool Allows(ObjectAssemblyStation station, ObjectGrab ingredient)
        {
            // Ordinary ingredients keep their existing contact behavior, including insertion while carried.
            return ingredient != null && (!ingredient.TryGetComponent(out ObjectAssemblyProduct product)
                || (product.Dock == null || product.Dock.Station == station)
                    && (!product.Settings.WaitForNextIngredient || !ingredient.IsHeld));
        }

        /// <summary>Ends a preview when another interaction moves the object or its eligibility changes.</summary>
        /// <param name="station">Station reevaluating its retained contact.</param>
        /// <returns>The withdrawn item, or null when no preview was released.</returns>
        internal ObjectItem Refresh(ObjectAssemblyStation station)
        {
            // A pose change or physical impulse owns the object now; never snap it back every frame.
            if (Ingredient == null)
            {
                Release();
                return null;
            }
            if (product != null && product.isActiveAndEnabled && product.Dock == this && body != null && body.isKinematic
                && Ingredient.transform.parent == parent && AtPose() && station.WaitsForIngredient(Ingredient)
                && station.CanAccept(Ingredient, false))
                return null;
            ObjectItem withdrawn = Ingredient.Item;
            Release();
            return withdrawn;
        }

        /// <summary>Compares both physics and visible poses so transform animations and impulses can release the preview.</summary>
        /// <returns>True while the ingredient still occupies its original preview pose.</returns>
        private bool AtPose()
        {
            // Small transform conversion errors must not make a stationary preview repeatedly detach.
            return (Ingredient.transform.position - pose.position).sqrMagnitude < 0.000001f
                && (body.position - pose.position).sqrMagnitude < 0.000001f
                && Quaternion.Angle(Ingredient.transform.rotation, pose.rotation) < 0.01f
                && Quaternion.Angle(body.rotation, pose.rotation) < 0.01f
                && (Ingredient.transform.localScale - snappedScale).sqrMagnitude < 0.000001f;
        }

        #endregion

        #region Placement

        /// <summary>Snaps one eligible waiting product to the exact pose and scale it will have after insertion.</summary>
        /// <param name="station">Empty station whose next recipe provides the magnet.</param>
        /// <param name="ingredient">Completed product still retaining all of its own interactions.</param>
        internal void Snap(ObjectAssemblyStation station, ObjectGrab ingredient)
        {
            // No product prefab, assembly part, collision proxy or reservation is created for a preview.
            if (Ingredient != null || !station.WaitsForIngredient(ingredient) || !Allows(station, ingredient)
                || !station.CanAccept(ingredient, false))
                return;
            ObjectAssemblyProduct template = station.Settings.ProductPrefab.GetComponent<ObjectAssemblyProduct>();
            if (!template.CanAccept(ingredient, out int index, false))
                return;
            AssemblyMagnet magnet = template.Settings.Magnets[index];
            Station = station;
            Ingredient = ingredient;
            product = ingredient.GetComponent<ObjectAssemblyProduct>();
            product.Dock = this;
            body = ingredient.Body;
            originalBody = new CarryBodyState(body);
            parent = ingredient.transform.parent;
            originalScale = ingredient.transform.localScale;
            Quaternion rotation = station.transform.rotation * Quaternion.Euler(station.Settings.OutputRotation);
            Matrix4x4 output = Matrix4x4.TRS(station.OutputPosition, rotation, station.OutputScale);
            Matrix4x4 local = Matrix4x4.TRS(magnet.Position, Quaternion.Euler(magnet.Rotation), Vector3.Scale(originalScale, magnet.Scale));
            Matrix4x4 world = output * local;
            snappedScale = (parent != null ? parent.worldToLocalMatrix * world : world).lossyScale;
            pose = new Pose(output.MultiplyPoint3x4(magnet.Position), rotation * Quaternion.Euler(magnet.Rotation));
            // Suspend gravity, keeping the authored colliders and every interaction available.
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            ingredient.transform.SetPositionAndRotation(pose.position, pose.rotation);
            ingredient.transform.localScale = snappedScale;
            body.position = pose.position;
            body.rotation = pose.rotation;
        }

        /// <summary>Restores temporary preview overrides before pickup, actual assembly or station teardown.</summary>
        internal void Release()
        {
            // Clear ownership first so reentrant component callbacks cannot release the same preview twice.
            if (product != null && product.Dock == this)
                product.Dock = null;
            if (Ingredient != null)
            {
                if ((Ingredient.transform.localScale - snappedScale).sqrMagnitude < 0.000001f)
                    Ingredient.transform.localScale = originalScale;
                // A dynamic body already belongs to an impulse or another physics interaction; keep its velocity.
                if (body != null && body.isKinematic && !Ingredient.IsHeld)
                    originalBody.Restore(body, false);
            }
            Ingredient = null;
            Station = null;
            product = null;
            body = null;
            parent = null;
        }

        #endregion

        #endregion
    }
}
