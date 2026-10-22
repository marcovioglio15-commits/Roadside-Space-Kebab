using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Displays released session earnings on an existing green HUD text without creating UI during gameplay.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class CollectedPriceHud : MonoBehaviour
    {
        #region Fields

        [Header("Counter")]
        [Tooltip("Existing Text displaying the released total followed by a dollar sign. Runtime updates retain the authored layout and font.")]
        [SerializeField]
        private Text text;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Subscribes once and immediately displays earnings accumulated while the HUD was inactive.</summary>
        private void OnEnable()
        {
            // A reactivated label immediately catches up with already released earnings.
            Initialize();
        }

        /// <summary>Stops presentation callbacks while leaving earned money untouched.</summary>
        private void OnDisable()
        {
            // Presentation ownership never changes the session balance.
            CollectedPrice.Changed -= Refresh;
        }

        /// <summary>Restores subscriptions after static session reset when scene reload is disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            // Discovery happens only at Play entry; subsequent updates are event driven.
            foreach (CollectedPriceHud hud in FindObjectsByType<CollectedPriceHud>())
                if (hud.isActiveAndEnabled)
                    hud.Initialize();
        }

        /// <summary>Rebinds this preauthored label without allocating replacement graphics.</summary>
        private void Initialize()
        {
            // The required Text already exists; resolve it once if a manually added component has no explicit binding.
            if (text == null)
                text = GetComponent<Text>();
            CollectedPrice.Changed -= Refresh;
            CollectedPrice.Changed += Refresh;
            Refresh();
        }

        #endregion

        #region Presentation

        /// <summary>Changes text only on activation or when a completed collection batch becomes visible.</summary>
        private void Refresh()
        {
            if (text == null)
                return;
            text.color = Color.green;
            text.text = CollectedPrice.DisplayedTotal.ToString("0.##", CultureInfo.InvariantCulture) + "$";
        }

        #endregion

        #endregion
    }
}
