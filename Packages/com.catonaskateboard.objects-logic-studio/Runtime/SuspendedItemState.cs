using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Captures interaction and physics state for temporary inventory or spawn-animation suspension.</summary>
    internal sealed class SuspendedItemState
    {
        #region State

        private readonly ObjectInteraction[] interactions;
        private readonly bool[] interactionStates;
        private readonly bool[][] unlockProgress;
        private readonly bool[] applied;
        private readonly ObjectDialogue.StoredProgress?[] dialogueProgress;
        private readonly ObjectItem[] items;
        private readonly bool[] itemStates;
        private readonly Rigidbody[] bodies;
        private readonly CarryBodyState[] bodyStates;
        private readonly Collider[] colliders;
        private readonly bool[] colliderStates;

        #endregion

        #region Properties

        /// <summary>Object whose component policy is restored after temporary suspension.</summary>
        internal GameObject Root { get; }

        #endregion

        #region Methods

        #region Snapshot

        /// <summary>Captures an item after its carry overrides have been released.</summary>
        /// <param name="root">Item root, including any assembled ingredient hierarchy.</param>
        internal SuspendedItemState(GameObject root)
        {
            // Allocation occurs once per deposit, never while a stored item waits.
            Root = root;
            interactions = root.GetComponentsInChildren<ObjectInteraction>(true);
            items = root.GetComponentsInChildren<ObjectItem>(true);
            bodies = root.GetComponentsInChildren<Rigidbody>(true);
            colliders = root.GetComponentsInChildren<Collider>(true);
            interactionStates = new bool[interactions.Length];
            unlockProgress = new bool[interactions.Length][];
            applied = new bool[interactions.Length];
            dialogueProgress = new ObjectDialogue.StoredProgress?[interactions.Length];
            itemStates = new bool[items.Length];
            bodyStates = new CarryBodyState[bodies.Length];
            colliderStates = new bool[colliders.Length];
            for (int index = 0; index < interactions.Length; index++)
            {
                interactionStates[index] = interactions[index].enabled;
                if (interactions[index] is ObjectDialogue dialogue)
                    dialogueProgress[index] = dialogue.CaptureStoredProgress();
                if (interactions[index] is ObjectInteractionUnlock rule)
                {
                    unlockProgress[index] = rule.CaptureStoredProgress();
                    applied[index] = rule.IsApplied;
                }
            }
            for (int index = 0; index < items.Length; index++)
                itemStates[index] = items[index].enabled;
            for (int index = 0; index < bodies.Length; index++)
                bodyStates[index] = new CarryBodyState(bodies[index]);
            for (int index = 0; index < colliders.Length; index++)
                colliderStates[index] = colliders[index].enabled;
        }

        #endregion

        #region Transfer

        /// <summary>Deactivates the root and suspends its captured interactions, items, colliders and bodies.</summary>
        internal void Suspend()
        {
            // Disable collision and logic even for visible items, so storage cannot trigger contact consumption.
            Root.SetActive(false);
            foreach (ObjectInteraction interaction in interactions)
                if (interaction != null)
                    interaction.enabled = false;
            foreach (ObjectItem item in items)
                if (item != null)
                    item.enabled = false;
            foreach (Collider collider in colliders)
                if (collider != null)
                    collider.enabled = false;
            foreach (Rigidbody body in bodies)
                if (body != null)
                {
                    body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                    body.isKinematic = true;
                    body.detectCollisions = false;
                    body.useGravity = false;
                    body.interpolation = RigidbodyInterpolation.None;
                }
        }

        /// <summary>Restores original component states and retained interaction progress before normal updates resume.</summary>
        internal void Restore()
        {
            // Restore while inactive so activation callbacks observe a complete physical hierarchy.
            Root.SetActive(false);
            for (int index = 0; index < bodies.Length; index++)
                if (bodies[index] != null)
                    bodyStates[index].Restore(bodies[index], false);
            for (int index = 0; index < colliders.Length; index++)
                if (colliders[index] != null)
                    colliders[index].enabled = colliderStates[index];
            for (int index = 0; index < items.Length; index++)
                if (items[index] != null)
                    items[index].enabled = itemStates[index];
            for (int index = 0; index < interactions.Length; index++)
                if (interactions[index] != null)
                    interactions[index].enabled = interactionStates[index];
            Root.SetActive(true);
            for (int index = 0; index < interactions.Length; index++)
                switch (interactions[index])
                {
                    case ObjectInteractionUnlock rule:
                        rule.RestoreStoredProgress(unlockProgress[index], applied[index]);
                        break;
                    case ObjectDialogue dialogue when dialogueProgress[index].HasValue:
                        dialogue.RestoreStoredProgress(dialogueProgress[index].Value);
                        break;
                }
        }

        #endregion

        #endregion
    }
}
