using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Optionally aligns a saved spatial counter with the gameplay camera.</summary>
    [DisallowMultipleComponent]
    public sealed class CounterFacing : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Facing")]
        [Tooltip("Optional camera. Empty uses the current Observer camera.")]
        [SerializeField]
        private Camera view;

        #endregion

        #region Methods

        #region Presentation

        /// <summary>Runs only when this optional component is enabled.</summary>
        private void LateUpdate()
        {
            // The observer already caches its active camera.
            Camera current = view != null ? view : HoverObserver.ActiveView;
            if (current != null)
                transform.rotation = current.transform.rotation;
        }

        #endregion

        #endregion
    }
}
