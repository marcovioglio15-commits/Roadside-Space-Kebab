using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Owns one preauthored panel on the scene Canvas without changing its hierarchy or Canvas state.</summary>
    public abstract class ObserverHudPanel : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Shared Scene UI")]
        [Tooltip("Scene Canvas shared with the other gameplay panels.")]
        [SerializeField]
        private Canvas canvas;
        [Tooltip("Existing child rectangle hidden independently of the shared Canvas.")]
        [SerializeField]
        private RectTransform panel;

        #endregion

        #region State

        private HoverObserver owner;

        #endregion

        #region Properties

        /// <summary>Shared rendering surface used for screen-to-UI coordinate conversion.</summary>
        public Canvas Canvas => canvas;
        /// <summary>Existing presentation rectangle, authored before Play.</summary>
        public RectTransform Panel => panel;
        /// <summary>Whether an active observer owns this usable scene panel.</summary>
        internal bool Ready => owner != null && isActiveAndEnabled && canvas != null && canvas.isActiveAndEnabled && panel != null;

        #endregion

        #region Methods

        #region Ownership

        /// <summary>Starts hidden without disabling sibling gameplay panels.</summary>
        protected virtual void OnEnable()
        {
            Hide();
        }

        /// <summary>Releases presentation when the scene or the panel host is disabled.</summary>
        protected virtual void OnDisable()
        {
            Hide();
            owner = null;
        }

        /// <summary>Claims an already validated panel for the current observer.</summary>
        /// <param name="observer">Observer controlling this scene's gameplay view.</param>
        internal void Bind(HoverObserver observer)
        {
            if (owner == observer)
                return;
            Hide();
            owner = observer;
        }

        /// <summary>Releases this observer's panel without affecting another owner's presentation.</summary>
        /// <param name="observer">Observer leaving gameplay.</param>
        internal void Unbind(HoverObserver observer)
        {
            if (owner != observer)
                return;
            Hide();
            owner = null;
        }

        /// <summary>Checks exclusive presentation ownership before an interaction starts.</summary>
        /// <param name="observer">Observer requesting the panel.</param>
        /// <returns>True for the current owner of a usable panel.</returns>
        internal bool IsOwnedBy(HoverObserver observer) => Ready && owner == observer;

        #endregion

        #region Presentation

        /// <summary>Hides only this panel, retaining its authored graphics for the next interaction.</summary>
        public virtual void Hide()
        {
            if (panel != null && panel.gameObject.activeSelf)
                panel.gameObject.SetActive(false);
        }

        /// <summary>Reveals existing graphics after their content and layout have been updated.</summary>
        protected void ShowPanel()
        {
            if (!panel.gameObject.activeSelf)
                panel.gameObject.SetActive(true);
        }

        /// <summary>Validates the shared hierarchy once at binding or authoring boundaries.</summary>
        /// <returns>True when the panel belongs to an existing screen-space scene Canvas.</returns>
        public virtual bool IsValid()
        {
            // Panels must not disable their controller or own nested rendering surfaces.
            return canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay && transform.IsChildOf(canvas.transform)
                && panel != null && panel != transform && panel.IsChildOf(transform)
                && GetComponentsInChildren<Canvas>(true).Length == 0
                && GetComponentsInChildren<Rigidbody>(true).Length == 0 && GetComponentsInChildren<Rigidbody2D>(true).Length == 0;
        }

        #endregion

        #endregion
    }
}
