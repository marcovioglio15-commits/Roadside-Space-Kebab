using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Allocates a bounded visual pool once and resets particle/trail state when slots are reused.</summary>
    internal sealed class PlayerToolVisualPool : IDisposable
    {
        #region State

        private readonly GameObject root;
        private readonly Transform[] instances;
        private readonly ParticleSystem[][] particles;
        private readonly TrailRenderer[][] trails;
        private readonly int[] free;
        private readonly bool[] rented;
        private readonly Vector3[] scales;
        private readonly float[][] speeds;
        private readonly float[] scaleMultipliers;
        private int available;

        #endregion

        #region Properties

        /// <summary>Whether another visual can be emitted without allocating an object.</summary>
        internal bool HasSpace => available > 0;
        /// <summary>Measured lifetime envelope used only by automatic impact fitting.</summary>
        internal Bounds Envelope { get; }

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Creates reusable visuals; automatic fitting briefly activates one prototype to measure its particles before rendering.</summary>
        /// <param name="prefab">Validated bullet or impact prefab.</param>
        /// <param name="capacity">Fixed number of reusable instances.</param>
        /// <param name="scene">Scene owning the firing player.</param>
        /// <param name="scale">Uniform visual and particle travel multiplier.</param>
        /// <param name="alignBillboards">Whether ordinary billboards follow the impact's local plane.</param>
        /// <param name="measureLifetime">Positive effect lifetime requests a one-time clearance envelope measurement.</param>
        /// <param name="faceCamera">Whether automatic placement explicitly overrides ordinary billboards to face the camera.</param>
        internal PlayerToolVisualPool(GameObject prefab, int capacity, Scene scene, float scale = 1f, bool alignBillboards = false,
            float measureLifetime = 0f, bool faceCamera = false)
        {
            // The disabled parent keeps even active prefab roots inert until a slot is rented.
            root = new GameObject(prefab.name + " Pool");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            instances = new Transform[capacity];
            particles = new ParticleSystem[capacity][];
            trails = new TrailRenderer[capacity][];
            free = new int[capacity];
            rented = new bool[capacity];
            scales = new Vector3[capacity];
            speeds = measureLifetime > 0f ? new float[capacity][] : Array.Empty<float[]>();
            scaleMultipliers = measureLifetime > 0f ? new float[capacity] : Array.Empty<float>();
            available = capacity;
            for (int index = 0; index < capacity; index++)
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab, root.transform);
                instance.SetActive(false);
                instance.transform.localScale *= scale;
                scales[index] = instance.transform.localScale;
                instances[index] = instance.transform;
                particles[index] = instance.GetComponentsInChildren<ParticleSystem>(true);
                trails[index] = instance.GetComponentsInChildren<TrailRenderer>(true);
                // Pooled prefabs are visuals: only the swept query determines a bullet contact.
                foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true))
                {
                    body.isKinematic = true;
                    body.useGravity = false;
                }
                foreach (ParticleSystem particle in particles[index])
                {
                    ParticleSystem.MainModule main = particle.main;
                    main.stopAction = ParticleSystemStopAction.None;
                    // Child systems inherit effect scale; world-space emission speed scales separately.
                    if (scale != 1f || measureLifetime > 0f)
                    {
                        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                        main.startSpeedMultiplier *= scale;
                    }
                    if (alignBillboards && particle.TryGetComponent(out ParticleSystemRenderer renderer)
                        && renderer.renderMode == ParticleSystemRenderMode.Billboard)
                    {
                        renderer.alignment = ParticleSystemRenderSpace.Local;
                        // Unity's billboard front faces local -Z; turn it toward the impact's +Z normal.
                        main.startRotation3D = true;
                        main.startRotationX = new ParticleSystem.MinMaxCurve(0f);
                        main.startRotationY = new ParticleSystem.MinMaxCurve(Mathf.PI);
                    }
                }
                free[index] = capacity - index - 1;
                if (measureLifetime > 0f)
                {
                    scaleMultipliers[index] = 1f;
                    speeds[index] = new float[particles[index].Length];
                    for (int particle = 0; particle < particles[index].Length; particle++)
                    {
                        speeds[index][particle] = particles[index][particle].main.startSpeedMultiplier;
                        if (faceCamera && particles[index][particle].TryGetComponent(out ParticleSystemRenderer renderer)
                            && renderer.renderMode == ParticleSystemRenderMode.Billboard)
                            renderer.alignment = ParticleSystemRenderSpace.Facing;
                    }
                }
            }
            root.SetActive(true);
            if (measureLifetime > 0f)
            {
                // Sample one live prototype synchronously before rendering, then clear and park it for normal reuse.
                instances[0].gameObject.SetActive(true);
                Envelope = PlayerToolEffectBounds.Measure(instances[0], particles[0], measureLifetime);
                instances[0].gameObject.SetActive(false);
            }
        }

        /// <summary>Destroys the entire owned pool only when its player module is released.</summary>
        public void Dispose()
        {
            // No per-shot destruction occurs during ordinary play.
            if (root != null)
            {
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
            }
        }

        #endregion

        #region Slots

        /// <summary>Activates one cached visual at its final world pose.</summary>
        /// <param name="position">Spawn position.</param>
        /// <param name="rotation">Spawn orientation.</param>
        /// <param name="scale">Optional automatic-fit multiplier relative to the pooled prefab scale.</param>
        /// <returns>Rented slot index, or minus one when the pool is full.</returns>
        internal int Rent(Vector3 position, Quaternion rotation, float scale = 1f)
        {
            if (!HasSpace)
                return -1;
            int index = free[--available];
            rented[index] = true;
            instances[index].localScale = scales[index] * scale;
            if (scaleMultipliers.Length > 0 && scaleMultipliers[index] != scale)
            {
                scaleMultipliers[index] = scale;
                // Travel and size shrink together, so a reduced burst stays inside its fitted envelope.
                for (int particle = 0; particle < particles[index].Length; particle++)
                {
                    ParticleSystem.MainModule main = particles[index][particle].main;
                    main.startSpeedMultiplier = speeds[index][particle] * scale;
                }
            }
            instances[index].SetPositionAndRotation(position, rotation);
            instances[index].gameObject.SetActive(true);
            // Clear cached trails before replaying particle systems at the new location.
            foreach (TrailRenderer trail in trails[index])
                if (trail != null)
                    trail.Clear();
            foreach (ParticleSystem particle in particles[index])
                if (particle != null)
                {
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particle.Play(false);
                }
            return index;
        }

        /// <summary>Returns one slot and clears its visible state before reuse.</summary>
        /// <param name="index">Previously rented slot.</param>
        internal void Return(int index)
        {
            // Guard cleanup against duplicate completion paths.
            if (!rented[index])
                return;
            foreach (ParticleSystem particle in particles[index])
                if (particle != null)
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (instances[index] != null)
                instances[index].gameObject.SetActive(false);
            rented[index] = false;
            free[available++] = index;
        }

        /// <summary>Updates only the position of a live straight-moving projectile.</summary>
        /// <param name="index">Rented bullet slot.</param>
        /// <param name="position">Next world position.</param>
        internal void Move(int index, Vector3 position)
        {
            if (instances[index] != null)
                instances[index].position = position;
        }

        #endregion

        #endregion
    }
}
