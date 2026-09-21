using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Animates existing prefab UI; it never creates graphics, fonts or materials during Play.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Objects Logic Studio/Hover Label")]
    public sealed class HoverLabel : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Authored UI")]
        [Tooltip("Dedicated overlay canvas created in Edit mode and enabled only while this label is visible.")]
        [SerializeField]
        private Canvas canvas;

        [Tooltip("Movable panel under the canvas. Its size is authored in pixels at a 1080-pixel viewport height.")]
        [SerializeField]
        private RectTransform panel;

        [Tooltip("Existing text component containing content, font, size, alignment and color.")]
        [SerializeField]
        private Text text;

        [Tooltip("Optional existing background controlled by Show Background in the assigned hover preset.")]
        [SerializeField]
        private Image background;

        #endregion

        #region State

        private bool shown;
        private float appearedAt;

        #endregion

        #region Properties

        /// <summary>Canvas inspected and wired by editor tooling.</summary>
        public Canvas Canvas => canvas;
        /// <summary>Authored layout rectangle.</summary>
        public RectTransform Panel => panel;
        /// <summary>Existing typography and content component.</summary>
        public Text Text => text;
        /// <summary>Optional preauthored background.</summary>
        public Image Background => background;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Prevents authored preview state from leaking into the first rendered frame.</summary>
        private void Awake()
        {
            // A disabled canvas preserves geometry without drawing or accepting input.
            Hide();
        }

        /// <summary>Removes the overlay immediately when its hierarchy is disabled.</summary>
        private void OnDisable()
        {
            // No UI object is destroyed when the interaction disappears.
            Hide();
        }

        #endregion

        #region Presentation

        /// <summary>Stops rendering while retaining the complete authored UI hierarchy.</summary>
        public void Hide()
        {
            // Assign only at the visible-to-hidden transition or initialization.
            shown = false;
            if (canvas != null && canvas.enabled)
                canvas.enabled = false;
        }

        /// <summary>Tracks an eligible object and advances its optional entry animation.</summary>
        /// <param name="view">Observer camera used for pixel projection and target display.</param>
        /// <param name="anchor">Detection anchor in world space.</param>
        /// <param name="settings">Validated placement and animation settings.</param>
        /// <param name="time">Current unscaled time shared by all interactions.</param>
        internal void Present(Camera view, Vector3 anchor, HoverSettings settings, float time)
        {
            // A label offset behind the camera must never create a mirrored overlay.
            Vector3 origin = view.WorldToScreenPoint(anchor);
            Vector3 destination = view.WorldToScreenPoint(anchor + settings.WorldOffset);
            if (origin.z < view.nearClipPlane || destination.z < view.nearClipPlane)
            {
                Hide();
                return;
            }

            // Starting an entry animation is separate from its per-frame movement.
            if (!shown)
            {
                appearedAt = time;
                shown = true;
                canvas.targetDisplay = view.targetDisplay;
                canvas.enabled = true;
            }
            float progress = settings.Appearance == HoverAppearance.Instant ? 1f : Mathf.Clamp01((time - appearedAt) / settings.Duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            float pixelScale = view.pixelHeight / 1080f;
            Vector2 screenPoint = Vector2.Lerp(origin, (Vector2)destination + settings.ScreenOffset * pixelScale, eased);

            // Convert actual screen pixels to the existing canvas, including partial camera viewports.
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)canvas.transform, screenPoint, null, out Vector3 position))
                panel.position = position;
            panel.localScale = Vector3.one * (settings.Appearance == HoverAppearance.Instant
                ? pixelScale : Mathf.Lerp(settings.StartScale, 1f, eased) * pixelScale);
        }

        #endregion

        #region Validation

        /// <summary>Checks ownership and UI references without repairing or replacing authored objects.</summary>
        /// <param name="owner">Interaction whose prefab must contain this label.</param>
        /// <param name="warning">Receives a missing or unsafe reference.</param>
        /// <returns>True when the label can render independently of its object transform.</returns>
        public bool TryValidate(Transform owner, out string warning)
        {
            // Dedicated descendants prevent one label from disabling the object or another canvas.
            warning = string.Empty;
            if (canvas == null || panel == null || text == null || text.font == null)
                warning = "Create the hover UI in Objects Logic Studio and assign a font.";
            else if (transform == owner || !transform.IsChildOf(owner) || canvas.transform != transform
                || panel == transform || !panel.IsChildOf(transform) || !text.transform.IsChildOf(panel)
                || background != null && !background.transform.IsChildOf(panel))
                warning = "Label, canvas, panel and graphics must belong to this object's dedicated UI child.";
            else if (canvas.renderMode != RenderMode.ScreenSpaceOverlay
                || transform.parent != null && transform.parent.GetComponentInParent<Canvas>() != null)
                warning = "Hover UI requires its own Screen Space Overlay canvas.";
            else if (!gameObject.activeSelf || !panel.gameObject.activeSelf || !text.enabled || !text.gameObject.activeSelf)
                warning = "Keep label objects and Text active; visibility is controlled by the canvas.";
            else if (text.raycastTarget || background != null && background.raycastTarget)
                warning = "Disable Raycast Target on hover graphics so labels cannot intercept pointer input.";
            else if (!float.IsFinite(panel.sizeDelta.x) || !float.IsFinite(panel.sizeDelta.y)
                || panel.sizeDelta.x <= 0f || panel.sizeDelta.y <= 0f)
                warning = "Label Size must contain positive, finite dimensions.";
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
