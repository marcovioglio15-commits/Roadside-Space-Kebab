using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Stores a detached proposal for one Grab, Drop or Throw card.</summary>
    [Serializable]
    internal sealed class SingleInteractionDraft
    {
        #region Fields

        [Header("Binding")]
        [Tooltip("Name shown on this feature's card.")]
        public string Name = string.Empty;
        [Tooltip("Allow this feature to participate in runtime interaction selection.")]
        public bool Enabled = true;
        [Tooltip("Button action from the player's Input Actions asset. The action's map is managed by PlayerInput.")]
        public InputActionReference Action;
        [Tooltip("Show grab targeting and throw trajectory guides when this object is selected.")]
        public bool DrawGizmos = true;
        [Header("Configuration")]
        [Tooltip("Grab targeting, carry pose and collision settings.")]
        public GrabSettings Grab = new GrabSettings();
        [Tooltip("Body and surface response for the selected Drop or Throw.")]
        public ReleaseSettings Release = new ReleaseSettings();
        [Tooltip("Launch strength, direction and spin for Throw.")]
        public ThrowSettings Throw = new ThrowSettings();

        #endregion

        #region Methods

        #region Snapshot

        /// <summary>Copies the applied feature into a proposal without retaining mutable configuration objects.</summary>
        /// <param name="feature">Selected component, or null for an empty card selection.</param>
        /// <returns>A detached snapshot with native asset references retained.</returns>
        internal static SingleInteractionDraft Capture(ObjectSingleInteraction feature)
        {
            // The unused settings remain defaults so each card stores only its own meaningful proposal.
            SingleInteractionDraft draft = new SingleInteractionDraft();
            if (feature == null)
                return draft;
            draft.Name = feature.InteractionName;
            draft.Enabled = feature.enabled;
            draft.Action = feature.Action;
            if (feature is ObjectGrab grab)
            {
                draft.Grab = ObjectWorkspace.Copy(grab.Settings);
                draft.DrawGizmos = grab.DrawGizmos;
            }
            if (feature is ObjectRelease release)
                draft.Release = ObjectWorkspace.Copy(release.PhysicsSettings);
            if (feature is ObjectThrow launch)
                draft.Throw = ObjectWorkspace.Copy(launch.Trajectory);
            return draft;
        }

        #endregion

        #region Validation

        /// <summary>Validates the proposal against the existing object without editing its components.</summary>
        /// <param name="feature">Applied component that will receive this proposal.</param>
        /// <param name="warning">Receives the first dependency, input or settings conflict.</param>
        /// <returns>True when Apply can commit this feature safely.</returns>
        internal bool TryValidate(ObjectSingleInteraction feature, out string warning)
        {
            // Disabling Grab while release features remain enabled would silently disable their purpose.
            warning = string.Empty;
            if (!Enabled)
            {
                if (feature is ObjectGrab)
                    foreach (ObjectRelease release in feature.GetComponents<ObjectRelease>())
                        if (release.enabled)
                        {
                            warning = "Disable Drop and Throw before disabling their Grab.";
                            return false;
                        }
                return true;
            }
            if (Action == null || Action.action == null || Action.action.type != InputActionType.Button)
                warning = "Choose a Button action from the player's Input Actions asset.";
            else if (feature is ObjectGrab)
                return Grab != null && Grab.TryValidate(out warning) && ObjectGrab.ValidateBody(feature.gameObject, out warning);
            else if (feature.GetComponent<ObjectGrab>() is not ObjectGrab owner || !owner.enabled)
                warning = "Drop and Throw require an enabled Grab on the same object.";
            else if (Release == null || !Release.TryValidate(out warning))
                return false;
            else if (feature is ObjectThrow && (Throw == null || !Throw.TryValidate(out warning)))
                return false;
            else
                foreach (ObjectRelease other in feature.GetComponents<ObjectRelease>())
                    if (other != feature && other.enabled && other.Action != null && other.Action.action != null
                        && other.Action.asset == Action.asset && other.Action.action.id == Action.action.id)
                    {
                        warning = "Assign different actions to Drop and Throw on the same object.";
                        break;
                    }
            return warning.Length == 0;
        }

        #endregion

        #endregion
    }
}
