using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Moves one generated object into place while its interactions and physics remain suspended.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5000)]
    [AddComponentMenu("")]
    internal sealed class SpawnArrival : MonoBehaviour
    {
        #region State

        private SuspendedItemState state;
        private SpawnBatch batch;
        private SpawnAnimationSettings settings;
        private Vector3 endPosition;
        private Quaternion endRotation;
        private Vector3 endScale;
        private float started;
        private bool finished;

        #endregion

        #region Methods

        #region Initialization

        /// <summary>Prepares an inactive clone before any of its interactions can activate.</summary>
        /// <param name="created">Inactive generated prefab root.</param>
        /// <param name="position">Final world position.</param>
        /// <param name="rotation">Final world orientation.</param>
        /// <param name="configuration">Validated transform animation.</param>
        /// <param name="completion">Batch receiving this object's final result.</param>
        internal static void Begin(GameObject created, Vector3 position, Quaternion rotation, SpawnAnimationSettings configuration, SpawnBatch completion)
        {
            // Only explicitly requested spawn animations add this short-lived runtime controller.
            SpawnArrival arrival = created.AddComponent<SpawnArrival>();
            arrival.state = new SuspendedItemState(created);
            arrival.settings = configuration;
            arrival.batch = completion;
            arrival.endPosition = position;
            arrival.endRotation = rotation;
            arrival.endScale = created.transform.localScale;
            arrival.started = Time.time;
            arrival.state.Suspend();
            arrival.Apply(0f);
            created.SetActive(true);
        }

        #endregion

        #region Animation

        /// <summary>Updates only the requested transform animation while gameplay time advances.</summary>
        private void LateUpdate()
        {
            // Original colliders and interactions remain disabled until the complete final pose is restored.
            if (finished || Time.timeScale <= 0f)
                return;
            float progress = (Time.time - started) / settings.Duration;
            if (progress < 1f)
            {
                Apply(settings.Progress.Evaluate(progress));
                return;
            }
            Apply(1f);
            finished = true;
            state.Restore();
            batch.Finish(true);
            Destroy(this);
        }

        /// <summary>Blends the authored starting offset, rotation and scale toward the final prefab pose.</summary>
        /// <param name="progress">Curve-evaluated transform progress.</param>
        private void Apply(float progress)
        {
            // Existing transforms are the only objects changed during animation frames.
            transform.SetPositionAndRotation(Vector3.LerpUnclamped(endPosition + endRotation * settings.Offset, endPosition, progress),
                Quaternion.SlerpUnclamped(endRotation * Quaternion.Euler(settings.Rotation), endRotation, progress));
            transform.localScale = Vector3.Scale(endScale, Vector3.LerpUnclamped(settings.Scale, Vector3.one, progress));
        }

        /// <summary>Releases a cancelled arrival from its batch if the generated object is destroyed early.</summary>
        private void OnDestroy()
        {
            // An aborted spawn cannot masquerade as a completed interaction.
            if (!finished)
            {
                finished = true;
                batch?.Finish(false);
            }
        }

        #endregion

        #endregion
    }
}
