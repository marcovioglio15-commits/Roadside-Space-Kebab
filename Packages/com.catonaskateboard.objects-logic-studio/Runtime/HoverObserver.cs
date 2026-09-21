using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Evaluates all registered object hovers from one camera and a player resolved by tag.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    [AddComponentMenu("Objects Logic Studio/Hover Observer")]
    public sealed class HoverObserver : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Observation")]
        [Tooltip("Main gameplay camera. An empty reference resolves Camera.main at activation and when the cached camera is lost.")]
        [SerializeField]
        private Camera view;

        [Tooltip("Tag identifying the player root. A matching camera ancestor takes priority; otherwise exactly one active tagged object is required.")]
        [SerializeField]
        private string playerTag = "Player";

        #endregion

        #region State

        private static HoverObserver active;
        private readonly HoverPhysics physics = new HoverPhysics();
        private readonly SingleInteractionDriver singles = new SingleInteractionDriver();
        private Camera resolvedView;
        private Transform player;
        private float nextResolve;
        private bool hadContext;
        private string lastWarning = string.Empty;

        #endregion

        #region Properties

        /// <summary>Camera currently supplying projection, clipping and sight origin.</summary>
        public Camera View => resolvedView;
        /// <summary>Authored camera reference available to editor setup before Play begins.</summary>
        public Camera ConfiguredView => view;
        /// <summary>Cached tagged root used for distance and player-collider filtering.</summary>
        public Transform Player => player;
        /// <summary>Tag used by the observer's player lookup.</summary>
        public string PlayerTag => playerTag;
        /// <summary>Most recent unavailable-context diagnostic.</summary>
        public string Warning => lastWarning;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Clears singleton ownership at every Play entry, including disabled domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOwnership()
        {
            // An enabled observer claims the role again from its first LateUpdate.
            active = null;
        }

        /// <summary>Requests fresh context when the observer is explicitly enabled.</summary>
        private void OnEnable()
        {
            // Reacquisition never instantiates a player, camera or UI.
            RefreshContext();
        }

        /// <summary>Releases ownership and hides all labels before another observer takes over.</summary>
        private void OnDisable()
        {
            // Input subscriptions belong to this observer even when another observer owns the view.
            singles.Reset();
            // A duplicate observer must not hide the active observer's output.
            if (active != this)
                return;
            active = null;
            HoverRegistry.HideAll();
        }

        /// <summary>Runs after Player Studio's camera follow and updates existing labels.</summary>
        private void LateUpdate()
        {
            // One active observer prevents competing projections onto the same UI.
            if (active != null && active != this)
            {
                Report("Only one Hover Observer can be active. Disable the previous observer before switching views.");
                return;
            }
            active = this;
            if (Time.unscaledTime >= nextResolve)
            {
                nextResolve = Time.unscaledTime + 1f;
                ResolveContext();
            }
            bool available = resolvedView != null && resolvedView.isActiveAndEnabled && player != null
                && player.gameObject.activeInHierarchy;
            if (available)
            {
                singles.Tick(this);
                HoverRegistry.Tick(this);
            }
            else if (hadContext)
            {
                singles.Reset();
                HoverRegistry.HideAll();
            }
            hadContext = available;
        }

        #endregion

        #region Context

        /// <summary>Requests a new camera and tagged player after an explicit runtime camera or tag change.</summary>
        public void RefreshContext()
        {
            // Camera replacement also releases held objects and old PlayerInput subscriptions.
            singles.Reset();
            // Reset cached ownership without looking through the scene every frame.
            resolvedView = null;
            player = null;
            nextResolve = 0f;
            lastWarning = string.Empty;
            if (active == this)
                HoverRegistry.HideAll();
        }

        /// <summary>Resolves missing context at most once per second, allowing player spawn and respawn.</summary>
        internal void ResolveContext()
        {
            // An explicit camera remains authoritative even while temporarily disabled.
            Camera nextView = view != null ? view : resolvedView != null && resolvedView.isActiveAndEnabled ? resolvedView : Camera.main;
            if (nextView != resolvedView)
            {
                resolvedView = nextView;
                player = null;
            }
            if (resolvedView == null)
            {
                Report("Assign the gameplay camera to Hover Observer or tag it MainCamera.");
                return;
            }
            try
            {
                // Keep a valid player cached; transform changes need no lookup.
                if (player != null && player.gameObject.activeInHierarchy && player.CompareTag(playerTag))
                {
                    lastWarning = string.Empty;
                    return;
                }
                player = null;
                for (Transform parent = resolvedView.transform; parent != null; parent = parent.parent)
                    if (parent.CompareTag(playerTag))
                    {
                        player = parent;
                        lastWarning = string.Empty;
                        return;
                    }

                // A non-parented camera requires one unambiguous active tagged player.
                GameObject[] candidates = GameObject.FindGameObjectsWithTag(playerTag);
                if (candidates.Length == 1)
                {
                    player = candidates[0].transform;
                    lastWarning = string.Empty;
                }
                else
                    Report("Hover Observer needs one active object tagged '" + playerTag + "', or a tagged ancestor of its camera.");
            }
            catch (UnityException)
            {
                // Undefined tags are configuration warnings, never an exception on each rendered frame.
                player = null;
                Report("Player Tag is empty or undefined. Select an existing player tag in Hover Observer.");
            }
        }

        /// <summary>Reports context changes once instead of repeating the same warning during retries.</summary>
        /// <param name="warning">Action needed to restore observation.</param>
        private void Report(string warning)
        {
            // Recovery clears the warning so a later independent failure can be reported again.
            if (lastWarning == warning)
                return;
            lastWarning = warning;
            Debug.LogWarning(warning, this);
        }

        #endregion

        #region Detection

        /// <summary>Checks range, camera viewport, mode-specific targeting and clean line of sight.</summary>
        /// <param name="target">Interaction with validated configuration.</param>
        /// <param name="colliders">Cached target collider hierarchy.</param>
        /// <returns>True when this object may display its label.</returns>
        internal bool Evaluate(ObjectHover target, Collider[] colliders)
        {
            // Cheap rejection precedes physics; range is measured from the tagged player, not the camera.
            Vector3 anchor = target.WorldAnchor;
            HoverSettings settings = target.Settings;
            if ((anchor - player.position).sqrMagnitude > settings.PlayerDistance * settings.PlayerDistance)
                return false;
            Vector3 projected = resolvedView.WorldToScreenPoint(anchor);
            Rect viewport = resolvedView.pixelRect;
            if (projected.z < resolvedView.nearClipPlane || projected.z > resolvedView.farClipPlane
                || !viewport.Contains(projected) || (resolvedView.cullingMask & (1 << target.gameObject.layer)) == 0)
                return false;

            // Exact cursor hits and center proximity share the same final obstruction check.
            switch (settings.TargetMode)
            {
                case HoverTargetMode.ViewCenter:
                    float radius = viewport.height * settings.CenterRadius;
                    if (((Vector2)projected - viewport.center).sqrMagnitude > radius * radius)
                        return false;
                    break;
                case HoverTargetMode.Cursor:
                    if (Mouse.current == null || Cursor.lockState == CursorLockMode.Locked || resolvedView.targetDisplay != 0)
                        return false;
                    Vector2 cursor = Mouse.current.position.ReadValue();
                    if (!viewport.Contains(cursor) || !HoverPhysics.TryCursorHit(resolvedView.ScreenPointToRay(cursor), colliders,
                        resolvedView.cullingMask, Vector3.Distance(resolvedView.transform.position, player.position) + settings.PlayerDistance, out anchor))
                        return false;
                    break;
                default:
                    return false;
            }
            return physics.HasSight(this, target, anchor);
        }

        #endregion

        #endregion
    }
}
