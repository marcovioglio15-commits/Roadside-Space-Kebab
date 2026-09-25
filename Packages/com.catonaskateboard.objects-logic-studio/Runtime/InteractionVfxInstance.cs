using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Owns the finite lifetime of one requested effect without constructing runtime UI.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    internal sealed class InteractionVfxInstance : MonoBehaviour
    {
        #region State

        private ObjectInteraction owner;
        private ParticleSystem[] particles;
        private Animator[] animators;
        private bool[] playing;
        private float[] speeds;
        private float expires;
        private bool automatic;
        private bool paused;
        private bool stopping;

        #endregion

        #region Methods

        #region Creation

        /// <summary>Creates one validated visual prefab and starts its bounded lifetime.</summary>
        /// <param name="owner">Exact interaction component requesting its independent effect.</param>
        /// <param name="settings">Validated prefab, placement and timing.</param>
        /// <param name="duration">Resolved manual or automatic lifetime.</param>
        internal static void Create(ObjectInteraction owner, InteractionVfxSettings settings, float duration)
        {
            // Instantiation happens only on successful starts, never while checking interaction eligibility.
            GameObject created = Instantiate(settings.Prefab, owner.transform.TransformPoint(settings.Position),
                owner.transform.rotation * Quaternion.Euler(settings.Rotation));
            SceneManager.MoveGameObjectToScene(created, owner.gameObject.scene);
            created.transform.localScale = Vector3.Scale(created.transform.localScale, settings.Scale);
            if (settings.FollowObject)
                created.transform.SetParent(owner.transform, true);
            InteractionVfxInstance effect = created.AddComponent<InteractionVfxInstance>();
            effect.owner = owner;
            effect.automatic = settings.AutoTiming;
            effect.expires = Time.time + duration;
            // Only timed interactions need playback snapshots for pause and resume.
            if (effect.automatic)
            {
                effect.particles = created.GetComponentsInChildren<ParticleSystem>(true);
                effect.animators = created.GetComponentsInChildren<Animator>(true);
                effect.playing = new bool[effect.particles.Length];
                effect.speeds = new float[effect.animators.Length];
            }
        }

        #endregion

        #region Lifetime

        /// <summary>Advances the effect lifetime and follows pauses only for automatic interaction timing.</summary>
        private void Update()
        {
            // A manual world-space effect may finish after its source has disappeared.
            if (stopping)
                return;
            if (automatic)
            {
                if (owner == null || !owner.isActiveAndEnabled || !owner.VfxRunning)
                {
                    Stop();
                    return;
                }
                SetPaused(owner.VfxPaused);
                if (paused)
                {
                    expires += Time.deltaTime;
                    return;
                }
            }
            if (Time.time >= expires)
                Stop();
        }

        /// <summary>Pauses existing particle and animator playback only when the interaction's pause state changes.</summary>
        /// <param name="requested">Whether the source timed interaction is paused.</param>
        private void SetPaused(bool requested)
        {
            // Playback discovery is cached at creation; ordinary frames perform no component searches.
            if (paused == requested)
                return;
            paused = requested;
            for (int index = 0; index < particles.Length; index++)
                if (particles[index] != null)
                {
                    if (paused)
                    {
                        playing[index] = particles[index].isPlaying;
                        particles[index].Pause(false);
                    }
                    else if (playing[index])
                        particles[index].Play(false);
                }
            for (int index = 0; index < animators.Length; index++)
                if (animators[index] != null)
                {
                    if (paused)
                    {
                        speeds[index] = animators[index].speed;
                        animators[index].speed = 0f;
                    }
                    else
                        animators[index].speed = speeds[index];
                }
        }

        /// <summary>Removes a completed or superseded effect exactly once.</summary>
        internal void Stop()
        {
            // Hiding immediately prevents a completed effect from being rendered for one extra frame.
            if (stopping)
                return;
            stopping = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        /// <summary>Prevents a following effect from remaining orphaned when its hierarchy is deactivated.</summary>
        private void OnDisable()
        {
            // Destruction is safe during activation callbacks; no hierarchy reparenting occurs here.
            if (!stopping && Application.isPlaying)
            {
                stopping = true;
                Destroy(gameObject);
            }
        }

        #endregion

        #endregion
    }
}
