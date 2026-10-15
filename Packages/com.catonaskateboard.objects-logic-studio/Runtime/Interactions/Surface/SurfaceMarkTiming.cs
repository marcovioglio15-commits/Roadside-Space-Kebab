using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Captures a deposit's independent lifetime without modifying the shared trail settings.</summary>
    internal readonly struct SurfaceMarkTiming
    {
        #region Fields
        internal readonly float Lifetime;
        internal readonly float Fade;
        internal readonly bool Permanent;
        #endregion
        #region Methods
        #region Configuration
        /// <summary>Resolves ordinary, timed or permanent residue when the contact is accepted.</summary>
        /// <param name="settings">Shared ordinary lifetime.</param>
        /// <param name="rule">Optional exposed target override.</param>
        internal SurfaceMarkTiming(SurfaceTrailSettings settings, SpraySurfaceRule rule = null)
        {
            Lifetime = rule != null && rule.TrailMode == SprayTrailMode.Timed ? rule.TrailLifetime : settings.Lifetime;
            Fade = rule != null && rule.TrailMode == SprayTrailMode.Timed ? rule.TrailFadeDuration : settings.FadeDuration;
            Permanent = rule != null && rule.TrailMode == SprayTrailMode.Permanent;
        }

        /// <summary>Checks whether nearby deposits share a compatible lifetime policy.</summary>
        /// <param name="other">Existing deposit timing.</param>
        /// <returns>True when rewetting does not change the existing lifetime policy.</returns>
        internal bool Matches(SurfaceMarkTiming other)
        {
            return Permanent == other.Permanent && (Permanent || Lifetime.Equals(other.Lifetime) && Fade.Equals(other.Fade));
        }

        /// <summary>Packs fade timing without sending infinities to the shader.</summary>
        /// <param name="born">Last wetting time.</param>
        /// <returns>Shader timing, including the explicit permanent flag.</returns>
        internal Vector4 ShaderValue(float born)
        {
            return new Vector4(born, Lifetime, Fade, Permanent ? 1f : 0f);
        }
        #endregion
        #endregion
    }
}
