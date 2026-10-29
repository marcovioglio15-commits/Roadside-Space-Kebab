using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Retains an exact interaction destination for backward and forward navigation.</summary>
    [Serializable]
    internal sealed class ObjectNavigationStep
    {
        #region Fields

        [Header("History")]
        [Tooltip("Object to reopen.")]
        public ObjectWorkspaceTarget Target;
        [Tooltip("Interaction to reopen.")]
        public long ComponentId;
        [Tooltip("Interaction name shown in navigation history.")]
        public string Name;

        #endregion
    }
}
