using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Calculates planar intent without accessing input devices, scene objects or a collision motor.</summary>
    public static class PlayerPlanarMotion
    {
        #region Methods

        #region Integration

        /// <summary>Advances velocity toward the current command and integrates the travelled distance over this interval.</summary>
        /// <param name="velocity">Finite planar velocity; receives the velocity at the end of the interval.</param>
        /// <param name="input">Finite two-axis command: X maps to world X and Y maps to world Z.</param>
        /// <param name="settings">Previously validated speed and response rates.</param>
        /// <param name="deltaTime">Positive elapsed simulation time in seconds.</param>
        /// <returns>Requested displacement, including the portion travelled after reaching the target velocity.</returns>
        public static Vector3 Advance(ref Vector3 velocity, Vector2 input, PlayerLocomotionSettings settings, float deltaTime)
        {
            // Cap command magnitude, not preset values: diagonals stay bounded while analog input remains analog.
            input = Vector2.ClampMagnitude(input, 1f);
            Vector3 target = new Vector3(input.x, 0f, input.y) * settings.Speed;
            Vector3 difference = target - velocity;
            float distance = difference.magnitude;
            float rate = input.Equals(Vector2.zero) ? settings.Deceleration : settings.Acceleration;
            float transitionTime = Mathf.Min(deltaTime, distance / rate);

            // Reach the target exactly, or advance along the velocity difference without overshooting it.
            Vector3 nextVelocity = distance <= rate * deltaTime ? target
                : velocity + difference * (rate * deltaTime / distance);
            Vector3 displacement = (velocity + nextVelocity) * (0.5f * transitionTime)
                + target * (deltaTime - transitionTime);
            velocity = nextVelocity;
            return displacement;
        }

        #endregion

        #endregion
    }
}
