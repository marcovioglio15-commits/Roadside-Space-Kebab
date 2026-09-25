using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Distinguishes a successful activation from its committed result.</summary>
    public enum InteractionMoment { Started, Completed }
    /// <summary>Chooses an existing interaction event or a dedicated player Button.</summary>
    public enum UnlockTrigger { Interaction, InputAction }
    /// <summary>Chooses whether conditions release an initial lock, add a lock, or replace one feature with another.</summary>
    public enum InteractionAvailabilityChange { Unlock, Lock, Replace }

    /// <summary>Defines one condition for changing the availability of existing interactions.</summary>
    [Serializable]
    public sealed class InteractionUnlockCondition
    {
        #region Fields

        [Header("Condition")]
        [Tooltip("Apply the operation after an interaction event or a performed player Button action.")]
        public UnlockTrigger Trigger;
        [Tooltip("Existing interaction whose successful start or completion satisfies this condition.")]
        public ObjectInteraction Source;
        [Tooltip("Completed waits for the final committed result, including timed contact effects and dialogue pages.")]
        public InteractionMoment Moment = InteractionMoment.Completed;
        [Tooltip("Dedicated Button resolved in the observer player's private PlayerInput action asset.")]
        public InputActionReference Action;
        [Tooltip("Maximum player distance in metres for this condition Button. Interaction-event conditions have no range requirement.")]
        public float Distance = 3f;

        #endregion
    }

    /// <summary>Changes existing feature availability after independent event or input conditions.</summary>
    [Serializable]
    public sealed class InteractionUnlockSettings
    {
        #region Fields

        [Header("Availability")]
        [Tooltip("Unlock removes this rule's initial lock; Lock blocks the target after conditions; Replace exchanges two features of the same type.")]
        public InteractionAvailabilityChange Operation;
        [Tooltip("Existing interaction to unlock, lock, or replace. Disabling this rule removes only the locks it owns.")]
        public ObjectInteraction Target;
        [Tooltip("Existing incoming interaction of the same type. This rule locks it initially and releases it when replacing the target.")]
        public ObjectInteraction Replacement;
        [Tooltip("Require every condition; otherwise any one condition applies the operation. Progress accumulates during this activation.")]
        public bool RequireAll = true;
        [Tooltip("Interaction events or dedicated player inputs that apply this rule once per activation.")]
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
            if (Operation is not (InteractionAvailabilityChange.Unlock or InteractionAvailabilityChange.Lock or InteractionAvailabilityChange.Replace))
                warning = "Choose Unlock, Lock or Replace.";
            else if (!IsLocal(Target, root))
                warning = "Select an existing interaction in this prefab as the target.";
            else if (Operation == InteractionAvailabilityChange.Replace
                && (!IsLocal(Replacement, root) || Replacement == Target || !IsSameKind(Target, Replacement)))
                warning = "Select a different existing interaction of the same type in this prefab as the replacement.";
            else if (Conditions == null || Conditions.Length == 0)
                warning = "Add at least one availability condition.";
            else
                foreach (InteractionUnlockCondition condition in Conditions)
                {
                    if (condition == null || condition.Trigger is not (UnlockTrigger.Interaction or UnlockTrigger.InputAction))
                        warning = "Choose a supported condition.";
                    else
                        switch (condition.Trigger)
                        {
                            case UnlockTrigger.Interaction:
                                if (!IsLocal(condition.Source, root)
                                    || Operation == InteractionAvailabilityChange.Unlock && condition.Source == Target
                                    || Operation == InteractionAvailabilityChange.Replace && condition.Source == Replacement
                                    || condition.Moment is not (InteractionMoment.Started or InteractionMoment.Completed))
                                    warning = "Choose a valid event from an existing interaction that this rule does not initially lock.";
                                break;
                            case UnlockTrigger.InputAction:
                                if (condition.Action == null || condition.Action.action is not { type: InputActionType.Button }
                                    || !InteractionValues.Positive(condition.Distance))
                                    warning = "Assign a Button action and a positive finite activation distance.";
                                break;
                        }
                    if (warning.Length > 0)
                        break;
                }
            return warning.Length == 0;
        }

        /// <summary>Checks prefab ownership before a rule acquires any target lock.</summary>
        /// <param name="feature">Existing target or condition source.</param>
        /// <param name="root">Root of the prefab containing the rule.</param>
        /// <returns>True for an ordinary interaction belonging to this prefab instance.</returns>
        internal static bool IsLocal(ObjectInteraction feature, Transform root)
        {
            // Invalid references must never mutate another prefab or a rule's own availability.
            return feature != null && feature is not ObjectInteractionUnlock && feature.transform.root == root;
        }

        /// <summary>Matches supported interaction kinds without runtime reflection or name comparisons.</summary>
        /// <param name="first">Outgoing interaction selected in the rule.</param>
        /// <param name="second">Proposed incoming interaction.</param>
        /// <returns>True when both components implement the same interaction type.</returns>
        public static bool IsSameKind(ObjectInteraction first, ObjectInteraction second)
        {
            // Shared component families still distinguish Grab from Drop and Dialogue from Contact.
            return first switch
            {
                ObjectHover => second is ObjectHover,
                ObjectSingleInteraction single => second is ObjectSingleInteraction other && single.Kind == other.Kind,
                ObjectExtendedInteraction extended when extended is not ObjectInteractionUnlock =>
                    second is ObjectExtendedInteraction other && extended.Kind == other.Kind,
                _ => false
            };
        }

        #endregion

        #endregion
    }
}
