using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Owns active tool selection and staged transform transitions for one player.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(PlayerHost))]
    [AddComponentMenu("Player Studio/Player Tools")]
    public sealed class PlayerTools : MonoBehaviour
    {
        #region State

        private PlayerToolsPreset configuration;
        private PlayerToolCommands commands;
        private PlayerToolRuntime[] behaviors;
        private PlayerToolIdleMotion arm;
        private Transform[] targets;
        private Transform[] modelChildren;
        private PlayerToolPose[] original;
        private PlayerToolSlotTransition[] transitions;
        private PlayerToolAnimationRun[] entrances;
        private PlayerToolAnimationRun[] exits;
        private int active = -1;
        private int requested = -1;
        private int destination = -1;
        private int phase;
        private float elapsed;
        private bool ready;
        private bool hasSlots;
        private bool useRequested;
        private bool reloadRequested;
        private Guid useActionId;

        #endregion

        #region Properties

        /// <summary>The settled active identity; transitions expose no usable tool.</summary>
        public PlayerTool ActiveTool => ready && phase == 0 && active >= 0 ? configuration.Tools[active].Tool : null;
        /// <summary>Whether outgoing, slot or incoming motion is still running.</summary>
        public bool IsSwitching => phase != 0;
        /// <summary>Whether configuration and visual bindings passed initialization.</summary>
        public bool IsReady => ready;
        /// <summary>Whether the active tool is playing use or reload keyframes.</summary>
        public bool IsUsing => ready && active >= 0 && behaviors[active].IsBusy;
        /// <summary>Whether the active tool is playing its reload combo.</summary>
        public bool IsReloading => ready && active >= 0 && behaviors[active].IsReloading;
        /// <summary>Emitted after a tool has reached its active slot and completed its incoming animation.</summary>
        public event Action<PlayerTool> Changed;
        /// <summary>Emitted when an active tool accepts a use command, before its final muzzle shot.</summary>
        public event Action<PlayerTool> Used;
        /// <summary>Stable action currently assigned to Use Tool, shared with optional impact interaction routing.</summary>
        public Guid UseActionId => useActionId;
        /// <summary>Emitted once for each real bullet collision after the bullet returns to its pool.</summary>
        public static event Action<PlayerToolHit> ProjectileHit;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Clears external projectile listeners before packages register for a new Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetProjectileEvents()
        {
            // Disabled domain reload must not preserve listeners from a previous gameplay session.
            ProjectileHit = null;
        }

        /// <summary>Publishes a physical hit without depending on any optional interaction package.</summary>
        /// <param name="hit">Shot and exact physics contact.</param>
        internal static void PublishHit(PlayerToolHit hit)
        {
            // Subscribers may queue work until their own normal gameplay update.
            ProjectileHit?.Invoke(hit);
        }

        /// <summary>Initializes after PlayerInput have completed startup.</summary>
        private void Start()
        {
            // Resolve authored modules once; weapon visual pools are prewarmed after binding succeeds.
            Initialize();
        }

        /// <summary>Restarts a previously disabled module on its next update.</summary>
        private void OnEnable()
        {
            // Start handles first activation; later activation uses the same initialization path.
            ready = false;
        }

        /// <summary>Releases input callbacks and restores the original visual poses.</summary>
        private void OnDisable()
        {
            Release();
        }

        /// <summary>Restores retained scene objects before starting another Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            // One startup scan also supports sessions with both domain and scene reload disabled.
            foreach (PlayerTools tools in FindObjectsByType<PlayerTools>())
                if (tools.isActiveAndEnabled)
                    tools.Release();
        }

        /// <summary>Releases input ownership and restores cached visual transforms before initialization.</summary>
        private void Release()
        {
            // Disabling during a switch must not leave a half-animated model on the next enable.
            commands?.Dispose();
            commands = null;
            RestorePresentation();
            if (behaviors != null)
                foreach (PlayerToolRuntime behavior in behaviors)
                    behavior?.Dispose();
            behaviors = null;
            arm = null;
            if (modelChildren != null)
                for (int index = 0; index < modelChildren.Length; index++)
                    if (modelChildren[index] != null)
                        original[index].Apply(modelChildren[index]);
            modelChildren = null;
            configuration = null;
            ready = false;
            phase = 0;
            useRequested = reloadRequested = false;
            active = requested = destination = -1;
        }

        /// <summary>Consumes queued selection and samples transforms only during a transition.</summary>
        private void Update()
        {
            // Re-enable initialization runs once and does not retry invalid presets each frame.
            if (!ready)
            {
                if (configuration == null)
                    Initialize();
                return;
            }
            commands?.Refresh();
            if (Time.timeScale <= 0f)
            {
                useRequested = reloadRequested = false;
                return;
            }
            RestorePresentation();
            if (active >= 0)
                behaviors[active].Advance(Time.deltaTime);
            if (phase == 0 && requested != active && !IsUsing)
                BeginSwitch();
            if (phase != 0)
                Advance(Time.deltaTime);
            // Selection has priority over an activation queued during the same frame.
            if (ActiveTool != null && requested == active)
            {
                if (reloadRequested)
                    behaviors[active].Reload();
                else if (useRequested && behaviors[active].CanUse)
                {
                    behaviors[active].Use();
                    Used?.Invoke(ActiveTool);
                }
            }
            useRequested = reloadRequested = false;
        }

        /// <summary>Applies reversible idle and aim after the camera and all authored animation poses.</summary>
        private void LateUpdate()
        {
            if (!ready || Time.deltaTime <= 0f || ActiveTool == null)
                return;
            if (!IsUsing)
                arm?.Sample(Time.deltaTime);
            arm?.SampleRotation(Time.deltaTime);
            behaviors[active].Present(Time.deltaTime);
        }

        /// <summary>Continues bullets and impact lifetimes independently of the selected tool.</summary>
        private void FixedUpdate()
        {
            if (!ready)
                return;
            foreach (PlayerToolRuntime behavior in behaviors)
                behavior.Simulate(Time.fixedDeltaTime);
        }

        /// <summary>Removes procedural layers in reverse order before editing or restoring base transforms.</summary>
        private void RestorePresentation()
        {
            // Aim was applied after idle, so it is removed first.
            if (behaviors != null && active >= 0)
                behaviors[active]?.RestoreAim();
            arm?.Restore();
        }

        /// <summary>Builds immutable target bindings and validates every animation before moving anything.</summary>
        private void Initialize()
        {
            // A missing optional module is inert; invalid assigned configuration reports once.
            PlayerHost host = GetComponent<PlayerHost>();
            configuration = host.MasterPreset != null ? host.MasterPreset.ToolsPreset : null;
            if (configuration == null)
            {
                enabled = false;
                return;
            }
            if (!configuration.TryValidate(out string warning))
            {
                Debug.LogWarning(warning, this);
                return;
            }
            PlayerToolInputSettings inputSettings = host.MasterPreset.InputPreset != null ? host.MasterPreset.InputPreset.Tools : null;
            useActionId = inputSettings?.Activate != null && inputSettings.Activate.action != null ? inputSettings.Activate.action.id : Guid.Empty;
            if (inputSettings != null && !inputSettings.TryValidate(out warning, !configuration.SingleTool))
            {
                Debug.LogWarning(warning, this);
                return;
            }
            Transform model = PlayerHierarchy.Resolve(transform, configuration.RootPath);
            HashSet<Transform> moved = new HashSet<Transform>();
            PlayerCameraRig camera = GetComponent<PlayerCameraRig>();
            behaviors = new PlayerToolRuntime[configuration.Tools.Length];
            arm = configuration.Arm.Enabled ? new PlayerToolIdleMotion(configuration.Arm, model) : null;
            if (model == null || arm != null && !arm.CollectTargets(moved, transform, camera != null && camera.View != null ? camera.View.transform : null))
            {
                Debug.LogWarning("Select an existing Tools hierarchy and a player arm below the player root. A smoothed arm must not contain the camera.", this);
                return;
            }
            targets = new Transform[configuration.Tools.Length];
            transitions = new PlayerToolSlotTransition[targets.Length];
            entrances = new PlayerToolAnimationRun[targets.Length];
            exits = new PlayerToolAnimationRun[targets.Length];
            Transform commonParent = null;
            hasSlots = false;
            for (int index = 0; index < targets.Length; index++)
            {
                PlayerToolEntry entry = configuration.Tools[index];
                bool moveVisual = !configuration.SingleTool && entry.MoveVisual;
                targets[index] = moveVisual ? PlayerHierarchy.Resolve(model, entry.Path) : null;
                entrances[index] = new PlayerToolAnimationRun(!configuration.SingleTool ? entry.Tool.SwitchIn : null, model);
                exits[index] = new PlayerToolAnimationRun(!configuration.SingleTool ? entry.Tool.SwitchOut : null, model);
                behaviors[index] = new PlayerToolRuntime(entry.Tool, model, transform, camera);
                if (moveVisual && (targets[index] == null || targets[index] == transform)
                    || !entrances[index].CollectTargets(moved, transform) || !exits[index].CollectTargets(moved, transform)
                    || !behaviors[index].CollectTargets(moved, transform)
                    || configuration.Layout == PlayerToolLayout.Cyclic && targets[index] != null
                    && commonParent != null && targets[index].parent != commonParent)
                {
                    Debug.LogWarning("Select existing tool/animation children. Cyclic slots need a common parent; Auto Aim needs an existing player camera and a visual pivot containing its muzzle.", this);
                    return;
                }
                if (targets[index] != null)
                {
                    moved.Add(targets[index]);
                    commonParent = targets[index].parent;
                    hasSlots = true;
                }
            }
            modelChildren = new Transform[moved.Count];
            moved.CopyTo(modelChildren);
            original = new PlayerToolPose[modelChildren.Length];
            for (int index = 0; index < modelChildren.Length; index++)
                original[index] = PlayerToolPose.Read(modelChildren[index]);
            active = requested = configuration.SingleTool ? 0 : configuration.IndexOf(configuration.InitialTool);
            for (int index = 0; index < targets.Length; index++)
                if (targets[index] != null)
                    configuration.Destination(index, active).Apply(targets[index]);
            foreach (PlayerToolRuntime behavior in behaviors)
                behavior.Prepare(transform);
            ready = true;
            commands = new PlayerToolCommands(this, GetComponent<PlayerInput>(), inputSettings, !configuration.SingleTool);
        }

        #endregion

        #region Input

        /// <summary>Queues the most recent explicit selection, including null to park every tool.</summary>
        /// <param name="tool">Configured identity or null for no tool.</param>
        /// <returns>True when this module accepted the selection.</returns>
        public bool Select(PlayerTool tool)
        {
            // A request made during motion starts after the current transition finishes.
            if (!ready || !isActiveAndEnabled || configuration.SingleTool || tool != null && configuration.IndexOf(tool) < 0)
                return false;
            requested = configuration.IndexOf(tool);
            return true;
        }

        /// <summary>Queues the next tool in the configured order.</summary>
        public void Cycle()
        {
            // Rapid input advances from the pending selection without interrupting a transform mid-track.
            if (ready && isActiveAndEnabled && !configuration.SingleTool && configuration.Tools.Length > 0)
                requested = (requested + 1) % configuration.Tools.Length;
        }

        /// <summary>Queues one use of the settled tool for the next update.</summary>
        /// <returns>True when the tool is available and the request was queued.</returns>
        public bool Use()
        {
            // No delayed activation is retained through a switch, pause or cooldown.
            if (!isActiveAndEnabled || Time.timeScale <= 0f || ActiveTool == null || requested != active
                || useRequested || reloadRequested || !behaviors[active].CanUse)
                return false;
            useRequested = true;
            return true;
        }

        /// <summary>Queues the settled tool's optional reload combo.</summary>
        /// <returns>True when a reload request was accepted.</returns>
        public bool Reload()
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0f || ActiveTool == null || requested != active
                || IsUsing || useRequested || reloadRequested || !ActiveTool.ReloadEnabled)
                return false;
            reloadRequested = true;
            return true;
        }

        #endregion

        #region Transitions

        /// <summary>Starts outgoing motion while keeping the requested destination stable for this switch.</summary>
        private void BeginSwitch()
        {
            // A later input can change requested without redirecting the active animation.
            destination = requested;
            phase = 1;
            elapsed = 0f;
            if (active >= 0)
            {
                behaviors[active].Park();
                exits[active].Begin();
            }
        }

        /// <summary>Completes outgoing animation, slot interpolation and incoming animation in order.</summary>
        /// <param name="delta">Scaled seconds available to this update.</param>
        private void Advance(float delta)
        {
            // Carry unused frame time across phase boundaries, including zero-duration slots.
            for (int step = 0; step < 3 && phase != 0; step++)
            {
                float duration = phase switch
                {
                    1 => active >= 0 ? exits[active].Duration : 0f,
                    2 => hasSlots ? configuration.SwitchDuration : 0f,
                    _ => destination >= 0 ? entrances[destination].Duration : 0f
                };
                float consumed = Mathf.Min(delta, Mathf.Max(0f, duration - elapsed));
                elapsed += consumed;
                delta -= consumed;
                switch (phase)
                {
                    case 1 when active >= 0:
                        exits[active].Sample(elapsed);
                        break;
                    case 2:
                        SampleSlots(duration > 0f ? Mathf.SmoothStep(0f, 1f, elapsed / duration) : 1f);
                        break;
                    case 3 when destination >= 0:
                        entrances[destination].Sample(elapsed);
                        break;
                }
                if (elapsed < duration)
                    return;
                elapsed = 0f;
                phase++;
                if (phase == 2)
                    for (int index = 0; index < targets.Length; index++)
                        if (targets[index] != null)
                            transitions[index] = new PlayerToolSlotTransition(PlayerToolPose.Read(targets[index]),
                                configuration.Destination(index, destination), configuration);
                if (phase == 3 && destination >= 0)
                    entrances[destination].Begin();
                if (phase == 4)
                {
                    active = destination;
                    phase = 0;
                    Changed?.Invoke(ActiveTool);
                }
            }
        }

        /// <summary>Moves every slotted visual together, reusing buffers captured at the phase boundary.</summary>
        /// <param name="amount">Eased normalized slot progress.</param>
        private void SampleSlots(float amount)
        {
            // Endpoints and wheel geometry were captured once at the phase boundary.
            for (int index = 0; index < targets.Length; index++)
                if (targets[index] != null)
                    transitions[index].Apply(targets[index], amount);
        }

        #endregion

        #endregion
    }
}
