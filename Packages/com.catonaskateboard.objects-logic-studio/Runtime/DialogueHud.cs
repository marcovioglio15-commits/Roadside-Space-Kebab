using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Presents explicit dialogue pages using UI authored on the prefab before Play.</summary>
    [DisallowMultipleComponent]
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

        /// <summary>Checks that existing graphics belong to the supplied interaction hierarchy.</summary>
        /// <param name="owner">Interaction object owning this prefab-local HUD.</param>
        /// <returns>True when the canvas and page text can be used safely.</returns>
        public bool IsValid(Transform owner)
        {
            // External scene UI cannot become a hidden dependency of a reusable object prefab.
            return canvas != null && body != null && transform.IsChildOf(owner) && canvas.transform.IsChildOf(transform)
                && body.transform.IsChildOf(canvas.transform) && (speaker == null || speaker.transform.IsChildOf(canvas.transform));
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
