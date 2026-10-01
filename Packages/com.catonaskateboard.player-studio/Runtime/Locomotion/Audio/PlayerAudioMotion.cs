using CatOnASkateboard.AudioStudio;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Uses existing controller contacts and camera cadence without extra per-frame physics queries.</summary>
    internal sealed class PlayerAudioMotion
    {
        #region State

        private PlayerAudioSettings settings;
        private Transform source;
        private float nextStep;
        private float nextImpact;
        private int groundLayer;
        private bool moving;

        #endregion

        #region Methods
        #region Playback

        /// <summary>Captures validated settings when the motor initializes.</summary>
        /// <param name="configuration">Audio settings from the locomotion preset.</param>
        /// <param name="player">Emitter and diagnostic owner.</param>
        internal void Bind(PlayerAudioSettings configuration, Transform player)
        {
            // Invalid audio leaves locomotion operational and reports its own warning.
            settings = null;
            source = player;
            nextStep = nextImpact = 0f;
            moving = false;
            groundLayer = -1;
            if (configuration != null && configuration.TryValidate(out string warning))
                settings = configuration;
            else
                Debug.LogWarning("Review the Player Studio locomotion audio settings.", player);
        }

        /// <summary>Schedules interval footsteps only when the camera is not providing a step phase.</summary>
        /// <param name="velocity">Achieved controller velocity.</param>
        /// <param name="grounded">Whether the current move has support below.</param>
        /// <param name="headTilt">Whether camera step timing is currently active.</param>
        internal void Tick(Vector3 velocity, bool grounded, bool headTilt)
        {
            // Wall pushing and airborne motion cannot generate regular footsteps.
            moving = settings != null && grounded && velocity.x * velocity.x + velocity.z * velocity.z
                > settings.MinimumSpeed * settings.MinimumSpeed;
            if (!moving)
                nextStep = Time.time;
            else if (!headTilt && Time.time >= nextStep)
            {
                Step();
                nextStep = Time.time + settings.Interval;
            }
        }

        /// <summary>Plays one step at either a head-tilt trough or a fallback interval.</summary>
        internal void Step()
        {
            // Resolve layers only at actual step boundaries, not on every frame.
            if (!moving || settings == null || !settings.Footsteps || Time.timeScale <= 0f)
                return;
            FootstepSurface surface = settings.DefaultSurface;
            foreach (FootstepLayers row in settings.Surfaces)
                if (groundLayer >= 0 && (row.Layers.value & 1 << groundLayer) != 0)
                {
                    surface = row.Surface;
                    break;
                }
            StudioAudio.Play("sfx_footstep", source, "Surface", surface.ToString());
        }

        /// <summary>Records support and filters impact intensity along the actual contact normal.</summary>
        /// <param name="hit">Controller contact from the current Move.</param>
        /// <param name="velocity">Requested displacement divided by its duration, including gravity.</param>
        /// <param name="supported">Whether the preceding move was already grounded.</param>
        internal void Contact(ControllerColliderHit hit, Vector3 velocity, bool supported)
        {
            // Persistent ground adhesion is not a landing impact.
            if (hit.normal.y > 0.5f)
            {
                groundLayer = hit.collider.gameObject.layer;
                if (supported)
                    return;
            }
            if (settings == null || !settings.Collision.Enabled || Time.time < nextImpact)
                return;
            if (hit.rigidbody != null)
                velocity -= hit.rigidbody.GetPointVelocity(hit.point);
            float speed = -Vector3.Dot(velocity, hit.normal);
            if (speed < settings.Collision.MinimumSpeed)
                return;
            nextImpact = Time.time + settings.Collision.Cooldown;
            StudioAudio.Play("sfx_playercollision", source);
        }

        #endregion
        #endregion
    }
}
