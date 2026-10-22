using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Measures one pooled effect across its playback before any gameplay shot is fired.</summary>
    internal static class PlayerToolEffectBounds
    {
        #region Methods

        #region Measurement

        /// <summary>Builds an oriented lifetime envelope from sampled particles and authored non-particle geometry.</summary>
        /// <param name="root">Effect prototype sampled synchronously before the next render.</param>
        /// <param name="particles">Cached particle systems below this instance.</param>
        /// <param name="lifetime">Maximum effect lifetime used by Tools.</param>
        /// <returns>Conservative bounds in the effect's rotation frame, measured in world units.</returns>
        internal static Bounds Measure(Transform root, ParticleSystem[] particles, float lifetime)
        {
            // One scratch buffer serves every system and is released after initialization.
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 0.01f);
            int capacity = 1;
            float duration = 0.01f;
            foreach (ParticleSystem system in particles)
            {
                capacity = Mathf.Max(capacity, system.main.maxParticles);
                duration = Mathf.Max(duration, system.main.duration + system.main.startLifetime.constantMax + system.main.startDelay.constantMax);
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            ParticleSystem.Particle[] samples = new ParticleSystem.Particle[capacity];
            Quaternion inverse = Quaternion.Inverse(root.rotation);
            float step = Mathf.Min(duration, lifetime) / 16f;
            // Sampling retains bursts, moving sparks and ordinary billboard sizes in the same envelope.
            for (int frame = 0; frame < 16; frame++)
                foreach (ParticleSystem system in particles)
                {
                    system.Simulate(step, false, frame == 0, false);
                    ParticleSystem.MainModule main = system.main;
                    ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                    float scale = main.scalingMode == ParticleSystemScalingMode.Hierarchy ? Maximum(system.transform.lossyScale)
                        : main.scalingMode == ParticleSystemScalingMode.Local ? Maximum(system.transform.localScale) : 1f;
                    int count = system.GetParticles(samples);
                    for (int index = 0; index < count; index++)
                    {
                        Vector3 position = main.simulationSpace switch
                        {
                            ParticleSystemSimulationSpace.Local => system.transform.TransformPoint(samples[index].position),
                            ParticleSystemSimulationSpace.Custom when main.customSimulationSpace != null => main.customSimulationSpace.TransformPoint(samples[index].position),
                            _ => samples[index].position
                        };
                        float size = Maximum(samples[index].GetCurrentSize3D(system)) * scale;
                        float radius = size * 0.75f;
                        if (renderer != null && renderer.renderMode == ParticleSystemRenderMode.Stretch)
                            radius += (size * renderer.lengthScale + samples[index].velocity.magnitude * renderer.velocityScale) * 0.5f;
                        Vector3 centre = inverse * (position - root.position);
                        bounds.Encapsulate(centre - Vector3.one * radius);
                        bounds.Encapsulate(centre + Vector3.one * radius);
                    }
                }
            // Reset simulation before this instance becomes available to its first real shot.
            foreach (ParticleSystem system in particles)
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer is not ParticleSystemRenderer && renderer.bounds.size.sqrMagnitude > 0f)
                {
                    Vector3 centre = inverse * (renderer.bounds.center - root.position);
                    float radius = renderer.bounds.extents.magnitude;
                    bounds.Encapsulate(centre - Vector3.one * radius);
                    bounds.Encapsulate(centre + Vector3.one * radius);
                }
            bounds.Expand(bounds.size * 0.15f + Vector3.one * 0.005f);
            return bounds;
        }

        /// <summary>Reads the largest absolute coordinate used by uniform clearance estimates.</summary>
        /// <param name="value">Size or transform scale.</param>
        /// <returns>Largest absolute axis.</returns>
        private static float Maximum(Vector3 value)
        {
            return Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        #endregion

        #endregion
    }
}
