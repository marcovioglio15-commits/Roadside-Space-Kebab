using System.Collections.Generic;
using CatOnASkateboard.PlayerStudio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Connects scene-authored gameplay panels to observers without creating or moving UI at runtime.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [AddComponentMenu("Objects Logic Studio/Scene HUD")]
    public sealed class ObjectSceneHud : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Gameplay Panels")]
        [Tooltip("Single label reused by every hover target, including dynamically spawned items.")]
        [SerializeField]
        private HoverLabel hover;
        [Tooltip("Existing dialogue panel on this Canvas.")]
        [SerializeField]
        private DialogueHud dialogue;
        [Tooltip("Existing assembly icon and charge ring on this Canvas.")]
        [SerializeField]
        private AssemblyChargeHud assembly;
        [Tooltip("Existing centered cursor graphic on this Canvas.")]
        [SerializeField]
        private PlayerCenteredCursor cursor;

        [Tooltip("Optional preauthored stopwatch on the shared gameplay Canvas.")]
        [SerializeField]
        private OrderTimerHud orderTimer;

        #endregion

        #region State

        private static readonly List<ObjectSceneHud> registered = new List<ObjectSceneHud>();
        private HoverObserver owner;
        private Transform player;

        #endregion

        #region Properties

        /// <summary>Shared hover presentation.</summary>
        public HoverLabel Hover => hover;
        /// <summary>Shared dialogue presentation.</summary>
        public DialogueHud Dialogue => dialogue;
        /// <summary>Shared prolonged assembly presentation.</summary>
        public AssemblyChargeHud Assembly => assembly;

        #endregion

        #region Methods

        #region Registration

        /// <summary>Registers authored scene UI at activation, including additive scene loading.</summary>
        private void OnEnable()
        {
            if (!registered.Contains(this))
                registered.Add(this);
        }

        /// <summary>Hides presentation and removes the scene from the observer's lookup.</summary>
        private void OnDisable()
        {
            Unbind(owner);
            registered.Remove(this);
        }

        /// <summary>Rebuilds retained registration when entering Play without scene reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            registered.Clear();
            foreach (ObjectSceneHud hud in FindObjectsByType<ObjectSceneHud>())
            {
                hud.Unbind(hud.owner);
                registered.Add(hud);
            }
        }

        /// <summary>Resolves the sole active HUD saved in the observer's scene.</summary>
        /// <param name="scene">Gameplay scene containing the observer.</param>
        /// <returns>The unambiguous authored HUD, or null when missing or duplicated.</returns>
        internal static ObjectSceneHud Find(Scene scene)
        {
            ObjectSceneHud result = null;
            foreach (ObjectSceneHud hud in registered)
                if (hud != null && hud.isActiveAndEnabled && hud.gameObject.scene == scene)
                {
                    if (result != null)
                        return null;
                    result = hud;
                }
            return result;
        }

        #endregion

        #region Ownership

        /// <summary>Validates every gameplay panel against one authored Canvas.</summary>
        /// <returns>True when all required graphics share this rendering surface.</returns>
        public bool IsValid()
        {
            Canvas canvas = GetComponent<Canvas>();
            return hover != null && dialogue != null && assembly != null && cursor != null
                && hover.Canvas == canvas && dialogue.Canvas == canvas && assembly.Canvas == canvas
                && hover.IsValid() && dialogue.IsValid() && assembly.IsValid()
                && (orderTimer == null || orderTimer.Canvas == canvas && orderTimer.IsValid())
                && cursor.transform.IsChildOf(transform) && cursor.canvasRenderer != null
                && GetComponentsInChildren<Canvas>(true).Length == 1;
        }

        /// <summary>Connects existing panels once when an observer acquires this scene.</summary>
        /// <param name="observer">Observer requesting exclusive scene presentation.</param>
        /// <returns>True when this observer owns a valid shared HUD.</returns>
        internal bool Bind(HoverObserver observer)
        {
            // Revalidate hierarchy only on acquisition; retained owners may still resolve a newly spawned player.
            if (observer == null || owner != null && owner != observer || owner != observer && !IsValid())
                return false;
            owner = observer;
            hover.Bind(observer);
            dialogue.Bind(observer);
            assembly.Bind(observer);
            if (orderTimer != null)
                orderTimer.Bind(observer);
            if (player != observer.Player)
            {
                player = observer.Player;
                cursor.Bind(player != null ? player.GetComponentInChildren<PlayerCameraRig>(true) : null);
            }
            return true;
        }

        /// <summary>Releases only the current observer, keeping scene UI available through player respawns.</summary>
        /// <param name="observer">Observer whose presentation is ending.</param>
        internal void Unbind(HoverObserver observer)
        {
            if (owner != observer)
                return;
            if (hover != null)
                hover.Unbind(observer);
            if (dialogue != null)
                dialogue.Unbind(observer);
            if (assembly != null)
                assembly.Unbind(observer);
            if (orderTimer != null)
                orderTimer.Unbind(observer);
            if (cursor != null)
                cursor.Bind(null);
            owner = null;
            player = null;
        }

        #endregion

        #endregion
    }
}
