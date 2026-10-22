using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Resolves day-specific scene links outside the frame loop and resets scene animation state.</summary>
    internal sealed class SpawnFlowSceneState : IDisposable
    {
        #region State

        private readonly List<TriggerAnimationReset> resets = new List<TriggerAnimationReset>();

        #endregion

        #region Methods
        #region Boundaries

        /// <summary>Returns the day's gate only when its exact card is available in the active gameplay scene.</summary>
        /// <param name="day">Day about to start its first spawn.</param>
        /// <param name="interaction">Receives the selected scene card, or null when no gate is required.</param>
        /// <returns>True when the gate is ready to observe or disabled.</returns>
        internal static bool Bind(SpawnFlowDay day, out ObjectInteraction interaction)
        {
            interaction = day.WaitBeforeFirstSpawn ? day.StartInteraction.Resolve(SceneManager.GetActiveScene()) : null;
            return !day.WaitBeforeFirstSpawn || interaction != null;
        }

        /// <summary>Captures the finishing day's reset targets once, including inactive components.</summary>
        /// <param name="day">Finishing day's reset selection.</param>
        /// <param name="owner">Manager used as warning context for missing links.</param>
        /// <returns>A batch that retains animation suppression until the next day starts.</returns>
        internal static SpawnFlowSceneState Reset(SpawnFlowDay day, ObjectSpawnManager owner)
        {
            SpawnFlowSceneState state = new SpawnFlowSceneState();
            Scene scene = SceneManager.GetActiveScene();
            switch (day.ResetAnimations)
            {
                case DayAnimationReset.All:
                    // Discovery is confined to day end, including animations currently disabled by other interactions.
                    foreach (GameObject root in scene.GetRootGameObjects())
                        foreach (ObjectTriggerAnimation animation in root.GetComponentsInChildren<ObjectTriggerAnimation>(true))
                            state.Add(animation);
                    break;
                case DayAnimationReset.Selected:
                    foreach (SceneInteractionReference reference in day.AnimationsToReset)
                        if (reference.Resolve(scene) is ObjectTriggerAnimation animation)
                            state.Add(animation);
                        else
                            Debug.LogWarning("Day Flow could not resolve a selected animation to reset: " + reference.Path, owner);
                    break;
            }
            return state;
        }

        /// <summary>Retains each reset once, even when a selection repeats the same scene animation.</summary>
        /// <param name="animation">Resolved animation whose live pose must return to its initial endpoint.</param>
        private void Add(ObjectTriggerAnimation animation)
        {
            TriggerAnimationReset reset = animation.ResetToInitial();
            if (reset != null && !resets.Contains(reset))
                resets.Add(reset);
        }

        /// <summary>Advances all returns together while waiting for the slowest target.</summary>
        /// <param name="deltaTime">Scaled elapsed time; pause contributes no progress.</param>
        /// <returns>True once every retained animation has completed or disappeared.</returns>
        internal bool Tick(float deltaTime)
        {
            bool complete = true;
            // Every reset receives the same frame time regardless of its position in the batch.
            foreach (TriggerAnimationReset reset in resets)
                complete &= reset.Tick(deltaTime);
            return complete;
        }

        /// <summary>Releases day ownership without forcing incomplete animations to an endpoint.</summary>
        public void Dispose()
        {
            foreach (TriggerAnimationReset reset in resets)
                reset.Dispose();
            resets.Clear();
        }

        #endregion
        #endregion
    }
}
