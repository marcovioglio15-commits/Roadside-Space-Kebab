using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Schedules cursor impacts independently of the selected tool or its current animation.</summary>
    internal sealed class PlayerToolCursorTravel
    {
        #region State

        private readonly List<(PlayerToolHit Hit, int Layers, double Arrival)> pending =
            new List<(PlayerToolHit, int, double)>();

        #endregion

        #region Methods

        #region Playback

        /// <summary>Records a shot using scaled game time so pause also stops travel.</summary>
        /// <param name="hit">Original cursor contact and firing context.</param>
        /// <param name="layers">Physics and camera layer mask at emission.</param>
        /// <param name="delay">Flight duration in seconds.</param>
        internal void Add(PlayerToolHit hit, int layers, float delay)
        {
            // Shots in a burst can arrive in a different order from emission.
            double arrival = Time.timeAsDouble + delay;
            int index = pending.Count;
            while (index > 0 && pending[index - 1].Arrival > arrival)
                index--;
            pending.Insert(index, (hit, layers, arrival));
        }

        /// <summary>Dispatches ready contacts before the tool emits new shots for this frame.</summary>
        /// <param name="now">Current scaled time.</param>
        /// <param name="runtime">Weapon that owns the contact and impact effects.</param>
        internal void Advance(double now, PlayerToolRuntime runtime)
        {
            // Remove first because impact callbacks may shut down the player.
            while (pending.Count > 0 && pending[0].Arrival <= now)
            {
                (PlayerToolHit hit, int layers, _) = pending[0];
                pending.RemoveAt(0);
                runtime.ResolveCursorImpact(hit, layers);
            }
        }

        /// <summary>Cancels outstanding contacts when the tool runtime is disposed.</summary>
        internal void Clear()
        {
            pending.Clear();
        }

        #endregion

        #endregion
    }
}

