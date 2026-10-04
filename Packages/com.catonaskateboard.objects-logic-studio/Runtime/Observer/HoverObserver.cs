using CatOnASkateboard.StudioIdentity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Evaluates all registered object hovers from one camera and a player resolved by flag.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    [AddComponentMenu("Objects Logic Studio/Hover Observer")]
    public sealed class HoverObserver : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Observation")]
        [Tooltip("Main gameplay camera. An empty reference uses a Camera on this same object.")]
        [SerializeField]
        private Camera view;

        [Tooltip("Flag identifying the player root. A matching camera ancestor takes priority; otherwise exactly one active object with this flag is required.")]
        [SerializeField]
        private ObjectFlag playerFlag;

        [Tooltip("Single authored dialogue overlay shared by every object seen by this observer. Create it in Scene Observer before Play.")]
        [SerializeField]
        private DialogueHud dialogueHud;

        #endregion

        #region State

        private static HoverObserver active;
        private readonly HoverPhysics physics = new HoverPhysics();
        private readonly SingleInteractionDriver singles = new SingleInteractionDriver();
        private readonly DialogueDriver dialogues = new DialogueDriver();
        private Camera resolvedView;
        private Transform player;
        private Transform toolsOwner;
        private CatOnASkateboard.PlayerStudio.PlayerTools tools;
        private float nextResolve;
        private bool hadContext;
        private string lastWarning = string.Empty;

        #endregion

        #region Properties

        /// <summary>Camera currently supplying projection, clipping and sight origin.</summary>
        public Camera View => resolvedView;
        /// <summary>Authored camera reference available to editor setup before Play begins.</summary>
        public Camera ConfiguredView => view;
        /// <summary>Shared dialogue presentation authored on the player or in the scene.</summary>
        public DialogueHud DialogueHud => dialogueHud;
        /// <summary>Cached identified root used for distance and player-collider filtering.</summary>
        public Transform Player => player;
        /// <summary>Cached tools belonging to the sole active observer player.</summary>
        internal static CatOnASkateboard.PlayerStudio.PlayerTools ActiveTools => HasPlayer ? active.tools : null;
        /// <summary>Whether tool restrictions have an active player context.</summary>
        internal static bool HasPlayer => active != null && active.player != null && active.player.gameObject.activeInHierarchy;
        /// <summary>Flag used by the observer's player lookup.</summary>
        public ObjectFlag PlayerFlag => playerFlag;
        /// <summary>Most recent unavailable-context diagnostic.</summary>
        public string Warning => lastWarning;
        /// <summary>The player's single occupied carry slot; null after Drop, Throw or loss of context.</summary>
        public ObjectGrab HeldObject => singles.Held;

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
            if (dialogueHud != null)
                dialogueHud.Bind(this);
        }

        /// <summary>Releases ownership and hides all labels before another observer takes over.</summary>
        private void OnDisable()
        {
            // Input subscriptions belong to this observer even when another observer owns the view.
            singles.Reset();
            dialogues.Reset();
            if (dialogueHud != null)
                dialogueHud.Unbind(this);
            // A duplicate observer must not hide the active observer's output.
            if (active != this)
                return;
            active = null;
            InteractionUnlockRegistry.Reset();
            AssemblyInteractionRegistry.Reset();
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
            if (active != this && dialogueHud != null)
                dialogueHud.Bind(this);
            active = this;
            if (Time.unscaledTime >= nextResolve)
            {
                nextResolve = Time.unscaledTime + 1f;
                ResolveContext();
            }
            bool available = resolvedView != null && resolvedView.isActiveAndEnabled && player != null
                && player.gameObject.activeInHierarchy;
            if (toolsOwner != player)
            {
                toolsOwner = player;
                tools = player != null ? player.GetComponentInChildren<CatOnASkateboard.PlayerStudio.PlayerTools>(true) : null;
            }
            if (available)
            {
                // Preparation precedes the dialogue readiness check without reparenting during activation callbacks.
                if (dialogueHud != null)
                    dialogueHud.Prepare(this);
                InteractionUnlockRegistry.Tick(this);
                AssemblyInteractionRegistry.Tick(this);
                HoverRegistry.Tick(this);
                singles.Prepare(this);
                singles.Tick(this, dialogues.Tick(this, singles));
            }
            else if (hadContext)
            {
                singles.Reset();
                dialogues.Reset();
                InteractionUnlockRegistry.Reset();
                AssemblyInteractionRegistry.Reset();
                HoverRegistry.HideAll();
            }
            hadContext = available;
        }

        #endregion

        #region Context

        /// <summary>Requests a new camera and identified player after an explicit runtime camera or flag change.</summary>
        public void RefreshContext()
        {
            // Camera replacement also releases held objects and old PlayerInput subscriptions.
            singles.Reset();
            dialogues.Reset();
            InteractionUnlockRegistry.Reset();
            AssemblyInteractionRegistry.Reset();
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
            // An explicit camera remains authoritative; no Unity Tag is used for discovery.
            Camera nextView = view != null ? view : GetComponent<Camera>();
            if (nextView != resolvedView)
            {
                resolvedView = nextView;
                player = null;
            }
            if (resolvedView == null || playerFlag == null)
            {
                player = null;
                Report("Assign the gameplay camera and an Object Flag for the player.");
                return;
            }
            // Camera ancestors take precedence; otherwise the active registry must be unambiguous.
            ObjectIdentity identity = ObjectIdentity.FindInParents(resolvedView.transform, playerFlag);
            if (identity == null)
                identity = ObjectIdentityRegistry.FindUnique(playerFlag);
            player = identity != null ? identity.transform : null;
            if (player == null)
                Report("Hover Observer needs one active player with the selected flag, or a matching camera ancestor.");
            else
                lastWarning = string.Empty;
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
        /// <param name="hitDistance">Receives squared camera-to-target distance for centre-hit arbitration.</param>
        /// <returns>True when this object may display its label.</returns>
        internal bool Evaluate(ObjectHover target, Collider[] colliders, out float hitDistance)
        {
            // Collider modes test the actual hit surface even when the root pivot is off-screen or out of reach.
            Vector3 anchor = target.WorldAnchor;
            HoverSettings settings = target.Settings;
            Rect viewport = resolvedView.pixelRect;
            hitDistance = float.PositiveInfinity;
            switch (settings.TargetMode)
            {
                case HoverDetectionMode.ViewCenter:
                    Vector3 projected = resolvedView.WorldToScreenPoint(anchor);
                    float radius = viewport.height * settings.CenterRadius;
                    if (projected.z < resolvedView.nearClipPlane || projected.z > resolvedView.farClipPlane
                        || !viewport.Contains(projected) || (resolvedView.cullingMask & (1 << target.gameObject.layer)) == 0
                        || ((Vector2)projected - viewport.center).sqrMagnitude > radius * radius)
                        return false;
                    break;
                case HoverDetectionMode.Cursor:
                case HoverDetectionMode.CenterCollider:
                    Vector2 cursor = viewport.center;
                    if (settings.TargetMode == HoverDetectionMode.Cursor)
                    {
                        if (Mouse.current == null || Cursor.lockState == CursorLockMode.Locked || resolvedView.targetDisplay != 0)
                            return false;
                        cursor = Mouse.current.position.ReadValue();
                    }
                    if (!viewport.Contains(cursor) || !HoverPhysics.TryCursorHit(resolvedView.ScreenPointToRay(cursor), colliders,
                        resolvedView.cullingMask, Vector3.Distance(resolvedView.transform.position, player.position) + settings.PlayerDistance, out anchor))
                        return false;
                    float depth = resolvedView.WorldToViewportPoint(anchor).z;
                    if (depth < resolvedView.nearClipPlane || depth > resolvedView.farClipPlane)
                        return false;
                    break;
                default:
                    return false;
            }
            hitDistance = (anchor - resolvedView.transform.position).sqrMagnitude;
            return (anchor - player.position).sqrMagnitude <= settings.PlayerDistance * settings.PlayerDistance
                && physics.HasSight(this, target, anchor);
        }

        #endregion

        #endregion
    }
}
