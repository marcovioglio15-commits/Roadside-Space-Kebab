using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Animates one shared scene label using the selected interaction's existing configuration.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1100)]
    [AddComponentMenu("Objects Logic Studio/Hover Label")]
    public sealed class HoverLabel : ObserverHudPanel
    {
        #region Serialized Fields

        [Header("Hover Graphics")]
        [Tooltip("Font used when a hover leaves its optional font override empty.")]
        [SerializeField]
        private Font defaultFont;

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
        private float previousTime;
        private bool requested;
        private Vector3 trackedAnchor;
        private float amount;
        private float scale;
        private float fromAmount;
        private float fromScale;
        private ObjectHover source;

        #endregion

        #region Properties

        /// <summary>Authored fallback restored when switching from an item with a custom font.</summary>
        internal Font DefaultFont => defaultFont;
        /// <summary>Existing typography and content component.</summary>
        public Text Text => text;
        /// <summary>Optional preauthored background.</summary>
        public Image Background => background;

        #endregion

        #region Methods

        #region Presentation

        /// <summary>Reuses the same graphics for the selected hover, retaining only its optional exit animation.</summary>
        /// <param name="observer">Observer owning the scene Canvas.</param>
        /// <param name="selected">Current hover winner, or null while the previous label exits.</param>
        /// <param name="time">Shared unscaled presentation time.</param>
        internal void Present(HoverObserver observer, ObjectHover selected, float time)
        {
            // Another interaction callback may have invalidated the selected target.
            if (selected != null && !selected.IsHovered)
                selected = null;
            if (selected != null && source != selected)
            {
                Hide();
                source = selected;
                source.Configuration.Style.Apply(this);
            }
            if (!Ready || source == null || !source.isActiveAndEnabled)
            {
                Hide();
                return;
            }
            Animate(observer.View, source.WorldAnchor, source.Settings, time, source == selected);
        }

        /// <summary>Removes a destroyed, disabled or consumed owner's text without affecting another target.</summary>
        /// <param name="interaction">Object whose hover presentation is ending.</param>
        internal void Release(ObjectHover interaction)
        {
            if (source == interaction)
                Hide();
        }

        /// <summary>Clears animation and source ownership while preserving the shared Canvas.</summary>
        public override void Hide()
        {
            shown = false;
            requested = false;
            source = null;
            base.Hide();
        }

        /// <summary>Projects current object motion every frame and reverses entry or exit without restarting from the anchor.</summary>
        /// <param name="view">Observer camera used for pixel projection and target display.</param>
        /// <param name="anchor">Detection anchor in world space.</param>
        /// <param name="settings">Validated placement and animation settings.</param>
        /// <param name="time">Current unscaled time shared by all interactions.</param>
        /// <param name="visible">Whether detection currently requests a visible label.</param>
        private void Animate(Camera view, Vector3 anchor, HoverSettings settings, float time, bool visible)
        {
            // Invisible labels do no projection work; optional exits retain their existing canvas.
            if (!shown && !visible)
                return;
            if (!visible && !settings.PopIn)
            {
                Hide();
                return;
            }
            if (!shown)
            {
                amount = 0f;
                scale = settings.Appearance == HoverAppearance.PopUp ? settings.StartScale : 1f;
                trackedAnchor = anchor;
                previousTime = time;
                shown = true;
                requested = false;
                ShowPanel();
            }
            if (requested != visible)
            {
                requested = visible;
                appearedAt = time;
                fromAmount = amount;
                fromScale = scale;
            }
            float duration = visible ? settings.Appearance == HoverAppearance.Instant ? 0f : settings.Duration : settings.ExitDuration;
            float progress = duration > 0f ? Mathf.Clamp01((time - appearedAt) / duration) : 1f;
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            amount = Mathf.Lerp(fromAmount, visible ? 1f : 0f, eased);
            scale = Mathf.Lerp(fromScale, visible ? 1f : 0f, eased);
            if (!visible && progress >= 1f)
            {
                Hide();
                return;
            }

            float pixelScale = view.pixelHeight / 1080f;
            if (settings.FixedHud)
            {
                Position(view.pixelRect.center + settings.ScreenOffset * pixelScale, pixelScale);
                previousTime = time;
                return;
            }
            // Smooth world motion only; fresh camera projection avoids lag when the player turns.
            trackedAnchor = Vector3.Lerp(trackedAnchor, anchor, settings.FollowSmoothing > 0f
                ? 1f - Mathf.Exp(-(time - previousTime) / settings.FollowSmoothing) : 1f);
            previousTime = time;
            // A label offset behind the camera must never create a mirrored overlay.
            Vector3 origin = view.WorldToScreenPoint(trackedAnchor);
            Vector3 destination = view.WorldToScreenPoint(trackedAnchor + settings.WorldOffset);
            if (origin.z < view.nearClipPlane || destination.z < view.nearClipPlane)
            {
                Hide();
                return;
            }

            Vector2 screenPoint = Vector2.Lerp(origin, (Vector2)destination + settings.ScreenOffset * pixelScale, amount);
            Position(screenPoint, pixelScale);
        }

        /// <summary>Places the existing panel consistently for world-linked labels and fixed HUD labels.</summary>
        /// <param name="screenPoint">Final screen pixel position including viewport offset.</param>
        /// <param name="pixelScale">Reference-height scaling shared by placement and size.</param>
        private void Position(Vector2 screenPoint, float pixelScale)
        {
            // Convert actual screen pixels to the existing canvas, including partial camera viewports.
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)Canvas.transform, screenPoint, null, out Vector3 position))
                Panel.position = position;
            Panel.localScale = Vector3.one * (scale * pixelScale / Canvas.scaleFactor);
        }

        #endregion

        #region Validation

        /// <summary>Checks shared graphics independently of any individual interaction hierarchy.</summary>
        /// <returns>True when the label can render every item's local style on the scene Canvas.</returns>
        public override bool IsValid()
        {
            return base.IsValid() && text != null && defaultFont != null && text.canvasRenderer != null
                && text.transform.IsChildOf(Panel) && !text.raycastTarget
                && (background == null || background.transform.IsChildOf(Panel) && !background.raycastTarget);
        }

        #endregion

        #endregion
    }
}
