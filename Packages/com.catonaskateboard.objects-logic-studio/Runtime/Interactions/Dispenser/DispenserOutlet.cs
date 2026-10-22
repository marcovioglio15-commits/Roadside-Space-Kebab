using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Defines a local spawn pose edited against the dispenser's existing geometry.</summary>
    [Serializable]
    public sealed class DispenserOutlet
    {
        #region Fields

        [Tooltip("Output position relative to the dispenser root.")]
        public Vector3 Position;
        [Tooltip("Output Euler rotation relative to the dispenser root. The arrow shows positive Z.")]
        public Vector3 Rotation;

        #endregion

        #region Properties

        /// <summary>Whether the output can be transformed without invalid coordinates.</summary>
        public bool IsValid => InteractionValues.Finite(Position) && InteractionValues.Finite(Rotation);

        #endregion
    }
}
