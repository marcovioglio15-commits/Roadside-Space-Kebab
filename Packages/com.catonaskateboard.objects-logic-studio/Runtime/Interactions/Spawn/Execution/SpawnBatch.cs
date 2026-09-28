namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Commits an animated spawn cycle only after every generated object reaches its final pose.</summary>
    internal sealed class SpawnBatch
    {
        #region State

        private readonly ObjectSpawnManager owner;
        private int remaining;
        private bool cancelled;

        #endregion

        #region Methods

        #region Completion

        /// <summary>Starts a completion group for one generated prefab draw.</summary>
        /// <param name="owner">Rule receiving the final completion.</param>
        /// <param name="count">Number of objects expected to finish their arrival.</param>
        internal SpawnBatch(ObjectSpawnManager owner, int count)
        {
            // Different source instances can own simultaneous, independent animation batches.
            this.owner = owner;
            remaining = count;
        }

        /// <summary>Records one arrival or cancellation without reporting partial success as complete.</summary>
        /// <param name="completed">Whether the object reached its final pose before destruction.</param>
        internal void Finish(bool completed)
        {
            // Destroying an unfinished object releases bookkeeping but never satisfies completion-based unlocks.
            cancelled |= !completed;
            remaining--;
            if (remaining == 0 && owner != null)
                owner.CompleteAnimation(!cancelled);
        }

        #endregion

        #endregion
    }
}
