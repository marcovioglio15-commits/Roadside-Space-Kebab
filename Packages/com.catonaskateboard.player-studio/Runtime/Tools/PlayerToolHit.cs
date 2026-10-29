using System;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Describes a real projectile contact using the tool and action captured at firing time.</summary>
    public readonly struct PlayerToolHit
    {
        #region Properties

        /// <summary>Player whose pool emitted this bullet.</summary>
        public PlayerTools Shooter { get; }
        /// <summary>Tool identity captured by this projectile, independent of later selection changes.</summary>
        public PlayerTool Tool { get; }
        /// <summary>Stable Use Tool action ID captured when the bullet was fired.</summary>
        public Guid ActionId { get; }
        /// <summary>Whether this shot routes matching interactions exclusively through its physical contact.</summary>
        public bool TriggersInteractions { get; }
        /// <summary>Physics contact identifying the exact struck collider and surface.</summary>
        public RaycastHit Contact { get; }
        /// <summary>World muzzle position at firing time.</summary>
        public Vector3 Origin { get; }
        /// <summary>Normalized world flight direction.</summary>
        public Vector3 Direction { get; }
        /// <summary>World flight speed captured when this projectile was fired.</summary>
        public float Speed { get; }
        /// <summary>Whether this contact came from an instant cursor request and retains normal interaction reach limits.</summary>
        public bool IsCursorUse { get; }
        /// <summary>Incoming world velocity used by interactions that derive an impulse from the shot.</summary>
        public Vector3 Velocity => Direction * Speed;

        #endregion

        #region Methods

        #region Construction

        /// <summary>Captures one successful contact before any interaction can modify the target hierarchy.</summary>
        /// <param name="shooter">Player owning the projectile pool.</param>
        /// <param name="tool">Tool identity that fired the bullet.</param>
        /// <param name="actionId">Use Tool action ID at firing time.</param>
        /// <param name="triggersInteractions">Impact-only interaction setting captured at firing.</param>
        /// <param name="contact">Actual swept collision result.</param>
        /// <param name="origin">Original world muzzle position.</param>
        /// <param name="direction">Normalized flight direction.</param>
        /// <param name="speed">Flight speed captured at emission, in metres per second.</param>
        /// <param name="isCursorUse">Whether the contact represents an instant cursor interaction.</param>
        internal PlayerToolHit(PlayerTools shooter, PlayerTool tool, Guid actionId, bool triggersInteractions,
            RaycastHit contact, Vector3 origin, Vector3 direction, float speed, bool isCursorUse = false)
        {
            // Value snapshots prevent a later tool switch from redirecting an in-flight interaction.
            Shooter = shooter;
            Tool = tool;
            ActionId = actionId;
            TriggersInteractions = triggersInteractions;
            Contact = contact;
            Origin = origin;
            Direction = direction;
            Speed = speed;
            IsCursorUse = isCursorUse;
        }

        #endregion

        #endregion
    }
}
