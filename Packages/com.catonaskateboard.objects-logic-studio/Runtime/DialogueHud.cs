using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Presents all dialogue pages through the observer's single authored overlay.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1100)]
    [AddComponentMenu("Objects Logic Studio/Dialogue HUD")]
    public sealed class DialogueHud : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Authored UI")]
        [Tooltip("Existing overlay canvas dedicated to dialogue presentation.")]
        [SerializeField]
        private Canvas canvas;
        [Tooltip("Existing text graphic receiving the current dialogue page.")]
        [SerializeField]
        private Text body;
        [Tooltip("Optional existing text graphic receiving the current speaker name.")]
        [SerializeField]
        private Text speaker;

        #endregion

        #region State

        private Transform originalParent;
        private bool bound;
        private bool detached;
        private bool returnPending;
        private bool ownedChild;
        private HoverObserver owner;

        #endregion

        #region Properties

        /// <summary>Whether the already validated shared overlay can display a page.</summary>
        internal bool Ready => bound && detached && isActiveAndEnabled && canvas != null && body != null;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Starts hidden without instantiating a canvas, panel or text object.</summary>
        private void OnEnable()
        {
            // The dialogue driver is the only owner allowed to display this prefab's HUD.
            Hide();
        }

        /// <summary>Prevents stale text when the owning object is pooled or consumed.</summary>
        private void OnDisable()
        {
            // Keep the authored canvas component's enabled state consistent on reactivation.
            Hide();
        }

        #endregion

        #region Presentation

        /// <summary>Finishes a pending bind before dialogue arbitration at the observer's safe LateUpdate boundary.</summary>
        /// <param name="observer">Observer about to present dialogue through its authored overlay.</param>
        internal void Prepare(HoverObserver observer)
        {
            // Bind remains harmless inside OnEnable; only this frame boundary can detach the overlay.
            if (!bound || detached || owner != observer || !isActiveAndEnabled)
                return;
            transform.SetParent(null, false);
            detached = true;
        }

        /// <summary>Checks the shared overlay and excludes physics components from its UI hierarchy.</summary>
        /// <returns>True when existing graphics can be presented without simulation or runtime construction.</returns>
        public bool IsValid()
        {
            // These hierarchy checks run at setup boundaries, never on each dialogue frame.
            return canvas != null && canvas.transform == transform && canvas.renderMode == RenderMode.ScreenSpaceOverlay
                && body != null && body.transform.IsChildOf(transform)
                && (speaker == null || speaker.transform.IsChildOf(transform))
                && GetComponentsInChildren<Rigidbody>(true).Length == 0
                && GetComponentsInChildren<Rigidbody2D>(true).Length == 0;
        }

        /// <summary>Claims the authored overlay and schedules hierarchy changes outside activation callbacks.</summary>
        /// <param name="observer">Observer claiming this shared presentation.</param>
        internal void Bind(HoverObserver observer)
        {
            // Re-enabling in the same frame keeps the original lifetime relationship.
            if (bound || observer == null || owner != null && owner != observer)
                return;
            if (!IsValid())
            {
                Debug.LogWarning("Dialogue HUD needs an overlay Canvas, valid text references and no UI rigidbodies.", this);
                return;
            }
            owner = observer;
            if (!detached)
            {
                originalParent = transform.parent;
                ownedChild = originalParent != null;
            }
            returnPending = false;
            bound = true;
            Hide();
        }

        /// <summary>Hides immediately and defers reparenting until Unity finishes the activation callback.</summary>
        /// <param name="observer">Observer releasing only its own presentation.</param>
        internal void Unbind(HoverObserver observer)
        {
            // SetParent is illegal while the destination parent is processing SetActive.
            if (owner != observer)
                return;
            Hide();
            bound = false;
            returnPending = true;
        }

        /// <summary>Moves the existing overlay only at a safe frame boundary and preserves its owner's lifetime.</summary>
        private void LateUpdate()
        {
            // A detached child must not survive destruction of its original player hierarchy.
            if (detached && ownedChild && (originalParent == null || owner == null))
            {
                Destroy(gameObject);
                return;
            }
            if (returnPending)
            {
                returnPending = false;
                detached = false;
                owner = null;
                if (originalParent != null)
                    transform.SetParent(originalParent, false);
                originalParent = null;
                ownedChild = false;
            }
            else if (bound && !detached)
                Prepare(owner);
        }

        /// <summary>Checks that this observer owns the ready overlay before starting a dialogue.</summary>
        /// <param name="observer">Observer requesting presentation.</param>
        /// <returns>True only for the current owner of a usable overlay.</returns>
        internal bool IsOwnedBy(HoverObserver observer)
        {
            // Duplicate observers cannot hide or reuse another observer's active presentation.
            return Ready && owner == observer;
        }

        /// <summary>Displays one complete authored page without controlling player input or cursor state.</summary>
        /// <param name="line">Page selected by the dialogue driver.</param>
        internal void Show(DialogueLine line)
        {
            // Text changes only on page boundaries; ordinary frames do no layout or string work.
            body.text = line.Text;
            if (speaker != null)
            {
                speaker.text = line.Speaker;
                speaker.enabled = !string.IsNullOrEmpty(line.Speaker);
            }
            canvas.enabled = true;
        }

        /// <summary>Hides presentation while preserving the interaction's current entry and page.</summary>
        internal void Hide()
        {
            // Hiding the canvas leaves the authored UI hierarchy available for later resumption.
            if (canvas != null)
                canvas.enabled = false;
        }

        #endregion

        #endregion
    }
}
