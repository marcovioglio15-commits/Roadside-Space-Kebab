using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares bounded sight queries across dialogues and avoids repeating them within one observer frame.</summary>
    internal sealed class DialogueVisibility
    {
        #region State

        private readonly HoverPhysics physics = new HoverPhysics();
        private readonly Dictionary<ObjectDialogue, bool> results = new Dictionary<ObjectDialogue, bool>();
        private HoverObserver observer;
        private int frame = -1;

        #endregion

        #region Methods

        #region Queries

        /// <summary>Checks the configured anchor while preserving the same obstruction rules as other object interactions.</summary>
        /// <param name="context">Observer supplying the camera, player and physics scene.</param>
        /// <param name="dialogue">Dialogue whose visibility is required.</param>
        /// <returns>True when external solid geometry does not block the sight segment.</returns>
        internal bool HasSight(HoverObserver context, ObjectDialogue dialogue)
        {
            // Callers skip this method when their particular visibility requirement is disabled.
            if (context == null || context.View == null || context.Player == null || dialogue == null)
                return false;
            if (frame != Time.frameCount || observer != context)
            {
                results.Clear();
                frame = Time.frameCount;
                observer = context;
                Physics.SyncTransforms();
            }
            if (!results.TryGetValue(dialogue, out bool visible))
            {
                visible = physics.HasSight(context, dialogue.transform,
                    dialogue.transform.TransformPoint(dialogue.Settings.SightOffset), dialogue.Settings.ObstacleMask);
                results.Add(dialogue, visible);
            }
            return visible;
        }

        /// <summary>Discards cached references when observer ownership or Play context changes.</summary>
        internal void Reset()
        {
            // A new observer never inherits the previous camera's same-frame visibility result.
            results.Clear();
            observer = null;
            frame = -1;
        }

        #endregion

        #endregion
    }
}
