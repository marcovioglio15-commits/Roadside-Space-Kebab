using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Writes to existing HUD or spatial text without constructing graphics.</summary>
    internal static class CounterText
    {
        #region Methods

        #region Presentation

        /// <summary>Updates whichever saved text bindings are present.</summary>
        /// <param name="hud">Optional uGUI text.</param>
        /// <param name="spatial">Optional TMP text.</param>
        /// <param name="value">Already formatted counter value.</param>
        /// <param name="color">Configured text color.</param>
        internal static void Write(Text hud, TMP_Text spatial, string value, Color color)
        {
            // Both representations share the same formatted value.
            if (hud != null)
            {
                hud.text = value;
                hud.color = color;
            }
            if (spatial != null)
            {
                spatial.text = value;
                spatial.color = color;
            }
        }

        /// <summary>Checks spatial text without relaxing other gameplay panels' Canvas rules.</summary>
        /// <param name="text">Existing spatial text binding.</param>
        /// <returns>True for mesh text or text on a world-space Canvas.</returns>
        internal static bool IsSpatial(TMP_Text text)
        {
            // A screen-space TMP label is not a world counter.
            return text is TextMeshPro || text is TextMeshProUGUI && text.canvas != null
                && text.canvas.renderMode == RenderMode.WorldSpace;
        }

        #endregion

        #endregion
    }
}
