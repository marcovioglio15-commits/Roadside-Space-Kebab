using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Distinguishes a successful activation from its committed result.</summary>
    public enum InteractionMoment { Started, Completed }
    /// <summary>Chooses an existing interaction event or a dedicated player Button.</summary>
    public enum UnlockTrigger { Interaction, InputAction }

    /// <summary>Defines one explicit condition for unlocking an existing interaction.</summary>
    [Serializable]
    public sealed class InteractionUnlockCondition
    {
        #region Fields

        [Header("Condition")]
        [Tooltip("Unlock after an interaction event or a performed player Button action.")]
        public UnlockTrigger Trigger;
        [Tooltip("Existing interaction whose successful start or completion satisfies this condition.")]
        public ObjectInteraction Source;
        [Tooltip("Completed waits for the final committed result, including timed contact effects and dialogue pages.")]
        public InteractionMoment Moment = InteractionMoment.Completed;
        [Tooltip("Dedicated Button resolved in the observer player's private PlayerInput action asset.")]
        public InputActionReference Action;
        [Tooltip("Maximum player distance in metres for this unlock Button. Interaction-event conditions have no range requirement.")]
        public float Distance = 3f;

        #endregion
    }

    /// <summary>Locks one existing feature until its independent conditions are satisfied.</summary>
    [Serializable]
    public sealed class InteractionUnlockSettings
    {
        #region Fields

        [Header("Unlock")]
        [Tooltip("Existing interaction initially locked by this rule. Disabling the rule removes only its own lock.")]
        public ObjectInteraction Target;
        [Tooltip("Require every condition; otherwise any one condition unlocks the target. Conditions accumulate during this activation.")]
        public bool RequireAll = true;
        [Tooltip("Interaction events or dedicated player inputs that release this rule's lock.")]
        public InteractionUnlockCondition[] Conditions = Array.Empty<InteractionUnlockCondition>();

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks references, command types and distances without creating missing interactions.</summary>
        /// <param name="root">Prefab root containing this unlock rule.</param>
        /// <param name="warning">Receives the first invalid or missing condition.</param>
        /// <returns>True when the target and every condition are usable.</returns>
        public bool TryValidate(Transform root, out string warning)
        {
            // A prefab asset cannot link directly to another spawned prefab instance.
            warning = string.Empty;
            if (Target == null || Target is ObjectInteractionUnlock || Target.transform.root != root)
                warning = "Select an existing interaction in this prefab as the locked target.";
            else if (Conditions == null || Conditions.Length == 0)
                warning = "Add at least one unlock condition.";
            else
                foreach (InteractionUnlockCondition condition in Conditions)
                {
                    if (condition == null || condition.Trigger is not (UnlockTrigger.Interaction or UnlockTrigger.InputAction))
                        warning = "Choose a supported unlock condition.";
                    else
                        switch (condition.Trigger)
                        {
                            case UnlockTrigger.Interaction:
                                if (condition.Source == null || condition.Source == Target || condition.Source is ObjectInteractionUnlock
                                    || condition.Source.transform.root != root || condition.Moment is not (InteractionMoment.Started or InteractionMoment.Completed))
                                    warning = "Select another existing interaction in this prefab and a valid event.";
                                break;
                            case UnlockTrigger.InputAction:
                                if (condition.Action == null || condition.Action.action is not { type: InputActionType.Button }
                                    || !InteractionValues.Positive(condition.Distance))
                                    warning = "Assign an unlock Button action and a positive finite activation distance.";
                                break;
                        }
                    if (warning.Length > 0)
                        break;
                }
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
