using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Stages one interaction addition or removal until the object is applied.</summary>
    [Serializable]
    internal sealed class ObjectStructureChange
    {
        #region Fields

        [Header("Operation")]
        [Tooltip("Object to change.")]
        public ObjectWorkspaceTarget Target;
        [Tooltip("Interaction type to add or remove.")]
        public string TypeName;
        [Tooltip("Component to remove. Names are not used to choose it.")]
        public long ComponentId;
        [Tooltip("Add the interaction. Otherwise remove it.")]
        public bool Add;

        #endregion
    }
}
