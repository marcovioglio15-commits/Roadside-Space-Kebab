using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Tracks footsteps without allocating objects or changing the motor's movement frame.</summary>
    internal struct PlayerHeadTiltMotion
    {
        #region State

        private float phase;
        private float variation;
        private Vector3 offset;
        private float roll;

        #endregion

        #region Methods

        #region Presentation

        /// <summary>Adds a smoothed footstep offset to the fresh camera pose, never to last frame's result.</summary>
        /// <param name="settings">Validated first-person camera configuration.</param>
        /// <param name="velocity">Achieved world velocity from the controller.</param>
        /// <param name="grounded">Whether the controller has downward support.</param>
        /// <param name="deltaTime">Elapsed render time in seconds.</param>
        /// <param name="position">Base camera position receiving the displacement.</param>
        /// <param name="rotation">Base camera rotation receiving lateral roll.</param>
        internal void Apply(PlayerHeadTiltSettings settings, Vector3 velocity, bool grounded, float deltaTime,
            ref Vector3 position, ref Quaternion rotation)
        {
            // Pitch never turns forward motion into a lateral step or a vertical movement request.
            velocity.y = 0f;
            float speed = settings.GroundedOnly && !grounded ? 0f : velocity.magnitude;
            float strength = Mathf.Clamp01(speed / settings.ReferenceSpeed);
            Quaternion heading = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
            float side = speed > 0.001f ? Vector3.Dot(heading * Vector3.right, velocity) / speed : 0f;
            bool irregular = settings.Irregular && (settings.AmplitudeVariation > 0f || settings.CadenceVariation > 0f);
            float noise = irregular ? Mathf.PerlinNoise((settings.Seed & 65535) * 0.031f, variation) * 2f - 1f : 0f;
            phase = Mathf.Repeat(phase + speed * deltaTime / settings.StrideLength * Mathf.PI * 2f
                * (irregular ? 1f + noise * settings.CadenceVariation : 1f), Mathf.PI * 2f);
            if (irregular)
                variation = Mathf.Repeat(variation + speed * deltaTime / settings.StrideLength * settings.VariationRate, 4096f);

            // Side amplitude retains its sign; lean cannot flip away from the movement direction.
            float amplitude = strength * (irregular ? 1f + noise * settings.AmplitudeVariation : 1f);
            Vector3 desired = new Vector3(side * settings.SideAmplitude * Mathf.Sin(phase),
                settings.Height * Mathf.Sin(phase * 2f), 0f) * amplitude;
            float response = 1f - Mathf.Exp(-settings.Response * deltaTime);
            offset = Vector3.Lerp(offset, desired, response);
            roll = Mathf.Lerp(roll, settings.Lean > 0f
                ? -side * strength * (settings.Lean + settings.RollAmplitude * Mathf.Sin(phase)) : 0f, response);
            position += heading * offset;
            rotation *= Quaternion.Euler(0f, 0f, roll);
        }

        #endregion

        #endregion
    }
}
