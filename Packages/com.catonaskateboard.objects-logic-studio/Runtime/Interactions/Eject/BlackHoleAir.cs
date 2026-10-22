using CatOnASkateboard.PlayerStudio;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Redirects preauthored air particles into the collector using one retained particle buffer.</summary>
    internal sealed class BlackHoleAir
    {
        #region State

        private ParticleSystem system;
        private ParticleSystem.Particle[] particles;
        private ParticleSystemCullingMode originalCullingMode;

        #endregion
        #region Methods
        #region Playback

        /// <summary>Starts the existing child effect only when optional air suction is enabled.</summary>
        /// <param name="root">Ejector hierarchy containing the saved particle system.</param>
        /// <param name="settings">Air path and activation policy.</param>
        internal void Begin(Transform root, BlackHoleSettings settings)
        {
            Stop();
            if (!settings.Air)
                return;
            Transform target = PlayerHierarchy.Resolve(root, settings.AirPath);
            system = target != null ? target.GetComponent<ParticleSystem>() : null;
            if (system == null)
                return;
            if (particles == null || particles.Length < system.main.maxParticles)
                particles = new ParticleSystem.Particle[system.main.maxParticles];
            ParticleSystem.MainModule main = system.main;
            originalCullingMode = main.cullingMode;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            system.Play();
        }

        /// <summary>Steers live air streaks and removes them when they reach the collector.</summary>
        /// <param name="destination">World collector center plus offset.</param>
        /// <param name="speed">World speed in metres per second.</param>
        internal void Tick(Vector3 destination, float speed)
        {
            if (system == null)
                return;
            ParticleSystem.MainModule main = system.main;
            Transform frame = main.simulationSpace == ParticleSystemSimulationSpace.Custom ? main.customSimulationSpace
                : main.simulationSpace == ParticleSystemSimulationSpace.Local ? system.transform : null;
            int count = system.GetParticles(particles);
            for (int index = 0; index < count; index++)
            {
                Vector3 position = frame != null ? frame.TransformPoint(particles[index].position) : particles[index].position;
                Vector3 difference = destination - position;
                if (difference.sqrMagnitude < 0.04f)
                    particles[index].remainingLifetime = 0f;
                else
                {
                    Vector3 velocity = difference.normalized * speed;
                    particles[index].velocity = frame != null ? frame.InverseTransformVector(velocity) : velocity;
                }
            }
            system.SetParticles(particles, count);
        }

        /// <summary>Stops and clears only the effect owned by this suction run.</summary>
        internal void Stop()
        {
            if (system != null)
            {
                ParticleSystem.MainModule main = system.main;
                main.cullingMode = originalCullingMode;
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            system = null;
        }

        #endregion
        #endregion
    }
}
