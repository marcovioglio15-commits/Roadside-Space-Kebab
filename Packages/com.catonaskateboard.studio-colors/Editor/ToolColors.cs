using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Provides opt-in, persistent text and button colors for custom tools.</summary>
    public static class ToolColors
    {
        #region Methods
        #region Registration
        /// <summary>Registers live coloring and a context menu on an existing element.</summary>
        /// <param name="element">Label, button or container owned by the calling tool.</param>
        /// <param name="key">Stable, unique key such as AudioStudio.Apply.</param>
        public static void Register(VisualElement element, string key)
        {
            // Capture authored inline styles so reset also respects styles supplied by USS.
            StyleColor text = element.style.color;
            StyleColor background = element.style.backgroundColor;
            System.Action refresh = () =>
            {
                ElementRule rule = ColorSettings.instance.FindElement(key);
                element.style.color = rule != null && rule.OverrideText ? new StyleColor(rule.Text) : text;
                element.style.backgroundColor = rule != null && rule.OverrideBackground ? new StyleColor(rule.Background) : background;
            };
            element.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                ColorSettings.Changed -= refresh;
                ColorSettings.Changed += refresh;
                refresh();
            });
            element.RegisterCallback<DetachFromPanelEvent>(evt => ColorSettings.Changed -= refresh);
            element.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Colors/Edit", action => StudioColorsWindow.EditElement(key));
                evt.menu.AppendAction("Colors/Reset", action => ColorSettings.instance.RemoveElement(key));
            }));
            if (element.panel != null)
                ColorSettings.Changed += refresh;
            refresh();
        }

        /// <summary>Creates a colored IMGUI style without mutating Unity's shared styles.</summary>
        /// <param name="key">The same stable key used by UI Toolkit controls.</param>
        /// <param name="source">Original style to clone once when constructing a tool.</param>
        /// <returns>A private style carrying the configured text color.</returns>
        public static GUIStyle CreateStyle(string key, GUIStyle source)
        {
            // IMGUI consumers cache this result and rebuild on ColorSettings.Changed.
            GUIStyle result = new GUIStyle(source);
            ElementRule rule = ColorSettings.instance.FindElement(key);
            if (rule != null && rule.OverrideText)
            {
                result.normal.textColor = rule.Text;
                result.hover.textColor = rule.Text;
                result.active.textColor = rule.Text;
                result.focused.textColor = rule.Text;
            }
            return result;
        }
        #endregion
        #endregion
    }
}
