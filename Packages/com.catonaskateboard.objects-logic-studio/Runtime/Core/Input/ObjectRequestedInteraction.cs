using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares observer input arbitration with features that request a button during a persistent process.</summary>
    public abstract class ObjectRequestedInteraction : ObjectExtendedInteraction
    {
        #region Fields

        [Header("Input")]
        [Tooltip("Optional Button action resolved in the Observer player's private input asset when this process requests input.")]
        [SerializeField]
        private InputActionReference action;
        private static readonly List<ObjectRequestedInteraction> registered = new List<ObjectRequestedInteraction>();

        #endregion
        #region Properties

        /// <summary>Authored button identifier; its action maps remain owned by PlayerInput.</summary>
        public InputActionReference Action => action;
        /// <summary>Whether this feature configuration includes an input request.</summary>
        public abstract bool UsesInput { get; }
        /// <summary>Range and aiming policy for the requested button.</summary>
        public abstract TransferTargetSettings Target { get; }
        /// <summary>Authored geometry cached at activation.</summary>
        internal Collider[] Colliders { get; private set; }
        /// <summary>Active process commands participating in the common observer.</summary>
        internal static IReadOnlyList<ObjectRequestedInteraction> Registered => registered;
        /// <summary>Changes only when the observer must rebuild its bindings.</summary>
        internal static int Revision { get; private set; }

        #endregion
        #region Methods
        #region Lifecycle

        /// <summary>Caches target geometry and registers once at activation.</summary>
        protected virtual void OnEnable()
        {
            if (!UsesInput)
                return;
            Colliders = GetComponentsInChildren<Collider>(true);
            if (!registered.Contains(this))
            {
                registered.Add(this);
                Revision++;
            }
        }

        /// <summary>Withdraws disabled processes from button arbitration.</summary>
        protected virtual void OnDisable()
        {
            if (registered.Remove(this))
                Revision++;
        }

        /// <summary>Rebuilds input membership for Play sessions with retained scene objects.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            registered.Clear();
            Revision++;
            foreach (ObjectRequestedInteraction interaction in FindObjectsByType<ObjectRequestedInteraction>())
                if (interaction.isActiveAndEnabled && interaction.UsesInput)
                {
                    interaction.Colliders = interaction.GetComponentsInChildren<Collider>(true);
                    registered.Add(interaction);
                }
        }

        #endregion
        #region Requests

        /// <summary>Checks whether this process is currently waiting for a player request.</summary>
        /// <returns>True when its bound button may compete with other commands.</returns>
        internal abstract bool CanRequest();

        /// <summary>Commits one request after the observer confirms reach and visibility.</summary>
        /// <returns>True when the input changes the running process.</returns>
        internal abstract bool Request();

        /// <summary>Validates a process's optional button without enabling project action maps.</summary>
        /// <param name="required">Whether the proposed configuration needs input.</param>
        /// <param name="reference">Proposed Button action.</param>
        /// <param name="warning">Receives a missing or incompatible input binding.</param>
        /// <returns>True when input is unused or its reference is a Button.</returns>
        public static bool ValidateInput(bool required, InputActionReference reference, out string warning)
        {
            warning = required && (reference == null || reference.action == null || reference.action.type != InputActionType.Button)
                ? "Assign a Button action for the enabled interaction request." : string.Empty;
            return warning.Length == 0;
        }

        #endregion
        #endregion
    }
}
