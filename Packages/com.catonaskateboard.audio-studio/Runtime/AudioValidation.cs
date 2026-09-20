using System;
using System.Collections.Generic;

namespace CatOnASkateboard.AudioStudio
{
    /// <summary>Reports malformed authoring data without clamping or inserting gameplay defaults.</summary>
    public static class AudioValidation
    {
        #region Methods
        #region Validation
        /// <summary>Collects all actionable preset diagnostics.</summary>
        /// <param name="preset">Audio configuration to inspect.</param>
        /// <param name="messages">Destination warning list.</param>
        public static void Collect(AudioPreset preset, List<string> messages)
        {
            // Missing optional event paths are valid while a new project's map is being authored.
            if (preset == null)
            {
                messages.Add("Select an audio preset.");
                return;
            }
            if (preset.Playback.Enabled)
            {
                Nonnegative(preset.Playback.MasterVolume, "Master volume", messages);
                Distances(preset.Playback.MinimumDistance, preset.Playback.MaximumDistance, "Default", messages);
            }
            Nonnegative(preset.Routing.SfxVolume, "SFX volume", messages);
            Nonnegative(preset.Routing.MusicVolume, "Music volume", messages);
            foreach (string bus in new[] { preset.Routing.MasterBus, preset.Routing.SfxBus, preset.Routing.MusicBus })
                if (!string.IsNullOrEmpty(bus) && !bus.StartsWith("bus:/", StringComparison.Ordinal))
                    messages.Add("Bus paths must start with bus:/.");
            if (!float.IsFinite(preset.MusicCrossfadeSeconds) || preset.MusicCrossfadeSeconds <= 0f)
                messages.Add("Music crossfade must be finite and greater than zero.");

            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (AudioBinding binding in preset.Events)
            {
                if (binding == null)
                {
                    messages.Add("Remove null event entries.");
                    continue;
                }
                Identity(binding.Key, keys, "Event", messages);
                EventPath(binding.EventPath, binding.Key, messages);
                Nonnegative(binding.Volume, binding.Key + " volume", messages);
                if (!float.IsFinite(binding.Pitch) || binding.Pitch <= 0f)
                    messages.Add(binding.Key + ": pitch must be positive and finite.");
                if (binding.Spatialize && binding.OverrideDistances)
                    Distances(binding.MinimumDistance, binding.MaximumDistance, binding.Key, messages);
                if (binding.LimitRate && (binding.MaxPlays < 1 || !float.IsFinite(binding.WindowSeconds) || binding.WindowSeconds <= 0f))
                    messages.Add(binding.Key + ": rate limits need positive plays and a positive finite window.");
                HashSet<string> parameters = new HashSet<string>(StringComparer.Ordinal);
                foreach (AudioParameter parameter in binding.Parameters)
                {
                    Identity(parameter.Name, parameters, binding.Key + " parameter", messages);
                    if (!float.IsFinite(parameter.Value))
                        messages.Add(binding.Key + ": parameter values must be finite.");
                }
            }
            keys.Clear();
            foreach (AudioMusic music in preset.Music)
            {
                Identity(music.Context, keys, "Music context", messages);
                if (!music.Enabled)
                    continue;
                EventPath(music.EventPath, music.Context, messages);
                Nonnegative(music.Volume, music.Context + " volume", messages);
            }
        }

        /// <summary>Validates one nonnegative scalar without modifying it.</summary>
        /// <param name="value">Authored scalar.</param>
        /// <param name="label">Field label for diagnostics.</param>
        /// <param name="messages">Destination warnings.</param>
        private static void Nonnegative(float value, string label, List<string> messages)
        {
            // NaN and infinity must not slip through ordinary range comparisons.
            if (!float.IsFinite(value) || value < 0f)
                messages.Add(label + " must be finite and nonnegative.");
        }

        /// <summary>Validates an attenuation interval.</summary>
        /// <param name="minimum">Near distance.</param>
        /// <param name="maximum">Far distance.</param>
        /// <param name="label">Owning event or preset label.</param>
        /// <param name="messages">Destination warnings.</param>
        private static void Distances(float minimum, float maximum, string label, List<string> messages)
        {
            // Do not silently sort or snap inverted distances.
            if (!float.IsFinite(minimum) || !float.IsFinite(maximum) || minimum < 0f || maximum <= minimum)
                messages.Add(label + ": maximum distance must exceed a nonnegative minimum.");
        }

        /// <summary>Checks unique project identifiers within one collection.</summary>
        /// <param name="key">Authored identifier.</param>
        /// <param name="keys">Identifiers already encountered.</param>
        /// <param name="label">Collection label.</param>
        /// <param name="messages">Destination warnings.</param>
        private static void Identity(string key, HashSet<string> keys, string label, List<string> messages)
        {
            // Empty identities and duplicates require explicit project decisions.
            if (string.IsNullOrWhiteSpace(key) || !keys.Add(key))
                messages.Add(label + ": keys must be nonempty and unique.");
        }

        /// <summary>Checks the scheme of an optional Studio event path.</summary>
        /// <param name="path">Authored path.</param>
        /// <param name="label">Owning binding.</param>
        /// <param name="messages">Destination warnings.</param>
        private static void EventPath(string path, string label, List<string> messages)
        {
            // Empty paths are intentionally permitted until FMOD content is ready.
            if (!string.IsNullOrEmpty(path) && (!path.StartsWith("event:/", StringComparison.Ordinal) || path.Length <= 7))
                messages.Add(label + ": choose a complete event:/ path.");
        }
        #endregion
        #endregion
    }
}
