using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Displays the current order stopwatch using text authored on the shared gameplay Canvas.</summary>
    [DisallowMultipleComponent]
    public sealed class OrderTimerHud : ObserverHudPanel
    {
        #region Fields

        [Header("Stopwatch")]
        [Tooltip("Existing HUD text. Font, alignment, size and layout remain editable on this component's Text.")]
        [SerializeField]
        private Text text;
        [Tooltip("Text tint while the customer's orders are being timed.")]
        [SerializeField]
        private Color runningColor = Color.white;
        [Tooltip("Text tint after Day Flow accepts the customer's final completion.")]
        [SerializeField]
        private Color completedColor = new Color(0.5f, 1f, 0.65f);
        private OrderTimerSession current;
        private long displayed = -1;
        private bool finished;

        #endregion
        #region Methods
        #region Presentation

        /// <summary>Updates digits only when the displayed second or tenth changes.</summary>
        private void Update()
        {
            // Hidden and unbound HUDs allocate no strings and never create replacement graphics.
            OrderTimerSession next = OrderTimerSession.Current(gameObject.scene);
            if (!Ready || next == null || next.Completed && !next.Settings.KeepTimerResult)
            {
                Hide();
                return;
            }
            long ticks = (long)(next.Elapsed * (next.Settings.TimerTenths ? 10d : 1d));
            if (!ReferenceEquals(current, next) || displayed != ticks || finished != next.Completed)
            {
                current = next;
                displayed = ticks;
                finished = next.Completed;
                long seconds = next.Settings.TimerTenths ? ticks / 10 : ticks;
                text.text = next.Settings.TimerLabel + " " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00")
                    + (next.Settings.TimerTenths ? "." + (ticks % 10) : string.Empty);
                text.color = finished ? completedColor : runningColor;
            }
            ShowPanel();
        }

        /// <summary>Clears presentation state without changing the customer's clock.</summary>
        public override void Hide()
        {
            current = null;
            displayed = -1;
            base.Hide();
        }

        /// <summary>Validates that the preauthored stopwatch belongs to the shared panel.</summary>
        /// <returns>True when the saved text can be displayed without runtime UI creation.</returns>
        public override bool IsValid()
        {
            return base.IsValid() && text != null && text.transform.IsChildOf(Panel) && !text.raycastTarget;
        }

        #endregion
        #endregion
    }
}
