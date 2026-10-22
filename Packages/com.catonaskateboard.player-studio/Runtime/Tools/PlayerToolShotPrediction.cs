using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Shares one collision and fitted impact forecast between arm preparation and live bullet simulation.</summary>
    internal readonly struct PlayerToolShotPrediction
    {
        #region Properties

        /// <summary>Whether the swept trajectory reaches a real collider.</summary>
        internal bool Hit { get; }
        /// <summary>Confirmed physical surface, independent of the visual retreat.</summary>
        internal RaycastHit Contact { get; }
        /// <summary>Whether the impact envelope fits at its predicted visual endpoint.</summary>
        internal bool HasImpact { get; }
        /// <summary>Fitted impact origin, or the physical flight endpoint when fitting failed.</summary>
        internal Vector3 Position { get; }
        /// <summary>Selected world impact orientation.</summary>
        internal Quaternion Rotation { get; }
        /// <summary>Fallback scale relative to the prepared impact pool.</summary>
        internal float Scale { get; }

        #endregion

        #region Methods

        #region Construction

        /// <summary>Captures a forecast without spawning visuals, emitting contacts or changing scene transforms.</summary>
        /// <param name="hit">Whether a physical surface was found.</param>
        /// <param name="contact">Nearest swept contact.</param>
        /// <param name="hasImpact">Whether a visual envelope fits.</param>
        /// <param name="position">Visual origin or physical endpoint.</param>
        /// <param name="rotation">Final visual rotation.</param>
        /// <param name="scale">Visual fit multiplier.</param>
        internal PlayerToolShotPrediction(bool hit, RaycastHit contact, bool hasImpact, Vector3 position, Quaternion rotation, float scale)
        {
            Hit = hit;
            Contact = contact;
            HasImpact = hasImpact;
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        #endregion

        #endregion
    }
}
