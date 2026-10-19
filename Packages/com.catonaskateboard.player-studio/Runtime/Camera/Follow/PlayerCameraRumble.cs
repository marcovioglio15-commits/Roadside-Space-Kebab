using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Composes short external camera effects after the rig computes its ordinary pose.</summary>
    public static class PlayerCameraRumble
    {
        #region State

        /// <summary>Retains one source's timeline and authored spatial waveform.</summary>
        private sealed class Pulse
        {
            internal Object Owner;
            internal Vector3 Center;
            internal PlayerCameraRumbleSettings Settings;
            internal float Started;
            internal float Duration;
        }

        private static readonly List<Pulse> pulses = new List<Pulse>();

        #endregion
        #region Methods
        #region Ownership

        /// <summary>Starts one spatial source without adding a camera component or changing input.</summary>
        /// <param name="owner">Effect owning this pulse.</param>
        /// <param name="center">World position used for distance attenuation.</param>
        /// <param name="settings">Validated feedback choices.</param>
        /// <param name="duration">Scaled active-effect duration before the optional end pulse.</param>
        public static void Begin(Object owner, Vector3 center, PlayerCameraRumbleSettings settings, float duration)
        {
            Cancel(owner);
            if (settings.Enabled)
                pulses.Add(new Pulse { Owner = owner, Center = center, Settings = settings, Started = Time.time, Duration = duration });
        }

        /// <summary>Removes feedback owned by a cancelled or disabled interaction.</summary>
        /// <param name="owner">Source whose camera contribution is ending.</param>
        public static void Cancel(Object owner)
        {
            for (int index = pulses.Count - 1; index >= 0; index--)
                if (pulses[index].Owner == owner)
                    pulses.RemoveAt(index);
        }

        /// <summary>Forgets retained timelines when entering Play without domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            pulses.Clear();
        }

        #endregion
        #region Sampling

        /// <summary>Adds bounded positional and angular oscillation to the current camera pose.</summary>
        /// <param name="position">Camera position before feedback.</param>
        /// <param name="rotation">Camera orientation before feedback.</param>
        internal static void Apply(ref Vector3 position, ref Quaternion rotation)
        {
            Vector3 translation = Vector3.zero;
            Vector3 angles = Vector3.zero;
            // Empty lists cost no projection or allocation, and expired effects are removed on sampling.
            for (int index = pulses.Count - 1; index >= 0; index--)
            {
                Pulse pulse = pulses[index];
                PlayerCameraRumbleSettings settings = pulse.Settings;
                float elapsed = Time.time - pulse.Started;
                if (pulse.Owner == null || elapsed > pulse.Duration + (settings.AtEnd ? settings.PulseDuration : 0f))
                {
                    pulses.RemoveAt(index);
                    continue;
                }
                float envelope = settings.Sustained && elapsed < pulse.Duration ? Mathf.Min(1f, elapsed / 0.1f) : 0f;
                if (settings.AtStart && elapsed < settings.PulseDuration)
                    envelope = Mathf.Max(envelope, Mathf.Sin(elapsed / settings.PulseDuration * Mathf.PI));
                if (settings.AtEnd && elapsed >= pulse.Duration)
                    envelope = Mathf.Max(envelope, Mathf.Sin((elapsed - pulse.Duration) / settings.PulseDuration * Mathf.PI));
                float weight = envelope * Mathf.Clamp01(1f - Vector3.Distance(position, pulse.Center) / settings.Range);
                if (weight <= 0f)
                    continue;
                float phase = elapsed * settings.Frequency;
                Vector3 wave = new Vector3(Sample(settings, phase), Sample(settings, phase * 1.13f + 0.31f), Sample(settings, phase * 0.87f + 0.67f));
                translation += Vector3.Scale(wave, settings.Position) * weight;
                angles += Vector3.Scale(wave, settings.Rotation) * weight;
            }
            position += rotation * translation;
            rotation *= Quaternion.Euler(angles);
        }

        /// <summary>Samples one normalized cycle without retaining per-frame objects.</summary>
        /// <param name="settings">Oscillation configuration.</param>
        /// <param name="phase">Continuous cycle count.</param>
        /// <returns>Finite signed waveform amplitude.</returns>
        private static float Sample(PlayerCameraRumbleSettings settings, float phase)
        {
            float value = settings.CustomWave ? settings.Wave.Evaluate(Mathf.Repeat(phase, 1f)) : Mathf.Sin(phase * Mathf.PI * 2f);
            return float.IsFinite(value) ? value : 0f;
        }

        #endregion
        #endregion
    }
}
