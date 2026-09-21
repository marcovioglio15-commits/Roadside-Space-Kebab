using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Releases a carried object in place with its independently configured fall and contact response.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ObjectGrab))]
    [AddComponentMenu("Objects Logic Studio/Object Drop")]
    public sealed class ObjectDrop : ObjectRelease
    {
        #region Properties

        /// <summary>Identifies the Drop feature in the tool.</summary>
        public override SingleInteractionKind Kind => SingleInteractionKind.Drop;

        #endregion
    }
}
