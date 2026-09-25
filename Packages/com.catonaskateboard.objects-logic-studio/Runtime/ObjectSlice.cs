using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Advances one finite slicing sequence while retaining progress through storage and reactivation.</summary>
    [AddComponentMenu("Objects Logic Studio/Object Slice")]
    public sealed class ObjectSlice : ObjectExtendedInteraction
    {
        #region Serialized Fields

        [Header("Slice")]
        [Tooltip("Targeting and ordered mesh, material and prefab changes for this sequence.")]
        [SerializeField]
        private SliceSettings settings = new SliceSettings();
        [Header("Input")]
        [Tooltip("Button advancing exactly one step per performed press, resolved in the observer player's private input asset.")]
        [SerializeField]
        private InputActionReference action;

        #endregion

        #region State

        private float nextPress;
        private bool executing;
        private int currentSession = -1;
        private static int session;

        #endregion

        #region Properties

        /// <summary>Saved targeting and step configuration.</summary>
        public SliceSettings Settings => settings;
        /// <summary>Input action used to perform the next cut.</summary>
        public InputActionReference Action => action;
        /// <summary>Number of steps successfully committed by this instance.</summary>
        public int CompletedSteps { get; private set; }
        /// <summary>Whether the final step has already committed.</summary>
        public bool IsComplete => settings != null && settings.Steps != null && CompletedSteps >= settings.Steps.Length;
        /// <summary>Identifies Slice in the Multiple category.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.Slice;
        /// <summary>Cached shapes used by visible-surface targeting.</summary>
        internal Collider[] Colliders { get; private set; }

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Invalidates instance progress once per Play entry even when domain reload is disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            // Ordinary component or inventory activation keeps its sequence cursor.
            session++;
        }

        /// <summary>Registers this sequence without replaying already committed appearance changes.</summary>
        private void OnEnable()
        {
            // Refresh geometry at an ownership boundary, including retrieval from visible storage.
            Initialize();
            SliceRegistry.Register(this);
        }

        /// <summary>Removes input eligibility while preserving the instance's completed steps.</summary>
        private void OnDisable()
        {
            // No HUD or controller state belongs to a slice sequence.
            SliceRegistry.Unregister(this);
        }

        /// <summary>Refreshes cached geometry and resets progress only on a new Play session.</summary>
        internal void Initialize()
        {
            // Session recovery also handles enabled components retained without scene reload.
            Colliders = GetComponentsInChildren<Collider>(true);
            if (currentSession == session)
                return;
            currentSession = session;
            CompletedSteps = 0;
            nextPress = 0f;
            executing = false;
        }

        #endregion

        #region Validation

        /// <summary>Checks the applied sequence and existing component bindings before input registration.</summary>
        /// <param name="warning">Receives missing input, invalid settings or missing geometry.</param>
        /// <returns>True when every step can bind to this item.</returns>
        public override bool TryValidate(out string warning)
        {
            // Draft validation uses this same path before writing the prefab.
            return TryValidate(settings, action, out warning);
        }

        /// <summary>Checks a detached proposal without altering this item or shared assets.</summary>
        /// <param name="configuration">Proposed sequence and targeting.</param>
        /// <param name="button">Proposed player Button action.</param>
        /// <param name="warning">Receives the first unusable dependency.</param>
        /// <returns>True when the complete proposal can run.</returns>
        public bool TryValidate(SliceSettings configuration, InputActionReference button, out string warning)
        {
            // A prefab preset validates data separately; the item supplies real renderer and collider paths.
            warning = "Slice needs settings and an Object Item.";
            if (configuration == null || Item == null || !configuration.TryValidate(out warning))
                return false;
            if (button == null || button.action is not { type: InputActionType.Button })
            {
                warning = "Assign a Button action to advance the Slice sequence.";
                return false;
            }
            foreach (SliceStep step in configuration.Steps)
                if (!SliceStepRun.TryPrepare(Item, step, out _, out warning))
                    return false;
            return true;
        }

        #endregion

        #region Sequence

        /// <summary>Checks availability without consuming input or changing sequence progress.</summary>
        /// <returns>True when another step may be performed now.</returns>
        internal bool CanAdvance()
        {
            // Contact transactions retain appearance ownership until they finish or release their reservation.
            return !executing && !IsComplete && Available(InteractionChannels.Slice)
                && !Item.IsReserved && Time.time >= nextPress;
        }

        /// <summary>Commits one cut, publishing sequence start and completion at their respective boundaries.</summary>
        /// <returns>True when this press performed one complete step.</returns>
        internal bool Advance()
        {
            // Failed dependency checks leave progress, visuals and prefab output unchanged.
            if (!CanAdvance())
                return false;
            if (!SliceStepRun.TryPrepare(Item, settings.Steps[CompletedSteps], out SliceStepRun run, out string warning))
            {
                Debug.LogWarning(warning, this);
                return false;
            }
            executing = true;
            try
            {
                run.Commit(transform);
                CompletedSteps++;
                nextPress = Time.time + settings.Interval;
                if (CompletedSteps == 1)
                    Signal(InteractionMoment.Started);
                if (IsComplete)
                    Signal(InteractionMoment.Completed);
                return true;
            }
            finally
            {
                executing = false;
            }
        }

        #endregion

        #endregion
    }
}
