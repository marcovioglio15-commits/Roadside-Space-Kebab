using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Presents dialogue pages through one existing panel on the scene Canvas.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Dialogue HUD")]
    public sealed class DialogueHud : ObserverHudPanel
    {
        #region Serialized Fields

        [Header("Dialogue Graphics")]
        [Tooltip("Existing text graphic receiving the current dialogue page.")]
        [SerializeField]
        private Text body;
        [Tooltip("Optional existing text graphic receiving the speaker name.")]
        [SerializeField]
        private Text speaker;

        #endregion

        #region Methods

        #region Presentation

        /// <summary>Checks existing text references before the scene HUD claims this panel.</summary>
        /// <returns>True when the page and optional speaker belong to this panel.</returns>
        public override bool IsValid()
        {
            return base.IsValid() && body != null && body.font != null && body.transform.IsChildOf(Panel)
                && (speaker == null || speaker.transform.IsChildOf(Panel));
        }

        /// <summary>Updates one authored page without altering sibling panels or input ownership.</summary>
        /// <param name="line">Current dialogue page and speaker.</param>
        internal void Show(DialogueLine line)
        {
            // Content changes only at page boundaries.
            body.text = line.Text;
            if (speaker != null)
            {
                speaker.text = line.Speaker;
                speaker.enabled = !string.IsNullOrEmpty(line.Speaker);
            }
            ShowPanel();
        }

        #endregion

        #endregion
    }
}
