using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Owns active tool selection and staged transform transitions for one player.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHost))]
    [AddComponentMenu("Player Studio/Player Tools")]
    public sealed class PlayerTools : MonoBehaviour
    {
        #region State

        private PlayerToolsPreset configuration;
        private PlayerToolInputSettings inputSettings;
        private PlayerInput input;
        private InputActionAsset boundAsset;
        private InputAction[] actions = Array.Empty<InputAction>();
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

        #endregion

        #region Properties

        /// <summary>The settled active identity; transitions expose no usable tool.</summary>
        public PlayerTool ActiveTool => ready && phase == 0 && active >= 0 ? configuration.Tools[active].Tool : null;
        /// <summary>Whether outgoing, slot or incoming motion is still running.</summary>
        public bool IsSwitching => phase != 0;
        /// <summary>Whether configuration and visual bindings passed initialization.</summary>
        public bool IsReady => ready;
        /// <summary>Emitted after a tool has reached its active slot and completed its incoming animation.</summary>
        public event Action<PlayerTool> Changed;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Initializes after PlayerInput have completed startup.</summary>
        private void Start()
        {
            // Resolve authored modules once, without creating runtime components or visuals.
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
            Bind(null);
            if (modelChildren != null)
                for (int index = 0; index < modelChildren.Length; index++)
                    if (modelChildren[index] != null)
                        original[index].Apply(modelChildren[index]);
            modelChildren = null;
            configuration = null;
            ready = false;
            phase = 0;
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
            InputActionAsset asset = input != null && input.isActiveAndEnabled && input.inputIsActive ? input.actions : null;
            if (asset != boundAsset)
                Bind(asset);
            if (Time.timeScale <= 0f)
                return;
            if (phase == 0 && requested != active)
                BeginSwitch();
            if (phase != 0)
                Advance(Time.deltaTime);
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
            input = GetComponent<PlayerInput>();
            inputSettings = host.MasterPreset.InputPreset != null ? host.MasterPreset.InputPreset.Tools : null;
            if (inputSettings != null && !inputSettings.TryValidate(out warning))
            {
                Debug.LogWarning(warning, this);
                return;
            }
            Transform model = PlayerHierarchy.Resolve(transform, configuration.RootPath);
            HashSet<Transform> moved = new HashSet<Transform>();
            targets = new Transform[configuration.Tools.Length];
            transitions = new PlayerToolSlotTransition[targets.Length];
            entrances = new PlayerToolAnimationRun[targets.Length];
            exits = new PlayerToolAnimationRun[targets.Length];
            Transform commonParent = null;
            hasSlots = false;
            for (int index = 0; index < targets.Length; index++)
            {
                PlayerToolEntry entry = configuration.Tools[index];
                targets[index] = entry.MoveVisual ? PlayerHierarchy.Resolve(model, entry.Path) : null;
                entrances[index] = new PlayerToolAnimationRun(entry.Tool.SwitchIn, model);
                exits[index] = new PlayerToolAnimationRun(entry.Tool.SwitchOut, model);
                if (entry.MoveVisual && (targets[index] == null || targets[index] == transform)
                    || !entrances[index].CollectTargets(moved, transform) || !exits[index].CollectTargets(moved, transform)
                    || configuration.Layout == PlayerToolLayout.Cyclic && targets[index] != null
                    && commonParent != null && targets[index].parent != commonParent)
                {
                    Debug.LogWarning("Select existing tool children; cyclic slots need a common parent. The player root cannot be animated.", this);
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
            active = requested = configuration.IndexOf(configuration.InitialTool);
            for (int index = 0; index < targets.Length; index++)
                if (targets[index] != null)
                    configuration.Destination(index, active).Apply(targets[index]);
            ready = true;
            Bind(input != null && input.isActiveAndEnabled && input.inputIsActive ? input.actions : null);
        }

        #endregion

        #region Input

        /// <summary>Queues the most recent explicit selection, including null to park every tool.</summary>
        /// <param name="tool">Configured identity or null for no tool.</param>
        /// <returns>True when this module accepted the selection.</returns>
        public bool Select(PlayerTool tool)
        {
            // A request made during motion starts after the current transition finishes.
            if (!ready || !isActiveAndEnabled || tool != null && configuration.IndexOf(tool) < 0)
                return false;
            requested = configuration.IndexOf(tool);
            return true;
        }

        /// <summary>Queues the next tool in the configured order.</summary>
        public void Cycle()
        {
            // Rapid input advances from the pending selection without interrupting a transform mid-track.
            if (ready && isActiveAndEnabled && configuration.Tools.Length > 0)
                requested = (requested + 1) % configuration.Tools.Length;
        }

        /// <summary>Connects only to this player's action instances and releases stale subscriptions.</summary>
        /// <param name="asset">Current private action asset, or null when input ownership ends.</param>
        private void Bind(InputActionAsset asset)
        {
            // Never enable or disable maps owned by PlayerInput.
            foreach (InputAction action in actions)
                if (action != null)
                    action.performed -= OnPerformed;
            boundAsset = asset;
            actions = Array.Empty<InputAction>();
            if (asset == null || inputSettings == null)
                return;
            actions = new InputAction[inputSettings.Mode == PlayerToolInputMode.SharedCycle ? 1 : inputSettings.Bindings.Length];
            for (int index = 0; index < actions.Length; index++)
            {
                InputActionReference reference = inputSettings.Mode == PlayerToolInputMode.SharedCycle
                    ? inputSettings.UseTool : inputSettings.Bindings[index].Action;
                actions[index] = reference != null ? asset.FindAction(reference.action.id) : null;
                if (actions[index] != null)
                    actions[index].performed += OnPerformed;
            }
        }

        /// <summary>Routes one performed Button command to shared cycling or explicit selection.</summary>
        /// <param name="context">Immediate callback from the player's action instance.</param>
        private void OnPerformed(InputAction.CallbackContext context)
        {
            // Paused and inactive owners cannot queue commands for a later unpause.
            if (!ready || Time.timeScale <= 0f || input == null || !input.isActiveAndEnabled || !input.inputIsActive
                || input.actions != boundAsset)
                return;
            if (inputSettings.Mode == PlayerToolInputMode.SharedCycle)
                Cycle();
            else
                for (int index = 0; index < actions.Length; index++)
                    if (actions[index] == context.action)
                    {
                        Select(inputSettings.Bindings[index].Tool);
                        break;
                    }
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
                exits[active].Begin();
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
