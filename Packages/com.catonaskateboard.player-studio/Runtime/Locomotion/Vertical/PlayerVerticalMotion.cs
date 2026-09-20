using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Integrates vertical motion without querying colliders or changing scene objects.</summary>
    public static class PlayerVerticalMotion
    {
        #region Methods

        #region Integration

        /// <summary>Calculates the vertical distance for one frame; the native controller later resolves contact.</summary>
        /// <param name="velocity">Current vertical speed, including upward launch; never below minus TerminalSpeed. Updated in place.</param>
        /// <param name="grounded">Whether the preceding movement reported contact below.</param>
        /// <param name="settings">Previously validated gravity snapshot.</param>
        /// <param name="deltaTime">Positive frame duration in seconds.</param>
        /// <returns>Requested vertical displacement, with downward movement negative.</returns>
        public static float Advance(ref float velocity, bool grounded, PlayerGravitySettings settings, float deltaTime)
        {
            // Disabled gravity never carries fall momentum into planar-only movement.
            if (!settings.Enabled)
            {
                velocity = 0f;
                return 0f;
            }

            // A small continuous downward request refreshes contact without a separate raycast or teleport.
            if (grounded && velocity <= 0f)
            {
                velocity = -settings.GroundSpeed;
                return velocity * deltaTime;
            }

            // Integrate acceleration until terminal speed, then cover the remaining time at constant speed.
            float transitionTime = Mathf.Min(deltaTime, (settings.TerminalSpeed + velocity) / settings.Acceleration);
            float nextVelocity = Mathf.Max(-settings.TerminalSpeed, velocity - settings.Acceleration * deltaTime);
            float displacement = (velocity + nextVelocity) * (0.5f * transitionTime)
                - settings.TerminalSpeed * (deltaTime - transitionTime);
            velocity = nextVelocity;
            return displacement;
        }

        #endregion

        #endregion
    }
}
