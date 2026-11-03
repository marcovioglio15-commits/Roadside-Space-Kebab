using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Positions the existing speaker graphic above its box without creating or replacing UI.</summary>
    internal static class DialogueSpeakerLayout
    {
        #region Methods

        #region Placement

        /// <summary>Fits the label width and aligns its pivot between the box's left and right margins.</summary>
        /// <param name="speaker">Saved speaker text graphic.</param>
        /// <param name="box">Saved dialogue panel used as the horizontal reference.</param>
        /// <param name="position">Zero for left, one for right, or an intermediate position.</param>
        internal static void Apply(Text speaker, RectTransform box, float position)
        {
            RectTransform label = speaker.rectTransform;
            RectTransform parent = label.parent as RectTransform;
            if (parent == null)
                return;
            // Preferred width makes margin alignment apply to the visible name, including short names.
            speaker.alignment = (TextAnchor)((int)speaker.alignment / 3 * 3 + 1);
            label.pivot = new Vector2(position, label.pivot.y);
            label.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(speaker.preferredWidth, box.rect.width));
            Vector3 local = label.localPosition;
            local.x = parent.InverseTransformPoint(box.TransformPoint(new Vector3(Mathf.Lerp(box.rect.xMin, box.rect.xMax, position), 0f))).x;
            label.localPosition = local;
        }

        #endregion

        #endregion
    }
}
