using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains per-interaction dialogue progress while the observer arbitrates a single visible HUD.</summary>
    [AddComponentMenu("Objects Logic Studio/Dialogue")]
    public sealed class ObjectDialogue : ObjectExtendedInteraction
    {
        #region Serialized Fields

        [Header("Dialogue")]
        [Tooltip("Range, trigger, priority, entry selection and interruption behavior.")]
        [SerializeField]
        private DialogueSettings settings = new DialogueSettings();
        [Header("Bindings")]
        [Tooltip("Button used to request dialogue when Trigger is Input Action. Resolved in the player's private PlayerInput asset.")]
        [SerializeField]
        private InputActionReference startAction;
        [Tooltip("Button used to display the next explicit dialogue page and close after the final page.")]
        [SerializeField]
        private InputActionReference advanceAction;
        [Tooltip("Existing prefab-local Dialogue HUD authored before Play. Several dialogue components may share one HUD.")]
        [SerializeField]
        private DialogueHud hud;

        #endregion

        #region State

        private readonly List<int> eligible = new List<int>();
        private int consumptionRevision = -1;
        private int startedRevision = -1;
        private int entry = -1;
        private int line;
        private int nextEntry;
        private bool armed = true;
        private bool retained;

        #endregion

        #region Properties

        /// <summary>Saved trigger and dialogue content.</summary>
        public DialogueSettings Settings => settings;
        /// <summary>Imported action used by input-triggered activation.</summary>
        public InputActionReference StartAction => startAction;
        /// <summary>Imported action used for page advancement.</summary>
        public InputActionReference AdvanceAction => advanceAction;
        /// <summary>Existing overlay assigned by the prefab authoring tool.</summary>
        public DialogueHud Hud => hud;
        /// <summary>Whether this interaction currently owns the visible dialogue.</summary>
        public bool IsSpeaking { get; private set; }
        /// <summary>Whether the latest activation validation passed.</summary>
        internal bool Ready { get; private set; }
        /// <summary>Identifies this multiple-interaction feature.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.Dialogue;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Initializes local progress and registers an independently prioritized dialogue component.</summary>
        private void OnEnable()
        {
            // Multiple components on one item retain separate entry cursors and interrupted pages.
            Initialize();
            DialogueRegistry.Register(this);
        }

        /// <summary>Hides presentation and invalidates observer input bindings when this feature disappears.</summary>
        private void OnDisable()
        {
            // Consuming the item cannot leave its HUD visible or keep its advance action subscribed.
            Interrupt();
            DialogueRegistry.Unregister(this);
        }

        /// <summary>Resets runtime progress at activation and Play entry without changing saved settings.</summary>
        internal void Initialize()
        {
            // Explicit initialization also covers projects with disabled domain and scene reload.
            if (IsSpeaking && hud != null)
                hud.Hide();
            IsSpeaking = false;
            retained = false;
            armed = true;
            entry = consumptionRevision = startedRevision = -1;
            line = nextEntry = 0;
            eligible.Clear();
            Ready = TryValidate(out string warning);
            if (!Ready)
                Debug.LogWarning(warning, this);
        }

        #endregion

        #region Validation

        /// <summary>Checks pages, project tags, input references and the existing local HUD.</summary>
        /// <param name="warning">Receives the first missing dependency.</param>
        /// <returns>True when the observer can run this dialogue without creating runtime UI.</returns>
        public override bool TryValidate(out string warning)
        {
            // Runtime and editor drafts use identical dependency and tag checks.
            return TryValidate(settings, startAction, advanceAction, out warning);
        }

        /// <summary>Checks a detached dialogue proposal against the existing prefab-local HUD.</summary>
        /// <param name="configuration">Proposed range, conditions and dialogue pages.</param>
        /// <param name="start">Proposed optional activation Button.</param>
        /// <param name="advance">Proposed page-advance Button.</param>
        /// <param name="warning">Receives missing input, HUD or tag dependencies.</param>
        /// <returns>True when the proposal can run on this object.</returns>
        public bool TryValidate(DialogueSettings configuration, InputActionReference start, InputActionReference advance, out string warning)
        {
            // Input references identify actions; the observer binds their private runtime counterparts.
            warning = "Dialogue requires an Object Item and complete settings.";
            if (GetComponent<ObjectItem>() == null || configuration == null || !configuration.TryValidate(out warning))
                return false;
            if (advance == null || advance.action is not { type: InputActionType.Button }
                || configuration.Trigger == DialogueTrigger.InputAction && (start == null || start.action is not { type: InputActionType.Button }))
                warning = "Assign an advance Button action and, for input activation, a start Button action.";
            else if (hud == null || !hud.IsValid(transform))
                warning = "Create or assign a complete Dialogue HUD inside this object's prefab hierarchy.";
            else
                try
                {
                    foreach (DialogueEntry dialogue in configuration.Entries)
                        foreach (ItemTagRequirement requirement in dialogue.RequiredTags)
                            gameObject.CompareTag(requirement.Tag);
                }
                catch (UnityException)
                {
                    warning = "A dialogue consumption tag is not defined in this project.";
                }
            return warning.Length == 0;
        }

        #endregion

        #region Eligibility

        /// <summary>Updates range rearming and consumption eligibility without drawing random entries.</summary>
        /// <param name="player">Observed player root.</param>
        /// <returns>True when this component may request the idle dialogue channel.</returns>
        internal bool CanStart(Transform player)
        {
            // Conditions are recomputed only when receipts change, while distance remains responsive each frame.
            if (!Ready || !Available(InteractionChannels.Dialogue) || Item == null || hud == null || !hud.isActiveAndEnabled)
                return false;
            float distance = (transform.position - player.position).sqrMagnitude;
            if (settings.ReplayOnReturn && distance > settings.ExitDistance * settings.ExitDistance)
                armed = true;
            RefreshEntries();
            return !IsSpeaking && distance <= settings.Distance * settings.Distance && eligible.Count > 0
                && (armed || settings.Trigger == DialogueTrigger.Consumption && startedRevision != Item.ConsumptionRevision);
        }

        /// <summary>Checks whether a visible dialogue still has its item, HUD and exit range.</summary>
        /// <param name="player">Observed player root.</param>
        /// <returns>True while presentation may continue.</returns>
        internal bool CanContinue(Transform player)
        {
            // No controller, cursor or movement state is changed when range is lost.
            return Available(InteractionChannels.Dialogue) && hud != null && hud.isActiveAndEnabled
                && (transform.position - player.position).sqrMagnitude <= settings.ExitDistance * settings.ExitDistance;
        }

        /// <summary>Rebuilds the compact entry catalog only after consumption history changes.</summary>
        private void RefreshEntries()
        {
            // Zero-weight entries are excluded only in weighted mode.
            if (consumptionRevision == Item.ConsumptionRevision)
                return;
            consumptionRevision = Item.ConsumptionRevision;
            eligible.Clear();
            for (int index = 0; index < settings.Entries.Length; index++)
                if (settings.Entries[index].Matches(Item)
                    && (settings.Selection != DialogueSelection.WeightedRandom || settings.Entries[index].Weight > 0f))
                    eligible.Add(index);
        }

        #endregion

        #region Flow

        /// <summary>Resumes retained progress or selects an eligible entry when this component wins arbitration.</summary>
        internal void Begin()
        {
            // Random selection occurs once per new dialogue, never during eligibility polling.
            RefreshEntries();
            if (!retained || !eligible.Contains(entry))
            {
                entry = ChooseEntry();
                line = 0;
            }
            if (entry < 0)
                return;
            bool resumed = retained;
            retained = false;
            armed = false;
            startedRevision = Item.ConsumptionRevision;
            IsSpeaking = true;
            hud.Show(settings.Entries[entry].Lines[line]);
            if (!resumed)
                Signal(InteractionMoment.Started);
        }

        /// <summary>Selects the next eligible entry according to the configured ordering policy.</summary>
        /// <returns>An eligible array index, or -1 when none match.</returns>
        private int ChooseEntry()
        {
            // Sequence skips unmet conditions while retaining the authored ordering of eligible entries.
            if (eligible.Count == 0)
                return -1;
            switch (settings.Selection)
            {
                case DialogueSelection.Sequence:
                    for (int offset = 0; offset < settings.Entries.Length; offset++)
                    {
                        int candidate = (nextEntry + offset) % settings.Entries.Length;
                        if (!eligible.Contains(candidate))
                            continue;
                        nextEntry = (candidate + 1) % settings.Entries.Length;
                        return candidate;
                    }
                    return -1;
                case DialogueSelection.Random:
                    return eligible[Random.Range(0, eligible.Count)];
                case DialogueSelection.WeightedRandom:
                    double total = 0d;
                    foreach (int candidate in eligible)
                        total += settings.Entries[candidate].Weight;
                    double choice = Random.value * total;
                    foreach (int candidate in eligible)
                    {
                        choice -= settings.Entries[candidate].Weight;
                        if (choice < 0d)
                            return candidate;
                    }
                    return eligible[eligible.Count - 1];
                default:
                    return -1;
            }
        }

        /// <summary>Displays exactly one next page or finishes after the final authored page.</summary>
        internal void Advance()
        {
            // A completed interaction waits for rearming so lower-priority eligible dialogues can run.
            if (!IsSpeaking)
                return;
            line++;
            if (line < settings.Entries[entry].Lines.Length)
                hud.Show(settings.Entries[entry].Lines[line]);
            else
            {
                hud.Hide();
                IsSpeaking = false;
                retained = false;
                entry = -1;
                line = 0;
                Signal(InteractionMoment.Completed);
            }
        }

        /// <summary>Hides the HUD and records the configured behavior for the next eligible return.</summary>
        internal void Interrupt()
        {
            // Preserve a page only when this feature actually owned the visible dialogue.
            if (!IsSpeaking)
                return;
            if (hud != null)
                hud.Hide();
            IsSpeaking = false;
            armed = true;
            retained = settings.Interruption != DialogueInterruption.SelectNext;
            if (settings.Interruption != DialogueInterruption.Resume)
                line = 0;
            if (!retained)
                entry = -1;
        }

        #endregion

        #endregion
    }
}
