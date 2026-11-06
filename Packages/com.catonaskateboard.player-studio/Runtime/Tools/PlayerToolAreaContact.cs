using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Captures a nearby surface reached by a confirmed weapon impact.</summary>
    public readonly struct PlayerToolAreaContact
    {
        #region Properties

        /// <summary>Collider reached by the area query.</summary>
        public Collider Collider { get; }
        /// <summary>Closest sampled point on the target.</summary>
        public Vector3 Point { get; }
        /// <summary>Distance from the primary impact to the target surface.</summary>
        internal float Distance { get; }

        #endregion

        #region Methods

        #region Construction

        /// <summary>Stores a nearby contact before interaction callbacks can change the scene.</summary>
        /// <param name="collider">Target shape reached by the impact.</param>
        /// <param name="point">Sampled world point on the shape.</param>
        /// <param name="distance">Distance from the centre of the impact.</param>
        internal PlayerToolAreaContact(Collider collider, Vector3 point, float distance)
        {
            Collider = collider;
            Point = point;
            Distance = distance;
        }

        #endregion

        #endregion
    }
}
