using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Lets external suspension systems identify bodies eligible for the player's floating contact policy.</summary>
    public interface IPlayerSuspensionSource
    {
        #region Methods

        /// <summary>Checks active suspension ownership without searching the scene or relying on useGravity alone.</summary>
        /// <param name="body">Body contacted by the player's native controller.</param>
        /// <returns>True when an active suspension currently owns this body.</returns>
        bool IsBodySuspended(Rigidbody body);

        #endregion
    }
}
